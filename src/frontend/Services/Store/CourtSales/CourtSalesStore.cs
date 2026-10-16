using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.CourtSales;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.CourtSales;

namespace Bookennis.Client.Services.Store.CourtSales;

public class CourtSalesStore(ICourtSalesHttpClient httpClient) : SemaphoreStore, ICourtSalesStore
{
    public event Action? OnCourtSalesChanged;

    public CourtSaleOptionsResult? Options { get; private set; }
    public List<CourtPriceRuleDto> PriceRules { get; private set; } = [];
    public CourtSaleSettingsDto? Settings { get; private set; }
    public GetCourtPurchasesResult? Purchases { get; private set; }
    public GetBookingCreditsResult? Credits { get; private set; }

    public Task LoadOptions() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetOptions(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Options = response.Dto;
            OnCourtSalesChanged?.Invoke();
        }
    }, nameof(LoadOptions));

    public Task LoadPriceRules() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetPriceRules(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            PriceRules = response.Dto;
            OnCourtSalesChanged?.Invoke();
        }
    }, nameof(LoadPriceRules));

    public Task LoadSettings() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetSettings(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Settings = response.Dto;
            OnCourtSalesChanged?.Invoke();
        }
    }, nameof(LoadSettings));

    public Task LoadPurchases(DateOnly? from, DateOnly? to, CourtPurchaseStatus? status) => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetPurchases(from, to, status, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Purchases = response.Dto;
            OnCourtSalesChanged?.Invoke();
        }
    }, nameof(LoadPurchases));

    public Task LoadCredits() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetCredits(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Credits = response.Dto;
            OnCourtSalesChanged?.Invoke();
        }
    }, nameof(LoadCredits));

    public async Task<HttpResult<CourtSaleSettingsDto>> UpdateSettings(UpdateCourtSaleSettingsModel model)
        => (HttpResult<CourtSaleSettingsDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.UpdateSettings(model, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Settings = result.Dto;
                OnCourtSalesChanged?.Invoke();
            }

            return result;
        }, nameof(UpdateSettings));

    public async Task<HttpResult<CourtPriceRuleDto>> SavePriceRule(int? ruleId, SaveCourtPriceRuleModel model)
        => (HttpResult<CourtPriceRuleDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = ruleId is { } id
                ? await httpClient.UpdatePriceRule(id, model, cancellationToken)
                : await httpClient.CreatePriceRule(model, cancellationToken);
            if (result.Success)
                await LoadPriceRules();

            return result;
        }, nameof(SavePriceRule));

    public Task<HttpResult> DeletePriceRule(int ruleId)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.DeletePriceRule(ruleId, cancellationToken);
            if (result.Success)
                await LoadPriceRules();

            return result;
        }, nameof(DeletePriceRule));

    public Task<HttpResult<CourtPurchaseQuoteResult>> GetQuote(int courtId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => httpClient.GetQuote(courtId, from, to, cancellationToken);

    public async Task<HttpResult<StartCourtPurchaseResult>> StartPurchase(int courtId, DateTimeOffset from, DateTimeOffset to)
        => (HttpResult<StartCourtPurchaseResult>)await RunInSavingContextAsync(
            async cancellationToken => await httpClient.StartPurchase(new StartCourtPurchaseModel { CourtId = courtId, From = from, To = to }, cancellationToken),
            nameof(StartPurchase));

    public Task<HttpResult<CourtPurchaseDto>> GetPurchase(int purchaseId) => httpClient.GetPurchase(purchaseId);

    public async Task<HttpResult<CourtPurchaseDto>> ReleasePurchase(int purchaseId)
        => (HttpResult<CourtPurchaseDto>)await RunInSavingContextAsync(async cancellationToken => await httpClient.ReleasePurchase(purchaseId, cancellationToken), nameof(ReleasePurchase));

    public async Task<HttpResult<CancelCourtPurchaseResult>> CancelPurchase(int purchaseId)
        => (HttpResult<CancelCourtPurchaseResult>)await RunInSavingContextAsync(async cancellationToken => await httpClient.CancelPurchase(purchaseId, cancellationToken), nameof(CancelPurchase));

    public async Task<HttpResult<BookingCreditBalanceDto>> GrantCredits(int memberId, int count, string? note)
        => (HttpResult<BookingCreditBalanceDto>)await RunInSavingContextAsync(async cancellationToken =>
        {
            var result = await httpClient.GrantCredits(new GrantBookingCreditsModel { MemberId = memberId, Count = count, Note = note }, cancellationToken);
            if (result.Success)
                await LoadCredits();

            return result;
        }, nameof(GrantCredits));

    public Task<HttpResult<List<BookingCreditCandidateDto>>> GetCreditCandidates(string? search, CancellationToken cancellationToken = default)
        => httpClient.GetCreditCandidates(search, cancellationToken);
}
