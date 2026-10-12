using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// Who gets to know what about a member. The full record (email, birthday, roles, seasons) is for the member themself and for those
/// who manage the members of the club. Everybody else, including the clubs that share their players, only gets name and picture.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class MemberPrivacySecurityTests(ApiSecurityFixture app)
{
    private const string EmailDomain = "@security.test";
    private const string Birthday = "1990-01-01";

    private Task<ApiResponse> Get(string url, string? cookie = null, SeededKey? key = null)
        => app.Send(HttpMethod.Get, url, key?.Bearer, configure: request =>
        {
            if (cookie is not null)
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
        });

    private static void ShouldBeNameOnly(ApiResponse response, string because)
    {
        response.Status.Should().Be(HttpStatusCode.OK, because);
        response.Body.Should().Contain("firstName", because).And.Contain("lastName", because);
        response.Body.Should().NotContain(EmailDomain, because).And.NotContain(Birthday, because);
        response.Body.Should().NotContainAny(["email", "birthday", "gender", "roles", "allowedSeasonIds", "familyId", "lastPlayed", "bookingsPerWeek"], because);
    }

    [Fact]
    public async Task Member_ReadsTheOwnFullRecord()
    {
        var cookie = await app.Login(app.MemberOfA);

        var response = await Get($"/api/Clubs/{app.ClubA.Id}/Members/{app.MemberOfA.MemberId}", cookie);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain(app.MemberOfA.Email).And.Contain(Birthday);
    }

    [Fact]
    public async Task Member_GetsOnlyNameAndPictureOfAnotherMemberOfTheClub()
    {
        var cookie = await app.Login(app.MemberOfA);
        var other = app.ClubA.Member.MemberId;

        var full = await Get($"/api/Clubs/{app.ClubA.Id}/Members/{other}", cookie);
        full.Status.Should().Be(HttpStatusCode.Forbidden);
        full.Body.Should().NotContain(EmailDomain).And.NotContain(Birthday);

        ShouldBeNameOnly(await Get($"/api/Clubs/{app.ClubA.Id}/Members/{other}/profile", cookie), "profile of another member");
    }

    [Fact]
    public async Task Member_CannotReadWhatIsReservedForThoseWhoManageTheMembers()
    {
        var cookie = await app.Login(app.MemberOfA);
        var club = $"/api/Clubs/{app.ClubA.Id}/Members";
        var other = app.ClubA.Member.MemberId;

        using var scope = new AssertionScope();
        foreach (var url in new[] { club, $"{club}/emails", $"{club}/summary", $"{club}/{other}/overview", $"{club}/{other}/BookingHistory?seasonId={app.ClubA.SeasonId}" })
        {
            var response = await Get(url, cookie);
            response.Status.Should().Be(HttpStatusCode.Forbidden, url);
            response.Body.Should().NotContain(EmailDomain, url);
        }
    }

    [Fact]
    public async Task AdminOfTheClub_ReadsTheFullRecordOfItsMembers_WithCookieAndWithKey()
    {
        var url = $"/api/Clubs/{app.ClubA.Id}/Members/{app.ClubA.Member.MemberId}";

        foreach (var response in new[] { await Get(url, await app.Login(app.ClubA.Admin)), await Get(url, key: await app.NewKey(app.ClubA)) })
        {
            response.Status.Should().Be(HttpStatusCode.OK);
            response.Body.Should().Contain(app.ClubA.Member.Email).And.Contain(Birthday);
        }
    }

    [Fact]
    public async Task ClubsThatShareTheirPlayers_OnlyShareNameAndPicture()
    {
        var own = await app.NewClub("SHARINGONE", sharesPlayers: true);
        var partner = await app.NewClub("SHARINGTWO", sharesPlayers: true);

        // Even the admin of a club (here with a key) does not get the full record of a partner club's member
        var key = await app.NewKey(own);
        var members = $"/api/Clubs/{own.Id}/Members";

        using var scope = new AssertionScope();
        foreach (var member in new[] { partner.Member, partner.Admin })
        {
            var full = await Get($"{members}/{member.MemberId}", key: key);
            full.Status.Should().Be(HttpStatusCode.NotFound);
            full.Body.Should().NotContain(EmailDomain).And.NotContain(Birthday);

            var profile = await Get($"{members}/{member.MemberId}/profile", key: key);
            ShouldBeNameOnly(profile, "profile of a partner club's player");
            profile.Body.Should().Contain("FirstSHARINGTWO");

            foreach (var url in new[] { $"{members}/{member.MemberId}/overview", $"{members}/{member.MemberId}/BookingHistory?seasonId={own.SeasonId}" })
            {
                var response = await Get(url, key: key);
                response.Status.Should().NotBe(HttpStatusCode.OK, url);
                response.Body.Should().NotContain(EmailDomain, url).And.NotContain("SHARINGTWO", url);
            }
        }

        // Neither the list of members nor the list of their emails contains the partner club
        foreach (var url in new[] { members, $"{members}/emails" })
        {
            var response = await Get(url, key: key);
            response.Status.Should().Be(HttpStatusCode.OK, url);
            response.Body.Should().NotContain("SHARINGTWO", url).And.NotContain("sharingtwo", url);
        }
    }

    [Fact]
    public async Task ClubsThatDoNotShareTheirPlayers_ShareNothing()
    {
        var key = await app.NewKey(app.ClubA);

        foreach (var url in new[] { $"/api/Clubs/{app.ClubA.Id}/Members/{app.ClubB.Member.MemberId}", $"/api/Clubs/{app.ClubA.Id}/Members/{app.ClubB.Member.MemberId}/profile" })
        {
            var response = await Get(url, key: key);
            response.Status.Should().Be(HttpStatusCode.NotFound, url);
            response.Body.Should().NotContain(ApiSecurityFixture.Canary, url);
        }
    }
}
