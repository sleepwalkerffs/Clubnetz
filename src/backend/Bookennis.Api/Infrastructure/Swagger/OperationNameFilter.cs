using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookennis.Api.Infrastructure.Swagger;

/// <summary>
/// Gives every operation an id (the action name) and, where the action has no XML summary, a readable one derived from that name
/// (<c>GetClubAnnouncements</c> becomes "Get club announcements"), so the API reference does not consist of bare routes.
/// </summary>
public partial class OperationNameFilter : IOperationFilter
{
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex WordBoundary();

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor action)
            return;

        operation.OperationId ??= $"{action.ControllerName}_{action.ActionName}";

        if (string.IsNullOrWhiteSpace(operation.Summary))
            operation.Summary = Humanize(action.ActionName);
    }

    public static string Humanize(string actionName)
    {
        var words = WordBoundary().Split(actionName);
        return string.Join(' ', words.Select((word, index) => index == 0 ? word : word.ToLowerInvariant()));
    }
}
