using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Bookings;

public interface IBookingDomainService
{
    public Booking BookCourt(BookingDomainService.Context context, DateTimeOffsetInterval offsetInterval, string timeZoneInfoId, string? comment);
    public void EditBooking(BookingDomainService.Context context, Booking existingBooking, DateTimeOffsetInterval offsetInterval, string timeZoneInfoId, string? comment);
}

public class BookingDomainService : IBookingDomainService
{
    private const int SpontaneousBookingInAdvance = 30;

    public enum ErrorCode
    {
        InvalidAmountOfPlayers = 0,
        CourtInactive = 1,
        CourtOccupied = 2,
        InvalidTime = 3,
        OutsideClubOpeningHours = 4,
        BookingInThePast = 5,
        MaximumReservationsReached = 6,
        PlayerIsNotAllwedToPlay = 7,
        ChildMembersAreNotAllowedToPlayWithinPrimeTime = 8,
        PlayerHasActiveBookingAtBookedInterval = 9,
        CannotBookInAdvanceTwice = 10,
        CannotBookWithGuestMembers = 11,
        CannotBookInAdvanceAsGuest = 12,
        CannotBookWithinPrimeTimeAsGuest = 13,
        NoActiveSeasonForClub = 14,
        MemberNotAllowedInSeason = 15,
        MaxBookingsPerSeasonReached = 16,
        CourtBlocked = 17,
    }

    public Booking BookCourt(Context context, DateTimeOffsetInterval offsetInterval, string timeZoneInfoId, string? comment)
    {
        ValidateBooking(context, offsetInterval, comment);
        return new Booking(context.ClubId, context.Court.Id, context.PlayMode.Id, offsetInterval, timeZoneInfoId, context.Participants.ConvertAll(p => p.Member.Id), comment);
    }

    public void EditBooking(Context context, Booking existingBooking, DateTimeOffsetInterval offsetInterval, string timeZoneInfoId, string? comment)
    {
        ValidateBooking(context, offsetInterval, comment);
        existingBooking.Update(context.Court.Id, context.PlayMode.Id, offsetInterval, timeZoneInfoId, context.Participants.ConvertAll(p => p.Member.Id), comment);
    }

