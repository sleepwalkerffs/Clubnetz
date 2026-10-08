using Bookennis.Api.Data;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Leaderboards;

public record ToggleLeaderboardOptOut(int UserId, int SeasonId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<ToggleLeaderboardOptOut, Unit>
    {
        public async Task<Unit> Handle(ToggleLeaderboardOptOut request, CancellationToken cancellationToken)
        {
            var memberIds = await context.Set<Member>()
                .IgnoreQueryFilters()
                .Where(m => m.UserId == request.UserId)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            var memberSeason = await context.MemberSeasons
                .FirstOrDefaultAsync(ms => memberIds.Contains(ms.MemberId) && ms.SeasonId == request.SeasonId, cancellationToken);

            if (memberSeason is null)
                return Unit.Value;

            memberSeason.LeaderboardOptOut = !memberSeason.LeaderboardOptOut;

            await context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
