namespace Bookennis.Shared.Controller.Booking.Shared;
public record PlayerResult(int MemberId, string FirstName, string LastName, bool IsGuest, bool IsAtpPlayer = false, int UserId = 0, string? ProfilePictureUrl = null, string? DisplayBadgeImageUrl = null, int? DisplayBadgeLevel = null)
{
    /// <summary>The sports of the player's enrollment in the active season; empty = every sport (and players of other clubs). Pickers leave out players who can't play on the court.</summary>
    public List<Bookennis.Shared.Controller.Shared.Sport> Sports { get; init; } = [];
}