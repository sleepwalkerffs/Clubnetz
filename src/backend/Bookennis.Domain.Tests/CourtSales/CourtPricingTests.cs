using Bookennis.Domain.CourtSales;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.CourtSales;

/// <summary>
/// The price list of a club in Vienna: Mon-Fri 08:00-12:00 = 20 €/h, 12:00-17:00 = 24 €/h, 17:00-19:00 = 28 €/h (prime time),
/// weekends 08:00-20:00 = 22 €/h on all courts, and a free youth court (court 3) on Saturday mornings before the weekend rule.
/// </summary>
public class CourtPricingTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static readonly List<CourtPriceRule> Rules =
    [
        Rule([], Weekdays, 8, 12, 2000, "Morning"),
        Rule([], Weekdays, 12, 17, 2400, "Day"),
        Rule([], Weekdays, 17, 19, 2800, "Prime time"),
        Rule([1, 2], [DayOfWeek.Saturday, DayOfWeek.Sunday], 8, 20, 2200, "Weekend"),
        Rule([3], [DayOfWeek.Saturday], 8, 12, 0, "Youth"),
    ];

    [Fact]
    public void OneHourInOneRule_IsThePricePerHour()
    {
        var quote = CourtPricing.Quote(Rules, 1, Local(2026, 10, 14, 9, 0, 60), Vienna)!; // Wednesday

        quote.PriceCents.Should().Be(2000);
        quote.Currency.Should().Be("eur");
        quote.Lines.Should().ContainSingle().Which.PricePerHourCents.Should().Be(2000);
    }

    [Fact]
    public void HalfAnHour_IsHalfThePrice()
    {
        CourtPricing.Quote(Rules, 1, Local(2026, 10, 14, 9, 0, 30), Vienna)!.PriceCents.Should().Be(1000);
    }

    [Fact]
    public void SlotSpanningTwoRules_PaysEachPartWithItsPrice()
    {
        // 16:30-18:00: 30 minutes at 24 €/h + 60 minutes at 28 €/h
        var quote = CourtPricing.Quote(Rules, 1, Local(2026, 10, 14, 16, 30, 90), Vienna)!;

        quote.PriceCents.Should().Be(1200 + 2800);
        quote.Lines.Should().HaveCount(2);
        quote.Lines[0].PricePerHourCents.Should().Be(2400);
        quote.Lines[0].RuleName.Should().Be("Day");
        quote.Lines[1].PricePerHourCents.Should().Be(2800);
        (quote.Lines[1].To - quote.Lines[1].From).Should().Be(TimeSpan.FromHours(1));
        quote.Lines[0].To.Should().Be(quote.Lines[1].From);
    }

    [Fact]
    public void MinuteWithoutRule_IsNotForSale()
    {
        // 18:30-19:30: the last half hour has no rule
        CourtPricing.Quote(Rules, 1, Local(2026, 10, 14, 18, 30, 60), Vienna).Should().BeNull();

        // Before the first rule of the day
        CourtPricing.Quote(Rules, 1, Local(2026, 10, 14, 7, 30, 60), Vienna).Should().BeNull();
    }

    [Fact]
    public void RuleForSomeCourts_DoesNotPriceOtherCourts()
    {
        // Sunday: the weekend rule is for courts 1 and 2 only
        CourtPricing.Quote(Rules, 1, Local(2026, 10, 18, 10, 0, 60), Vienna)!.PriceCents.Should().Be(2200);
        CourtPricing.Quote(Rules, 3, Local(2026, 10, 18, 10, 0, 60), Vienna).Should().BeNull();
    }

    [Fact]
    public void RuleForAllCourts_PricesEveryCourt()
    {
        foreach (var courtId in new[] { 1, 2, 3, 99 })
            CourtPricing.Quote(Rules, courtId, Local(2026, 10, 14, 10, 0, 60), Vienna)!.PriceCents.Should().Be(2000);
    }

    [Fact]
    public void FreeRule_CostsNothing()
    {
        var quote = CourtPricing.Quote(Rules, 3, Local(2026, 10, 17, 9, 0, 60), Vienna)!; // Saturday

        quote.PriceCents.Should().Be(0);
        quote.Lines.Should().ContainSingle().Which.RuleName.Should().Be("Youth");
    }

    [Fact]
    public void WeekdayBoundary_FridayEveningAndSaturdayMorningUseTheRuleOfTheirDay()
    {
        // Friday 17:00 (prime time) and Saturday 08:00 (weekend) in local time, whatever the UTC date is
        CourtPricing.Quote(Rules, 1, Local(2026, 10, 16, 17, 0, 60), Vienna)!.PriceCents.Should().Be(2800);
        CourtPricing.Quote(Rules, 1, Local(2026, 10, 17, 8, 0, 60), Vienna)!.PriceCents.Should().Be(2200);
    }

    [Fact]
    public void MidnightRule_CoversTheLastHalfHourOfTheDay()
    {
        List<CourtPriceRule> late = [new(1, new CourtPriceRule.RuleData([], [DayOfWeek.Wednesday], new TimeOnly(22, 0), TimeOnly.MinValue, 1000, "Late"))];

        CourtPricing.Quote(late, 1, Local(2026, 10, 14, 23, 30, 30), Vienna)!.PriceCents.Should().Be(500);
        CourtPricing.Quote(late, 1, Local(2026, 10, 14, 23, 30, 60), Vienna).Should().BeNull("the minutes after midnight are Thursday");
    }

    [Fact]
    public void DaylightSavingTimeEnds_TheRepeatedHourIsPlayedAndPaidTwice()
    {
        // Sunday 25 Oct 2026: clocks go back from 03:00 to 02:00 in Vienna. 01:30-03:30 local is three hours of play.
        List<CourtPriceRule> night = [new(1, new CourtPriceRule.RuleData([], [DayOfWeek.Sunday], TimeOnly.MinValue, new TimeOnly(6, 0), 1000, null))];
        var from = new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.Zero); // 01:30 CEST
        var to = new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.Zero); // 03:30 CET

        var quote = CourtPricing.Quote(night, 1, new DateTimeOffsetInterval(from, to), Vienna)!;

        quote.PriceCents.Should().Be(3000);
    }

    [Fact]
    public void DaylightSavingTimeStarts_ThePricesFollowTheLocalTime()
    {
        // Sunday 29 Mar 2026: clocks go forward from 02:00 to 03:00. A rule from 03:00 starts right after 01:59 local.
        List<CourtPriceRule> rules =
        [
            new(1, new CourtPriceRule.RuleData([], [DayOfWeek.Sunday], TimeOnly.MinValue, new TimeOnly(2, 0), 1000, "Night")),
            new(1, new CourtPriceRule.RuleData([], [DayOfWeek.Sunday], new TimeOnly(3, 0), new TimeOnly(6, 0), 2000, "Early")),
        ];
        var from = new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero); // 01:30 CET
        var to = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero); // 03:30 CEST

        var quote = CourtPricing.Quote(rules, 1, new DateTimeOffsetInterval(from, to), Vienna)!;

        quote.PriceCents.Should().Be(500 + 1000, "half an hour at night and half an hour early: one hour was played");
        quote.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void Rounding_HappensOnceAtTheEnd()
    {
        // 9.99 €/h for 90 minutes = 14.985 € -> 14.99 €
        List<CourtPriceRule> rules = [new(1, new CourtPriceRule.RuleData([], Weekdays, new TimeOnly(8, 0), new TimeOnly(20, 0), 999, null))];

        CourtPricing.Quote(rules, 1, Local(2026, 10, 14, 10, 0, 90), Vienna)!.PriceCents.Should().Be(1499);
    }

    [Fact]
    public void EmptyOrPartialMinute_IsNotForSale()
    {
        var from = Local(2026, 10, 14, 10, 0, 0).From;
        CourtPricing.Quote(Rules, 1, new DateTimeOffsetInterval(from, from), Vienna).Should().BeNull();
        CourtPricing.Quote(Rules, 1, new DateTimeOffsetInterval(from, from.AddSeconds(90)), Vienna).Should().BeNull();
    }

    [Fact]
    public void QuoteFirstMinutes_IsThePriceOfTheFirstHour()
    {
        // 16:30-18:30: the first hour is 30 min at 24 €/h + 30 min at 28 €/h
        var slot = Local(2026, 10, 14, 16, 30, 120);

        CourtPricing.QuoteFirstMinutes(Rules, 1, slot, Vienna, 60)!.PriceCents.Should().Be(1200 + 1400);
        CourtPricing.Quote(Rules, 1, slot, Vienna)!.PriceCents.Should().Be(1200 + 2800 + 1400);
    }

    [Fact]
    public void QuoteFirstMinutes_ShorterSlot_IsTheWholeSlot()
    {
        var slot = Local(2026, 10, 14, 9, 0, 30);

        CourtPricing.QuoteFirstMinutes(Rules, 1, slot, Vienna, 60)!.PriceCents.Should().Be(1000);
    }

    // ---------- rules ----------

    [Fact]
    public void Rules_SharingAWeekdayACourtAndTime_Overlap()
    {
        var morning = Rule([], Weekdays, 8, 12, 2000, "Morning");

        morning.Overlaps(Rule([1], [DayOfWeek.Monday], 11, 13, 1000, null)).Should().BeTrue("all courts include court 1");
        morning.Overlaps(Rule([], [DayOfWeek.Saturday], 8, 12, 1000, null)).Should().BeFalse("no shared weekday");
        morning.Overlaps(Rule([], Weekdays, 12, 14, 1000, null)).Should().BeFalse("12:00 is where the morning ends");
        Rule([1], Weekdays, 8, 12, 1, null).Overlaps(Rule([2], Weekdays, 8, 12, 1, null)).Should().BeFalse("different courts");
    }

    [Fact]
    public void EnsureNoOverlap_NamesTheOtherRule()
    {
        var rule = Rule([], [DayOfWeek.Monday], 11, 13, 1000, null);

        var act = () => rule.EnsureNoOverlap(Rules);

        var exception = act.Should().Throw<Exceptions.PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPriceRulesOverlap));
        exception.ErrorDetails.Should().Equal("Morning", "08:00", "12:00");
    }

    [Theory]
    [InlineData(10, 0, 9, 0)]
    [InlineData(10, 0, 10, 0)]
    [InlineData(10, 15, 11, 0)]
    [InlineData(10, 0, 11, 45)]
    public void Rule_InvalidTimes_AreRejected(int fromHour, int fromMinute, int toHour, int toMinute)
    {
        var act = () => new CourtPriceRule(1, new CourtPriceRule.RuleData([], Weekdays, new TimeOnly(fromHour, fromMinute), new TimeOnly(toHour, toMinute), 1000, null));

        act.Should().Throw<Exceptions.PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPriceRuleInvalidTime));
    }

    [Fact]
    public void Rule_WithoutWeekday_OrNegativePrice_IsRejected()
    {
        var noDay = () => new CourtPriceRule(1, new CourtPriceRule.RuleData([], [], new TimeOnly(8, 0), new TimeOnly(9, 0), 1000, null));
        var negative = () => new CourtPriceRule(1, new CourtPriceRule.RuleData([], Weekdays, new TimeOnly(8, 0), new TimeOnly(9, 0), -1, null));

        noDay.Should().Throw<Exceptions.PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPriceRuleWeekdayRequired));
        negative.Should().Throw<Exceptions.PreconditionException>().Which.ErrorCode.Should().Be(nameof(CourtSaleErrorCode.CourtPriceRuleInvalidPrice));
    }

    private static CourtPriceRule Rule(int[] courts, DayOfWeek[] days, int fromHour, int toHour, int price, string? name)
        => new(1, new CourtPriceRule.RuleData(courts, days, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0), price, name));

    /// <summary>A slot starting at a local time in Vienna, as UTC.</summary>
    private static DateTimeOffsetInterval Local(int year, int month, int day, int hour, int minute, int minutes)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        var from = new DateTimeOffset(local, Vienna.GetUtcOffset(local)).ToUniversalTime();
        return new DateTimeOffsetInterval(from, from.AddMinutes(minutes));
    }
}
