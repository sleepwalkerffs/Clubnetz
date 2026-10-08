using Microsoft.AspNetCore.Builder;

namespace Bookennis.Api.Infrastructure.Configuration;

public static class SecurityHeadersConfiguration
{
    // Applied to the badge public-image endpoints: they're embedded via a plain <img> URL in
    // badge-awarded emails, so they must stay loadable cross-origin from mail clients.
    public const string PublicAssetPolicy = "PublicAsset";

    public static void AddSecurityHeaders(this WebApplicationBuilder builder)
    {
        var defaultPolicy = BuildDefaultPolicy();
        var publicAssetPolicy = defaultPolicy.Copy().AddCrossOriginResourcePolicy(x => x.CrossOrigin());

        builder.Services.AddSecurityHeaderPolicies()
            .SetDefaultPolicy(defaultPolicy)
            .AddPolicy(PublicAssetPolicy, publicAssetPolicy);
    }

    // script-src needs 'wasm-unsafe-eval' for the Blazor WebAssembly runtime.
    // style-src needs 'unsafe-inline' because Blazor/MudBlazor render extensive inline style="" attributes.
    private static HeaderPolicyCollection BuildDefaultPolicy() =>
        new HeaderPolicyCollection()
            .AddDefaultSecurityHeaders()
            .AddContentSecurityPolicy(csp =>
            {
                csp.AddDefaultSrc().Self();
                csp.AddScriptSrc().Self().WasmUnsafeEval();
                csp.AddStyleSrc().Self().UnsafeInline().From("https://fonts.googleapis.com");
                csp.AddFontSrc().Self().From("https://fonts.gstatic.com");
                csp.AddImgSrc().Self().Data();
                csp.AddConnectSrc().Self();
                csp.AddObjectSrc().None();
                csp.AddFormAction().Self();
                csp.AddFrameAncestors().None();
                csp.AddBaseUri().Self();
            });
}
