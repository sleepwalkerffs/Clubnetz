using Bookennis.Api.Business.Wrapped;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Api.Tests.Business.ClubEvents;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Statistics;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;

namespace Bookennis.Api.Tests.Business.Wrapped;

public class GetSeasonWrappedTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // A past season: June of last year
    private static readonly DateOnly SeasonStart = new(Today.Year - 1, 6, 1);
    private static readonly DateOnly SeasonEnd = new(Today.Year - 1, 6, 30);

    [Fact]
    public async Task GetSeasonWrapped_CalculatesTheHighlightsOfTheSeason()
    {
        var (seasonId, partnerId) = await SeedSeasonWithBookings(publish: true);

        var result = await SendAsync(new GetSeasonWrapped(TestDataSeed.UserId, TestDataSeed.ClubId, seasonId, CanManage: false));

        result.IsPublished.Should().BeTrue();
        result.ClubName.Should().Be("TestClub");
        result.FirstName.Should().Be("us");

        // Member1 played 4 matches in the season (3 Tuesday evenings of a series + 1 Saturday morning), the one in July does not count
        result.Matches.Should().Be(4);
        result.HoursOnCourt.Should().Be(4);
        result.FirstMatch.Should().Be(new[] { NextOrSame(SeasonStart, DayOfWeek.Tuesday), NextOrSame(SeasonStart, DayOfWeek.Saturday) }.Min());
        result.DaysPlayed.Should().Be(4);
        result.PlayerType.Should().Be(PlayerType.NightOwl);
        result.FavouriteHour.Should().Be(19);

        result.TopPartners.Should().ContainSingle();
        result.TopPartners[0].MemberId.Should().Be(partnerId);
        result.TopPartners[0].FirstName.Should().Be("Pat");
        result.TopPartners[0].Matches.Should().Be(3);
        result.DistinctPartners.Should().Be(1);

        result.FavouriteCourt!.Name.Should().Be("Court 1");
        result.FavouriteCourt.Matches.Should().Be(3);
        result.FavouriteCourt.SharePercentage.Should().Be(75);
        result.CourtsPlayed.Should().Be(2);

        result.RegularSlot!.StartTime.Should().Be(TimeSpan.FromHours(19));
        result.RegularSlot.CourtName.Should().Be("Court 1");
        result.RegularSlot.Matches.Should().Be(3);
        result.RecurringMatches.Should().Be(3);

        result.Badges.Select(b => b.Name).Should().Equal("Champion", "Gold", "Bronze");
        result.Badges[0].Level.Should().BeNull();
        result.EventsAttended.Should().Be(1);

        // Member1 and the partner share the first place with 4 matches each, Member2 is enrolled without a match
        result.Rank.Should().Be(1);
        result.EnrolledMembers.Should().Be(3);
        result.BetterThanPercentage.Should().Be(50);

        result.Club.Matches.Should().Be(5);
        result.Club.ActivePlayers.Should().Be(2);
        result.Club.BusiestCourt.Should().Be("Court 1");
        result.Club.Events.Should().Be(1);
        result.Club.OwnSharePercentage.Should().Be(80);
    }

    [Fact]
    public async Task GetSeasonWrapped_MemberWithoutMatches_GetsOnlyTheClub()
    {
        var (seasonId, _) = await SeedSeasonWithBookings(publish: true);

        var result = await SendAsync(new GetSeasonWrapped(TestDataSeed.AdminId, TestDataSeed.ClubId, seasonId, CanManage: false));

        result.Matches.Should().Be(0);
        result.Rank.Should().BeNull();
        result.TopPartners.Should().BeEmpty();
        result.FavouriteCourt.Should().BeNull();
        result.PlayerType.Should().BeNull();
        result.RegularSlot.Should().BeNull();
        result.Club.Matches.Should().Be(5);
    }

    [Fact]
    public async Task GetSeasonWrapped_NotPublished_IsOnlyForClubAdmins()
    {
        var (seasonId, _) = await SeedSeasonWithBookings(publish: false);

        var asMember = () => SendAsync(new GetSeasonWrapped(TestDataSeed.UserId, TestDataSeed.ClubId, seasonId, CanManage: false));
        (await asMember.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(GetSeasonWrapped.ErrorCode.WrappedNotPublished));

        var preview = await SendAsync(new GetSeasonWrapped(TestDataSeed.UserId, TestDataSeed.ClubId, seasonId, CanManage: true));
        preview.IsPublished.Should().BeFalse();
        preview.Matches.Should().Be(4);
    }

    [Fact]
    public async Task GetSeasonWrapped_SeasonNotStarted_IsRejected()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(Today.AddDays(10), Today.AddDays(100)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        var act = () => SendAsync(new GetSeasonWrapped(TestDataSeed.UserId, TestDataSeed.ClubId, seasonId, CanManage: true));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(Season.ErrorCode.WrappedSeasonNotStarted));
    }

    [Fact]
    public async Task GetSeasonWrapped_SeasonOfAnotherClub_IsNotFound()
    {
        var (seasonId, _) = await SeedSeasonWithBookings(publish: true);

        var act = () => SendAsync(new GetSeasonWrapped(TestDataSeed.UserId, TestDataSeed.ClubId + 1, seasonId, CanManage: true));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetWrappedSeasons_MembersSeeReleasedSeasons_AdminsEveryStartedSeason()
    {
        var (publishedId, _) = await SeedSeasonWithBookings(publish: true);
        var (unpublishedId, upcomingId) = await QueryAsync(async ctx =>
        {
            var unpublished = new Season(TestDataSeed.ClubId, new DateOnlyInterval(new DateOnly(Today.Year - 2, 6, 1), new DateOnly(Today.Year - 2, 6, 30)));
            var upcoming = new Season(TestDataSeed.ClubId, new DateOnlyInterval(Today.AddDays(10), Today.AddDays(100)));
            ctx.AddRange(unpublished, upcoming);
            await ctx.SaveChangesAsync();
            return (unpublished.Id, upcoming.Id);
        });

        var asMember = await SendAsync(new GetWrappedSeasons(TestDataSeed.UserId, TestDataSeed.ClubId, CanManage: false));
        asMember.CanManage.Should().BeFalse();
        asMember.Seasons.Should().ContainSingle().Which.SeasonId.Should().Be(publishedId);
        asMember.Seasons[0].Matches.Should().Be(4);
        asMember.Seasons[0].PublishedAt.Should().NotBeNull();

        var asAdmin = await SendAsync(new GetWrappedSeasons(TestDataSeed.UserId, TestDataSeed.ClubId, CanManage: true));
        asAdmin.Seasons.Select(s => s.SeasonId).Should().Equal(publishedId, unpublishedId);
        asAdmin.Seasons.Should().NotContain(s => s.SeasonId == upcomingId);
    }

    /// <summary>
    /// A season in June of last year. Member1 plays a weekly series with "Pat" on Tuesdays at 19:00 on court 1 (3 matches)
    /// and one Saturday morning alone on court 2. Pat plays once more without Member1, and Member1 plays once in July (outside the season).
    /// Member1 has two tier badges and a one-time badge of the season and attended one event.
    /// </summary>
    private Task<(int SeasonId, int PartnerId)> SeedSeasonWithBookings(bool publish) => QueryAsync(async ctx =>
    {
        var club = ctx.TestData().Club;
        var playModeId = club.PlayModes[0].Id;

        var season = new Season(club.Id, new DateOnlyInterval(SeasonStart, SeasonEnd));
        ctx.Add(season);
        await ctx.SaveChangesAsync();
        if (publish)
            season.PublishWrapped(DateTimeOffset.UtcNow, Today);

        var partner = await AddClubMember(ctx, club.Id, "Pat", "Partner");
        ctx.AddRange(new MemberSeason(TestDataSeed.Member1Id, season.Id), new MemberSeason(TestDataSeed.Member2Id, season.Id), new MemberSeason(partner.Id, season.Id));

        var firstTuesday = NextOrSame(SeasonStart, DayOfWeek.Tuesday);
        var series = new RecurringBookingSeries(club.Id, TestDataSeed.Court1Id, playModeId, DayOfWeek.Tuesday, new TimeOnly(19, 0), new TimeOnly(20, 0), TimeZoneInfo.Utc.Id, 1, firstTuesday, SeasonEnd, null, [TestDataSeed.Member1Id, partner.Id]);
        ctx.Add(series);
        await ctx.SaveChangesAsync();

        for (var week = 0; week < 3; week++)
            AddBooking(ctx, club.Id, TestDataSeed.Court1Id, playModeId, firstTuesday.AddDays(7 * week), 19, series.Id, TestDataSeed.Member1Id, partner.Id);

        AddBooking(ctx, club.Id, TestDataSeed.Court2Id, playModeId, NextOrSame(SeasonStart, DayOfWeek.Saturday), 9, null, TestDataSeed.Member1Id);
        AddBooking(ctx, club.Id, TestDataSeed.Court1Id, playModeId, SeasonEnd, 10, null, partner.Id);
        AddBooking(ctx, club.Id, TestDataSeed.Court1Id, playModeId, SeasonEnd.AddDays(3), 10, null, TestDataSeed.Member1Id);

        var bronze = new BadgeTier(club.Id, season.Id, 1, "Bronze", "", 1, 1);
        var gold = new BadgeTier(club.Id, season.Id, 2, "Gold", "", 3, 2);
        var champion = new OneTimeBadge(club.Id, season.Id, "Champion", "");
        ctx.AddRange(bronze, gold, champion);
        await ctx.SaveChangesAsync();
        ctx.AddRange(new MemberBadge(TestDataSeed.Member1Id, bronze.Id, season.Id), new MemberBadge(TestDataSeed.Member1Id, gold.Id, season.Id), new MemberOneTimeBadge(TestDataSeed.Member1Id, champion.Id));

        var clubEvent = new ClubEvent(club.Id, null, ClubEventSeed.Data(date: SeasonStart.AddDays(10), withQuestions: false));
        ctx.Add(clubEvent);
        await ctx.SaveChangesAsync();
        clubEvent.Register(TestDataSeed.Member1Id, 1, [], null, new DateTimeOffset(SeasonStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        await ctx.SaveChangesAsync();
        return (season.Id, partner.Id);
    });

    private static DateOnly NextOrSame(DateOnly date, DayOfWeek dayOfWeek)
        => date.AddDays(((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7);

    private static void AddBooking(AppDbContext ctx, int clubId, int courtId, int playModeId, DateOnly date, int hour, int? seriesId, params int[] memberIds)
    {
        var from = new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);
        ctx.Add(new Booking(clubId, courtId, playModeId, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [.. memberIds], recurringBookingSeriesId: seriesId));
    }

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
