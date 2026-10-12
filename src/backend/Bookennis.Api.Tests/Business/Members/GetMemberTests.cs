using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.Business.Memberships;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Members;
using Bookennis.Shared.Controller.Memberships;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Fusonic.Extensions.Common.Entities;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMemberTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMember_ReturnsCorrectMemberDetails()
    {
        var (memberId, user) = Query(ctx =>
        {
            var member = ctx.TestData().Member1;
            var u = ctx.TestData().User;
            return (member.Id, u);
        });

        var result = await SendAsync(new GetMember(memberId));

        result.MemberId.Should().Be(memberId);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
    }

    [Fact]
    public async Task GetMember_TellsHowTheMemberGotIntoEachSeason()
    {
        var seasons = await QueryAsync(async ctx =>
        {
            var byHand = await MembershipSeed.SeedSeason(ctx, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31));
            var online = await MembershipSeed.SeedSeason(ctx, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
            var cash = await MembershipSeed.SeedSeason(ctx, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
            await MembershipSeed.SeedSeason(ctx, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

            ctx.Add(new MemberSeason(TestDataSeed.Member1Id, byHand.Id));

            var onlineOrder = await MembershipSeed.SeedOrder(ctx, await MembershipSeed.SeedPlan(ctx, online.Id), TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            onlineOrder.MarkPaid(Domain.Memberships.MembershipPaymentMethod.Online, DateTimeOffset.UtcNow, paymentIntentId: "pi_1");
            ctx.Add(new MemberSeason(TestDataSeed.Member1Id, online.Id, onlineOrder.Id));

            var cashOrder = await MembershipSeed.SeedOrder(ctx, await MembershipSeed.SeedPlan(ctx, cash.Id), TestDataSeed.Member1Id, TestDataSeed.Member1Id);
            cashOrder.MarkPaid(Domain.Memberships.MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow);
            ctx.Add(new MemberSeason(TestDataSeed.Member1Id, cash.Id, cashOrder.Id));

            await ctx.SaveChangesAsync();
            return new { ByHand = byHand.Id, Online = online.Id, Cash = cash.Id };
        });

        var result = await SendAsync(new GetMember(TestDataSeed.Member1Id));

        result.AllowedSeasonIds.Should().BeEquivalentTo([seasons.ByHand, seasons.Online, seasons.Cash]);
        result.PaidSeasonIds.Should().BeEquivalentTo([seasons.Online, seasons.Cash]);
        result.SeasonEnrollments.Should().BeEquivalentTo(
        [
            new MemberSeasonEnrollment(seasons.ByHand, MembershipEnrollmentSource.ByHand),
            new MemberSeasonEnrollment(seasons.Online, MembershipEnrollmentSource.PaidOnline),
            new MemberSeasonEnrollment(seasons.Cash, MembershipEnrollmentSource.MarkedAsPaid),
        ]);

        // The members list carries the paid seasons too, for the edit dialog
        var list = await SendAsync(new GetMembers(new Bookennis.Api.Infrastructure.Utils.Paging.PaginationParameters { Page = 1, PageSize = 100, EnablePaging = true }, null));
        list.Entities.Single(m => m.MemberId == TestDataSeed.Member1Id).PaidSeasonIds.Should().BeEquivalentTo([seasons.Online, seasons.Cash]);
        list.Entities.Single(m => m.MemberId == TestDataSeed.Member2Id).PaidSeasonIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMember_NonExistentMember_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new GetMember(int.MaxValue));

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
