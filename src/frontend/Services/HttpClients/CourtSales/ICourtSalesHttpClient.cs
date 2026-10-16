using Bookennis.Shared.Controller.CourtSales;

namespace Bookennis.Client.Services.HttpClients.CourtSales;

/// <summary><c>api/Clubs/{clubId}/CourtSales</c>: prices, buying court time, sales and credits.</summary>
public interface ICourtSalesHttpClient
{
    public Task<HttpResult<CourtSaleSettingsDto>> GetSettings(CancellationToken cancellationToken = default);
    public Task<HttpResult<CourtSaleSettingsDto>> UpdateSettings(UpdateCourtSaleSettingsModel model, CancellationToken cancellationToken = default);

    public Task<HttpResult<List<CourtPriceRuleDto>>> GetPriceRules(CancellationToken cancellationToken = default);
    public Task<HttpResult<CourtPriceRuleDto>> CreatePriceRule(SaveCourtPriceRuleModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<CourtPriceRuleDto>> UpdatePriceRule(int ruleId, SaveCourtPriceRuleModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> DeletePriceRule(int ruleId, CancellationToken cancellationToken = default);

    public Task<HttpResult<CourtSaleOptionsResult>> GetOptions(CancellationToken cancellationToken = default);
    public Task<HttpResult<CourtPurchaseQuoteResult>> GetQuote(int courtId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    public Task<HttpResult<StartCourtPurchaseResult>> StartPurchase(StartCourtPurchaseModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<CourtPurchaseDto>> GetPurchase(int purchaseId, CancellationToken cancellationToken = default);
    public Task<HttpResult<CourtPurchaseDto>> ReleasePurchase(int purchaseId, CancellationToken cancellationToken = default);

    public Task<HttpResult<GetCourtPurchasesResult>> GetPurchases(DateOnly? from, DateOnly? to, CourtPurchaseStatus? status, CancellationToken cancellationToken = default);
    public Task<HttpResult<CancelCourtPurchaseResult>> CancelPurchase(int purchaseId, CancellationToken cancellationToken = default);

    public Task<HttpResult<GetBookingCreditsResult>> GetCredits(CancellationToken cancellationToken = default);
    public Task<HttpResult<BookingCreditBalanceDto>> GrantCredits(GrantBookingCreditsModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<List<BookingCreditCandidateDto>>> GetCreditCandidates(string? search, CancellationToken cancellationToken = default);
}
