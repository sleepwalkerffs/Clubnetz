using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record AdminPlayModeRequest
{
    public required string Name { get; init; }
    public required List<MemberRole> AllowedRoles { get; init; }
    public required int Color { get; init; }
    public required int? FixedPlayerCount { get; init; }
    public required bool IsChargingBookingSubscription { get; init; }
    public required TimeSpan? FixedDuration { get; init; }
    public required bool CanOverbook { get; init; }
    public required bool CommentAllowed { get; init; }
    public int? MaxBookingsPerSeason { get; init; }
    public bool AllowRecurring { get; init; }
}
