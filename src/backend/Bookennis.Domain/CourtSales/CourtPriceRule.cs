using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Exceptions;

namespace Bookennis.Domain.CourtSales;

/// <summary>
/// The price of a court per hour on some weekdays between two local times (in <see cref="CourtSaleSettings.TimeZoneId"/>),
/// e.g. Mon-Fri 08:00-12:00 = 20 €/h. Without courts the rule is for all courts. Rules of a club never overlap,
/// so every minute has at most one price; a minute without a rule is not for sale.
/// </summary>
[Guid("5E9B2C71-0A4D-4F3E-8C16-7D2A9B4E1F08")]
public class CourtPriceRule : TenantDomainEntity, IAggregateRoot
{
    public const int MaxNameLength = 60;
    public const int MaxPricePerHourCents = 100_000;
    public const string DefaultCurrency = "eur";
    private const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// <c>To</c> = 00:00 means midnight at the end of the day. Times are on a full or half hour.
    /// An empty <c>CourtIds</c> means all courts of the club.
    /// </summary>
    public record RuleData(IReadOnlyCollection<int> CourtIds, IReadOnlyCollection<DayOfWeek> Weekdays, TimeOnly From, TimeOnly To, int PricePerHourCents, string? Name);

#pragma warning disable CS8618
    private CourtPriceRule() { }
#pragma warning restore CS8618

    public CourtPriceRule(int clubId, RuleData data)
    {
        ClubId = clubId;
        Currency = DefaultCurrency;
        CourtIds = [];
        Weekdays = [];
        Update(data);
    }

    /// <summary>Empty: all courts of the club, also courts added later.</summary>
    public int[] CourtIds { get; private set; }

    public DayOfWeek[] Weekdays { get; private set; }
    public TimeOnly From { get; private set; }

    /// <summary>00:00 is midnight at the end of the day.</summary>
    public TimeOnly To { get; private set; }

    public int PricePerHourCents { get; private set; }
    public string Currency { get; private set; }

    /// <summary>A label for the treasurers and buyers, e.g. "Prime time".</summary>
    public string? Name { get; private set; }

    public bool IsForAllCourts => CourtIds.Length == 0;

    /// <summary>The first minute of the day the rule covers.</summary>
    public int StartMinute => (int)From.ToTimeSpan().TotalMinutes;

    /// <summary>The minute of the day the rule ends (exclusive); 1440 for midnight.</summary>
    public int EndMinute => To == TimeOnly.MinValue ? MinutesPerDay : (int)To.ToTimeSpan().TotalMinutes;

    public void Update(RuleData data)
    {
        var weekdays = data.Weekdays.Distinct().Order().ToArray();
        if (weekdays.Length == 0)
            throw new PreconditionException(CourtSaleErrorCode.CourtPriceRuleWeekdayRequired, "A price rule needs at least one weekday.");

        var start = (int)data.From.ToTimeSpan().TotalMinutes;
        var end = data.To == TimeOnly.MinValue ? MinutesPerDay : (int)data.To.ToTimeSpan().TotalMinutes;
        if (start >= end || !IsOnSlot(data.From) || !IsOnSlot(data.To))
            throw new PreconditionException(CourtSaleErrorCode.CourtPriceRuleInvalidTime, "The start has to be before the end, both on a full or half hour.");

        if (data.PricePerHourCents is < 0 or > MaxPricePerHourCents)
            throw new PreconditionException(CourtSaleErrorCode.CourtPriceRuleInvalidPrice, "Invalid price.");

        var name = string.IsNullOrWhiteSpace(data.Name) ? null : data.Name.Trim();
        if (name?.Length > MaxNameLength)
            throw new PreconditionException(CourtSaleErrorCode.CourtPriceRuleNameTooLong, [MaxNameLength.ToString()], "The name is too long.");

        CourtIds = data.CourtIds.Distinct().Order().ToArray();
        Weekdays = weekdays;
        From = data.From;
        To = data.To;
        PricePerHourCents = data.PricePerHourCents;
        Name = name;
    }

    public bool AppliesTo(int courtId, DayOfWeek day, int minuteOfDay)
        => Weekdays.Contains(day)
        && (IsForAllCourts || CourtIds.Contains(courtId))
        && minuteOfDay >= StartMinute && minuteOfDay < EndMinute;

    /// <summary>Both rules can price the same minute: a shared weekday, a shared court (no courts = all) and intersecting times.</summary>
    public bool Overlaps(CourtPriceRule other)
        => Weekdays.Intersect(other.Weekdays).Any()
        && (IsForAllCourts || other.IsForAllCourts || CourtIds.Intersect(other.CourtIds).Any())
        && StartMinute < other.EndMinute && other.StartMinute < EndMinute;

    /// <summary>Throws <see cref="CourtSaleErrorCode.CourtPriceRulesOverlap"/> when the rule overlaps one of the other rules of the club.</summary>
    public void EnsureNoOverlap(IEnumerable<CourtPriceRule> otherRules)
    {
        var overlapping = otherRules.FirstOrDefault(other => !ReferenceEquals(other, this) && (other.Id == 0 || other.Id != Id) && Overlaps(other));
        if (overlapping is not null)
        {
            throw new PreconditionException(
                CourtSaleErrorCode.CourtPriceRulesOverlap,
                [overlapping.Name ?? "", overlapping.From.ToString("HH:mm"), overlapping.To.ToString("HH:mm")],
                "The price rule overlaps another rule.");
        }
    }

    private static bool IsOnSlot(TimeOnly time) => time.Second == 0 && time.Millisecond == 0 && time.Minute % CourtSaleSettings.SlotMinutes == 0;
}
