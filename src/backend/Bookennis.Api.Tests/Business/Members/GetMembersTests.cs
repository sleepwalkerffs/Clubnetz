using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Infrastructure.Utils.Sorting;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Members;
using FluentAssertions;
using Xunit;
using SharedAgeGroup = Bookennis.Shared.Controller.Members.AgeGroup;
using SharedGender = Bookennis.Shared.Controller.Shared.Gender;
using SharedMemberRole = Bookennis.Shared.Controller.Shared.MemberRole;
using SortDirection = Bookennis.Shared.Utils.Sorting.SortDirection;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMembersTests(TestFixture fixture) : TestBase(fixture)
{
    // Unique part of the last names of the members created in these tests, used as search term to ignore seeded members
    private const string Marker = "Zfilter";

    private static readonly PaginationParameters AllOnOnePage = new() { Page = 1, PageSize = 100, EnablePaging = true };

    [Fact]
    public async Task GetMembers_ReturnsAllMembers()
    {
        var result = await SendAsync(new GetMembers(AllOnOnePage, null));

        result.Entities.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetMembers_WithSearchTerm_FiltersResults()
    {
        var result = await SendAsync(new GetMembers(AllOnOnePage, null, new MemberFilter { SearchTerm = "ad min" }));

        result.Entities.Should().OnlyContain(m => m.FullName.Contains("ad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetMembers_SeasonFilters_FindLapsedReturningAndNeverEnrolledMembers()
    {
        var data = await SeedSeasons();

        // Last season but not this season
        var lapsed = await Names(new MemberFilter { InSeasonIds = [data.LastSeasonId], NotInSeasonIds = [data.ThisSeasonId] });
        lapsed.Should().BeEquivalentTo(["Lapsed"]);

        // Both seasons
        var returning = await Names(new MemberFilter { InSeasonIds = [data.LastSeasonId, data.ThisSeasonId] });
        returning.Should().BeEquivalentTo(["Returning"]);

        // This season only
        var newMembers = await Names(new MemberFilter { InSeasonIds = [data.ThisSeasonId], NotInSeasonIds = [data.LastSeasonId] });
        newMembers.Should().BeEquivalentTo(["New"]);

        // Never enrolled
        var never = await Names(new MemberFilter { NotInSeasonIds = [data.LastSeasonId, data.ThisSeasonId] });
        never.Should().BeEquivalentTo(["Never"]);
    }

    [Fact]
    public async Task GetMembers_SeasonFilter_EmailsMatchTheList()
    {
        var data = await SeedSeasons();
        var filter = new MemberFilter { SearchTerm = Marker, InSeasonIds = [data.LastSeasonId], NotInSeasonIds = [data.ThisSeasonId] };

        var members = await SendAsync(new GetMembers(AllOnOnePage, null, filter));
        var emails = await SendAsync(new GetMemberEmails(filter));

        emails.Emails.Should().BeEquivalentTo(members.Entities.Select(m => m.ContactEmail));
        emails.Emails.Should().ContainSingle().Which.Should().Be("lapsed@zfilter.test");
    }

    [Fact]
    public async Task GetMembers_NoBookingsInSeason_ReturnsEnrolledMembersWithoutPlayedBooking()
    {
        var data = await SeedSeasons();

        await QueryAsync(async ctx =>
        {
            var court = ctx.TestData().Court1;
            var playMode = ctx.TestData().Club.PlayModes[0];
            var from = DateTimeOffset.UtcNow.AddDays(-2);
            ctx.Add(new Booking(court.ClubId, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id, [data.ReturningId]));

            // A future booking does not count as played
            var future = DateTimeOffset.UtcNow.AddDays(2);
            ctx.Add(new Booking(court.ClubId, court.Id, playMode.Id, new DateTimeOffsetInterval(future, future.AddHours(1)), TimeZoneInfo.Utc.Id, [data.NewId]));
            await ctx.SaveChangesAsync();
        });

        var names = await Names(new MemberFilter { InSeasonIds = [data.ThisSeasonId], NoBookingsInSeasonId = data.ThisSeasonId });

        names.Should().BeEquivalentTo(["New"]);
    }

    [Fact]
    public async Task GetMembers_GenderAgeAndEmailFilters_Work()
    {
        await QueryAsync(async ctx =>
        {
            var parent = ctx.TestData().User;
            await AddMember(ctx, "Kid", DateOnly.FromDateTime(DateTime.Today).AddYears(-8), Gender.Female, email: null, belongsToUserId: parent.Id);
            await AddMember(ctx, "Teen", DateOnly.FromDateTime(DateTime.Today).AddYears(-15), Gender.Male, "teen@zfilter.test");
            await AddMember(ctx, "Adult", DateOnly.FromDateTime(DateTime.Today).AddYears(-30), Gender.Female, "adult@zfilter.test");
            await AddMember(ctx, "Senior", DateOnly.FromDateTime(DateTime.Today).AddYears(-60), Gender.Male, "senior@zfilter.test");
        });

        (await Names(new MemberFilter { AgeGroups = [SharedAgeGroup.Kids] })).Should().BeEquivalentTo(["Kid"]);
        (await Names(new MemberFilter { AgeGroups = [SharedAgeGroup.Teenagers, SharedAgeGroup.Seniors] })).Should().BeEquivalentTo(["Teen", "Senior"]);
        (await Names(new MemberFilter { Genders = [SharedGender.Female] })).Should().BeEquivalentTo(["Kid", "Adult"]);
        (await Names(new MemberFilter { HasEmail = false })).Should().BeEquivalentTo(["Kid"]);
        (await Names(new MemberFilter { HasEmail = true, Genders = [SharedGender.Male] })).Should().BeEquivalentTo(["Teen", "Senior"]);
    }

    [Fact]
    public async Task GetMembers_RoleFilter_Works()
    {
        await QueryAsync(async ctx =>
        {
            await AddMember(ctx, "Trainer", new DateOnly(1990, 1, 1), Gender.Male, "trainer@zfilter.test", roles: [MemberRole.User, MemberRole.Trainer]);
            await AddMember(ctx, "Player", new DateOnly(1990, 1, 1), Gender.Male, "player@zfilter.test");
        });

        (await Names(new MemberFilter { Roles = [SharedMemberRole.Trainer] })).Should().BeEquivalentTo(["Trainer"]);
    }

    [Fact]
    public async Task GetMembers_ReturnsLastPlayedAndSortsByIt()
    {
        var data = await SeedSeasons();

        await QueryAsync(async ctx =>
        {
            var court = ctx.TestData().Court1;
            var playMode = ctx.TestData().Club.PlayModes[0];
            var older = DateTimeOffset.UtcNow.AddDays(-20);
            var newer = DateTimeOffset.UtcNow.AddDays(-3);
            ctx.Add(new Booking(court.ClubId, court.Id, playMode.Id, new DateTimeOffsetInterval(older, older.AddHours(1)), TimeZoneInfo.Utc.Id, [data.LapsedId]));
            ctx.Add(new Booking(court.ClubId, court.Id, playMode.Id, new DateTimeOffsetInterval(newer, newer.AddHours(1)), TimeZoneInfo.Utc.Id, [data.ReturningId]));
            await ctx.SaveChangesAsync();
        });

        var sort = new SortParameters { [nameof(MemberResult.LastPlayed)] = SortDirection.Descending };
        var result = await SendAsync(new GetMembers(AllOnOnePage, sort, new MemberFilter { SearchTerm = Marker, InSeasonIds = [data.LastSeasonId] }));

        result.Entities.Select(m => m.FirstName).Should().Equal("Returning", "Lapsed");
        result.Entities[0].LastPlayed.Should().NotBeNull();
        result.Entities[0].ContactEmail.Should().Be("returning@zfilter.test");
    }

    [Fact]
    public async Task GetMembers_Paging_ReturnsRequestedPage()
    {
        await SeedSeasons();

        var result = await SendAsync(new GetMembers(new PaginationParameters { Page = 2, PageSize = 3, EnablePaging = true }, null, new MemberFilter { SearchTerm = Marker }));

        result.Total.Should().Be(4);
        result.Page.Should().Be(2);
        result.Entities.Should().ContainSingle();
    }

    private async Task<List<string>> Names(MemberFilter filter)
    {
        filter.SearchTerm ??= Marker;
        var result = await SendAsync(new GetMembers(AllOnOnePage, null, filter));
        return result.Entities.Select(m => m.FirstName).ToList();
    }

    private sealed record SeasonData(int LastSeasonId, int ThisSeasonId, int ReturningId, int LapsedId, int NewId, int NeverId);

    private Task<SeasonData> SeedSeasons() => QueryAsync(async ctx =>
    {
        var clubId = ctx.TestData().Club.Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastSeason = new Season(clubId, new DateOnlyInterval(today.AddYears(-1).AddDays(-30), today.AddYears(-1).AddDays(30)));
        var thisSeason = new Season(clubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
        ctx.AddRange(lastSeason, thisSeason);
        await ctx.SaveChangesAsync();

        var returning = await AddMember(ctx, "Returning", new DateOnly(1990, 1, 1), Gender.Male, "returning@zfilter.test");
        var lapsed = await AddMember(ctx, "Lapsed", new DateOnly(1990, 1, 1), Gender.Female, "lapsed@zfilter.test");
        var newMember = await AddMember(ctx, "New", new DateOnly(1990, 1, 1), Gender.Male, "new@zfilter.test");
        var never = await AddMember(ctx, "Never", new DateOnly(1990, 1, 1), Gender.Male, "never@zfilter.test");

        ctx.AddRange(
            new MemberSeason(returning.Id, lastSeason.Id),
            new MemberSeason(returning.Id, thisSeason.Id),
            new MemberSeason(lapsed.Id, lastSeason.Id),
            new MemberSeason(newMember.Id, thisSeason.Id));
        await ctx.SaveChangesAsync();

        return new SeasonData(lastSeason.Id, thisSeason.Id, returning.Id, lapsed.Id, newMember.Id, never.Id);
    });

    private static async Task<ClubMember> AddMember(
        AppDbContext ctx, string firstName, DateOnly birthday, Gender gender, string? email, int? belongsToUserId = null, MemberRole[]? roles = null)
    {
        var user = email is null
            ? new User(firstName, Marker, birthday, gender, belongsToUserId ?? ctx.TestData().User.Id)
            : new User(email, email, firstName, Marker, birthday, gender);
        ctx.Add(user);
        await ctx.SaveChangesAsync();

        var member = new ClubMember(user.Id, ctx.TestData().Club.Id, roles ?? [MemberRole.User]);
        ctx.Add(member);
        await ctx.SaveChangesAsync();
        return member;
    }
}
