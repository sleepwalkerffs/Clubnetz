using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin;

public record UpdateAdminClub(int ClubId, string Name, TimeOnlyInterval OpeningHours, TimeOnlyInterval PrimeTimeHours, PrimeTimeSettings PrimeTimeSettings, int? BookingGracePeriodInMinutes, bool IsAtpClub, Domain.User.Country? Country = null, string? TimeZoneId = null, string? Currency = null) : ICommand
{
    public class Handler(AppDbContext context) : IRequestHandler<UpdateAdminClub>
    {
        public async Task<Unit> Handle(UpdateAdminClub request, CancellationToken cancellationToken)
        {
            var club = await context.Clubs.SingleOrDefaultAsync(c => c.Id == request.ClubId, cancellationToken)
                ?? throw new Fusonic.Extensions.Common.Entities.EntityNotFoundException(typeof(Domain.Clubs.Club), request.ClubId);

            club.Update(request.Name, request.OpeningHours, request.PrimeTimeHours, request.BookingGracePeriodInMinutes, request.IsAtpClub, request.PrimeTimeSettings);
            var currency = (request.Currency ?? club.Currency).Trim().ToLowerInvariant();
            if (currency != club.Currency && await HasMoneyIn(club.Id, cancellationToken))
                throw new PreconditionException(Club.RegionErrorCode.ClubCurrencyInUse, "The club has prices or payments in its currency.");

            club.SetRegion(request.Country ?? club.Country, request.TimeZoneId ?? club.TimeZoneId, currency);
            await context.SaveChangesAsync(cancellationToken);
            return default;
        }

        /// <summary>Prices, payments or accounting of the club: they are amounts in its currency, so the currency can't change any more.</summary>
        private async Task<bool> HasMoneyIn(int clubId, CancellationToken cancellationToken)
            => await context.MembershipPlans.IgnoreQueryFilters().AnyAsync(p => p.ClubId == clubId, cancellationToken)
               || await context.MembershipOrders.IgnoreQueryFilters().AnyAsync(o => o.ClubId == clubId, cancellationToken)
               || await context.CourtPriceRules.IgnoreQueryFilters().AnyAsync(r => r.ClubId == clubId, cancellationToken)
               || await context.CourtPurchases.IgnoreQueryFilters().AnyAsync(p => p.ClubId == clubId, cancellationToken)
               || await context.ClubhouseItems.IgnoreQueryFilters().AnyAsync(i => i.ClubId == clubId, cancellationToken)
               || await context.ClubhouseTabs.IgnoreQueryFilters().AnyAsync(t => t.ClubId == clubId, cancellationToken)
               || await context.MoneyAccounts.IgnoreQueryFilters().AnyAsync(a => a.ClubId == clubId, cancellationToken)
               || await context.AccountingEntries.IgnoreQueryFilters().AnyAsync(e => e.ClubId == clubId, cancellationToken)
               || await context.Invoices.IgnoreQueryFilters().AnyAsync(i => i.ClubId == clubId, cancellationToken);
    }
}
