using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record AdminAddPlayMode(int ClubId, string Name, MemberRole[] AllowedRoles, int ColorArgb, int? FixedPlayerCount, bool IsChargingBookingSubscription, TimeSpan? FixedDuration, bool CanOverbook, bool CommentAllowed, int? MaxBookingsPerSeason = null, bool AllowRecurring = false) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<AdminAddPlayMode>
    {
        public async Task<Unit> Handle(AdminAddPlayMode request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.Include(c => c.PlayModes).SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Club), request.ClubId);

            club.AddPlayMode(new Club.PlayModeDto(
                request.AllowedRoles,
                System.Drawing.Color.FromArgb(request.ColorArgb),
                request.FixedPlayerCount,
                request.IsChargingBookingSubscription,
                request.Name,
                request.FixedDuration,
                request.CanOverbook,
                request.CommentAllowed,
                request.MaxBookingsPerSeason,
                request.AllowRecurring));

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
