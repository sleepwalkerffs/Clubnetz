using System.Globalization;
using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Business.ClubEvents;
using Bookennis.Api.Business.Members;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Payments.Stripe;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Courts;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.CourtSales;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;
using CourtPurchaseStatus = Bookennis.Domain.CourtSales.CourtPurchaseStatus;
using SharedStatus = Bookennis.Shared.Controller.CourtSales.CourtPurchaseStatus;

namespace Bookennis.Api.Business.CourtSales;

/// <summary>The checks of a slot that don't need the database: length, start, how far ahead.</summary>
public static class CourtSlotRules
{
    public static CourtSaleQuoteUnavailableReason? Check(CourtSaleSettings settings, DateTimeOffsetInterval interval, DateTimeOffset now)
    {
        if (!settings.IsSelling())
            return CourtSaleQuoteUnavailableReason.NotEnabled;

        var minutes = (interval.To - interval.From).TotalMinutes;
        var startsOnSlot = interval.From.Second == 0 && interval.From.Millisecond == 0 && interval.From.ToUniversalTime().Minute % CourtSaleSettings.SlotMinutes == 0;
        if (!startsOnSlot || minutes <= 0 || minutes % CourtSaleSettings.SlotMinutes != 0 || minutes > settings.MaxBookingMinutes)
            return CourtSaleQuoteUnavailableReason.InvalidDuration;

        if (interval.From <= now)
            return CourtSaleQuoteUnavailableReason.InThePast;

        var timeZone = settings.GetTimeZone();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(interval.From, timeZone).DateTime);
        if (day.DayNumber - today.DayNumber > settings.MaxDaysInAdvance)
            return CourtSaleQuoteUnavailableReason.TooFarInAdvance;

        return null;
    }

    /// <summary>Throws the error that belongs to a reason of <see cref="Check"/>.</summary>
    public static void Ensure(CourtSaleSettings settings, DateTimeOffsetInterval interval, DateTimeOffset now)
    {
        switch (Check(settings, interval, now))
        {
            case CourtSaleQuoteUnavailableReason.NotEnabled:
                throw new PreconditionException(CourtPurchaseErrorCode.CourtSalesNotEnabled, "The club does not sell court time.");
            case CourtSaleQuoteUnavailableReason.InvalidDuration:
                throw new PreconditionException(CourtPurchaseErrorCode.CourtPurchaseDurationInvalid, [settings.MaxBookingMinutes.ToString(CultureInfo.InvariantCulture)], "Invalid slot.");
            case CourtSaleQuoteUnavailableReason.InThePast:
                throw new PreconditionException(BookingDomainService.ErrorCode.BookingInThePast, "Cannot book in the past");
            case CourtSaleQuoteUnavailableReason.TooFarInAdvance:
                throw new PreconditionException(CourtPurchaseErrorCode.CourtPurchaseTooFarInAdvance, [settings.MaxDaysInAdvance.ToString(CultureInfo.InvariantCulture)], "The slot is too far ahead.");
        }
    }

    /// <summary>The price of a slot and how it is paid by a buyer with <paramref name="credits"/>.</summary>
    public static (CourtQuote Quote, bool CreditUsed, int AmountDueCents)? Price(IReadOnlyCollection<CourtPriceRule> rules, CourtSaleSettings settings, int courtId, DateTimeOffsetInterval interval, int credits)
    {
        var timeZone = settings.GetTimeZone();
        var quote = CourtPricing.Quote(rules, courtId, interval, timeZone);
        if (quote is null)
            return null;

        if (credits <= 0 || quote.PriceCents == 0)
            return (quote, false, quote.PriceCents);

        // One credit pays for a booking of up to an hour; a longer booking pays the rest online.
        // A credit is never spent on free time: when the first hour costs nothing it is kept.
        var firstHour = CourtPricing.QuoteFirstMinutes(rules, courtId, interval, timeZone, CourtSaleSettings.MinutesCoveredByCredit)!;
        if (firstHour.PriceCents == 0)
            return (quote, false, quote.PriceCents);

        return (quote, true, Math.Max(0, quote.PriceCents - firstHour.PriceCents));
    }
}

