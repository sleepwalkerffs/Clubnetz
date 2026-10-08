using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Shared.Controller.Club;
public record GetPlayersResult(List<PlayerResult> ClubMembers, List<PlayerResult> GuestMembers);
