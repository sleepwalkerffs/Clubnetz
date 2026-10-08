using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Data;
using Bookennis.Domain.ClubEvents;
using Bookennis.Shared.Controller.ClubEvents;

namespace Bookennis.Api.Business.ClubEvents;

/// <summary>Creates the current member's registration or replaces an existing one.</summary>
public record RegisterForClubEvent(int ClubId, int UserId, int ClubEventId, int HeadCount, string? Comment, IReadOnlyCollection<ClubEvent.AnswerData> Answers) : ICommand<ClubEventDto>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<RegisterForClubEvent, ClubEventDto>
    {
        public async Task<ClubEventDto> Handle(RegisterForClubEvent request, CancellationToken cancellationToken)
        {
            var memberId = await context.GetRequiredClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var clubEvent = await context.ClubEvents.GetEventWithDetails(request.ClubId, request.ClubEventId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            clubEvent.Register(memberId, request.HeadCount, request.Answers, request.Comment, now);

            await context.SaveChangesAsync(cancellationToken);

            var members = await ClubEventMapper.LoadMemberInfos(context, clubEvent, cancellationToken);
            return ClubEventMapper.ToDto(renderer, clubEvent, members, memberId, now);
        }
    }
}