/// <summary>What the caller can do with court sales (booking page).</summary>
public record GetCourtSaleOptions(int ClubId, int UserId) : IQuery<CourtSaleOptionsResult>
{
    public class Handler(AppDbContext context, IPaymentAvailability payments) : IRequestHandler<GetCourtSaleOptions, CourtSaleOptionsResult>
    {
        public async Task<CourtSaleOptionsResult> Handle(GetCourtSaleOptions request, CancellationToken cancellationToken)
        {
            var settings = await context.GetCourtSaleSettings(request.ClubId, cancellationToken);
            var onlinePayment = await payments.CanAcceptPayments(request.ClubId, cancellationToken);
            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);

            var credits = memberId is { } id ? await context.GetCreditBalance(request.ClubId, id, cancellationToken) : 0;
            var upcoming = memberId is { } id2 ? await context.CountUpcomingPurchases(request.ClubId, id2, cancellationToken) : 0;

            CourtSaleUnavailableReason? reason = null;
            if (!settings.IsSelling())
                reason = CourtSaleUnavailableReason.NotEnabled;
            else if (memberId is null)
                reason = CourtSaleUnavailableReason.NoMember;
            else if (await ActiveMembership.IsEnrolledInActiveSeason(context, request.ClubId, memberId.Value, cancellationToken))
                reason = CourtSaleUnavailableReason.AlreadyEnrolled;
            else if (upcoming >= settings.MaxUpcomingBookings)
                reason = CourtSaleUnavailableReason.LimitReached;
            else if (!onlinePayment && credits <= 0 && !await HasFreeRule(request.ClubId, cancellationToken))
                reason = CourtSaleUnavailableReason.PaymentUnavailable;

            return new CourtSaleOptionsResult
            {
                IsEnabled = settings.IsSelling(),
                CanBuy = reason is null,
                Reason = reason,
                IsOnlinePaymentAvailable = onlinePayment,
                Credits = Math.Max(0, credits),
                MaxBookingMinutes = settings.MaxBookingMinutes,
                MaxDaysInAdvance = settings.MaxDaysInAdvance,
                MaxUpcomingBookings = settings.MaxUpcomingBookings,
                UpcomingPurchases = upcoming,
                PlayModeId = settings.PlayModeId,
                TimeZoneId = settings.TimeZoneId,
                Currency = CourtPriceRule.DefaultCurrency,
                MinutesCoveredByCredit = CourtSaleSettings.MinutesCoveredByCredit,
            };
        }

        private Task<bool> HasFreeRule(int clubId, CancellationToken cancellationToken)
            => context.CourtPriceRules.AnyAsync(r => r.ClubId == clubId && r.PricePerHourCents == 0, cancellationToken);
    }
}

