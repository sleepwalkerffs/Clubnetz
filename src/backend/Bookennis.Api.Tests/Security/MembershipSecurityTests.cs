using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bookennis.Domain.Memberships;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Membership fees are money and membership: a member must not see or touch the order of somebody else, order for people outside the
/// own family, or do what only the treasurer does. Where the money of the club goes (its Stripe account) is for a signed-in admin only.
/// And nobody but Stripe may report a payment.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class MembershipSecurityTests(ApiSecurityFixture app)
{
    private string Club => $"/api/Clubs/{app.ClubA.Id}";
    private string Orders => $"{Club}/Memberships/orders";
    private string Payments => $"{Club}/MembershipPayments";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser? user, string? body = null, SeededKey? key = null)
    {
        var cookie = user is null ? null : await app.Login(user);
        return await app.Send(method, url, key?.Bearer, body, request =>
        {
            if (cookie is not null)
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
        });
    }

    /// <summary>An open order of <see cref="ApiSecurityFixture.OtherMemberOfA"/> for themself, with a note of the treasurer.</summary>
    private Task<int> NewOrderOfOtherMember(string planName = "Adult") => app.Db(async context =>
    {
        var plan = await NewPlan(context, planName);
        var order = new MembershipOrder(plan, app.OtherMemberOfA.MemberId, [app.OtherMemberOfA.MemberId], "Note of the treasurer");
        context.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    });

    private async Task<MembershipPlan> NewPlan(Data.AppDbContext context, string name, int priceCents = 12000, bool selfService = true)
    {
        var plan = new MembershipPlan(app.ClubA.Id, app.ClubA.SeasonId, new MembershipPlan.PlanData(
            $"{name} {Guid.NewGuid():N}", null, priceCents, 2, true, null, null, false, false, false, true, null, null, selfService), 99);
        context.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    private Task<MembershipOrder> Order(int orderId)
        => app.Db(c => c.MembershipOrders.IgnoreQueryFilters().Include(o => o.Members).SingleAsync(o => o.Id == orderId));

    private Task RemoveOrders(params int[] memberIds) => app.Db(async context =>
    {
        var orders = await context.MembershipOrders.IgnoreQueryFilters()
            .Where(o => o.ClubId == app.ClubA.Id && (memberIds.Contains(o.PayerMemberId ?? 0) || o.Members.Any(m => memberIds.Contains(m.MemberId))))
            .ToListAsync();
        var orderIds = orders.ConvertAll(o => o.Id);
        context.RemoveRange(await context.MemberSeasons.Where(ms => orderIds.Contains(ms.MembershipOrderId ?? 0)).ToListAsync());
        context.RemoveRange(orders);
        await context.SaveChangesAsync();
    });

    // ---------- the order of somebody else ----------

    [Fact]
    public async Task Member_CannotReadPayConfirmOrCancelTheOrderOfSomebodyElse()
    {
        var orderId = await NewOrderOfOtherMember();

        var attempts = new Dictionary<string, ApiResponse>
        {
            ["read"] = await Send(HttpMethod.Get, $"{Orders}/{orderId}", app.MemberOfA),
            ["pay"] = await Send(HttpMethod.Post, $"{Orders}/{orderId}/checkout", app.MemberOfA),
            ["confirm"] = await Send(HttpMethod.Post, $"{Orders}/{orderId}/confirm", app.MemberOfA),
            ["cancel"] = await Send(HttpMethod.Delete, $"{Orders}/{orderId}", app.MemberOfA),
        };

        using (new AssertionScope())
        {
            foreach (var (what, response) in attempts)
            {
                response.Status.Should().Be(HttpStatusCode.Forbidden, what);
                response.Body.Should().NotContain("Adult", what);
            }
        }

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        await RemoveOrders(app.OtherMemberOfA.MemberId);
    }

    [Fact]
    public async Task Member_OnlySeesTheOwnOrdersInTheOptions()
    {
        var orderId = await NewOrderOfOtherMember("Secretplan");

        var options = await Send(HttpMethod.Get, $"{Club}/Memberships/options?seasonId={app.ClubA.SeasonId}", app.MemberOfA);

        options.Status.Should().Be(HttpStatusCode.OK);
        options.Body.Should().Contain("Ownchild", "a parent sees the own family");
        options.Body.Should().NotContain("Otherchild").And.NotContain("Note of the treasurer");
        // Only the list of orders: a plan can have the same id as the order
        using var json = JsonDocument.Parse(options.Body);
        json.RootElement.GetProperty("orders").EnumerateArray().Select(o => o.GetProperty("id").GetInt32()).Should().NotContain(orderId);
        await RemoveOrders(app.OtherMemberOfA.MemberId);
    }

    [Fact]
    public async Task OwnerAndParent_ReadTheirOrder_ButNeverTheNoteOrAnythingOfStripe()
    {
        var orderId = await app.Db(async context =>
        {
            var plan = await NewPlan(context, "Familyplan");
            var order = new MembershipOrder(plan, app.MemberOfA.MemberId, [app.FamilyOfMemberOfA.ChildMemberId], "Note of the treasurer");
            order.StartCheckout("cs_test_secret_session");
            order.MarkProcessing("pi_secret_intent");
            context.Add(order);
            await context.SaveChangesAsync();
            return order.Id;
        });

        var response = await Send(HttpMethod.Get, $"{Orders}/{orderId}", app.MemberOfA);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain("Familyplan").And.Contain("Ownchild");
        response.Body.Should().NotContain("Note of the treasurer").And.NotContain("cs_test_secret_session").And.NotContain("pi_secret_intent");

        // The parent of another family has nothing to do with it
        (await Send(HttpMethod.Get, $"{Orders}/{orderId}", app.OtherMemberOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
        await RemoveOrders(app.MemberOfA.MemberId, app.FamilyOfMemberOfA.ChildMemberId);
    }

    // ---------- ordering for others ----------

    [Fact]
    public async Task Member_CannotOrderForSomebodyOutsideTheOwnFamily()
    {
        var planId = await app.Db(async context => (await NewPlan(context, "Group")).Id);
        var strangers = new[]
        {
            app.OtherMemberOfA.MemberId,
            app.FamilyOfOtherMemberOfA.ChildMemberId,
            app.ClubA.Admin.MemberId,
            app.ClubB.Member.MemberId,
        };

        using (new AssertionScope())
        {
            foreach (var stranger in strangers)
            {
                var alone = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{planId}},"memberIds":[{{stranger}}]}""");
                var withMe = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{planId}},"memberIds":[{{app.MemberOfA.MemberId}},{{stranger}}]}""");

                alone.Status.Should().Be(HttpStatusCode.Forbidden, $"member {stranger}");
                withMe.Status.Should().Be(HttpStatusCode.Forbidden, $"member {stranger} together with myself");
            }
        }

        (await app.Db(c => c.MembershipOrders.IgnoreQueryFilters().CountAsync(o => o.MembershipPlanId == planId))).Should().Be(0);
    }

    [Fact]
    public async Task Member_OrdersForTheOwnFamily_AndCanNotOrderAPlanOfAnotherClubOrOneOnlyTheTreasurerAssigns()
    {
        var (planId, hiddenPlanId) = await app.Db(async context => ((await NewPlan(context, "Family")).Id, (await NewPlan(context, "Honorary", selfService: false)).Id));
        var me = app.MemberOfA.MemberId;
        var child = app.FamilyOfMemberOfA.ChildMemberId;

        var planOfClubB = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{app.ClubB.PlanId}},"memberIds":[{{me}}]}""");
        var hiddenPlan = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{hiddenPlanId}},"memberIds":[{{me}}]}""");
        planOfClubB.Status.Should().Be(HttpStatusCode.NotFound);
        hiddenPlan.Status.Should().Be(HttpStatusCode.PreconditionFailed);
        hiddenPlan.Body.Should().Contain("MembershipPlanNotSelfService");

        var created = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{planId}},"memberIds":[{{me}},{{child}}]}""");
        created.Status.Should().Be(HttpStatusCode.OK, created.Body);

        var order = await app.Db(c => c.MembershipOrders.IgnoreQueryFilters().Include(o => o.Members).SingleAsync(o => o.MembershipPlanId == planId));
        order.ClubId.Should().Be(app.ClubA.Id);
        order.PayerMemberId.Should().Be(me);
        order.Members.Select(m => m.MemberId).Should().BeEquivalentTo([me, child]);
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment, "ordering alone never makes anybody a paid member");
        (await app.Db(c => c.MemberSeasons.CountAsync(ms => ms.MembershipOrderId == order.Id))).Should().Be(0);

        // A second membership in the same season is turned down
        var again = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{planId}},"memberIds":[{{me}}]}""");
        again.Status.Should().Be(HttpStatusCode.PreconditionFailed);
        again.Body.Should().Contain("AlreadyHasMembership");

        await RemoveOrders(me, child);
    }

    [Fact]
    public async Task Member_CannotGetEnrolledWithoutPaying()
    {
        var planId = await app.Db(async context => (await NewPlan(context, "Paid plan")).Id);
        var me = app.MemberOfA.MemberId;
        var created = await Send(HttpMethod.Post, Orders, app.MemberOfA, $$"""{"membershipPlanId":{{planId}},"memberIds":[{{me}}]}""");
        var orderId = (await app.Db(c => c.MembershipOrders.IgnoreQueryFilters().SingleAsync(o => o.MembershipPlanId == planId))).Id;
        created.Status.Should().Be(HttpStatusCode.OK);

        // Confirming is for free plans, marking as paid is for the treasurer, and coming back from the payment page proves nothing
        var confirm = await Send(HttpMethod.Post, $"{Orders}/{orderId}/confirm", app.MemberOfA);
        var markPaid = await Send(HttpMethod.Post, $"{Payments}/{orderId}/mark-paid", app.MemberOfA, """{"method":"Cash","paidOn":"2026-01-01"}""");
        var approve = await Send(HttpMethod.Post, $"{Payments}/{orderId}/approve", app.MemberOfA);
        var record = await Send(HttpMethod.Post, Payments, app.MemberOfA, $$"""{"membershipPlanId":{{planId}},"memberIds":[{{me}}],"method":"Cash","paidOn":"2026-01-01"}""");
        var success = await Send(HttpMethod.Get, $"{Orders}/{orderId}?checkout=success", app.MemberOfA);

        confirm.Status.Should().Be(HttpStatusCode.PreconditionFailed);
        confirm.Body.Should().Contain("MembershipOrderNotFree");
        markPaid.Status.Should().Be(HttpStatusCode.Forbidden);
        approve.Status.Should().Be(HttpStatusCode.Forbidden);
        record.Status.Should().Be(HttpStatusCode.Forbidden);
        success.Status.Should().Be(HttpStatusCode.OK);

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        (await app.Db(c => c.MemberSeasons.CountAsync(ms => ms.MemberId == me && ms.SeasonId == app.ClubA.SeasonId))).Should().Be(0);
        await RemoveOrders(me);
    }

    // ---------- what only the treasurer does ----------

    [Fact]
    public async Task Member_HasNoAccessToThePaymentsOfTheClub()
    {
        var orderId = await NewOrderOfOtherMember();
        var season = app.ClubA.SeasonId;

        var attempts = new Dictionary<string, ApiResponse>
        {
            ["list"] = await Send(HttpMethod.Get, $"{Payments}?seasonId={season}", app.MemberOfA),
            ["summary"] = await Send(HttpMethod.Get, $"{Payments}/summary?seasonId={season}", app.MemberOfA),
            ["export"] = await Send(HttpMethod.Get, $"{Payments}/export?seasonId={season}", app.MemberOfA),
            ["approve"] = await Send(HttpMethod.Post, $"{Payments}/{orderId}/approve", app.MemberOfA),
            ["reject"] = await Send(HttpMethod.Post, $"{Payments}/{orderId}/reject", app.MemberOfA),
            ["cancel"] = await Send(HttpMethod.Post, $"{Payments}/{orderId}/cancel", app.MemberOfA),
            ["mark as paid"] = await Send(HttpMethod.Post, $"{Payments}/{orderId}/mark-paid", app.MemberOfA, """{"method":"Cash"}"""),
            ["payment configuration"] = await Send(HttpMethod.Get, $"{Club}/Payments/configuration", app.MemberOfA),
            ["payment account"] = await Send(HttpMethod.Get, $"{Club}/Payments/account", app.MemberOfA),
            ["create a plan"] = await Send(HttpMethod.Post, $"{Club}/MembershipPlans?seasonId={season}", app.MemberOfA, """{"name":"Free for me","priceCents":0}"""),
            ["change a plan"] = await Send(HttpMethod.Put, $"{Club}/MembershipPlans/{app.ClubA.PlanId}", app.MemberOfA, """{"name":"Free for me","priceCents":0}"""),
            ["delete a plan"] = await Send(HttpMethod.Delete, $"{Club}/MembershipPlans/{app.ClubA.PlanId}", app.MemberOfA),
            ["sort the plans"] = await Send(HttpMethod.Put, $"{Club}/MembershipPlans/sort", app.MemberOfA, $$"""{"seasonId":{{season}},"planIds":[]}"""),
            ["copy the plans"] = await Send(HttpMethod.Post, $"{Club}/MembershipPlans/copy", app.MemberOfA, $$"""{"sourceSeasonId":{{season}},"targetSeasonId":{{season}}}"""),
        };

        using (new AssertionScope())
        {
            foreach (var (what, response) in attempts)
            {
                response.Status.Should().Be(HttpStatusCode.Forbidden, what);
                response.Body.Should().NotContain(ApiSecurityFixture.Marker, what);
            }
        }

        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        (await app.Db(c => c.MembershipPlans.IgnoreQueryFilters().CountAsync(p => p.Name == "Free for me"))).Should().Be(0);
        (await app.Db(c => c.MembershipPlans.IgnoreQueryFilters().SingleAsync(p => p.Id == app.ClubA.PlanId))).PriceCents.Should().Be(12000);
        await RemoveOrders(app.OtherMemberOfA.MemberId);
    }

    [Fact]
    public async Task Member_SeesThePlansTheyCanChoose_ButNotTheOnesOnlyTheTreasurerAssignsOrHowOftenAPlanWasOrdered()
    {
        await app.Db(async context => await NewPlan(context, "Honoraryhidden", selfService: false));
        var url = $"{Club}/MembershipPlans?seasonId={app.ClubA.SeasonId}";

        var asMember = await Send(HttpMethod.Get, url, app.MemberOfA);
        var asTreasurer = await Send(HttpMethod.Get, url, app.TreasurerOfA);

        asMember.Status.Should().Be(HttpStatusCode.OK);
        asMember.Body.Should().Contain($"Plan {ApiSecurityFixture.Marker}").And.NotContain("Honoraryhidden").And.NotContain("\"orderCount\":1");
        asTreasurer.Body.Should().Contain("Honoraryhidden").And.Contain("\"orderCount\":1");
    }

    [Fact]
    public async Task Treasurer_GetsThePaymentsList_WithNamesPlanAndPaymentAndNothingElseAboutAPerson()
    {
        var response = await Send(HttpMethod.Get, $"{Payments}?seasonId={app.ClubA.SeasonId}", app.TreasurerOfA);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain($"First{ApiSecurityFixture.Marker}").And.Contain($"Plan {ApiSecurityFixture.Marker}").And.Contain($"Note {ApiSecurityFixture.Marker}");
        response.Body.Should().Contain("Ownchild", "children are members too");

        // Treasurers have no access to the members list. Nothing of it may come through the payments list.
        response.Body.Should().NotContain("@security.test").And.NotContain("email", "no email addresses");
        response.Body.Should().NotContain("1990-01-01").And.NotContain("2016-05-17").And.NotContain("birth", "no birthdays");
        response.Body.Should().NotContain($"Street {ApiSecurityFixture.Marker}").And.NotContain($"City {ApiSecurityFixture.Marker}");

        (await Send(HttpMethod.Get, $"{Club}/Members", app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.Forbidden, "the members list stays closed to treasurers");
    }

    [Fact]
    public async Task Treasurer_ApprovesAnOrder_ButCannotPutAMemberOfAnotherClubOnIt()
    {
        var other = app.OtherMemberOfA.MemberId;
        var orderId = await app.Db(async context =>
        {
            var plan = new MembershipPlan(app.ClubA.Id, app.ClubA.SeasonId, new MembershipPlan.PlanData(
                $"Needs approval {Guid.NewGuid():N}", null, 12000, 2, true, null, null, false, false, false, true, null, null, true) with { RequiresApproval = true }, 99);
            context.Add(plan);
            await context.SaveChangesAsync();

            var order = new MembershipOrder(plan, other, [other]);
            context.Add(order);
            await context.SaveChangesAsync();
            return order.Id;
        });

        var foreign = await Send(HttpMethod.Post, $"{Payments}/{orderId}/approve", app.TreasurerOfA, $$"""{"memberIds":[{{other}},{{app.ClubB.Member.MemberId}}]}""");

        foreign.Status.Should().Be(HttpStatusCode.PreconditionFailed, foreign.Body);
        foreign.Body.Should().Contain("MembershipMemberNotInClub");
        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingApproval);
        (await app.Db(c => c.MembershipOrderMembers.IgnoreQueryFilters().Where(m => m.MembershipOrderId == orderId).Select(m => m.MemberId).ToListAsync())).Should().Equal(other);

        // Without a body the order is confirmed as the member placed it, with one the family is put on it
        var child = app.FamilyOfOtherMemberOfA.ChildMemberId;
        var approved = await Send(HttpMethod.Post, $"{Payments}/{orderId}/approve", app.TreasurerOfA, $$"""{"memberIds":[{{other}},{{child}}]}""");

        approved.Status.Should().Be(HttpStatusCode.OK, approved.Body);
        (await Order(orderId)).Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        (await app.Db(c => c.MembershipOrderMembers.IgnoreQueryFilters().Where(m => m.MembershipOrderId == orderId).Select(m => m.MemberId).ToListAsync())).Should().BeEquivalentTo([other, child]);
        await RemoveOrders(other);
    }

    [Fact]
    public async Task Treasurer_CannotRecordAPaymentForMembersOrPlansOfAnotherClub()
    {
        var before = await app.Fingerprint(app.ClubB);
        var a = app.ClubA;
        var b = app.ClubB;

        var attempts = new Dictionary<string, ApiResponse>
        {
            ["a member of club B"] = await Send(HttpMethod.Post, Payments, app.TreasurerOfA, $$"""{"membershipPlanId":{{a.PlanId}},"memberIds":[{{b.Member.MemberId}}],"method":"Cash"}"""),
            ["a payer of club B"] = await Send(HttpMethod.Post, Payments, app.TreasurerOfA, $$"""{"membershipPlanId":{{a.PlanId}},"memberIds":[{{app.OtherMemberOfA.MemberId}}],"payerMemberId":{{b.Member.MemberId}},"method":"Cash"}"""),
            ["a plan of club B"] = await Send(HttpMethod.Post, Payments, app.TreasurerOfA, $$"""{"membershipPlanId":{{b.PlanId}},"memberIds":[{{app.OtherMemberOfA.MemberId}}],"method":"Cash"}"""),
            ["an order of club B"] = await Send(HttpMethod.Post, $"{Payments}/{b.OrderId}/cancel", app.TreasurerOfA),
            ["the payments of club B"] = await Send(HttpMethod.Get, $"/api/Clubs/{b.Id}/MembershipPayments?seasonId={b.SeasonId}", app.TreasurerOfA),
            ["the payments of club A with a season of club B"] = await Send(HttpMethod.Get, $"{Payments}?seasonId={b.SeasonId}", app.TreasurerOfA),
        };

        using (new AssertionScope())
        {
            foreach (var (what, response) in attempts)
            {
                ((int)response.Status).Should().BeOneOf([403, 404, 412], what);
                response.Body.Should().NotContain(ApiSecurityFixture.Canary, what);
            }
        }

        (await app.Fingerprint(app.ClubB)).Should().Be(before);
        (await app.Db(c => c.MembershipOrders.IgnoreQueryFilters().CountAsync(o => o.ClubId == a.Id && o.Members.Any(m => m.MemberId == app.OtherMemberOfA.MemberId)))).Should().Be(0);
    }

    // ---------- the Stripe account of the club ----------

    [Fact]
    public async Task StripeAccount_IsOnlyConnectedOrDisconnectedByASignedInAdmin()
    {
        await app.Db(async context =>
        {
            context.Add(new ClubPaymentAccount(app.ClubA.Id, "acct_security_test", DateTimeOffset.UtcNow));
            await context.SaveChangesAsync();
        });

        var account = $"{Club}/Payments/account";
        var key = await app.NewKey(app.ClubA);
        var attackers = new (string Who, SeededUser? User, SeededKey? Key, HttpStatusCode Expected)[]
        {
            ("the treasurer", app.TreasurerOfA, null, HttpStatusCode.Forbidden),
            ("a member", app.MemberOfA, null, HttpStatusCode.Forbidden),
            ("the admin of another club", app.ClubB.Admin, null, HttpStatusCode.Forbidden),
            ("an API key of an admin", null, key, HttpStatusCode.Unauthorized),
            ("nobody", null, null, HttpStatusCode.Unauthorized),
        };

        using (new AssertionScope())
        {
            foreach (var (who, user, apiKey, expected) in attackers)
            {
                (await Send(HttpMethod.Post, $"{account}/connect", user, """{"country":"Austria"}""", apiKey)).Status.Should().Be(expected, $"{who} connects");
                (await Send(HttpMethod.Post, $"{account}/refresh", user, "{}", apiKey)).Status.Should().Be(expected, $"{who} refreshes");
                (await Send(HttpMethod.Delete, account, user, key: apiKey)).Status.Should().Be(expected, $"{who} disconnects");
            }
        }

        (await app.Db(c => c.ClubPaymentAccounts.IgnoreQueryFilters().CountAsync(x => x.ClubId == app.ClubA.Id && x.StripeAccountId == "acct_security_test"))).Should().Be(1);

        // The treasurer and a key may look at the status, which never includes the id of the Stripe account
        foreach (var status in new[] { await Send(HttpMethod.Get, account, app.TreasurerOfA), await Send(HttpMethod.Get, account, null, key: key) })
        {
            status.Status.Should().Be(HttpStatusCode.OK);
            status.Body.Should().Contain("\"isConnected\":true").And.NotContain("acct_security_test");
        }

        // The admin disconnects
        (await Send(HttpMethod.Delete, account, app.ClubA.Admin)).Status.Should().Be(HttpStatusCode.OK);
        (await app.Db(c => c.ClubPaymentAccounts.IgnoreQueryFilters().CountAsync(x => x.ClubId == app.ClubA.Id))).Should().Be(0);
    }

    // ---------- the webhook ----------

    [Fact]
    public async Task Webhook_RejectsRequestsThatStripeDidNotSign_AndStoresNothing()
    {
        var payload = EventPayload("evt_forged", "checkout.session.completed", "acct_security_test", "cs_forged", "checkout.session");
        var before = await app.Db(c => c.StripeWebhookEvents.CountAsync());

        var attempts = new Dictionary<string, ApiResponse>
        {
            ["no signature"] = await SendWebhook(payload, null),
            ["an empty signature"] = await SendWebhook(payload, ""),
            ["garbage"] = await SendWebhook(payload, "t=1,v1=deadbeef"),
            ["signed with another secret"] = await SendWebhook(payload, Sign(payload, "whsec_guessed")),
            ["a signature of another payload"] = await SendWebhook(payload, Sign(payload + " ", ApiSecurityFixture.StripeWebhookSecret)),
            ["an old signature"] = await SendWebhook(payload, Sign(payload, ApiSecurityFixture.StripeWebhookSecret, DateTimeOffset.UtcNow.AddHours(-2))),
            ["no json"] = await SendWebhook("not json", Sign("not json", ApiSecurityFixture.StripeWebhookSecret)),
        };

        using (new AssertionScope())
        {
            foreach (var (what, response) in attempts)
                response.Status.Should().Be(HttpStatusCode.BadRequest, what);
        }

        (await app.Db(c => c.StripeWebhookEvents.CountAsync())).Should().Be(before);
    }

    [Fact]
    public async Task Webhook_AcceptsAnEventSignedByStripe_Once()
    {
        // An event of a Stripe account no club is connected with: accepted and stored, and nothing happens
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = EventPayload(eventId, "account.updated", "acct_of_no_club", "acct_of_no_club", "account");
        var signature = Sign(payload, ApiSecurityFixture.StripeWebhookSecret);

        var first = await SendWebhook(payload, signature);
        var second = await SendWebhook(payload, signature);

        first.Status.Should().Be(HttpStatusCode.OK, first.Body);
        second.Status.Should().Be(HttpStatusCode.OK);
        (await app.Db(c => c.StripeWebhookEvents.CountAsync(e => e.EventId == eventId))).Should().Be(1);
    }

    [Fact]
    public async Task Webhook_EventOfAStrangersStripeAccount_CannotMarkAnOrderAsPaid()
    {
        // Somebody with an own Stripe account points a signed-looking event at an order of club A. Without the secret it is rejected,
        // and even a correctly signed event of an account that is not the club's changes nothing.
        var orderId = await NewOrderOfOtherMember();
        var payload = EventPayload($"evt_{Guid.NewGuid():N}", "checkout.session.completed", "acct_of_the_attacker", "cs_attacker", "checkout.session", clientReferenceId: orderId);

        var unsigned = await SendWebhook(payload, Sign(payload, "whsec_of_the_attacker"));
        var signed = await SendWebhook(payload, Sign(payload, ApiSecurityFixture.StripeWebhookSecret));

        unsigned.Status.Should().Be(HttpStatusCode.BadRequest);
        signed.Status.Should().Be(HttpStatusCode.OK);

        // The event is processed in the background; give it a moment and make sure the order was not touched
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        var order = await Order(orderId);
        order.Status.Should().Be(MembershipOrderStatus.AwaitingPayment);
        (await app.Db(c => c.MemberSeasons.CountAsync(ms => ms.MembershipOrderId == orderId))).Should().Be(0);
        await RemoveOrders(app.OtherMemberOfA.MemberId);
    }

    private Task<ApiResponse> SendWebhook(string payload, string? signature)
        => app.Send(HttpMethod.Post, "/api/Stripe/Webhook", body: payload, configure: request =>
        {
            if (signature is not null)
                request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        });

    private static string EventPayload(string eventId, string type, string account, string objectId, string objectType, int? clientReferenceId = null)
    {
        var reference = clientReferenceId is null ? "null" : $"\"{clientReferenceId.Value.ToString(CultureInfo.InvariantCulture)}\"";
        return $$"""
            {
              "id": "{{eventId}}", "object": "event", "api_version": "2025-01-27.acacia", "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
              "type": "{{type}}", "account": "{{account}}", "livemode": false, "pending_webhooks": 1,
              "data": { "object": { "id": "{{objectId}}", "object": "{{objectType}}", "client_reference_id": {{reference}}, "payment_status": "paid" } }
            }
            """;
    }

    /// <summary>The signature header as Stripe builds it: an HMAC-SHA256 over "timestamp.payload" with the signing secret of the endpoint.</summary>
    private static string Sign(string payload, string secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }
}
