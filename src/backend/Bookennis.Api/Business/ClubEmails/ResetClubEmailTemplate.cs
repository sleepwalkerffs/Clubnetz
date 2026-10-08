using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.EmailTemplates;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.ClubEmails;

/// <summary>Removes the club's templates of one email type, so the default template is used again.</summary>
public record ResetClubEmailTemplate(int ClubId, ClubEmailType Type) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<ResetClubEmailTemplate>
    {
        public async Task<Unit> Handle(ResetClubEmailTemplate request, CancellationToken cancellationToken)
        {
            var templates = await context.ClubEmailTemplates
                .Where(t => t.ClubId == request.ClubId && t.Type == request.Type)
                .ToListAsync(cancellationToken);

            context.ClubEmailTemplates.RemoveRange(templates);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
