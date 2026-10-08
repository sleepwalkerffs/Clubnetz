using System.Net.Http.Json;
using System.Text.Json;
using Bookennis.Shared.Controller.ClubEmailTemplates;

namespace Bookennis.Client.Services.HttpClients.ClubEmailTemplates;

public class ClubEmailTemplatesHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IClubEmailTemplatesHttpClient
{
    public async Task<HttpResult<GetClubEmailTemplatesResult>> GetClubEmailTemplates(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("", cancellationToken)).AsHttpResult<GetClubEmailTemplatesResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateClubEmailSettings(UpdateClubEmailSettingsModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync("settings", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetClubEmailTemplateResult>> GetClubEmailTemplate(ClubEmailType type, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync($"{type}", cancellationToken)).AsHttpResult<GetClubEmailTemplateResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> UpdateClubEmailTemplate(ClubEmailType type, UpdateClubEmailTemplateModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PutAsJsonAsync($"{type}", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ResetClubEmailTemplate(ClubEmailType type, CancellationToken cancellationToken = default)
        => await (await httpClient.DeleteAsync($"{type}", cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<PreviewClubEmailTemplateResult>> PreviewClubEmailTemplate(ClubEmailType type, ClubEmailTemplateContentModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"{type}/preview", model, jsonOptions, cancellationToken)).AsHttpResult<PreviewClubEmailTemplateResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> SendTestClubEmail(ClubEmailType type, ClubEmailTemplateContentModel model, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync($"{type}/test", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}
