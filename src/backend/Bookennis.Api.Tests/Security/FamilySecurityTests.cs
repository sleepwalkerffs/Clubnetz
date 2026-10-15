using System.Net;
using Bookennis.Domain.Members;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// A family belongs to its members. Another member of the club must not read it (it holds the birthdays of children), join it,
/// or change or delete it. Whoever is a parent of a family may book for its children, so joining a family is a way to gain rights.
/// </summary>
[Collection(ApiSecurityTestGroup.Name)]
public class FamilySecurityTests(ApiSecurityFixture app)
{
    private const string ChildBirthday = "2016-05-17";

    private string Families => $"/api/Clubs/{app.ClubA.Id}/Families";
    private SeededFamily Own => app.FamilyOfMemberOfA;
    private SeededFamily Foreign => app.FamilyOfOtherMemberOfA;

    private async Task<ApiResponse> Send(HttpMethod method, string url, SeededUser? user, string? body = null, SeededKey? key = null)
    {
        var cookie = user is null ? null : await app.Login(user);
        return await app.Send(method, url, key?.Bearer, body, request =>
        {
            if (cookie is not null)
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
        });
    }

    private Task<List<(int MemberId, bool IsParent)>> MembersOf(SeededFamily family) => app.Db(async context =>
    {
        var loaded = await context.Families.IgnoreQueryFilters().Include(f => f.Parents).Include(f => f.Children).SingleAsync(f => f.Id == family.Id);
        return loaded.Parents.Select(p => (p.MemberId, true)).Concat(loaded.Children.Select(c => (c.MemberId, false))).OrderBy(m => m.MemberId).ToList();
    });

    [Fact]
    public async Task Member_ReadsTheOwnFamily()
    {
        var response = await Send(HttpMethod.Get, $"{Families}/{Own.Id}", app.MemberOfA);

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body.Should().Contain("Ownchild").And.Contain(ChildBirthday);
    }

    [Fact]
    public async Task Member_CannotReadTheFamilyOfSomebodyElse()
    {
        var single = await Send(HttpMethod.Get, $"{Families}/{Foreign.Id}", app.MemberOfA);
        var all = await Send(HttpMethod.Get, Families, app.MemberOfA);

        foreach (var response in new[] { single, all })
        {
            response.Status.Should().Be(HttpStatusCode.Forbidden);
            response.Body.Should().NotContain("Otherchild").And.NotContain(ChildBirthday);
        }
    }

    [Fact]
    public async Task Member_CannotJoinChangeOrDeleteTheFamilyOfSomebodyElse()
    {
        var before = await MembersOf(Foreign);
        var me = app.MemberOfA.MemberId;
        var family = $"{Families}/{Foreign.Id}";

        var attempts = new Dictionary<string, ApiResponse>
        {
            ["add myself as parent"] = await Send(HttpMethod.Post, $"{family}/Parent", app.MemberOfA, $$"""{"memberId":{{me}}}"""),
            ["add a member as child"] = await Send(HttpMethod.Post, $"{family}/Child", app.MemberOfA, $$"""{"memberIds":[{{app.ClubA.Member.MemberId}}]}"""),
            ["add a new child"] = await Send(HttpMethod.Post, $"{family}/NewChild", app.MemberOfA, """{"firstName":"Planted","lastName":"Child","birthday":"2015-01-01","gender":"Male"}"""),
            ["rename the child"] = await Send(HttpMethod.Put, $"{family}/Child/{Foreign.ChildMemberId}", app.MemberOfA, """{"firstName":"Renamed","lastName":"Child","birthday":"2015-01-01","gender":"Male"}"""),
            ["remove the child"] = await Send(HttpMethod.Delete, $"{family}/Member/{Foreign.ChildMemberId}", app.MemberOfA),
            ["remove the parent"] = await Send(HttpMethod.Delete, $"{family}/Member/{Foreign.ParentMemberId}", app.MemberOfA),
            ["delete the family"] = await Send(HttpMethod.Delete, family, app.MemberOfA),
        };

        using (new AssertionScope())
        {
            foreach (var (what, response) in attempts)
                response.Status.Should().Be(HttpStatusCode.Forbidden, what);
        }

        (await MembersOf(Foreign)).Should().Equal(before);
        (await app.Db(c => c.Users.Where(u => u.Id == Foreign.ChildUserId).Select(u => u.FirstName).SingleAsync())).Should().Be("Otherchild");
        (await app.Db(c => c.Users.CountAsync(u => u.FirstName == "Planted"))).Should().Be(0);
    }

