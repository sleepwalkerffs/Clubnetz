using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Club;

public record PlayModeRequest
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

    /// <summary>The courts the play mode can be booked on; empty means every court.</summary>
    public List<int> CourtIds { get; init; } = [];
}

public record PlayModeCourtConflictsRequest
{
    /// <summary>The courts the play mode would be limited to; empty means every court.</summary>
    public required List<int> CourtIds { get; init; }
}

public record PlayModeCourtConflictsResult
{
    /// <summary>Upcoming bookings of the play mode on courts outside <see cref="PlayModeCourtConflictsRequest.CourtIds"/>. They are kept.</summary>
    public required int UpcomingBookingsOnOtherCourts { get; init; }
}
