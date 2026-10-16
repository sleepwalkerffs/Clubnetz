using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Shared.Controller.Club;
/// <summary>The players that can be picked for a booking.</summary>
public record GetPlayersResult(List<PlayerResult> ClubMembers);
