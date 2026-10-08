using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global;
using Bookennis.Shared.Controller.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Statistics;

public record GetMemberStatistics(int UserId, int ClubId) : IQuery<GetMemberStatisticsResult>
{
    // From/To are in the local time of the booking; DurationHours comes from the UTC interval (correct across DST changes)
    private sealed record BookingData(int Id, DateTime From, DateTime To, double DurationHours, int CourtId, int PlayModeId)
    {
        public DateOnly Date => DateOnly.FromDateTime(From);
    }

    private sealed record PlayerData(int BookingEntryId, int MemberId, int UserId, string FirstName, string LastName, DateOnly Birthday, bool HasProfilePicture, bool IsSelf);

    private sealed record CourtInfo(string Name, int SortOrder);

    private sealed record MemberBookingData(int MemberId, DateOnly Date);

    // A player type is only derived from a meaningful number of bookings
    private const int MinBookingsForPlayerType = 3;
    private const double PlayerTypeThreshold = 0.5;
    private const double WeekendWarriorThreshold = 0.6;

    public class Handler(AppDbContext context) : IRequestHandler<GetMemberStatistics, GetMemberStatisticsResult>
    {
        public async Task<GetMemberStatisticsResult> Handle(GetMemberStatistics request, CancellationToken cancellationToken)
        {
            var localTime = new LocalTimeConverter();

            var memberIds = await context.Set<Member>()
                .IgnoreQueryFilters()
                .Where(m => m.UserId == request.UserId)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            var memberForClub = await context.Set<Member>()
                .IgnoreQueryFilters()
                .Where(m => m.UserId == request.UserId && m.ClubId == request.ClubId)
                .OrderBy(m => m.MemberType)
                .FirstOrDefaultAsync(cancellationToken);

            var chargingPlayModeIds = await context.PlayModes
                .Where(pm => pm.IsChargingBookingSubscription)
                .Select(pm => pm.Id)
                .ToListAsync(cancellationToken);

            var activatedSeasonIds = await context.MemberSeasons
                .Where(ms => memberIds.Contains(ms.MemberId))
                .Select(ms => ms.SeasonId)
                .ToListAsync(cancellationToken);

            var seasons = await context.Seasons
                .OrderByDescending(s => s.Period.From)
                .ToListAsync(cancellationToken);

            var bookingRows = await (
                from booking in context.Bookings
                where chargingPlayModeIds.Contains(booking.PlayModeId)
                where context.BookingPlayers.Any(bp => bp.BookingEntryId == booking.Id && memberIds.Contains(bp.MemberId))
                select new { booking.Id, booking.Interval.From, booking.Interval.To, booking.TimeZoneInfoId, booking.CourtId, booking.PlayModeId }
            ).ToListAsync(cancellationToken);

            var bookings = bookingRows
                .Select(b => new BookingData(
                    b.Id,
                    localTime.ToLocal(b.From, b.TimeZoneInfoId),
                    localTime.ToLocal(b.To, b.TimeZoneInfoId),
                    (b.To - b.From).TotalHours,
                    b.CourtId,
                    b.PlayModeId))
                .ToList();

            var bookingIds = bookings.Select(b => b.Id).ToList();

            var allPlayers = await (
                from bp in context.BookingPlayers
                where bookingIds.Contains(bp.BookingEntryId)
                join member in context.Set<Member>().IgnoreQueryFilters() on bp.MemberId equals member.Id
                join user in context.Users on member.UserId equals user.Id
                select new PlayerData(
                    bp.BookingEntryId,
                    member.Id,
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Birthday,
                    context.UserProfilePictures.Any(p => p.UserId == user.Id),
                    memberIds.Contains(member.Id))
            ).ToListAsync(cancellationToken);

            var courts = await context.Courts
                .IgnoreQueryFilters()
                .Where(c => bookings.Select(b => b.CourtId).Distinct().Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => new CourtInfo(c.Name, c.SortOrder), cancellationToken);

            var playModeNames = await context.PlayModes
                .Where(pm => chargingPlayModeIds.Contains(pm.Id))
                .ToDictionaryAsync(pm => pm.Id, pm => pm.Name, cancellationToken);

            // --- Play mode quotas ---
            var limitedPlayModes = await context.PlayModes
                .Where(pm => pm.ClubId == request.ClubId && pm.MaxBookingsPerSeason.HasValue)
                .ToListAsync(cancellationToken);

            List<(int PlayModeId, string Name, int Max)> accessibleLimitedPlayModes = [];
            if (memberForClub is not null)
            {
                var memberRoles = memberForClub.UserRoles;
                var isAdmin = memberRoles.Contains(MemberRole.Admin);
                accessibleLimitedPlayModes = (isAdmin
                    ? limitedPlayModes
                    : limitedPlayModes.Where(pm => pm.AllowedRoles.Any(r => memberRoles.Contains(r))))
                    .Select(pm => (pm.Id, pm.Name, pm.MaxBookingsPerSeason!.Value))
                    .ToList();
            }

            List<(int PlayModeId, DateOnly Date)> quotaBookings = [];
            if (memberForClub is not null && accessibleLimitedPlayModes.Count > 0)
            {
                var accessiblePlayModeIds = accessibleLimitedPlayModes.Select(pm => pm.PlayModeId).ToList();
                var memberId = memberForClub.Id;
                var quotaRows = await context.BookingPlayers
                    .Where(bp => bp.MemberId == memberId && accessiblePlayModeIds.Contains(bp.Booking.PlayModeId))
                    .Select(bp => new { bp.Booking.PlayModeId, bp.Booking.Interval.From, bp.Booking.TimeZoneInfoId })
                    .ToListAsync(cancellationToken);

                quotaBookings = quotaRows
                    .Select(x => (x.PlayModeId, DateOnly.FromDateTime(localTime.ToLocal(x.From, x.TimeZoneInfoId))))
                    .ToList();
            }
            // -------------------------

            // --- Club comparison (anonymous): bookings of all members of the club ---
            var clubBookingRows = await context.BookingPlayers
                .Where(bp => bp.Booking.ClubId == request.ClubId && chargingPlayModeIds.Contains(bp.Booking.PlayModeId))
                .Select(bp => new { bp.MemberId, bp.Booking.Interval.From, bp.Booking.TimeZoneInfoId })
                .ToListAsync(cancellationToken);

            var clubBookings = clubBookingRows
                .Select(x => new MemberBookingData(x.MemberId, DateOnly.FromDateTime(localTime.ToLocal(x.From, x.TimeZoneInfoId))))
                .ToList();

            var seasonIds = seasons.Select(s => s.Id).ToList();
            var enrollments = await context.MemberSeasons
                .Where(ms => seasonIds.Contains(ms.SeasonId))
                .Select(ms => new { ms.SeasonId, ms.MemberId })
                .ToListAsync(cancellationToken);
            // -------------------------

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var seasonStats = new List<SeasonStatisticsResult>();
            foreach (var season in seasons)
            {
                var isActivated = activatedSeasonIds.Contains(season.Id);
                StatisticsData? data = null;
                ClubComparison? clubComparison = null;

                if (isActivated)
                {
                    var seasonBookings = bookings
                        .Where(b => IsInSeason(b.Date, season))
                        .ToList();

                    data = BuildStatisticsData(seasonBookings, allPlayers, courts, playModeNames, today);

                    var enrolledMemberIds = enrollments
                        .Where(e => e.SeasonId == season.Id)
                        .Select(e => e.MemberId)
                        .ToHashSet();

                    clubComparison = BuildClubComparison(data.TotalBookings, clubBookings.Where(b => IsInSeason(b.Date, season)), enrolledMemberIds, memberIds);
                }

                var playModeQuotas = accessibleLimitedPlayModes
                    .Select(pm => new PlayModeQuotaEntry
                    {
                        PlayModeName = pm.Name,
                        MaxBookingsPerSeason = pm.Max,
                        UsedBookings = quotaBookings.Count(b => b.PlayModeId == pm.PlayModeId && IsInSeason(b.Date, season))
                    })
                    .ToList();

                seasonStats.Add(new SeasonStatisticsResult
                {
                    SeasonId = season.Id,
                    StartDate = season.Period.From,
                    EndDate = season.Period.To,
                    IsActivated = isActivated,
                    Data = data,
                    PlayModeQuotas = playModeQuotas,
                    ClubComparison = clubComparison,
                });
            }

            var allTimeData = BuildStatisticsData(bookings, allPlayers, courts, playModeNames, today);

            return new GetMemberStatisticsResult
            {
                SeasonStatistics = seasonStats,
                AllTime = allTimeData,
            };
        }

