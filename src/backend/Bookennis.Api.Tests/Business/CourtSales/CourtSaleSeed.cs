using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>Seeds what court sales need in the test club: a season, a play mode for bought bookings, settings, prices, buyers.</summary>
internal static class CourtSaleSeed
{
    public const string TimeZone = "Europe/Vienna";

    /// <summary>A season around today, so a member can be enrolled (and is then no buyer).</summary>
    public static async Task<int> AddActiveSeason(AppDbContext context)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var season = new Season(TestDataSeed.ClubId, new DateOnlyInterval(today.AddDays(-30), today.AddDays(300)));
        context.Add(season);
        await context.SaveChangesAsync();
        return season.Id;
    }

    public static async Task Enroll(AppDbContext context, int memberId, int seasonId)
    {
        context.Add(new MemberSeason(memberId, seasonId));
        await context.SaveChangesAsync();
    }

    /// <summary>Switched-on settings with a play mode "Bought" (no fixed players, does not charge the subscription).</summary>
    public static async Task<CourtSaleSettings> EnableSales(AppDbContext context, int maxUpcomingBookings = 1, int maxBookingMinutes = 120, int maxDaysInAdvance = 14)
    {
        var club = await context.Clubs.Include(c => c.PlayModes).SingleAsync(c => c.Id == TestDataSeed.ClubId);
        club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], System.Drawing.Color.Gray, null, false, "Bought", null, false, false));
        await context.SaveChangesAsync();

        var settings = new CourtSaleSettings(TestDataSeed.ClubId);
        settings.Update(new CourtSaleSettings.SettingsData(true, club.PlayModes.Single(p => p.Name == "Bought").Id, TimeZone, maxBookingMinutes, maxDaysInAdvance, maxUpcomingBookings));
        context.Add(settings);
        await context.SaveChangesAsync();
        return settings;
    }

    /// <summary>Every day of the week from 00:00 to midnight on all courts.</summary>
    public static async Task<CourtPriceRule> AddAllDayRule(AppDbContext context, int pricePerHourCents = 2000, string? name = "All day")
    {
        var rule = new CourtPriceRule(TestDataSeed.ClubId, new CourtPriceRule.RuleData([], Enum.GetValues<DayOfWeek>(), TimeOnly.MinValue, TimeOnly.MinValue, pricePerHourCents, name));
        context.Add(rule);
        await context.SaveChangesAsync();
        return rule;
    }

    public static async Task<(int UserId, int MemberId)> AddBuyer(AppDbContext context, string email = "buyer@test.com")
        => await MemberSeed.AddUserWithMember(context, email);

    public static async Task AddCredits(AppDbContext context, int memberId, int count)
    {
        context.Add(new BookingCreditGrant(TestDataSeed.ClubId, memberId, count, BookingCreditReason.GrantedByClub, null, null));
        await context.SaveChangesAsync();
    }

    /// <summary>A slot tomorrow in the time zone of the sales (10:00 local), well inside the opening hours (07:00-22:00 UTC).</summary>
    public static DateTimeOffsetInterval Tomorrow(int minutes = 60, int localHour = 10)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        var localToday = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).Date;
        var local = localToday.AddDays(1).AddHours(localHour);
        var from = new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
        return new DateTimeOffsetInterval(from, from.AddMinutes(minutes));
    }

    public static async Task<CourtPurchase> AddPurchase(
        AppDbContext context,
        int memberId,
        DateTimeOffsetInterval interval,
        CourtPurchaseStatus status = CourtPurchaseStatus.Paid,
        int priceCents = 2000,
        bool creditUsed = false,
        string? sessionId = null,
        string? paymentIntentId = null,
        bool withBooking = true,
        DateTimeOffset? holdExpiresAt = null)
    {
        int? bookingId = null;
        if (withBooking)
        {
            var playModeId = (await context.CourtSaleSettings.SingleOrDefaultAsync(s => s.ClubId == TestDataSeed.ClubId))?.PlayModeId
                ?? context.PlayModes.First(p => p.ClubId == TestDataSeed.ClubId).Id;
            var booking = new Domain.Bookings.Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, interval, TimeZone, [memberId]);
            context.Add(booking);
            await context.SaveChangesAsync();
            bookingId = booking.Id;
        }

        var amountDue = creditUsed ? 0 : priceCents;
        var purchase = new CourtPurchase(TestDataSeed.ClubId, memberId, TestDataSeed.Court1Id, bookingId, interval, new CourtQuote(priceCents, "eur", []), creditUsed, amountDue);

        switch (status)
        {
            case CourtPurchaseStatus.AwaitingPayment:
                if (amountDue > 0)
                    purchase.StartCheckout(sessionId ?? "cs_test", holdExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(31));
                break;
            case CourtPurchaseStatus.Paid when amountDue == 0:
                purchase.CompleteWithoutPayment(DateTimeOffset.UtcNow);
                break;
            case CourtPurchaseStatus.Paid:
                purchase.StartCheckout(sessionId ?? "cs_test", DateTimeOffset.UtcNow.AddMinutes(31));
                purchase.MarkPaid(DateTimeOffset.UtcNow, paymentIntentId ?? "pi_test");
                break;
            case CourtPurchaseStatus.Processing:
                purchase.StartCheckout(sessionId ?? "cs_test", DateTimeOffset.UtcNow.AddMinutes(31));
                purchase.MarkProcessing(paymentIntentId ?? "pi_test");
                break;
            case CourtPurchaseStatus.Expired:
                purchase.StartCheckout(sessionId ?? "cs_test", DateTimeOffset.UtcNow.AddMinutes(-40));
                purchase.Expire();
                purchase.DetachBooking();
                break;
            case CourtPurchaseStatus.Cancelled:
                purchase.MarkPaid(DateTimeOffset.UtcNow, paymentIntentId ?? "pi_test");
                purchase.Cancel(null, DateTimeOffset.UtcNow);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Not seeded.");
        }

        context.Add(purchase);
        await context.SaveChangesAsync();
        return purchase;
    }
}
