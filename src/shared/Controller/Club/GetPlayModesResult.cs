using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Shared.Controller.Club;
public record GetPlayModesResult
{
    public required List<PlayModeDto> PlayModes { get; init; }
}
