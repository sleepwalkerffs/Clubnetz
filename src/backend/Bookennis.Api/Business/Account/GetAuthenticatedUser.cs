using System.Globalization;
using System.Security.Claims;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Shared.Controller.Account;
using Fusonic.Extensions.Common.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bookennis.Api.Business.Account;

public record GetAuthenticatedUser : IQuery<GetAuthenticatedUserResult>
{
    public class Handler(IUserAccessor userAccessor) : IRequestHandler<GetAuthenticatedUser, GetAuthenticatedUserResult>
    {
        public Task<GetAuthenticatedUserResult> Handle(GetAuthenticatedUser request, CancellationToken cancellationToken)
        {
            if (!userAccessor.TryGetUser(out var user) || user.Identity is null || !user.Identity.IsAuthenticated)
                throw new AuthorizationFailedException();

            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

            return Task.FromResult(
                new GetAuthenticatedUserResult(
                    int.Parse(user.FindFirst(JwtRegisteredClaimNames.Sub)!.Value, CultureInfo.InvariantCulture),
                    user.FindFirst(JwtRegisteredClaimNames.GivenName)!.Value,
                    user.FindFirst(JwtRegisteredClaimNames.FamilyName)!.Value,
                    user.FindFirst(JwtRegisteredClaimNames.Email)!.Value,
                    user.FindFirst(JwtRegisteredClaimNames.Name)!.Value,
                    roles
                )
            );
        }
    }
}
