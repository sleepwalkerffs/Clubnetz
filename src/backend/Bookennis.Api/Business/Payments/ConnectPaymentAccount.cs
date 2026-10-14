using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.Memberships;
using Bookennis.Shared.Controller.Memberships;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Payments;

/// <summary>
/// Connects the club with Stripe: creates its Stripe account once and returns a link to Stripe's onboarding form.
/// Calling it again continues an onboarding that was not finished. Coming back from Stripe the page calls <see cref="RefreshPaymentAccount"/>.
/// </summary>
/// <param name="ClubId">The club.</param>
/// <param name="Country">ISO code of the country the club is based in. It can't be changed once the account exists.</param>
public record ConnectPaymentAccount(int ClubId, string Country) : ICommand<ConnectPaymentAccountResult>
{
    public class Handler(AppDbContext context, IStripeGateway gateway, AppSettings appSettings) : IRequestHandler<ConnectPaymentAccount, ConnectPaymentAccountResult>
    {
        public async Task<ConnectPaymentAccountResult> Handle(ConnectPaymentAccount request, CancellationToken cancellationToken)
        {
            if (!gateway.IsConfigured)
                throw new PaymentsNotConfiguredException();

            var account = await context.ClubPaymentAccounts.FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);
            if (account is null)
            {
                var club = await context.Clubs
                    .Where(c => c.Id == request.ClubId)
                    .Select(c => new { c.Name, c.WebsiteUrl, c.ReplyToEmail })
                    .SingleRequiredAsync(cancellationToken);

                var accountId = await StripeCalls.Run(() => gateway.CreateAccount(
                    new StripeAccountData(request.ClubId, club.Name, club.ReplyToEmail, club.WebsiteUrl, request.Country),
                    cancellationToken));

                // Saved before the link is created: the account exists at Stripe now and must not be created a second time
                account = new ClubPaymentAccount(request.ClubId, accountId, DateTimeOffset.UtcNow);
                context.ClubPaymentAccounts.Add(account);
                await context.SaveChangesAsync(cancellationToken);
            }

            // Both lead back to the plans page, where the account card checks the status again
            var page = $"{appSettings.AppUri.AbsoluteUri.TrimEnd('/')}/clubs/{request.ClubId}/membership-plans";
            var url = await StripeCalls.Run(() => gateway.CreateAccountLink(account.StripeAccountId, $"{page}?stripe=refresh", $"{page}?stripe=return", cancellationToken));

            return new ConnectPaymentAccountResult(url);
        }
    }
}
