using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.HttpClients.ClubEmailTemplates;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubEmailTemplates;

namespace Bookennis.Client.Services.Store.ClubEmailTemplates;

public class ClubEmailTemplatesStore(IClubEmailTemplatesHttpClient httpClient) : SemaphoreStore, IClubEmailTemplatesStore
{
    public event Action? OnTemplatesChanged;
    public event Action? OnTemplateChanged;

    public GetClubEmailTemplatesResult? Templates { get; private set; }
    public GetClubEmailTemplateResult? Template { get; private set; }

    public Task LoadTemplates() => RunInLoadingContextAsync(async cancellationToken =>
    {
        var response = await httpClient.GetClubEmailTemplates(cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Templates = response.Dto;
            OnTemplatesChanged?.Invoke();
        }
    }, nameof(LoadTemplates));

    public Task LoadTemplate(ClubEmailType type) => RunInLoadingContextAsync(async cancellationToken =>
    {
        if (Template?.Type != type)
        {
            Template = null;
            OnTemplateChanged?.Invoke();
        }

        var response = await httpClient.GetClubEmailTemplate(type, cancellationToken);
        if (response is { Success: true, Dto: not null })
        {
            Template = response.Dto;
            OnTemplateChanged?.Invoke();
        }
    }, nameof(LoadTemplate));

    public Task<HttpResult> UpdateSettings(UpdateClubEmailSettingsModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.UpdateClubEmailSettings(model, cancellationToken);
            if (response.Success)
                await LoadTemplates();
            return response;
        }, nameof(UpdateSettings));

    public Task<HttpResult> UpdateTemplate(ClubEmailType type, UpdateClubEmailTemplateModel model)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.UpdateClubEmailTemplate(type, model, cancellationToken);
            if (response.Success)
                await Task.WhenAll(LoadTemplate(type), LoadTemplates());
            return response;
        }, nameof(UpdateTemplate));

    public Task<HttpResult> ResetTemplate(ClubEmailType type)
        => RunInSavingContextAsync(async cancellationToken =>
        {
            var response = await httpClient.ResetClubEmailTemplate(type, cancellationToken);
            if (response.Success)
                await Task.WhenAll(LoadTemplate(type), LoadTemplates());
            return response;
        }, nameof(ResetTemplate));

    // A read: a newer preview request cancels the previous one.
    public async Task<HttpResult<PreviewClubEmailTemplateResult>> PreviewTemplate(ClubEmailType type, ClubEmailTemplateContentModel model)
    {
        HttpResult<PreviewClubEmailTemplateResult>? result = null;
        await RunInLoadingContextAsync(async cancellationToken =>
        {
            result = await httpClient.PreviewClubEmailTemplate(type, model, cancellationToken);
        }, nameof(PreviewTemplate));
        return result!;
    }

    public Task<HttpResult> SendTestEmail(ClubEmailType type, ClubEmailTemplateContentModel model)
        => RunInSavingContextAsync(cancellationToken => httpClient.SendTestClubEmail(type, model, cancellationToken), nameof(SendTestEmail));
}
