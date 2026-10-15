using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Statistics;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global;
using Bookennis.Shared.Controller.Statistics;
using Bookennis.Shared.Controller.Wrapped;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Wrapped;

/// <summary>
/// The season review ("Wrapped") of the signed-in member. Nothing is stored: it is calculated from the bookings, badges and events
/// of the season whenever it is opened, so it can be generated for every past season. Members only get released seasons,
/// club admins (<paramref name="CanManage"/>) can preview every season that has started.
/// </summary>
public record GetSeasonWrapped(int UserId, int ClubId, int SeasonId, bool CanManage) : IQuery<SeasonWrappedResult>
{
    public enum ErrorCode
    {
        WrappedNotPublished = 0,
    }

    private const int TopPartnerCount = 3;

    public class Handler(AppDbContext context) : IRequestHandler<GetSeasonWrapped, SeasonWrappedResult>
    {
        public async Task<SeasonWrappedResult> Handle(GetSeasonWrapped request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var season = await context.Seasons
                .AsNoTracking()
                .SingleRequiredAsync(s => s.Id == request.SeasonId && s.ClubId == request.ClubId, cancellationToken);

            if (!season.HasStartedOn(today))
                throw new PreconditionException(Season.ErrorCode.WrappedSeasonNotStarted, "The season has not started yet.");
            if (!season.IsWrappedPublished && !request.CanManage)
                throw new PreconditionException(ErrorCode.WrappedNotPublished, "The Wrapped of this season has not been released yet.");

            var clubName = await context.Clubs
                .Where(c => c.Id == request.ClubId)
                .Select(c => c.Name)
                .SingleAsync(cancellationToken);

            var firstName = await context.Users
                .Where(u => u.Id == request.UserId)
                .Select(u => u.FirstName)
                .FirstOrDefaultAsync(cancellationToken);

            // Support mode has no member: the Wrapped then only shows the club
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var clubBookings = await WrappedBookings.Load(context, request.ClubId, season.Period, cancellationToken);
            var ownBookings = memberId is { } id ? clubBookings.Where(b => b.PlayerIds.Contains(id)).ToList() : [];

            var memberTypes = await context.Set<Member>()
                .Where(m => m.ClubId == request.ClubId)
                .Select(m => new { m.Id, m.MemberType })
                .ToDictionaryAsync(m => m.Id, m => m.MemberType, cancellationToken);

            var enrolledMemberIds = await context.MemberSeasons
                .Where(ms => ms.SeasonId == season.Id)
                .Select(ms => ms.MemberId)
                .ToListAsync(cancellationToken);

            var courtNames = await context.Courts
                .Where(c => c.ClubId == request.ClubId)
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

            var weekdayCounts = new int[7];
            var hourCounts = new int[24];
            foreach (var booking in ownBookings)
            {
                weekdayCounts[((int)booking.From.DayOfWeek + 6) % 7]++;
                hourCounts[booking.From.Hour]++;
            }

            var playDates = ownBookings.Select(b => b.Date).Distinct().Order().ToList();
            var weeks = playDates.Select(CalendarWeeks.GetMonday).ToHashSet();
            var (rank, betterThan) = GetRank(memberId, ownBookings.Count, clubBookings, enrolledMemberIds);

            return new SeasonWrappedResult
            {
                SeasonId = season.Id,
                StartDate = season.Period.From,
                EndDate = season.Period.To,
                IsActive = season.IsActiveOn(today),
                IsPublished = season.IsWrappedPublished,
                ClubName = clubName,
                FirstName = firstName,
                Matches = ownBookings.Count,
                HoursOnCourt = Math.Round(ownBookings.Sum(b => b.DurationHours), 1),
                FirstMatch = playDates.Count > 0 ? playDates[0] : null,
                LastMatch = playDates.Count > 0 ? playDates[^1] : null,
                ActiveWeeks = weeks.Count,
                DaysPlayed = playDates.Count,
                Rank = rank,
                EnrolledMembers = enrolledMemberIds.Count,
                BetterThanPercentage = betterThan,
                TopPartners = await GetTopPartners(memberId, ownBookings, cancellationToken),
                DistinctPartners = ownBookings.SelectMany(b => b.PlayerIds).Where(p => p != memberId).Distinct().Count(),
                PlayerType = GetMemberStatistics.Handler.GetPlayerType(ownBookings.Count, weekdayCounts, hourCounts),
                FavouriteWeekday = ownBookings.Count > 0 ? (DayOfWeek)((Array.IndexOf(weekdayCounts, weekdayCounts.Max()) + 1) % 7) : null,
                FavouriteHour = ownBookings.Count > 0 ? Array.IndexOf(hourCounts, hourCounts.Max()) : null,
                FavouriteCourt = GetFavouriteCourt(ownBookings, courtNames),
                CourtsPlayed = ownBookings.Select(b => b.CourtId).Distinct().Count(),
                BusiestMonth = ownBookings
                    .GroupBy(b => (b.From.Year, b.From.Month))
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Select(g => new MonthRecord { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
                    .FirstOrDefault(),
                BestWeek = ownBookings
                    .GroupBy(b => CalendarWeeks.GetMonday(b.Date))
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Select(g => new WeekRecord { WeekStart = g.Key, Count = g.Count() })
                    .FirstOrDefault(),
                LongestWeekStreak = GetMemberStatistics.Handler.GetLongestWeekStreak(weeks),
                LongestBreakDays = playDates.Count < 2 ? null : playDates.Zip(playDates.Skip(1), (a, b) => b.DayNumber - a.DayNumber).Max(),
                RegularSlot = await GetRegularSlot(ownBookings, courtNames, cancellationToken),
                RecurringMatches = ownBookings.Count(b => b.RecurringBookingSeriesId is not null),
                Badges = await GetBadges(memberId, season, cancellationToken),
                EventsAttended = memberId is { } attendee
                    ? await context.ClubEventRegistrations
                        .Where(r => r.MemberId == attendee)
                        .Join(context.ClubEvents, r => r.ClubEventId, e => e.Id, (r, e) => e)
                        .CountAsync(e => e.ClubId == request.ClubId && e.StartDate >= season.Period.From && e.StartDate <= season.Period.To && e.StartDate <= today, cancellationToken)
                    : 0,
                Club = new WrappedClubDto
                {
                    Matches = clubBookings.Count,
                    HoursOnCourt = Math.Round(clubBookings.Sum(b => b.DurationHours), 1),
                    ActivePlayers = CountPlayers(clubBookings, memberTypes, MemberType.ClubMember),
                    GuestPlayers = CountPlayers(clubBookings, memberTypes, MemberType.GuestMember),
                    BusiestCourt = clubBookings
                        .GroupBy(b => b.CourtId)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Key)
                        .Select(g => courtNames.GetValueOrDefault(g.Key))
                        .FirstOrDefault(),
                    BusiestWeekday = clubBookings.Count == 0
                        ? null
                        : clubBookings.GroupBy(b => b.From.DayOfWeek).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key,
                    Events = await context.ClubEvents
                        .CountAsync(e => e.ClubId == request.ClubId && e.StartDate >= season.Period.From && e.StartDate <= season.Period.To && e.StartDate <= today, cancellationToken),
                    OwnSharePercentage = clubBookings.Count == 0 ? 0 : Math.Round(ownBookings.Count * 100.0 / clubBookings.Count, 1),
                },
            };
        }

        /// <summary>Place among the enrolled members by matches; ties share a place. Only for a member who played.</summary>
        private static (int? Rank, int? BetterThanPercentage) GetRank(int? memberId, int ownMatches, List<WrappedBooking> clubBookings, List<int> enrolledMemberIds)
        {
            if (memberId is null || ownMatches == 0)
                return (null, null);

            var matchesByMember = clubBookings
                .SelectMany(b => b.PlayerIds)
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => g.Count());

            var others = enrolledMemberIds
                .Where(id => id != memberId)
                .Select(id => matchesByMember.GetValueOrDefault(id))
                .ToList();

            var rank = 1 + others.Count(c => c > ownMatches);
            int? betterThan = others.Count == 0 ? null : (int)Math.Round(others.Count(c => c < ownMatches) * 100.0 / others.Count);
            return (rank, betterThan);
        }

