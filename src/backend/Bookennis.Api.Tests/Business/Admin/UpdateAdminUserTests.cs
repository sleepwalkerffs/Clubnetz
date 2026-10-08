using Bookennis.Api.Business.Admin;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Admin;

public class UpdateAdminUserTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateAdminUser_UpdatesUserDetails()
    {
        var userId = Query(ctx => ctx.TestData().User.Id);

        await SendAsync(new UpdateAdminUser(userId, "UpdatedFirst", "UpdatedLast", new DateOnly(1995, 6, 15), Gender.Diverse, "Main St 5", "Graz", "8010", Country.Austria));

        var user = await QueryAsync(ctx => ctx.Users.SingleAsync(u => u.Id == userId));
        user.FirstName.Should().Be("UpdatedFirst");
        user.LastName.Should().Be("UpdatedLast");
        user.Birthday.Should().Be(new DateOnly(1995, 6, 15));
        user.Gender.Should().Be(Gender.Diverse);
        user.Street.Should().Be("Main St 5");
        user.City.Should().Be("Graz");
        user.ZipCode.Should().Be("8010");
        user.Country.Should().Be(Country.Austria);
    }

    [Fact]
    public async Task UpdateAdminUser_NonExistentUser_ThrowsEntityNotFoundException()
    {
        var act = () => SendAsync(new UpdateAdminUser(999999, "Test", "Test", new DateOnly(2000, 1, 1), Gender.Male, "Street", "City", "12345", Country.Austria));

        await act.Should().ThrowAsync<Fusonic.Extensions.Common.Entities.EntityNotFoundException>();
    }
}
