using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Api.Config;

namespace Bookennis.Api.Business.Clubhouse.SumUp;

/// <summary>The REST API of SumUp (<see cref="SumUpSettings.ApiUrl"/>): OAuth (authorization code flow) and checkouts.</summary>
public sealed class SumUpGateway(AppSettings appSettings, ILogger<SumUpGateway> logger) : ISumUpGateway, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    private SumUpSettings Settings => appSettings.SumUp;
    private string ApiUrl => Settings.ApiUrl.TrimEnd('/');

    public bool IsConfigured => Settings.IsConfigured;

    public void Dispose() => httpClient.Dispose();

    public string AuthorizeUrl(string state, string redirectUri)
    {
        EnsureConfigured();
        var query = string.Join("&", new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = Settings.ClientId!,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Settings.Scopes,
            ["state"] = state,
        }.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{ApiUrl}/authorize?{query}";
    }

    public Task<SumUpTokens> ExchangeCode(string code, string redirectUri, CancellationToken cancellationToken)
        => RequestTokens(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        }, cancellationToken);

    public Task<SumUpTokens> RefreshTokens(string refreshToken, CancellationToken cancellationToken)
        => RequestTokens(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, cancellationToken);

    public async Task<SumUpMerchant> GetMerchant(string accessToken, CancellationToken cancellationToken)
    {
        using var response = await Send(HttpMethod.Get, "/v0.1/me", accessToken, null, cancellationToken);
        await EnsureSuccess(response, "read the merchant profile", cancellationToken);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(Json, cancellationToken);
        var code = me?.MerchantProfile?.MerchantCode;
        if (string.IsNullOrWhiteSpace(code))
            throw new SumUpException("SumUp returned no merchant code for the account.");

        return new SumUpMerchant(code, me!.MerchantProfile!.CompanyName);
    }

    public async Task<SumUpCheckout> CreateCheckout(string accessToken, SumUpCheckoutData data, CancellationToken cancellationToken)
    {
        var body = new CreateCheckoutRequest(
            data.Reference,
            ToMajorUnits(data.AmountCents),
            data.Currency.ToUpperInvariant(),
            data.MerchantCode,
            data.Description,
            data.ReturnUrl,
            data.RedirectUrl,
            data.ValidUntil.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            new HostedCheckout(true));

        using var response = await Send(HttpMethod.Post, "/v0.1/checkouts", accessToken, JsonContent.Create(body, options: Json), cancellationToken);
        await EnsureSuccess(response, "create a checkout", cancellationToken);

        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(Json, cancellationToken)
            ?? throw new SumUpException("SumUp returned no checkout.");
        if (string.IsNullOrWhiteSpace(checkout.HostedCheckoutUrl))
            throw new SumUpException("SumUp returned no hosted checkout page for the checkout.");

        return checkout.ToCheckout();
    }

    public async Task<SumUpCheckout?> GetCheckout(string accessToken, string checkoutId, CancellationToken cancellationToken)
    {
        using var response = await Send(HttpMethod.Get, $"/v0.1/checkouts/{Uri.EscapeDataString(checkoutId)}", accessToken, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccess(response, "read a checkout", cancellationToken);
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(Json, cancellationToken);
        return checkout?.ToCheckout();
    }

    public async Task DeactivateCheckout(string accessToken, string checkoutId, CancellationToken cancellationToken)
    {
        using var response = await Send(HttpMethod.Delete, $"/v0.1/checkouts/{Uri.EscapeDataString(checkoutId)}", accessToken, null, cancellationToken);

        // Already paid, expired or gone: nothing to close
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
            return;

        await EnsureSuccess(response, "deactivate a checkout", cancellationToken);
    }

    private async Task<SumUpTokens> RequestTokens(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        form["client_id"] = Settings.ClientId!;
        form["client_secret"] = Settings.ClientSecret!;

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync($"{ApiUrl}/token", new FormUrlEncodedContent(form), cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException { InnerException: TimeoutException })
        {
            throw new SumUpException("SumUp could not be reached.", e);
        }

        using (response)
        {
            // invalid_grant & co: the code or the refresh token is not (or no longer) valid
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                var error = await ReadError(response, cancellationToken);
                throw new SumUpAuthorizationException($"SumUp refused the token request: {error}");
            }

            await EnsureSuccess(response, "get tokens", cancellationToken);
            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken);
            if (string.IsNullOrWhiteSpace(tokens?.AccessToken))
                throw new SumUpException("SumUp returned no access token.");

            return new SumUpTokens(
                tokens.AccessToken,
                DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn is > 0 ? tokens.ExpiresIn.Value : 3600),
                string.IsNullOrWhiteSpace(tokens.RefreshToken) ? null : tokens.RefreshToken,
                tokens.Scope);
        }
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string accessToken, HttpContent? content, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(method, ApiUrl + path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException { InnerException: TimeoutException })
        {
            throw new SumUpException("SumUp could not be reached.", e);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new SumUpAuthorizationException("SumUp refused the access token.");
        }

        return response;
    }

    private async Task EnsureSuccess(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await ReadError(response, cancellationToken);
        logger.LogWarning("SumUp refused to {What}: {Status} {Error}", what, (int)response.StatusCode, error);
        throw new SumUpException($"SumUp refused to {what}: {(int)response.StatusCode} {error}");
    }

    /// <summary>The error code of SumUp without the rest of the body (it may echo what was sent).</summary>
    private static async Task<string> ReadError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(Json, cancellationToken);
            return error?.ErrorCode ?? error?.Error ?? error?.ErrorMessage ?? "";
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            return "";
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("SumUp is not configured.");
    }

    private static decimal ToMajorUnits(int cents) => cents / 100m;

    private static int ToCents(decimal amount) => (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private sealed record CreateCheckoutRequest(
        string CheckoutReference,
        decimal Amount,
        string Currency,
        string MerchantCode,
        string Description,
        string ReturnUrl,
        string RedirectUrl,
        string ValidUntil,
        HostedCheckout HostedCheckout);

    private sealed record HostedCheckout(bool Enabled);

    private sealed record CheckoutResponse(
        string Id,
        string? CheckoutReference,
        string? MerchantCode,
        decimal Amount,
        string? Currency,
        string? Status,
        string? HostedCheckoutUrl,
        List<TransactionResponse>? Transactions)
    {
        public SumUpCheckout ToCheckout() => new(
            Id,
            CheckoutReference,
            MerchantCode,
            ToCents(Amount),
            Currency,
            Status ?? "",
            HostedCheckoutUrl,
            Transactions?.FirstOrDefault(t => t.Status == "SUCCESSFUL")?.TransactionCode);
    }

    private sealed record TransactionResponse(string? Status, string? TransactionCode);

    private sealed record TokenResponse(string? AccessToken, int? ExpiresIn, string? RefreshToken, string? Scope);

    private sealed record MeResponse(MerchantProfileResponse? MerchantProfile);

    private sealed record MerchantProfileResponse(string? MerchantCode, string? CompanyName);

    private sealed record ErrorResponse(string? ErrorCode, string? Error, string? ErrorMessage);
}
