using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>Why the treasurers have to look at a purchase. The texts are in Notifications(.de).resx (<c>CourtPurchase_Reason_...</c>).</summary>
public enum CourtPurchaseAttentionReason
{
    /// <summary>The SEPA debit failed; the booking was removed if it was not over.</summary>
    PaymentFailed,
    Refunded,
    Disputed,

    /// <summary>Money arrived for a purchase that was cancelled, failed or refunded.</summary>
    UnexpectedPayment,

    /// <summary>Money arrived after the hold expired and somebody else booked the slot in the meantime.</summary>
    PaidWithoutSlot,

    /// <summary>The club removed the booking of a bought slot (court blocked or inactive, overbooked).</summary>
    BookingRemoved,

    /// <summary>The money of an expired purchase with a credit arrived after the credit was spent elsewhere.</summary>
    CreditNoLongerAvailable,
}

/// <summary>
/// The emails about bought court time. They are always sent and have no notification preference: they are about money.
/// Send them after the changes are saved.
/// </summary>
public interface ICourtPurchaseNotifier
{
    /// <summary>To the buyer: the slot is booked (paid, paid with a credit or free, or its debit started).</summary>
    Task Confirmed(CourtPurchase purchase, CancellationToken cancellationToken);

    /// <summary>To the buyer: the debit failed and the booking was removed.</summary>
    Task PaymentFailed(CourtPurchase purchase, CancellationToken cancellationToken);

    /// <summary>To the treasurers and admins of the club.</summary>
    Task NeedsAttention(CourtPurchase purchase, CourtPurchaseAttentionReason reason, CancellationToken cancellationToken);
}

public class CourtPurchaseNotifier(AppDbContext context, IMediator mediator, AppSettings appSettings) : ICourtPurchaseNotifier
{
    public Task Confirmed(CourtPurchase purchase, CancellationToken cancellationToken)
        => SendToBuyer(purchase, ClubEmailType.CourtPurchaseConfirmed, cancellationToken);

    public Task PaymentFailed(CourtPurchase purchase, CancellationToken cancellationToken)
        => SendToBuyer(purchase, ClubEmailType.CourtPurchaseFailed, cancellationToken);

    public async Task NeedsAttention(CourtPurchase purchase, CourtPurchaseAttentionReason reason, CancellationToken cancellationToken)
    {
        // Query filters are ignored because Stripe reports events outside of a request of the club; the club is filtered explicitly
        var recipients = await (
            from member in context.ClubMembers.IgnoreQueryFilters()
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == purchase.ClubId
               && user.Email != null
               && (member.UserRoles.Contains(MemberRole.Treasurer) || member.UserRoles.Contains(MemberRole.Admin))
            orderby member.Id
            select new { user.Email, user.FirstName, user.LastName, user.Language }
        ).ToListAsync(cancellationToken);

        if (recipients.Count == 0)
            return;

        var details = await LoadDetails(purchase, cancellationToken);
        var url = $"{AppUrl}/clubs/{purchase.ClubId}/court-sales";

        foreach (var recipient in recipients.DistinctBy(r => r.Email!.ToLowerInvariant()))
        {
            var culture = recipient.Language.ToCultureInfo();
            var member = new MemberVariables(recipient.FirstName, recipient.LastName);
            var variables = new CourtPurchaseNeedsAttentionEmailVariables(
                member,
                details.ToVariables(purchase, culture, url),
                NotificationTexts.Get(culture, $"CourtPurchase_Reason_{reason}"),
                details.BuyerName ?? "");

            await mediator.Send(new SendClubEmail(purchase.ClubId, ClubEmailType.CourtPurchaseNeedsAttention, recipient.Email!, member.FullName, recipient.Language, variables), cancellationToken);
        }
    }

