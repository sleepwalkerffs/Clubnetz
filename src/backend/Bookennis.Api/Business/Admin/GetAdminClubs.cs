using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record GetAdminClubs : IQuery<List<AdminClubResult>>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClubs, List<AdminClubResult>>
    {
        public async Task<List<AdminClubResult>> Handle(GetAdminClubs request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var since = now.AddDays(-AdminClubQueryExtensions.ActivityDays);

            var clubs = await context.Clubs
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    MemberCount = context.ClubMembers.Count(m => m.ClubId == c.Id),
                    PlayModeCount = c.PlayModes.Count,
                    CourtCount = context.Courts.Count(court => court.ClubId == c.Id),
                    BookingsLast30Days = context.Bookings.Count(b => b.ClubId == c.Id && b.Interval.From >= since && b.Interval.From <= now)
                })
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var admins = await context.GetClubAdmins(clubId: null, cancellationToken);
            var clubsWithActiveSeason = await context.ClubsWithActiveSeason(cancellationToken);

            return clubs.ConvertAll(c => new AdminClubResult
            {
                Id = c.Id,
                Name = c.Name,
                MemberCount = c.MemberCount,
                PlayModeCount = c.PlayModeCount,
                CourtCount = c.CourtCount,
                HasActiveSeason = clubsWithActiveSeason.Contains(c.Id),
                BookingsLast30Days = c.BookingsLast30Days,
                Admins = admins.GetValueOrDefault(c.Id) ?? []
            });
        }
    }
}

public static class AdminClubQueryExtensions
{
    /// <summary>The number of days the booking activity in the admin panel looks back.</summary>
    public const int ActivityDays = 30;

    /// <summary>
    /// The clubs that have a season containing their today (the local day of each club, so it is decided in memory).
    /// Only the seasons around the UTC day are read.
    /// </summary>
    public static async Task<HashSet<int>> ClubsWithActiveSeason(this AppDbContext context, CancellationToken cancellationToken)
    {
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var seasons = await context.Seasons
            .Where(s => s.Period.From <= utcToday.AddDays(1) && s.Period.To >= utcToday.AddDays(-1))
            .Select(s => new { s.ClubId, s.Period.From, s.Period.To })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var regions = await context.GetClubRegions(seasons.Select(s => s.ClubId).Distinct().ToList(), cancellationToken);
        return seasons
            .Where(s => regions[s.ClubId].Today() is var today && s.From <= today && today <= s.To)
            .Select(s => s.ClubId)
            .ToHashSet();
    }

    /// <summary>The club admins by club id, of one club or of all clubs.</summary>
    public static async Task<Dictionary<int, List<AdminClubContact>>> GetClubAdmins(this AppDbContext context, int? clubId, CancellationToken cancellationToken)
    {
        var admins = await (from member in context.ClubMembers
                            join user in context.Users on member.UserId equals user.Id
                            where clubId == null || member.ClubId == clubId
                            where member.UserRoles.Contains(MemberRole.Admin)
                            orderby user.LastName, user.FirstName
                            select new { member.ClubId, user.Id, user.FullName, user.Email })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return admins
            .GroupBy(a => a.ClubId)
            .ToDictionary(g => g.Key, g => g.Select(a => new AdminClubContact { UserId = a.Id, FullName = a.FullName, Email = a.Email }).ToList());
    }
}
