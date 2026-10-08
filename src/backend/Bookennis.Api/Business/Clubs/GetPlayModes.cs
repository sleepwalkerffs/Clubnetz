using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Booking.Shared;
using Bookennis.Shared.Controller.Club;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record GetPlayModes() : ICommand<GetPlayModesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPlayModes, GetPlayModesResult>
    {
        public async Task<GetPlayModesResult> Handle(GetPlayModes request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.Include(x => x.PlayModes).SingleRequiredAsync(cancellationToken);
            return new GetPlayModesResult
            {
                PlayModes = club.PlayModes.OrderBy(x => x.Id).Select(x => new PlayModeDto()
                {
                    Id = x.Id,
                    AllowedRoles = x.AllowedRoles.Select(role => (MemberRole)role).ToList(),
                    Color = x.Color.ToArgb(),
                    FixedDuration = x.FixedDuration,
                    FixedPlayerCount = x.FixedPlayerCount,
                    IsChargingBookingSubscription = x.IsChargingBookingSubscription,
                    Name = x.Name,
                    CanOverbook = x.CanOverbook,
                    CommentAllowed = x.CommentAllowed,
                    MaxBookingsPerSeason = x.MaxBookingsPerSeason,
                    AllowRecurring = x.AllowRecurring,
                }).ToList()
            };
        }
    }
}