        private static bool IsInSeason(DateOnly date, Season season)
            => date >= season.Period.From && date <= season.Period.To;

        private static ClubComparison? BuildClubComparison(
            int ownBookings,
            IEnumerable<MemberBookingData> seasonClubBookings,
            HashSet<int> enrolledMemberIds,
            List<int> ownMemberIds)
        {
            var bookingsByMember = seasonClubBookings
                .Where(b => enrolledMemberIds.Contains(b.MemberId))
                .GroupBy(b => b.MemberId)
                .ToDictionary(g => g.Key, g => g.Count());

            var otherCounts = enrolledMemberIds
                .Where(id => !ownMemberIds.Contains(id))
                .Select(id => bookingsByMember.GetValueOrDefault(id))
                .ToList();

            if (otherCounts.Count == 0)
                return null;

            return new ClubComparison
            {
                ClubAverageBookings = Math.Round((otherCounts.Sum() + ownBookings) / (double)(otherCounts.Count + 1), 1),
                BetterThanPercentage = (int)Math.Round(otherCounts.Count(c => c < ownBookings) * 100.0 / otherCounts.Count),
                EnrolledMembers = otherCounts.Count + 1,
            };
        }

        private static StatisticsData BuildStatisticsData(
            List<BookingData> filteredBookings,
            List<PlayerData> allPlayers,
            Dictionary<int, CourtInfo> courts,
            Dictionary<int, string> playModeNames,
            DateOnly today)
        {
            var bookingIds = filteredBookings.Select(b => b.Id).ToHashSet();

            var relevantPlayers = allPlayers
                .Where(p => bookingIds.Contains(p.BookingEntryId))
                .ToList();

            var courtUsage = filteredBookings
                .GroupBy(b => b.CourtId)
                .Select(g => new
                {
                    SortOrder = courts.TryGetValue(g.Key, out var ci) ? ci.SortOrder : int.MaxValue,
                    Entry = new ChartEntry
                    {
                        Label = courts.TryGetValue(g.Key, out var info) ? info.Name : $"Court {g.Key}",
                        Value = g.Count()
                    }
                })
                .OrderBy(c => c.SortOrder)
                .Select(c => c.Entry)
                .ToList();

            var partnerUsage = relevantPlayers
                .Where(p => !p.IsSelf)
                .GroupBy(p => p.UserId)
                .Select(g =>
                {
                    var partner = g.First();
                    return new PartnerEntry
                    {
                        MemberId = partner.MemberId,
                        FirstName = partner.FirstName,
                        LastName = partner.LastName,
                        ProfilePictureUrl = partner.HasProfilePicture ? $"/api/Profile/picture/{partner.UserId}" : null,
                        Count = g.Select(p => p.BookingEntryId).Distinct().Count(),
                    };
                })
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.FirstName)
                .ThenBy(p => p.LastName)
                .ToList();

