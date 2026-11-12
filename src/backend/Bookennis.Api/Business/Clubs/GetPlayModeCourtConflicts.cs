using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Club;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

/// <summary>
/// How many upcoming bookings of a play mode are on courts it would no longer be allowed on, so the admin is warned before
/// limiting the play mode. Those bookings are kept; the limit only applies to new bookings and changes.
/// </summary>
public record GetPlayModeCourtConflicts(int ClubId, int PlayModeId, int[] CourtIds) : ICommand<PlayModeCourtConflictsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetPlayModeCourtConflicts, PlayModeCourtConflictsResult>
    {
        public async Task<PlayModeCourtConflictsResult> Handle(GetPlayModeCourtConflicts request, CancellationToken cancellationToken)
        {
            if (request.CourtIds.Length == 0)
                return new PlayModeCourtConflictsResult { UpcomingBookingsOnOtherCourts = 0 };

            var now = DateTimeOffset.UtcNow;
            var count = await context.Bookings.CountAsync(
                b => b.ClubId == request.ClubId
                    && b.PlayModeId == request.PlayModeId
                    && b.Interval.From > now
                    && !request.CourtIds.Contains(b.CourtId),
                cancellationToken);

            return new PlayModeCourtConflictsResult { UpcomingBookingsOnOtherCourts = count };
        }
    }
}
