using Bookennis.Global.Intervals;

namespace Bookennis.Domain.CourtSales;

/// <summary>A price: the amount in cents and the parts of the time with their price per hour.</summary>
public sealed record CourtQuote(int PriceCents, string Currency, IReadOnlyList<CourtQuoteLine> Lines);

/// <summary>A part of the booked time that has one price per hour. <c>From</c>/<c>To</c> are UTC.</summary>
public sealed record CourtQuoteLine(DateTimeOffset From, DateTimeOffset To, int PricePerHourCents, string? RuleName);

/// <summary>
/// What a court costs. Walks the booked time minute by minute in the local time of the club, so a slot can span two rules
/// and days with a change of daylight saving time are priced by the time that is really played.
/// </summary>
public static class CourtPricing
{
    /// <summary>
    /// The price of the court for the interval, or <c>null</c> when a minute of it is not covered by a rule (not for sale)
    /// or the interval is empty or not made of whole minutes. The amount is rounded to cents once, at the end.
    /// </summary>
    public static CourtQuote? Quote(IReadOnlyCollection<CourtPriceRule> rules, int courtId, DateTimeOffsetInterval interval, TimeZoneInfo timeZone)
    {
        var totalMinutes = (interval.To - interval.From).TotalMinutes;
        if (totalMinutes <= 0 || totalMinutes != Math.Floor(totalMinutes))
            return null;

        var from = interval.From.ToUniversalTime();
        var to = interval.To.ToUniversalTime();

        // Sum of price per hour over the minutes: the amount is that divided by 60
        long centMinutes = 0;
        var lines = new List<CourtQuoteLine>();
        CourtPriceRule? currentRule = null;
        var lineStart = from;

        for (var minute = from; minute < to; minute = minute.AddMinutes(1))
        {
            var local = TimeZoneInfo.ConvertTime(minute, timeZone);
            var minuteOfDay = local.Hour * 60 + local.Minute;
            var rule = rules.FirstOrDefault(r => r.AppliesTo(courtId, local.DayOfWeek, minuteOfDay));
            if (rule is null)
                return null;

            if (currentRule is not null && !ReferenceEquals(rule, currentRule))
            {
                lines.Add(new CourtQuoteLine(lineStart, minute, currentRule.PricePerHourCents, currentRule.Name));
                lineStart = minute;
            }

            currentRule = rule;
            centMinutes += rule.PricePerHourCents;
        }

        lines.Add(new CourtQuoteLine(lineStart, to, currentRule!.PricePerHourCents, currentRule.Name));

        var amount = (int)Math.Round(centMinutes / 60m, MidpointRounding.AwayFromZero);
        return new CourtQuote(amount, currentRule.Currency, lines);
    }

    /// <summary>The price of the first <paramref name="minutes"/> of the interval (all of it when it is shorter), e.g. what a credit covers.</summary>
    public static CourtQuote? QuoteFirstMinutes(IReadOnlyCollection<CourtPriceRule> rules, int courtId, DateTimeOffsetInterval interval, TimeZoneInfo timeZone, int minutes)
    {
        var end = interval.From.AddMinutes(minutes);
        var first = end < interval.To ? new DateTimeOffsetInterval(interval.From, end) : interval;
        return Quote(rules, courtId, first, timeZone);
    }
}
