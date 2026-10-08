using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminClubMembersTests(TestFixture fixture) : TestBase(fixture)
{
    private static PaginationParameters DefaultPagination => new() { Page = 1, PageSize = 10, EnablePaging = true };

    [Fact]
    public async Task GetAdminClubMembers_ReturnsMembersForClub()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubMembers(clubId, DefaultPagination, null));

        result.Entities.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Total.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAdminClubMembers_NonExistentClub_ReturnsEmptyResult()
    {
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubMembers(999999, DefaultPagination, null));

        result.Entities.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetAdminClubMembers_WithSearchTerm_FiltersResults()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var result = await SendAsync(new GetAdminClubMembers(clubId, DefaultPagination, "nonexistentuser"));

        result.Entities.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetAdminClubMembers_WithPagination_ReturnsPagedResults()
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        SetTenantId(0);

        var pagination = new PaginationParameters { Page = 1, PageSize = 1, EnablePaging = true };
        var result = await SendAsync(new GetAdminClubMembers(clubId, pagination, null));

        result.Entities.Should().HaveCount(1);
        result.Total.Should().BeGreaterThanOrEqualTo(2);
    }
}
