using Bookennis.Api.Data;
using Bookennis.Shared.Controller.ClubEmailTemplates;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ClubEmailType = Bookennis.Shared.Controller.ClubEmailTemplates.ClubEmailType;
using Language = Bookennis.Shared.Controller.Shared.Language;

namespace Bookennis.Api.Business.ClubEmails;

public record GetClubEmailTemplates(int ClubId) : IQuery<GetClubEmailTemplatesResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetClubEmailTemplates, GetClubEmailTemplatesResult>
    {
        public async Task<GetClubEmailTemplatesResult> Handle(GetClubEmailTemplates request, CancellationToken cancellationToken)
        {
            var settings = await context.Clubs
                .Where(c => c.Id == request.ClubId)
                .Select(c => new ClubEmailSettingsDto { WebsiteUrl = c.WebsiteUrl, ReplyToEmail = c.ReplyToEmail })
                .AsNoTracking()
                .SingleRequiredAsync(cancellationToken);

            var customized = await context.ClubEmailTemplates
                .Where(t => t.ClubId == request.ClubId)
                .Select(t => new { t.Type, t.Language })
                .ToListAsync(cancellationToken);

            return new GetClubEmailTemplatesResult
            {
                Settings = settings,
                Templates = ClubEmailCatalog.All
                    .Select(d => new ClubEmailTemplateSummaryDto
                    {
                        Type = (ClubEmailType)d.Type,
                        CustomizedLanguages = customized
                            .Where(c => c.Type == d.Type)
                            .Select(c => (Language)c.Language)
                            .OrderBy(l => l)
                            .ToList()
                    })
                    .ToList()
            };
        }
    }
}
