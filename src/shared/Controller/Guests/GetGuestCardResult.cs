namespace Bookennis.Shared.Controller.Guests;

public record GetGuestCardResult
{
    public required List<GuestCardDto> GuestCards { get; set; }
}
