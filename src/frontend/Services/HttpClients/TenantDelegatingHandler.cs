namespace Bookennis.Client.Services.HttpClients;

public class TenantDelegatingHandler(ITenantProvider tenantProvider) : DelegatingHandler
{
    internal const string TenantPlaceholder = "__tenant__";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (tenantProvider.HasClubSelected && request.RequestUri?.OriginalString.Contains(TenantPlaceholder) == true)
        {
            var resolved = request.RequestUri.OriginalString
                .Replace(TenantPlaceholder, tenantProvider.CurrentClubId.ToString());
            request.RequestUri = new Uri(resolved);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
