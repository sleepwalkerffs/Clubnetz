using Bookennis.Api.Business.Push;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.Notifications;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

/// <summary>
/// Collects the personal data stored about the current user (GDPR Art. 15 / 20) in a machine readable form. The controller hands it out
/// as a JSON download. Co-players of bookings are included by name only, because they are part of the user's own booking records.
/// </summary>
public record ExportPersonalData : IQuery<PersonalDataExport>
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor) : IRequestHandler<ExportPersonalData, PersonalDataExport>
    {
        public async Task<PersonalDataExport> Handle(ExportPersonalData request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            var user = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
                ?? throw new AuthorizationFailedException();

            var hasProfilePicture = await context.UserProfilePictures.AnyAsync(p => p.UserId == userId, cancellationToken);

            var members = await (
                from member in context.Set<Member>().IgnoreQueryFilters()
                join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id
                where member.UserId == userId
                orderby club.Name, member.MemberType
                select new { member.Id, ClubName = club.Name, member.MemberType, member.UserRoles }
            ).ToListAsync(cancellationToken);
            var memberIds = members.ConvertAll(m => m.Id);

            var seasons = await (
                from memberSeason in context.MemberSeasons.IgnoreQueryFilters()
                join season in context.Seasons.IgnoreQueryFilters() on memberSeason.SeasonId equals season.Id
                where memberIds.Contains(memberSeason.MemberId)
                orderby season.Period.From
                select new { memberSeason.MemberId, season.Period.From, season.Period.To, memberSeason.LeaderboardOptOut }
            ).ToListAsync(cancellationToken);

            var bookings = await (
                from bookingPlayer in context.BookingPlayers
                join booking in context.Bookings.IgnoreQueryFilters() on bookingPlayer.BookingEntryId equals booking.Id
                join club in context.Clubs.IgnoreQueryFilters() on booking.ClubId equals club.Id
                join court in context.Courts.IgnoreQueryFilters() on booking.CourtId equals court.Id
                join playMode in context.PlayModes.IgnoreQueryFilters() on booking.PlayModeId equals playMode.Id
                where memberIds.Contains(bookingPlayer.MemberId)
                orderby booking.Interval.From
                select new
                {
                    booking.Id,
                    booking.Interval.From,
                    booking.Interval.To,
                    ClubName = club.Name,
                    CourtName = court.Name,
                    PlayModeName = playMode.Name,
                    booking.Comment,
                    CoPlayers = (from otherPlayer in context.BookingPlayers
                                 join otherMember in context.Set<Member>().IgnoreQueryFilters() on otherPlayer.MemberId equals otherMember.Id
                                 join otherUser in context.Users on otherMember.UserId equals otherUser.Id
                                 where otherPlayer.BookingEntryId == booking.Id && !memberIds.Contains(otherPlayer.MemberId)
                                 select otherUser.FullName).ToList()
                }
            ).ToListAsync(cancellationToken);

            var registrations = await (
                from registration in context.ClubEventRegistrations
                join clubEvent in context.ClubEvents.IgnoreQueryFilters() on registration.ClubEventId equals clubEvent.Id
                join club in context.Clubs.IgnoreQueryFilters() on clubEvent.ClubId equals club.Id
                where memberIds.Contains(registration.MemberId)
                orderby clubEvent.StartDate
                select new
                {
                    ClubName = club.Name,
                    clubEvent.Title,
                    clubEvent.StartDate,
                    registration.HeadCount,
                    registration.Comment,
                    registration.RegisteredAt,
                    Answers = (from answer in context.ClubEventRegistrationAnswers
                               join option in context.ClubEventQuestionOptions on answer.ClubEventQuestionOptionId equals option.Id
                               join question in context.ClubEventQuestions on option.ClubEventQuestionId equals question.Id
                               where answer.ClubEventRegistrationId == registration.Id
                               orderby question.SortOrder, option.SortOrder
                               select new PersonalDataEventAnswer(question.Text, option.Label, answer.Quantity)).ToList()
                }
            ).ToListAsync(cancellationToken);

            var tierBadges = await (
                from memberBadge in context.MemberBadges
                join tier in context.BadgeTiers.IgnoreQueryFilters() on memberBadge.BadgeTierId equals tier.Id
                join club in context.Clubs.IgnoreQueryFilters() on tier.ClubId equals club.Id
                where memberIds.Contains(memberBadge.MemberId)
                orderby memberBadge.EarnedAt
                select new PersonalDataBadge(club.Name, tier.Name, memberBadge.EarnedAt)
            ).ToListAsync(cancellationToken);

            var oneTimeBadges = await (
                from award in context.MemberOneTimeBadges
                join badge in context.OneTimeBadges.IgnoreQueryFilters() on award.OneTimeBadgeId equals badge.Id
                join club in context.Clubs.IgnoreQueryFilters() on badge.ClubId equals club.Id
                where memberIds.Contains(award.MemberId)
                orderby award.AwardedAt
                select new PersonalDataBadge(club.Name, badge.Name, award.AwardedAt)
            ).ToListAsync(cancellationToken);

            var guestCards = await (
                from guestCard in context.GuestCards.IgnoreQueryFilters()
                join club in context.Clubs.IgnoreQueryFilters() on guestCard.ClubId equals club.Id
                where memberIds.Contains(guestCard.GuestMemberId)
                select new PersonalDataGuestCard(club.Name, guestCard.PurchasedBookings)
            ).ToListAsync(cancellationToken);

            var subscriptionPlans = await context.SubscriptionPlans
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(p => p.Participants)
                .Where(p => p.OwnerUserId == userId)
                .OrderBy(p => p.StartDate)
                .ToListAsync(cancellationToken);

            var children = await context.Users
                .AsNoTracking()
                .Where(u => u.BelongsToUserId == userId)
                .OrderBy(u => u.FirstName)
                .Select(u => new PersonalDataChild(u.FirstName, u.LastName, u.Birthday, u.Gender))
                .ToListAsync(cancellationToken);

            var pushSubscriptions = await context.PushSubscriptions
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.Id)
                .Select(s => new { s.Endpoint, s.Metadata.Created })
                .ToListAsync(cancellationToken);

            var notificationPreferences = await context.NotificationPreferences
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.Type)
                .Select(p => new PersonalDataNotificationPreference(p.Type, p.Push, p.Email))
                .ToListAsync(cancellationToken);

            return new PersonalDataExport(
                DateTimeOffset.UtcNow,
                new PersonalDataAccount(
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Birthday,
                    user.Gender,
                    user.Language,
                    NullIfEmpty(user.Street),
                    NullIfEmpty(user.ZipCode),
                    NullIfEmpty(user.City),
                    user.Country,
                    user.EmailConfirmed,
                    hasProfilePicture,
                    user.PrivacyPolicyAcceptedAt),
                members.ConvertAll(m => new PersonalDataMembership(
                    m.ClubName,
                    m.MemberType,
                    m.UserRoles,
                    seasons.Where(s => s.MemberId == m.Id).Select(s => new PersonalDataSeason(s.From, s.To, s.LeaderboardOptOut)).ToList())),
                bookings.ConvertAll(b => new PersonalDataBooking(b.From, b.To, b.ClubName, b.CourtName, b.PlayModeName, b.CoPlayers, b.Comment)),
                registrations.ConvertAll(r => new PersonalDataEventRegistration(r.ClubName, r.Title, r.StartDate, r.HeadCount, r.Comment, r.RegisteredAt, r.Answers)),
                tierBadges,
                oneTimeBadges,
                guestCards,
                subscriptionPlans.ConvertAll(p => new PersonalDataSubscriptionPlan(
                    p.Name,
                    p.StartDate,
                    p.EndDate,
                    p.Participants.OrderBy(x => x.SortOrder).Select(x => new PersonalDataPlanParticipant(x.Name, x.Percentage)).ToList())),
                children,
                pushSubscriptions.ConvertAll(s => new PersonalDataPushDevice(PushEndpoint.Host(s.Endpoint), s.Created)),
                notificationPreferences);
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public record PersonalDataExport(
    DateTimeOffset ExportedAt,
    PersonalDataAccount Account,
    List<PersonalDataMembership> Memberships,
    List<PersonalDataBooking> Bookings,
    List<PersonalDataEventRegistration> EventRegistrations,
    List<PersonalDataBadge> Badges,
    List<PersonalDataBadge> OneTimeBadges,
    List<PersonalDataGuestCard> GuestCards,
    List<PersonalDataSubscriptionPlan> SubscriptionPlans,
    List<PersonalDataChild> Children,
    List<PersonalDataPushDevice> PushDevices,
    List<PersonalDataNotificationPreference> NotificationPreferences);

