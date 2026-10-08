using System.Drawing;
using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Statistics;

/// <param name="ClubId">The club.</param>
/// <param name="Today">Overrides the current local date of the club (only used by tests).</param>
public record GetClubStatistics(int ClubId, DateOnly? Today = null) : IQuery<GetClubStatisticsResult>
{
    private const int TopPlayerCount = 10;

    public class Handler(AppDbContext context) : IRequestHandler<GetClubStatistics, GetClubStatisticsResult>
    {
        public async Task<GetClubStatisticsResult> Handle(GetClubStatistics request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.PlayModes)
                .SingleAsync(c => c.Id == request.ClubId, cancellationToken);

            var seasons = await context.Seasons
                .Where(s => s.ClubId == request.ClubId)
                .OrderBy(s => s.Period.From)
                .ToListAsync(cancellationToken);

            var courts = await context.Courts
                .IgnoreQueryFilters()
                .Where(c => c.ClubId == request.ClubId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);

            var bookingRows = await context.Bookings
                .Where(b => b.ClubId == request.ClubId)
                .Select(b => new
                {
                    b.Id,
                    b.Interval.From,
                    b.Interval.To,
                    b.CourtId,
                    b.PlayModeId,
                    b.TimeZoneInfoId,
                    b.RecurringBookingSeriesId,
                    b.Metadata.Created,
                    PlayerIds = context.BookingPlayers.Where(bp => bp.BookingEntryId == b.Id).Select(bp => bp.MemberId).ToList(),
                })
                .ToListAsync(cancellationToken);

            var localTime = new LocalTimeConverter();
            var allBookings = bookingRows
                .Select(b => new BookingData(
                    b.Id,
                    localTime.ToLocal(b.From, b.TimeZoneInfoId),
                    localTime.ToLocal(b.To, b.TimeZoneInfoId),
                    (b.To - b.From).TotalHours,
                    b.CourtId,
                    b.PlayModeId,
                    b.RecurringBookingSeriesId is not null,
                    b.RecurringBookingSeriesId is null ? b.From.UtcDateTime - DateTime.SpecifyKind(b.Created, DateTimeKind.Utc) : null,
                    b.PlayerIds))
                .ToList();