/// <summary>The price of a slot for the caller: the lines of the rules, whether a credit pays and what is paid online.</summary>
public record GetCourtPurchaseQuote(int ClubId, int UserId, int CourtId, DateTimeOffset From, DateTimeOffset To) : IQuery<CourtPurchaseQuoteResult>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCourtPurchaseQuote, CourtPurchaseQuoteResult>
    {
        public async Task<CourtPurchaseQuoteResult> Handle(GetCourtPurchaseQuote request, CancellationToken cancellationToken)
        {
            if (!await context.Courts.AnyAsync(c => c.Id == request.CourtId && c.ClubId == request.ClubId, cancellationToken))
                throw new EntityNotFoundException(typeof(Court));

            var settings = await context.GetCourtSaleSettings(request.ClubId, cancellationToken);
            if (request.To < request.From)
                return NotForSale(CourtSaleQuoteUnavailableReason.InvalidDuration);

            var interval = new DateTimeOffsetInterval(request.From.ToUniversalTime(), request.To.ToUniversalTime());
            if (CourtSlotRules.Check(settings, interval, DateTimeOffset.UtcNow) is { } reason)
                return NotForSale(reason);

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken);
            var credits = memberId is { } id ? await context.GetCreditBalance(request.ClubId, id, cancellationToken) : 0;
            var rules = await context.GetCourtPriceRules(request.ClubId, cancellationToken);

            if (CourtSlotRules.Price(rules, settings, request.CourtId, interval, credits) is not { } price)
                return NotForSale(CourtSaleQuoteUnavailableReason.NotForSale);

            return new CourtPurchaseQuoteResult
            {
                IsForSale = true,
                PriceCents = price.Quote.PriceCents,
                CreditUsed = price.CreditUsed,
                AmountDueCents = price.AmountDueCents,
                Currency = price.Quote.Currency,
                Lines = price.Quote.Lines.Select(l => new CourtQuoteLineDto { From = l.From, To = l.To, PricePerHourCents = l.PricePerHourCents, RuleName = l.RuleName }).ToList(),
            };
        }

        private static CourtPurchaseQuoteResult NotForSale(CourtSaleQuoteUnavailableReason reason) => new()
        {
            IsForSale = false,
            Reason = reason,
            PriceCents = 0,
            CreditUsed = false,
            AmountDueCents = 0,
            Currency = CourtPriceRule.DefaultCurrency,
            Lines = [],
        };
    }
}

/// <summary>
/// A member who is not enrolled in the active season buys a slot. The booking (the buyer as the only player, the play mode of the
/// settings) is created right away so the slot is held, after the rules of every booking were checked. A credit or a free rule pays
/// at once; otherwise the buyer is sent to a Stripe payment page that closes after 30 minutes, when the hold is released.
/// </summary>
public record StartCourtPurchase(int ClubId, int UserId, int CourtId, DateTimeOffsetInterval Interval, int? ClientTimeZoneOffset = null) : ICommand<StartCourtPurchaseResult>
{
    /// <summary>Stripe closes a payment page 30 minutes after it was created at the earliest; one more minute for the time until Stripe gets the request.</summary>
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(31);

    public class Handler(
        AppDbContext context,
        IBookingDomainService bookingService,
        IStripeGateway gateway,
        ICourtPurchaseNotifier notifier,
        AppSettings appSettings,
        IUserLanguageAccessor languageAccessor) : IRequestHandler<StartCourtPurchase, StartCourtPurchaseResult>
    {
        public async Task<StartCourtPurchaseResult> Handle(StartCourtPurchase request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var interval = new DateTimeOffsetInterval(request.Interval.From.ToUniversalTime(), request.Interval.To.ToUniversalTime());

            var court = await context.Courts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourtId && c.ClubId == request.ClubId, cancellationToken)
                ?? throw new EntityNotFoundException(typeof(Court));

            var settings = await context.GetCourtSaleSettings(request.ClubId, cancellationToken);
            CourtSlotRules.Ensure(settings, interval, now);

            var memberId = await context.GetClubMemberId(request.UserId, request.ClubId, cancellationToken)
                ?? throw new PreconditionException(CourtPurchaseErrorCode.CourtPurchaseRequiresMember, "Only a member of the club can buy court time.");

            if (await ActiveMembership.IsEnrolledInActiveSeason(context, request.ClubId, memberId, cancellationToken))
                throw new PreconditionException(CourtPurchaseErrorCode.AlreadyEnrolled, "Members of the season book for free.");

            // One purchase of the club after the other: the overlap check and the limits below see every hold
            await context.LockCourts(request.ClubId, cancellationToken);

            if (await context.CountUpcomingPurchases(request.ClubId, memberId, cancellationToken) >= settings.MaxUpcomingBookings)
                throw new PreconditionException(CourtPurchaseErrorCode.CourtPurchaseLimitReached, [settings.MaxUpcomingBookings.ToString(CultureInfo.InvariantCulture)], "Too many upcoming bought bookings.");

            var rules = await context.GetCourtPriceRules(request.ClubId, cancellationToken);
            var credits = await context.GetCreditBalance(request.ClubId, memberId, cancellationToken);
            var price = CourtSlotRules.Price(rules, settings, request.CourtId, interval, credits)
                ?? throw new PreconditionException(CourtPurchaseErrorCode.CourtNotForSale, "The slot is not for sale.");

            // Online payment is checked before anything is held
            string? accountId = null;
            if (price.AmountDueCents > 0)
            {
                if (!gateway.IsConfigured)
                    throw new PaymentsNotConfiguredException();

                var account = await context.ClubPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ClubId == request.ClubId, cancellationToken);
                if (account is not { ChargesEnabled: true })
                    throw new PreconditionException(PaymentErrorCode.PaymentAccountNotReady, "The club can't take online payments at the moment.");

                accountId = account.StripeAccountId;
            }

