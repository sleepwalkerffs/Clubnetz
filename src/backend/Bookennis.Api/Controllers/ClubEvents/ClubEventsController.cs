using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.ClubEvents;
using Bookennis.Shared.Controller.ClubEvents;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.ClubEvents;

/// <summary>
/// Club calendar. Club members (not guests) can read events and register;
/// Maintainers, SportsDirectors, YouthSportsDirectors and Admins plan the events.
/// </summary>
public class ClubEventsController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public Task<GetClubEventsResult> GetClubEvents(int clubId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => mediator.Send(new GetClubEvents(clubId, userAccessor.GetUserId(), from, to), cancellationToken);

    [HttpGet("{clubEventId:int}")]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public Task<ClubEventDto> GetClubEvent(int clubId, int clubEventId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubEvent(clubId, userAccessor.GetUserId(), clubEventId), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubEventManager)]
    public Task<int> CreateClubEvent(int clubId, [FromBody] SaveClubEventModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateClubEvent(clubId, userAccessor.GetUserId(), ClubEventMapper.ToEventData(model)), cancellationToken);

    [HttpPut("{clubEventId:int}")]
    [Authorize(AuthorizationPolicies.ClubEventManager)]
    public Task<ClubEventDto> UpdateClubEvent(int clubId, int clubEventId, [FromBody] SaveClubEventModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateClubEvent(clubId, userAccessor.GetUserId(), clubEventId, ClubEventMapper.ToEventData(model)), cancellationToken);

    [HttpPost("preview")]
    [Authorize(AuthorizationPolicies.ClubEventManager)]
    public Task<PreviewClubEventDescriptionResult> PreviewClubEventDescription([FromBody] PreviewClubEventDescriptionModel model, CancellationToken cancellationToken)
        => mediator.Send(new PreviewClubEventDescription(model.Description ?? ""), cancellationToken);

    [HttpDelete("{clubEventId:int}")]
    [Authorize(AuthorizationPolicies.ClubEventManager)]
    public Task DeleteClubEvent(int clubId, int clubEventId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteClubEvent(clubId, clubEventId), cancellationToken);

    [HttpPut("{clubEventId:int}/registration")]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public Task<ClubEventDto> RegisterForClubEvent(int clubId, int clubEventId, [FromBody] RegisterForClubEventModel model, CancellationToken cancellationToken)
        => mediator.Send(new RegisterForClubEvent(
            clubId,
            userAccessor.GetUserId(),
            clubEventId,
            model.HeadCount,
            model.Comment,
            model.Answers.Select(a => new ClubEvent.AnswerData(a.OptionId, a.Quantity)).ToList()), cancellationToken);

    [HttpDelete("{clubEventId:int}/registration")]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public Task<ClubEventDto> UnregisterFromClubEvent(int clubId, int clubEventId, CancellationToken cancellationToken)
        => mediator.Send(new UnregisterFromClubEvent(clubId, userAccessor.GetUserId(), clubEventId), cancellationToken);

    [HttpDelete("{clubEventId:int}/registrations/{registrationId:int}")]
    [Authorize(AuthorizationPolicies.ClubEventManager)]
    public Task<ClubEventDto> RemoveClubEventRegistration(int clubId, int clubEventId, int registrationId, CancellationToken cancellationToken)
        => mediator.Send(new RemoveClubEventRegistration(clubId, userAccessor.GetUserId(), clubEventId, registrationId), cancellationToken);

    [HttpGet("{clubEventId:int}/export")]
    [Authorize(AuthorizationPolicies.ClubEventManager)]
    public async Task<IActionResult> ExportClubEventParticipants(int clubId, int clubEventId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportClubEventParticipants(clubId, userAccessor.GetUserId(), clubEventId), cancellationToken);
        return File(result.Data, ExportClubEventParticipantsResult.ContentType, result.FileName);
    }
}
