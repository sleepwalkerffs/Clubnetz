using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.Members;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Attacks the club API keys through the running app (see <see cref="ApiSecurityFixture"/>). The sweeps go over every endpoint the app
/// has, so they also cover endpoints that are added later. What a key must never do:
/// leave its club, outlive its creator's admin role, write when it is read-only, or show or change the data of another club.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class ClubApiKeySecurityTests(ApiSecurityFixture app)
{
    /// <summary>
    /// The controllers an API key can reach, on the routes of its own club. A new entry here is a decision: everything the controller
    /// offers becomes available to scripts. <c>ClubApiKeys</c> must never be in this list.
    /// </summary>
    private static readonly string[] ControllersOpenToKeys =
    [
        "BadgeTiers", "Bookings", "ClubAnnouncements", "ClubEmailTemplates", "ClubEvents", "ClubProfile", "Clubs", "CourtBlockings", "Courts",
        "Families", "Guests", "Leaderboards", "MemberBadges", "Members", "OneTimeBadges", "PlayModes", "Players", "Seasons", "Statistics",
        "SubscriptionPlans",
    ];

    /// <summary>Ids that don't exist, for calling write endpoints with a working key without changing the seeded data.</summary>
    private static readonly SeededClub NoResources = new()
    {
        Admin = new SeededUser(987_654_321, 987_654_321, ""),
        Member = new SeededUser(987_654_321, 987_654_321, ""),
        CourtId = 987_654_321,
        SeasonId = 987_654_321,
        BookingId = 987_654_321,
        ClubEventId = 987_654_321,
        ClubAnnouncementId = 987_654_321,
        CourtBlockingId = 987_654_321,
    };

    private IReadOnlyList<ApiEndpoint> Endpoints => ApiEndpoint.All(app.Services);
    private IReadOnlyList<ApiEndpoint> ClubEndpoints => Endpoints.Where(e => e.IsClubRoute).ToList();
    private IReadOnlyList<ApiEndpoint> EndpointsOpenToKeys => ClubEndpoints.Where(e => ControllersOpenToKeys.Contains(e.Controller) && !e.AllowsAnonymous).ToList();

    private string CourtsOf(SeededClub club) => $"/api/Clubs/{club.Id}/Courts";

    // ---------- where a key works ----------

    [Fact]
    public async Task ValidKey_IsOnlyAcceptedOnTheRoutesOfItsClub()
    {
        // The creator is admin of club A and administrator of the installation: the most powerful account there is
        var keys = new KeyRing(app, app.ClubA);
        var acceptedBy = new HashSet<string>();
        Endpoints.Count.Should().BeGreaterThan(150, "the sweep has to see the whole app");

        using var scope = new AssertionScope();
        foreach (var endpoint in Endpoints)
        {
            var url = endpoint.Url(app.ClubA.Id, endpoint.IsRead ? app.ClubA : NoResources);
            var response = await app.Send(endpoint, url, await keys.Next());

            if (!endpoint.IsClubRoute)
            {
                response.RanAsKey.Should().BeFalse($"{endpoint} is not a route of the club, a key must be ignored there");

                var anonymous = await app.Send(endpoint, url, key: null);
                if (response.Status != HttpStatusCode.TooManyRequests && anonymous.Status != HttpStatusCode.TooManyRequests)
                    response.Status.Should().Be(anonymous.Status, $"a key must not change anything on {endpoint}");
                continue;
            }

            if (!response.RanAsKey)
                continue;

            acceptedBy.Add(endpoint.Controller);
            response.UserId.Should().Be(app.ClubA.Admin.UserId.ToString(CultureInfo.InvariantCulture), $"{endpoint} must run as the creator of the key");
            response.Roles.Should().Be("User", $"a key never gets the role of an administrator ({endpoint})");
        }

        acceptedBy.Should().BeEquivalentTo(ControllersOpenToKeys, "this is the reviewed list of what a key can reach");
    }

    [Fact]
    public async Task ValidKey_IsAcceptedOnEveryEndpointOfTheOpenControllers()
    {
        var keys = new KeyRing(app, app.ClubA);

        using var scope = new AssertionScope();
        foreach (var endpoint in EndpointsOpenToKeys)
        {
            var response = await app.Send(endpoint, endpoint.Url(app.ClubA.Id, endpoint.IsRead ? app.ClubA : NoResources), await keys.Next());
            response.RanAsKey.Should().BeTrue($"{endpoint} belongs to a controller that is open to keys");
            response.Status.Should().NotBe(HttpStatusCode.Unauthorized, endpoint.ToString());
        }
    }

    [Fact]
    public async Task KeyManagement_CannotBeReachedWithAKey()
    {
        var key = await app.NewKey(app.ClubA);
        var other = await app.NewKey(app.ClubA);
        var url = $"/api/Clubs/{app.ClubA.Id}/ClubApiKeys";

        var list = await app.Send(HttpMethod.Get, url, key.Bearer);
        var create = await app.Send(HttpMethod.Post, url, key.Bearer, """{"name":"Made by a key","isReadOnly":false}""");
        var delete = await app.Send(HttpMethod.Delete, $"{url}/{other.Id}", key.Bearer);

        foreach (var response in new[] { list, create, delete })
        {
            response.Status.Should().Be(HttpStatusCode.Unauthorized);
            response.RanAsKey.Should().BeFalse();
        }

        (await app.Db(c => c.ClubApiKeys.CountAsync(k => k.Name == "Made by a key"))).Should().Be(0);
        (await app.Db(c => c.ClubApiKeys.AnyAsync(k => k.Id == other.Id))).Should().BeTrue();
    }

    // ---------- keys that must be rejected ----------

    public enum RejectedKey
    {
        Unknown,
        HashInsteadOfKey,
        Expired,
        Revoked,
        OfAnotherClub,

        /// <summary>A key of club B whose creator is admin of club A as well: the key is bound to its club, not to what its creator may do.</summary>
        OfAnotherClubWhereItsCreatorIsAdminToo,
        CreatorIsNoLongerAdmin,
        CreatorLeftTheClub,
    }

    [Theory]
    [InlineData(RejectedKey.Unknown)]
    [InlineData(RejectedKey.HashInsteadOfKey)]
    [InlineData(RejectedKey.Expired)]
    [InlineData(RejectedKey.Revoked)]
    [InlineData(RejectedKey.OfAnotherClub)]
    [InlineData(RejectedKey.OfAnotherClubWhereItsCreatorIsAdminToo)]
    [InlineData(RejectedKey.CreatorIsNoLongerAdmin)]
    [InlineData(RejectedKey.CreatorLeftTheClub)]
    public async Task RejectedKey_IsRejectedOnEveryEndpoint(RejectedKey kind)
    {
        var key = await CreateRejectedKey(kind);

        using var scope = new AssertionScope();
        foreach (var endpoint in ClubEndpoints)
        {
            // Real ids on purpose: if the key were accepted, the request would read or destroy data
            var response = await app.Send(endpoint, endpoint.Url(app.ClubA.Id, app.ClubA), key);

            response.RanAsKey.Should().BeFalse($"{kind}: {endpoint}");
            if (!endpoint.AllowsAnonymous)
                response.Status.Should().Be(HttpStatusCode.Unauthorized, $"{kind}: {endpoint}");
        }
    }

    [Fact]
    public async Task Key_StopsWorkingTheMomentItsCreatorLosesTheAdminRole_AndWorksAgainWhenItIsBack()
    {
        var admin = await app.NewAdmin(app.ClubA);
        var key = await app.NewKey(app.ClubA, admin.UserId);
        (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer)).Status.Should().Be(HttpStatusCode.OK);

        await SetRoles(admin, [MemberRole.User, MemberRole.Maintainer, MemberRole.Treasurer, MemberRole.SportsDirector]);
        var demoted = await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer);
        demoted.Status.Should().Be(HttpStatusCode.Unauthorized, "there is no cache that keeps a key alive");
        demoted.RanAsKey.Should().BeFalse();

        await SetRoles(admin, [MemberRole.Admin]);
        (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer)).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectedKeys_AllGetTheSameAnswer()
    {
        // Otherwise the answer would tell an attacker whether a key exists, is expired or belongs to another club
        var answers = new List<(RejectedKey Kind, ApiResponse Response)>();
        foreach (var kind in Enum.GetValues<RejectedKey>())
            answers.Add((kind, await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), (await CreateRejectedKey(kind)).Bearer)));

        var noKey = await app.Send(HttpMethod.Get, CourtsOf(app.ClubA));

        using var scope = new AssertionScope();
        foreach (var (kind, response) in answers)
        {
            response.Status.Should().Be(noKey.Status, kind.ToString());
            response.Body.Should().Be(noKey.Body, kind.ToString());
            response.Headers.Contains("WWW-Authenticate").Should().Be(noKey.Headers.Contains("WWW-Authenticate"), kind.ToString());
        }
    }

    // ---------- how a key is sent ----------

    [Theory]
    [InlineData("Bearer {key}", true)]
    [InlineData("bearer {key}", true)]
    [InlineData("Bearer   {key}  ", true)]
    [InlineData("{key}", false)]
    [InlineData("Token {key}", false)]
    [InlineData("ApiKey {key}", false)]
    [InlineData("Bearer{key}", false)]
    [InlineData("Bearer {KEY}", false)]
    [InlineData("Bearer {key}x", false)]
    [InlineData("Bearer {key-1}", false)]
    [InlineData("Bearer x{key}", false)]
    [InlineData("Bearer {key} {key}", false)]
    [InlineData("Bearer {key},Bearer {key}", false)]
    [InlineData("Bearer cnz_", false)]
    [InlineData("Bearer ", false)]
    [InlineData("Bearer {key}%00", false)]
    [InlineData("Bearer {key}\t", true)]
    public async Task Key_IsOnlyReadFromACleanBearerHeader(string header, bool accepted)
    {
        var key = await app.NewKey(app.ClubA);
        var value = header
            .Replace("{key-1}", key.Token[..^1], StringComparison.Ordinal)
            .Replace("{KEY}", key.Token.ToUpperInvariant(), StringComparison.Ordinal)
            .Replace("{key}", key.Token, StringComparison.Ordinal);

        var response = await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), value);

        response.RanAsKey.Should().Be(accepted);
        response.Status.Should().Be(accepted ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Key_IsNotAcceptedAnywhereButTheAuthorizationHeader()
    {
        var key = await app.NewKey(app.ClubA);
        var url = CourtsOf(app.ClubA);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"key:{key.Token}"));

        var attempts = new Dictionary<string, Task<ApiResponse>>
        {
            ["query access_token"] = app.Send(HttpMethod.Get, $"{url}?access_token={key.Token}"),
            ["query api_key"] = app.Send(HttpMethod.Get, $"{url}?api_key={key.Token}&apiKey={key.Token}&key={key.Token}&token={key.Token}"),
            ["X-Api-Key"] = app.Send(HttpMethod.Get, url, configure: r => r.Headers.TryAddWithoutValidation("X-Api-Key", key.Token)),
            ["Proxy-Authorization"] = app.Send(HttpMethod.Get, url, configure: r => r.Headers.TryAddWithoutValidation("Proxy-Authorization", key.Bearer)),
            ["cookie"] = app.Send(HttpMethod.Get, url, configure: r => r.Headers.TryAddWithoutValidation("Cookie", $"Authorization={key.Token}; ClubApiKey={key.Token}; .AspNetCore.Identity.Application={key.Token}")),
            ["basic"] = app.Send(HttpMethod.Get, url, $"Basic {basic}"),
            ["body"] = app.Send(HttpMethod.Post, url, body: $$"""{"authorization":"{{key.Bearer}}","apiKey":"{{key.Token}}"}"""),
        };

        using var scope = new AssertionScope();
        foreach (var (name, attempt) in attempts)
        {
            var response = await attempt;
            response.RanAsKey.Should().BeFalse(name);
            response.Status.Should().NotBe(HttpStatusCode.OK, name);
        }
    }

    // ---------- the club in the address ----------

    public static TheoryData<string> PathsThatTryToReachAnotherClub() => new()
    {
        "/api/Clubs/{B}/Courts",
        "/api/clubs/{B}/courts",
        "/API/CLUBS/{B}/COURTS",
        "/api/Clubs/{B}",
        "/api/Clubs/{B}/",
        "/api/Clubs/0{B}/Courts",
        "/api/Clubs/000000000{B}/Courts",
        "/api/Clubs/+{B}/Courts",
        "/api/Clubs/{B}.0/Courts",
        "/api/Clubs/{B}e0/Courts",
        "/api/Clubs/ {B}/Courts",
        "/api/Clubs/{B} /Courts",
        "/api/Clubs/{B}%20/Courts",
        "/api/Clubs/{B-unicode}/Courts",
        "/api/Clubs/{A}/../{B}/Courts",
        "/api/Clubs/{A}/..%2f{B}/Courts",
        "/api/Clubs/{A}%2f..%2f{B}/Courts",
        "/api/Clubs/{A}/Courts/../../{B}/Courts",
        "/api/Clubs/{A}/Courts/..%2f..%2f{B}%2fCourts",
        "/api/Clubs/{A}/%2e%2e/{B}/Courts",
        "/api/Clubs/{A}\\..\\{B}/Courts",
        "/api/Clubs/{A}/api/Clubs/{B}/Courts",
        "/api/Clubs/{A},{B}/Courts",
        "/api/Clubs/{A};clubId={B}/Courts",
        "/api/Clubs/{A}/Courts;clubId={B}",
        "/api/Clubs/{A}{B}/Courts",
        "/api/Clubs//{B}/Courts",
        "//api/Clubs/{B}/Courts",
        "/x/../api/Clubs/{B}/Courts",
        "/api/Clubs/{A}/Courts?clubId={B}",
        "/api/Clubs/{A}/Courts?ClubId={B}&clubId={B}&tenantId={B}",
        "/api/Clubs/{A}/Members/{B-member}",
        "/api/Clubs/{A}/Members/{B-member}/overview",
        "/api/Clubs/{A}/Bookings/{B-booking}",
        "/api/Clubs/{A}/ClubEvents/{B-event}",
        "/api/Clubs/{A}/ClubAnnouncements/{B-news}",
        "/api/Clubs/{A}/Members/{both-member}/BookingHistory?seasonId={A-season}",
        "/api/Clubs/{A}/Members/{both-member}/overview",
        "/api/Clubs/{A}/Members/{B-member}/BookingHistory?seasonId={A-season}",
        "/api/Clubs/{A}/Members/{B-member}/BookingHistory?seasonId={B-season}",
    };

    [Theory]
    [MemberData(nameof(PathsThatTryToReachAnotherClub))]
    public async Task KeyOfClubA_NeverGetsDataOfClubB_WhateverTheAddressLooksLike(string template)
    {
        var key = await app.NewKey(app.ClubA);
        var target = Fill(template);

        // The path is handed to the app as it is, without the clean-up a client or the web server would do
        var response = await SendRaw(target, key, _ => { });
        response.Body.Should().NotContain(ApiSecurityFixture.Canary, target);

        // The same with the headers some proxies use to rewrite the address
        var clubB = $"/api/Clubs/{app.ClubB.Id}/Courts";
        var rewritten = await SendRaw(target, key, request =>
        {
            request.Headers["X-Original-URL"] = clubB;
            request.Headers["X-Rewrite-URL"] = clubB;
            request.Headers["X-Forwarded-Prefix"] = $"/api/Clubs/{app.ClubB.Id}";
            request.Headers["X-Forwarded-Path"] = clubB;
            request.Headers["X-HTTP-Method-Override"] = "GET";
        });
        rewritten.Body.Should().NotContain(ApiSecurityFixture.Canary, target);
    }

    [Fact]
    public async Task TheCanary_IsWhatTheOwnerOfClubBSees()
    {
        // Makes sure the leak detection of the tests above and below can fire at all
        var key = await app.NewKey(app.ClubB);

        foreach (var path in new[] { "Courts", "Members", "ClubEvents", "ClubAnnouncements", "CourtBlockings/manage", "Bookings" })
        {
            var endpoint = ClubEndpoints.Single(e => e.Method == "GET" && e.Template.EndsWith("/" + path, StringComparison.Ordinal) && e.Controller == path.Split('/')[0]);
            var response = await app.Send(endpoint, endpoint.Url(app.ClubB.Id, app.ClubB), key);
            response.Status.Should().Be(HttpStatusCode.OK, path);
            response.Body.Should().Contain(ApiSecurityFixture.Canary, path);
        }
    }

    // ---------- the data of another club ----------

    [Fact]
    public async Task KeyOfClubA_NeverReadsDataOfClubB_OnAnyEndpoint()
    {
        var keys = new KeyRing(app, app.ClubA);
        var keysOfAdminInBothClubs = new KeyRing(app, app.ClubA, userId: app.AdminOfAWhoPlaysInB.UserId);
        var answered = 0;

        // Every read endpoint, always in the address of club A:
        var attempts = new (string What, KeyRing Keys, SeededClub Ids)[]
        {
            ("the ids of club A", keys, app.ClubA),
            ("the ids of club B", keys, app.ClubB),
            ("the ids of club B and a season of club A", keys, Copy(app.ClubB, seasonId: app.ClubA.SeasonId)),
            ("the ids of club A and a season of club B", keys, Copy(app.ClubA, seasonId: app.ClubB.SeasonId)),
            ("a key of an admin who is also a member of club B", keysOfAdminInBothClubs, Copy(app.ClubA, member: app.AdminOfAWhoPlaysInB)),
        };

        using var scope = new AssertionScope();
        foreach (var endpoint in ClubEndpoints.Where(e => e.IsRead))
        {
            foreach (var (what, ring, ids) in attempts)
            {
                var response = await app.Send(endpoint, endpoint.Url(app.ClubA.Id, ids), await ring.Next());
                response.Body.Should().NotContain(ApiSecurityFixture.Canary, $"{endpoint} with {what}");
                if (response.Status == HttpStatusCode.OK)
                    answered++;
            }
        }

        answered.Should().BeGreaterThan(60, "the sweep must reach real data, otherwise it proves nothing");
    }

    [Fact]
    public async Task KeyOfClubA_NeverChangesDataOfClubB_OnAnyEndpoint()
    {
        var keys = new KeyRing(app, app.ClubA);
        var before = await app.Fingerprint(app.ClubB);

        // Every id a request body could carry points at club B
        var b = app.ClubB;
        var body = $$"""
            {
              "id": {{b.BookingId}}, "clubId": {{b.Id}}, "courtId": {{b.CourtId}}, "courtIds": [{{b.CourtId}}], "playModeId": {{b.PlayModeId}},
              "seasonId": {{b.SeasonId}}, "sourceSeasonId": {{b.SeasonId}}, "targetSeasonId": {{b.SeasonId}}, "allowedSeasonIds": [{{b.SeasonId}}],
              "memberId": {{b.Member.MemberId}}, "memberIds": [{{b.Member.MemberId}}, {{b.Admin.MemberId}}], "players": [{{b.Member.MemberId}}, {{b.Admin.MemberId}}],
              "parentContractUserIds": [{{b.Admin.UserId}}], "childrenUserContractIds": [{{b.Member.UserId}}], "newChildUsers": [],
              "roles": ["Admin"], "title": "Attack", "name": "Attack", "body": "Attack", "description": "Attack", "subject": "Attack",
              "firstName": "Attack", "lastName": "Attack", "email": "attack@security.test", "purchasedBookings": 1,
              "category": "Social", "startDate": "2031-01-01", "endDate": "2031-01-02", "timeZoneInfoId": "UTC",
              "from": "2031-01-01T10:00:00Z", "to": "2031-01-01T11:00:00Z", "scope": "ThisAndFollowing", "audience": "AllMembers",
              "deleteConflictingBookings": true, "notifyMembers": false, "registrationEnabled": false, "isPinned": false, "headCount": 1, "answers": []
            }
            """;

        foreach (var endpoint in ClubEndpoints.Where(e => !e.IsRead))
        {
            var url = endpoint.Url(app.ClubA.Id, app.ClubB);
            await app.Send(new HttpMethod(endpoint.Method), url, (await keys.Next()).Bearer, endpoint.HasBody ? "{}" : null);
            await app.Send(new HttpMethod(endpoint.Method), url, (await keys.Next()).Bearer, body);
        }

        (await app.Fingerprint(app.ClubB)).Should().Be(before, "a key of club A must not be able to change anything of club B");
        await AssertNothingOfClubAPointsAtClubB();
    }

    [Fact]
    public async Task KeyOfClubA_CannotBuildOnResourcesOfClubB()
    {
        var key = await app.NewKey(app.ClubA);
        var a = app.ClubA;
        var b = app.ClubB;
        var before = await app.Fingerprint(b);

        string Booking(int courtId, int playModeId, int player, int hour) => $$"""
            {"courtId":{{courtId}},"from":"2031-03-0{{hour % 9 + 1}}T10:00:00Z","to":"2031-03-0{{hour % 9 + 1}}T11:00:00Z","timeZoneInfoId":"UTC","playModeId":{{playModeId}},"players":[{{a.Admin.MemberId}},{{player}}]}
            """;

        var bookings = $"/api/Clubs/{a.Id}/Bookings/BookCourt";
        await app.Send(HttpMethod.Post, bookings, key.Bearer, Booking(b.CourtId, a.PlayModeId, a.Member.MemberId, 1));
        await app.Send(HttpMethod.Post, bookings, key.Bearer, Booking(a.CourtId, b.PlayModeId, a.Member.MemberId, 2));
        await app.Send(HttpMethod.Post, bookings, key.Bearer, Booking(a.CourtId, a.PlayModeId, b.Member.MemberId, 3));
        await app.Send(HttpMethod.Post, bookings, key.Bearer, Booking(b.CourtId, b.PlayModeId, b.Member.MemberId, 4));

        await app.Send(HttpMethod.Post, $"/api/Clubs/{a.Id}/CourtBlockings", key.Bearer, $$"""
            {"title":"Attack","courtIds":[{{b.CourtId}}],"startDate":"2031-04-01","endDate":"2031-04-01","timeZoneInfoId":"UTC","deleteConflictingBookings":true}
            """);
        await app.Send(HttpMethod.Put, $"/api/Clubs/{a.Id}/CourtBlockings/{a.CourtBlockingId}", key.Bearer, $$"""
            {"title":"Attack","courtIds":[{{a.CourtId}},{{b.CourtId}}],"startDate":"2031-04-02","endDate":"2031-04-02","timeZoneInfoId":"UTC","deleteConflictingBookings":true}
            """);
        await app.Send(HttpMethod.Patch, $"/api/Clubs/{a.Id}/Members/{b.Member.MemberId}/Roles", key.Bearer, """{"roles":["User","Admin"]}""");
        await app.Send(HttpMethod.Patch, $"/api/Clubs/{a.Id}/Members/{a.Member.MemberId}/BookingOptions", key.Bearer, $$"""{"allowedSeasonIds":[{{b.SeasonId}}],"bookingsPerWeek":5}""");

        (await app.Fingerprint(b)).Should().Be(before);
        await AssertNothingOfClubAPointsAtClubB();
    }

    // ---------- read-only keys ----------

    [Fact]
    public async Task ReadOnlyKey_CannotWriteOnAnyEndpoint()
    {
        var keys = new KeyRing(app, app.ClubA, isReadOnly: true);
        var before = await app.Fingerprint(app.ClubA);
        var writes = EndpointsOpenToKeys.Where(e => !e.IsRead).ToList();
        writes.Count.Should().BeGreaterThan(50);

        using (new AssertionScope())
        {
            foreach (var endpoint in writes)
            {
                // Real ids: if the request got through, it would change or delete the seeded data
                var response = await app.Send(endpoint, endpoint.Url(app.ClubA.Id, app.ClubA), await keys.Next());
                response.Status.Should().Be(HttpStatusCode.Forbidden, endpoint.ToString());
                response.Body.Should().Contain(ClubApiKeyReadOnlyFilter.ErrorCode, endpoint.ToString());
            }
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before, "a read-only key must not change anything");
    }

    [Theory]
    [InlineData("X-HTTP-Method-Override")]
    [InlineData("X-HTTP-Method")]
    [InlineData("X-Method-Override")]
    public async Task ReadOnlyKey_CannotTurnAReadIntoAWrite(string header)
    {
        var key = await app.NewKey(app.ClubA, isReadOnly: true);
        var url = $"/api/Clubs/{app.ClubA.Id}/ClubEvents/{app.ClubA.ClubEventId}";

        var response = await app.Send(HttpMethod.Get, $"{url}?_method=DELETE", key.Bearer, configure: r => r.Headers.TryAddWithoutValidation(header, "DELETE"));

        response.Status.Should().Be(HttpStatusCode.OK);
        (await app.Db(c => c.ClubEvents.AnyAsync(e => e.Id == app.ClubA.ClubEventId))).Should().BeTrue();
    }

    [Fact]
    public async Task ReadOnlyKey_CanRead()
    {
        var key = await app.NewKey(app.ClubA, isReadOnly: true);

        var response = await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain(ApiSecurityFixture.Marker);
    }

    // ---------- signed-in users of another club ----------

    [Fact]
    public async Task SignedInUserOfAnotherClub_IsForbiddenOnEveryEndpointOfTheClub()
    {
        // The admin of club B is nobody in club A: a clean 403 everywhere, never data and never a server error
        var cookie = await app.Login(app.ClubB.Admin);
        var before = await app.Fingerprint(app.ClubA);

        using (new AssertionScope())
        {
            foreach (var endpoint in ClubEndpoints.Where(e => !e.AllowsAnonymous))
            {
                var response = await app.Send(new HttpMethod(endpoint.Method), endpoint.Url(app.ClubA.Id, app.ClubA), body: endpoint.HasBody ? "{}" : null,
                    configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));

                // An upload sent as JSON is turned down for its format before anybody is asked who it comes from
                ((int)response.Status).Should().BeOneOf([403, 415], endpoint.ToString());
                response.Body.Should().NotContain(ApiSecurityFixture.Marker, endpoint.ToString());
            }
        }

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }

    // ---------- limits ----------

    [Fact]
    public async Task Key_IsLimitedPerMinute_WithoutAffectingAnybodyElse()
    {
        var key = await app.NewKey(app.ClubA);
        var otherKey = await app.NewKey(app.ClubA);
        var cookie = await app.Login(app.ClubA.Admin);

        for (var i = 0; i < RateLimitingConfiguration.ClubApiKeyRequestsPerMinute; i++)
            (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer)).Status.Should().Be(HttpStatusCode.OK, $"request {i + 1} is within the limit");

        var limited = await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer);
        limited.Status.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").Should().BeTrue();

        // Writing counts as well, and the limit can't be dodged with another spelling of the address
        (await app.Send(HttpMethod.Post, $"/api/Clubs/{app.ClubA.Id}/ClubEvents/preview", key.Bearer, "{}")).Status.Should().Be(HttpStatusCode.TooManyRequests);
        (await app.Send(HttpMethod.Get, $"/API/clubs/{app.ClubA.Id}/courts?x=1", key.Bearer)).Status.Should().Be(HttpStatusCode.TooManyRequests);

        (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), otherKey.Bearer)).Status.Should().Be(HttpStatusCode.OK, "every key has its own limit");
        (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), configure: r => r.Headers.TryAddWithoutValidation("Cookie", cookie))).Status
            .Should().Be(HttpStatusCode.OK, "signed-in users are not limited by a key");
    }

    // ---------- managing keys ----------

    [Fact]
    public async Task KeyManagement_OnlyWorksForSignedInAdminsOfTheClub_AndNeverStoresTheKey()
    {
        var adminCookie = await app.Login(app.ClubA.Admin);
        var memberCookie = await app.Login(app.MemberOfA);
        var otherClubCookie = await app.Login(app.ClubB.Admin);
        var url = $"/api/Clubs/{app.ClubA.Id}/ClubApiKeys";

        // The other tests store their keys directly; the app itself allows only a limited number per club
        await app.Db(c => c.ClubApiKeys.IgnoreQueryFilters().Where(k => k.ClubId == app.ClubA.Id).ExecuteDeleteAsync());

        Task<ApiResponse> WithCookie(HttpMethod method, string target, string cookie, string? body = null)
            => app.Send(method, target, body: body, configure: r => r.Headers.TryAddWithoutValidation("Cookie", cookie));

        // The admin creates a key and gets it exactly once
        var created = await WithCookie(HttpMethod.Post, url, adminCookie, """{"name":"Created in the test","isReadOnly":true,"expiresInDays":30}""");
        created.Status.Should().Be(HttpStatusCode.OK, created.Body);
        using var json = JsonDocument.Parse(created.Body);
        var token = json.RootElement.GetProperty("token").GetString()!;
        var id = json.RootElement.GetProperty("id").GetInt32();
        token.Should().StartWith(ClubApiKey.TokenPrefix);

        (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), $"Bearer {token}")).Status.Should().Be(HttpStatusCode.OK);

        var list = await WithCookie(HttpMethod.Get, url, adminCookie);
        list.Status.Should().Be(HttpStatusCode.OK);
        list.Body.Should().Contain("Created in the test").And.NotContain(token).And.NotContain(token[10..]);

        // Neither the key nor its random part is anywhere in its row
        var secret = token[ClubApiKey.TokenPrefix.Length..];
        (await app.Scalar<long>("""SELECT count(*) FROM "ClubApiKeys" t WHERE t::text LIKE '%' || @secret || '%'""", new NpgsqlParameter("secret", secret[8..])))
            .Should().Be(0, "only the hash of a key is stored");

        // A member without the admin role and an admin of another club get nowhere
        using (new AssertionScope())
        {
            foreach (var (who, cookie) in new[] { ("member", memberCookie), ("admin of another club", otherClubCookie) })
            {
                (await WithCookie(HttpMethod.Get, url, cookie)).Status.Should().Be(HttpStatusCode.Forbidden, who);
                (await WithCookie(HttpMethod.Post, url, cookie, """{"name":"Not allowed"}""")).Status.Should().Be(HttpStatusCode.Forbidden, who);
                (await WithCookie(HttpMethod.Delete, $"{url}/{id}", cookie)).Status.Should().Be(HttpStatusCode.Forbidden, who);
            }

            // The admin of club B can't revoke club A's key through the address of the own club either
            ((int)(await WithCookie(HttpMethod.Delete, $"/api/Clubs/{app.ClubB.Id}/ClubApiKeys/{id}", otherClubCookie)).Status).Should().Be(404);
        }

        (await app.Db(c => c.ClubApiKeys.CountAsync(k => k.Name == "Not allowed"))).Should().Be(0);
        (await app.Db(c => c.ClubApiKeys.AnyAsync(k => k.Id == id))).Should().BeTrue();

        // Revoking works for the admin and kills the key at once
        (await WithCookie(HttpMethod.Delete, $"{url}/{id}", adminCookie)).Status.Should().Be(HttpStatusCode.OK);
        (await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), $"Bearer {token}")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- what is published ----------

    [Fact]
    public async Task OpenApiDocument_DescribesOnlyWhatAKeyCanReach_AndNothingElseIsPublished()
    {
        var document = await app.Send(HttpMethod.Get, "/api/openapi/club.json");
        document.Status.Should().Be(HttpStatusCode.OK);
        document.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");

        using var json = JsonDocument.Parse(document.Body);
        var paths = json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.Should().NotBeEmpty();
        paths.Should().OnlyContain(path => path.StartsWith("/api/Clubs/{clubId}", StringComparison.Ordinal));
        paths.Should().NotContain(path => path.Contains("ClubApiKeys", StringComparison.OrdinalIgnoreCase) || path.Contains("Admin", StringComparison.OrdinalIgnoreCase));

        // The document lists exactly the endpoints the sweeps found open, no more and no less
        var documented = json.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(method => $"{method.Name.ToUpperInvariant()} {path.Name}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var open = EndpointsOpenToKeys
            .Select(e => $"{e.Method} /{string.Join('/', e.Pattern.PathSegments.Select(s => string.Concat(s.Parts.Select(p => p switch
            {
                Microsoft.AspNetCore.Routing.Patterns.RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
                Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal => literal.Content,
                Microsoft.AspNetCore.Routing.Patterns.RoutePatternSeparatorPart separator => separator.Content,
                _ => "",
            }))))}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        documented.Should().BeEquivalentTo(open);

        // Development-only: the document with all endpoints and the Swagger UI
        foreach (var hidden in new[] { "/api/openapi/v1.json", "/swagger", "/swagger/index.html", "/swagger/v1/swagger.json" })
            (await app.Send(HttpMethod.Get, hidden)).Status.Should().NotBe(HttpStatusCode.OK, hidden);
    }

    [Fact]
    public async Task Api_CannotBeCalledFromAnotherWebsite()
    {
        var key = await app.NewKey(app.ClubA);
        const string Origin = "https://evil.example";

        var request = await app.Send(HttpMethod.Get, CourtsOf(app.ClubA), key.Bearer, configure: r => r.Headers.TryAddWithoutValidation("Origin", Origin));
        var preflight = await app.Send(HttpMethod.Options, CourtsOf(app.ClubA), configure: r =>
        {
            r.Headers.TryAddWithoutValidation("Origin", Origin);
            r.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");
            r.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization");
        });

        foreach (var response in new[] { request, preflight })
        {
            response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
            response.Headers.Contains("Access-Control-Allow-Headers").Should().BeFalse();
            response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("/hangfire")]
    [InlineData("/hangfire/jobs/enqueued")]
    public async Task Key_DoesNotOpenTheJobDashboard(string url)
    {
        // The creator of the key is an administrator of the installation, who may open the dashboard when signed in
        var key = await app.NewKey(app.ClubA);

        var response = await app.Send(HttpMethod.Get, url, key.Bearer);

        response.RanAsKey.Should().BeFalse();
        ((int)response.Status).Should().BeOneOf(401, 403, 404);
    }

    // ---------- helpers ----------

    private static SeededClub Copy(SeededClub club, int? seasonId = null, SeededUser? member = null) => new()
    {
        Id = club.Id,
        PlayModeId = club.PlayModeId,
        Admin = club.Admin,
        Member = member ?? club.Member,
        CourtId = club.CourtId,
        SeasonId = seasonId ?? club.SeasonId,
        BookingId = club.BookingId,
        ClubEventId = club.ClubEventId,
        ClubAnnouncementId = club.ClubAnnouncementId,
        CourtBlockingId = club.CourtBlockingId,
    };

    private string Fill(string template) => template
        .Replace("{A}", app.ClubA.Id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B-unicode}", string.Concat(app.ClubB.Id.ToString(CultureInfo.InvariantCulture).Select(digit => (char)('٠' + (digit - '0')))), StringComparison.Ordinal)
        .Replace("{both-member}", app.AdminOfAWhoPlaysInB.MemberId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{A-season}", app.ClubA.SeasonId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B-season}", app.ClubB.SeasonId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B-member}", app.ClubB.Member.MemberId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B-booking}", app.ClubB.BookingId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B-event}", app.ClubB.ClubEventId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B-news}", app.ClubB.ClubAnnouncementId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{B}", app.ClubB.Id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Sends a request whose path reaches the app exactly as written (no normalization by a client).</summary>
    private async Task<(int Status, string Body)> SendRaw(string target, SeededKey key, Action<HttpRequest> configure)
    {
        var queryStart = target.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? target : target[..queryStart];
        var query = queryStart < 0 ? "" : target[queryStart..];

        var context = await app.Server.SendAsync(http =>
        {
            http.Request.Method = "GET";
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("localhost");
            http.Request.Path = new PathString(path);
            http.Request.QueryString = new QueryString(query);
            http.Request.Headers.Authorization = key.Bearer;
            configure(http.Request);
        });

        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private async Task<SeededKey> CreateRejectedKey(RejectedKey kind)
    {
        switch (kind)
        {
            case RejectedKey.Unknown:
                return (await app.NewKey(app.ClubA)) with { Token = ClubApiKey.TokenPrefix + new string('A', 43) };

            case RejectedKey.HashInsteadOfKey:
                var real = await app.NewKey(app.ClubA);
                return real with { Token = ClubApiKey.TokenPrefix + ClubApiKey.Hash(real.Token) };

            case RejectedKey.Expired:
                return await app.NewKey(app.ClubA, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));

            case RejectedKey.Revoked:
                var revoked = await app.NewKey(app.ClubA);
                await app.Db(c => c.ClubApiKeys.Where(k => k.Id == revoked.Id).ExecuteDeleteAsync());
                return revoked;

            case RejectedKey.OfAnotherClub:
                return await app.NewKey(app.ClubB);

            case RejectedKey.OfAnotherClubWhereItsCreatorIsAdminToo:
                var adminOfBoth = await app.NewAdmin(app.ClubA);
                await app.Db(async c =>
                {
                    c.Add(new ClubMember(adminOfBoth.UserId, app.ClubB.Id, [MemberRole.User, MemberRole.Admin]));
                    await c.SaveChangesAsync();
                });
                var keyOfB = await app.NewKey(app.ClubB, adminOfBoth.UserId);
                (await app.Send(HttpMethod.Get, CourtsOf(app.ClubB), keyOfB.Bearer)).Status.Should().Be(HttpStatusCode.OK, "the key works in its own club");
                return keyOfB;

            case RejectedKey.CreatorIsNoLongerAdmin:
                var demoted = await app.NewAdmin(app.ClubA);
                var demotedKey = await app.NewKey(app.ClubA, demoted.UserId);
                await SetRoles(demoted, [MemberRole.User, MemberRole.Maintainer]);
                return demotedKey;

            case RejectedKey.CreatorLeftTheClub:
                var gone = await app.NewAdmin(app.ClubA);
                var goneKey = await app.NewKey(app.ClubA, gone.UserId);
                await app.Db(c => c.ClubMembers.IgnoreQueryFilters().Where(m => m.Id == gone.MemberId).ExecuteDeleteAsync());
                return goneKey;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private Task SetRoles(SeededUser user, MemberRole[] roles) => app.Db(async context =>
    {
        var member = await context.ClubMembers.IgnoreQueryFilters().SingleAsync(m => m.Id == user.MemberId);
        member.UpdateRoles(roles);
        await context.SaveChangesAsync();
    });

    /// <summary>Nothing that belongs to club A may reference a court, play mode, season or member of club B.</summary>
    private async Task AssertNothingOfClubAPointsAtClubB()
    {
        var a = app.ClubA.Id;
        var b = app.ClubB;
        var membersOfB = await app.Db(c => c.Set<Member>().IgnoreQueryFilters().Where(m => m.ClubId == b.Id).Select(m => m.Id).ToListAsync());

        using var scope = new AssertionScope();
        (await app.Db(c => c.Bookings.IgnoreQueryFilters().CountAsync(x => x.ClubId == a && (x.CourtId == b.CourtId || x.PlayModeId == b.PlayModeId))))
            .Should().Be(0, "a booking of club A must not use a court or play mode of club B");
        (await app.Db(c => c.BookingPlayers.IgnoreQueryFilters().CountAsync(x => x.Booking.ClubId == a && membersOfB.Contains(x.MemberId))))
            .Should().Be(0, "a booking of club A must not have players of club B");
        (await app.Db(c => c.CourtBlockingCourts.IgnoreQueryFilters().CountAsync(x => x.Blocking.ClubId == a && x.CourtId == b.CourtId)))
            .Should().Be(0, "a blocking of club A must not block a court of club B");
        (await app.Db(c => c.MemberSeasons.IgnoreQueryFilters().CountAsync(x => x.SeasonId == b.SeasonId && !membersOfB.Contains(x.MemberId))))
            .Should().Be(0, "a member of club A must not be enrolled in a season of club B");
    }

    /// <summary>Hands out working keys and replaces them before one runs into the request limit, so the sweeps test the endpoints and not the limiter.</summary>
    private sealed class KeyRing(ApiSecurityFixture app, SeededClub club, bool isReadOnly = false, int? userId = null)
    {
        private const int RequestsPerKey = 100;
        private SeededKey? current;
        private int used;

        public async Task<SeededKey> Next()
        {
            if (current is null || used >= RequestsPerKey)
            {
                current = await app.NewKey(club, userId, isReadOnly);
                used = 0;
            }

            used++;
            return current;
        }
    }
}
