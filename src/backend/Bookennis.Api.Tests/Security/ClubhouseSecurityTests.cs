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
    ];

    [Fact]
    public async Task ApiKey_NeverReachesTheClubhouse()
    {
        var key = await app.NewKey(app.ClubA);

        using var scope = new AssertionScope();
        foreach (var url in new[] { "board", "guests", "items", "tabs", $"tabs/{app.ClubA.ClubhouseTabId}", "my-tabs" })
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

            // Through the routes of club B with ids of club A: not found
            foreach (var (method, path, body) in HostActions(app.ClubA).Where(a => a.Path.Contains('/')))
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
}
