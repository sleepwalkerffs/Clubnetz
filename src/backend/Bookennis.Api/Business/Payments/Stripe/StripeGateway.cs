using System.Globalization;
using System.Net;
using Bookennis.Api.Config;
using Stripe;
using Stripe.Checkout;
using V2Core = global::Stripe.V2.Core;

namespace Bookennis.Api.Business.Payments.Stripe;

/// <summary>Talks to Stripe with Stripe.net. Calls for a club carry the <c>Stripe-Account</c> header of its connected account.</summary>
public class StripeGateway(AppSettings appSettings) : IStripeGateway
{
    public const string ClubIdMetadataKey = "club_id";
    public const string OrderIdMetadataKey = "membership_order_id";

    private readonly StripeSettings settings = appSettings.Stripe;
    private StripeClient? client;

    public bool IsConfigured => settings.IsConfigured;

    private StripeClient Client => client ??= settings.IsConfigured
        ? new StripeClient(settings.SecretKey)
        : throw new PaymentsNotConfiguredException();

    // Accounts are created with the Accounts v2 API: Stripe refuses v1 for new Connect platforms.
    public async Task<string> CreateAccount(StripeAccountData data, CancellationToken cancellationToken)
    {
        var options = new V2Core.AccountCreateOptions
        {
            DisplayName = data.ClubName,
            ContactEmail = data.Email,
            Identity = new V2Core.AccountCreateIdentityOptions { Country = data.Country.ToLowerInvariant() },
            // The club is the merchant: it uses the Stripe dashboard itself, pays the Stripe fees and answers its disputes
            Dashboard = "full",
            Defaults = new V2Core.AccountCreateDefaultsOptions
            {
                Responsibilities = new V2Core.AccountCreateDefaultsResponsibilitiesOptions { FeesCollector = "stripe", LossesCollector = "stripe" },
                Profile = PublicUrl(data.WebsiteUrl) is { } url ? new V2Core.AccountCreateDefaultsProfileOptions { BusinessUrl = url } : null,
            },
            Configuration = new V2Core.AccountCreateConfigurationOptions
            {
                Merchant = new V2Core.AccountCreateConfigurationMerchantOptions
                {
                    Capabilities = new V2Core.AccountCreateConfigurationMerchantCapabilitiesOptions
                    {
                        CardPayments = new V2Core.AccountCreateConfigurationMerchantCapabilitiesCardPaymentsOptions { Requested = true },
                        SepaDebitPayments = new V2Core.AccountCreateConfigurationMerchantCapabilitiesSepaDebitPaymentsOptions { Requested = true },
                    },
                },
            },
            Metadata = new Dictionary<string, string> { [ClubIdMetadataKey] = data.ClubId.ToString(CultureInfo.InvariantCulture) },
        };

        var account = await Client.V2.Core.Accounts.CreateAsync(options, cancellationToken: cancellationToken);
        return account.Id;
    }

    public async Task<string> CreateAccountLink(string accountId, string refreshUrl, string returnUrl, CancellationToken cancellationToken)
    {
        var options = new V2Core.AccountLinkCreateOptions
        {
            Account = accountId,
            UseCase = new V2Core.AccountLinkCreateUseCaseOptions
            {
                Type = "account_onboarding",
                AccountOnboarding = new V2Core.AccountLinkCreateUseCaseAccountOnboardingOptions { RefreshUrl = refreshUrl, ReturnUrl = returnUrl },
            },
        };

        var link = await Client.V2.Core.AccountLinks.CreateAsync(options, cancellationToken: cancellationToken);
        return link.Url;
    }

    // Read with the v1 endpoint, which also answers for accounts created with v2: it tells in three flags what the app needs
    public Task<StripeAccountInfo?> GetAccount(string accountId, CancellationToken cancellationToken)
        => OrNull(async () =>
        {
            var account = await new AccountService(Client).GetAsync(accountId, cancellationToken: cancellationToken);
            return new StripeAccountInfo(account.Id, account.ChargesEnabled, account.PayoutsEnabled, account.DetailsSubmitted);
        });

