using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.ClubEmailTemplates;

namespace Bookennis.Client.Services.Store.ClubEmailTemplates;

public interface IClubEmailTemplatesStore : ISemaphoreStore
{
    event Action? OnTemplatesChanged;
    event Action? OnTemplateChanged;

    GetClubEmailTemplatesResult? Templates { get; }

    /// <summary>The template currently opened in the editor.</summary>
    GetClubEmailTemplateResult? Template { get; }

    Task LoadTemplates();
    Task LoadTemplate(ClubEmailType type);
    Task<HttpResult> UpdateSettings(UpdateClubEmailSettingsModel model);
    Task<HttpResult> UpdateTemplate(ClubEmailType type, UpdateClubEmailTemplateModel model);
    Task<HttpResult> ResetTemplate(ClubEmailType type);
    Task<HttpResult<PreviewClubEmailTemplateResult>> PreviewTemplate(ClubEmailType type, ClubEmailTemplateContentModel model);
    Task<HttpResult> SendTestEmail(ClubEmailType type, ClubEmailTemplateContentModel model);
}
