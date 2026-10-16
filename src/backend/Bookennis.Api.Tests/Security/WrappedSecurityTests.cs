using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// The season review ("Wrapped"). Only club admins release it; members open released seasons of their own club only,
/// and see other members with name and picture, nothing more.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class WrappedSecurityTests(ApiSecurityFixture app)
{
    private string WrappedOf(SeededClub club) => $"/api/Clubs/{club.Id}/Wrapped/{club.SeasonId}";

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser user)
    {
        var cookie = await app.Login(user);
        return await app.Send(method, url, configure: request => request.Headers.TryAddWithoutValidation("Cookie", cookie));
    }

    private Task<DateTimeOffset?> PublishedAt(SeededClub club)
        => app.Db(context => context.Seasons.IgnoreQueryFilters().Where(s => s.Id == club.SeasonId).Select(s => s.WrappedPublishedAt).SingleAsync());

    [Fact]
    public async Task Member_CannotReleaseOrTakeBackTheWrapped()
    {
        foreach (var user in new[] { app.MemberOfA, app.TreasurerOfA })
        {
            (await Send(HttpMethod.Put, $"{WrappedOf(app.ClubA)}/publish", user)).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Send(HttpMethod.Delete, $"{WrappedOf(app.ClubA)}/publish", user)).Status.Should().Be(HttpStatusCode.Forbidden);
        }

        (await PublishedAt(app.ClubA)).Should().BeNull();
    }

    [Fact]
    public async Task Member_OpensTheWrappedOnlyOnceTheAdminReleasedIt()
    {
        try
        {
            // An active member (enrolled); members who are not active get no season review at all (ActiveMemberSecurityTests)
            var before = await Send(HttpMethod.Get, WrappedOf(app.ClubA), app.EnrolledMemberOfA);
            before.Status.Should().Be(HttpStatusCode.PreconditionFailed);
            (await Send(HttpMethod.Get, $"/api/Clubs/{app.ClubA.Id}/Wrapped", app.EnrolledMemberOfA)).Body.Should().NotContain($"\"seasonId\":{app.ClubA.SeasonId}");

            // The admin previews it before releasing it
            (await Send(HttpMethod.Get, WrappedOf(app.ClubA), app.ClubA.Admin)).Status.Should().Be(HttpStatusCode.OK);

            (await Send(HttpMethod.Put, $"{WrappedOf(app.ClubA)}/publish", app.ClubA.Admin)).Status.Should().Be(HttpStatusCode.OK);

            var after = await Send(HttpMethod.Get, WrappedOf(app.ClubA), app.EnrolledMemberOfA);
            after.Status.Should().Be(HttpStatusCode.OK);
            after.Body.Should().NotContainAny(["@security.test", "birthday", "email"]);
        }
        finally
        {
            (await Send(HttpMethod.Delete, $"{WrappedOf(app.ClubA)}/publish", app.ClubA.Admin)).Status.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task AdminOfAnotherClub_CannotOpenOrReleaseTheWrapped()
    {
        (await Send(HttpMethod.Get, WrappedOf(app.ClubB), app.MemberOfA)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Send(HttpMethod.Get, WrappedOf(app.ClubA), app.ClubB.Admin)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Send(HttpMethod.Put, $"{WrappedOf(app.ClubA)}/publish", app.ClubB.Admin)).Status.Should().Be(HttpStatusCode.Forbidden);

        // A season of club A through the route of club B
        (await Send(HttpMethod.Put, $"/api/Clubs/{app.ClubB.Id}/Wrapped/{app.ClubA.SeasonId}/publish", app.ClubB.Admin)).Status.Should().Be(HttpStatusCode.NotFound);
        (await PublishedAt(app.ClubA)).Should().BeNull();
    }
}
