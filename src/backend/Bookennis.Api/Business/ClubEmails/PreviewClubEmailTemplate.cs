using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.ClubEmailTemplates;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ClubEmailType = Bookennis.Domain.Clubs.EmailTemplates.ClubEmailType;

namespace Bookennis.Api.Business.ClubEmails;

/// <summary>Renders a (not yet saved) template with sample data and the club's settings.</summary>
public record PreviewClubEmailTemplate(int ClubId, ClubEmailType Type, Language Language, string Subject, string Body) : IQuery<PreviewClubEmailTemplateResult>
{
    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<PreviewClubEmailTemplate, PreviewClubEmailTemplateResult>
    {
        public async Task<PreviewClubEmailTemplateResult> Handle(PreviewClubEmailTemplate request, CancellationToken cancellationToken)
        {
            var definition = ClubEmailCatalog.Get(request.Type);
            var template = new ClubEmailTemplateContent(request.Subject, request.Body);
            renderer.Validate(definition, template);

            var club = await context.Clubs
                .Where(c => c.Id == request.ClubId)
                .Select(c => new ClubVariables(c.Name, c.WebsiteUrl, c.ReplyToEmail))
                .AsNoTracking()
                .SingleRequiredAsync(cancellationToken);

            var rendered = await renderer.Render(template, definition.CreateSample(request.Language) with { Club = club });
            return new PreviewClubEmailTemplateResult { Subject = rendered.Subject, Html = rendered.BodyHtml };
        }
    }
}