            // The rules of every booking: opening hours, court inactive or blocked, overlaps, grace period, prime time for bought slots
            var builder = await BookingContextBuilder.Load(context, settings.PlayModeId!.Value, [request.CourtId], [memberId], request.UserId, [interval], cancellationToken);
            var bookingContext = await builder.BuildContext(request.CourtId, interval, request.ClientTimeZoneOffset, [], cancellationToken) with { IsPurchase = true };
            var booking = context.Bookings.Add(bookingService.BookCourt(bookingContext, interval, settings.TimeZoneId, null)).Entity;

            var purchase = new CourtPurchase(request.ClubId, memberId, request.CourtId, booking.Id, interval, price.Quote, price.CreditUsed, price.AmountDueCents);
            context.CourtPurchases.Add(purchase);

            if (price.AmountDueCents == 0)
            {
                purchase.CompleteWithoutPayment(now);
                await context.SaveChangesAsync(cancellationToken);
                await notifier.Confirmed(purchase, cancellationToken);
                return new StartCourtPurchaseResult { PurchaseId = purchase.Id, Status = SharedStatus.Paid };
            }

            var holdExpiresAt = now.Add(HoldDuration);
            var email = await context.Users.Where(u => u.Id == request.UserId).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
            var session = await StripeCalls.Run(() => gateway.CreateCheckoutSession(
                accountId!,
                CheckoutData(purchase, court.Name, settings, holdExpiresAt, email),
                cancellationToken));

            if (session.Url is null)
                throw new PreconditionException(PaymentErrorCode.PaymentProviderUnavailable, "The payment page could not be created.");

            purchase.StartCheckout(session.Id, holdExpiresAt);
            await context.SaveChangesAsync(cancellationToken);

            return new StartCourtPurchaseResult { PurchaseId = purchase.Id, Status = SharedStatus.AwaitingPayment, CheckoutUrl = session.Url };
        }

        private StripeCheckoutData CheckoutData(CourtPurchase purchase, string courtName, CourtSaleSettings settings, DateTimeOffset expiresAt, string? email)
        {
            var culture = languageAccessor.GetCultureInfo();
            var timeZone = settings.GetTimeZone();
            var from = TimeZoneInfo.ConvertTime(purchase.Interval.From, timeZone);
            var to = TimeZoneInfo.ConvertTime(purchase.Interval.To, timeZone);

            // e.g. "Court 2 · Sat, 18 Oct 17:00–18:00"
            var productName = $"{courtName} · {from.ToString("ddd, d MMM", culture)} {from:HH:mm}–{to:HH:mm}";
            var page = $"{appSettings.AppUri.AbsoluteUri.TrimEnd('/')}/clubs/{purchase.ClubId}/booking?purchase={purchase.Id}&date={from:yyyy-MM-dd}";

            return new StripeCheckoutData(
                purchase.ClubId,
                purchase.Id,
                productName,
                purchase.CreditUsed ? Notifications.NotificationTexts.Get(culture, "CourtPurchase_Checkout_CreditUsed") : null,
                purchase.AmountDueCents,
                purchase.Currency,
                email,
                culture.TwoLetterISOLanguageName,
                StartMembershipCheckout.Handler.ApplicationFee(purchase.AmountDueCents, appSettings.Stripe.ApplicationFeePercent),
                $"{page}&checkout=success",
                $"{page}&checkout=cancelled",
                StripeCheckoutKind.CourtPurchase,
                expiresAt);
        }
    }
}

