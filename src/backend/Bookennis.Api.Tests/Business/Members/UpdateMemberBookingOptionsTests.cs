using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class UpdateMemberBookingOptionsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task UpdateMemberBookingOptions_ValidOptions_UpdatesSuccessfully()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        await SendAsync(new UpdateMemberBookingOptions(memberId, AllowedSeasonIds: [], BookingsPerWeek: 5));

        var member = await QueryAsync(ctx => ctx.ClubMembers.SingleAsync(m => m.Id == memberId));
        member.BookingsPerWeek.Should().Be(5);
    }

    [Fact]
    public async Task UpdateMemberBookingOptions_ExceedsClubLimit_ThrowsPreconditionException()
    {
        var memberId = Query(ctx => ctx.TestData().Member1.Id);

        var act = () => SendAsync(new UpdateMemberBookingOptions(memberId, AllowedSeasonIds: [], BookingsPerWeek: 999));

        await act.Should().ThrowAsync<PreconditionException>();
    }
}
