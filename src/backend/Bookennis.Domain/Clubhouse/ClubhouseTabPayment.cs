using System.Runtime.InteropServices;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Clubhouse;

/// <summary>Keep in sync with <c>Bookennis.Shared.Controller.Clubhouse.ClubhouseTabPaymentStatus</c>.</summary>
public enum ClubhouseTabPaymentStatus
{
    /// <summary>The payment page of SumUp is open (at most <see cref="ClubhouseTabPayment.Lifetime"/>).</summary>
    Pending = 0,
    Paid = 1,

    /// <summary>The payment page closed without money: ran out, given up or replaced.</summary>
    Expired = 2,
}

/// <summary>
/// One attempt of a member to pay a tab in the app: a checkout on the SumUp account of the host for the amount of the tab at that
/// moment. While it is pending the tab is locked (<see cref="ClubhouseTabStatus.AwaitingPayment"/>), so the amount stays right.
/// Only what SumUp answers changes it, never the redirect of the browser.
/// </summary>
[Guid("5B0D7C42-8E1A-4F3D-A6B9-2C7E9D14F083")]
public class ClubhouseTabPayment : TenantDomainEntity, IAggregateRoot
{
    /// <summary>How long the payment page of SumUp can be used; the hosted checkout of SumUp lasts as long.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public const int MaxCheckoutIdLength = 64;
    public const int MaxUrlLength = 500;
    public const int MaxTransactionCodeLength = 64;
    public const int MaxMerchantCodeLength = ClubhouseSumUpAccount.MaxMerchantCodeLength;

#pragma warning disable CS8618
    private ClubhouseTabPayment() { }
#pragma warning restore CS8618

    public ClubhouseTabPayment(ClubhouseTab tab, int? payerMemberId, string merchantCode, DateTimeOffset now)
    {
        if (tab.Status != ClubhouseTabStatus.AwaitingPayment)
            throw new InvalidOperationException("Start the online payment on the tab first: it locks the amount.");

        ClubId = tab.ClubId;
        TabId = tab.Id;
        PayerMemberId = payerMemberId;
        AmountCents = tab.TotalCents;
        Currency = tab.Currency;
        MerchantCode = merchantCode;
        Reference = System.Guid.NewGuid();
        Status = ClubhouseTabPaymentStatus.Pending;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
    }

    public int TabId { get; private set; }

    /// <summary>Who paid: the member of the tab or a parent of the family. <c>null</c> once that member left the club.</summary>
    public int? PayerMemberId { get; private set; }

    public int AmountCents { get; private set; }
    public string Currency { get; private set; }

    /// <summary>The SumUp account (of the host) the checkout was created on.</summary>
    public string MerchantCode { get; private set; }

    /// <summary>Our reference of the checkout at SumUp (<see cref="CheckoutReference"/>); unique, so a checkout is never created twice.</summary>
    public Guid Reference { get; private set; }

    /// <summary>The id SumUp gave the checkout; <c>null</c> only until it was created.</summary>
    public string? CheckoutId { get; private set; }

    /// <summary>Where the member pays (the hosted checkout of SumUp), kept so a second tap on "Pay" leads to the same page.</summary>
    public string? CheckoutUrl { get; private set; }

    public ClubhouseTabPaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>The code of the transaction at SumUp; the host finds the payment with it in the SumUp dashboard (e.g. to refund it).</summary>
    public string? TransactionCode { get; private set; }

    public bool IsPending => Status == ClubhouseTabPaymentStatus.Pending;

    /// <summary>The <c>checkout_reference</c> at SumUp, e.g. <c>clubhouse_tab:42:3f2c…</c> (at most 64 characters).</summary>
    public string CheckoutReference => $"clubhouse_tab:{TabId}:{Reference:N}";

    public void AttachCheckout(string checkoutId, string checkoutUrl)
    {
        CheckoutId = checkoutId;
        CheckoutUrl = checkoutUrl;
    }

    /// <summary>SumUp confirmed the payment. Repeating it changes nothing (the webhook and the member coming back both report it).</summary>
    public void MarkPaid(string? transactionCode, DateTimeOffset now)
    {
        if (Status == ClubhouseTabPaymentStatus.Paid)
            return;

        Status = ClubhouseTabPaymentStatus.Paid;
        TransactionCode = transactionCode;
        PaidAt = now;
    }

    /// <summary>The payment page closed without money. A paid payment stays paid.</summary>
    public void Expire()
    {
        if (Status == ClubhouseTabPaymentStatus.Pending)
            Status = ClubhouseTabPaymentStatus.Expired;
    }
}
