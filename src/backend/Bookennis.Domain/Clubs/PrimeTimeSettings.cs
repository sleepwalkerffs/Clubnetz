using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Clubs;

public class PrimeTimeSettings
{
    private PrimeTimeSettings()
    {
        ApplicableWeekdays = [];
        PrimeTimeHours = new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0));
    }

    public PrimeTimeSettings(bool isEnabled, TimeOnlyInterval primeTimeHours, DayOfWeek[] applicableWeekdays, bool restrictChildren, bool restrictGuests, int childAgeThreshold)
    {
        IsEnabled = isEnabled;
        PrimeTimeHours = primeTimeHours;
        ApplicableWeekdays = applicableWeekdays;
        RestrictChildren = restrictChildren;
        RestrictGuests = restrictGuests;
        ChildAgeThreshold = childAgeThreshold;
    }

    public bool IsEnabled { get; private set; }
    public TimeOnlyInterval PrimeTimeHours { get; private set; }
    public DayOfWeek[] ApplicableWeekdays { get; private set; }
    public bool RestrictChildren { get; private set; }
    public bool RestrictGuests { get; private set; }
    public int ChildAgeThreshold { get; private set; }

    public static PrimeTimeSettings Default => new(
        isEnabled: true,
        primeTimeHours: new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
        applicableWeekdays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        restrictChildren: true,
        restrictGuests: true,
        childAgeThreshold: 18);

    public bool IsPrimeTimeActiveOn(DayOfWeek dayOfWeek) => IsEnabled && ApplicableWeekdays.Contains(dayOfWeek);
}
