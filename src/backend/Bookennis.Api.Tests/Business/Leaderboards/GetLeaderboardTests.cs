using Bookennis.Api.Business.Leaderboards;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

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

        var result = await SendAsync(new GetLeaderboard(userId));

        result.AllTime.Entries[0].RankChange.Should().BeNull();
    }
}
