using Bookennis.Shared.Controller.ClubEmailTemplates;

namespace Bookennis.Client.Services.HttpClients.ClubEmailTemplates;

public interface IClubEmailTemplatesHttpClient
{
    public Task<HttpResult<GetClubEmailTemplatesResult>> GetClubEmailTemplates(CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateClubEmailSettings(UpdateClubEmailSettingsModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetClubEmailTemplateResult>> GetClubEmailTemplate(ClubEmailType type, CancellationToken cancellationToken = default);
    public Task<HttpResult> UpdateClubEmailTemplate(ClubEmailType type, UpdateClubEmailTemplateModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> ResetClubEmailTemplate(ClubEmailType type, CancellationToken cancellationToken = default);
    public Task<HttpResult<PreviewClubEmailTemplateResult>> PreviewClubEmailTemplate(ClubEmailType type, ClubEmailTemplateContentModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> SendTestClubEmail(ClubEmailType type, ClubEmailTemplateContentModel model, CancellationToken cancellationToken = default);
}
