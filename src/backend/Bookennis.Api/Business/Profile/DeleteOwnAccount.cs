using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Users;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Profile;

/// <summary>
/// Lets the current user delete their own account with all personal data (GDPR Art. 17). The current password is required and counts
/// towards the lockout. The user is signed out afterwards.
/// </summary>
public record DeleteOwnAccount([Required] string CurrentPassword) : ICommand
{
    public enum ErrorCode
    {
        PasswordMismatch,
        LockedOut,
        LastClubAdmin
    }

    public class Handler(AppDbContext context, SignInManager<User> signInManager, IUserAccessor userAccessor) : IRequestHandler<DeleteOwnAccount>
    {
        public async Task<Unit> Handle(DeleteOwnAccount request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var userId))
                throw new AuthorizationFailedException();

            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
                ?? throw new AuthorizationFailedException();

            var passwordCheck = await signInManager.CheckPasswordSignInAsync(user, request.CurrentPassword, lockoutOnFailure: true);
            if (passwordCheck.IsLockedOut)
                throw new PreconditionException(ErrorCode.LockedOut, "The account is locked out.");
            if (!passwordCheck.Succeeded)
                throw new PreconditionException(ErrorCode.PasswordMismatch, "The password is incorrect.");

            var memberIds = await context.ClubMembers.IgnoreQueryFilters().Where(m => m.UserId == user.Id).Select(m => m.Id).ToListAsync(cancellationToken);
            if (await MemberRemoval.FindClubLosingItsLastAdmin(context, memberIds, cancellationToken) is { } clubName)
                throw new PreconditionException(ErrorCode.LastClubAdmin, [clubName], $"The user is the last admin of the club {clubName}.");

            await UserDeletion.DeleteUser(context, user, cancellationToken);
            await signInManager.SignOutAsync();

            return default;
        }
    }
}
