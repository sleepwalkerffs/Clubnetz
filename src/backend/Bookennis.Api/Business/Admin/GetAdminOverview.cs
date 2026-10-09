using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

/// <summary>The numbers of the whole platform for the start page of the admin panel.</summary>
public record GetAdminOverview : IQuery<AdminOverviewResult>
{
    public const int RecentUserCount = 6;

    public class Handler(AppDbContext context) : IRequestHandler<GetAdminOverview, AdminOverviewResult>
    {
        public async Task<AdminOverviewResult> Handle(GetAdminOverview request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTimeOffset.UtcNow;
            var since = now.AddDays(-AdminClubQueryExtensions.ActivityDays);
            var sinceUtc = since.UtcDateTime;

            // Children without a login belong to their parent and are not accounts of their own
            var users = context.Users.Where(u => u.BelongsToUserId == null);

            var recentUsers = await users
                .OrderByDescending(u => u.Metadata.Created)
                .ThenByDescending(u => u.Id)
                .Take(RecentUserCount)
                .Select(u => new AdminRecentUser
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    EmailConfirmed = u.EmailConfirmed,
                    RegisteredAt = u.Metadata.Created
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new AdminOverviewResult
            {
                ClubCount = await context.Clubs.CountAsync(cancellationToken),
                ClubsWithoutActiveSeason = await context.Clubs.CountAsync(c => !c.Seasons.Any(s => s.Period.From <= today && today <= s.Period.To), cancellationToken),
                UserCount = await users.CountAsync(cancellationToken),
                UnconfirmedUserCount = await users.CountAsync(u => !u.EmailConfirmed, cancellationToken),
                NewUsersLast30Days = await users.CountAsync(u => u.Metadata.Created >= sinceUtc, cancellationToken),
                BookingsLast30Days = await context.Bookings.CountAsync(b => b.Interval.From >= since && b.Interval.From <= now, cancellationToken),
                UpcomingBookings = await context.Bookings.CountAsync(b => b.Interval.From > now, cancellationToken),
                RecentUsers = recentUsers
            };
        }
    }
}
