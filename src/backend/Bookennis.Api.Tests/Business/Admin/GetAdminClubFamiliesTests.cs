using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminClubFamiliesTests(TestFixture fixture) : TestBase(fixture)
{
    private static PaginationParameters DefaultPagination => new() { Page = 1, PageSize = 10, EnablePaging = true };

    [Fact]
    public async Task GetAdminClubFamilies_NoFamilies_ReturnsEmptyResult()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubFamilies(clubId, DefaultPagination, null));

        result.Entities.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetAdminClubFamilies_WithFamily_ReturnsFamilies()
    {
        var clubId = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;

            var family = new Domain.Families.Family(club.Id, [member1.Id], [member2.Id]);
            ctx.Add(family);
            await ctx.SaveChangesAsync();
            return club.Id;
        });

        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubFamilies(clubId, DefaultPagination, null));

        result.Entities.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Entities[0].Parents.Should().HaveCount(1);
        result.Entities[0].Children.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAdminClubFamilies_WithSearchTerm_FiltersResults()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubFamilies(clubId, DefaultPagination, "nonexistentfamily"));

        result.Entities.Should().BeEmpty();
        result.Total.Should().Be(0);
    }
}
