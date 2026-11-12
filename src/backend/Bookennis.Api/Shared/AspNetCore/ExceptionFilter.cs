using System.Net;
using System.Text.Json;
using Bookennis.Api.Infrastructure.Audit;
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
        await HandleException<EntityNotFoundException>(context, HttpStatusCode.NotFound, e => new { e.Message });
        await HandleException<PreconditionException>(context, HttpStatusCode.PreconditionFailed, e => new ErrorCodeResponse(e.Message, e.ErrorCode, e.ErrorDetails));
        // A failed call to another service (Stripe, SumUp, weather, ...) is a bad gateway. Passing on its status would let a 401 of
        // that service look like an expired session to the client.
        await HandleException<HttpRequestException>(context, HttpStatusCode.BadGateway);
        await HandleException<AuthorizationFailedException>(context, HttpStatusCode.Unauthorized, e => new { e.Message });
        // Invalid input that fails the data annotations of a request: a message for the user, not a 500
        await HandleException<Fusonic.Extensions.AspNetCore.Validation.RequestValidationException>(context, HttpStatusCode.PreconditionFailed, e => new ErrorCodeResponse(e.Message, "InvalidInput", null));
    }

    protected Task HandleException<T>(ExceptionContext context, HttpStatusCode httpStatusCode, Func<T, object?>? getResponse = null) => HandleException(context, _ => httpStatusCode, getResponse);

    protected async Task HandleException<T>(ExceptionContext context, Func<T, HttpStatusCode> getHttpStatusCode, Func<T, object?>? getResponse = null)
    {
        if (context.ExceptionHandled || context.Exception is not T ex)
            return;

        var httpStatusCode = getHttpStatusCode(ex);

        context.ExceptionHandled = true;
        context.HttpContext.Response.StatusCode = (int)httpStatusCode;

        // Expected answers to a client (not signed in, not found, invalid input) need no stack trace in the log; a failing service does
        if ((int)httpStatusCode >= 500)
            logger.LogError(context.Exception, "Handling exception with status code {HttpStatusCode}.", httpStatusCode);
        else if (httpStatusCode == HttpStatusCode.Unauthorized)
            logger.LogInformation("Handling {ExceptionType} with status code {HttpStatusCode}: {Message}", typeof(T).Name, httpStatusCode, context.Exception.Message);
        else
            logger.LogWarning("Handling {ExceptionType} with status code {HttpStatusCode}: {Message}", typeof(T).Name, httpStatusCode, context.Exception.Message);

        if (context.HttpContext.Response.HasStarted)
            return;

        var response = getResponse?.Invoke(ex);
        if (response != null)
            await context.HttpContext.Response.WriteAsJsonAsync(response, JsonSerializerOptions);
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
            // Denied by a resource check in a controller. A command that was denied is logged by its decorator already.
            if (!context.Exception.Data.Contains(AccessDeniedAudit.LoggedKey))
                await AccessDeniedAudit.Write(context.HttpContext.RequestServices, context.Exception.Message);

            await HandleException<AccessDeniedException>(context, HttpStatusCode.Forbidden, e => new { e.Message });
        }
    }
}
