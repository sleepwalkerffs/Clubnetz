using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record AdminClubDetailResult
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required TimeOnlyInterval OpeningHours { get; init; }
    public required PrimeTimeSettingsDto PrimeTimeSettings { get; init; }
    public required int? BookingGracePeriodInMinutes { get; init; }
    public required int ConcurrentAllowedBookings { get; init; }
    public required bool IsAtpClub { get; init; }
    public required List<PlayModeDto> PlayModes { get; init; }
    public required List<AdminSeasonResult> Seasons { get; init; }
    public int MemberCount { get; init; }
    public int GuestCount { get; init; }
    public int CourtCount { get; init; }
    public int BookingsLast30Days { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<AdminClubContact> Admins { get; init; } = [];
}