            // "Today" is the local date of the club, derived from the time zone most bookings are made in
            var clubTimeZoneId = bookingRows
                .GroupBy(b => b.TimeZoneInfoId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? TimeZoneInfo.Utc.Id;
            var today = request.Today ?? DateOnly.FromDateTime(localTime.ToLocal(DateTimeOffset.UtcNow, clubTimeZoneId));

            var memberSeasons = await context.MemberSeasons
                .Where(ms => context.Set<Member>().IgnoreQueryFilters().Any(m => m.Id == ms.MemberId && m.ClubId == request.ClubId && m.MemberType == MemberType.ClubMember))
                .Select(ms => new MemberSeasonData(ms.MemberId, ms.SeasonId))
                .ToListAsync(cancellationToken);

            var memberUsers = await (
                from member in context.Set<Member>().IgnoreQueryFilters()
                where member.ClubId == request.ClubId && member.MemberType == MemberType.ClubMember
                join user in context.Users on member.UserId equals user.Id
                select new MemberUserData(member.Id, user.Birthday, user.Gender)
            ).ToListAsync(cancellationToken);

            // Players can also be guests or members of other clubs (ATP)
            var playerIds = allBookings.SelectMany(b => b.PlayerIds).Distinct().ToList();
            var players = await (
                from member in context.Set<Member>().IgnoreQueryFilters()
                where playerIds.Contains(member.Id)
                join user in context.Users on member.UserId equals user.Id
                select new PlayerData(
                    member.Id,
                    user.FirstName,
                    user.LastName,
                    member.MemberType == MemberType.GuestMember,
                    context.UserProfilePictures.Any(p => p.UserId == user.Id) ? "/api/Profile/picture/" + user.Id : null)
            ).ToDictionaryAsync(p => p.MemberId, cancellationToken);

            var playModes = club.PlayModes.ToDictionary(pm => pm.Id, pm => (pm.Name, Color: ToHex(pm.Color)));

            // Play modes that count as match (booking subscription, badges, leaderboard)
            var matchPlayModeIds = club.PlayModes.Where(pm => pm.IsChargingBookingSubscription).Select(pm => pm.Id).ToHashSet();

            // Opening hours and prime time are stored in UTC, bookings are evaluated in local time
            int LocalHour(TimeOnly utcTime, bool isEnd)
            {
                var hour = localTime.ToLocal(new DateTimeOffset(today.ToDateTime(utcTime), TimeSpan.Zero), clubTimeZoneId).Hour;
                return isEnd && hour == 0 ? 24 : hour;
            }

            var primeTime = club.PrimeTimeSettings;
            var calculator = new OccupancyCalculator(
                courts.Select(c => (c.Id, c.Name, c.SortOrder)).ToList(),
                LocalHour(club.OpeningHours.From, false),
                LocalHour(club.OpeningHours.To, true),
                primeTime.IsEnabled,
                LocalHour(primeTime.PrimeTimeHours.From, false),
                LocalHour(primeTime.PrimeTimeHours.To, true),
                primeTime.ApplicableWeekdays);

            var seasonResults = new List<ClubSeasonStatisticsResult>();
            var seasonSummaries = new List<ClubSeasonSummary>();
            Season? previousSeason = null;

            foreach (var season in seasons)
            {
                var seasonBookings = GetSeasonBookings(allBookings, season);
                var effectiveEnd = Min(season.Period.To, today);
                var playedBookings = seasonBookings.Where(b => DateOnly.FromDateTime(b.From) <= effectiveEnd).ToList();
                var elapsedDays = Math.Max(0, effectiveEnd.DayNumber - season.Period.From.DayNumber + 1);
                var days = DaysBetween(season.Period.From, effectiveEnd);

                var seasonMemberIds = MemberIdsOf(memberSeasons, season.Id);
                var previousMemberIds = previousSeason is null ? null : MemberIdsOf(memberSeasons, previousSeason.Id);

                var occupancy = calculator.Calculate(playedBookings, days);
                var demographics = BuildMemberDemographics(memberUsers, seasonMemberIds, previousMemberIds, season);
                var playStats = BuildPlayStats(playedBookings, seasonBookings.Count - playedBookings.Count, days, playModes, matchPlayModeIds, calculator, season.Period.From, effectiveEnd);
                var memberActivity = BuildMemberActivity(playedBookings, seasonMemberIds, previousMemberIds, players, matchPlayModeIds);

                var state = season.Period.From > today ? SeasonState.Upcoming : season.Period.To < today ? SeasonState.Past : SeasonState.Current;
                var label = SeasonLabel(season);

                seasonResults.Add(new ClubSeasonStatisticsResult
                {
                    SeasonId = season.Id,
                    StartDate = season.Period.From,
                    EndDate = season.Period.To,
                    SeasonLabel = label,
                    State = state,
                    SeasonLengthDays = season.Period.To.DayNumber - season.Period.From.DayNumber + 1,
                    ElapsedDays = elapsedDays,
                    Occupancy = occupancy,
                    MemberDemographics = demographics,
                    PlayStats = playStats,
                    MemberActivity = memberActivity,
                    PreviousSeasonPace = previousSeason is null ? null : BuildPace(allBookings, previousSeason, elapsedDays),
                });

                seasonSummaries.Add(new ClubSeasonSummary
                {
                    SeasonId = season.Id,
                    SeasonLabel = label,
                    State = state,
                    EnrolledMembers = memberActivity.EnrolledMembers,
                    ActivePlayers = memberActivity.ActivePlayers,
                    ActivationRate = memberActivity.ActivationRate,
                    Bookings = playStats.TotalBookings,
                    Hours = playStats.TotalPlaytimeHours,
                    OccupancyPercentage = occupancy.OverallPercentage,
                    NewMembers = memberActivity.NewMembers,
                    ReturningMembers = memberActivity.ReturningMembers,
                    LapsedMembers = memberActivity.LapsedMembers,
                    Demographics = demographics,
                });

                previousSeason = season;
            }

            // Newest season first
            seasonResults.Reverse();

            return new GetClubStatisticsResult
            {
                SeasonStatistics = seasonResults,
                AllTime = BuildAllTime(seasons, allBookings, memberSeasons, players, playModes, matchPlayModeIds, calculator, seasonSummaries, today),
                Today = today,
                OpeningHourFrom = calculator.OpeningHourFrom,
                OpeningHourTo = calculator.OpeningHourTo,
                PrimeTime = primeTime.IsEnabled
                    ? new ClubPrimeTimeInfo
                    {
                        FromHour = calculator.PrimeTimeFrom,
                        ToHour = calculator.PrimeTimeTo,
                        Weekdays = primeTime.ApplicableWeekdays.ToList(),
                    }
                    : null,
            };
        }

