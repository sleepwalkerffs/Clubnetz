using System.Drawing;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Bookings;

public class BookingDomainServiceTests
{
    private static readonly DateTimeOffsetInterval Tomorrow = new(
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(10), TimeSpan.Zero),
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(11), TimeSpan.Zero));

    [Fact]
    public void BookCourt_CourtIsFree_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(), Tomorrow, "UTC", null);

        booking.Interval.Should().Be(Tomorrow);
    }

    [Fact]
    public void BookCourt_CourtIsBlocked_ThrowsWithTheReason()
    {
        var act = () => new BookingDomainService().BookCourt(Context(courtBlockingTitles: ["Club championship", "Maintenance"]), Tomorrow, "UTC", null);

        var exception = act.Should().Throw<PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtBlocked));
        exception.ErrorDetails.Should().Equal("Club championship, Maintenance");
    }

    [Fact]
    public void BookCourt_CourtIsBlocked_AlsoBlocksOverbookingPlayModes()
    {
        var act = () => new BookingDomainService().BookCourt(Context(canOverbook: true, courtBlockingTitles: ["Maintenance"]), Tomorrow, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtBlocked));
    }

    [Fact]
    public void BookCourt_CourtIsOccupied_ThrowsForAPlayModeThatCannotOverbook()
    {
        var act = () => new BookingDomainService().BookCourt(Context(intersectingBookings: [ExistingBooking()]), Tomorrow, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
    }

    [Fact]
    public void BookCourt_CourtIsOccupied_OverbookingPlayModeBooksAnyway()
    {
        var booking = new BookingDomainService().BookCourt(Context(canOverbook: true, intersectingBookings: [ExistingBooking()]), Tomorrow, "UTC", null);

        booking.Interval.Should().Be(Tomorrow);
    }

    [Fact]
    public void EditBooking_CourtIsOccupied_ThrowsForAPlayModeThatCannotOverbook()
    {
        var existing = ExistingBooking();
        var act = () => new BookingDomainService().EditBooking(Context(intersectingBookings: [ExistingBooking()]), existing, Tomorrow, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
    }

    [Fact]
    public void BookCourt_DurationDiffersFromTheFixedDuration_IsAnInvalidTime()
    {
        var twoHours = new DateTimeOffsetInterval(Tomorrow.From, Tomorrow.From.AddHours(2));

        var act = () => new BookingDomainService().BookCourt(Context(fixedDuration: TimeSpan.FromHours(1)), twoHours, "UTC", null);

        var exception = act.Should().Throw<PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.InvalidTime));
        exception.ErrorDetails.Should().Equal("Training", "2");
    }

    [Fact]
    public void BookCourt_DurationMatchesTheFixedDuration_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(fixedDuration: TimeSpan.FromHours(1)), Tomorrow, "UTC", null);

        booking.Interval.Should().Be(Tomorrow);
    }

    [Fact]
    public void EditBooking_ResizedAgainstTheFixedDuration_IsAnInvalidTime()
    {
        var existing = ExistingBooking();
        var resized = new DateTimeOffsetInterval(Tomorrow.From, Tomorrow.From.AddMinutes(90));

        var act = () => new BookingDomainService().EditBooking(Context(fixedDuration: TimeSpan.FromHours(1)), existing, resized, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.InvalidTime));
        existing.Interval.Should().NotBe(resized);
    }

    [Fact]
    public void BookCourt_NumberOfPlayersDiffersFromTheFixedPlayerCount_IsRejected()
    {
        var act = () => new BookingDomainService().BookCourt(Context(fixedPlayerCount: 2, participants: [Player(1)]), Tomorrow, "UTC", null);

        var exception = act.Should().Throw<PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.InvalidAmountOfPlayers));
        exception.ErrorDetails.Should().Equal("Training", "2");
    }

    [Fact]
    public void BookCourt_NumberOfPlayersMatchesTheFixedPlayerCount_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(fixedPlayerCount: 2, participants: [Player(1), Player(2)]), Tomorrow, "UTC", null);

        booking.Players.Select(p => p.MemberId).Should().Equal(1, 2);
    }

    [Fact]
    public void BookCourt_CourtIsInactive_IsRejectedAlsoForOverbookingPlayModes()
    {
        var court = new Court(1, "Court 1", "C1");
        court.SetInactive(new DateTimeOffsetInterval(Tomorrow.From.AddMinutes(30), Tomorrow.From.AddDays(1)));

        foreach (var canOverbook in new[] { false, true })
        {
            var act = () => new BookingDomainService().BookCourt(Context(canOverbook: canOverbook, court: court), Tomorrow, "UTC", null);

            act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtInactive));
        }
    }

    [Fact]
    public void BookCourt_CourtIsInactiveAtAnotherTime_CreatesBooking()
    {
        var court = new Court(1, "Court 1", "C1");
        court.SetInactive(new DateTimeOffsetInterval(Tomorrow.To, Tomorrow.To.AddDays(1)));

        var booking = new BookingDomainService().BookCourt(Context(court: court), Tomorrow, "UTC", null);

        booking.Interval.Should().Be(Tomorrow);
    }

    private static Booking ExistingBooking() => new(1, 1, 1, Tomorrow, "UTC", []);

    private static BookingDomainService.Context.PlayerContext Player(int memberId)
    {
        var member = new GuestMember(memberId, 1);
        member.AllowBooking();
        typeof(Bookennis.Domain.Base.DomainEntity).GetProperty(nameof(member.Id))!.SetValue(member, memberId);
        return new BookingDomainService.Context.PlayerContext
        {
            Member = member,
            FullName = $"Player {memberId}",
            IsChild = false,
            UpcomingBookingsCount = 0,
            ActiveBookingsAtTimeOfReservation = 0,
            BookingsOnThatDay = 0,
            IsAllowedInCurrentSeason = true,
            IsAtpParticipant = false,
            PlayModeBookingsInSeasonCount = 0,
        };
    }

    private static BookingDomainService.Context Context(
        bool canOverbook = false,
        IReadOnlyList<string>? courtBlockingTitles = null,
        List<Booking>? intersectingBookings = null,
        TimeSpan? fixedDuration = null,
        int? fixedPlayerCount = null,
        List<BookingDomainService.Context.PlayerContext>? participants = null,
        Court? court = null)
    {
        var club = new Club(
            "Club",
            new TimeOnlyInterval(new TimeOnly(0, 0), new TimeOnly(23, 59)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.Trainer], Color.Brown, fixedPlayerCount, false, "Training", fixedDuration, canOverbook, false)]);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            Court = court ?? new Court(1, "Court 1", "C1"),
            OpeningHours = club.OpeningHours,
            PrimeTimeSettings = club.PrimeTimeSettings,
            BookingMemberRole = [MemberRole.Trainer],
            Participants = participants ?? [],
            IntersectingBookings = intersectingBookings ?? [],
            PlayMode = club.PlayModes[0],
            ClientTimeZoneOffset = null,
            BookingGracePeriodInMinutes = null,
            ActiveSeasonId = 1,
            CourtBlockingTitles = courtBlockingTitles ?? [],
        };
    }
}
