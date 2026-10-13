using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>Why the treasurers have to look at an order. The texts are in Notifications(.de).resx (<c>Membership_Reason_...</c>).</summary>
public enum MembershipAttentionReason
{
    ApprovalRequested,
    PaymentFailed,
    Refunded,
    Disputed,

    /// <summary>An online payment arrived for an order that was cancelled or already paid another way while the member was paying.</summary>
    UnexpectedPayment,
}

/// <summary>
/// The emails about membership orders. They are always sent and have no notification preference:
/// they are about money the recipient paid or has to look after.
/// </summary>
public interface IMembershipNotifier
{
    /// <summary>To the payer: the fee was paid (or its debit was started) and the members are enrolled.</summary>
    Task PaymentReceived(MembershipOrder order, CancellationToken cancellationToken);

    /// <summary>To the member who asked for the plan: the club confirmed it, the fee can be paid now.</summary>
    Task Approved(MembershipOrder order, CancellationToken cancellationToken);

    /// <summary>To the payer: the payment did not go through.</summary>
    Task PaymentFailed(MembershipOrder order, CancellationToken cancellationToken);

    /// <summary>To the treasurers and admins of the club.</summary>
    Task NeedsAttention(MembershipOrder order, MembershipAttentionReason reason, CancellationToken cancellationToken);
}

public class MembershipNotifier(AppDbContext context, IMediator mediator, AppSettings appSettings) : IMembershipNotifier
{
    public Task PaymentReceived(MembershipOrder order, CancellationToken cancellationToken)
        => SendToPayer(order, ClubEmailType.MembershipPaymentReceived, cancellationToken);

    public Task Approved(MembershipOrder order, CancellationToken cancellationToken)
        => SendToPayer(order, ClubEmailType.MembershipApproved, cancellationToken);

    public Task PaymentFailed(MembershipOrder order, CancellationToken cancellationToken)
        => SendToPayer(order, ClubEmailType.MembershipPaymentFailed, cancellationToken);

    public async Task NeedsAttention(MembershipOrder order, MembershipAttentionReason reason, CancellationToken cancellationToken)
    {
        // Query filters are ignored because Stripe reports events outside of a request of the club; the club is filtered explicitly
        var recipients = await (
            from member in context.ClubMembers.IgnoreQueryFilters()
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == order.ClubId
               && user.Email != null
               && (member.UserRoles.Contains(MemberRole.Treasurer) || member.UserRoles.Contains(MemberRole.Admin))
            orderby member.Id
            select new { user.Email, user.FirstName, user.LastName, user.Language }
        ).ToListAsync(cancellationToken);

        if (recipients.Count == 0)
            return;

        var details = await LoadDetails(order, cancellationToken);
        var url = $"{AppUrl}/clubs/{order.ClubId}/membership-payments?season={order.SeasonId}";

        foreach (var recipient in recipients.DistinctBy(r => r.Email!.ToLowerInvariant()))
        {
            var culture = recipient.Language.ToCultureInfo();
            var member = new MemberVariables(recipient.FirstName, recipient.LastName);
            var variables = new MembershipNeedsAttentionEmailVariables(
                member,
                details.ToVariables(order, culture, url),
                NotificationTexts.Get(culture, $"Membership_Reason_{reason}"),
                details.PayerName ?? details.MemberNames);

            await mediator.Send(new SendClubEmail(order.ClubId, ClubEmailType.MembershipNeedsAttention, recipient.Email!, member.FullName, recipient.Language, variables), cancellationToken);
        }
    }

    private async Task SendToPayer(MembershipOrder order, ClubEmailType type, CancellationToken cancellationToken)
    {
        // The payer, or the first member of the order when the payer is not known (recorded by the treasurer for somebody who left)
        var recipientMemberId = order.PayerMemberId ?? order.Members.Select(m => (int?)m.MemberId).FirstOrDefault();
        if (recipientMemberId is null)
            return;

        // The own email or a parent's for children without one
        var recipient = await MemberEmailResolver.GetNotificationInfo(context, recipientMemberId.Value, cancellationToken);
        if (recipient?.Email is null)
            return;

        var details = await LoadDetails(order, cancellationToken);
        var culture = recipient.Language.ToCultureInfo();
        var member = new MemberVariables(recipient.FirstName, recipient.LastName);
        var url = $"{AppUrl}/clubs/{order.ClubId}/membership?season={order.SeasonId}&order={order.Id}";

        await mediator.Send(
            new SendClubEmail(order.ClubId, type, recipient.Email, member.FullName, recipient.Language, new MembershipEmailVariables(member, details.ToVariables(order, culture, url))),
            cancellationToken);
    }

    private string AppUrl => appSettings.AppUri.AbsoluteUri.TrimEnd('/');

    private async Task<OrderDetails> LoadDetails(MembershipOrder order, CancellationToken cancellationToken)
    {
        var memberIds = order.Members.Select(m => m.MemberId).ToList();
        if (order.PayerMemberId is { } payerId)
            memberIds.Add(payerId);

        var names = await (
            from member in context.ClubMembers.IgnoreQueryFilters()
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == order.ClubId && memberIds.Contains(member.Id)
            orderby user.LastName, user.FirstName
            select new { member.Id, Name = user.FirstName + " " + user.LastName }
        ).ToListAsync(cancellationToken);

        var season = await context.Seasons.IgnoreQueryFilters()
            .Where(s => s.Id == order.SeasonId && s.ClubId == order.ClubId)
            .Select(s => new { s.Period.From, s.Period.To })
            .SingleAsync(cancellationToken);

        var covered = order.Members.Select(m => m.MemberId).ToHashSet();
        return new OrderDetails(
            string.Join(", ", names.Where(n => covered.Contains(n.Id)).Select(n => n.Name)),
            names.FirstOrDefault(n => n.Id == order.PayerMemberId)?.Name,
            season.From,
            season.To);
    }

    private sealed record OrderDetails(string MemberNames, string? PayerName, DateOnly SeasonFrom, DateOnly SeasonTo)
    {
        public MembershipVariables ToVariables(MembershipOrder order, CultureInfo culture, string url) => new(
            order.PlanName,
            ClubEmailCatalog.FormatSeason(SeasonFrom, SeasonTo, culture).Period,
            MembershipTexts.Amount(order.AmountCents, culture),
            MemberNames,
            order.PaymentMethod is { } method ? NotificationTexts.Get(culture, $"Membership_Method_{method}") : "",
            url);
    }
}

public static class MembershipTexts
{
    /// <summary>An amount in euro as it is written in the language: "€ 215,00" in German, "€215.00" in English.</summary>
    public static string Amount(int amountCents, CultureInfo culture) => (amountCents / 100m).ToString("C2", MoneyFormat(culture));

    // en-AT is written like German on some systems and like English on others, depending on the ICU data.
    // English emails always get the English form.
    private static NumberFormatInfo MoneyFormat(CultureInfo culture)
    {
        if (culture.TwoLetterISOLanguageName != "en")
            return culture.NumberFormat;

        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.CurrencySymbol = "€";
        format.CurrencyPositivePattern = 0;
        format.CurrencyNegativePattern = 1;
        return format;
    }

    public static CultureInfo Culture(Language language) => language.ToCultureInfo();
}