        private static ClubAllTimeStatisticsResult BuildAllTime(
            List<Season> seasons,
            List<BookingData> allBookings,
            List<MemberSeasonData> memberSeasons,
            Dictionary<int, PlayerData> players,
            Dictionary<int, (string Name, string Color)> playModes,
            HashSet<int> matchPlayModeIds,
            OccupancyCalculator calculator,
            List<ClubSeasonSummary> seasonSummaries,
            DateOnly today)
        {
            var days = seasons
                .SelectMany(s => DaysBetween(s.Period.From, Min(s.Period.To, today)))
                .Distinct()
                .ToList();

            var playedBookings = seasons
                .SelectMany(s => GetSeasonBookings(allBookings, s))
                .DistinctBy(b => b.Id)
                .Where(b => DateOnly.FromDateTime(b.From) <= today)
                .ToList();

            return new ClubAllTimeStatisticsResult
            {
                Totals = new ClubAllTimeTotals
                {
                    Bookings = playedBookings.Count,
                    Hours = Math.Round(playedBookings.Sum(b => b.DurationHours), 1),
                    DistinctPlayers = playedBookings.SelectMany(b => b.PlayerIds).Distinct().Count(),
                    DistinctMembers = memberSeasons.Select(ms => ms.MemberId).Distinct().Count(),
                    Seasons = seasons.Count(s => s.Period.From <= today),
                    FirstSeasonStart = seasons.FirstOrDefault()?.Period.From,
                    AveragePlayersPerBooking = AveragePlayers(playedBookings),
                },
                Occupancy = calculator.Calculate(playedBookings, days),
                Seasons = seasonSummaries,
                PlayModeUsage = BuildPlayModeUsage(playedBookings, playModes),
                WeekdayActivity = BuildWeekdayActivity(playedBookings),
                TopPlayers = BuildTopPlayers(playedBookings, players),
                TopMatchPlayers = BuildTopPlayers(Matches(playedBookings, matchPlayModeIds), players),
                BookingBehavior = BuildBookingBehavior(playedBookings, calculator),
                MatchBookingBehavior = BuildBookingBehavior(Matches(playedBookings, matchPlayModeIds), calculator),
            };
        }

        private static List<BookingData> Matches(List<BookingData> bookings, HashSet<int> matchPlayModeIds)
            => bookings.Where(b => matchPlayModeIds.Contains(b.PlayModeId)).ToList();

        private static ClubBookingBehaviorResult BuildBookingBehavior(List<BookingData> bookings, OccupancyCalculator calculator)
        {
            var totalPlaytime = bookings.Sum(b => b.DurationHours);
            var primeTimeHours = calculator.PrimeTimeHours(bookings);

            return new ClubBookingBehaviorResult
            {
                Bookings = bookings.Count,
                RecurringBookings = bookings.Count(b => b.IsRecurring),
                PrimeTimeHoursShare = primeTimeHours is null ? null : totalPlaytime > 0 ? Math.Round(primeTimeHours.Value / totalPlaytime * 100, 1) : 0,
                LeadTimes = BuildLeadTimes(bookings),
            };
        }

        private static List<BookingData> GetSeasonBookings(List<BookingData> bookings, Season season)
            => bookings
                .Where(b =>
                {
                    var date = DateOnly.FromDateTime(b.From);
                    return date >= season.Period.From && date <= season.Period.To;
                })
                .ToList();

