using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using MembershipOrderStatus = Bookennis.Domain.Memberships.MembershipOrderStatus;
using MembershipPaymentMethod = Bookennis.Domain.Memberships.MembershipPaymentMethod;
using OrderStatus = Bookennis.Shared.Controller.Memberships.MembershipOrderStatus;

namespace Bookennis.Api.Tests.Business.Memberships;

public class GetMembershipPaymentsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMembershipPayments_ListsEveryClubMemberAndEveryOrderOfTheSeason()
    {
        var seeded = await QueryAsync(ctx => MembershipPaymentSeed.SeasonWithPayments(ctx));

        var result = await SendAsync(new GetMembershipPayments(TestDataSeed.ClubId, seeded.Family.SeasonId, null));

        result.SeasonId.Should().Be(seeded.Family.SeasonId);
        result.Members.Select(m => m.MemberId).Should().BeEquivalentTo([.. seeded.Family.Everybody, TestDataSeed.Member2Id, seeded.Cash, seeded.Nobody]);

        Enrollment(seeded.Family.Parent).Should().Be(MembershipEnrollmentSource.PaidOnline);
        Enrollment(seeded.Family.Kid).Should().Be(MembershipEnrollmentSource.PaidOnline);
        Enrollment(seeded.Cash).Should().Be(MembershipEnrollmentSource.MarkedAsPaid);
        Enrollment(TestDataSeed.Member2Id).Should().Be(MembershipEnrollmentSource.ByHand);
        Enrollment(seeded.Nobody).Should().Be(MembershipEnrollmentSource.NotEnrolled);

        var kid = result.Members.Single(m => m.MemberId == seeded.Family.Kid);
        kid.Should().BeEquivalentTo(new { FirstName = "Kid", LastName = "Tester", FamilyId = (int?)seeded.Family.FamilyId, IsChild = true }, o => o.ExcludingMissingMembers());

        result.Orders.Select(o => o.Id).Should().Equal(seeded.OpenOrderId, seeded.CashOrderId, seeded.FamilyOrderId);
        var familyOrder = result.Orders.Single(o => o.Id == seeded.FamilyOrderId);
        familyOrder.Should().BeEquivalentTo(new { PlanName = "Family", AmountCents = 21500, Status = OrderStatus.Paid, PayerMemberId = seeded.Family.Parent }, o => o.ExcludingMissingMembers());
        familyOrder.MemberIds.Should().BeEquivalentTo(seeded.Family.Everybody);
        result.Orders.Single(o => o.Id == seeded.CashOrderId).Note.Should().Be("cash");

        MembershipEnrollmentSource Enrollment(int memberId) => result.Members.Single(m => m.MemberId == memberId).Enrollment;
    }

    [Fact]
    public async Task GetMembershipPayments_WithStatus_OnlyReturnsOrdersInThatStatus()
    {
        var seeded = await QueryAsync(ctx => MembershipPaymentSeed.SeasonWithPayments(ctx));

        var result = await SendAsync(new GetMembershipPayments(TestDataSeed.ClubId, seeded.Family.SeasonId, MembershipOrderStatus.AwaitingPayment));

        result.Orders.Should().ContainSingle().Which.Id.Should().Be(seeded.OpenOrderId);
        result.Members.Should().HaveCount(7, "the members are always complete");
    }

    [Fact]
    public async Task GetMembershipPayments_CarriesNothingButNamesPlanAndPayment()
    {
        var seeded = await QueryAsync(ctx => MembershipPaymentSeed.SeasonWithPayments(ctx));

        var result = await SendAsync(new GetMembershipPayments(TestDataSeed.ClubId, seeded.Family.SeasonId, null));

        // Treasurers have no access to the members list: no email, birthday or address may come through here
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("@").And.NotContain("User St 1").And.NotContain("Graz").And.NotContain("2000-01-01").And.NotContain("1987");
    }

    [Fact]
    public async Task GetMembershipPaymentsSummary_AddsUpTheSeason()
    {
        var seeded = await QueryAsync(ctx => MembershipPaymentSeed.SeasonWithPayments(ctx));

        var result = await SendAsync(new GetMembershipPaymentsSummary(TestDataSeed.ClubId, seeded.Family.SeasonId));

        result.Should().BeEquivalentTo(new GetMembershipPaymentsSummaryResult
        {
            ReceivedCents = 21500 + 12000,
            ProcessingCents = 0,
            OpenCents = 12000,
            Currency = "eur",
            MembersPaid = 5,
            MembersEnrolled = 6,
            MembersTotal = 7,
            OrdersByStatus = new() { [OrderStatus.Paid] = 2, [OrderStatus.AwaitingPayment] = 1 },
        });
    }
}
