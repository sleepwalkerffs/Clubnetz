using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEmails;

public static class ClubEmailTemplateResolver
{
    /// <summary>
    /// The template to send: the club's template in the requested language, then the club's template in the
    /// other language, then the built-in default in the requested language.
    /// </summary>
    public static async Task<ClubEmailTemplateContent> Resolve(AppDbContext context, int clubId, ClubEmailType type, Language language, CancellationToken cancellationToken)
    {
        var clubTemplates = await context.ClubEmailTemplates
            .IgnoreQueryFilters()
            .Where(t => t.ClubId == clubId && t.Type == type)
            .Select(t => new { t.Language, t.Subject, t.Body })
            .ToListAsync(cancellationToken);

        var template = clubTemplates.FirstOrDefault(t => t.Language == language) ?? clubTemplates.FirstOrDefault();
        return template is not null
            ? new ClubEmailTemplateContent(template.Subject, template.Body)
            : ClubEmailDefaultTemplates.Get(type, language);
    }
}
