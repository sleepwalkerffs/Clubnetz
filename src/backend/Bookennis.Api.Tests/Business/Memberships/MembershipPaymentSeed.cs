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

internal static class MembershipPaymentSeed
{
    public static async Task<int> OpenOrder(Bookennis.Api.Data.AppDbContext context)
    {
        var plan = await MembershipSeed.SeedPlan(context, (await MembershipSeed.SeedSeason(context)).Id);
        return (await MembershipSeed.SeedOrder(context, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
    }

    public static async Task<int> OrderAwaitingApproval(Bookennis.Api.Data.AppDbContext context)
    {
        var plan = await MembershipSeed.SeedPlan(context, (await MembershipSeed.SeedSeason(context)).Id, MembershipSeed.Data("Student", 7000) with { RequiresApproval = true });
        return (await MembershipSeed.SeedOrder(context, plan, TestDataSeed.Member1Id, TestDataSeed.Member1Id)).Id;
    }

    /// <summary>
    /// A season with seven members: a family of four that paid online, a member who paid cash, the admin who was enrolled by hand
    /// and has an open order, and a member with nothing.
    /// </summary>
    public static async Task<SeededPayments> SeasonWithPayments(Bookennis.Api.Data.AppDbContext context)
    {
        var family = await MembershipSeed.SeedFamily(context);
        var cash = await MembershipSeed.AddMember(context, "Cash");
        var nobody = await MembershipSeed.AddMember(context, "Nobody");

        var familyPlan = await MembershipSeed.SeedPlan(context, family.SeasonId, MembershipSeed.FamilyData(), 1);
        var adultPlan = await MembershipSeed.SeedPlan(context, family.SeasonId, MembershipSeed.Data(), 2);

        var familyOrder = await MembershipSeed.SeedOrder(context, familyPlan, family.Parent, family.Everybody);
        familyOrder.MarkPaid(MembershipPaymentMethod.Online, DateTimeOffset.UtcNow.AddDays(-2), paymentIntentId: "pi_family");
        foreach (var memberId in family.Everybody)
            context.Add(new MemberSeason(memberId, family.SeasonId, familyOrder.Id));

        var cashOrder = await MembershipSeed.SeedOrder(context, adultPlan, cash, cash);
        cashOrder.MarkPaid(MembershipPaymentMethod.Cash, DateTimeOffset.UtcNow.AddDays(-1), TestDataSeed.Member2Id);
        cashOrder.SetNote("cash");
        context.Add(new MemberSeason(cash, family.SeasonId, cashOrder.Id));

        context.Add(new MemberSeason(TestDataSeed.Member2Id, family.SeasonId));
        var openOrder = await MembershipSeed.SeedOrder(context, adultPlan, TestDataSeed.Member2Id, TestDataSeed.Member2Id);

        // An order of another season must not show up
        var otherSeason = await MembershipSeed.SeedSeason(context, new DateOnly(2020, 1, 1), new DateOnly(2020, 12, 31));
        var otherPlan = await MembershipSeed.SeedPlan(context, otherSeason.Id, MembershipSeed.Data("Old"));
        await MembershipSeed.SeedOrder(context, otherPlan, nobody, nobody);

        await context.SaveChangesAsync();
        return new SeededPayments(family, cash, nobody, familyOrder.Id, cashOrder.Id, openOrder.Id);
    }
}
