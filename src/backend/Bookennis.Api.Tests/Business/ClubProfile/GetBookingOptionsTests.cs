using Bookennis.Api.Business.ClubProfile;
using Bookennis.Api.Tests.Business.CourtSales;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Xunit;

namespace Bookennis.Api.Tests.Business.ClubProfile;

public class GetBookingOptionsTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task ClubMember_EnrolledInTheActiveSeason_IsAllowedToBookAndActive()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
            await CourtSaleSeed.Enroll(ctx, TestDataSeed.Member1Id, seasonId);
            return ctx.TestData().User.Id;
        });

        var result = await SendAsync(new GetBookingOptions(userId, TestDataSeed.ClubId));

        result.IsAllowedToBook.Should().BeTrue();
        result.IsActiveMember.Should().BeTrue();
    }

    [Fact]
    public async Task ClubMember_NotEnrolled_IsNotAllowedToBookAndNotActive()
    {
        var userId = await QueryAsync(async ctx =>
        {
            await CourtSaleSeed.AddActiveSeason(ctx);
            return ctx.TestData().User.Id;
        });

        var result = await SendAsync(new GetBookingOptions(userId, TestDataSeed.ClubId));

        result.IsAllowedToBook.Should().BeFalse();
        result.IsActiveMember.Should().BeFalse();
    }

    [Fact]
    public async Task Parent_OfAnEnrolledChild_BooksForTheChildAndIsActive()
    {
        var (userId, childMemberId) = await QueryAsync(async ctx =>
        {
            var seasonId = await CourtSaleSeed.AddActiveSeason(ctx);
            var (parentUserId, parentMemberId) = await MemberSeed.AddUserWithMember(ctx, "parent-options@test.com");
            var (_, childId) = await MemberSeed.AddChild(ctx, parentUserId, "Kid");
            await MemberSeed.AddFamily(ctx, [parentMemberId], [childId]);
            await CourtSaleSeed.Enroll(ctx, childId, seasonId);
            return (parentUserId, childId);
        });

        var result = await SendAsync(new GetBookingOptions(userId, TestDataSeed.ClubId));

        result.IsAllowedToBook.Should().BeTrue();
        result.IsBookingOnlyForChildren.Should().BeTrue();
        result.EligibleChildMemberIds.Should().Equal(childMemberId);
        result.IsActiveMember.Should().BeTrue();
    }

    [Fact]
    public async Task FormerGuest_WithOnlyAGuestMember_IsNoMember()
    {
        var userId = await QueryAsync(async ctx =>
        {
            var user = new User("guestoptions@test.com", "guestoptions@test.com", "Guest", "Options", new DateOnly(1990, 1, 1), Gender.Male);
            ctx.Add(user);
            await ctx.SaveChangesAsync();

            var guestMember = new GuestMember(user.Id, TestDataSeed.ClubId);
            guestMember.AllowBooking();
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();
            return user.Id;
        });

        var result = await SendAsync(new GetBookingOptions(userId, TestDataSeed.ClubId));

        result.IsAllowedToBook.Should().BeFalse("the guest system was removed: a guest member gives no rights");
        result.IsActiveMember.Should().BeFalse();
    }
}
