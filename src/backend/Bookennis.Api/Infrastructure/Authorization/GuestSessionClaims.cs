namespace Bookennis.Api.Infrastructure.Authorization;

/// <summary>Claims added by <see cref="Business.Account.LoginGuest"/> to mark a guest session.</summary>
public static class GuestSessionClaims
{
    public const string GuestSession = "guest_session";
    public const string GuestMemberId = "guest_member_id";
}
