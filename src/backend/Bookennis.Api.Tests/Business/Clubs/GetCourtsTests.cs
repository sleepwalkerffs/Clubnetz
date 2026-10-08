using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Tests.TestUtils;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

public class GetCourtsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetCourts_ReturnsCourtsOrderedBySortOrder()
    {
        var result = await SendAsync(new GetCourts());

        result.Courts.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Courts.Should().BeInAscendingOrder(c => c.SortOrder);
    }

    [Fact]
    public async Task GetCourts_ContainsExpectedCourtNames()
    {
        var result = await SendAsync(new GetCourts());

        result.Courts.Should().Contain(c => c.Name == "Court 1");
        result.Courts.Should().Contain(c => c.Name == "Court 2");
    }
}
