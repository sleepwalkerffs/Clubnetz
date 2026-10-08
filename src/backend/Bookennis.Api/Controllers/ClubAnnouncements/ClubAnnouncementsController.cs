using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.ClubAnnouncements;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClubAnnouncementAudience = Bookennis.Domain.ClubAnnouncements.ClubAnnouncementAudience;

namespace Bookennis.Api.Controllers.ClubAnnouncements;

/// <summary>
/// Club news. Club members (not guests) read the announcements;
/// SportsDirectors, YouthSportsDirectors and Admins write them and send them as email.
/// </summary>
public class ClubAnnouncementsController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    [HttpGet]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public async Task<GetClubAnnouncementsResult> GetClubAnnouncements(int clubId, [FromQuery] bool includeExpired, [FromQuery] int? take, CancellationToken cancellationToken)
        => await mediator.Send(new GetClubAnnouncements(clubId, await CanManage(), includeExpired, take), cancellationToken);

    [HttpGet("{clubAnnouncementId:int}")]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public async Task<ClubAnnouncementDto> GetClubAnnouncement(int clubId, int clubAnnouncementId, CancellationToken cancellationToken)
        => await mediator.Send(new GetClubAnnouncement(clubId, clubAnnouncementId, await CanManage()), cancellationToken);

    [HttpPost]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<int> CreateClubAnnouncement(int clubId, [FromBody] SaveClubAnnouncementModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateClubAnnouncement(clubId, userAccessor.GetUserId(), ClubAnnouncementMapper.ToData(model)), cancellationToken);

    [HttpPut("{clubAnnouncementId:int}")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<ClubAnnouncementDto> UpdateClubAnnouncement(int clubId, int clubAnnouncementId, [FromBody] SaveClubAnnouncementModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateClubAnnouncement(clubId, clubAnnouncementId, ClubAnnouncementMapper.ToData(model)), cancellationToken);

    [HttpDelete("{clubAnnouncementId:int}")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task DeleteClubAnnouncement(int clubId, int clubAnnouncementId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteClubAnnouncement(clubId, clubAnnouncementId), cancellationToken);

    [HttpPost("preview")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<PreviewClubAnnouncementResult> PreviewClubAnnouncement([FromBody] PreviewClubAnnouncementModel model, CancellationToken cancellationToken)
        => mediator.Send(new PreviewClubAnnouncement(model.Body ?? ""), cancellationToken);

    [HttpPost("{clubAnnouncementId:int}/attachments")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<ClubAnnouncementDto> AddClubAnnouncementAttachment(int clubId, int clubAnnouncementId, IFormFile file, CancellationToken cancellationToken)
        => mediator.Send(new AddClubAnnouncementAttachment(clubId, clubAnnouncementId, file), cancellationToken);

    [HttpDelete("{clubAnnouncementId:int}/attachments/{attachmentId:int}")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<ClubAnnouncementDto> RemoveClubAnnouncementAttachment(int clubId, int clubAnnouncementId, int attachmentId, CancellationToken cancellationToken)
        => mediator.Send(new RemoveClubAnnouncementAttachment(clubId, clubAnnouncementId, attachmentId), cancellationToken);

    // Always served as a download (never inline), the files are uploaded by club members
    [HttpGet("{clubAnnouncementId:int}/attachments/{attachmentId:int}")]
    [Authorize(AuthorizationPolicies.ClubMember)]
    public async Task<IActionResult> GetClubAnnouncementAttachment(int clubId, int clubAnnouncementId, int attachmentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetClubAnnouncementAttachment(clubId, clubAnnouncementId, attachmentId, await CanManage()), cancellationToken);
        return File(result.Data, result.ContentType, result.FileName);
    }

    [HttpPost("email-recipients")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<ClubAnnouncementEmailRecipientsResult> GetClubAnnouncementEmailRecipients(int clubId, [FromBody] SendClubAnnouncementEmailModel model, CancellationToken cancellationToken)
        => mediator.Send(new GetClubAnnouncementEmailRecipients(clubId, (ClubAnnouncementAudience)model.Audience, ToRoles(model)), cancellationToken);

    [HttpPost("{clubAnnouncementId:int}/email")]
    [Authorize(AuthorizationPolicies.ClubAnnouncementManager)]
    public Task<ClubAnnouncementDto> SendClubAnnouncementEmail(int clubId, int clubAnnouncementId, [FromBody] SendClubAnnouncementEmailModel model, CancellationToken cancellationToken)
        => mediator.Send(new SendClubAnnouncementEmail(clubId, clubAnnouncementId, (ClubAnnouncementAudience)model.Audience, ToRoles(model)), cancellationToken);

    /// <summary>Members who manage the announcements also see expired ones and the email details.</summary>
    private async Task<bool> CanManage()
        => (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ClubAnnouncementManager)).Succeeded;

    private static List<MemberRole> ToRoles(SendClubAnnouncementEmailModel model)
        => model.Roles.Select(r => (MemberRole)(int)r).ToList();
}
