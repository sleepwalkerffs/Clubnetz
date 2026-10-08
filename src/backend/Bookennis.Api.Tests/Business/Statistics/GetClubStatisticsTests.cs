using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Business.Statistics;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Statistics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Statistics;

public class GetClubStatisticsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetClubStatistics_NoSeasons_ReturnsEmptyStatistics()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        var result = await SendAsync(new GetClubStatistics(clubId));

        result.SeasonStatistics.Should().BeEmpty();
        result.AllTime.Occupancy.CourtHeatmaps.Should().NotBeEmpty();
        result.AllTime.Seasons.Should().BeEmpty();
        result.AllTime.Totals.Bookings.Should().Be(0);
    }

    [Fact]
    public async Task GetClubStatistics_WithSeason_ReturnsSeasonData()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        await QueryAsync(async ctx =>
        {
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        result.SeasonStatistics.Should().HaveCount(1);
        var season = result.SeasonStatistics[0];
        season.StartDate.Should().Be(new DateOnly(2025, 4, 1));
        season.EndDate.Should().Be(new DateOnly(2025, 9, 30));
        season.Occupancy.CourtHeatmaps.Should().NotBeEmpty();
        season.State.Should().Be(SeasonState.Past);
    }

    [Fact]
    public async Task GetClubStatistics_CourtHeatmap_CalculatesOccupancyCorrectly()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;

            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];

            // Book court 1 at 9-10 for 5 out of 10 days
            for (int day = 1; day <= 5; day++)
            {
                var from = new DateTimeOffset(2025, 6, day, 9, 0, 0, TimeSpan.Zero);
                ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id));
            }

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var seasonResult = result.SeasonStatistics.Single(s => s.SeasonId == seasonId);
        var court1Heatmap = seasonResult.Occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1");

        var hour9 = court1Heatmap.Hours.Single(h => h.Hour == 9);
        hour9.OccupancyPercentage.Should().Be(50.0); // 5 out of 10 days
    }

    [Fact]
    public async Task GetClubStatistics_MemberDemographics_CategorizesByAgeAndGender()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        int seasonId = 0;
        await QueryAsync(async ctx =>
        {
            // Season in 2025
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            seasonId = season.Id;

            // Add child (age 8 in 2025) - male
            var childUser = new User("Child", "Boy", new DateOnly(2017, 6, 1), Gender.Male, TestDataSeed.UserId);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();

            var childMember = new ClubMember(childUser.Id, clubId, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(childMember.Id, seasonId));
            await ctx.SaveChangesAsync();

            // Add senior (age 40 in 2025) - female
            var seniorUser = new User("Senior", "Lady", new DateOnly(1985, 6, 1), Gender.Female, TestDataSeed.UserId);
            ctx.Add(seniorUser);
            await ctx.SaveChangesAsync();

            var seniorMember = new ClubMember(seniorUser.Id, clubId, [MemberRole.User]);
            ctx.Add(seniorMember);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberSeason(seniorMember.Id, seasonId));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var seasonResult = result.SeasonStatistics.Single(s => s.SeasonId == seasonId);
        seasonResult.MemberDemographics.MaleKids.Should().Be(1);
        seasonResult.MemberDemographics.FemaleSeniors.Should().Be(1);
    }

    [Fact]
    public async Task GetClubStatistics_NewMembers_CountsCorrectly()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        await QueryAsync(async ctx =>
        {
            // Season 1
            var season1 = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.Add(season1);
            await ctx.SaveChangesAsync();

            // Season 2
            var season2 = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season2);
            await ctx.SaveChangesAsync();

            // Member in both seasons
            var user1 = new User("Existing", "Member", new DateOnly(1990, 1, 1), Gender.Male, TestDataSeed.UserId);
            ctx.Add(user1);
            await ctx.SaveChangesAsync();
            var member1 = new ClubMember(user1.Id, clubId, [MemberRole.User]);
            ctx.Add(member1);
            await ctx.SaveChangesAsync();
            ctx.Add(new MemberSeason(member1.Id, season1.Id));
            ctx.Add(new MemberSeason(member1.Id, season2.Id));
            await ctx.SaveChangesAsync();

            // Member only in season 2 (new)
            var user2 = new User("New", "Member", new DateOnly(1995, 1, 1), Gender.Female, TestDataSeed.UserId);
            ctx.Add(user2);
            await ctx.SaveChangesAsync();
            var member2 = new ClubMember(user2.Id, clubId, [MemberRole.User]);
            ctx.Add(member2);
            await ctx.SaveChangesAsync();
            ctx.Add(new MemberSeason(member2.Id, season2.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        // Seasons are returned newest first
        var season2Result = result.SeasonStatistics[0];
        season2Result.MemberDemographics.NewMembers.Should().Be(1); // only user2 is new
    }

    [Fact]
    public async Task GetClubStatistics_PlayStats_CalculatesPlaytimeCorrectly()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];

            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            // 2 bookings of 1.5 hours each
            var from1 = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from1, from1.AddMinutes(90)), TimeZoneInfo.Utc.Id));

            var from2 = new DateTimeOffset(2025, 6, 2, 14, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from2, from2.AddMinutes(90)), TimeZoneInfo.Utc.Id));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var seasonResult = result.SeasonStatistics[0];
        seasonResult.PlayStats.TotalBookings.Should().Be(2);
        seasonResult.PlayStats.TotalPlaytimeHours.Should().Be(3.0);
        seasonResult.PlayStats.PlayModeUsage.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetClubStatistics_DaysWithoutBookings_CalculatesCorrectly()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];

            // 10-day season
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            // Bookings on 3 distinct days (June 1, 2, 3)
            for (int day = 1; day <= 3; day++)
            {
                var from = new DateTimeOffset(2025, 6, day, 10, 0, 0, TimeSpan.Zero);
                ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id));
            }

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var seasonResult = result.SeasonStatistics[0];
        seasonResult.PlayStats.DaysWithoutBookings.Should().Be(7); // 10 days - 3 days with bookings
        seasonResult.ElapsedDays.Should().Be(10);
        seasonResult.SeasonLengthDays.Should().Be(10);
        seasonResult.PlayStats.DaysUnder3Hours.Should().Be(3); // 3 days with 1h each (< 3h)
        seasonResult.PlayStats.Days3To5Hours.Should().Be(0);
        seasonResult.PlayStats.Days5To10Hours.Should().Be(0);
    }

    [Fact]
    public async Task GetClubStatistics_AllTime_AggregatesAcrossSeasons()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];

            var season1 = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 4, 1), new DateOnly(2024, 9, 30)));
            ctx.Add(season1);
            var season2 = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)));
            ctx.Add(season2);
            await ctx.SaveChangesAsync();

            // Booking in season 1
            var from1 = new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from1, from1.AddHours(1)), TimeZoneInfo.Utc.Id));

            // Booking in season 2
            var from2 = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from2, from2.AddHours(1)), TimeZoneInfo.Utc.Id));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        result.AllTime.Occupancy.CourtHeatmaps.Should().NotBeEmpty();
        result.AllTime.Seasons.Should().HaveCount(2);
        result.AllTime.Seasons.Select(s => s.SeasonLabel).Should().Equal("2024", "2025");
        result.AllTime.Totals.Bookings.Should().Be(2);
        result.AllTime.Totals.Hours.Should().Be(2.0);
        result.AllTime.Occupancy.Days.Should().Be(183 * 2);
    }

    [Fact]
    public async Task GetClubStatistics_RecurringBookingCreatedViaBookRecurringCourt_CountsEveryOccurrence()
    {
        var bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();

        var (clubId, courtId, playModeId, memberId, userId) = await QueryAsync(async ctx =>
        {
            await ctx.RemoveMigrationSeedData();

            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];
            playMode.Update(playMode.AllowedRoles, playMode.Color, playMode.FixedPlayerCount,
                playMode.IsChargingBookingSubscription, playMode.Name, playMode.FixedDuration,
                playMode.CanOverbook, playMode.CommentAllowed, playMode.MaxBookingsPerSeason, allowRecurring: true);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var season = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(60)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var member = ctx.TestData().Member1;
            ctx.Add(new MemberSeason(member.Id, season.Id));
            await ctx.SaveChangesAsync();

            return (club.Id, ctx.TestData().Court1.Id, playMode.Id, member.Id, ctx.TestData().User.Id);
        });

        bookingService.BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(callInfo => new Booking(clubId, courtId, playModeId, callInfo.Arg<DateTimeOffsetInterval>(), TimeZoneInfo.Utc.Id, [memberId]));

        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, now.Day, 10, 0, 0, TimeSpan.Zero).AddDays(1);
        var endDate = DateOnly.FromDateTime(from.DateTime).AddDays(21); // 4 weekly occurrences

        await SendAsync(new BookRecurringCourt(courtId, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, playModeId,
            [memberId], RecurrenceIntervalWeeks: 1, EndDate: endDate, Comment: null, ClientTimeZoneOffset: null, userId));

        var occurrenceCount = await QueryAsync(ctx => ctx.Bookings.CountAsync(b => b.RecurringBookingSeriesId != null));
        occurrenceCount.Should().Be(4);

        // The occurrences start tomorrow, so they only count as played at the end of the season
        var seasonEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        var result = await SendAsync(new GetClubStatistics(clubId, seasonEnd));

        var seasonResult = result.SeasonStatistics.Single();
        var playStats = seasonResult.PlayStats;
        playStats.TotalBookings.Should().Be(4);
        playStats.TotalPlaytimeHours.Should().Be(4.0);
        playStats.RecurringBookings.Should().Be(4);
        playStats.PlayModeUsage.Single().Hours.Should().Be(4.0);
        playStats.DaysWithoutBookings.Should().Be(seasonResult.ElapsedDays - 4);

        // Today nothing has been played yet
        var todayResult = await SendAsync(new GetClubStatistics(clubId));
        todayResult.SeasonStatistics.Single().PlayStats.TotalBookings.Should().Be(0);
        todayResult.SeasonStatistics.Single().PlayStats.UpcomingBookings.Should().Be(4);
        todayResult.SeasonStatistics.Single().State.Should().Be(SeasonState.Current);
    }

    [Fact]
    public async Task GetClubStatistics_RecurringSeries_CountsOccurrencesTogetherWithSingleBookings()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];

            // 10-day season
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10))));
            await ctx.SaveChangesAsync();

            // Recurring booking: 2 hours every Monday (June 2 and June 9)
            var seriesId = await AddWeeklySeries(ctx, clubId, court1Id, playMode.Id, TimeZoneInfo.Utc.Id,
                new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 10), new TimeOnly(10, 0), TimeSpan.FromHours(2));

            // An individually edited occurrence still counts
            var excluded = await ctx.Bookings.Where(b => b.RecurringBookingSeriesId == seriesId).OrderBy(b => b.Interval.From).FirstAsync();
            excluded.ExcludeFromSeries();

            // Single booking on June 2 as well
            var from = new DateTimeOffset(2025, 6, 2, 14, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var seasonResult = result.SeasonStatistics.Single();
        seasonResult.PlayStats.TotalBookings.Should().Be(3);
        seasonResult.PlayStats.TotalPlaytimeHours.Should().Be(5.0);
        seasonResult.PlayStats.DaysWithoutBookings.Should().Be(8);
        seasonResult.PlayStats.DaysUnder3Hours.Should().Be(1); // June 9: 2h
        seasonResult.PlayStats.Days3To5Hours.Should().Be(1); // June 2: 3h
        seasonResult.PlayStats.RecurringBookings.Should().Be(2);

        var court1Heatmap = seasonResult.Occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1");
        court1Heatmap.Hours.Single(h => h.Hour == 10).OccupancyPercentage.Should().Be(20.0);
        court1Heatmap.Hours.Single(h => h.Hour == 11).OccupancyPercentage.Should().Be(20.0);
        court1Heatmap.Hours.Single(h => h.Hour == 12).OccupancyPercentage.Should().Be(0.0);
    }

    [Fact]
    public async Task GetClubStatistics_RecurringSeriesAcrossDstChange_UsesLocalTimeOfBooking()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];

            // DST starts in Vienna on 2025-03-30: 18:00 local is 17:00 UTC before and 16:00 UTC after
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 3, 1), new DateOnly(2025, 4, 30))));
            await ctx.SaveChangesAsync();

            // Every Monday at 18:00 local (9 occurrences: March 3 to April 28)
            await AddWeeklySeries(ctx, clubId, court1Id, playMode.Id, ViennaTimeZoneId,
                new DateOnly(2025, 3, 3), new DateOnly(2025, 4, 30), new TimeOnly(18, 0), TimeSpan.FromHours(1));
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var seasonResult = result.SeasonStatistics.Single();
        seasonResult.PlayStats.TotalBookings.Should().Be(9);
        seasonResult.PlayStats.TotalPlaytimeHours.Should().Be(9.0);
        seasonResult.PlayStats.DaysWithoutBookings.Should().Be(61 - 9);

        var expected = Math.Round(9d / 61 * 100, 1);
        var court1Heatmap = seasonResult.Occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1");
        court1Heatmap.Hours.Single(h => h.Hour == 18).OccupancyPercentage.Should().Be(expected);
        court1Heatmap.Hours.Where(h => h.Hour != 18).Should().OnlyContain(h => h.OccupancyPercentage == 0);

        var allTimeCourt1Heatmap = result.AllTime.Occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1");
        allTimeCourt1Heatmap.Hours.Single(h => h.Hour == 18).OccupancyPercentage.Should().Be(expected);
    }

    [Fact]
    public async Task GetClubStatistics_BookingShortlyAfterLocalMidnight_IsAssignedToLocalDayAndSeason()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];

            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10))));
            await ctx.SaveChangesAsync();

            // 2025-06-01 00:30 in Vienna is 2025-05-31 22:30 UTC
            var from = new DateTimeOffset(2025, 5, 31, 22, 30, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), ViennaTimeZoneId));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var playStats = result.SeasonStatistics.Single().PlayStats;
        playStats.TotalBookings.Should().Be(1);
        playStats.DaysWithoutBookings.Should().Be(9);
    }

    [Fact]
    public async Task GetClubStatistics_CurrentSeason_UsesDaysUntilTodayForOccupancy()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30))));
            await ctx.SaveChangesAsync();

            // Court 1 at 9-10 on June 1 to 5
            for (var day = 1; day <= 5; day++)
            {
                var from = new DateTimeOffset(2025, 6, day, 9, 0, 0, TimeSpan.Zero);
                ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id));
            }

            // Booking after "today" is not played yet
            var future = new DateTimeOffset(2025, 6, 20, 9, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(future, future.AddHours(1)), TimeZoneInfo.Utc.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId, new DateOnly(2025, 6, 10)));

        var season = result.SeasonStatistics.Single();
        season.State.Should().Be(SeasonState.Current);
        season.ElapsedDays.Should().Be(10);
        season.SeasonLengthDays.Should().Be(30);
        season.Occupancy.Days.Should().Be(10);
        season.Occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1").Hours.Single(h => h.Hour == 9).OccupancyPercentage.Should().Be(50.0);
        season.PlayStats.TotalBookings.Should().Be(5);
        season.PlayStats.UpcomingBookings.Should().Be(1);
        season.PlayStats.DaysWithoutBookings.Should().Be(5);
        season.PlayStats.WeeklyActivity.Select(w => w.WeekStart).Should().Equal(new DateOnly(2025, 5, 26), new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 9));
        season.PlayStats.WeeklyActivity.Sum(w => w.Bookings).Should().Be(5);

        // 5 booked hours of 10 days * 15 opening hours * 2 courts
        season.Occupancy.OverallPercentage.Should().Be(Math.Round(5d / 300 * 100, 1));
        result.AllTime.Occupancy.Days.Should().Be(10);
        result.AllTime.Totals.Bookings.Should().Be(5);
    }

    [Fact]
    public async Task GetClubStatistics_UpcomingSeason_HasNoOccupancy()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);

        await QueryAsync(async ctx =>
        {
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30))));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId, new DateOnly(2025, 5, 1)));

        var season = result.SeasonStatistics.Single();
        season.State.Should().Be(SeasonState.Upcoming);
        season.ElapsedDays.Should().Be(0);
        season.Occupancy.OverallPercentage.Should().Be(0);
        season.PlayStats.DaysWithoutBookings.Should().Be(0);
        season.PlayStats.WeeklyActivity.Should().BeEmpty();
        result.AllTime.Totals.Seasons.Should().Be(0);
    }

    [Fact]
    public async Task GetClubStatistics_Occupancy_CountsPartialHoursAndCapsOverbookedSlots()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];

            // 2025-06-02 is a Monday
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 2))));
            await ctx.SaveChangesAsync();

            var from = new DateTimeOffset(2025, 6, 2, 9, 30, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id));

            // Two overlapping bookings at 14-15 (overbooking) only fill the slot once
            var overbooked = new DateTimeOffset(2025, 6, 2, 14, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(overbooked, overbooked.AddHours(1)), TimeZoneInfo.Utc.Id));
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(overbooked, overbooked.AddHours(1)), TimeZoneInfo.Utc.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var occupancy = result.SeasonStatistics.Single().Occupancy;
        var court1 = occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1");
        court1.Hours.Single(h => h.Hour == 9).OccupancyPercentage.Should().Be(50.0);
        court1.Hours.Single(h => h.Hour == 10).OccupancyPercentage.Should().Be(50.0);
        court1.Hours.Single(h => h.Hour == 14).OccupancyPercentage.Should().Be(100.0);
        court1.BookedHours.Should().Be(2.0);

        var monday = occupancy.WeekdayHeatmaps.Single(w => w.DayOfWeek == DayOfWeek.Monday);
        monday.Hours.Single(h => h.Hour == 14).OccupancyPercentage.Should().Be(50.0); // 1 of 2 courts
        occupancy.WeekdayHeatmaps.Single(w => w.DayOfWeek == DayOfWeek.Tuesday).OccupancyPercentage.Should().Be(0);
        occupancy.WeekdayHeatmaps.First().DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task GetClubStatistics_PrimeTime_CalculatesShareAndOccupancy()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];

            // Prime time 18-20 on weekdays; 2025-06-02 is a Monday
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 2))));
            await ctx.SaveChangesAsync();

            var from = new DateTimeOffset(2025, 6, 2, 17, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(2)), TimeZoneInfo.Utc.Id));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var season = result.SeasonStatistics.Single();
        season.PlayStats.BookingBehavior.PrimeTimeHoursShare.Should().Be(50.0); // 18-19 of 17-19
        season.Occupancy.PrimeTimePercentage.Should().Be(25.0); // 1 of 2 hours on 1 of 2 courts
        result.PrimeTime!.FromHour.Should().Be(18);
        result.PrimeTime.ToHour.Should().Be(20);
    }

    [Fact]
    public async Task GetClubStatistics_MemberActivity_CalculatesActivationRetentionAndTopPlayers()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);
        var member1Id = Query(ctx => ctx.TestData().Member1.Id);
        var member2Id = Query(ctx => ctx.TestData().Member2.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];
            var season1 = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            var season2 = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.AddRange(season1, season2);

            var lapsedUser = new User("Lapsed", "Member", new DateOnly(1990, 1, 1), Gender.Female, TestDataSeed.UserId);
            ctx.Add(lapsedUser);
            await ctx.SaveChangesAsync();
            var lapsedMember = new ClubMember(lapsedUser.Id, clubId, [MemberRole.User]);
            ctx.Add(lapsedMember);
            await ctx.SaveChangesAsync();

            // Season 1: member 1 and the lapsed member, season 2: member 1 (returning) and member 2 (new)
            ctx.AddRange(
                new MemberSeason(member1Id, season1.Id),
                new MemberSeason(lapsedMember.Id, season1.Id),
                new MemberSeason(member1Id, season2.Id),
                new MemberSeason(member2Id, season2.Id));

            // Member 1 plays twice in season 2, member 2 never
            for (var day = 1; day <= 2; day++)
            {
                var from = new DateTimeOffset(2025, 6, day, 10, 0, 0, TimeSpan.Zero);
                ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1.5)), TimeZoneInfo.Utc.Id, [member1Id]));
            }

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var activity = result.SeasonStatistics[0].MemberActivity;
        activity.EnrolledMembers.Should().Be(2);
        activity.ActivePlayers.Should().Be(1);
        activity.ActivationRate.Should().Be(50.0);
        activity.ReturningMembers.Should().Be(1);
        activity.NewMembers.Should().Be(1);
        activity.LapsedMembers.Should().Be(1);
        activity.BookingsPerPlayer.Single(b => b.Category == ActivityBucketCategory.None).Members.Should().Be(1);
        activity.BookingsPerPlayer.Single(b => b.Category == ActivityBucketCategory.TwoToFive).Members.Should().Be(1);

        var topPlayer = activity.TopPlayers.Single();
        topPlayer.MemberId.Should().Be(member1Id);
        topPlayer.Bookings.Should().Be(2);
        topPlayer.Hours.Should().Be(3.0);
        topPlayer.IsGuest.Should().BeFalse();

        result.SeasonStatistics[0].PlayStats.AveragePlayersPerBooking.Should().Be(1.0);
        result.SeasonStatistics[0].PreviousSeasonPace!.SeasonLabel.Should().Be("2024");
        result.SeasonStatistics[1].PreviousSeasonPace.Should().BeNull();

        var summaries = result.AllTime.Seasons;
        summaries.Should().HaveCount(2);
        summaries[0].EnrolledMembers.Should().Be(2);
        summaries[1].LapsedMembers.Should().Be(1);
        result.AllTime.Totals.DistinctMembers.Should().Be(3);
        result.AllTime.Totals.DistinctPlayers.Should().Be(1);
        result.AllTime.TopPlayers.Single().MemberId.Should().Be(member1Id);
    }

    [Fact]
    public async Task GetClubStatistics_LeadTimes_IgnoreRecurringBookings()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30))));
            await ctx.SaveChangesAsync();

            var sameDay = new Booking(clubId, court1Id, playMode.Id, OneHour(new DateTimeOffset(2025, 6, 10, 18, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc.Id);
            var twoDays = new Booking(clubId, court1Id, playMode.Id, OneHour(new DateTimeOffset(2025, 6, 12, 18, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc.Id);
            var twoWeeks = new Booking(clubId, court1Id, playMode.Id, OneHour(new DateTimeOffset(2025, 6, 24, 18, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc.Id);
            ctx.AddRange(sameDay, twoDays, twoWeeks);
            await ctx.SaveChangesAsync();

            await AddWeeklySeries(ctx, clubId, court1Id, playMode.Id, TimeZoneInfo.Utc.Id,
                new DateOnly(2025, 6, 3), new DateOnly(2025, 6, 10), new TimeOnly(10, 0), TimeSpan.FromHours(1));

            // The creation date is set when saving, so it is overwritten afterwards
            var created = new DateTime(2025, 6, 10, 8, 0, 0, DateTimeKind.Utc);
            foreach (var id in new[] { sameDay.Id, twoDays.Id, twoWeeks.Id })
                await ctx.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Bookings" SET "Metadata_Created" = {created} WHERE "Id" = {id}""");
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var leadTimes = result.SeasonStatistics.Single().PlayStats.BookingBehavior.LeadTimes;
        leadTimes.Single(l => l.Category == LeadTimeCategory.LessThanADay).Bookings.Should().Be(1);
        leadTimes.Single(l => l.Category == LeadTimeCategory.OneToTwoDays).Bookings.Should().Be(1);
        leadTimes.Single(l => l.Category == LeadTimeCategory.ThreeToSevenDays).Bookings.Should().Be(0);
        leadTimes.Single(l => l.Category == LeadTimeCategory.MoreThanAWeek).Bookings.Should().Be(1);
    }

    private static DateTimeOffsetInterval OneHour(DateTimeOffset from) => new(from, from.AddHours(1));

    [Fact]
    public async Task GetClubStatistics_OpeningHoursStoredInUtc_AreShownInLocalTimeOfTheClub()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);

        await QueryAsync(async ctx =>
        {
            var playMode = ctx.TestData().Club.PlayModes[0];
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 2))));
            await ctx.SaveChangesAsync();

            // 18:00 local time in Vienna (summer time, UTC+2)
            var from = new DateTimeOffset(2025, 6, 2, 16, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), ViennaTimeZoneId));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId, new DateOnly(2025, 6, 2)));

        // Opening hours 07-22 UTC and prime time 18-20 UTC are 09-24 and 20-22 in Vienna
        result.OpeningHourFrom.Should().Be(9);
        result.OpeningHourTo.Should().Be(24);
        result.PrimeTime!.FromHour.Should().Be(20);
        result.PrimeTime.ToHour.Should().Be(22);

        var court1 = result.SeasonStatistics.Single().Occupancy.CourtHeatmaps.First(h => h.CourtName == "Court 1");
        court1.Hours.First().Hour.Should().Be(9);
        court1.Hours.Single(h => h.Hour == 18).OccupancyPercentage.Should().Be(100.0);
    }

    [Fact]
    public async Task GetClubStatistics_MatchesOnly_IgnoresPlayModesThatDoNotCountAsMatch()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        var court1Id = Query(ctx => ctx.TestData().Court1.Id);
        var member1Id = Query(ctx => ctx.TestData().Member1.Id);
        var member2Id = Query(ctx => ctx.TestData().Member2.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var match = club.PlayModes[0]; // counts as match
            club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], System.Drawing.Color.Blue, null, false, "Training", null, false, false));
            ctx.Add(new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30))));
            await ctx.SaveChangesAsync();
            var training = club.PlayModes.Single(pm => pm.Name == "Training");

            // Member 1 plays one match, member 2 trains a lot (during prime time on a Monday)
            var matchFrom = new DateTimeOffset(2025, 6, 7, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(clubId, court1Id, match.Id, new DateTimeOffsetInterval(matchFrom, matchFrom.AddHours(1)), TimeZoneInfo.Utc.Id, [member1Id]));
            for (var day = 2; day <= 4; day++)
            {
                var from = new DateTimeOffset(2025, 6, day, 18, 0, 0, TimeSpan.Zero);
                ctx.Add(new Booking(clubId, court1Id, training.Id, new DateTimeOffsetInterval(from, from.AddHours(2)), TimeZoneInfo.Utc.Id, [member2Id]));
            }

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetClubStatistics(clubId));

        var season = result.SeasonStatistics.Single();
        season.MemberActivity.TopPlayers.First().MemberId.Should().Be(member2Id);
        season.MemberActivity.TopMatchPlayers.Should().ContainSingle().Which.MemberId.Should().Be(member1Id);

        season.PlayStats.BookingBehavior.Bookings.Should().Be(4);
        season.PlayStats.BookingBehavior.PrimeTimeHoursShare.Should().BeGreaterThan(0);
        season.PlayStats.MatchBookingBehavior.Bookings.Should().Be(1);
        season.PlayStats.MatchBookingBehavior.PrimeTimeHoursShare.Should().Be(0);
        season.PlayStats.MatchBookingBehavior.LeadTimes.Sum(l => l.Bookings).Should().Be(1);

        result.AllTime.TopMatchPlayers.Should().ContainSingle().Which.MemberId.Should().Be(member1Id);
        result.AllTime.MatchBookingBehavior.Bookings.Should().Be(1);
        result.AllTime.BookingBehavior.Bookings.Should().Be(4);
    }

    private const string ViennaTimeZoneId = "Europe/Vienna";

    // Creates a weekly series and its occurrences the same way BookRecurringCourt does
    private static async Task<int> AddWeeklySeries(
        AppDbContext ctx,
        int clubId,
        int courtId,
        int playModeId,
        string timeZoneInfoId,
        DateOnly startDate,
        DateOnly endDate,
        TimeOnly startTime,
        TimeSpan duration)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneInfoId);
        var series = new RecurringBookingSeries(clubId, courtId, playModeId, startDate.DayOfWeek, startTime, startTime.Add(duration),
            timeZoneInfoId, 1, startDate, endDate, null, []);
        ctx.Add(series);
        await ctx.SaveChangesAsync();

        for (var date = startDate; date <= endDate; date = date.AddDays(7))
        {
            var localStart = date.ToDateTime(startTime);
            var occurrenceStart = new DateTimeOffset(localStart, tz.GetUtcOffset(localStart)).ToUniversalTime();
            ctx.Add(new Booking(clubId, courtId, playModeId, new DateTimeOffsetInterval(occurrenceStart, occurrenceStart.Add(duration)),
                timeZoneInfoId, recurringBookingSeriesId: series.Id));
        }

        await ctx.SaveChangesAsync();
        return series.Id;
    }
}
