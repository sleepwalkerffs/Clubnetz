using System.Globalization;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.User;
using Bookennis.Shared.Utils;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

/// <summary>
/// Where a club is: its time zone (the local day, opening hours, events, prices), its country (how dates, times and amounts
/// are written, see <see cref="RegionalCulture"/>) and its currency. Read it with <see cref="ClubRegionQueries.GetClubRegion"/>
/// (once per request) instead of assuming Vienna, "de-AT" or euros.
/// </summary>
public sealed record ClubRegion(int ClubId, string TimeZoneId, Country Country, string Currency)
{
    public TimeZoneInfo TimeZone { get; } = ClubTimeZone.Get(TimeZoneId);

    /// <summary>A club in Vienna that charges euros: what every club was before the region was a setting.</summary>
    public static ClubRegion Default(int clubId = 0) => new(clubId, ClubTimeZone.DefaultId, Country.Austria, Club.DefaultCurrency);

    public static ClubRegion Of(Club club) => new(club.Id, club.TimeZoneId, club.Country, club.Currency);

    /// <summary>The local day of the club now (in New York the UTC day starts at 19:00 or 20:00 local time).</summary>
    public DateOnly Today() => ClubTimeZone.Today(TimeZone);

    public DateOnly Today(DateTimeOffset now) => ClubTimeZone.Today(TimeZone, now);

    /// <summary>The wall-clock time of the club at <paramref name="value"/>.</summary>
    public DateTime ToLocal(DateTimeOffset value) => ClubTimeZone.ToLocal(value, TimeZone);

    /// <summary><paramref name="value"/> with the club's offset at that moment (for formatting).</summary>
    public DateTimeOffset ToLocalOffset(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZone);

    public DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(ToLocal(value));

    /// <summary>The moment the local day <paramref name="date"/> starts in the club.</summary>
    public DateTimeOffset StartOfDay(DateOnly date) => ClubTimeZone.StartOfDay(date, TimeZone);

    /// <summary>The moment of the local time <paramref name="time"/> on <paramref name="date"/> in the club.</summary>
    public DateTimeOffset At(DateOnly date, TimeOnly time) => ClubTimeZone.At(date, time, TimeZone);

    public global::Bookennis.Shared.Controller.Shared.Country SharedCountry => (global::Bookennis.Shared.Controller.Shared.Country)Country;

    /// <summary>How a reader of <paramref name="language"/> sees dates, times and amounts of the club.</summary>
    public RegionalCulture Culture(Language language) => RegionalCulture.For(language == Language.German, SharedCountry, Currency);

    /// <summary>The language of <paramref name="culture"/> (e.g. of the request) with the conventions of the club.</summary>
    public RegionalCulture Culture(CultureInfo culture) => RegionalCulture.For(culture.TwoLetterISOLanguageName == "de", SharedCountry, Currency);

    /// <summary>The first day of a week in the club's country: Sunday in the United States, Monday in Europe.</summary>
    public DayOfWeek FirstDayOfWeek { get; } = RegionalCulture.For(false, (global::Bookennis.Shared.Controller.Shared.Country)Country).DateTimeFormat.FirstDayOfWeek;

    /// <summary>The days of a week in the order of the club's calendar.</summary>
    public IReadOnlyList<DayOfWeek> WeekDays => Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)FirstDayOfWeek + i) % 7)).ToList();

    /// <summary>The first day of the club's week that contains <paramref name="date"/>.</summary>
    public DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7));

    /// <summary>
    /// For texts made per language (push notifications, <c>IPushNotificationService.Notify</c>): <paramref name="create"/> gets the
    /// club's culture in the language it is asked for.
    /// </summary>
    public Func<CultureInfo, T> InCulture<T>(Func<RegionalCulture, T> create) => language => create(Culture(language));

    /// <summary>An amount in cents, e.g. "€ 12,50" or "$12.50".</summary>
    public string Money(Language language, long cents, string? currency = null) => Culture(language).Money(cents, currency ?? Currency);
}

public static class ClubRegionQueries
{
    /// <summary>
    /// The region of the club, read once per <see cref="AppDbContext"/> (a request). A club that does not exist gets
    /// <see cref="ClubRegion.Default"/>. Ignores the tenant filter: a club only reads its own region, background jobs read any.
    /// </summary>
    public static async Task<ClubRegion> GetClubRegion(this AppDbContext context, int clubId, CancellationToken cancellationToken)
    {
        if (context.ClubRegionCache.TryGetValue(clubId, out var cached))
            return cached;

        var row = await context.Clubs
            .IgnoreQueryFilters()
            .Where(c => c.Id == clubId)
            .Select(c => new { c.TimeZoneId, c.Country, c.Currency })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var region = row is null ? ClubRegion.Default(clubId) : new ClubRegion(clubId, row.TimeZoneId, row.Country, row.Currency);
        context.ClubRegionCache[clubId] = region;
        return region;
    }

    /// <summary>The region of the club of the request; <see cref="ClubRegion.Default"/> outside a club route.</summary>
    public static Task<ClubRegion> GetTenantRegion(this AppDbContext context, CancellationToken cancellationToken)
        => context.TenantId is int clubId ? context.GetClubRegion(clubId, cancellationToken) : Task.FromResult(ClubRegion.Default());

    /// <summary>The local day of the club now: use it as "today" (active season, age of children, events) instead of the UTC date.</summary>
    public static async Task<DateOnly> GetClubToday(this AppDbContext context, int clubId, CancellationToken cancellationToken)
        => (await context.GetClubRegion(clubId, cancellationToken)).Today();

    /// <summary>The local day of the club of the request now.</summary>
    public static async Task<DateOnly> GetTenantToday(this AppDbContext context, CancellationToken cancellationToken)
        => (await context.GetTenantRegion(cancellationToken)).Today();

    /// <summary>The regions of several clubs in one query (admin overviews, background jobs).</summary>
    public static async Task<IReadOnlyDictionary<int, ClubRegion>> GetClubRegions(this AppDbContext context, IReadOnlyCollection<int>? clubIds, CancellationToken cancellationToken)
    {
        var query = context.Clubs.IgnoreQueryFilters();
        if (clubIds is not null)
        {
            var missing = clubIds.Where(id => !context.ClubRegionCache.ContainsKey(id)).Distinct().ToList();
            query = query.Where(c => missing.Contains(c.Id));
        }

        var rows = await query
            .Select(c => new { c.Id, c.TimeZoneId, c.Country, c.Currency })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
            context.ClubRegionCache[row.Id] = new ClubRegion(row.Id, row.TimeZoneId, row.Country, row.Currency);

        var ids = clubIds ?? rows.Select(r => r.Id).ToList();
        return ids.Distinct().ToDictionary(id => id, id => context.ClubRegionCache.TryGetValue(id, out var region) ? region : ClubRegion.Default(id));
    }
}