    [Fact]
    public async Task Treasurer_SetsUpTheFamilyOfAMember_AndTheNewChildBelongsToTheParent()
    {
        // A treasurer who records the fee of a family puts the family together
        var family = $"{Families}/{Foreign.Id}";

        var read = await Send(HttpMethod.Get, family, app.TreasurerOfA);
        var added = await Send(HttpMethod.Post, $"{family}/NewChild", app.TreasurerOfA, """{"firstName":"Bytreasurer","lastName":"Child","birthday":"2015-01-01","gender":"Male"}""");

        read.Status.Should().Be(HttpStatusCode.OK, read.Body);
        added.Status.Should().Be(HttpStatusCode.OK, added.Body);

        var child = await app.Db(c => c.Users.Where(u => u.FirstName == "Bytreasurer").Select(u => new { u.Id, u.BelongsToUserId }).SingleAsync());
        child.BelongsToUserId.Should().Be(app.OtherMemberOfA.UserId, "the account of a child belongs to a parent, not to the treasurer");

        var childMemberId = await app.Db(c => c.ClubMembers.IgnoreQueryFilters().Where(m => m.UserId == child.Id).Select(m => m.Id).SingleAsync());
        var removed = await Send(HttpMethod.Delete, $"{family}/Member/{childMemberId}", app.TreasurerOfA);
        removed.Status.Should().Be(HttpStatusCode.OK, removed.Body);
        (await MembersOf(Foreign)).Should().HaveCount(2);

        await app.Db(async c =>
        {
            await c.ClubMembers.IgnoreQueryFilters().Where(m => m.Id == childMemberId).ExecuteDeleteAsync();
            await c.Users.Where(u => u.Id == child.Id).ExecuteDeleteAsync();
        });
    }

    [Fact]
    public async Task Treasurer_FoundsAFamilyForAMember_ButNotInAnotherClub()
    {
        var parent = app.ClubA.Member.MemberId;

        var foreign = await Send(HttpMethod.Post, Families, app.TreasurerOfA, $$"""{"parentContractUserIds":[{{app.ClubB.Member.MemberId}}],"childrenUserContractIds":[],"newChildUsers":[]}""");
        var foreignRead = await Send(HttpMethod.Get, $"/api/Clubs/{app.ClubB.Id}/Families", app.TreasurerOfA);
        var founded = await Send(HttpMethod.Post, Families, app.TreasurerOfA, $$"""{"parentContractUserIds":[{{parent}}],"childrenUserContractIds":[],"newChildUsers":[]}""");

        foreign.Status.Should().NotBe(HttpStatusCode.OK, "a member of another club can't be put into a family");
        foreignRead.Status.Should().Be(HttpStatusCode.Forbidden);
        founded.Status.Should().Be(HttpStatusCode.OK, founded.Body);
        (await app.Db(c => c.FamilyMembers.IgnoreQueryFilters().CountAsync(fm => fm.MemberId == app.ClubB.Member.MemberId))).Should().Be(0);

        var familyId = int.Parse(founded.Body, System.Globalization.CultureInfo.InvariantCulture);
        (await Send(HttpMethod.Delete, $"{Families}/{familyId}", app.TreasurerOfA)).Status.Should().Be(HttpStatusCode.OK);
        (await app.Db(c => c.FamilyMembers.IgnoreQueryFilters().CountAsync(fm => fm.MemberId == parent))).Should().Be(0);
    }

    [Fact]
    public async Task Member_CannotFoundAFamilyForOtherPeople()
    {
        // Parents would be two other members: whoever is a parent of a family may book for its children
        var admin = app.ClubA.Admin.MemberId;
        var member = app.ClubA.Member.MemberId;

        var response = await Send(HttpMethod.Post, Families, app.MemberOfA, $$"""{"parentContractUserIds":[{{admin}}],"childrenUserContractIds":[{{member}}],"newChildUsers":[]}""");

        response.Status.Should().Be(HttpStatusCode.Forbidden);
        (await app.Db(c => c.FamilyMembers.IgnoreQueryFilters().CountAsync(fm => fm.MemberId == admin || fm.MemberId == member))).Should().Be(0);
    }

