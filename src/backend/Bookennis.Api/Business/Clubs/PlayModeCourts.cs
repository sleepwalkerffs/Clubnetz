using Bookennis.Api.Data;
using Bookennis.Domain.Courts;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

/// <summary>Checks for the courts a play mode is limited to (<see cref="Domain.Clubs.PlayMode.CourtIds"/>).</summary>
internal static class PlayModeCourts
{
    /// <exception cref="EntityNotFoundException">A court does not exist in the club.</exception>
    public static async Task EnsureCourtsOfClub(AppDbContext context, int clubId, IReadOnlyCollection<int>? courtIds, CancellationToken cancellationToken)
    {
        if (courtIds is null || courtIds.Count == 0)
            return;

        var distinctIds = courtIds.Distinct().ToList();
        var found = await context.Courts.CountAsync(c => c.ClubId == clubId && distinctIds.Contains(c.Id), cancellationToken);
        if (found != distinctIds.Count)
            throw new EntityNotFoundException(typeof(Court));
    }
}