            var playModeUsage = filteredBookings
                .GroupBy(b => b.PlayModeId)
                .Select(g => new ChartEntry
                {
                    Label = playModeNames.GetValueOrDefault(g.Key, $"Mode {g.Key}"),
                    Value = g.Count()
                })
                .OrderByDescending(c => c.Value)
                .ToList();

            var opponentAges = relevantPlayers
                .Where(p => !p.IsSelf)
                .Select(p => (double)(today.Year - p.Birthday.Year))
                .ToList();

            var averageAge = opponentAges.Count > 0 ? opponentAges.Average() : 0;

            var weekdayCounts = new int[7];
            var hourCounts = new int[24];
            foreach (var booking in filteredBookings)
            {
                weekdayCounts[((int)booking.From.DayOfWeek + 6) % 7]++;
                hourCounts[booking.From.Hour]++;
            }

            var playDates = filteredBookings.Select(b => b.Date).Distinct().Order().ToList();
            var weeks = playDates.Select(CalendarWeeks.GetMonday).ToHashSet();

            var bestWeek = filteredBookings
                .GroupBy(b => CalendarWeeks.GetMonday(b.Date))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => new WeekRecord { WeekStart = g.Key, Count = g.Count() })
                .FirstOrDefault();

            var busiestMonth = filteredBookings
                .GroupBy(b => (b.From.Year, b.From.Month))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => new MonthRecord { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
                .FirstOrDefault();

            var activityDays = filteredBookings
                .GroupBy(b => b.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DayActivity { Date = g.Key, Count = g.Count() })
                .ToList();

            return new StatisticsData
            {
                CourtUsage = courtUsage,
                PartnerUsage = partnerUsage,
                PlayModeUsage = playModeUsage,
                AverageOpponentAge = Math.Round(averageAge, 1),
                TotalBookings = filteredBookings.Count,
                TotalHoursOnCourt = Math.Round(filteredBookings.Sum(b => b.DurationHours), 1),
                DistinctPartners = partnerUsage.Count,
                WeekdayCounts = [.. weekdayCounts],
                HourCounts = [.. hourCounts],
                PlayerType = GetPlayerType(filteredBookings.Count, weekdayCounts, hourCounts),
                LongestWeekStreak = GetLongestWeekStreak(weeks),
                CurrentWeekStreak = GetCurrentWeekStreak(weeks, today),
                LongestBreakDays = playDates.Count < 2 ? null : playDates.Zip(playDates.Skip(1), (a, b) => b.DayNumber - a.DayNumber).Max(),
                BestWeek = bestWeek,
                BusiestMonth = busiestMonth,
                FirstBooking = playDates.Count > 0 ? playDates[0] : null,
                LastBooking = playDates.Count > 0 ? playDates[^1] : null,
                ActivityDays = activityDays,
            };
        }