        private static HashSet<int> MemberIdsOf(List<MemberSeasonData> memberSeasons, int seasonId)
            => memberSeasons.Where(ms => ms.SeasonId == seasonId).Select(ms => ms.MemberId).ToHashSet();

        private static List<DateOnly> DaysBetween(DateOnly from, DateOnly to)
        {
            var days = new List<DateOnly>();
            for (var day = from; day <= to; day = day.AddDays(1))
                days.Add(day);
            return days;
        }

        private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

        private static string SeasonLabel(Season season)
            => season.Period.From.Year == season.Period.To.Year
                ? season.Period.From.Year.ToString(CultureInfo.InvariantCulture)
                : $"{season.Period.From.Year}/{season.Period.To:yy}";

        private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private static double AveragePlayers(List<BookingData> bookings)
        {
            var withPlayers = bookings.Where(b => b.PlayerIds.Count > 0).ToList();
            return withPlayers.Count == 0 ? 0 : Math.Round(withPlayers.Average(b => b.PlayerIds.Count), 1);
        }

        private static SeasonPaceComparison BuildPace(List<BookingData> allBookings, Season previousSeason, int elapsedDays)
        {
            var bookings = GetSeasonBookings(allBookings, previousSeason);
            var cutoff = previousSeason.Period.From.AddDays(elapsedDays);
            var atSamePoint = bookings.Where(b => DateOnly.FromDateTime(b.From) < cutoff).ToList();

            return new SeasonPaceComparison
            {
                SeasonLabel = SeasonLabel(previousSeason),
                HoursAtSamePoint = Math.Round(atSamePoint.Sum(b => b.DurationHours), 1),
                BookingsAtSamePoint = atSamePoint.Count,
                ActivePlayersAtSamePoint = atSamePoint.SelectMany(b => b.PlayerIds).Distinct().Count(),
                TotalHours = Math.Round(bookings.Sum(b => b.DurationHours), 1),
            };
        }

        private static MemberDemographicsResult BuildMemberDemographics(
            List<MemberUserData> memberUsers,
            HashSet<int> seasonMemberIds,
            HashSet<int>? previousMemberIds,
            Season season)
        {
            var seasonMembers = memberUsers.Where(m => seasonMemberIds.Contains(m.MemberId)).ToList();

            // Use the middle of the season as the reference date for age calculation
            var referenceDate = season.Period.From.AddDays((season.Period.To.DayNumber - season.Period.From.DayNumber) / 2);

            int maleKids = 0, femaleKids = 0, maleTeenagers = 0, femaleTeenagers = 0;
            int maleMembers = 0, femaleMembers = 0, maleSeniors = 0, femaleSeniors = 0;
            var ages = new List<int>();

            foreach (var member in seasonMembers)
            {
                var age = CalculateAge(member.Birthday, referenceDate);
                ages.Add(age);
                var isMale = member.Gender == Gender.Male;

                switch (age)
                {
                    case <= 12:
                        if (isMale)
                            maleKids++;
                        else
                            femaleKids++;
                        break;
                    case <= 17:
                        if (isMale)
                            maleTeenagers++;
                        else
                            femaleTeenagers++;
                        break;
                    case <= 34:
                        if (isMale)
                            maleMembers++;
                        else
                            femaleMembers++;
                        break;
                    default:
                        if (isMale)
                            maleSeniors++;
                        else
                            femaleSeniors++;
                        break;
                }
            }

            return new MemberDemographicsResult
            {
                MaleKids = maleKids,
                FemaleKids = femaleKids,
                MaleTeenagers = maleTeenagers,
                FemaleTeenagers = femaleTeenagers,
                MaleMembers = maleMembers,
                FemaleMembers = femaleMembers,
                MaleSeniors = maleSeniors,
                FemaleSeniors = femaleSeniors,
                NewMembers = previousMemberIds is null ? seasonMemberIds.Count : seasonMemberIds.Count(id => !previousMemberIds.Contains(id)),
                AverageAge = ages.Count == 0 ? null : Math.Round(ages.Average(), 1),
            };
        }

