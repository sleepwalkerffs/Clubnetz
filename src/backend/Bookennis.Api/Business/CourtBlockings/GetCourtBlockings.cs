using Bookennis.Api.Data;
using Bookennis.Shared.Controller.CourtBlockings;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtBlockings;

/// <summary>The court blockings of the club for the management page: running and upcoming ones first, then (optionally) the ones that are over.</summary>
public record GetCourtBlockings(int ClubId, bool IncludePast) : IQuery<GetCourtBlockingsResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCourtBlockings, GetCourtBlockingsResult>
    {
        public async Task<GetCourtBlockingsResult> Handle(GetCourtBlockings request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;

            var blockings = await context.CourtBlockings
                .Where(b => b.ClubId == request.ClubId && (request.IncludePast || b.Occurrences.Any(o => o.Interval.To > now)))
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    CourtIds = b.Courts.Select(c => c.CourtId).ToList(),
                    b.StartDate,
                    b.EndDate,
                    b.StartTime,
                    b.EndTime,
                    b.RecurrenceIntervalWeeks,
                    b.RecurrenceEndDate,
                    OccurrenceCount = b.Occurrences.Count,
                    NextOccurrence = b.Occurrences
                        .Where(o => o.Interval.To > now)
                        .OrderBy(o => o.Interval.From)
                        .Select(o => (DateTimeOffset?)o.Interval.From)
                        .FirstOrDefault(),
                })
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var upcoming = blockings.Where(b => b.NextOccurrence is not null).OrderBy(b => b.NextOccurrence).ThenBy(b => b.Title);
            var past = blockings.Where(b => b.NextOccurrence is null).OrderByDescending(b => b.RecurrenceEndDate ?? b.EndDate).ThenBy(b => b.Title);

            return new GetCourtBlockingsResult
            {
                Blockings = upcoming.Concat(past).Select(b => new CourtBlockingDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    CourtIds = b.CourtIds,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    StartTime = b.StartTime?.ToTimeSpan(),
                    EndTime = b.EndTime?.ToTimeSpan(),
                    RecurrenceIntervalWeeks = b.RecurrenceIntervalWeeks,
                    RecurrenceEndDate = b.RecurrenceEndDate,
                    OccurrenceCount = b.OccurrenceCount,
                    NextOccurrence = b.NextOccurrence,
                }).ToList(),
            };
        }
    }
}
