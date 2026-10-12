using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

namespace Bookennis.Api.Infrastructure.Swagger;

/// <summary>
/// The published OpenAPI document: the endpoints a club API key can call. It is served in every environment at <see cref="Url"/>,
/// whereas the document with all endpoints (<see cref="AllEndpointsName"/>) only exists in Development.
/// </summary>
public static class ClubApiDocument
{
    public const string Name = "club";
    public const string AllEndpointsName = "v1";
    public const string SecurityScheme = "ClubApiKey";

    /// <summary>Route template of the OpenAPI documents; the club document is at <see cref="Url"/>.</summary>
    public const string RouteTemplate = "api/openapi/{documentName}.json";
    public const string Url = $"/api/openapi/{Name}.json";

    public static OpenApiInfo Info => new()
    {
        Title = "Clubnetz Club API",
        Version = "1",
        Description =
            $"""
            Everything a club API key can do in its club. These are the endpoints the Clubnetz app itself uses.
            A guide with examples is at https://clubnetz.app/docs/api/.

            **Authentication:** create a key in the app (club administration, API keys) and send it with every request as
            `Authorization: Bearer cnz_...`. A key acts as the club admin who created it and only works for the routes of its
            club (`/api/Clubs/<clubId>/...`). It stops working when it expires, is revoked, or its creator is no longer an admin of the club.

            **Read-only keys** may only send GET requests, everything else is answered with 403.

            **Limits:** {RateLimitingConfiguration.ClubApiKeyRequestsPerMinute} requests per minute and key, then 429 with a `Retry-After` header.

            **Errors:** invalid input is answered with 412 and a JSON body with `message`, `errorCode` and `errorDetail`.

            **Stability:** the app and the API are released together, so endpoints can change with a new version of Clubnetz.
            Every response carries the running version in the `X-App-Version` header.
            """,
    };

    public static OpenApiSecurityScheme SecuritySchemeDefinition => new()
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "cnz_...",
        Description = "A club API key, created in the club administration of the app.",
    };

    /// <summary>An endpoint belongs to the document when one of its policies accepts the club API key scheme.</summary>
    public static bool Includes(ApiDescription api, AuthorizationOptions authorizationOptions)
    {
        var metadata = api.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
            return false;

        return metadata.OfType<IAuthorizeData>()
            .Select(authorize => authorize.Policy is null ? null : authorizationOptions.GetPolicy(authorize.Policy))
            .Any(policy => policy is not null && policy.AuthenticationSchemes.Contains(CustomAuthenticationSchemes.ClubApiKey));
    }
}
