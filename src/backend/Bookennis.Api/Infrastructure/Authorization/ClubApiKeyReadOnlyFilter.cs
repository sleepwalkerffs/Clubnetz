using System.Net;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Shared.Controller.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bookennis.Api.Infrastructure.Authorization;

/// <summary>A read-only club API key may only read: every request that is not a GET (or HEAD) is rejected, whatever the endpoint is.</summary>
public class ClubApiKeyReadOnlyFilter : IAuthorizationFilter
{
    public const string ErrorCode = "ApiKeyReadOnly";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!context.HttpContext.User.IsReadOnlyClubApiKey() || HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            return;

        context.Result = new ObjectResult(new ErrorCodeResponse("This API key is read-only.", ErrorCode, null))
        {
            StatusCode = (int)HttpStatusCode.Forbidden,
        };
    }
}
