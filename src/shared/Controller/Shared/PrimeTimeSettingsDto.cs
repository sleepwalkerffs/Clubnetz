using Bookennis.Global.Intervals;

namespace Bookennis.Shared.Controller.Shared;

public record PrimeTimeSettingsDto
{
    public required bool IsEnabled { get; init; }
    public required TimeOnlyInterval PrimeTimeHours { get; init; }
    public required DayOfWeek[] ApplicableWeekdays { get; init; }
    public required bool RestrictChildren { get; init; }
    public required bool RestrictGuests { get; init; }
    public required int ChildAgeThreshold { get; init; }

    public static DayOfWeek[] DefaultWeekdays =>
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
}