        private static int CalculateAge(DateOnly birthday, DateOnly referenceDate)
        {
            var age = referenceDate.Year - birthday.Year;
            if (birthday.AddYears(age) > referenceDate)
                age--;
            return age;
        }

        private static ClubPlayStatsResult BuildPlayStats(
            List<BookingData> bookings,
            int upcomingBookings,
            List<DateOnly> days,
            Dictionary<int, (string Name, string Color)> playModes,
            HashSet<int> matchPlayModeIds,
            OccupancyCalculator calculator,
            DateOnly seasonStart,
            DateOnly effectiveEnd)
        {
            var totalPlaytime = bookings.Sum(b => b.DurationHours);

            var perDay = bookings
                .GroupBy(b => DateOnly.FromDateTime(b.From))
                .ToDictionary(g => g.Key, g => (Hours: g.Sum(b => b.DurationHours), Bookings: g.Count()));

            var busiestDay = perDay
                .OrderByDescending(d => d.Value.Hours)
                .ThenBy(d => d.Key)
                .Select(d => new BusiestDayEntry { Date = d.Key, Hours = Math.Round(d.Value.Hours, 1), Bookings = d.Value.Bookings })
                .FirstOrDefault();

            return new ClubPlayStatsResult
            {
                TotalBookings = bookings.Count,
                TotalPlaytimeHours = Math.Round(totalPlaytime, 1),
                UpcomingBookings = upcomingBookings,
                RecurringBookings = bookings.Count(b => b.IsRecurring),
                AveragePlayersPerBooking = AveragePlayers(bookings),
                AverageHoursPerDay = days.Count == 0 ? 0 : Math.Round(totalPlaytime / days.Count, 1),
                BusiestDay = busiestDay,
                DaysWithoutBookings = days.Count(d => !perDay.ContainsKey(d)),
                DaysUnder3Hours = perDay.Count(d => d.Value.Hours < 3),
                Days3To5Hours = perDay.Count(d => d.Value.Hours is >= 3 and < 5),
                Days5To10Hours = perDay.Count(d => d.Value.Hours is >= 5 and < 10),
                DaysOver10Hours = perDay.Count(d => d.Value.Hours >= 10),
                PlayModeUsage = BuildPlayModeUsage(bookings, playModes),
                WeekdayActivity = BuildWeekdayActivity(bookings),
                WeeklyActivity = BuildWeeklyActivity(bookings, seasonStart, effectiveEnd),
                BookingBehavior = BuildBookingBehavior(bookings, calculator),
                MatchBookingBehavior = BuildBookingBehavior(Matches(bookings, matchPlayModeIds), calculator),
            };
        }

        private static List<PlayModeUsageEntry> BuildPlayModeUsage(List<BookingData> bookings, Dictionary<int, (string Name, string Color)> playModes)
            => bookings
                .GroupBy(b => b.PlayModeId)
                .Select(g =>
                {
                    var found = playModes.TryGetValue(g.Key, out var playMode);
                    return new PlayModeUsageEntry
                    {
                        Name = found ? playMode.Name : $"Mode {g.Key}",
                        Color = found ? playMode.Color : "#9E9E9E",
                        Hours = Math.Round(g.Sum(b => b.DurationHours), 1),
                        Bookings = g.Count(),
                    };
                })
                .OrderByDescending(p => p.Hours)
                .ToList();

        private static List<WeekdayActivityEntry> BuildWeekdayActivity(List<BookingData> bookings)
        {
            var byWeekday = bookings.GroupBy(b => b.From.DayOfWeek).ToDictionary(g => g.Key);
            return OccupancyCalculator.WeekdaysMondayFirst
                .Select(day => new WeekdayActivityEntry
                {
                    DayOfWeek = day,
                    Bookings = byWeekday.TryGetValue(day, out var group) ? group.Count() : 0,
                    Hours = group is null ? 0 : Math.Round(group.Sum(b => b.DurationHours), 1),
                })
                .ToList();
        }

