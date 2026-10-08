using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEmails;

/// <summary>
/// Saves the club's templates of one email type. Languages that are not part of <paramref name="Languages"/>
/// fall back to the default template.
/// </summary>
public record UpdateClubEmailTemplate(int ClubId, ClubEmailType Type, IReadOnlyList<UpdateClubEmailTemplate.LanguageContent> Languages) : ICommand
{
    public record LanguageContent(Language Language, string Subject, string Body);

    public enum ErrorCode
    {
        DuplicateEmailTemplateLanguage = 0
    }

    public class Handler(AppDbContext context, IClubEmailRenderer renderer) : IRequestHandler<UpdateClubEmailTemplate>
    {
        public async Task<Unit> Handle(UpdateClubEmailTemplate request, CancellationToken cancellationToken)
        {
            if (request.Languages.GroupBy(l => l.Language).Any(g => g.Count() > 1))
                throw new PreconditionException(ErrorCode.DuplicateEmailTemplateLanguage, "Every language can only be set once.");

            var definition = ClubEmailCatalog.Get(request.Type);
            foreach (var content in request.Languages)
            {
                var template = new ClubEmailTemplateContent(content.Subject, content.Body);
                renderer.Validate(definition, template);
                // Catches errors that only show up when rendering (e.g. invalid filter arguments).
                await renderer.Render(template, definition.CreateSample(content.Language));
            }

            var existing = await context.ClubEmailTemplates
                .Where(t => t.ClubId == request.ClubId && t.Type == request.Type)
                .ToListAsync(cancellationToken);

            foreach (var template in existing.Where(t => request.Languages.All(l => l.Language != t.Language)))
                context.ClubEmailTemplates.Remove(template);

            foreach (var content in request.Languages)
            {
                var template = existing.FirstOrDefault(t => t.Language == content.Language);
                if (template is null)
                    context.ClubEmailTemplates.Add(new ClubEmailTemplate(request.ClubId, request.Type, content.Language, content.Subject, content.Body));
                else
                    template.Update(content.Subject, content.Body);
            }

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
