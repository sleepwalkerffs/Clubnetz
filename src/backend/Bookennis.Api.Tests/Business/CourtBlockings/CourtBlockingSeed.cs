using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Courts;
using Bookennis.Global.Intervals;

namespace Bookennis.Api.Tests.Business.CourtBlockings;

internal static class CourtBlockingSeed
{
    public const string Title = "Club championship";

    public static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

    /// <summary>Blocks court 1 on <see cref="Day"/> from 10:00 to 14:00 (UTC).</summary>
    public static CourtBlocking.BlockingData Data(DateOnly? day = null, IReadOnlyCollection<int>? courtIds = null) => new(
        Title,
        courtIds ?? [TestDataSeed.Court1Id],
        day ?? Day,
        day ?? Day,
        new TimeOnly(10, 0),
        new TimeOnly(14, 0),
        TimeZoneInfo.Utc.Id,
        null,
        null);

    public static async Task<CourtBlocking> Seed(AppDbContext context, CourtBlocking.BlockingData? data = null)
    {
        var blocking = new CourtBlocking(TestDataSeed.ClubId, data ?? Data());
        context.CourtBlockings.Add(blocking);
        await context.SaveChangesAsync();
        return blocking;
    }

    /// <summary>A one hour booking of member 1 (UTC).</summary>
    public static async Task<Booking> SeedBooking(AppDbContext context, DateOnly day, int hour, int courtId = TestDataSeed.Court1Id)
    {
        var playModeId = context.TestData().Club.PlayModes[0].Id;
        var booking = new Booking(TestDataSeed.ClubId, courtId, playModeId, At(day, hour, hour + 1), TimeZoneInfo.Utc.Id, [TestDataSeed.Member1Id]);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return booking;
    }

    public static DateTimeOffsetInterval AllDay(DateOnly day)
        => new(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    public static DateTimeOffsetInterval At(DateOnly day, int fromHour, int toHour)
        => new(new DateTimeOffset(day.ToDateTime(new TimeOnly(fromHour, 0)), TimeSpan.Zero), new DateTimeOffset(day.ToDateTime(new TimeOnly(toHour, 0)), TimeSpan.Zero));
}
