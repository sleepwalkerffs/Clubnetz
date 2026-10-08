using System.Net;
using System.Text.Json;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Domain.Exceptions;
using Bookennis.Shared.Controller.Shared;
using Fusonic.Extensions.Common.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bookennis.Api.Shared.AspNetCore;

public class ExceptionFilter(ILogger<ExceptionFilter> logger) : IAsyncExceptionFilter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger logger = logger;

    protected virtual JsonSerializerOptions JsonSerializerOptions => SerializerOptions;

    public virtual async Task OnExceptionAsync(ExceptionContext context)
    {
        await HandleAccessDenied(context);
        HandleException<EntityNotFoundException>(context, HttpStatusCode.NotFound, e => new { e.Message });
        HandleException<PreconditionException>(context, HttpStatusCode.PreconditionFailed, e => new ErrorCodeResponse(e.Message, e.ErrorCode, e.ErrorDetails));
        HandleException<HttpRequestException>(context, e => e.StatusCode ?? HttpStatusCode.InternalServerError);
        HandleException<AuthorizationFailedException>(context, HttpStatusCode.Unauthorized, e => new { e.Message });
    }

    protected void HandleException<T>(ExceptionContext context, HttpStatusCode httpStatusCode, Func<T, object?>? getResponse = null) => HandleException(context, _ => httpStatusCode, getResponse);

    protected void HandleException<T>(ExceptionContext context, Func<T, HttpStatusCode> getHttpStatusCode, Func<T, object?>? getResponse = null)
    {
        if (context.ExceptionHandled || context.Exception is not T ex)
            return;

        var httpStatusCode = getHttpStatusCode(ex);

        context.ExceptionHandled = true;
        context.HttpContext.Response.StatusCode = (int)httpStatusCode;

        logger.LogWarning(context.Exception, "Handling exception with status code {HttpStatusCode}.", httpStatusCode);

        if (context.HttpContext.Response.HasStarted)
            return;

        var response = getResponse?.Invoke(ex);
        if (response != null)
            context.HttpContext.Response.WriteAsJsonAsync(response, JsonSerializerOptions).Wait();
    }

    private async Task HandleAccessDenied(ExceptionContext context)
    {
        if (context.ExceptionHandled || context.Exception is not AccessDeniedException)
            return;

        if (context.HttpContext.User.Identity is not { IsAuthenticated: true })
        {
            context.ExceptionHandled = true;
            await context.HttpContext.ChallengeAsync();
        }
        else
        {
            HandleException<AccessDeniedException>(context, HttpStatusCode.Forbidden, e => new { e.Message });
        }
    }
}
