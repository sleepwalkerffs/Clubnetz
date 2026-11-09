using System.Net;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Shared.Controller.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bookennis.Api.Infrastructure.Authorization;

/// <summary>
/// A read-only club API key may only read: every request that is not a GET (or HEAD) is rejected, whatever the endpoint is.
/// The refusal is recorded in the audit log, like the refusals of the authorization policies.
/// </summary>
public class ClubApiKeyReadOnlyFilter : IAsyncAuthorizationFilter
{
    public const string ErrorCode = "ApiKeyReadOnly";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!context.HttpContext.User.IsReadOnlyClubApiKey() || HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            return;

        await AccessDeniedAudit.Write(context.HttpContext.RequestServices, ErrorCode);
        context.Result = new ObjectResult(new ErrorCodeResponse("This API key is read-only.", ErrorCode, null))
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };
    }
}
