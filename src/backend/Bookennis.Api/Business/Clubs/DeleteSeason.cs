using Bookennis.Api.Data;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubs;

public record DeleteSeason(int SeasonId) : ICommand
{
    public enum ErrorCode
    {
        /// <summary>Membership fees were paid for the season. Its orders are the record of these payments.</summary>
        SeasonHasPayments = 0,
    }

    public class Handler(AppDbContext context) : IRequestHandler<DeleteSeason>
    {
        public async Task<Unit> Handle(DeleteSeason request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.Seasons)
                .SingleRequiredAsync(cancellationToken);

            var hasPayments = await context.MembershipOrders.AnyAsync(
                o => o.SeasonId == request.SeasonId && (o.Status == MembershipOrderStatus.Paid || o.Status == MembershipOrderStatus.Processing),
                cancellationToken);

            if (hasPayments)
                throw new PreconditionException(ErrorCode.SeasonHasPayments, "A season with paid membership fees can't be deleted.");

            // Orders nobody paid go with the season. They are removed first because they point to the plans of the season.
            var orders = await context.MembershipOrders.Where(o => o.SeasonId == request.SeasonId).ToListAsync(cancellationToken);
            context.MembershipOrders.RemoveRange(orders);

            club.RemoveSeason(request.SeasonId);

            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
