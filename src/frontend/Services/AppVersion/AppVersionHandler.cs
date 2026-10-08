using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.AppVersion;

/// <summary>Reads the app version from every API response.</summary>
public class AppVersionHandler(IAppVersionService appVersionService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues(AppVersionHeader.Name, out var values))
            appVersionService.ReportServerVersion(values.FirstOrDefault() ?? "");

        return response;
    }
}
