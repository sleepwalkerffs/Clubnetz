using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMembersSummaryTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMembersSummary_NoSeasons_CanTranslateQuery()
    {
        SetTenantId(Query(ctx => ctx.TestData().Club.Id));

        var result = await SendAsync(new GetMembersSummary());

        result.TotalMembers.Should().BeGreaterThanOrEqualTo(2);
        result.ActiveSeasonId.Should().BeNull();
        result.ActiveSeasonMembers.Should().Be(0);
        result.LapsedMembers.Should().Be(0);
    }

    [Fact]
    public async Task GetMembersSummary_CountsActiveNewAndLapsedMembers()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(clubId);

        var (lastSeasonId, thisSeasonId) = await QueryAsync(async ctx =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var lastSeason = new Season(clubId, new DateOnlyInterval(today.AddYears(-1).AddDays(-30), today.AddYears(-1).AddDays(30)));
            var thisSeason = new Season(clubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(30)));
            ctx.AddRange(lastSeason, thisSeason);

            var childUser = new User("No", "Email", new DateOnly(2016, 1, 1), Gender.Male, ctx.TestData().User.Id);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();
            var child = new ClubMember(childUser.Id, clubId, [MemberRole.User]);
            ctx.Add(child);
            await ctx.SaveChangesAsync();

            var member1 = ctx.TestData().Member1.Id;
            var member2 = ctx.TestData().Member2.Id;

            // Member 1 returns, member 2 is new, the child did not come back
            ctx.AddRange(
                new MemberSeason(member1, lastSeason.Id),
                new MemberSeason(child.Id, lastSeason.Id),
                new MemberSeason(member1, thisSeason.Id),
                new MemberSeason(member2, thisSeason.Id));
            await ctx.SaveChangesAsync();

            return (lastSeason.Id, thisSeason.Id);
        });

        var result = await SendAsync(new GetMembersSummary());

        result.ActiveSeasonId.Should().Be(thisSeasonId);
        result.PreviousSeasonId.Should().Be(lastSeasonId);
        result.ActiveSeasonMembers.Should().Be(2);
        result.NewMembers.Should().Be(1);
        result.LapsedMembers.Should().Be(1);
        result.WithoutEmail.Should().Be(1);
    }
}
