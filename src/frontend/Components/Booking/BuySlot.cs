using Bookennis.Shared.Controller.Club;

namespace Bookennis.Client.Components.Booking;

/// <summary>A free slot a member who is not enrolled clicked to buy: the court, its local start time and how long it is free.</summary>
public record BuySlot(CourtResult Court, TimeSpan Start, int FreeMinutes);