        private static int CountPlayers(List<WrappedBooking> bookings, Dictionary<int, MemberType> memberTypes, MemberType type)
            => bookings
                .SelectMany(b => b.PlayerIds)
                .Distinct()
                .Count(id => memberTypes.TryGetValue(id, out var t) && t == type);

        private static WrappedCourtDto? GetFavouriteCourt(List<WrappedBooking> ownBookings, Dictionary<int, string> courtNames)
            => ownBookings
                .GroupBy(b => b.CourtId)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => new WrappedCourtDto
                {
                    Name = courtNames.GetValueOrDefault(g.Key, ""),
                    Matches = g.Count(),
                    SharePercentage = (int)Math.Round(g.Count() * 100.0 / ownBookings.Count),
                })
                .FirstOrDefault();

        /// <summary>Partners appear with name and picture only, like everywhere else other members are shown.</summary>
        private async Task<List<WrappedPartnerDto>> GetTopPartners(int? memberId, List<WrappedBooking> ownBookings, CancellationToken cancellationToken)
        {
            var counts = ownBookings
                .SelectMany(b => b.PlayerIds)
                .Where(p => p != memberId)
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => g.Count());

            if (counts.Count == 0)
                return [];

            var partnerIds = counts.Keys.ToList();
            var partners = await (
                from member in context.Set<Member>()
                where partnerIds.Contains(member.Id)
                join user in context.Users on member.UserId equals user.Id
                select new
                {
                    member.Id,
                    UserId = user.Id,
                    user.FirstName,
                    user.LastName,
                    HasProfilePicture = context.UserProfilePictures.Any(p => p.UserId == user.Id),
                }
            ).ToListAsync(cancellationToken);