    private static void ValidateBooking(Context context, DateTimeOffsetInterval offsetInterval, string? comment)
    {
        var spontaneousBookingInterval = new DateTimeOffsetInterval(DateTimeOffset.UtcNow.AddMinutes((-context.BookingGracePeriodInMinutes) ?? 0), DateTimeOffset.UtcNow.AddMinutes(SpontaneousBookingInAdvance));
        bool isSpontaneousBooking = spontaneousBookingInterval.Contains(offsetInterval.From);

        if (!context.SkipPastBookingValidation && offsetInterval.From < DateTimeOffset.UtcNow.AddMinutes((-context.BookingGracePeriodInMinutes) ?? 0))
            throw new PreconditionException(ErrorCode.BookingInThePast, "Cannot book in the past");

        if (offsetInterval.From >= offsetInterval.To)
            throw new PreconditionException(ErrorCode.InvalidTime, "Cannot play this amount of time");

        if (TimeOnly.FromDateTime(offsetInterval.From.UtcDateTime) < context.OpeningHours.From || TimeOnly.FromDateTime(offsetInterval.To.UtcDateTime) > context.OpeningHours.To)
        {
            throw new PreconditionException(ErrorCode.OutsideClubOpeningHours,
                [$"{context.OpeningHours.From.AddHours(context.ClientTimeZoneOffset ?? 0)}", $"{context.OpeningHours.To.AddHours(context.ClientTimeZoneOffset ?? 0)}"],
                $"Cannot play outside the club opening hours from {context.OpeningHours.From} to {context.OpeningHours.To} (UTC)");
        }

        var isWithinPrimeTime = context.PrimeTimeSettings.IsPrimeTimeActiveOn(offsetInterval.From.DayOfWeek)
            && offsetInterval.TryToTimeOnly(out var timeOnlybookingInterval)
            && context.PrimeTimeSettings.PrimeTimeHours.Intersects(timeOnlybookingInterval);

        if (isWithinPrimeTime
            && context.PrimeTimeSettings.RestrictChildren
            && context.Participants.All(i => i.IsChild) && context.Participants.Count != 0)
        {
            throw new PreconditionException(ErrorCode.ChildMembersAreNotAllowedToPlayWithinPrimeTime, "If only children play they cannot play during prime time");
        }

        if (context.Participants.Select(x => x.Member.MemberType).Distinct().Count() > 1)
        {
            throw new PreconditionException(ErrorCode.CannotBookWithGuestMembers, "Club members cannot book with guests");
        }

        if (context.Participants.Any(p => p.Member.MemberType == MemberType.ClubMember) && context.ActiveSeasonId is null)
            throw new PreconditionException(ErrorCode.NoActiveSeasonForClub, "No active season for the club");

        foreach (var participant in context.Participants)
        {
            if (participant.Member.MemberType == MemberType.GuestMember && participant.UpcomingBookingsCount > 0)
                throw new PreconditionException(ErrorCode.CannotBookInAdvanceAsGuest, "Guests cannot book in advance twice");

            if (participant.Member.MemberType == MemberType.GuestMember && isWithinPrimeTime && context.PrimeTimeSettings.RestrictGuests)
                throw new PreconditionException(ErrorCode.CannotBookWithinPrimeTimeAsGuest, "Guests cannot book during prime time hours");

            if (!participant.Member.CanParticipate && !participant.IsAtpParticipant && !participant.IsAllowedInCurrentSeason)
                throw new PreconditionException(ErrorCode.PlayerIsNotAllwedToPlay, [participant.FullName], $"User {participant.FullName} is not allowed to play");

            if (participant.ActiveBookingsAtTimeOfReservation > 0 && context.PlayMode.IsChargingBookingSubscription)
                throw new PreconditionException(ErrorCode.PlayerHasActiveBookingAtBookedInterval, [participant.FullName], $"User {participant.FullName} has already an booked at that time");

            if (!isSpontaneousBooking && participant.BookingsOnThatDay > 0 && context.PlayMode.IsChargingBookingSubscription)
                throw new PreconditionException(ErrorCode.CannotBookInAdvanceTwice, [participant.FullName], $"User {participant.FullName} cannot book twice in advance on a day");

            if (!isSpontaneousBooking && context.PlayMode.IsChargingBookingSubscription && participant.UpcomingBookingsCount >= participant.Member.BookingsPerWeek)
                throw new PreconditionException(ErrorCode.MaximumReservationsReached, [participant.FullName], $"User {participant.FullName} has reached the maximum amount of reservations");

            if (context.PlayMode.MaxBookingsPerSeason is not null && participant.PlayModeBookingsInSeasonCount >= context.PlayMode.MaxBookingsPerSeason.Value)
                throw new PreconditionException(ErrorCode.MaxBookingsPerSeasonReached, [participant.FullName, context.PlayMode.Name], $"User {participant.FullName} has reached the maximum bookings per season for {context.PlayMode.Name}");
        }

        if (context.Court.IsInactive(offsetInterval))
            throw new PreconditionException(ErrorCode.CourtInactive, "Court is inactive during booked time");

        if (context.CourtBlockingTitles.Count > 0)
            throw new PreconditionException(ErrorCode.CourtBlocked, [string.Join(", ", context.CourtBlockingTitles)], "Court is blocked during booked time");

        if (context.PlayMode.FixedPlayerCount is not null && context.PlayMode.FixedPlayerCount != context.Participants.Count)
        {
            throw new PreconditionException(ErrorCode.InvalidAmountOfPlayers, [context.PlayMode.Name, context.PlayMode.FixedPlayerCount.Value.ToString()],
                $"{context.PlayMode.Name} requires {context.PlayMode.FixedPlayerCount} players");
        }

        var duration = offsetInterval.To - offsetInterval.From;
        if (context.PlayMode.FixedDuration is not null && context.PlayMode.FixedDuration != duration)
            throw new PreconditionException(ErrorCode.InvalidTime, [context.PlayMode.Name, duration.TotalHours.ToString()], $"Cannot play {context.PlayMode.Name} for {duration.TotalHours} hours");

        if (context.IntersectingBookings.Count > 0 && !context.PlayMode.CanOverbook)
            throw new PreconditionException(ErrorCode.CourtOccupied, "Court is already occupied during booked time");

        if (!context.PlayMode.CommentAllowed && comment.CleanNullable() is not null)
            throw new InvalidOperationException("Play mode does not allow comments");
    }

    public record Context
    {
        public required int ClubId { get; init; }
        public required Court Court { get; init; }
        public required TimeOnlyInterval OpeningHours { get; init; }
        public required PrimeTimeSettings PrimeTimeSettings { get; init; }
        public required MemberRole[] BookingMemberRole { get; init; }
        public required List<PlayerContext> Participants { get; init; }
        public required List<Booking> IntersectingBookings { get; init; }
        public required PlayMode PlayMode { get; init; }
        public required int? ClientTimeZoneOffset { get; init; }
        public required int? BookingGracePeriodInMinutes { get; init; }
        public required int? ActiveSeasonId { get; init; }
        public bool SkipPastBookingValidation { get; init; }

        /// <summary>Titles of the court blockings that intersect the booked time.</summary>
        public IReadOnlyList<string> CourtBlockingTitles { get; init; } = [];

        public record PlayerContext
        {
            public required Member Member { get; init; }
            public required string FullName { get; init; }
            public required bool IsChild { get; init; }
            public required int UpcomingBookingsCount { get; init; }
            public required int ActiveBookingsAtTimeOfReservation { get; init; }
            public required int BookingsOnThatDay { get; init; }
            public required bool IsAllowedInCurrentSeason { get; init; }
            public required bool IsAtpParticipant { get; init; }
            public required int PlayModeBookingsInSeasonCount { get; init; }
        }
    }
}