using Bookennis.Api.Config;
using Bookennis.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Bookennis.Api.Business.Demo;

public enum DemoErrorCode
{
    /// <summary>The action is switched off in the public demo (<see cref="DisabledInDemoAttribute"/>).</summary>
    DisabledInDemo,
}

/// <summary>
/// Switches an action off in the public demo (<see cref="DemoSettings.Enabled"/>): 412 with <see cref="DemoErrorCode.DisabledInDemo"/>.
/// For what one visitor could use against the next ones: taking over a demo account (password, email, deleting it, leaving the club),
/// new accounts that could never be confirmed (no email leaves the demo) and uploading files that other visitors would see.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DisabledInDemoAttribute : Attribute, IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.RequestServices.GetRequiredService<IOptions<AppSettings>>().Value.Demo.Enabled)
            throw new PreconditionException(DemoErrorCode.DisabledInDemo, "This is switched off in the demo.");

        return next();
    }
}
