using Bookennis.Api.Data;
using Bookennis.Shared.Controller.CourtBlockings;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtBlockings;

/// <summary>
/// The blocked time windows of the days [DayFrom, DayTo] for the booking grid.
/// The range is extended by a day on both sides, because the days of the client are in its local time.
/// </summary>
public record GetCourtBlockingOccurrences(int ClubId, DateOnly DayFrom, DateOnly DayTo) : IQuery<GetCourtBlockingOccurrencesResult>
{
    public const int MaxRangeInDays = 100;

    public class Handler(AppDbContext context) : IRequestHandler<GetCourtBlockingOccurrences, GetCourtBlockingOccurrencesResult>
    {
        public async Task<GetCourtBlockingOccurrencesResult> Handle(GetCourtBlockingOccurrences request, CancellationToken cancellationToken)
        {
            var dayTo = request.DayTo.DayNumber - request.DayFrom.DayNumber > MaxRangeInDays ? request.DayFrom.AddDays(MaxRangeInDays) : request.DayTo;
            var rangeFrom = new DateTimeOffset(request.DayFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
            var rangeTo = new DateTimeOffset(dayTo.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(2);

            var occurrences = await (
                from blocking in context.CourtBlockings
                from occurrence in blocking.Occurrences
                where blocking.ClubId == request.ClubId && occurrence.Interval.From < rangeTo && occurrence.Interval.To > rangeFrom
                orderby occurrence.Interval.From, blocking.Title
                select new CourtBlockingOccurrenceDto
                {
                    CourtBlockingId = blocking.Id,
                    Title = blocking.Title,
                    CourtIds = blocking.Courts.Select(c => c.CourtId).ToList(),
                    Interval = occurrence.Interval,
                    IsRecurring = blocking.RecurrenceIntervalWeeks != null,
                }
            )
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return new GetCourtBlockingOccurrencesResult { Occurrences = occurrences };
        }
    }
}
