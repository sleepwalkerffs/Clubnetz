using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.ClubEmailTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClubEmailType = Bookennis.Shared.Controller.ClubEmailTemplates.ClubEmailType;
using DomainClubEmailType = Bookennis.Domain.Clubs.EmailTemplates.ClubEmailType;
using DomainLanguage = Bookennis.Domain.User.Language;

namespace Bookennis.Api.Controllers.Clubs;

[Authorize(AuthorizationPolicies.ClubAdministrator)]
public class ClubEmailTemplatesController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public Task<GetClubEmailTemplatesResult> GetClubEmailTemplates(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubEmailTemplates(clubId), cancellationToken);

    [HttpPut("settings")]
    public Task UpdateClubEmailSettings(int clubId, [FromBody] UpdateClubEmailSettingsModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateClubEmailSettings(clubId, model.WebsiteUrl, model.ReplyToEmail), cancellationToken);

    [HttpGet("{type}")]
    public Task<GetClubEmailTemplateResult> GetClubEmailTemplate(int clubId, ClubEmailType type, CancellationToken cancellationToken)
        => mediator.Send(new GetClubEmailTemplate(clubId, (DomainClubEmailType)type), cancellationToken);

    [HttpPut("{type}")]
    public Task UpdateClubEmailTemplate(int clubId, ClubEmailType type, [FromBody] UpdateClubEmailTemplateModel model, CancellationToken cancellationToken)
        => mediator.Send(
            new UpdateClubEmailTemplate(
                clubId,
                (DomainClubEmailType)type,
                model.Languages.ConvertAll(l => new UpdateClubEmailTemplate.LanguageContent((DomainLanguage)l.Language, l.Subject, l.Body))),
            cancellationToken);

    [HttpDelete("{type}")]
    public Task ResetClubEmailTemplate(int clubId, ClubEmailType type, CancellationToken cancellationToken)
        => mediator.Send(new ResetClubEmailTemplate(clubId, (DomainClubEmailType)type), cancellationToken);

    [HttpPost("{type}/preview")]
    public Task<PreviewClubEmailTemplateResult> PreviewClubEmailTemplate(int clubId, ClubEmailType type, [FromBody] ClubEmailTemplateContentModel model, CancellationToken cancellationToken)
        => mediator.Send(new PreviewClubEmailTemplate(clubId, (DomainClubEmailType)type, (DomainLanguage)model.Language, model.Subject, model.Body), cancellationToken);

    [HttpPost("{type}/test")]
    public Task SendTestClubEmail(int clubId, ClubEmailType type, [FromBody] ClubEmailTemplateContentModel model, CancellationToken cancellationToken)
        => mediator.Send(new SendTestClubEmail(clubId, (DomainClubEmailType)type, (DomainLanguage)model.Language, model.Subject, model.Body), cancellationToken);
}
