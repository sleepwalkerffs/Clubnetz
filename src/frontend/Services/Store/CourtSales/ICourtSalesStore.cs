using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.CourtSales;

namespace Bookennis.Client.Services.Store.CourtSales;

/// <summary>
/// Court sales: members who are not enrolled in the active season buy free court time (booking page), treasurers and admins set the prices,
/// see the sales and give credits (court sales page).
/// </summary>
public interface ICourtSalesStore : ISemaphoreStore
{
    /// <summary>Raised whenever settings, price rules, options, sales or credits were (re)loaded.</summary>
    event Action? OnCourtSalesChanged;

    /// <summary>What the signed-in member can do (booking page); <c>null</c> until it was loaded.</summary>
    CourtSaleOptionsResult? Options { get; }

    /// <summary>The price rules of the club (every member may read them).</summary>
    List<CourtPriceRuleDto> PriceRules { get; }

    /// <summary>Settings for treasurers and admins; <c>null</c> until it was loaded.</summary>
    CourtSaleSettingsDto? Settings { get; }

    GetCourtPurchasesResult? Purchases { get; }
    GetBookingCreditsResult? Credits { get; }

    Task LoadOptions();
    Task LoadPriceRules();
    Task LoadSettings();
    Task LoadPurchases(DateOnly? from, DateOnly? to, CourtPurchaseStatus? status);
    Task LoadCredits();

    Task<HttpResult<CourtSaleSettingsDto>> UpdateSettings(UpdateCourtSaleSettingsModel model);
    Task<HttpResult<CourtPriceRuleDto>> SavePriceRule(int? ruleId, SaveCourtPriceRuleModel model);
    Task<HttpResult> DeletePriceRule(int ruleId);

    /// <summary>The price of a slot for the caller. Does not touch the state of the store (the buy dialog asks while the duration changes).</summary>
    Task<HttpResult<CourtPurchaseQuoteResult>> GetQuote(int courtId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    Task<HttpResult<StartCourtPurchaseResult>> StartPurchase(int courtId, DateTimeOffset from, DateTimeOffset to);

    /// <summary>A purchase (runs the payment sync on the server). Does not touch the state: the booking page polls it after the payment page.</summary>
    Task<HttpResult<CourtPurchaseDto>> GetPurchase(int purchaseId);

    /// <summary>The buyer came back from the payment page without paying: frees the slot at once.</summary>
    Task<HttpResult<CourtPurchaseDto>> ReleasePurchase(int purchaseId);

    Task<HttpResult<CancelCourtPurchaseResult>> CancelPurchase(int purchaseId);
    Task<HttpResult<BookingCreditBalanceDto>> GrantCredits(int memberId, int count, string? note);
    Task<HttpResult<List<BookingCreditCandidateDto>>> GetCreditCandidates(string? search, CancellationToken cancellationToken = default);
}
