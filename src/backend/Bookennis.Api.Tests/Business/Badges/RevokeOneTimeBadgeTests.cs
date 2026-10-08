using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class RevokeOneTimeBadgeTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task RevokeOneTimeBadge_AwardedMember_RemovesAward()
    {
        var (clubId, badgeId, memberId) = await SeedAwardedBadge();

        await SendAsync(new RevokeOneTimeBadge(clubId, badgeId, memberId));

        var exists = await QueryAsync(ctx => ctx.MemberOneTimeBadges.AnyAsync(motb => motb.OneTimeBadgeId == badgeId && motb.MemberId == memberId));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeOneTimeBadge_NotAwarded_DoesNotThrow()
    {
        var (clubId, badgeId, memberId) = await SeedAwardedBadge();

        await SendAsync(new RevokeOneTimeBadge(clubId, badgeId, memberId));
        var act = () => SendAsync(new RevokeOneTimeBadge(clubId, badgeId, memberId));

        await act.Should().NotThrowAsync();
    }

    private async Task<(int ClubId, int BadgeId, int MemberId)> SeedAwardedBadge()
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

            ctx.Add(new MemberOneTimeBadge(testData.Member1.Id, badge.Id));
            await ctx.SaveChangesAsync();

            return (clubId, badge.Id, testData.Member1.Id);
        });
    }
}
