using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class GetSeasonClubMembersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetSeasonClubMembers_ExcludesGuestsAndOtherSeasonMembers()
    {
        var seeded = await SeedData();

        var result = await SendAsync(new GetSeasonClubMembers(seeded.ClubId, seeded.SeasonAId));

        result.Members.Select(m => m.MemberId).Should().BeEquivalentTo([seeded.Member1Id]);
        result.Members.Should().OnlyContain(m => !m.IsGuest);
    }

    private async Task<(int ClubId, int SeasonAId, int Member1Id)> SeedData()
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;

            var seasonA = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            var seasonB = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.AddRange(seasonA, seasonB);
            await ctx.SaveChangesAsync();

            // Member1 enrolled in season A only
            ctx.Add(new MemberSeason(testData.Member1.Id, seasonA.Id));

            // Member2 enrolled in season B only - should be excluded when querying season A
            ctx.Add(new MemberSeason(testData.Member2.Id, seasonB.Id));

            // A guest member enrolled in season A - should be excluded (guests aren't eligible)
            var guestUser = new User("onetimebadgeguest@test.com", "onetimebadgeguest@test.com", "Guest", "Player", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(guestUser);
            await ctx.SaveChangesAsync();
            var guestMember = new GuestMember(guestUser.Id, clubId);
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();
            ctx.Add(new MemberSeason(guestMember.Id, seasonA.Id));

            await ctx.SaveChangesAsync();

            return (clubId, seasonA.Id, testData.Member1.Id);
        });
    }
}