        private static List<WeeklyActivityEntry> BuildWeeklyActivity(List<BookingData> bookings, DateOnly seasonStart, DateOnly effectiveEnd)
        {
            var weeks = new List<WeeklyActivityEntry>();
            if (effectiveEnd < seasonStart)
                return weeks;

            var byWeek = bookings.GroupBy(b => WeekStart(DateOnly.FromDateTime(b.From))).ToDictionary(g => g.Key);
            for (var week = WeekStart(seasonStart); week <= effectiveEnd; week = week.AddDays(7))
            {
                byWeek.TryGetValue(week, out var group);
                weeks.Add(new WeeklyActivityEntry
                {
                    WeekStart = week,
                    Bookings = group?.Count() ?? 0,
                    Hours = group is null ? 0 : Math.Round(group.Sum(b => b.DurationHours), 1),
                    ActivePlayers = group?.SelectMany(b => b.PlayerIds).Distinct().Count() ?? 0,
                });
            }

            return weeks;
        }

        private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        private static List<LeadTimeBucket> BuildLeadTimes(List<BookingData> bookings)
        {
            // Recurring occurrences are created together with the series, their lead time says nothing about booking behavior
            var counts = bookings
                .Where(b => b.LeadTime is not null)
                .GroupBy(b => b.LeadTime!.Value.TotalDays switch
                {
                    < 1 => LeadTimeCategory.LessThanADay,
                    < 3 => LeadTimeCategory.OneToTwoDays,
                    < 8 => LeadTimeCategory.ThreeToSevenDays,
                    _ => LeadTimeCategory.MoreThanAWeek,
                })
                .ToDictionary(g => g.Key, g => g.Count());

            return Enum.GetValues<LeadTimeCategory>()
                .Select(category => new LeadTimeBucket { Category = category, Bookings = counts.GetValueOrDefault(category) })
                .ToList();
        }

        private static ClubMemberActivityResult BuildMemberActivity(
            List<BookingData> bookings,
            HashSet<int> seasonMemberIds,
            HashSet<int>? previousMemberIds,
            Dictionary<int, PlayerData> players,
            HashSet<int> matchPlayModeIds)
        {
            var bookingsPerMember = bookings
                .SelectMany(b => b.PlayerIds)
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var activePlayers = seasonMemberIds.Count(bookingsPerMember.ContainsKey);
            var guestPlayers = bookingsPerMember.Keys.Count(id => players.TryGetValue(id, out var player) && player.IsGuest);

            var buckets = seasonMemberIds
                .GroupBy(id => bookingsPerMember.GetValueOrDefault(id) switch
                {
                    0 => ActivityBucketCategory.None,
                    1 => ActivityBucketCategory.One,
                    <= 5 => ActivityBucketCategory.TwoToFive,
                    <= 10 => ActivityBucketCategory.SixToTen,
                    <= 20 => ActivityBucketCategory.ElevenToTwenty,
                    _ => ActivityBucketCategory.MoreThanTwenty,
                })
                .ToDictionary(g => g.Key, g => g.Count());

            var returning = previousMemberIds is null ? 0 : seasonMemberIds.Count(previousMemberIds.Contains);

            return new ClubMemberActivityResult
            {
                EnrolledMembers = seasonMemberIds.Count,
                ActivePlayers = activePlayers,
                ActivationRate = seasonMemberIds.Count == 0 ? 0 : Math.Round((double)activePlayers / seasonMemberIds.Count * 100, 1),
                GuestPlayers = guestPlayers,
                ReturningMembers = returning,
                NewMembers = seasonMemberIds.Count - returning,
                LapsedMembers = previousMemberIds?.Count(id => !seasonMemberIds.Contains(id)) ?? 0,
                BookingsPerPlayer = Enum.GetValues<ActivityBucketCategory>()
                    .Select(category => new ActivityBucket { Category = category, Members = buckets.GetValueOrDefault(category) })
                    .ToList(),
                TopPlayers = BuildTopPlayers(bookings, players),
                TopMatchPlayers = BuildTopPlayers(Matches(bookings, matchPlayModeIds), players),
            };
        }

