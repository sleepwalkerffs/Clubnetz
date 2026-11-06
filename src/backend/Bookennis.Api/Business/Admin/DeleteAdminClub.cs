using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record DeleteAdminClub(int ClubId) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<DeleteAdminClub>
    {
        public async Task<Unit> Handle(DeleteAdminClub request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs
                .Include(c => c.PlayModes)
                .SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.Clubs.Club), request.ClubId);

            // The books first: entries keep their categories and accounts from being deleted, also by the cascade of the club
            await context.BankTransactions.IgnoreQueryFilters().Where(t => t.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.BankStatementImports.IgnoreQueryFilters().Where(i => i.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.BankRules.IgnoreQueryFilters().Where(r => r.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.BankCsvLayouts.IgnoreQueryFilters().Where(l => l.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.AccountingProposals.IgnoreQueryFilters().Where(p => p.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.AccountingSourceSettings.IgnoreQueryFilters().Where(s => s.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.AccountingSettings.IgnoreQueryFilters().Where(s => s.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.AccountingReceipts.IgnoreQueryFilters().Where(r => r.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.AccountingEntries.IgnoreQueryFilters().Where(e => e.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);
            await context.FiscalYears.IgnoreQueryFilters().Where(y => y.ClubId == request.ClubId).ExecuteDeleteAsync(cancellationToken);

            // Remove associated members
            var members = await context.ClubMembers.Where(m => m.ClubId == request.ClubId).ToListAsync(cancellationToken);
            context.ClubMembers.RemoveRange(members);

            context.Clubs.Remove(club);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }
    }
}
