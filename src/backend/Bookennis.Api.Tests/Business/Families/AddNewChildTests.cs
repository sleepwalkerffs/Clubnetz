using Bookennis.Api.Business.Families;
using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Infrastructure.Tenant;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Families;

public class AddNewChildTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task AddNewChild_AddsChildToFamily()
    {
        var (familyId, userId, clubId) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], []);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return (family.Id, ctx.TestData().User.Id, club.Id);
        });

        var childModel = new ChildUserModel("Child", "Test", new DateOnly(2015, 1, 1), Gender.Male);

        // AddNewChild directly calls tenantService.GetTenantId()!.Value,
        // so the tenant must be set within the same scope as the handler.
        await ScopedAsync(async () =>
        {
            GetInstance<ITenantService>().SetTenantId(clubId);
            await GetInstance<IMediator>().Send(new AddNewChild(familyId, childModel, userId));
        });

        var familyMembers = await QueryAsync(ctx => ctx.FamilyMembers.Where(fm => fm.FamilyId == familyId).ToListAsync());
        familyMembers.Should().HaveCountGreaterThan(1);
    }

    [Theory]
    [InlineData(TestDataSeed.UserId)]
    [InlineData(TestDataSeed.AdminId)]
    public async Task AddNewChild_TheAccountOfTheChildBelongsToTheParent_AlsoWhenSomebodyElseAddsIt(int callerUserId)
    {
        // Member1 (the user) is the parent. When an admin or a treasurer adds the child, it must not become theirs.
        var familyId = await QueryAsync(async ctx =>
        {
            var family = new Domain.Families.Family(TestDataSeed.ClubId, [TestDataSeed.Member1Id], []);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return family.Id;
        });

        await ScopedAsync(async () =>
        {
            GetInstance<ITenantService>().SetTenantId(TestDataSeed.ClubId);
            await GetInstance<IMediator>().Send(new AddNewChild(familyId, new ChildUserModel("Child", "Test", new DateOnly(2015, 1, 1), Gender.Male), callerUserId));
        });

        var owner = await QueryAsync(ctx => ctx.Users.Where(u => u.FirstName == "Child" && u.LastName == "Test").Select(u => u.BelongsToUserId).SingleAsync());
        owner.Should().Be(TestDataSeed.UserId);
    }
}