        private static List<TopPlayerEntry> BuildTopPlayers(List<BookingData> bookings, Dictionary<int, PlayerData> players)
            => bookings
                .SelectMany(b => b.PlayerIds.Select(id => (MemberId: id, b.DurationHours)))
                .GroupBy(x => x.MemberId)
                .Select(g => (MemberId: g.Key, Hours: g.Sum(x => x.DurationHours), Bookings: g.Count()))
                .Where(x => players.ContainsKey(x.MemberId))
                .OrderByDescending(x => x.Hours)
                .ThenByDescending(x => x.Bookings)
                .ThenBy(x => x.MemberId)
                .Take(TopPlayerCount)
                .Select(x =>
                {
                    var player = players[x.MemberId];
                    return new TopPlayerEntry
                    {
                        MemberId = x.MemberId,
                        FirstName = player.FirstName,
                        LastName = player.LastName,
                        ProfilePictureUrl = player.ProfilePictureUrl,
                        IsGuest = player.IsGuest,
                        Bookings = x.Bookings,
                        Hours = Math.Round(x.Hours, 1),
                    };
                })
                .ToList();
    }

    /// <summary>
    /// Calculates how much of the opening hours the courts were booked. Every hour slot of a court and day counts
    /// with the minutes that are covered by bookings (at most 60, overbooked slots are not counted twice).
    /// </summary>
    /// <remarks>All hours are local hours of the club.</remarks>
    private sealed class OccupancyCalculator(
        List<(int Id, string Name, int SortOrder)> courts,
        int openingHourFrom,
        int openingHourTo,
        bool primeTimeEnabled,
        int primeTimeFrom,
        int primeTimeTo,
        DayOfWeek[] primeTimeWeekdays)
    {
        public static readonly DayOfWeek[] WeekdaysMondayFirst =
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

        public int OpeningHourFrom => openingHourFrom;
        public int OpeningHourTo => openingHourTo;
        public int PrimeTimeFrom => primeTimeFrom;
        public int PrimeTimeTo => primeTimeTo;

        public ClubOccupancyResult Calculate(List<BookingData> bookings, List<DateOnly> days)
        {
            var dayCount = days.Count;
            var daySet = days.ToHashSet();
            var slots = BookedMinutesPerSlot(bookings.Where(b => daySet.Contains(DateOnly.FromDateTime(b.From))));
            var hoursPerDay = Math.Max(0, openingHourTo - openingHourFrom);

            var byCourtHour = slots.GroupBy(s => (s.Key.CourtId, s.Key.Hour)).ToDictionary(g => g.Key, g => g.Sum(s => s.Value) / 60d);
            var byWeekdayHour = slots.GroupBy(s => (s.Key.Date.DayOfWeek, s.Key.Hour)).ToDictionary(g => g.Key, g => g.Sum(s => s.Value) / 60d);

            double BookedHours(Func<(int CourtId, DateOnly Date, int Hour), bool> predicate)
                => slots.Where(s => predicate(s.Key)).Sum(s => s.Value) / 60d;

            double Percentage(double booked, double capacity) => capacity > 0 ? Math.Round(booked / capacity * 100, 1) : 0;

            var courtHeatmaps = courts.Select(court =>
            {
                var hours = Hours()
                    .Select(hour =>
                    {
                        var booked = byCourtHour.GetValueOrDefault((court.Id, hour));
                        return new HeatmapHourResult { Hour = hour, BookedHours = Math.Round(booked, 1), OccupancyPercentage = Percentage(booked, dayCount) };
                    })
                    .ToList();

                var courtBooked = hours.Sum(h => byCourtHour.GetValueOrDefault((court.Id, h.Hour)));
                return new CourtHeatmapResult
                {
                    CourtName = court.Name,
                    CourtSortOrder = court.SortOrder,
                    BookedHours = Math.Round(courtBooked, 1),
                    OccupancyPercentage = Percentage(courtBooked, (double)dayCount * hoursPerDay),
                    Hours = hours,
                };
            }).ToList();

            var weekdayHeatmaps = WeekdaysMondayFirst.Select(weekday =>
            {
                var capacity = (double)days.Count(d => d.DayOfWeek == weekday) * courts.Count;
                var hours = Hours()
                    .Select(hour =>
                    {
                        var booked = byWeekdayHour.GetValueOrDefault((weekday, hour));
                        return new HeatmapHourResult { Hour = hour, BookedHours = Math.Round(booked, 1), OccupancyPercentage = Percentage(booked, capacity) };
                    })
                    .ToList();

                return new WeekdayHeatmapResult
                {
                    DayOfWeek = weekday,
                    OccupancyPercentage = Percentage(Hours().Sum(hour => byWeekdayHour.GetValueOrDefault((weekday, hour))), capacity * hoursPerDay),
                    Hours = hours,
                };
            }).ToList();

            double? primeTimePercentage = null;
            if (primeTimeEnabled)
            {
                var primeDays = days.Count(d => primeTimeWeekdays.Contains(d.DayOfWeek));
                var primeHours = Hours().Count(IsPrimeTimeHour);
                primeTimePercentage = Percentage(
                    BookedHours(s => primeTimeWeekdays.Contains(s.Date.DayOfWeek) && IsPrimeTimeHour(s.Hour)),
                    (double)primeDays * primeHours * courts.Count);
            }

            return new ClubOccupancyResult
            {
                Days = dayCount,
                OverallPercentage = Percentage(slots.Sum(s => s.Value) / 60d, (double)dayCount * hoursPerDay * courts.Count),
                PrimeTimePercentage = primeTimePercentage,
                CourtHeatmaps = courtHeatmaps,
                WeekdayHeatmaps = weekdayHeatmaps,
            };
        }

        /// <summary>Played hours during prime time, null if prime time is disabled.</summary>
        public double? PrimeTimeHours(List<BookingData> bookings)
        {
            if (!primeTimeEnabled)
                return null;

            var minutes = bookings
                .Where(b => primeTimeWeekdays.Contains(b.From.DayOfWeek))
                .Sum(b => OverlapMinutes(b, b.From.Date.AddHours(primeTimeFrom), b.From.Date.AddHours(primeTimeTo)));
            return minutes / 60d;
        }

        private bool IsPrimeTimeHour(int hour) => hour >= primeTimeFrom && hour < primeTimeTo;

        private IEnumerable<int> Hours() => Enumerable.Range(openingHourFrom, Math.Max(0, openingHourTo - openingHourFrom));

        private Dictionary<(int CourtId, DateOnly Date, int Hour), double> BookedMinutesPerSlot(IEnumerable<BookingData> bookings)
        {
            var slots = new Dictionary<(int CourtId, DateOnly Date, int Hour), double>();
            foreach (var booking in bookings)
            {
                var date = DateOnly.FromDateTime(booking.From);
                foreach (var hour in Hours())
                {
                    var slotStart = booking.From.Date.AddHours(hour);
                    var minutes = OverlapMinutes(booking, slotStart, slotStart.AddHours(1));
                    if (minutes <= 0)
                        continue;

                    var key = (booking.CourtId, date, hour);
                    slots[key] = Math.Min(60, slots.GetValueOrDefault(key) + minutes);
                }
            }

            return slots;
        }

        private static double OverlapMinutes(BookingData booking, DateTime from, DateTime to)
        {
            var start = booking.From > from ? booking.From : from;
            var end = booking.To < to ? booking.To : to;
            return end > start ? (end - start).TotalMinutes : 0;
        }
    }

    // From/To are in the local time of the booking; DurationHours comes from the UTC interval (correct across DST changes)
    private sealed record BookingData(
        int Id,
        DateTime From,
        DateTime To,
        double DurationHours,
        int CourtId,
        int PlayModeId,
        bool IsRecurring,
        TimeSpan? LeadTime,
        List<int> PlayerIds);

    private sealed record MemberUserData(int MemberId, DateOnly Birthday, Gender Gender);
    private sealed record MemberSeasonData(int MemberId, int SeasonId);
    private sealed record PlayerData(int MemberId, string FirstName, string LastName, bool IsGuest, string? ProfilePictureUrl);
}
