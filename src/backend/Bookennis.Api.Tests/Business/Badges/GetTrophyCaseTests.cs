using Bookennis.Api.Business.Badges;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Badges;

public class GetTrophyCaseTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetTrophyCase_PublicTrophyCase_ReturnsBadges()
    {
        var seeded = await SeedTrophyCaseData(isPublic: true);

        var result = await SendAsync(new GetTrophyCase(seeded.RequestingUserId, seeded.TargetMemberId));

        result.IsOwnProfile.Should().BeFalse();
        result.IsPublic.Should().BeTrue();
        result.Clubs.Should().ContainSingle();
        result.Clubs[0].ClubId.Should().Be(seeded.ClubId);
        result.Clubs[0].Seasons.Should().ContainSingle();
        result.Clubs[0].Seasons[0].Badges.Should().ContainSingle();
        result.Clubs[0].Seasons[0].Badges[0].BadgeTierId.Should().Be(seeded.BadgeTierId);
        result.Clubs[0].Seasons[0].Badges[0].Name.Should().Be("Bronze");
    }

    [Fact]
    public async Task GetTrophyCase_PrivateTrophyCaseViewedByOtherUser_ReturnsEmpty()
    {
        var seeded = await SeedTrophyCaseData(isPublic: false);

        var result = await SendAsync(new GetTrophyCase(seeded.RequestingUserId, seeded.TargetMemberId));

        result.IsOwnProfile.Should().BeFalse();
        result.IsPublic.Should().BeFalse();
        result.Clubs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrophyCase_OwnProfile_ReturnsBadgesEvenWhenPrivate()
    {
        var seeded = await SeedTrophyCaseData(isPublic: false, viewAsOwner: true);

        var result = await SendAsync(new GetTrophyCase(seeded.RequestingUserId, seeded.TargetMemberId));

        result.IsOwnProfile.Should().BeTrue();
        result.IsPublic.Should().BeFalse();
        result.Clubs.Should().ContainSingle();
        result.Clubs[0].Seasons[0].Badges.Should().ContainSingle();
    }

    private async Task<(int ClubId, int RequestingUserId, int TargetMemberId, int BadgeTierId)> SeedTrophyCaseData(bool isPublic, bool viewAsOwner = false)
    {
        return await QueryAsync(async ctx =>
        {
            var testData = ctx.TestData();
            var club = testData.Club;
            var member = testData.Member1;

            var season = new Season(club.Id, new DateOnlyInterval(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
            ctx.Add(season);
            await ctx.SaveChangesAsync();

            var badgeTier = new BadgeTier(club.Id, season.Id, 1, "Bronze", "Bronze badge", 1, 1);
            ctx.Add(badgeTier);
            await ctx.SaveChangesAsync();

            ctx.Add(new MemberBadge(member.Id, badgeTier.Id, season.Id));

            var settings = new MemberBadgeSettings(member.Id);
            settings.SetTrophyCaseVisibility(isPublic);
            ctx.Add(settings);

            await ctx.SaveChangesAsync();

            var requestingUserId = viewAsOwner ? testData.User.Id : testData.Admin.Id;
            return (club.Id, requestingUserId, member.Id, badgeTier.Id);
        });
    }
}