    public async Task<StripeCheckoutSession> CreateCheckoutSession(string accountId, StripeCheckoutData data, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            [ClubIdMetadataKey] = data.ClubId.ToString(CultureInfo.InvariantCulture),
            [OrderIdMetadataKey] = data.OrderId.ToString(CultureInfo.InvariantCulture),
        };

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            // One line with the name and the amount of the order. Nothing is kept in Stripe's product catalog,
            // so a plan that changes later can't break an order. The payment methods are the ones the club switched on in Stripe.
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = data.Currency,
                        UnitAmount = data.AmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = data.ProductName, Description = data.Description },
                    },
                },
            ],
            CustomerEmail = data.CustomerEmail,
            ClientReferenceId = data.OrderId.ToString(CultureInfo.InvariantCulture),
            Metadata = metadata,
            Locale = data.Locale,
            SuccessUrl = data.SuccessUrl,
            CancelUrl = data.CancelUrl,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Description = data.ProductName,
                Metadata = metadata,
                ApplicationFeeAmount = data.ApplicationFeeCents is > 0 ? data.ApplicationFeeCents : null,
            },
        };

        var session = await new SessionService(Client).CreateAsync(options, ForAccount(accountId), cancellationToken);
        return ToSession(session);
    }

    public Task<StripeCheckoutSession?> GetCheckoutSession(string accountId, string sessionId, CancellationToken cancellationToken)
        => OrNull(async () => ToSession(await new SessionService(Client).GetAsync(sessionId, requestOptions: ForAccount(accountId), cancellationToken: cancellationToken)));

    public async Task ExpireCheckoutSession(string accountId, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await new SessionService(Client).ExpireAsync(sessionId, requestOptions: ForAccount(accountId), cancellationToken: cancellationToken);
        }
        catch (StripeException e) when (e.HttpStatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            // Already completed or expired: nothing left to close
        }
    }

    public Task<StripeChargeInfo?> GetCharge(string accountId, string chargeId, CancellationToken cancellationToken)
        => OrNull(async () =>
        {
            var charge = await new ChargeService(Client).GetAsync(chargeId, requestOptions: ForAccount(accountId), cancellationToken: cancellationToken);
            return new StripeChargeInfo(charge.Id, charge.PaymentIntentId, charge.Refunded);
        });

    public Task<StripeDisputeInfo?> GetDispute(string accountId, string disputeId, CancellationToken cancellationToken)
        => OrNull(async () =>
        {
            var dispute = await new DisputeService(Client).GetAsync(disputeId, requestOptions: ForAccount(accountId), cancellationToken: cancellationToken);
            return new StripeDisputeInfo(dispute.Id, dispute.PaymentIntentId);
        });

    public async Task<IReadOnlyList<StripeEventInfo>> ListUndeliveredEvents(string accountId, IReadOnlyCollection<string> types, int limit, CancellationToken cancellationToken)
    {
        var options = new EventListOptions { DeliverySuccess = false, Types = types.ToList(), Limit = 100 };
        var events = new List<StripeEventInfo>();

        await foreach (var stripeEvent in new EventService(Client).ListAutoPagingAsync(options, ForAccount(accountId), cancellationToken))
        {
            if ((stripeEvent.Data?.Object as IHasId)?.Id is not { Length: > 0 } objectId)
                continue;

            events.Add(new StripeEventInfo(stripeEvent.Id, stripeEvent.Type, stripeEvent.Account ?? accountId, objectId));
            if (events.Count >= limit)
                break;
        }

        return events;
    }

    public StripeEventInfo ParseWebhookEvent(string payload, string? signatureHeader)
    {
        if (!settings.IsConfigured)
            throw new PaymentsNotConfiguredException();

        if (string.IsNullOrWhiteSpace(signatureHeader))
            throw new StripeSignatureException("The Stripe-Signature header is missing.");

        Event stripeEvent;
        try
        {
            // Only the signature and the ids are used; the object is loaded again from Stripe when the event is processed,
            // so an event of another API version than the one of the library is fine
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, settings.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new StripeSignatureException("The webhook request could not be verified.", e);
        }

        var objectId = (stripeEvent.Data?.Object as IHasId)?.Id;
        if (string.IsNullOrEmpty(stripeEvent.Id) || string.IsNullOrEmpty(stripeEvent.Type) || string.IsNullOrEmpty(objectId))
            throw new StripeSignatureException("The webhook event is incomplete.");

        return new StripeEventInfo(stripeEvent.Id, stripeEvent.Type, stripeEvent.Account, objectId);
    }

    private static RequestOptions ForAccount(string accountId) => new() { StripeAccount = accountId };

    private static StripeCheckoutSession ToSession(Session session) => new(
        session.Id,
        session.Url,
        session.Status,
        session.PaymentStatus,
        session.ClientReferenceId,
        session.Metadata is not null && session.Metadata.TryGetValue(ClubIdMetadataKey, out var clubId) ? clubId : null,
        session.PaymentIntentId);

    /// <summary>Stripe only takes public https/http addresses as the website of a business.</summary>
    private static string? PublicUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && !uri.IsLoopback ? url : null;

    private static async Task<T?> OrNull<T>(Func<Task<T>> load)
        where T : class
    {
        try
        {
            return await load();
        }
        catch (StripeException e) when (e.HttpStatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // The object does not exist on this account, or the account was disconnected from the platform
            return null;
        }
    }
}
