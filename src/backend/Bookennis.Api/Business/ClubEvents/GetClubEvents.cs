using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubEvents;
using Microsoft.EntityFrameworkCore;
using ClubEventCategory = Bookennis.Shared.Controller.ClubEvents.ClubEventCategory;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Events overlapping the date range [From, To], ordered chronologically.</summary>
public record GetClubEvents(int ClubId, int UserId, DateOnly From, DateOnly To) : IQuery<GetClubEventsResult>
{
    public const int MaxRangeInDays = 800;

    public class Handler(AppDbContext context) : IRequestHandler<GetClubEvents, GetClubEventsResult>
    {
        public async Task<GetClubEventsResult> Handle(GetClubEvents request, CancellationToken cancellationToken)
        {
            var from = request.From;
            var to = request.To.DayNumber - from.DayNumber > MaxRangeInDays ? from.AddDays(MaxRangeInDays) : request.To;
            var myMemberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var events = await context.ClubEvents
                .Where(e => e.ClubId == request.ClubId && e.StartDate <= to && (e.EndDate ?? e.StartDate) >= from)
                .OrderBy(e => e.StartDate)
                .ThenBy(e => e.StartTime)
                .ThenBy(e => e.Title)
                .Select(e => new ClubEventSummaryDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    Location = e.Location,
                    Category = (ClubEventCategory)e.Category,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    RegistrationEnabled = e.RegistrationEnabled,
                    MaxParticipants = e.MaxParticipants,
                    RegistrationDeadline = e.RegistrationDeadline,
                    TotalHeadCount = e.Registrations.Sum(r => r.HeadCount),
                    RegistrationCount = e.Registrations.Count,
                    MyHeadCount = e.Registrations.Where(r => r.MemberId == myMemberId).Select(r => (int?)r.HeadCount).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            return new GetClubEventsResult { Events = events };
        }
    }
}
