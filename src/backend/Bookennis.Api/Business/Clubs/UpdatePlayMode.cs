using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record UpdatePlayMode(int PlayModeId, string Name, MemberRole[] AllowedRoles, int ColorArgb, int? FixedPlayerCount, bool IsChargingBookingSubscription, TimeSpan? FixedDuration, bool CanOverbook, bool CommentAllowed, int? MaxBookingsPerSeason = null, bool AllowRecurring = false) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdatePlayMode>
    {
        public async Task<Unit> Handle(UpdatePlayMode request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.Include(c => c.PlayModes).SingleRequiredAsync(cancellationToken);

            club.UpdatePlayMode(request.PlayModeId, new Club.PlayModeDto(
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
