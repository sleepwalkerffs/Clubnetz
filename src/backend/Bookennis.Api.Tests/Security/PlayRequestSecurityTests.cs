using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Play requests ("Spielersuche") over HTTP: a request is only visible to the member who made it and the members who were asked, only active
/// members take part at all, nothing is reachable with an API key, other clubs' requests are out of reach, and groups are personal.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class PlayRequestSecurityTests(ApiSecurityFixture app)
{
    private static string RequestsOf(SeededClub club) => $"/api/Clubs/{club.Id}/PlayRequests";
    private static string GroupsOf(SeededClub club) => $"/api/Clubs/{club.Id}/PlayerGroups";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user, string? body = null)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, body: body, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    /// <summary>Everything that works on one request.</summary>
    private static (HttpMethod Method, string Path)[] AnswerActions(SeededClub club, int playRequestId) =>
    [
        (HttpMethod.Post, $"{RequestsOf(club)}/{playRequestId}/accept"),
        (HttpMethod.Post, $"{RequestsOf(club)}/{playRequestId}/decline"),
        (HttpMethod.Delete, $"{RequestsOf(club)}/{playRequestId}"),
    ];

    [Fact]
    public async Task ApiKey_NeverReachesPlayRequestsOrGroups()
    {
        var key = await app.NewKey(app.ClubA);

        using var scope = new AssertionScope();
        foreach (var url in new[] { RequestsOf(app.ClubA), $"{RequestsOf(app.ClubA)}/candidates", GroupsOf(app.ClubA) })
        {
            var response = await app.Send(HttpMethod.Get, url, key.Bearer);
            response.Status.Should().Be(HttpStatusCode.Unauthorized, url);
            response.RanAsKey.Should().BeFalse(url);
        }

        foreach (var (method, url) in AnswerActions(app.ClubA, app.ClubA.PlayRequestId).Append((HttpMethod.Delete, $"{GroupsOf(app.ClubA)}/{app.ClubA.PlayerGroupId}")))
        {
            var response = await app.Send(method, url, key.Bearer);
            response.Status.Should().Be(HttpStatusCode.Unauthorized, $"{method} {url}");
            response.RanAsKey.Should().BeFalse($"{method} {url}");
        }
    }

    [Fact]
    public async Task MembersWhoAreNotActive_AreKeptOutOfEverything_EvenWhenTheyWereAsked()
    {
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            // ClubA.Member was asked by the seeded request but is not enrolled in the season
            foreach (var user in new[] { app.ClubA.Member, app.MemberOfA, app.OtherMemberOfA, app.HostOfA })
            {
                foreach (var url in new[] { RequestsOf(app.ClubA), $"{RequestsOf(app.ClubA)}/candidates", GroupsOf(app.ClubA) })
                    (await Send(HttpMethod.Get, url, user)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} GET {url}");

                foreach (var (method, url) in AnswerActions(app.ClubA, app.ClubA.PlayRequestId))
                    (await Send(method, url, user)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} {method} {url}");
            }
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task ActiveMemberWhoWasNotAsked_NeitherSeesNorAnswersNorCancelsARequest()
    {
        // The treasurer of club A is active, but is only asked by the request for active invitees, not by the one of the seeded club
        var outsider = app.TreasurerOfA;
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            var list = await Send(HttpMethod.Get, RequestsOf(app.ClubA), outsider);
            list.Status.Should().Be(HttpStatusCode.OK);
            list.Body.Should().NotContain($"Request {ApiSecurityFixture.Marker}", "the treasurer was not asked by this request");

            foreach (var (method, url) in AnswerActions(app.ClubA, app.ClubA.PlayRequestId))
                (await Send(method, url, outsider)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");

            // Somebody who was not asked can't even tell that a request exists: an unknown id answers the same
            foreach (var (method, url) in AnswerActions(app.ClubA, 987_654_321))
                (await Send(method, url, outsider)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");

            // The admin of the club is no exception: requests belong to the people in them
            var other = app.EnrolledMemberOfA;
            foreach (var (method, url) in AnswerActions(app.ClubA, app.ClubA.PlayRequestId))
                (await Send(method, url, other)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task AdminOfTheClub_ThatWasNotAsked_DoesNotSeeTheRequestsOfOthers()
    {
        // The admin of club B passes every club policy of club B, but plays no part in the request of the plain member there
        var admin = app.ClubB.Admin;
        var own = await Send(HttpMethod.Get, RequestsOf(app.ClubB), admin);
        own.Status.Should().Be(HttpStatusCode.OK);
        own.Body.Should().Contain($"Request {ApiSecurityFixture.Canary}", "the admin of club B made this request");

        // The treasurer of club A (staff, active) is no part of the requests of the admin of club A with the plain member
        var treasurer = await Send(HttpMethod.Get, RequestsOf(app.ClubA), app.TreasurerOfA);
        treasurer.Body.Should().NotContain($"Request {ApiSecurityFixture.Marker}");
        treasurer.Body.Should().Contain($"Request for active {ApiSecurityFixture.Marker}", "the treasurer was asked by this one");
    }

    [Fact]
    public async Task Invitee_SeesTheRequestButNotWhoElseWasAskedAndCannotCancelIt()
    {
        var before = await app.Fingerprint(app.ClubA);

        var list = await Send(HttpMethod.Get, RequestsOf(app.ClubA), app.EnrolledMemberOfA);
        var cancel = await Send(HttpMethod.Delete, $"{RequestsOf(app.ClubA)}/{app.PlayRequestOfActiveInviteesId}", app.EnrolledMemberOfA);

        using var scope = new AssertionScope();
        list.Status.Should().Be(HttpStatusCode.OK);
        list.Body.Should().Contain($"Request for active {ApiSecurityFixture.Marker}");
        list.Body.Should().Contain("\"invitees\":[]", "somebody who was asked does not learn who else was asked");
        list.Body.Should().NotContain("@", "names only, never email addresses");
        cancel.Status.Should().Be(HttpStatusCode.PreconditionFailed, "only the requester withdraws a request");
        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Requester_SeesTheAnswersOfTheInvitees()
    {
        var response = await Send(HttpMethod.Get, RequestsOf(app.ClubA), app.ClubA.Admin);

        using var scope = new AssertionScope();
        response.Status.Should().Be(HttpStatusCode.OK, response.Body);
        response.Body.Should().Contain($"Request for active {ApiSecurityFixture.Marker}");
        response.Body.Should().Contain("\"isMine\":true");
        response.Body.Should().NotContain(ApiSecurityFixture.Canary);
        response.Body.Should().NotContain("@", "names only, never email addresses");
    }

    [Fact]
    public async Task OtherClubs_CannotBeReachedWithIdsOfClubA()
    {
        var before = await app.Fingerprint(app.ClubA);
        var beforeB = await app.Fingerprint(app.ClubB);
        var attacker = app.ClubB.Admin;
        var invitedInB = app.ClubB.Member;

        using (new AssertionScope())
        {
            // Through the routes of club A: no member there
            foreach (var url in new[] { RequestsOf(app.ClubA), GroupsOf(app.ClubA) })
                (await Send(HttpMethod.Get, url, attacker)).Status.Should().Be(HttpStatusCode.Forbidden, url);

            foreach (var (method, url) in AnswerActions(app.ClubA, app.ClubA.PlayRequestId))
                (await Send(method, url, attacker)).Status.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");

            // Through the routes of club B with the id of a request of club A: the request is not part of club B
            foreach (var (method, url) in AnswerActions(app.ClubB, app.PlayRequestOfActiveInviteesId))
                foreach (var user in new[] { attacker, invitedInB })
                    (await Send(method, url, user)).Status.Should().Be(HttpStatusCode.Forbidden, $"{user.Email} {method} {url}");

            (await Send(HttpMethod.Put, $"{GroupsOf(app.ClubB)}/{app.ClubA.PlayerGroupId}", attacker, $$"""{"name":"Mine now","memberIds":[{{app.ClubB.Member.MemberId}}]}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Delete, $"{GroupsOf(app.ClubB)}/{app.ClubA.PlayerGroupId}", attacker)).Status.Should().Be(HttpStatusCode.NotFound);

            // Asking members of club A through club B, or booking a court of club A for a request of club B
            var start = DateTimeOffset.UtcNow.AddDays(12).Date.AddHours(15);
            var invite = $$"""{"playModeId":{{app.ClubB.PlayModeId}},"from":"{{start:O}}Z","to":"{{start.AddHours(1):O}}Z","timeZoneInfoId":"UTC","courtIds":[{{app.ClubB.CourtId}}],"inviteeMemberIds":[{{app.EnrolledMemberOfA.MemberId}}]}""";
            (await Send(HttpMethod.Post, RequestsOf(app.ClubB), attacker, invite)).Status.Should().NotBe(HttpStatusCode.OK);
            var foreignCourt = invite.Replace($"[{app.ClubB.CourtId}]", $"[{app.ClubA.CourtId}]").Replace($"[{app.EnrolledMemberOfA.MemberId}]", $"[{app.ClubB.Member.MemberId}]");
            (await Send(HttpMethod.Post, RequestsOf(app.ClubB), attacker, foreignCourt)).Status.Should().NotBe(HttpStatusCode.OK);
            (await app.Fingerprint(app.ClubB)).Should().Be(beforeB, "no request was created, neither with members nor with a court of club A");

            // Nothing of club B shows up in club A's lists
            (await Send(HttpMethod.Get, RequestsOf(app.ClubA), app.ClubA.Admin)).Body.Should().NotContain(ApiSecurityFixture.Canary);
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    [Fact]
    public async Task Groups_ArePersonal()
    {
        var before = await app.Fingerprint(app.ClubA);
        // The treasurer is active: allowed to keep groups, but not to see or change the ones of the admin
        var other = app.TreasurerOfA;

        using (new AssertionScope())
        {
            var list = await Send(HttpMethod.Get, GroupsOf(app.ClubA), other);
            list.Status.Should().Be(HttpStatusCode.OK);
            list.Body.Should().NotContain($"Group {ApiSecurityFixture.Marker}");

            (await Send(HttpMethod.Put, $"{GroupsOf(app.ClubA)}/{app.ClubA.PlayerGroupId}", other, $$"""{"name":"Mine now","memberIds":[{{app.ClubA.Member.MemberId}}]}"""))
                .Status.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Delete, $"{GroupsOf(app.ClubA)}/{app.ClubA.PlayerGroupId}", other)).Status.Should().Be(HttpStatusCode.NotFound);

            var own = await Send(HttpMethod.Get, GroupsOf(app.ClubA), app.ClubA.Admin);
            own.Body.Should().Contain($"Group {ApiSecurityFixture.Marker}");
            own.Body.Should().NotContain("@");
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }
}
