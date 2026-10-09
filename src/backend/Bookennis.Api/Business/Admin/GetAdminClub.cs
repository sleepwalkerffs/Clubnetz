using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record GetAdminClub(int ClubId) : IQuery<AdminClubDetailResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAdminClub, AdminClubDetailResult>
    {
        public async Task<AdminClubDetailResult> Handle(GetAdminClub request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var club = await context.Clubs
                .Include(c => c.PlayModes)
                .Include(c => c.Seasons)
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken) ?? throw new EntityNotFoundException(typeof(Domain.Clubs.Club), request.ClubId);

            var now = DateTimeOffset.UtcNow;
            var since = now.AddDays(-AdminClubQueryExtensions.ActivityDays);
            var admins = await context.GetClubAdmins(club.Id, cancellationToken);

            return new AdminClubDetailResult
            {
                MemberCount = await context.ClubMembers.CountAsync(m => m.ClubId == club.Id, cancellationToken),
                GuestCount = await context.GuestMembers.CountAsync(m => m.ClubId == club.Id, cancellationToken),
                CourtCount = await context.Courts.CountAsync(c => c.ClubId == club.Id, cancellationToken),
                BookingsLast30Days = await context.Bookings.CountAsync(b => b.ClubId == club.Id && b.Interval.From >= since && b.Interval.From <= now, cancellationToken),
                CreatedAt = club.Metadata.Created,
                Admins = admins.GetValueOrDefault(club.Id) ?? [],
                Id = club.Id,
                Name = club.Name,
                OpeningHours = club.OpeningHours,
                PrimeTimeSettings = new PrimeTimeSettingsDto
                {
                    IsEnabled = club.PrimeTimeSettings.IsEnabled,
                    PrimeTimeHours = club.PrimeTimeSettings.PrimeTimeHours,
                    ApplicableWeekdays = club.PrimeTimeSettings.ApplicableWeekdays,
                    RestrictChildren = club.PrimeTimeSettings.RestrictChildren,
                    RestrictGuests = club.PrimeTimeSettings.RestrictGuests,
                    ChildAgeThreshold = club.PrimeTimeSettings.ChildAgeThreshold
                },
                BookingGracePeriodInMinutes = club.BookingGracePeriodInMinutes,
                ConcurrentAllowedBookings = club.ConcurrentAllowedBookings,
                IsAtpClub = club.IsAtpClub,
                PlayModes = club.PlayModes.OrderBy(p => p.Id).Select(p => new PlayModeDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    AllowedRoles = p.AllowedRoles.Select(r => (MemberRole)r).ToList(),
                    Color = p.Color.ToArgb(),
                    FixedPlayerCount = p.FixedPlayerCount,
                    IsChargingBookingSubscription = p.IsChargingBookingSubscription,
                    FixedDuration = p.FixedDuration,
                    CanOverbook = p.CanOverbook,
                    CommentAllowed = p.CommentAllowed,
                    MaxBookingsPerSeason = p.MaxBookingsPerSeason,
                    AllowRecurring = p.AllowRecurring
                }).ToList(),
                Seasons = club.Seasons.OrderByDescending(s => s.Period.From).Select(s => new AdminSeasonResult
                {
                    Id = s.Id,
                    ClubId = s.ClubId,
                    StartDate = s.Period.From,
                    EndDate = s.Period.To,
                    IsActive = s.Period.From <= today && today <= s.Period.To
                }).ToList()
            };
        }
    }
}