        private static PlayerType? GetPlayerType(int totalBookings, int[] weekdayCounts, int[] hourCounts)
        {
            if (totalBookings < MinBookingsForPlayerType)
                return null;

            double Share(int count) => (double)count / totalBookings;

            if (Share(weekdayCounts[5] + weekdayCounts[6]) >= WeekendWarriorThreshold)
                return PlayerType.WeekendWarrior;

            var morning = hourCounts[..12].Sum();
            var afternoon = hourCounts[12..17].Sum();
            var evening = hourCounts[17..].Sum();

            if (Share(morning) >= PlayerTypeThreshold)
                return PlayerType.EarlyBird;
            if (Share(evening) >= PlayerTypeThreshold)
                return PlayerType.NightOwl;
            if (Share(afternoon) >= PlayerTypeThreshold)
                return PlayerType.AfternoonAce;

            return PlayerType.AllRounder;
        }

        private static int GetLongestWeekStreak(HashSet<DateOnly> weeks)
        {
            var longest = 0;
            foreach (var monday in weeks)
            {
                // Only start counting at the first week of a streak
                if (weeks.Contains(monday.AddDays(-7)))
                    continue;

                var length = 1;
                while (weeks.Contains(monday.AddDays(7 * length)))
                    length++;

                longest = Math.Max(longest, length);
            }

            return longest;
        }

        private static int GetCurrentWeekStreak(HashSet<DateOnly> weeks, DateOnly today)
        {
            // The streak is still alive when nothing was played yet in the current week
            var monday = CalendarWeeks.GetMonday(today);
            if (!weeks.Contains(monday))
                monday = monday.AddDays(-7);

            var streak = 0;
            while (weeks.Contains(monday))
            {
                streak++;
                monday = monday.AddDays(-7);
            }

            return streak;
        }
    }
}
