using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.MemberBadges;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class UpdateMemberBadgeSettingsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateMemberBadgeSettings_SetsDisplayBadgeAndTrophyCaseVisibility()
    {
        var (memberId, badgeId) = await SeedMemberBadge(memberId: TestDataSeed.Member1Id);

        await SendAsync(new UpdateMemberBadgeSettings(memberId, new UpdateMemberBadgeSettingsModel
        {
            DisplayBadgeId = badgeId,
            TrophyCasePublic = false
        }));

        var settings = await QueryAsync(ctx => ctx.MemberBadgeSettings.SingleAsync(s => s.MemberId == memberId));

        settings.DisplayBadgeId.Should().Be(badgeId);
        settings.TrophyCasePublic.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMemberBadgeSettings_DisplayBadgeFromOtherMember_SetsDisplayBadgeToNull()
    {
        var (_, otherMembersBadgeId) = await SeedMemberBadge(memberId: TestDataSeed.Member2Id);

        await SendAsync(new UpdateMemberBadgeSettings(TestDataSeed.Member1Id, new UpdateMemberBadgeSettingsModel
        {
            DisplayBadgeId = otherMembersBadgeId,
            TrophyCasePublic = false
        }));

        var settings = await QueryAsync(ctx => ctx.MemberBadgeSettings.SingleAsync(s => s.MemberId == TestDataSeed.Member1Id));

        settings.DisplayBadgeId.Should().BeNull();
        settings.TrophyCasePublic.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMemberBadgeSettings_SetsDisplayOneTimeBadge_ClearsDisplayBadge()
    {
        var (memberId, badgeId) = await SeedMemberBadge(memberId: TestDataSeed.Member1Id);
        var oneTimeBadgeId = await SeedMemberOneTimeBadge(memberId);

        await SendAsync(new UpdateMemberBadgeSettings(memberId, new UpdateMemberBadgeSettingsModel
        {
            DisplayBadgeId = badgeId,
            TrophyCasePublic = true
        }));

        await SendAsync(new UpdateMemberBadgeSettings(memberId, new UpdateMemberBadgeSettingsModel
        {
            DisplayOneTimeBadgeId = oneTimeBadgeId,
            TrophyCasePublic = true
        }));

        var settings = await QueryAsync(ctx => ctx.MemberBadgeSettings.SingleAsync(s => s.MemberId == memberId));
        settings.DisplayOneTimeBadgeId.Should().Be(oneTimeBadgeId);
        settings.DisplayBadgeId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMemberBadgeSettings_SetsDisplayBadge_ClearsDisplayOneTimeBadge()
    {
        var (memberId, badgeId) = await SeedMemberBadge(memberId: TestDataSeed.Member1Id);
        var oneTimeBadgeId = await SeedMemberOneTimeBadge(memberId);

        await SendAsync(new UpdateMemberBadgeSettings(memberId, new UpdateMemberBadgeSettingsModel
        {
            DisplayOneTimeBadgeId = oneTimeBadgeId,
            TrophyCasePublic = true
        }));

        await SendAsync(new UpdateMemberBadgeSettings(memberId, new UpdateMemberBadgeSettingsModel
        {
            DisplayBadgeId = badgeId,
            TrophyCasePublic = true
        }));

        var settings = await QueryAsync(ctx => ctx.MemberBadgeSettings.SingleAsync(s => s.MemberId == memberId));
        settings.DisplayBadgeId.Should().Be(badgeId);
        settings.DisplayOneTimeBadgeId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMemberBadgeSettings_BothDisplayBadgeTypesSet_ThrowsPreconditionException()
    {
        var (memberId, badgeId) = await SeedMemberBadge(memberId: TestDataSeed.Member1Id);
        var oneTimeBadgeId = await SeedMemberOneTimeBadge(memberId);

        var act = () => SendAsync(new UpdateMemberBadgeSettings(memberId, new UpdateMemberBadgeSettingsModel
        {
            DisplayBadgeId = badgeId,
            DisplayOneTimeBadgeId = oneTimeBadgeId,
            TrophyCasePublic = true
        }));

        await act.Should().ThrowAsync<PreconditionException>();
    }

    private async Task<(int MemberId, int BadgeId)> SeedMemberBadge(int memberId)
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var tier = new BadgeTier(clubId, season.Id, memberId == TestDataSeed.Member1Id ? 1 : 2, "Tier", "Tier badge", 1, 1);
            ctx.Add(tier);
            await ctx.SaveChangesAsync();

            var memberBadge = new MemberBadge(memberId, tier.Id, season.Id);
            ctx.Add(memberBadge);
            await ctx.SaveChangesAsync();

            return (memberId, memberBadge.Id);
        });
    }

    private async Task<int> SeedMemberOneTimeBadge(int memberId)
    {
        return await QueryAsync(async ctx =>
        {
            var clubId = ctx.TestData().Club.Id;
            var season = new Season(clubId, new DateOnlyInterval(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var badge = new OneTimeBadge(clubId, season.Id, "Singles Champion Men", "Description");
            ctx.Add(badge);
            await ctx.SaveChangesAsync();

            var memberOneTimeBadge = new MemberOneTimeBadge(memberId, badge.Id);
            ctx.Add(memberOneTimeBadge);
            await ctx.SaveChangesAsync();

            return memberOneTimeBadge.Id;
        });
    }
}
