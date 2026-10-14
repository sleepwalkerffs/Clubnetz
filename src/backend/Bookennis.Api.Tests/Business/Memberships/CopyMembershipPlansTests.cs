using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Memberships;

public class CopyMembershipPlansTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task CopyMembershipPlans_ShiftsBirthYearsAndDatesByTheDifferenceOfTheSeasonYears()
    {
        var (sourceId, targetId) = await SeedSeasons();
        await QueryAsync(async ctx =>
        {
            await MembershipSeed.SeedPlan(ctx, sourceId, MembershipSeed.Data("Youth", 6000) with { BirthYearFrom = 2009, BirthYearTo = 2013 }, sortOrder: 2);
            await MembershipSeed.SeedPlan(ctx, sourceId, MembershipSeed.Data("Early bird", 10000) with { AvailableUntil = new DateOnly(2026, 3, 31), RequiresApproval = true }, sortOrder: 1);
            var family = await MembershipSeed.SeedPlan(ctx, sourceId, MembershipSeed.FamilyData(), sortOrder: 3);
            await MembershipSeed.SeedOrder(ctx, family, TestDataSeed.Member1Id, TestDataSeed.Member1Id);
        });

        await SendAsync(new CopyMembershipPlans(TestDataSeed.ClubId, sourceId, targetId));

        var copies = await QueryAsync(ctx => ctx.MembershipPlans.Where(p => p.SeasonId == targetId).OrderBy(p => p.SortOrder).ToListAsync());
        copies.Select(p => p.Name).Should().Equal("Early bird", "Youth", "Family");
        copies.Select(p => p.SortOrder).Should().Equal(1, 2, 3);

        copies[0].AvailableUntil.Should().Be(new DateOnly(2027, 3, 31));
        copies[0].RequiresApproval.Should().BeTrue();
        copies[1].BirthYearFrom.Should().Be(2010);
        copies[1].BirthYearTo.Should().Be(2014);
        copies[2].Should().BeEquivalentTo(new { PriceCents = 21500, MaxAdults = 2, IncludesChildren = true }, o => o.ExcludingMissingMembers());

        (await QueryAsync(ctx => ctx.MembershipOrders.CountAsync())).Should().Be(1, "orders are never copied");
        (await QueryAsync(ctx => ctx.MembershipPlans.CountAsync(p => p.SeasonId == sourceId))).Should().Be(3);
    }

    [Fact]
    public async Task CopyMembershipPlans_SourceWithoutPlans_Throws()
    {
        var (sourceId, targetId) = await SeedSeasons();

        var act = () => SendAsync(new CopyMembershipPlans(TestDataSeed.ClubId, sourceId, targetId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CopyMembershipPlans.ErrorCode.MembershipPlanSourceSeasonEmpty));
    }

    [Fact]
    public async Task CopyMembershipPlans_TargetAlreadyHasPlans_Throws()
    {
        var (sourceId, targetId) = await SeedSeasons();
        await QueryAsync(async ctx =>
        {
            await MembershipSeed.SeedPlan(ctx, sourceId);
            await MembershipSeed.SeedPlan(ctx, targetId, MembershipSeed.Data("Existing"));
        });

        var act = () => SendAsync(new CopyMembershipPlans(TestDataSeed.ClubId, sourceId, targetId));

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(CopyMembershipPlans.ErrorCode.MembershipPlanTargetSeasonNotEmpty));
        (await QueryAsync(ctx => ctx.MembershipPlans.CountAsync(p => p.SeasonId == targetId))).Should().Be(1);
    }

    private Task<(int SourceId, int TargetId)> SeedSeasons() => QueryAsync(async ctx =>
    {
        var source = await MembershipSeed.SeedSeason(ctx, new DateOnly(2026, 4, 1), new DateOnly(2026, 10, 31));
        var target = await MembershipSeed.SeedSeason(ctx, new DateOnly(2027, 4, 1), new DateOnly(2027, 10, 31));
        return (source.Id, target.Id);
    });
}
