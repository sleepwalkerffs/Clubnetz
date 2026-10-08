using Microsoft.AspNetCore.Components;

namespace Bookennis.Client.Services;

public class RedirectToLoginOnUnauthorizedHandler(NavigationManager navigationManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized)
            navigationManager.NavigateToLoginAndReturn();

        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden)
            navigationManager.NavigateTo("/accessdenied");

        if (response.StatusCode is System.Net.HttpStatusCode.InternalServerError)
            navigationManager.NavigateTo("/error");

        return response;
    }
}