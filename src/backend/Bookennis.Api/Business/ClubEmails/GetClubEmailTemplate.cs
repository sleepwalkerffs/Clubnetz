using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.ClubEmailTemplates;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ClubEmailType = Bookennis.Domain.Clubs.EmailTemplates.ClubEmailType;
using SharedClubEmailType = Bookennis.Shared.Controller.ClubEmailTemplates.ClubEmailType;
using SharedLanguage = Bookennis.Shared.Controller.Shared.Language;

namespace Bookennis.Api.Business.ClubEmails;

public record GetClubEmailTemplate(int ClubId, ClubEmailType Type) : IQuery<GetClubEmailTemplateResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubEmailTemplate, GetClubEmailTemplateResult>
    {
        public async Task<GetClubEmailTemplateResult> Handle(GetClubEmailTemplate request, CancellationToken cancellationToken)
        {
            var definition = ClubEmailCatalog.Get(request.Type);

            var club = await context.Clubs
                .Where(c => c.Id == request.ClubId)
                .Select(c => new ClubVariables(c.Name, c.WebsiteUrl, c.ReplyToEmail))
                .AsNoTracking()
                .SingleRequiredAsync(cancellationToken);

            var templates = await context.ClubEmailTemplates
                .Where(t => t.ClubId == request.ClubId && t.Type == request.Type)
                .Select(t => new { t.Language, t.Subject, t.Body })
                .ToListAsync(cancellationToken);

            return new GetClubEmailTemplateResult
            {
                Type = (SharedClubEmailType)request.Type,
                Languages = Enum.GetValues<Language>()
                    .Select(language =>
                    {
                        var custom = templates.FirstOrDefault(t => t.Language == language);
                        var defaultTemplate = ClubEmailDefaultTemplates.Get(request.Type, language);
                        return new ClubEmailTemplateLanguageDto
                        {
                            Language = (SharedLanguage)language,
                            IsCustomized = custom is not null,
                            Subject = custom?.Subject ?? defaultTemplate.Subject,
                            Body = custom?.Body ?? defaultTemplate.Body,
                            DefaultSubject = defaultTemplate.Subject,
                            DefaultBody = defaultTemplate.Body,
                            Variables = definition.GetVariables(language, club)
                                .Select(v => new ClubEmailVariableDto { Name = v.Name, SampleValue = v.SampleValue })
                                .ToList()
                        };
                    })
                    .ToList()
            };
        }
    }
}
