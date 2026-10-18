using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Bookennis.Api.Tests.Security;

/// <summary>
/// One controller action of the running app. The security tests don't keep a list of endpoints: they read all of them from the app,
/// so an endpoint that is added later is attacked like the existing ones.
/// </summary>
public sealed record ApiEndpoint(string Method, RoutePattern Pattern, string Controller, string Action, bool AllowsAnonymous, bool TakesUpload)
{
    private const string ClubRoutePrefix = "api/Clubs/{clubId";

    public string Template => Pattern.RawText ?? "";

    /// <summary>The route belongs to a club (<c>api/Clubs/{clubId}/...</c>). Decided from the route only, not from the policies under test.</summary>
    public bool IsClubRoute => Template.StartsWith(ClubRoutePrefix, StringComparison.OrdinalIgnoreCase);

    public bool IsRead => Method is "GET" or "HEAD";
    public bool HasBody => Method is "POST" or "PUT" or "PATCH";

    public override string ToString() => $"{Method} {Template}";

    public static IReadOnlyList<ApiEndpoint> All(IServiceProvider services)
    {
        var endpoints = services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Endpoint: endpoint, Action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(x => x.Action is not null)
            .SelectMany(x => (x.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => new ApiEndpoint(
                    method.ToUpperInvariant(),
                    x.Endpoint.RoutePattern,
                    x.Action!.ControllerName,
                    x.Action.ActionName,
                    x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                    x.Endpoint.Metadata.OfType<IAcceptsMetadata>().Any(accepts => accepts.ContentTypes.Any(type => type.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))))))
            .OrderBy(e => e.Template, StringComparer.Ordinal).ThenBy(e => e.Method, StringComparer.Ordinal)
            .ToList();

        return endpoints;
    }

    /// <summary>
    /// The url of the endpoint for a club. Ids in the route and the usual query parameters are taken from <paramref name="ids"/>
    /// (the resources of a seeded club), so an endpoint is called with ids that really exist.
    /// </summary>
    public string Url(int clubId, SeededClub ids)
    {
        var segments = Pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => Value(parameter, clubId, ids),
            _ => "",
        })));

        var path = "/" + string.Join('/', segments);
        if (!IsRead)
            return path;

        // Wide ranges and every optional switch, so list endpoints return what there is
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = today.AddDays(60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{path}?dayFrom={from}&dayTo={to}&from={from}&to={to}&seasonId={ids.SeasonId}&includeExpired=true&includePast=true&Page=1&PageSize=100&EnablePaging=false&amount=50&take=50";
    }

    private static string Value(RoutePatternParameterPart parameter, int clubId, SeededClub ids)
    {
        var id = parameter.Name.ToLowerInvariant() switch
        {
            "clubid" => clubId,
            "memberid" => ids.Member.MemberId,
            "courtid" => ids.CourtId,
            "seasonid" => ids.SeasonId,
            "playmodeid" => ids.PlayModeId,
            "clubeventid" => ids.ClubEventId,
            "clubannouncementid" => ids.ClubAnnouncementId,
            "courtblockingid" => ids.CourtBlockingId,
            "planid" => ids.PlanId,
            "orderid" => ids.OrderId,
            "purchaseid" => ids.CourtPurchaseId,
            "ruleid" => ids.PriceRuleId,
            "itemid" => ids.ClubhouseItemId,
            "tabid" => ids.ClubhouseTabId,
            "lineid" => ids.ClubhouseTabLineId,
            "id" => ids.BookingId,
            _ => (int?)null,
        };

        if (id is not null)
            return id.Value.ToString(CultureInfo.InvariantCulture);

        // Other numeric ids (family, badge, attachment, ...) and non-numeric parameters (the type of an email template)
        var isNumber = parameter.ParameterPolicies.Any(policy => policy.Content == "int") || parameter.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase);
        return isNumber ? "1" : "Welcome";
    }
}
