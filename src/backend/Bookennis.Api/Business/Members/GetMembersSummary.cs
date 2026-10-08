using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>Key figures shown above the members list.</summary>
public record GetMembersSummary : IQuery<GetMembersSummaryResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetMembersSummary, GetMembersSummaryResult>
    {
        public async Task<GetMembersSummaryResult> Handle(GetMembersSummary request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var activeSeason = await context.Seasons
                .Where(s => s.Period.From <= today && today <= s.Period.To)
                .OrderByDescending(s => s.Period.From)
                .FirstOrDefaultAsync(cancellationToken);

            // Without an active season the latest started season is the reference for the previous one
            var referenceStart = activeSeason?.Period.From ?? today.AddDays(1);
            var previousSeason = await context.Seasons
                .Where(s => s.Period.To < referenceStart)
                .OrderByDescending(s => s.Period.To)
                .FirstOrDefaultAsync(cancellationToken);

            var memberIds = context.ClubMembers.Select(m => m.Id);

            var activeMemberIds = activeSeason is null
                ? []
                : await context.MemberSeasons
                    .Where(ms => ms.SeasonId == activeSeason.Id && memberIds.Contains(ms.MemberId))
                    .Select(ms => ms.MemberId)
                    .ToHashSetAsync(cancellationToken);

            var previousMemberIds = previousSeason is null
                ? []
                : await context.MemberSeasons
                    .Where(ms => ms.SeasonId == previousSeason.Id && memberIds.Contains(ms.MemberId))
                    .Select(ms => ms.MemberId)
                    .ToHashSetAsync(cancellationToken);

            var withoutEmail = await (await MemberFilterQuery.Build(context, new MemberFilter { HasEmail = false }, cancellationToken))
                .CountAsync(cancellationToken);

            return new GetMembersSummaryResult
            {
                TotalMembers = await context.ClubMembers.CountAsync(cancellationToken),
                ActiveSeasonId = activeSeason?.Id,
                PreviousSeasonId = previousSeason?.Id,
                ActiveSeasonMembers = activeMemberIds.Count,
                NewMembers = activeSeason is null ? 0 : activeMemberIds.Count(id => !previousMemberIds.Contains(id)),
                LapsedMembers = previousMemberIds.Count(id => !activeMemberIds.Contains(id)),
                WithoutEmail = withoutEmail,
            };
        }
    }
}
