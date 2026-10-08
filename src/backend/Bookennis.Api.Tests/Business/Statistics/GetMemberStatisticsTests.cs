using Bookennis.Api.Business.Statistics;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Statistics;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Statistics;

public class GetMemberStatisticsTests(TestFixture fixture) : TestBase(fixture)
{
    private int ClubId => Query(ctx => ctx.TestData().Club.Id);

    [Fact]
    public async Task GetMemberStatistics_NoBookings_ReturnsEmptyStatistics()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        result.AllTime.TotalBookings.Should().Be(0);
        result.AllTime.TotalHoursOnCourt.Should().Be(0);
        result.AllTime.CourtUsage.Should().BeEmpty();
        result.AllTime.PartnerUsage.Should().BeEmpty();
        result.AllTime.PlayModeUsage.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberStatistics_WithBookings_ReturnsCorrectStatistics()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddMinutes(90)), TimeZoneInfo.Utc.Id, [member1.Id, member2.Id]));

            var from2 = new DateTimeOffset(2025, 6, 2, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from2, from2.AddMinutes(90)), TimeZoneInfo.Utc.Id, [member1.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        result.AllTime.TotalBookings.Should().Be(2);
        result.AllTime.TotalHoursOnCourt.Should().Be(3.0);
        result.AllTime.CourtUsage.Should().HaveCount(1);
        result.AllTime.PlayModeUsage.Should().HaveCount(1);
        result.AllTime.PartnerUsage.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMemberStatistics_OnlyTracksChargingPlayModes()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;

            var chargingPlayMode = club.PlayModes[0];
            chargingPlayMode.IsChargingBookingSubscription.Should().BeTrue();

            var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, chargingPlayMode.Id, new DateTimeOffsetInterval(from, from.AddMinutes(90)), TimeZoneInfo.Utc.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        result.AllTime.TotalBookings.Should().Be(1);
    }

    [Fact]
    public async Task GetMemberStatistics_SeasonActivation_ShowsCorrectState()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(member1.Id, season.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        result.SeasonStatistics.Should().NotBeEmpty();
        var activatedSeason = result.SeasonStatistics.FirstOrDefault(s => s.IsActivated);
        activatedSeason.Should().NotBeNull();
        activatedSeason!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMemberStatistics_NonActivatedSeason_HasNullData()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        var nonActivated = result.SeasonStatistics.FirstOrDefault(s => !s.IsActivated);
        nonActivated.Should().NotBeNull();
        nonActivated!.Data.Should().BeNull();
        nonActivated.ClubComparison.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberStatistics_PlayRhythm_UsesLocalTimeOfBooking()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            // Tuesdays at 19:00 in Vienna (17:00 UTC in summer), and one Thursday at 08:00 (06:00 UTC)
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 3, 17, 0, 0, TimeSpan.Zero), ViennaTimeZoneId, member1.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 10, 17, 0, 0, TimeSpan.Zero), ViennaTimeZoneId, member1.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 17, 17, 0, 0, TimeSpan.Zero), ViennaTimeZoneId, member1.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 19, 6, 0, 0, TimeSpan.Zero), ViennaTimeZoneId, member1.Id);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        var data = result.AllTime;
        data.WeekdayCounts.Should().Equal(0, 3, 0, 1, 0, 0, 0);
        data.HourCounts.Should().HaveCount(24);
        data.HourCounts[19].Should().Be(3);
        data.HourCounts[8].Should().Be(1);
        data.HourCounts.Sum().Should().Be(4);
        data.PlayerType.Should().Be(PlayerType.NightOwl);
    }

    [Fact]
    public async Task GetMemberStatistics_PlayerType_WeekendWarriorAndTooFewBookings()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;
            ctx.Add(new MemberSeason(member1.Id, season.Id));

            // Saturday and Sunday mornings in the season
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 7, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 8, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 14, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);

            // Only 2 bookings in 2024
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2024, 6, 7, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        result.SeasonStatistics.Single(s => s.SeasonId == seasonId).Data!.PlayerType.Should().Be(PlayerType.WeekendWarrior);
        result.AllTime.PlayerType.Should().Be(PlayerType.WeekendWarrior);

        // A season with fewer than 3 bookings has no player type
        var fewBookings = await QueryAsync(async ctx =>
        {
            var season = new Season(ctx.TestData().Club.Id, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            ctx.Add(new MemberSeason(ctx.TestData().Member1.Id, season.Id));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        result = await SendAsync(new GetMemberStatistics(userId, ClubId));
        var fewBookingsData = result.SeasonStatistics.Single(s => s.SeasonId == fewBookings).Data!;
        fewBookingsData.TotalBookings.Should().Be(1);
        fewBookingsData.PlayerType.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberStatistics_StreaksAndRecords_AreCalculated()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            // Weeks of June 2, 9 and 16 (3-week streak, two games in the week of June 9), then a break until June 30
            foreach (var day in new[] { 3, 10, 12, 17, 30 })
                AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, day, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);

            // July: one game
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 7, 20, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        var data = result.AllTime;
        data.LongestWeekStreak.Should().Be(3);
        data.CurrentWeekStreak.Should().Be(0);
        data.LongestBreakDays.Should().Be(20); // June 30 -> July 20
        data.BestWeek.Should().BeEquivalentTo(new WeekRecord { WeekStart = new DateOnly(2025, 6, 9), Count = 2 });
        data.BusiestMonth.Should().BeEquivalentTo(new MonthRecord { Year = 2025, Month = 6, Count = 5 });
        data.FirstBooking.Should().Be(new DateOnly(2025, 6, 3));
        data.LastBooking.Should().Be(new DateOnly(2025, 7, 20));
        data.ActivityDays.Should().HaveCount(6);
        data.ActivityDays.Should().OnlyContain(d => d.Count == 1);
    }

    [Fact]
    public async Task GetMemberStatistics_CurrentWeekStreak_CountsBackFromThisWeek()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            // Played in the previous two weeks, not (yet) in the current week: the streak is still alive
            var monday = CalendarWeeks.GetMonday(DateOnly.FromDateTime(DateTime.UtcNow));
            foreach (var weeksAgo in new[] { 1, 2, 4 })
            {
                var date = monday.AddDays(-7 * weeksAgo + 2);
                var from = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
                AddBooking(ctx, club.Id, court.Id, playMode.Id, from, TimeZoneInfo.Utc.Id, member1.Id);
            }

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        result.AllTime.CurrentWeekStreak.Should().Be(2);
        result.AllTime.LongestWeekStreak.Should().Be(2);
    }

    [Fact]
    public async Task GetMemberStatistics_Partners_AreRankedWithProfileData()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var (member2Id, member3Id) = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            var member3 = await AddClubMember(ctx, club.Id, "Third", "Partner");

            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id, member2.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 2, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id, member2.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 3, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id, member3.Id);
            await ctx.SaveChangesAsync();

            return (member2.Id, member3.Id);
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        var data = result.AllTime;
        data.DistinctPartners.Should().Be(2);
        data.PartnerUsage.Select(p => (p.MemberId, p.Count)).Should().Equal((member2Id, 2), (member3Id, 1));
        data.PartnerUsage[1].FirstName.Should().Be("Third");
        data.PartnerUsage[1].LastName.Should().Be("Partner");
        data.PartnerUsage[1].ProfilePictureUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberStatistics_ClubComparison_ComparesWithEnrolledMembers()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            var member3 = await AddClubMember(ctx, club.Id, "Lazy", "Member");
            var notEnrolled = await AddClubMember(ctx, club.Id, "Not", "Enrolled");

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;

            ctx.Add(new MemberSeason(member1.Id, season.Id));
            ctx.Add(new MemberSeason(member2.Id, season.Id));
            ctx.Add(new MemberSeason(member3.Id, season.Id));

            // Member 1: 3 games, member 2: 1 game, member 3: none, not enrolled member: 5 games (ignored)
            for (var day = 1; day <= 3; day++)
                AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, day, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member1.Id);
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, 4, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member2.Id);
            for (var day = 10; day < 15; day++)
                AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 6, day, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, notEnrolled.Id);

            // Outside of the season (ignored)
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 7, 4, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc.Id, member2.Id);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        var comparison = result.SeasonStatistics.Single(s => s.SeasonId == seasonId).ClubComparison;
        comparison.Should().NotBeNull();
        comparison!.EnrolledMembers.Should().Be(3);
        comparison.ClubAverageBookings.Should().Be(1.3); // (3 + 1 + 0) / 3
        comparison.BetterThanPercentage.Should().Be(100);
    }

    [Fact]
    public async Task GetMemberStatistics_SeasonBoundary_UsesLocalDate()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;
            ctx.Add(new MemberSeason(member1.Id, season.Id));

            // 2025-06-01 00:30 in Vienna is 2025-05-31 22:30 UTC
            AddBooking(ctx, club.Id, court.Id, playMode.Id, new DateTimeOffset(2025, 5, 31, 22, 30, 0, TimeSpan.Zero), ViennaTimeZoneId, member1.Id);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberStatistics(userId, ClubId));

        var data = result.SeasonStatistics.Single(s => s.SeasonId == seasonId).Data!;
        data.TotalBookings.Should().Be(1);
        data.ActivityDays.Single().Date.Should().Be(new DateOnly(2025, 6, 1));
        data.WeekdayCounts[6].Should().Be(1); // Sunday
    }

    private const string ViennaTimeZoneId = "Europe/Vienna";

    private static void AddBooking(AppDbContext ctx, int clubId, int courtId, int playModeId, DateTimeOffset from, string timeZoneInfoId, params int[] memberIds)
        => ctx.Add(new Booking(clubId, courtId, playModeId, new DateTimeOffsetInterval(from, from.AddHours(1)), timeZoneInfoId, [.. memberIds]));

    private static async Task<ClubMember> AddClubMember(AppDbContext ctx, int clubId, string firstName, string lastName)
    {
        var user = new User(firstName, lastName, new DateOnly(1990, 1, 1), Gender.Male, TestDataSeed.UserId);
        ctx.Add(user);
        await ctx.SaveChangesAsync();

        var member = new ClubMember(user.Id, clubId, [MemberRole.User]);
        ctx.Add(member);
        await ctx.SaveChangesAsync();
        return member;
    }
}
