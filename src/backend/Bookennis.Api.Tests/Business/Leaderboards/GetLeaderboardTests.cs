using Bookennis.Api.Business.Leaderboards;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Leaderboards;

public class GetLeaderboardTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetLeaderboard_NoBookings_ReturnsEmptyLeaderboard()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries.Should().BeEmpty();
        result.AllTime.CurrentMemberRank.Should().BeNull();
        result.AllTime.CurrentMemberDuration.Should().BeNull();
    }

    [Fact]
    public async Task GetLeaderboard_WithBookings_RanksByDuration()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            var from1 = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from1, from1.AddHours(2)), TimeZoneInfo.Utc.Id, [member1.Id]));

            var from2 = new DateTimeOffset(2025, 6, 2, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from2, from2.AddHours(3)), TimeZoneInfo.Utc.Id, [member2.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries.Should().HaveCount(2);
        result.AllTime.Entries[0].TotalHours.Should().Be(3.0);
        result.AllTime.Entries[0].Rank.Should().Be(1);
        result.AllTime.Entries[1].TotalHours.Should().Be(2.0);
        result.AllTime.Entries[1].Rank.Should().Be(2);
    }

    [Fact]
    public async Task GetLeaderboard_CurrentMemberRank_IsSet()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1.5)), TimeZoneInfo.Utc.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.CurrentMemberRank.Should().Be(1);
        result.AllTime.CurrentMemberDuration.Should().Be(1.5);
    }

    [Fact]
    public async Task GetLeaderboard_OnlyChargingPlayModes_AreCounted()
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
            ctx.Add(new Booking(club.Id, court.Id, chargingPlayMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries.Should().HaveCount(1);
        result.AllTime.Entries[0].TotalHours.Should().Be(1.0);
    }

    [Fact]
    public async Task GetLeaderboard_OptedOutMember_NotShownInSeasonLeaderboard()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var memberSeason = new MemberSeason(member1.Id, season.Id);
            memberSeason.LeaderboardOptOut = true;
            ctx.Add(memberSeason);

            var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(2)), TimeZoneInfo.Utc.Id, [member1.Id]));

            var from2 = new DateTimeOffset(2025, 6, 2, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from2, from2.AddHours(1)), TimeZoneInfo.Utc.Id, [member2.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        var seasonLeaderboard = result.SeasonLeaderboards.FirstOrDefault(s => s.StartDate == new DateOnly(2025, 1, 1));
        seasonLeaderboard.Should().NotBeNull();
        seasonLeaderboard!.Data.Entries.Should().HaveCount(1);
        seasonLeaderboard.Data.Entries[0].FirstName.Should().NotBe("us");
    }

    [Fact]
    public async Task GetLeaderboard_OptedOutSeason_NotCountedInAllTime()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var memberSeason = new MemberSeason(member1.Id, season.Id);
            memberSeason.LeaderboardOptOut = true;
            ctx.Add(memberSeason);

            var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(2)), TimeZoneInfo.Utc.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries.Should().NotContain(e => e.IsCurrentMember);
    }

    [Fact]
    public async Task GetLeaderboard_SeasonLeaderboard_ShowsOptOutStatus()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var member1 = ctx.TestData().Member1;

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var memberSeason = new MemberSeason(member1.Id, season.Id);
            memberSeason.LeaderboardOptOut = true;
            ctx.Add(memberSeason);
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        var seasonLeaderboard = result.SeasonLeaderboards.FirstOrDefault(s => s.StartDate == new DateOnly(2025, 1, 1));
        seasonLeaderboard.Should().NotBeNull();
        seasonLeaderboard!.IsOptedOut.Should().BeTrue();
    }

    [Fact]
    public async Task GetLeaderboard_LimitsToTop25()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var playMode = club.PlayModes[0];

            for (var i = 0; i < 30; i++)
            {
                var user = new Domain.User.User($"user{i}@test.com", $"user{i}@test.com", $"First{i}", $"Last{i}", new DateOnly(1990, 1, 1), Domain.User.Gender.Male);
                ctx.Add(user);
                await ctx.SaveChangesAsync();

                var member = new ClubMember(user.Id, club.Id, [MemberRole.User]);
                ctx.Add(member);
                await ctx.SaveChangesAsync();

                var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
                ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(i + 1)), TimeZoneInfo.Utc.Id, [member.Id]));
                await ctx.SaveChangesAsync();
            }
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries.Should().HaveCount(25);
        result.AllTime.Entries[0].Rank.Should().Be(1);
        result.AllTime.Entries[24].Rank.Should().Be(25);
    }

    [Fact]
    public async Task GetLeaderboard_FutureBookings_AreNotCounted()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var past = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(past, past.AddHours(1)), TimeZoneInfo.Utc.Id, [member1.Id]));

            var future = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(10);
            var futureOffset = new DateTimeOffset(future, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(futureOffset, futureOffset.AddHours(2)), TimeZoneInfo.Utc.Id, [member1.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries.Should().ContainSingle();
        result.AllTime.Entries[0].TotalHours.Should().Be(1.0);
        result.AllTime.Entries[0].Matches.Should().Be(1);
        result.AllTime.TotalMatches.Should().Be(1);
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsMatchesAndTotals()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            var from1 = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from1, from1.AddHours(1)), TimeZoneInfo.Utc.Id, [member1.Id, member2.Id]));

            var from2 = new DateTimeOffset(2025, 6, 2, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from2, from2.AddHours(2)), TimeZoneInfo.Utc.Id, [member2.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        var data = result.AllTime;
        data.TotalPlayers.Should().Be(2);
        data.TotalMatches.Should().Be(2);
        data.TotalHours.Should().Be(4.0);
        data.Entries[0].Matches.Should().Be(2);
        data.Entries[1].Matches.Should().Be(1);

        // Member1 is second and 2 hours behind member2
        data.CurrentMemberRank.Should().Be(2);
        data.CurrentMemberEntry!.MemberId.Should().Be(TestDataSeed.Member1Id);
        data.HoursToNextRank.Should().Be(2.0);
    }

    [Fact]
    public async Task GetLeaderboard_RankChange_ComparesWithLastWeek()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            // A month ago member2 led
            var monthAgo = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-30).AddHours(10), TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(monthAgo, monthAgo.AddHours(2)), TimeZoneInfo.Utc.Id, [member2.Id]));
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(monthAgo.AddDays(1), monthAgo.AddDays(1).AddHours(1)), TimeZoneInfo.Utc.Id, [member1.Id]));

            // Yesterday member1 overtook
            var yesterday = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-1).AddHours(10), TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(yesterday, yesterday.AddHours(3)), TimeZoneInfo.Utc.Id, [member1.Id]));

            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        var entries = result.AllTime.Entries;
        entries[0].MemberId.Should().Be(TestDataSeed.Member1Id);
        entries[0].RankChange.Should().Be(1);
        entries[1].RankChange.Should().Be(-1);
    }

    [Fact]
    public async Task GetLeaderboard_NewEntry_HasNoRankChange()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var yesterday = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-1).AddHours(10), TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(yesterday, yesterday.AddHours(1)), TimeZoneInfo.Utc.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.AllTime.Entries[0].RankChange.Should().BeNull();
    }

    [Fact]
    public async Task GetLeaderboard_Sport_CountsOnlyBookingsOnCourtsOfTheSport()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);
        await SeedTennisAndPadelBookings();

        var all = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));
        var padel = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId, Sport.Padel));
        var tennis = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId, Sport.Tennis));

        all.Sport.Should().BeNull();
        all.Sports.Should().Equal(Bookennis.Shared.Controller.Shared.Sport.Tennis, Bookennis.Shared.Controller.Shared.Sport.Padel);
        all.AllTime.Entries.Select(e => (e.MemberId, e.TotalHours)).Should().Equal((TestDataSeed.Member2Id, 3.0), (TestDataSeed.Member1Id, 2.0));
        all.AllTime.TotalMatches.Should().Be(3);

        padel.Sport.Should().Be(Bookennis.Shared.Controller.Shared.Sport.Padel);
        padel.AllTime.Entries.Select(e => (e.MemberId, e.TotalHours)).Should().Equal((TestDataSeed.Member2Id, 2.0));
        padel.AllTime.TotalMatches.Should().Be(1);

        tennis.AllTime.Entries.Select(e => (e.MemberId, e.TotalHours)).Should().Equal((TestDataSeed.Member1Id, 2.0), (TestDataSeed.Member2Id, 1.0));
    }

    [Fact]
    public async Task GetLeaderboard_OneSport_NoDefaultSport()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.Sports.Should().Equal(Bookennis.Shared.Controller.Shared.Sport.Tennis);
        result.DefaultSport.Should().BeNull();
    }

    [Fact]
    public async Task GetLeaderboard_SeveralSports_DefaultSportIsTheFirstEnrolledSport()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);
        await SeedActiveSeasonEnrollment([Sport.Padel]);

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.DefaultSport.Should().Be(Bookennis.Shared.Controller.Shared.Sport.Padel);
    }

    [Fact]
    public async Task GetLeaderboard_SeveralSportsEnrolledForEverySport_NoDefaultSport()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);
        await SeedActiveSeasonEnrollment([]);

        var result = await SendAsync(new GetLeaderboard(userId, TestDataSeed.ClubId));

        result.DefaultSport.Should().BeNull();
    }

    /// <summary>Court 2 is a padel court; the user (member 1) is enrolled in the running season.</summary>
    private Task SeedActiveSeasonEnrollment(Sport[] sports) => QueryAsync(async ctx =>
    {
        ctx.TestData().Court2.SetSport(Sport.Padel);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
        ctx.Add(season);
        await ctx.SaveChangesAsync();
        ctx.Add(new MemberSeason(TestDataSeed.Member1Id, season.Id, sports: sports));
        await ctx.SaveChangesAsync();
    });

    /// <summary>Court 2 is a padel court. Member 1: 2 h tennis. Member 2: 1 h tennis, 2 h padel.</summary>
    private Task SeedTennisAndPadelBookings() => QueryAsync(async ctx =>
    {
        var club = ctx.TestData().Club;
        ctx.TestData().Court2.SetSport(Sport.Padel);
        var playMode = club.PlayModes[0];

        var from = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
        ctx.Add(new Booking(club.Id, TestDataSeed.Court1Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(2)), TimeZoneInfo.Utc.Id, [TestDataSeed.Member1Id]));
        ctx.Add(new Booking(club.Id, TestDataSeed.Court1Id, playMode.Id, new DateTimeOffsetInterval(from.AddDays(1), from.AddDays(1).AddHours(1)), TimeZoneInfo.Utc.Id, [TestDataSeed.Member2Id]));
        ctx.Add(new Booking(club.Id, TestDataSeed.Court2Id, playMode.Id, new DateTimeOffsetInterval(from.AddDays(2), from.AddDays(2).AddHours(2)), TimeZoneInfo.Utc.Id, [TestDataSeed.Member2Id]));
        await ctx.SaveChangesAsync();
    });
}
