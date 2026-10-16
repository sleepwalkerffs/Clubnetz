using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.CourtSales;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.CourtSales;

/// <summary>
/// Court sales: members who are not enrolled in the active season buy free court time (paid online, with a booking credit or free).
/// Treasurers and admins set the prices, see the sales and give credits. Signed-in users only, never with an API key: it is about money
/// and holds on courts. A purchase is only open to its buyer and the treasurers (<see cref="AuthorizationPolicies.OwnsCourtPurchase"/>).
/// </summary>
public class CourtSalesController(IMediator mediator, IUserAccessor userAccessor, IAuthorizationService authorizationService) : ClubControllerBase
{
    // ---------- settings and prices ----------

    [HttpGet("settings")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<CourtSaleSettingsDto> GetCourtSaleSettings(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtSaleSettings(clubId), cancellationToken);

    [HttpPut("settings")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<CourtSaleSettingsDto> UpdateCourtSaleSettings(int clubId, [FromBody] UpdateCourtSaleSettingsModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateCourtSaleSettings(clubId, model), cancellationToken);

    /// <summary>The price rules; every member may read them.</summary>
    [HttpGet("price-rules")]
    [Authorize(AuthorizationPolicies.CourtSalesMember)]
    public Task<List<CourtPriceRuleDto>> GetCourtPriceRules(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtPriceRules(clubId), cancellationToken);

    [HttpPost("price-rules")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<CourtPriceRuleDto> CreateCourtPriceRule(int clubId, [FromBody] SaveCourtPriceRuleModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateCourtPriceRule(clubId, model), cancellationToken);

    [HttpPut("price-rules/{ruleId:int}")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<CourtPriceRuleDto> UpdateCourtPriceRule(int clubId, int ruleId, [FromBody] SaveCourtPriceRuleModel model, CancellationToken cancellationToken)
        => mediator.Send(new UpdateCourtPriceRule(clubId, ruleId, model), cancellationToken);

    [HttpDelete("price-rules/{ruleId:int}")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task DeleteCourtPriceRule(int clubId, int ruleId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteCourtPriceRule(clubId, ruleId), cancellationToken);

    // ---------- buying ----------

    /// <summary>What the caller can do: whether they can buy, their credits, the limits.</summary>
    [HttpGet("options")]
    [Authorize(AuthorizationPolicies.CourtSalesMember)]
    public Task<CourtSaleOptionsResult> GetCourtSaleOptions(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtSaleOptions(clubId, userAccessor.GetUserId()), cancellationToken);

    /// <summary>The price of a slot for the caller.</summary>
    [HttpGet("quote")]
    [Authorize(AuthorizationPolicies.CourtSalesMember)]
    public Task<CourtPurchaseQuoteResult> GetCourtPurchaseQuote(int clubId, [FromQuery] int courtId, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtPurchaseQuote(clubId, userAccessor.GetUserId(), courtId, from, to), cancellationToken);

    /// <summary>Buys a slot for the caller. Returns the payment page unless a credit or a free rule paid for it.</summary>
    [HttpPost("purchases")]
    [Authorize(AuthorizationPolicies.CourtSalesMember)]
    public Task<StartCourtPurchaseResult> StartCourtPurchase(int clubId, [FromBody] StartCourtPurchaseModel model, CancellationToken cancellationToken)
    {
        if (model.To < model.From)
            throw new Domain.Exceptions.PreconditionException(CourtPurchaseErrorCode.CourtPurchaseDurationInvalid, "Invalid slot.");

        var interval = new DateTimeOffsetInterval(model.From.ToUniversalTime(), model.To.ToUniversalTime());
        return mediator.Send(new StartCourtPurchase(clubId, userAccessor.GetUserId(), model.CourtId, interval, HttpContext.GetRequestTimeZoneOffset()), cancellationToken);
    }

    /// <summary>A purchase of the caller (or any purchase for treasurers). While it waits for its payment, Stripe is asked how it went.</summary>
    [HttpGet("purchases/{purchaseId:int}")]
    [Authorize(AuthorizationPolicies.CourtSalesMember)]
    public async Task<CourtPurchaseDto> GetCourtPurchase(int clubId, int purchaseId, CancellationToken cancellationToken)
    {
        await authorizationService.AuthorizeAsync(User, new CourtPurchaseAuthorizationModel(purchaseId), AuthorizationPolicies.OwnsCourtPurchase).EnsureSucceeded();
        await mediator.Send(new SyncCourtPurchaseCheckout(clubId, purchaseId), cancellationToken);
        return await mediator.Send(new GetCourtPurchase(clubId, purchaseId), cancellationToken);
    }

    /// <summary>The buyer left the payment page without paying: the slot is given free at once.</summary>
    [HttpPost("purchases/{purchaseId:int}/release")]
    [Authorize(AuthorizationPolicies.CourtSalesMember)]
    public async Task<CourtPurchaseDto> ReleaseCourtPurchase(int clubId, int purchaseId, CancellationToken cancellationToken)
    {
        await authorizationService.AuthorizeAsync(User, new CourtPurchaseAuthorizationModel(purchaseId, BuyerOnly: true), AuthorizationPolicies.OwnsCourtPurchase).EnsureSucceeded();
        return await mediator.Send(new ReleaseCourtPurchase(clubId, purchaseId), cancellationToken);
    }

    // ---------- sales and credits (treasurers) ----------

    /// <summary>The purchases of the club by the date of the slot (club time zone) and status.</summary>
    [HttpGet("purchases")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<GetCourtPurchasesResult> GetCourtPurchases(int clubId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] CourtPurchaseStatus? status, CancellationToken cancellationToken)
        => mediator.Send(new GetCourtPurchases(clubId, from, to, status), cancellationToken);

    /// <summary>Cancels a purchase. Money paid online is refunded in the Stripe dashboard; the result tells when.</summary>
    [HttpPost("purchases/{purchaseId:int}/cancel")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<CancelCourtPurchaseResult> CancelCourtPurchase(int clubId, int purchaseId, CancellationToken cancellationToken)
        => mediator.Send(new CancelCourtPurchase(clubId, purchaseId, userAccessor.GetUserId()), cancellationToken);

    [HttpGet("credits")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<GetBookingCreditsResult> GetBookingCredits(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetBookingCredits(clubId), cancellationToken);

    /// <summary>Gives a member credits, or takes some back with a negative count.</summary>
    [HttpPost("credits")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<BookingCreditBalanceDto> GrantBookingCredits(int clubId, [FromBody] GrantBookingCreditsModel model, CancellationToken cancellationToken)
        => mediator.Send(new GrantBookingCredits(clubId, userAccessor.GetUserId(), model.MemberId, model.Count, model.Note), cancellationToken);

    /// <summary>Members to give credits to, by name.</summary>
    [HttpGet("credits/members")]
    [Authorize(AuthorizationPolicies.CourtSalesManager)]
    public Task<List<BookingCreditCandidateDto>> GetBookingCreditCandidates(int clubId, [FromQuery] string? search, CancellationToken cancellationToken)
        => mediator.Send(new GetBookingCreditCandidates(clubId, search), cancellationToken);
}
