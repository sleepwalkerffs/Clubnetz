using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// The clubhouse over HTTP: the menu and the tabs of all guests are for the host and the admins of the club, a member only reads
/// their own tabs, the host of club A can't reach club B, and nothing is reachable with an API key.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class ClubhouseSecurityTests(ApiSecurityFixture app)
{
    private static string ClubhouseOf(SeededClub club) => $"/api/Clubs/{club.Id}/Clubhouse";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user, string? body = null)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    /// <summary>Everything only the host does, with the seeded ids of the club.</summary>
    private static (HttpMethod Method, string Path, string? Body)[] HostActions(SeededClub club) =>
    [
        (HttpMethod.Get, "board", null),
        (HttpMethod.Get, "guests", null),
        (HttpMethod.Get, "items", null),
        (HttpMethod.Get, "tabs", null),
        (HttpMethod.Get, $"tabs/{club.ClubhouseTabId}", null),
        (HttpMethod.Post, "items", """{"name":"Free beer","priceCents":0}"""),
        (HttpMethod.Put, $"items/{club.ClubhouseItemId}", """{"name":"Free beer","priceCents":0}"""),
        (HttpMethod.Delete, $"items/{club.ClubhouseItemId}", null),
        (HttpMethod.Post, "tabs", $$"""{"memberId":{{club.Member.MemberId}}}"""),
        (HttpMethod.Post, $"tabs/{club.ClubhouseTabId}/items", $$"""{"items":[{"itemId":{{club.ClubhouseItemId}},"quantity":1}]}"""),
        (HttpMethod.Delete, $"tabs/{club.ClubhouseTabId}/lines/{club.ClubhouseTabLineId}", null),
        (HttpMethod.Post, $"tabs/{club.ClubhouseTabId}/pay", """{"paymentMethod":"Cash"}"""),
        (HttpMethod.Delete, $"tabs/{club.ClubhouseTabId}", null),
        (HttpMethod.Get, "sumup", null),
        (HttpMethod.Post, "sumup/connect", null),
        (HttpMethod.Delete, "sumup", null),
    ];

    /// <summary>Paying a tab in the app: only for the guest of the tab (or a parent of their family).</summary>
    private static (HttpMethod Method, string Path)[] PayActions(SeededClub club) =>
    [
        (HttpMethod.Post, $"tabs/{club.ClubhouseTabId}/online-payment"),
        (HttpMethod.Get, $"tabs/{club.ClubhousePaidTabId}/online-payment/{club.ClubhouseTabPaymentId}"),
    ];

    [Fact]
    public async Task ApiKey_NeverReachesTheClubhouse()
    {
        var key = await app.NewKey(app.ClubA);

        using var scope = new AssertionScope();
        foreach (var url in new[] { "board", "guests", "items", "tabs", $"tabs/{app.ClubA.ClubhouseTabId}", "my-tabs", "sumup", $"tabs/{app.ClubA.ClubhousePaidTabId}/online-payment/{app.ClubA.ClubhouseTabPaymentId}" })
        {
            var response = await app.Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/{url}", key.Bearer);
            response.Status.Should().Be(HttpStatusCode.Unauthorized, url);
            response.RanAsKey.Should().BeFalse(url);
        }
    }

    [Fact]
    public async Task Members_TreasurersAndTheGuestOfTheTab_CannotDoWhatTheHostDoes()
    {
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            // ClubA.Member owns the seeded tab: being the guest gives no right to change it
            foreach (var user in new[] { app.ClubA.Member, app.MemberOfA, app.EnrolledMemberOfA, app.TreasurerOfA })
            foreach (var (method, path, body) in HostActions(app.ClubA))
                (await Send(method, $"{ClubhouseOf(app.ClubA)}/{path}", user, body)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} {method} {path}");
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task HostOfAnotherClub_CannotTouchTheTabsOrTheMenu()
    {
        // The admin of club B passes the host policy there, like a host
        var attacker = app.ClubB.Admin;
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            // Through the routes of club A: no member there
            foreach (var (method, path, body) in HostActions(app.ClubA))
                (await Send(method, $"{ClubhouseOf(app.ClubA)}/{path}", attacker, body)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");

            // Through the routes of club B with ids of club A: not found (the SumUp account of club B is the attacker's own)
            foreach (var (method, path, body) in HostActions(app.ClubA).Where(a => a.Path.Contains('/') && !a.Path.StartsWith("sumup")))
                (await Send(method, $"{ClubhouseOf(app.ClubB)}/{path}", attacker, body)).Status.Should().Be(HttpStatusCode.NotFound, $"{method} {path}");

            // A tab in club B for a member of club A, and a round of club B onto a tab with an item of club A
            (await Send(HttpMethod.Post, $"{ClubhouseOf(app.ClubB)}/tabs", attacker, $$"""{"memberId":{{app.MemberOfA.MemberId}}}""")).Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Post, $"{ClubhouseOf(app.ClubB)}/tabs/{app.ClubB.ClubhouseTabId}/items", attacker, $$"""{"items":[{"itemId":{{app.ClubA.ClubhouseItemId}},"quantity":1}]}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);

            // (The guests of club B are not checked: the admin of club A who plays in B is one of them, under the name of club A)
            foreach (var path in new[] { "board", "items", "tabs" })
                (await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubB)}/{path}", attacker)).Body.Should().NotContain(ApiSecurityFixture.Marker, path);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Host_SeesTheTabsOfTheOwnClubOnly_WithNamesButNoEmails()
    {
        var board = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/board", app.HostOfA);
        var guests = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/guests", app.HostOfA);
        var tab = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/tabs/{app.ClubA.ClubhouseTabId}", app.HostOfA);
        var foreignTab = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/tabs/{app.ClubB.ClubhouseTabId}", app.HostOfA);

        using var scope = new AssertionScope();
        board.Status.Should().Be(HttpStatusCode.OK, board.Body);
        board.Body.Should().Contain($"Guest {ApiSecurityFixture.Marker}").And.NotContain(ApiSecurityFixture.Canary);
        guests.Status.Should().Be(HttpStatusCode.OK);
        guests.Body.Should().Contain($"First{ApiSecurityFixture.Marker}").And.NotContain("@security.test").And.NotContain("1990").And.NotContain(ApiSecurityFixture.Canary);
        tab.Status.Should().Be(HttpStatusCode.OK);
        tab.Body.Should().Contain($"Beer {ApiSecurityFixture.Marker}");
        foreignTab.Status.Should().Be(HttpStatusCode.NotFound);

        // The host is no staff: what shows other members stays closed
        (await Send(HttpMethod.Get, $"/api/Clubs/{app.ClubA.Id}/Leaderboards", app.HostOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Member_ReadsOnlyTheOwnTabs()
    {
        var own = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/my-tabs", app.ClubA.Member);
        var other = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/my-tabs", app.MemberOfA);
        var otherClub = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubB)}/my-tabs", app.MemberOfA);

        using var scope = new AssertionScope();
        own.Status.Should().Be(HttpStatusCode.OK);
        own.Body.Should().Contain($"Beer {ApiSecurityFixture.Marker}").And.NotContain($"Guest {ApiSecurityFixture.Marker}");
        other.Status.Should().Be(HttpStatusCode.OK);
        other.Body.Should().NotContain($"Beer {ApiSecurityFixture.Marker}");
        otherClub.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApiKey_CannotPayATabOrConnectSumUp()
    {
        var key = await app.NewKey(app.ClubA);
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            foreach (var path in new[] { $"tabs/{app.ClubA.ClubhouseTabId}/online-payment", "sumup/connect" })
            {
                var response = await app.Send(HttpMethod.Post, $"{ClubhouseOf(app.ClubA)}/{path}", key.Bearer);
                response.Status.Should().Be(HttpStatusCode.Unauthorized, path);
                response.RanAsKey.Should().BeFalse(path);
            }

            (await app.Send(HttpMethod.Delete, $"{ClubhouseOf(app.ClubA)}/sumup", key.Bearer)).Status.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task OnlyTheGuestOfTheTab_PaysIt_NotOtherMembersTheHostOrAnotherClub()
    {
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            // Other members of the club, the treasurer, the host (who takes cash and card, but never pays for a guest) and the admin
            foreach (var user in new[] { app.MemberOfA, app.EnrolledMemberOfA, app.TreasurerOfA, app.HostOfA, app.ClubA.Admin })
            foreach (var (method, path) in PayActions(app.ClubA))
                (await Send(method, $"{ClubhouseOf(app.ClubA)}/{path}", user)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} {method} {path}");

            // The guest of a tab of club B, through the routes of club A and with the ids of club A in club B
            foreach (var (method, path) in PayActions(app.ClubA))
            {
                (await Send(method, $"{ClubhouseOf(app.ClubA)}/{path}", app.ClubB.Member)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path}");
                (await Send(method, $"{ClubhouseOf(app.ClubB)}/{path}", app.ClubB.Member)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {path} in B");
            }

            // A payment of club B through the own tab of club A: not found
            (await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/tabs/{app.ClubA.ClubhousePaidTabId}/online-payment/{app.ClubB.ClubhouseTabPaymentId}", app.ClubA.Member))
                .Status.Should().Be(HttpStatusCode.NotFound);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task TheGuestOfTheTab_PassesTheChecks_AndReadsTheOwnPayment()
    {
        var payment = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/tabs/{app.ClubA.ClubhousePaidTabId}/online-payment/{app.ClubA.ClubhouseTabPaymentId}", app.ClubA.Member);

        // SumUp is not configured in the app under test: paying is refused as not available, not as forbidden
        var start = await Send(HttpMethod.Post, $"{ClubhouseOf(app.ClubA)}/tabs/{app.ClubA.ClubhouseTabId}/online-payment", app.ClubA.Member);

        using var scope = new AssertionScope();
        payment.Status.Should().Be(HttpStatusCode.OK, payment.Body);
        payment.Body.Should().Contain("Paid").And.NotContain("chk_").And.NotContain($"TX{app.ClubA.Id}");
        start.Status.Should().Be(HttpStatusCode.PreconditionFailed, start.Body);
        start.Body.Should().Contain(nameof(Bookennis.Domain.Clubhouse.ClubhouseErrorCode.ClubhouseOnlinePaymentNotAvailable));
    }

    [Fact]
    public async Task SumUpAccount_ShowsTheStatusToTheHost_NeverATokenOrAnotherClub()
    {
        var own = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubA)}/sumup", app.HostOfA);
        var foreign = await Send(HttpMethod.Get, $"{ClubhouseOf(app.ClubB)}/sumup", app.HostOfA);

        using var scope = new AssertionScope();
        own.Status.Should().Be(HttpStatusCode.OK, own.Body);
        own.Body.Should().Contain($"Bar {ApiSecurityFixture.Marker}").And.NotContain("access ").And.NotContain("refresh ").And.NotContain(ApiSecurityFixture.Canary);
        foreign.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SumUpWebhook_ForgedOrForeign_ChangesNothing()
    {
        var beforeA = await app.Fingerprint(app.ClubA);
        var beforeB = await app.Fingerprint(app.ClubB);
        string[] bodies =
        [
            """{"event_type":"CHECKOUT_STATUS_CHANGED","id":"chk_does_not_exist"}""",
            $$"""{"event_type":"CHECKOUT_STATUS_CHANGED","id":"chk_{{app.ClubA.Id}}"}""",
            $$"""{"event_type":"CHECKOUT_STATUS_CHANGED","id":"chk_{{app.ClubB.Id}}"}""",
            """{"event_type":"SOMETHING_ELSE","id":"x"}""",
            "{}",
        ];

        using (new AssertionScope())
        {
            foreach (var body in bodies)
            {
                var response = await app.Send(HttpMethod.Post, "/api/SumUp/Webhook", body: body);
                response.Status.Should().Be(HttpStatusCode.OK, body);
                response.Body.Should().NotContain(ApiSecurityFixture.Marker).And.NotContain(ApiSecurityFixture.Canary);
            }
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(beforeA);
        (await app.Fingerprint(app.ClubB)).Should().Be(beforeB);
    }

    [Fact]
    public async Task SumUpCallback_NeedsTheSignedInUser_AndAForgedStateConnectsNothing()
    {
        var before = await app.Fingerprint(app.ClubA);
        var key = await app.NewKey(app.ClubA);

        var anonymous = await app.Send(HttpMethod.Get, "/api/SumUp/Callback?code=attacker-code&state=forged");
        var forged = await Send(HttpMethod.Get, "/api/SumUp/Callback?code=attacker-code&state=forged", app.HostOfA);
        var withKey = await app.Send(HttpMethod.Get, "/api/SumUp/Callback?code=attacker-code&state=forged", key.Bearer);

        using (new AssertionScope())
        {
            anonymous.Status.Should().Be(HttpStatusCode.Unauthorized);
            forged.Status.Should().Be(HttpStatusCode.Redirect);
            forged.Headers.Location!.OriginalString.Should().Be("/");
            withKey.Status.Should().Be(HttpStatusCode.Unauthorized);
            withKey.RanAsKey.Should().BeFalse();
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }
}