    private async Task SendToBuyer(CourtPurchase purchase, ClubEmailType type, CancellationToken cancellationToken)
    {
        if (purchase.MemberId is not { } memberId)
            return;

        var recipient = await MemberEmailResolver.GetNotificationInfo(context, memberId, cancellationToken);
        if (recipient?.Email is null)
            return;

        var details = await LoadDetails(purchase, cancellationToken);
        var culture = recipient.Language.ToCultureInfo();
        var member = new MemberVariables(recipient.FirstName, recipient.LastName);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(purchase.Interval.From, details.TimeZone).DateTime);
        var url = $"{AppUrl}/clubs/{purchase.ClubId}/booking?date={date:yyyy-MM-dd}";

        await mediator.Send(
            new SendClubEmail(purchase.ClubId, type, recipient.Email, member.FullName, recipient.Language, new CourtPurchaseEmailVariables(member, details.ToVariables(purchase, culture, url))),
            cancellationToken);
    }

    private string AppUrl => appSettings.AppUri.AbsoluteUri.TrimEnd('/');

    private async Task<PurchaseDetails> LoadDetails(CourtPurchase purchase, CancellationToken cancellationToken)
    {
        var court = await context.Courts.IgnoreQueryFilters()
            .Where(c => c.Id == purchase.CourtId && c.ClubId == purchase.ClubId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var buyer = purchase.MemberId is { } memberId
            ? await (
                from member in context.ClubMembers.IgnoreQueryFilters()
                join user in context.Users on member.UserId equals user.Id
                where member.Id == memberId && member.ClubId == purchase.ClubId
                select user.FirstName + " " + user.LastName
            ).FirstOrDefaultAsync(cancellationToken)
            : null;

        var timeZoneId = await context.CourtSaleSettings.IgnoreQueryFilters()
            .Where(s => s.ClubId == purchase.ClubId)
            .Select(s => s.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        return new PurchaseDetails(court ?? "", buyer, CourtSaleSettings.FindTimeZone(timeZoneId ?? CourtSaleSettings.DefaultTimeZoneId));
    }

    private sealed record PurchaseDetails(string CourtName, string? BuyerName, TimeZoneInfo TimeZone)
    {
        public CourtPurchaseVariables ToVariables(CourtPurchase purchase, CultureInfo culture, string url)
        {
            var from = TimeZoneInfo.ConvertTime(purchase.Interval.From, TimeZone);
            var to = TimeZoneInfo.ConvertTime(purchase.Interval.To, TimeZone);
            return new CourtPurchaseVariables(
                CourtName,
                NotificationTexts.Date(culture, DateOnly.FromDateTime(from.DateTime)),
                $"{NotificationTexts.Time(culture, TimeOnly.FromDateTime(from.DateTime))} – {NotificationTexts.Time(culture, TimeOnly.FromDateTime(to.DateTime))}",
                MembershipTexts.Amount(purchase.PriceCents, culture),
                CourtPurchaseTexts.Paid(purchase, culture),
                url);
        }
    }
}

public static class CourtPurchaseTexts
{
    /// <summary>How the slot was paid, e.g. "€28.00 online", "1 booking credit", "1 booking credit + €8.00 online", "free".</summary>
    public static string Paid(CourtPurchase purchase, CultureInfo culture)
    {
        var online = MembershipTexts.Amount(purchase.AmountDueCents, culture);
        return purchase switch
        {
            { CreditUsed: true, AmountDueCents: 0 } => NotificationTexts.Get(culture, "CourtPurchase_Paid_Credit"),
            { CreditUsed: true } when purchase.PaymentMethod is not null => NotificationTexts.Get(culture, "CourtPurchase_Paid_CreditAndOnline", online),
            { CreditUsed: false, AmountDueCents: 0 } => NotificationTexts.Get(culture, "CourtPurchase_Paid_Free"),
            { PaymentMethod: CourtPurchasePaymentMethod.Online } => NotificationTexts.Get(culture, "CourtPurchase_Paid_Online", online),
            _ => NotificationTexts.Get(culture, "CourtPurchase_Paid_Pending"),
        };
    }
}
