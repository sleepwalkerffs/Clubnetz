using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Members who are not active (not enrolled in the active season, no enrolled child, no staff role) see the booking calendar anonymized
/// and nothing about other members: no leaderboard, statistics, season review, profiles, trophy cases or player lists.
/// <see cref="ApiSecurityFixture.MemberOfA"/> is such a member; <see cref="ApiSecurityFixture.EnrolledMemberOfA"/> is active and
/// has a booking with a comment and a name (<see cref="ApiSecurityFixture.ActiveMemberName"/>) that must never reach the other.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class ActiveMemberSecurityTests(ApiSecurityFixture app)
{
    private string Club => $"/api/Clubs/{app.ClubA.Id}";

    private async Task<ApiResponse> Get(string url, SeededUser user)
    {
        var cookie = await app.Login(user);
        return await app.Send(HttpMethod.Get, url, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    [Fact]
    public async Task NotActiveMember_IsForbiddenWhatShowsOtherMembers()
    {
        var other = app.EnrolledMemberOfA.MemberId;
        var urls = new[]
        {
            $"{Club}/Leaderboards",
            $"{Club}/Statistics",
            $"{Club}/Wrapped",
            $"{Club}/Wrapped/{app.ClubA.SeasonId}",
            $"{Club}/Members/{other}/profile",
            $"{Club}/MemberBadges/Members/{other}/badges",
            $"{Club}/MemberBadges/Members/{other}/trophy-case",
            $"{Club}/OneTimeBadges?seasonId={app.ClubA.SeasonId}",
            $"{Club}/Bookings/RecurringSeries/1",
        };

        using var scope = new AssertionScope();
        foreach (var url in urls)
        {
            var response = await Get(url, app.MemberOfA);
            response.Status.Should().Be(HttpStatusCode.Forbidden, url);
            response.Body.Should().NotContain(ApiSecurityFixture.ActiveMemberName, url);
        }
    }

    [Fact]
    public async Task ActiveMember_StillReadsThem()
    {
        var other = app.MemberOfA.MemberId;

        using var scope = new AssertionScope();
        foreach (var url in new[] { $"{Club}/Leaderboards", $"{Club}/Statistics", $"{Club}/Members/{other}/profile", $"{Club}/MemberBadges/Members/{other}/trophy-case" })
            (await Get(url, app.EnrolledMemberOfA)).Status.Should().Be(HttpStatusCode.OK, url);

        var bookings = await Get($"{Club}/Bookings?dayFrom={Day(-1)}&dayTo={Day(10)}", app.EnrolledMemberOfA);
        bookings.Body.Should().Contain($"Comment {ApiSecurityFixture.ActiveMemberName}");

        // An active member sees who planned and registered for an event, and the members without a family
        (await Get($"{Club}/ClubEvents/{app.EventOfEnrolledMemberOfA}", app.TreasurerOfA)).Body.Should().Contain(ApiSecurityFixture.ActiveMemberName);
        (await Get($"{Club}/Families/AvailableFamilyMembers/{app.TreasurerOfA.MemberId}", app.TreasurerOfA)).Body.Should().Contain(ApiSecurityFixture.ActiveMemberName);
    }

    [Fact]
    public async Task NotActiveMember_ReadsTheOwnProfileTrophyCaseAndClub()
    {
        var me = app.MemberOfA.MemberId;

        using var scope = new AssertionScope();
        foreach (var url in new[] { $"{Club}/Members/{me}/profile", $"{Club}/MemberBadges/Members/{me}/trophy-case", $"{Club}/Courts", $"{Club}/Memberships/options" })
            (await Get(url, app.MemberOfA)).Status.Should().Be(HttpStatusCode.OK, url);

        var profile = await Get($"{Club}/ClubProfile", app.MemberOfA);
        profile.Body.Should().Contain("\"isActiveMember\":false");
        (await Get($"{Club}/ClubProfile", app.EnrolledMemberOfA)).Body.Should().Contain("\"isActiveMember\":true");
    }

    [Fact]
    public async Task NotActiveMember_SeesTheCalendarAnonymized()
    {
        var response = await Get($"{Club}/Bookings?dayFrom={Day(-1)}&dayTo={Day(10)}", app.MemberOfA);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().NotContain(ApiSecurityFixture.ActiveMemberName).And.NotContain("Comment ").And.NotContain(ApiSecurityFixture.Marker);

        var bookings = JsonDocument.Parse(response.Body).RootElement.GetProperty("bookings").EnumerateArray().ToList();
        bookings.Should().NotBeEmpty("the courts are shown as booked");
        bookings.Should().OnlyContain(b => b.GetProperty("isAnonymized").GetBoolean() && b.GetProperty("players").GetArrayLength() == 0 && b.GetProperty("playMode").GetProperty("id").GetInt32() == 0);

        var single = await Get($"{Club}/Bookings/{app.ClubA.BookingId}", app.MemberOfA);
        single.Status.Should().Be(HttpStatusCode.OK);
        single.Body.Should().Contain("\"isAnonymized\":true").And.NotContain(ApiSecurityFixture.Marker);
    }

    [Fact]
    public async Task NotActiveMember_PlayersAreOnlyTheOwnFamily()
    {
        var response = await Get($"{Club}/Players", app.MemberOfA);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain($"\"memberId\":{app.MemberOfA.MemberId}").And.Contain($"\"memberId\":{app.FamilyOfMemberOfA.ChildMemberId}");
        response.Body.Should().NotContain($"\"memberId\":{app.EnrolledMemberOfA.MemberId}").And.NotContain(ApiSecurityFixture.ActiveMemberName);
    }

    [Fact]
    public async Task NotActiveMember_NoAnswerOfTheClubContainsTheNameOrCommentOfAnotherMember()
    {
        var endpoints = ApiEndpoint.All(app.Services)
            .Where(e => e.IsClubRoute && e.IsRead && !e.AllowsAnonymous)
            .ToList();
        endpoints.Count.Should().BeGreaterThan(50);

        using var scope = new AssertionScope();
        foreach (var endpoint in endpoints)
        {
            var response = await Get(endpoint.Url(app.ClubA.Id, app.ClubA), app.MemberOfA);
            response.Body.Should().NotContain(ApiSecurityFixture.ActiveMemberName, endpoint.ToString());
        }

        // The members without a family (to put a family together) and the event planned and registered for by the active member
        var me = app.MemberOfA.MemberId;
        var from = Day(-1);
        var to = Day(30);
        foreach (var url in new[]
                 {
                     $"{Club}/Families/AvailableFamilyMembers/{me}",
                     $"{Club}/ClubEvents?from={from}&to={to}",
                     $"{Club}/ClubEvents/{app.EventOfEnrolledMemberOfA}",
                 })
        {
            var response = await Get(url, app.MemberOfA);
            response.Status.Should().Be(HttpStatusCode.OK, url);
            response.Body.Should().NotContain(ApiSecurityFixture.ActiveMemberName, url).And.NotContain("Comment of the active member", url);
        }
    }

    private static string Day(int days) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
