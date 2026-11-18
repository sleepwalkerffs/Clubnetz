using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Courts;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Shared;

/// <summary>
/// Statistics, leaderboards, badges and the Wrapped split by sport: a booking's sport is the sport of its court.
/// <c>null</c> = every sport (the default, so clients that send no sport keep getting the same numbers).
/// </summary>
public static class SportFilter
{
    /// <summary>Only bookings on courts of <paramref name="sport"/>; every booking when it is <c>null</c>.</summary>
    public static IQueryable<Booking> OfSport(this IQueryable<Booking> bookings, AppDbContext context, Sport? sport)
        => sport is { } s
            ? bookings.Where(b => context.Courts.Any(c => c.Id == b.CourtId && c.Sport == s))
            : bookings;

    /// <summary>Only players of bookings on courts of <paramref name="sport"/>; every player when it is <c>null</c>.</summary>
    public static IQueryable<BookingPlayer> OfSport(this IQueryable<BookingPlayer> players, AppDbContext context, Sport? sport)
        => sport is { } s
            ? players.Where(bp => context.Courts.Any(c => c.Id == bp.Booking.CourtId && c.Sport == s))
            : players;

    /// <summary>The sports of the club's courts in their order (Tennis first). More than one: the app shows sport filters.</summary>
    public static async Task<List<Sport>> OrderedClubSports(this AppDbContext context, int clubId, CancellationToken cancellationToken)
        => (await context.Courts
                .Where(c => c.ClubId == clubId)
                .Select(c => c.Sport)
                .Distinct()
                .ToListAsync(cancellationToken))
            .Order()
            .ToList();

    public static Bookennis.Shared.Controller.Shared.Sport ToDto(this Sport sport) => (Bookennis.Shared.Controller.Shared.Sport)sport;

    public static Bookennis.Shared.Controller.Shared.Sport? ToDto(this Sport? sport) => (Bookennis.Shared.Controller.Shared.Sport?)sport;

    public static Sport? ToDomain(this Bookennis.Shared.Controller.Shared.Sport? sport) => (Sport?)sport;
}
