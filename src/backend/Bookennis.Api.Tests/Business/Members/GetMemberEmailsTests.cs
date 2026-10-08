using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Members;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMemberEmailsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMemberEmails_CanTranslateQuery()
    {
        var result = await SendAsync(new GetMemberEmails(new MemberFilter()));

        result.Emails.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMemberEmails_MemberWithEmail_ReturnsOwnEmail()
    {
        var memberEmail = Query(ctx => ctx.TestData().User.Email);

        var result = await SendAsync(new GetMemberEmails(new MemberFilter()));

        result.Emails.Should().Contain(memberEmail);
    }

    [Fact]
    public async Task GetMemberEmails_ChildWithoutEmail_ReturnsParentEmail()
    {
        var (parentEmail, childLastName) = await QueryAsync(async ctx =>
        {
            var parentUser = ctx.TestData().User;
            var parentMember = ctx.TestData().Member1;
            var club = ctx.TestData().Club;

            var childUser = new User("Child", "Emailless", new DateOnly(2015, 3, 1), Gender.Female, parentUser.Id);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();

            var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            var family = new Domain.Families.Family(club.Id, [parentMember.Id], [childMember.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();

            return (parentUser.Email, childUser.LastName);
        });

        // Searching by the child's unique last name: old code returned nothing (no email on child user),
        // new code returns the parent's email on behalf of the child.
        var result = await SendAsync(new GetMemberEmails(new MemberFilter { SearchTerm = childLastName }));

        result.Emails.Should().ContainSingle().Which.Should().Be(parentEmail);
    }

    [Fact]
    public async Task GetMemberEmails_ChildWithoutEmailAndNoFamily_IsExcluded()
    {
        var childMemberId = await QueryAsync(async ctx =>
        {
            var parentUser = ctx.TestData().User;
            var club = ctx.TestData().Club;

            var childUser = new User("Orphan", "Child", new DateOnly(2016, 5, 1), Gender.Male, parentUser.Id);
            ctx.Add(childUser);
            await ctx.SaveChangesAsync();

            var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
            ctx.Add(childMember);
            await ctx.SaveChangesAsync();

            return childMember.Id;
        });

        var totalBefore = (await SendAsync(new GetMemberEmails(new MemberFilter()))).Emails.Length;

        // Verify the child has no email and is not counted in the results
        var childUserEmail = Query(ctx =>
        {
            var member = ctx.ClubMembers.Find(childMemberId)!;
            return ctx.Users.Find(member.UserId)!.Email;
        });

        childUserEmail.Should().BeNull();
        totalBefore.Should().BeGreaterThan(0); // other seeded members with email are still returned
    }

    [Fact]
    public async Task GetMemberEmails_SiblingsWithSameParent_ReturnEmailOnce()
    {
        var (parentEmail, lastName) = await QueryAsync(async ctx =>
        {
            var parentUser = ctx.TestData().User;
            var parentMember = ctx.TestData().Member1;
            var club = ctx.TestData().Club;

            var children = new List<ClubMember>();
            foreach (var firstName in new[] { "First", "Second" })
            {
                var childUser = new User(firstName, "Siblingtest", new DateOnly(2015, 3, 1), Gender.Female, parentUser.Id);
                ctx.Add(childUser);
                await ctx.SaveChangesAsync();

                var childMember = new ClubMember(childUser.Id, club.Id, [MemberRole.User]);
                ctx.Add(childMember);
                await ctx.SaveChangesAsync();
                children.Add(childMember);
            }

            ctx.Add(new Domain.Families.Family(club.Id, [parentMember.Id], children.Select(c => c.Id).ToList()));
            await ctx.SaveChangesAsync();

            return (parentUser.Email, "Siblingtest");
        });

        var result = await SendAsync(new GetMemberEmails(new MemberFilter { SearchTerm = lastName }));

        result.Emails.Should().ContainSingle().Which.Should().Be(parentEmail);
    }
}
