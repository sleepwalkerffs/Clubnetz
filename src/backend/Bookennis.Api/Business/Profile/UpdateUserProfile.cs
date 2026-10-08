using System.ComponentModel.DataAnnotations;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.User;
using Fusonic.Extensions.Common.Security;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Bookennis.Api.Business.Profile;

public record UpdateUserProfile([Required] string FirstName, [Required] string LastName, [Required] DateOnly Birthday, [Required] Gender Gender, [Required] Language Language, string? Street, string? City, string? ZipCode, [Required] Country Country) : ICommand
{
    public class Handler(AppDbContext context, IUserAccessor userAccessor, SignInManager<User> signInManager) : IRequestHandler<UpdateUserProfile>
    {
        public async Task<Unit> Handle(UpdateUserProfile request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUserId(out var id))
                throw new AuthorizationFailedException();

            var user = await context.Users.SingleRequiredAsync(x => x.Id == id, cancellationToken);
            user.Update(request.FirstName, request.LastName, request.Birthday, request.Gender, request.Language, request.Street ?? string.Empty, request.City ?? string.Empty, request.ZipCode ?? string.Empty, request.Country);
            await context.SaveChangesAsync(cancellationToken);

            await signInManager.RefreshSignInAsync(user);

            return default;
        }
    }
}