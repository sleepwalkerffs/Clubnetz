using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Data;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.Wrapped;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Wrapped;

/// <summary>The seasons whose Wrapped the signed-in member can open: the released ones, for club admins every season that has started.</summary>
public record GetWrappedSeasons(int UserId, int ClubId, bool CanManage) : IQuery<GetWrappedSeasonsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetWrappedSeasons, GetWrappedSeasonsResult>
    {
        public async Task<GetWrappedSeasonsResult> Handle(GetWrappedSeasons request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var seasons = await context.Seasons
                .Where(s => s.ClubId == request.ClubId && s.Period.From <= today)
                .Where(s => request.CanManage || s.WrappedPublishedAt != null)
                .OrderByDescending(s => s.Period.From)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            List<DateOnly> ownMatchDates = [];
            if (memberId is { } id && seasons.Count > 0)
            {
                var period = new DateOnlyInterval(seasons.Min(s => s.Period.From), seasons.Max(s => s.Period.To));
                ownMatchDates = (await WrappedBookings.Load(context, request.ClubId, period, cancellationToken))
                    .Where(b => b.PlayerIds.Contains(id))
                    .Select(b => b.Date)
                    .ToList();
            }

            return new GetWrappedSeasonsResult
            {
                CanManage = request.CanManage,
                Seasons = seasons
                    .Select(s => new WrappedSeasonDto
                    {
                        SeasonId = s.Id,
                        StartDate = s.Period.From,
                        EndDate = s.Period.To,
                        IsActive = s.IsActiveOn(today),
                        PublishedAt = s.WrappedPublishedAt,
                        Matches = ownMatchDates.Count(d => s.Period.From <= d && d <= s.Period.To),
                    })
                    .ToList(),
            };
        }
    }
}
