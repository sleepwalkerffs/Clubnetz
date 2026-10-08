using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class GetAdminUsersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetAdminUsers_ReturnsUsers()
    {
        var result = await SendAsync(new GetAdminUsers(new PaginationParameters { Page = 1, PageSize = 50, EnablePaging = true }, null, null));

        result.Entities.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAdminUsers_WithSearchTerm_FiltersResults()
    {
        var result = await SendAsync(new GetAdminUsers(new PaginationParameters { Page = 1, PageSize = 50, EnablePaging = true }, null, "ad min"));

        result.Entities.Should().NotBeEmpty();
        result.Entities.Should().AllSatisfy(u => u.FullName.Should().Contain("ad min"));
    }
}
