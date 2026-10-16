using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.CourtSales;

namespace Bookennis.Client.Services.HttpClients.CourtSales;

public class CourtSalesHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : ICourtSalesHttpClient
{
    public async Task<HttpResult<CourtSaleSettingsDto>> GetSettings(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("settings", cancellationToken)).AsHttpResult<CourtSaleSettingsDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtSaleSettingsDto>> UpdateSettings(UpdateCourtSaleSettingsModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("settings", model, jsonOptions, cancellationToken)).AsHttpResult<CourtSaleSettingsDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<CourtPriceRuleDto>>> GetPriceRules(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("price-rules", cancellationToken)).AsHttpResult<List<CourtPriceRuleDto>>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtPriceRuleDto>> CreatePriceRule(SaveCourtPriceRuleModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("price-rules", model, jsonOptions, cancellationToken)).AsHttpResult<CourtPriceRuleDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtPriceRuleDto>> UpdatePriceRule(int ruleId, SaveCourtPriceRuleModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"price-rules/{ruleId}", model, jsonOptions, cancellationToken)).AsHttpResult<CourtPriceRuleDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult> DeletePriceRule(int ruleId, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"price-rules/{ruleId}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtSaleOptionsResult>> GetOptions(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("options", cancellationToken)).AsHttpResult<CourtSaleOptionsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtPurchaseQuoteResult>> GetQuote(int courtId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"quote?courtId={courtId}&from={Encode(from)}&to={Encode(to)}", cancellationToken))
            .AsHttpResult<CourtPurchaseQuoteResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<StartCourtPurchaseResult>> StartPurchase(StartCourtPurchaseModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("purchases", model, jsonOptions, cancellationToken)).AsHttpResult<StartCourtPurchaseResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtPurchaseDto>> GetPurchase(int purchaseId, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"purchases/{purchaseId}", cancellationToken)).AsHttpResult<CourtPurchaseDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<CourtPurchaseDto>> ReleasePurchase(int purchaseId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"purchases/{purchaseId}/release", null, cancellationToken)).AsHttpResult<CourtPurchaseDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetCourtPurchasesResult>> GetPurchases(DateOnly? from, DateOnly? to, CourtPurchaseStatus? status, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from is { } f)
            query.Add($"from={f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is { } t)
            query.Add($"to={t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (status is { } s)
            query.Add($"status={s}");

        var url = query.Count == 0 ? "purchases" : "purchases?" + string.Join("&", query);
        return await (await httpClient.GetAsync(url, cancellationToken)).AsHttpResult<GetCourtPurchasesResult>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<CancelCourtPurchaseResult>> CancelPurchase(int purchaseId, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsync($"purchases/{purchaseId}/cancel", null, cancellationToken)).AsHttpResult<CancelCourtPurchaseResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetBookingCreditsResult>> GetCredits(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("credits", cancellationToken)).AsHttpResult<GetBookingCreditsResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult<BookingCreditBalanceDto>> GrantCredits(GrantBookingCreditsModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("credits", model, jsonOptions, cancellationToken)).AsHttpResult<BookingCreditBalanceDto>(jsonOptions, cancellationToken);

    public async Task<HttpResult<List<BookingCreditCandidateDto>>> GetCreditCandidates(string? search, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(string.IsNullOrWhiteSpace(search) ? "credits/members" : $"credits/members?search={Uri.EscapeDataString(search)}", cancellationToken))
            .AsHttpResult<List<BookingCreditCandidateDto>>(jsonOptions, cancellationToken);

    /// <summary>A point in time in UTC in the round-trip format, escaped for the query string.</summary>
    private static string Encode(DateTimeOffset value) => Uri.EscapeDataString(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
