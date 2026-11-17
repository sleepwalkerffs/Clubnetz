using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Memberships;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;

namespace Bookennis.Api.Tests.Business.Memberships;

/// <summary>A club with tennis and padel courts sells its memberships per sport: one membership per sport and season.</summary>
public class MembershipSportsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task PadelAfterTennis_IsAllowedAndAddsTheSportToTheEnrollment()
    {
        var (tennisId, padelId) = await SeedPlans();

        var tennis = await Record(tennisId);
        await Record(padelId);

        var enrollment = await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync(ms => ms.MemberId == TestDataSeed.Member1Id));
        enrollment.Sports.Should().Equal(Sport.Tennis, Sport.Padel);
        enrollment.MembershipOrderId.Should().Be(tennis.Id, "the first order enrolled the member");
    }

    [Fact]
    public async Task TheSameSportTwice_IsRejected()
    {
        var (tennisId, _) = await SeedPlans();
        await Record(tennisId);

        var act = () => Record(tennisId);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
    }

    [Fact]
    public async Task APlanForEverySport_OverlapsWithEveryOtherPlan()
    {
        var (_, padelId) = await SeedPlans();
        var allId = await QueryAsync(async ctx => (await MembershipSeed.SeedPlan(ctx, await SeasonId(ctx), MembershipSeed.Data("Everything"), 3)).Id);
        await Record(padelId);

        var act = () => Record(allId);

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
    }

    [Fact]
    public async Task GetMembershipOptions_WithTennis_OffersPadelButNotTennis()
    {
        var (tennisId, padelId) = await SeedPlans();
        await Record(tennisId);

        var options = await SendAsync(new GetMembershipOptions(TestDataSeed.ClubId, TestDataSeed.UserId, null));

        var tennis = options.Plans.Single(p => p.Plan.Id == tennisId).Choices.Single(c => c.MemberIds.SequenceEqual([TestDataSeed.Member1Id]));
        tennis.IsEligible.Should().BeFalse();
        tennis.Reasons.Should().Contain(nameof(MembershipEligibility.ErrorCode.AlreadyHasMembership));
        options.Plans.Single(p => p.Plan.Id == padelId).Choices.Single(c => c.MemberIds.SequenceEqual([TestDataSeed.Member1Id])).IsEligible.Should().BeTrue();
        options.Plans.Single(p => p.Plan.Id == padelId).Plan.Sports.Should().Equal(Bookennis.Shared.Controller.Shared.Sport.Padel);
    }

    [Fact]
    public async Task GetMembershipPayments_ListsTheSportsOfEachOrder()
    {
        var (tennisId, _) = await SeedPlans();
        var tennis = await Record(tennisId);
        var seasonId = await QueryAsync(SeasonId);

        var payments = await SendAsync(new GetMembershipPayments(TestDataSeed.ClubId, seasonId, null));

        payments.Orders.Single(o => o.Id == tennis.Id).Sports.Should().Equal(Bookennis.Shared.Controller.Shared.Sport.Tennis);
    }

    [Fact]
    public async Task CancellingThePadelOrder_KeepsTennis()
    {
        var (tennisId, padelId) = await SeedPlans();
        await Record(tennisId);
        var padel = await Record(padelId);

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, padel.Id, ByTreasurer: true, RemoveEnrollments: true));

        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync(ms => ms.MemberId == TestDataSeed.Member1Id))).Sports.Should().Equal(Sport.Tennis);
    }

    [Fact]
    public async Task CancellingTheTennisOrder_KeepsPadelOnTheEnrollmentItCreated()
    {
        var (tennisId, padelId) = await SeedPlans();
        var tennis = await Record(tennisId);
        await Record(padelId);

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, tennis.Id, ByTreasurer: true, RemoveEnrollments: true));

        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync(ms => ms.MemberId == TestDataSeed.Member1Id))).Sports.Should().Equal(Sport.Padel);
    }

    [Fact]
    public async Task CancellingBothOrdersTennisFirst_RemovesTheEnrollment()
    {
        var (tennisId, padelId) = await SeedPlans();
        var tennis = await Record(tennisId);
        var padel = await Record(padelId);

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, tennis.Id, ByTreasurer: true, RemoveEnrollments: true));
        var afterTennis = await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync(ms => ms.MemberId == TestDataSeed.Member1Id));
        afterTennis.MembershipOrderId.Should().Be(padel.Id, "the padel order still pays for the enrollment");

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, padel.Id, ByTreasurer: true, RemoveEnrollments: true));

        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse("no order pays for padel any more");
    }

    [Fact]
    public async Task WithdrawingAnUnpaidOrder_KeepsAnEnrollmentMadeByHand()
    {
        var (_, padelId) = await SeedPlans();
        await QueryAsync(async ctx =>
        {
            ctx.MemberSeasons.Add(new Domain.Members.MemberSeason(TestDataSeed.Member1Id, await SeasonId(ctx), sports: [Sport.Tennis, Sport.Padel]));
            await ctx.SaveChangesAsync();
            return 0;
        });
        var order = await SendAsync(new CreateMembershipOrder(TestDataSeed.ClubId, TestDataSeed.UserId, padelId, [TestDataSeed.Member1Id]));

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, order.Id, ByTreasurer: false));

        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync())).Sports.Should().Equal(Sport.Tennis, Sport.Padel);
    }

    [Fact]
    public async Task ChangingTheSportsOfAPlan_DoesNotChangeExistingOrders()
    {
        var (tennisId, padelId) = await SeedPlans();
        var padel = await Record(padelId);

        // The padel plan becomes a tennis plan: the order bought padel and keeps it
        await QueryAsync(async ctx =>
        {
            var plan = await ctx.MembershipPlans.SingleAsync(p => p.Id == padelId);
            plan.Update(MembershipSeed.Data("Padel", 9000) with { Sports = [Sport.Tennis] });
            await ctx.SaveChangesAsync();
            return 0;
        });

        (await QueryAsync(ctx => ctx.MembershipOrders.SingleAsync(o => o.Id == padel.Id))).Sports.Should().Equal(Sport.Padel);
        await Record(tennisId);
        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync())).Sports.Should().Equal(Sport.Tennis, Sport.Padel);
    }

    [Fact]
    public async Task CancellingTheOnlyOrder_RemovesTheEnrollment()
    {
        var (_, padelId) = await SeedPlans();
        var padel = await Record(padelId);

        await SendAsync(new CancelMembershipOrder(TestDataSeed.ClubId, padel.Id, ByTreasurer: true, RemoveEnrollments: true));

        (await QueryAsync(ctx => ctx.MemberSeasons.AnyAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task CopyMembershipPlans_KeepsTheSports()
    {
        var (_, padelId) = await SeedPlans();
        var target = await QueryAsync(ctx => MembershipSeed.SeedSeason(ctx, MembershipSeed.Today.AddDays(300), MembershipSeed.Today.AddDays(500)));
        var source = await QueryAsync(ctx => ctx.MembershipPlans.Where(p => p.Id == padelId).Select(p => p.SeasonId).SingleAsync());

        await SendAsync(new CopyMembershipPlans(TestDataSeed.ClubId, source, target.Id));

        (await QueryAsync(ctx => ctx.MembershipPlans.SingleAsync(p => p.SeasonId == target.Id && p.Name == "Padel"))).Sports.Should().Equal(Sport.Padel);
    }

    [Fact]
    public async Task UpdateMemberBookingOptions_SetsTheSportsOfAnEnrollmentByHand()
    {
        var seasonId = await QueryAsync(async ctx => (await MembershipSeed.SeedSeason(ctx)).Id);

        await SendAsync(new UpdateMemberBookingOptions(TestDataSeed.Member1Id, [seasonId], 5, new Dictionary<int, Sport[]> { [seasonId] = [Sport.Padel] }));
        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync())).Sports.Should().Equal(Sport.Padel);

        // Without sports for the season the enrollment keeps them
        await SendAsync(new UpdateMemberBookingOptions(TestDataSeed.Member1Id, [seasonId], 5));
        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync())).Sports.Should().Equal(Sport.Padel);

        await SendAsync(new UpdateMemberBookingOptions(TestDataSeed.Member1Id, [seasonId], 5, new Dictionary<int, Sport[]> { [seasonId] = [] }));
        (await QueryAsync(ctx => ctx.MemberSeasons.SingleAsync())).Sports.Should().BeEmpty("no sports is every sport");
    }

    private Task<(int TennisId, int PadelId)> SeedPlans() => QueryAsync(async ctx =>
    {
        var season = await MembershipSeed.SeedSeason(ctx);
        var tennis = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Tennis") with { Sports = [Sport.Tennis] }, 1);
        var padel = await MembershipSeed.SeedPlan(ctx, season.Id, MembershipSeed.Data("Padel", 9000) with { Sports = [Sport.Padel] }, 2);
        return (tennis.Id, padel.Id);
    });

    private static Task<int> SeasonId(Data.AppDbContext ctx) => ctx.Seasons.Select(s => s.Id).FirstAsync();

    private Task<Bookennis.Shared.Controller.Memberships.MembershipOrderDto> Record(int planId)
        => SendAsync(new RecordMembershipPayment(TestDataSeed.ClubId, TestDataSeed.AdminId, planId, [TestDataSeed.Member1Id], null, MembershipPaymentMethod.Cash, default, null));
}
