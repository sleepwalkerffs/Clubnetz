using Bookennis.Api.Infrastructure.Exceptions;
using Fusonic.Extensions.Common.Security;

namespace Bookennis.Api.Infrastructure.User;

public static class UserAccessorExtensions
{
    public static bool TryGetUserId(this IUserAccessor? userAccessor, out int id)
    {
        id = -1;
        return userAccessor is not null && userAccessor.TryGetUser(out var user) && user.TryGetId(out id);
    }

    public static int GetUserId(this IUserAccessor? userAccessor)
    {
        if (!userAccessor.TryGetUserId(out var id))
            throw new AuthorizationFailedException();
        return id;
    }
}
