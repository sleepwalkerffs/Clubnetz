namespace Bookennis.Shared.Controller.Guests;
public record GuestCardDto
{
    public required int Id { get; set; }
    public required string Email { get; set; }
    public required int PurchasedBookings { get; set; }
}
