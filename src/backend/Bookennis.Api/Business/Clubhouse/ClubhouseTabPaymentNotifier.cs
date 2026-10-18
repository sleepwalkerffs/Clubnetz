using System.Globalization;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Clubhouse;

/// <summary>Why the host has to look at an online payment. The texts are in Notifications(.de).resx (<c>ClubhouseTab_Reason_...</c>).</summary>
public enum ClubhouseTabPaymentAttentionReason
{
    /// <summary>The tab was already paid (cash, card or another online payment) when the money arrived: one of them has to be refunded.</summary>
    PaidTwice,

    /// <summary>The money of a payment page that ran out arrived after the tab was changed (or discarded): the tab stays open.</summary>
    TabChanged,

    /// <summary>The payment page ran out and SumUp could not be asked (account disconnected or refused): the tab was unlocked unchecked.</summary>
    CouldNotVerify,
}

/// <summary>
/// The emails about tabs paid in the app. They are always sent and have no notification preference: they are about money.
/// Send them after the changes are saved.
/// </summary>
public interface IClubhouseTabPaymentNotifier
{
    /// <summary>To the member who paid (or the parent who gets the emails of a child).</summary>
    Task Paid(ClubhouseTabPayment payment, CancellationToken cancellationToken);

    /// <summary>To the clubhouse hosts of the club, or to the admins when there is no host.</summary>
    Task NeedsAttention(ClubhouseTabPayment payment, ClubhouseTabPaymentAttentionReason reason, CancellationToken cancellationToken);
}

public class ClubhouseTabPaymentNotifier(AppDbContext context, IMediator mediator, AppSettings appSettings) : IClubhouseTabPaymentNotifier
{
    public async Task Paid(ClubhouseTabPayment payment, CancellationToken cancellationToken)
    {
        if (payment.PayerMemberId is not { } memberId)
            return;

        var recipient = await MemberEmailResolver.GetNotificationInfo(context, memberId, cancellationToken);
        if (recipient?.Email is null)
            return;

        var culture = recipient.Language.ToCultureInfo();
        var member = new MemberVariables(recipient.FirstName, recipient.LastName);
        var tab = await LoadTab(payment, culture, $"{AppUrl}/clubs/{payment.ClubId}/my-club", cancellationToken);

        await mediator.Send(new SendClubEmail(payment.ClubId, ClubEmailType.ClubhouseTabPaid, recipient.Email, member.FullName, recipient.Language,
            new ClubhouseTabPaidEmailVariables(member, tab.Variables)), cancellationToken);
    }

    public async Task NeedsAttention(ClubhouseTabPayment payment, ClubhouseTabPaymentAttentionReason reason, CancellationToken cancellationToken)
    {
        // Query filters are ignored because SumUp reports payments outside of a request of the club; the club is filtered explicitly
        var staff = await (
            from member in context.ClubMembers.IgnoreQueryFilters()
            join user in context.Users on member.UserId equals user.Id
            where member.ClubId == payment.ClubId
               && user.Email != null
               && (member.UserRoles.Contains(MemberRole.ClubhouseHost) || member.UserRoles.Contains(MemberRole.Admin))
            orderby member.Id
            select new { IsHost = member.UserRoles.Contains(MemberRole.ClubhouseHost), user.Email, user.FirstName, user.LastName, user.Language }
        ).ToListAsync(cancellationToken);

        var recipients = staff.Any(s => s.IsHost) ? staff.Where(s => s.IsHost).ToList() : staff;
        foreach (var recipient in recipients.DistinctBy(r => r.Email!.ToLowerInvariant()))
        {
            var culture = recipient.Language.ToCultureInfo();
            var member = new MemberVariables(recipient.FirstName, recipient.LastName);
            var tab = await LoadTab(payment, culture, $"{AppUrl}/clubs/{payment.ClubId}/clubhouse", cancellationToken);
            var variables = new ClubhouseTabNeedsAttentionEmailVariables(
                member,
                tab.Variables,
                NotificationTexts.Get(culture, $"ClubhouseTab_Reason_{reason}"),
                tab.GuestName);

            await mediator.Send(new SendClubEmail(payment.ClubId, ClubEmailType.ClubhouseTabNeedsAttention, recipient.Email!, member.FullName, recipient.Language, variables), cancellationToken);
        }
    }

    private string AppUrl => appSettings.AppUri.AbsoluteUri.TrimEnd('/');

    private async Task<(ClubhouseTabVariables Variables, string GuestName)> LoadTab(ClubhouseTabPayment payment, CultureInfo culture, string url, CancellationToken cancellationToken)
    {
        var tab = await context.ClubhouseTabs.IgnoreQueryFilters().AsNoTracking()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == payment.TabId && t.ClubId == payment.ClubId, cancellationToken);

        var guests = tab?.MemberId is { } memberId ? await context.LoadGuests(payment.ClubId, [memberId], cancellationToken) : [];
        var guestName = tab is null ? "" : ClubhouseQueries.Name(tab, tab.MemberId is { } id ? guests.GetValueOrDefault(id) : null);

        var items = tab is null
            ? ""
            : string.Join(", ", tab.Lines
                .GroupBy(l => l.Name)
                .OrderByDescending(g => g.Sum(l => l.Quantity)).ThenBy(g => g.Key)
                .Select(g => $"{g.Sum(l => l.Quantity)}× {g.Key}"));

        var paidAt = payment.PaidAt ?? payment.CreatedAt;
        var variables = new ClubhouseTabVariables(
            MembershipTexts.Amount(payment.AmountCents, culture),
            NotificationTexts.Date(culture, ClubhouseQueries.LocalDate(paidAt)),
            items,
            payment.TransactionCode ?? "–",
            url);

        return (variables, guestName);
    }
}
