using Microsoft.AspNetCore.Identity;

namespace Bookennis.Domain.User;

public sealed class UserRole : IdentityRole<int>
{
    private UserRole() { }

    public UserRole(UserRoles role)
    {
        Id = (int)role;
        Name = role.ToString();
        NormalizedName = role.ToString().Normalize();
    }
}