/// <summary>A purchase as its buyer sees it. Polled after coming back from the payment page.</summary>
public record GetCourtPurchase(int ClubId, int PurchaseId) : IQuery<CourtPurchaseDto>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetCourtPurchase, CourtPurchaseDto>
    {
        public async Task<CourtPurchaseDto> Handle(GetCourtPurchase request, CancellationToken cancellationToken)
        {
            var purchase = await context.GetCourtPurchase(request.ClubId, request.PurchaseId, cancellationToken);
            return await purchase.ToDto(context, cancellationToken);
        }
    }
}

/// <summary>
/// The buyer came back from the payment page: asks Stripe how it ended and books a completed payment without waiting for the webhook.
/// Only what Stripe answers counts. Best effort: when Stripe can't be reached, the webhook books it later.
/// </summary>
public record SyncCourtPurchaseCheckout(int ClubId, int PurchaseId) : ICommand
{
    public class Handler(AppDbContext context, ICourtPurchaseSettlement settlement, ILogger<SyncCourtPurchaseCheckout> logger) : IRequestHandler<SyncCourtPurchaseCheckout>
    {
        public async Task<Unit> Handle(SyncCourtPurchaseCheckout request, CancellationToken cancellationToken)
        {
            var purchase = await context.GetCourtPurchase(request.ClubId, request.PurchaseId, cancellationToken);
            if (purchase.Status is not (CourtPurchaseStatus.AwaitingPayment or CourtPurchaseStatus.Expired))
                return default;

            try
            {
                var emails = new List<Func<Task>>();
                if (await settlement.CheckSession(purchase, emails, cancellationToken) != SessionCheck.Settled)
                    return default;

                await context.SaveChangesAsync(cancellationToken);
                foreach (var send in emails)
                    await send();
            }
            catch (DbUpdateConcurrencyException)
            {
                // The webhook booked the payment at the same moment
                context.ChangeTracker.Clear();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "The payment page of court purchase {PurchaseId} could not be synced; the webhook books it.", purchase.Id);
                context.ChangeTracker.Clear();
            }

            return default;
        }
    }
}

/// <summary>
/// The buyer left the payment page without paying: the hold is released at once instead of after 30 minutes. The payment page is
/// closed first; when Stripe says it was completed after all (paid in another tab), the payment is booked instead.
/// Does nothing for a purchase that does not wait for its payment.
/// </summary>
public record ReleaseCourtPurchase(int ClubId, int PurchaseId) : ICommand<CourtPurchaseDto>
{
    public class Handler(AppDbContext context, ICourtPurchaseSettlement settlement, IMembershipCheckout checkout) : IRequestHandler<ReleaseCourtPurchase, CourtPurchaseDto>
    {
        public async Task<CourtPurchaseDto> Handle(ReleaseCourtPurchase request, CancellationToken cancellationToken)
        {
            var purchase = await context.GetCourtPurchase(request.ClubId, request.PurchaseId, cancellationToken);
            if (purchase.Status != CourtPurchaseStatus.AwaitingPayment)
                return await purchase.ToDto(context, cancellationToken);

            var emails = new List<Func<Task>>();
            if (purchase.StripeCheckoutSessionId is { } sessionId)
            {
                await checkout.ExpireSession(purchase.ClubId, sessionId, cancellationToken);
                if (await settlement.CheckSession(purchase, emails, cancellationToken) == SessionCheck.Settled)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    foreach (var send in emails)
                        await send();
                    return await purchase.ToDto(context, cancellationToken);
                }
            }

            // A payment that still arrives is booked again if the slot is free, otherwise the treasurers are told
            await settlement.ReleaseHold(purchase, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return await purchase.ToDto(context, cancellationToken);
        }
    }
}
