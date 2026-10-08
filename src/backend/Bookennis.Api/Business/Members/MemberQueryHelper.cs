using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

public static class MemberQueryHelper
{
    public static async Task<HashSet<int>> GetNewMemberIds(AppDbContext context, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activeSeason = await context.Seasons
            .Where(s => s.Period.From <= today && today <= s.Period.To)
            .OrderByDescending(s => s.Period.From)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSeason is null)
            return [];

        var activeSeasonMemberIds = await context.MemberSeasons
            .Where(ms => ms.SeasonId == activeSeason.Id)
            .Select(ms => ms.MemberId)
            .ToListAsync(cancellationToken);

        var previousSeason = await context.Seasons
            .Where(s => s.Period.To < activeSeason.Period.From)
            .OrderByDescending(s => s.Period.To)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousSeason is null)
            return activeSeasonMemberIds.ToHashSet();

        var previousSeasonMemberIds = await context.MemberSeasons
            .Where(ms => ms.SeasonId == previousSeason.Id)
            .Select(ms => ms.MemberId)
            .ToHashSetAsync(cancellationToken);

        return activeSeasonMemberIds.Where(id => !previousSeasonMemberIds.Contains(id)).ToHashSet();
    }
}
