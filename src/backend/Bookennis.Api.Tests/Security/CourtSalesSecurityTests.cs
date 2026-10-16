using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bookennis.Domain.CourtSales;
using Bookennis.Global.Intervals;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Court sales over HTTP: a purchase belongs to its buyer and the treasurers of its club, prices and credits are for treasurers,
/// members of the season don't buy, nothing is reachable with an API key, and Stripe events of a strange account change nothing.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class CourtSalesSecurityTests(ApiSecurityFixture app)
{
    private string SalesOf(SeededClub club) => $"/api/Clubs/{club.Id}/CourtSales";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user, string? body = null)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    private Task<CourtPurchase> Purchase(int purchaseId) => app.Db(c => c.CourtPurchases.IgnoreQueryFilters().SingleAsync(p => p.Id == purchaseId));

    [Fact]
    public async Task ApiKey_NeverReachesCourtSales()
    {
        var key = await app.NewKey(app.ClubA);

        using var scope = new AssertionScope();
        foreach (var url in new[] { "settings", "price-rules", "options", "purchases", "credits", $"purchases/{app.ClubA.CourtPurchaseId}" })
        {
            var response = await app.Send(HttpMethod.Get, $"{SalesOf(app.ClubA)}/{url}", key.Bearer);
            response.Status.Should().Be(HttpStatusCode.Unauthorized, url);
            response.RanAsKey.Should().BeFalse(url);
        }
    }

    [Fact]
    public async Task Member_CannotReadReleaseOrCancelThePurchaseOfSomebodyElse()
    {
        var before = await app.Fingerprint(app.ClubA);
        var purchase = $"{SalesOf(app.ClubA)}/purchases/{app.ClubA.CourtPurchaseId}";

        using (new AssertionScope())
        {
            foreach (var user in new[] { app.MemberOfA, app.OtherMemberOfA, app.EnrolledMemberOfA })
            {
                (await Send(HttpMethod.Get, purchase, user)).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Post, $"{purchase}/release", user)).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Post, $"{purchase}/cancel", user)).Status.Should().Be(HttpStatusCode.Forbidden);
            }
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Buyer_ReadsTheOwnPurchase_TreasurerReadsItButOnlyTheBuyerReleasesAHold()
    {
        var url = $"{SalesOf(app.ClubA)}/purchases/{app.ClubA.CourtPurchaseId}";

        (await Send(HttpMethod.Get, url, app.ClubA.Member)).Status.Should().Be(HttpStatusCode.OK);
        (await Send(HttpMethod.Get, url, app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.OK);

        // A hold of another member: the treasurer may look at it but not release it, and no other member may
        var holdId = await NewHold(app.OtherMemberOfA);
        try
        {
            var release = $"{SalesOf(app.ClubA)}/purchases/{holdId}/release";
            (await Send(HttpMethod.Post, release, app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Send(HttpMethod.Post, release, app.MemberOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Purchase(holdId)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);

            var own = await Send(HttpMethod.Post, release, app.OtherMemberOfA);
            own.Status.Should().Be(HttpStatusCode.OK, own.Body);
            (await Purchase(holdId)).Status.Should().Be(CourtPurchaseStatus.Expired);
        }
        finally
        {
            await RemovePurchase(holdId);
        }
    }

    [Fact]
    public async Task TreasurerOfAnotherClub_CannotTouchThePurchasesPricesOrCredits()
    {
        var before = await app.Fingerprint(app.ClubA);
        var attacker = app.ClubB.Admin;

        using (new AssertionScope())
        {
            // Through the routes of club A: no member there
            foreach (var (method, path) in new[]
                     {
                         (HttpMethod.Get, $"purchases/{app.ClubA.CourtPurchaseId}"), (HttpMethod.Post, $"purchases/{app.ClubA.CourtPurchaseId}/cancel"),
                         (HttpMethod.Get, "purchases"), (HttpMethod.Get, "credits"), (HttpMethod.Get, "settings"),
                         (HttpMethod.Delete, $"price-rules/{app.ClubA.PriceRuleId}"),
                     })
                (await Send(method, $"{SalesOf(app.ClubA)}/{path}", attacker)).Status.Should().Be(HttpStatusCode.Forbidden, path);

            // Through the routes of club B with ids of club A: not found
            (await Send(HttpMethod.Post, $"{SalesOf(app.ClubB)}/purchases/{app.ClubA.CourtPurchaseId}/cancel", attacker)).Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Get, $"{SalesOf(app.ClubB)}/purchases/{app.ClubA.CourtPurchaseId}", attacker)).Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Delete, $"{SalesOf(app.ClubB)}/price-rules/{app.ClubA.PriceRuleId}", attacker)).Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Post, $"{SalesOf(app.ClubB)}/credits", attacker, $$"""{"memberId":{{app.MemberOfA.MemberId}},"count":5}""")).Status.Should().Be(HttpStatusCode.NotFound);

            var list = await Send(HttpMethod.Get, $"{SalesOf(app.ClubB)}/purchases", attacker);
            list.Body.Should().NotContain(ApiSecurityFixture.Marker);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Member_CannotChangePricesSettingsOrCredits()
    {
        var before = await app.Fingerprint(app.ClubA);
        var rule = """{"courtIds":[],"weekdays":["Monday"],"from":"08:00:00","to":"09:00:00","pricePerHourCents":0}""";

        using (new AssertionScope())
        {
            foreach (var user in new[] { app.MemberOfA, app.EnrolledMemberOfA })
            {
                (await Send(HttpMethod.Post, $"{SalesOf(app.ClubA)}/price-rules", user, rule)).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Put, $"{SalesOf(app.ClubA)}/price-rules/{app.ClubA.PriceRuleId}", user, rule)).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Delete, $"{SalesOf(app.ClubA)}/price-rules/{app.ClubA.PriceRuleId}", user)).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Put, $"{SalesOf(app.ClubA)}/settings", user, """{"isEnabled":false,"timeZoneId":"Europe/Vienna","maxBookingMinutes":60,"maxDaysInAdvance":1,"maxUpcomingBookings":1}""")).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Post, $"{SalesOf(app.ClubA)}/credits", user, $$"""{"memberId":{{user.MemberId}},"count":10}""")).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Get, $"{SalesOf(app.ClubA)}/purchases", user)).Status.Should().Be(HttpStatusCode.Forbidden);
                (await Send(HttpMethod.Get, $"{SalesOf(app.ClubA)}/credits", user)).Status.Should().Be(HttpStatusCode.Forbidden);
            }

            // Every member may read the prices
            (await Send(HttpMethod.Get, $"{SalesOf(app.ClubA)}/price-rules", app.MemberOfA)).Status.Should().Be(HttpStatusCode.OK);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Treasurer_SeesTheSales_WithNamesOnly()
    {
        var response = await Send(HttpMethod.Get, $"{SalesOf(app.ClubA)}/purchases", app.TreasurerOfA);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain($"\"id\":{app.ClubA.CourtPurchaseId}").And.Contain("firstName");
        response.Body.Should().NotContainAny(["@security.test", "1990-01-01", "birthday", "email", "pi_", "cs_"]);

        var credits = await Send(HttpMethod.Get, $"{SalesOf(app.ClubA)}/credits", app.TreasurerOfA);
        credits.Body.Should().Contain($"\"memberId\":{app.ClubA.Member.MemberId}").And.NotContain("@security.test");
    }

    [Fact]
    public async Task EnrolledMember_DoesNotBuy()
    {
        var before = await app.Fingerprint(app.ClubA);
        var from = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(9);
        var body = $$"""{"courtId":{{app.ClubA.CourtId}},"from":"{{from:yyyy-MM-ddTHH:mm:ssZ}}","to":"{{from.AddHours(1):yyyy-MM-ddTHH:mm:ssZ}}"}""";

        var response = await Send(HttpMethod.Post, $"{SalesOf(app.ClubA)}/purchases", app.EnrolledMemberOfA, body);

        response.Status.Should().Be(HttpStatusCode.PreconditionFailed);
        response.Body.Should().Contain("AlreadyEnrolled");
        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Buyer_CannotBuyACourtOfAnotherClub()
    {
        var before = await app.Fingerprint(app.ClubB);
        var from = DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(9);
        var body = $$"""{"courtId":{{app.ClubB.CourtId}},"from":"{{from:yyyy-MM-ddTHH:mm:ssZ}}","to":"{{from.AddHours(1):yyyy-MM-ddTHH:mm:ssZ}}"}""";

        var response = await Send(HttpMethod.Post, $"{SalesOf(app.ClubA)}/purchases", app.MemberOfA, body);

        response.Status.Should().Be(HttpStatusCode.NotFound);
        (await app.Fingerprint(app.ClubB)).Should().Be(before);
    }

    [Fact]
    public async Task Webhook_EventOfAStrangersStripeAccount_CannotMarkAPurchaseAsPaid()
    {
        var holdId = await NewHold(app.OtherMemberOfA);
        try
        {
            var payload = EventPayload($"evt_{Guid.NewGuid():N}", "checkout.session.completed", "acct_of_the_attacker", "cs_attacker", $"court_purchase:{holdId}");

            var signed = await app.Send(HttpMethod.Post, "/api/Stripe/Webhook", body: payload, configure: request =>
                request.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(payload, ApiSecurityFixture.StripeWebhookSecret)));
            signed.Status.Should().Be(HttpStatusCode.OK);

            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            (await Purchase(holdId)).Status.Should().Be(CourtPurchaseStatus.AwaitingPayment);
        }
        finally
        {
            await RemovePurchase(holdId);
        }
    }

    /// <summary>A purchase of the member that waits for its payment, with a booking holding a slot next week.</summary>
    private Task<int> NewHold(SeededUser member) => app.Db(async context =>
    {
        var settings = await context.CourtSaleSettings.IgnoreQueryFilters().SingleAsync(s => s.ClubId == app.ClubA.Id);
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(8).AddHours(16), TimeSpan.Zero);
        var slot = new DateTimeOffsetInterval(start, start.AddHours(1));
        var booking = new Domain.Bookings.Booking(app.ClubA.Id, app.ClubA.CourtId, settings.PlayModeId!.Value, slot, "Europe/Vienna", [member.MemberId]);
        context.Add(booking);
        await context.SaveChangesAsync();

        var purchase = new CourtPurchase(app.ClubA.Id, member.MemberId, app.ClubA.CourtId, booking.Id, slot, new CourtQuote(2000, "eur", []), false, 2000);
        purchase.StartCheckout($"cs_hold_{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddMinutes(30));
        context.Add(purchase);
        await context.SaveChangesAsync();
        return purchase.Id;
    });

    private Task RemovePurchase(int purchaseId) => app.Db(async context =>
    {
        var purchase = await context.CourtPurchases.IgnoreQueryFilters().SingleAsync(p => p.Id == purchaseId);
        if (purchase.BookingId is { } bookingId)
            await context.Bookings.IgnoreQueryFilters().Where(b => b.Id == bookingId).ExecuteDeleteAsync();
        await context.CourtPurchases.IgnoreQueryFilters().Where(p => p.Id == purchaseId).ExecuteDeleteAsync();
    });

    private static string EventPayload(string eventId, string type, string account, string objectId, string clientReferenceId) => $$"""
        {
          "id": "{{eventId}}", "object": "event", "api_version": "2025-01-27.acacia", "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "type": "{{type}}", "account": "{{account}}", "livemode": false, "pending_webhooks": 1,
          "data": { "object": { "id": "{{objectId}}", "object": "checkout.session", "client_reference_id": "{{clientReferenceId}}", "payment_status": "paid" } }
        }
        """;

    private static string Sign(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }
}
