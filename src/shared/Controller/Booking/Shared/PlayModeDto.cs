using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Booking.Shared;

public record PlayModeDto
{
    public required int Id { get; init; }
    public required List<MemberRole> AllowedRoles { get; init; }
    public required int Color { get; init; }
    public required int? FixedPlayerCount { get; init; }
    public required bool IsChargingBookingSubscription { get; init; }
    public required string Name { get; init; }
    public required TimeSpan? FixedDuration { get; init; }
    public required bool CanOverbook { get; init; }
    public required bool CommentAllowed { get; init; }
    public required int? MaxBookingsPerSeason { get; init; }
    public required bool AllowRecurring { get; init; }
}