    [Fact]
    public async Task Parent_ChangesTheOwnFamily()
    {
        var family = $"{Families}/{Own.Id}";

        var renamed = await Send(HttpMethod.Put, $"{family}/Child/{Own.ChildMemberId}", app.MemberOfA, """{"firstName":"Ownchild","lastName":"Renamed","birthday":"2016-05-17","gender":"Male"}""");
        renamed.Status.Should().Be(HttpStatusCode.OK, renamed.Body);
        (await app.Db(c => c.Users.Where(u => u.Id == Own.ChildUserId).Select(u => u.LastName).SingleAsync())).Should().Be("Renamed");

        var added = await Send(HttpMethod.Post, $"{family}/NewChild", app.MemberOfA, """{"firstName":"Secondchild","lastName":"Own","birthday":"2018-03-03","gender":"Female"}""");
        added.Status.Should().Be(HttpStatusCode.OK, added.Body);

        var members = await MembersOf(Own);
        members.Should().HaveCount(3);
        var secondChild = members.Single(m => !m.IsParent && m.MemberId != Own.ChildMemberId).MemberId;

        var removed = await Send(HttpMethod.Delete, $"{family}/Member/{secondChild}", app.MemberOfA);
        removed.Status.Should().Be(HttpStatusCode.OK, removed.Body);
        (await MembersOf(Own)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ThoseWhoManageTheClub_ReadAllFamilies_WithCookieAndWithKey()
    {
        foreach (var response in new[]
                 {
                     await Send(HttpMethod.Get, Families, app.ClubA.Admin),
                     await Send(HttpMethod.Get, Families, user: null, key: await app.NewKey(app.ClubA)),
                     await Send(HttpMethod.Get, $"{Families}/{Foreign.Id}", app.ClubA.Admin),
                 })
        {
            response.Status.Should().Be(HttpStatusCode.OK);
            response.Body.Should().Contain("Otherchild");
        }
    }

    [Fact]
    public async Task AdminOfAnotherClub_HasNoAccessToTheFamilies()
    {
        var before = await MembersOf(Foreign);

        foreach (var (method, url) in new[]
                 {
                     (HttpMethod.Get, Families),
                     (HttpMethod.Get, $"{Families}/{Foreign.Id}"),
                     (HttpMethod.Delete, $"{Families}/{Foreign.Id}"),
                     (HttpMethod.Delete, $"{Families}/{Foreign.Id}/Member/{Foreign.ChildMemberId}"),
                     // Through the address of the own club, where this user is admin
                     (HttpMethod.Get, $"/api/Clubs/{app.ClubB.Id}/Families/{Foreign.Id}"),
                     (HttpMethod.Delete, $"/api/Clubs/{app.ClubB.Id}/Families/{Foreign.Id}"),
                 })
        {
            var response = await Send(method, url, app.ClubB.Admin);
            ((int)response.Status).Should().BeOneOf([403, 404], $"{method} {url}");
            response.Body.Should().NotContain("Otherchild");
        }

        (await MembersOf(Foreign)).Should().Equal(before);
    }

    [Fact]
    public async Task Parent_CanOnlyBookForTheOwnChildren()
    {
        var before = await app.Fingerprint(app.ClubA);
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(25).ToString("yyyy-MM-dd");
        var bookings = $"/api/Clubs/{app.ClubA.Id}/Bookings";

        string Single(int player) => $$"""
            {"courtId":{{app.ClubA.CourtId}},"from":"{{day}}T10:00:00Z","to":"{{day}}T11:00:00Z","timeZoneInfoId":"UTC","playModeId":{{app.ClubA.PlayModeId}},"players":[{{player}}]}
            """;
        string Batch(int player) => $$"""
            {"slots":[{"courtId":{{app.ClubA.CourtId}},"from":"{{day}}T12:00:00Z","to":"{{day}}T13:00:00Z"}],"timeZoneInfoId":"UTC","playModeId":{{app.ClubA.PlayModeId}},"players":[{{player}}],"comment":null,"dryRun":true}
            """;

        // Being the parent of a family gives no right to book for the child of another family or for another member
        foreach (var player in new[] { Foreign.ChildMemberId, Foreign.ParentMemberId })
        {
            foreach (var (url, body) in new[] { ($"{bookings}/BookCourt", Single(player)), ($"{bookings}/Batch", Batch(player)) })
            {
                var response = await Send(HttpMethod.Post, url, app.MemberOfA, body);
                response.Status.Should().Be(HttpStatusCode.PreconditionFailed, $"{url} for member {player}");
                response.Body.Should().Contain("UserNotAllowedToBook");
            }
        }

        // The own child passes the check (the dry run answers per slot)
        var ownChild = await Send(HttpMethod.Post, $"{bookings}/Batch", app.MemberOfA, Batch(Own.ChildMemberId));
        ownChild.Status.Should().Be(HttpStatusCode.OK);
        ownChild.Body.Should().NotContain("UserNotAllowedToBook");

        (await app.Fingerprint(app.ClubA)).Should().Be(before);
    }
}
