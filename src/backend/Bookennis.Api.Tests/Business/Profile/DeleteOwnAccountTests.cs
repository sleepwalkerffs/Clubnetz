using Bookennis.Api.Business.Profile;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Profile;

public class DeleteOwnAccountTests(TestFixture fixture) : TestBase(fixture)
{
    private readonly SignInManager<User> signInManager = IdentityTestHelper.CreateSignInManager();

    [Fact]
    public async Task CorrectPassword_DeletesUserWithMemberships_AndSignsOut()
    {
        var seeded = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "me@test.com"));
        signInManager.CheckPasswordSignInAsync(Arg.Is<User>(u => u.Id == seeded.UserId), "Secret-123", true).Returns(SignInResult.Success);

        await Handle(seeded.UserId, "Secret-123");

        (await QueryAsync(ctx => ctx.Users.AnyAsync(u => u.Id == seeded.UserId))).Should().BeFalse();
        (await QueryAsync(ctx => ctx.ClubMembers.AnyAsync(m => m.Id == seeded.MemberId))).Should().BeFalse();
        await signInManager.Received(1).SignOutAsync();
    }

    [Fact]
    public async Task WrongPassword_Throws_AndKeepsTheAccount()
    {
        var seeded = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "me@test.com"));
        signInManager.CheckPasswordSignInAsync(Arg.Any<User>(), "wrong", true).Returns(SignInResult.Failed);

        var act = () => Handle(seeded.UserId, "wrong");

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(DeleteOwnAccount.ErrorCode.PasswordMismatch));
        (await QueryAsync(ctx => ctx.Users.AnyAsync(u => u.Id == seeded.UserId))).Should().BeTrue();
        await signInManager.DidNotReceive().SignOutAsync();
    }

    [Fact]
    public async Task LockedOut_Throws()
    {
        var seeded = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "me@test.com"));
        signInManager.CheckPasswordSignInAsync(Arg.Any<User>(), "Secret-123", true).Returns(SignInResult.LockedOut);

        var act = () => Handle(seeded.UserId, "Secret-123");

        (await act.Should().ThrowAsync<PreconditionException>()).Which.ErrorCode.Should().Be(nameof(DeleteOwnAccount.ErrorCode.LockedOut));
    }

    [Fact]
    public async Task LastAdminOfAClub_Throws_AndKeepsTheAccount()
    {
        // The seeded admin is the only admin of the test club
        signInManager.CheckPasswordSignInAsync(Arg.Any<User>(), "Secret-123", true).Returns(SignInResult.Success);

        var act = () => Handle(TestDataSeed.AdminId, "Secret-123");

        var exception = (await act.Should().ThrowAsync<PreconditionException>()).Which;
        exception.ErrorCode.Should().Be(nameof(DeleteOwnAccount.ErrorCode.LastClubAdmin));
        exception.ErrorDetails.Should().BeEquivalentTo("TestClub");
        (await QueryAsync(ctx => ctx.Users.AnyAsync(u => u.Id == TestDataSeed.AdminId))).Should().BeTrue();
    }

    [Fact]
    public async Task AdminWithAnotherAdminInTheClub_CanDeleteTheAccount()
    {
        var seeded = await QueryAsync(ctx => MemberSeed.AddUserWithMember(ctx, "secondadmin@test.com", MemberRole.Admin));
        signInManager.CheckPasswordSignInAsync(Arg.Any<User>(), "Secret-123", true).Returns(SignInResult.Success);

        await Handle(seeded.UserId, "Secret-123");

        (await QueryAsync(ctx => ctx.Users.AnyAsync(u => u.Id == seeded.UserId))).Should().BeFalse();
    }

    private Task Handle(int userId, string password)
        => ScopedAsync(async () =>
        {
            var handler = new DeleteOwnAccount.Handler(GetInstance<AppDbContext>(), signInManager, IdentityTestHelper.CreateUserAccessor(userId));
            await handler.Handle(new DeleteOwnAccount(password), CancellationToken.None);
        });
}