public record PersonalDataAccount(
    string FirstName,
    string LastName,
    string? Email,
    DateOnly Birthday,
    Gender Gender,
    Language Language,
    string? Street,
    string? ZipCode,
    string? City,
    Country Country,
    bool EmailConfirmed,
    bool HasProfilePicture,
    DateTimeOffset? PrivacyPolicyAcceptedAt);

public record PersonalDataMembership(string Club, MemberType MemberType, MemberRole[] Roles, List<PersonalDataSeason> Seasons);

public record PersonalDataSeason(DateOnly From, DateOnly To, bool LeaderboardOptOut);

public record PersonalDataBooking(DateTimeOffset From, DateTimeOffset To, string Club, string Court, string PlayMode, List<string> CoPlayers, string? Comment);

public record PersonalDataEventRegistration(
    string Club,
    string Event,
    DateOnly Date,
    int HeadCount,
    string? Comment,
    DateTimeOffset RegisteredAt,
    List<PersonalDataEventAnswer> Answers);

public record PersonalDataEventAnswer(string Question, string Option, int Quantity);

public record PersonalDataBadge(string Club, string Name, DateTimeOffset AwardedAt);

public record PersonalDataGuestCard(string Club, int PurchasedBookings);

public record PersonalDataSubscriptionPlan(string Name, DateOnly From, DateOnly To, List<PersonalDataPlanParticipant> Participants);

public record PersonalDataPlanParticipant(string Name, int Percentage);

public record PersonalDataChild(string FirstName, string LastName, DateOnly Birthday, Gender Gender);

/// <summary>A device registered for push notifications, identified by the push service it is reached through.</summary>
public record PersonalDataPushDevice(string PushService, DateTime RegisteredAt);

/// <summary>The channels the user chose for a type of notification. Types that are missing use the defaults.</summary>
public record PersonalDataNotificationPreference(NotificationType Type, bool Push, bool Email);
