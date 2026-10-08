using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class AwardOneTimeBadgeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AwardOneTimeBadge_MultipleMembers_AwardsAll()
    {
        var (clubId, badgeId, member1Id, member2Id) = await SeedBadge();

        await SendAsync(new AwardOneTimeBadge(clubId, badgeId, [member1Id, member2Id]));

        var awardedMemberIds = await QueryAsync(ctx => ctx.MemberOneTimeBadges
            .Where(motb => motb.OneTimeBadgeId == badgeId)
            .Select(motb => motb.MemberId)
            .ToListAsync());

        awardedMemberIds.Should().BeEquivalentTo([member1Id, member2Id]);
    }

    [Fact]
    public async Task AwardOneTimeBadge_MemberAlreadyAwarded_IsIdempotent()
    {
        var (clubId, badgeId, member1Id, member2Id) = await SeedBadge();

        await SendAsync(new AwardOneTimeBadge(clubId, badgeId, [member1Id]));
        await SendAsync(new AwardOneTimeBadge(clubId, badgeId, [member1Id, member2Id]));

        var awardedMemberIds = await QueryAsync(ctx => ctx.MemberOneTimeBadges
            .Where(motb => motb.OneTimeBadgeId == badgeId)
            .Select(motb => motb.MemberId)
            .ToListAsync());

        awardedMemberIds.Should().BeEquivalentTo([member1Id, member2Id]);
    }

    private async Task<(int ClubId, int BadgeId, int Member1Id, int Member2Id)> SeedBadge()
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var clubId = testData.Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var badge = new OneTimeBadge(clubId, season.Id, "Singles Champion Men", "Description");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();

            return (clubId, badge.Id, testData.Member1.Id, testData.Member2.Id);
        });
    }
}
