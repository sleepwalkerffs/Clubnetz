using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Badges;

public record CreateOneTimeBadge(int ClubId, int SeasonId, string Name, string Description) : ICommand<int>
{
    public enum ErrorCode
    {
        NameAlreadyUsedInSeason = 0
    }

    public class Handler(AppDbContext context) : IRequestHandler<CreateOneTimeBadge, int>
    {
        public async Task<int> Handle(CreateOneTimeBadge request, CancellationToken cancellationToken)
        {
            var nameAlreadyUsed = await context.OneTimeBadges
                .AnyAsync(b => b.ClubId == request.ClubId && b.SeasonId == request.SeasonId && b.Name == request.Name, cancellationToken);

            if (nameAlreadyUsed)
                throw new PreconditionException(ErrorCode.NameAlreadyUsedInSeason, "A one-time badge with this name already exists for the selected season.");

            var badge = new OneTimeBadge(request.ClubId, request.SeasonId, request.Name, request.Description);
            context.OneTimeBadges.Add(badge);

            await context.SaveChangesAsync(cancellationToken);

            return badge.Id;
        }
    }
}