            return partners
                .Select(p => new WrappedPartnerDto
                {
                    MemberId = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    ProfilePictureUrl = p.HasProfilePicture ? $"/api/Profile/picture/{p.UserId}" : null,
                    Matches = counts[p.Id],
                })
                .OrderByDescending(p => p.Matches)
                .ThenBy(p => p.FirstName)
                .ThenBy(p => p.LastName)
                .Take(TopPartnerCount)
                .ToList();
        }

        private async Task<WrappedRegularSlotDto?> GetRegularSlot(List<WrappedBooking> ownBookings, Dictionary<int, string> courtNames, CancellationToken cancellationToken)
        {
            var top = ownBookings
                .Where(b => b.RecurringBookingSeriesId is not null)
                .GroupBy(b => b.RecurringBookingSeriesId!.Value)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .FirstOrDefault();

            if (top is null)
                return null;

            var series = await context.RecurringBookingSeries
                .Where(s => s.Id == top.Key)
                .Select(s => new { s.DayOfWeek, s.StartTime, s.CourtId })
                .FirstOrDefaultAsync(cancellationToken);

            // The series may have been deleted since, its bookings still tell the slot
            var first = top.First();
            return new WrappedRegularSlotDto
            {
                DayOfWeek = series?.DayOfWeek ?? first.From.DayOfWeek,
                StartTime = (series?.StartTime ?? TimeOnly.FromDateTime(first.From)).ToTimeSpan(),
                CourtName = courtNames.GetValueOrDefault(series?.CourtId ?? first.CourtId, ""),
                Matches = top.Count(),
            };
        }

        private async Task<List<WrappedBadgeDto>> GetBadges(int? memberId, Season season, CancellationToken cancellationToken)
        {
            if (memberId is not { } id)
                return [];

            var tierBadges = await (
                from badge in context.MemberBadges
                where badge.MemberId == id && badge.SeasonId == season.Id
                join tier in context.BadgeTiers on badge.BadgeTierId equals tier.Id
                select new WrappedBadgeDto
                {
                    Name = tier.Name,
                    Level = tier.Level,
                    AwardedAt = badge.EarnedAt,
                    ImageUrl = context.BadgeTierImages.Any(i => i.BadgeTierId == tier.Id)
                        ? "/api/Clubs/" + tier.ClubId + "/BadgeTiers/" + tier.Id + "/image"
                        : null,
                }
            ).ToListAsync(cancellationToken);

            var oneTimeBadges = await (
                from award in context.MemberOneTimeBadges
                where award.MemberId == id
                join badge in context.OneTimeBadges on award.OneTimeBadgeId equals badge.Id
                where badge.SeasonId == season.Id
                select new WrappedBadgeDto
                {
                    Name = badge.Name,
                    Level = null,
                    AwardedAt = award.AwardedAt,
                    ImageUrl = context.OneTimeBadgeImages.Any(i => i.OneTimeBadgeId == badge.Id)
                        ? "/api/Clubs/" + badge.ClubId + "/OneTimeBadges/" + badge.Id + "/image"
                        : null,
                }
            ).ToListAsync(cancellationToken);

            // One-time badges (club champion, ...) first, then the tiers from the highest down
            return oneTimeBadges.OrderBy(b => b.AwardedAt)
                .Concat(tierBadges.OrderByDescending(b => b.Level))
                .ToList();
        }
    }
}
