using Microsoft.Extensions.Configuration;

namespace Bookennis.Client.Services.Legal;

/// <summary>
/// Operator details shown on the imprint and privacy policy pages. Configured in the <c>Legal</c> section of <c>wwwroot/appsettings.json</c>,
/// so they can be changed per deployment without a code change.
/// </summary>
public class LegalSettings
{
    /// <summary>Name of the person or organisation operating Bookennis.</summary>
    public string OperatorName { get; set; } = "";

    public string Street { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }

    /// <summary>Where the app and the database run, e.g. "Microsoft Azure (Microsoft Ireland Operations Ltd.), region West Europe".</summary>
    public string HostingProvider { get; set; } = "";

    /// <summary>The service that delivers the emails, e.g. the SMTP provider.</summary>
    public string EmailProvider { get; set; } = "";

    // Read explicitly instead of the reflection based binder, which doesn't survive trimming in the WebAssembly build
    public static LegalSettings FromConfiguration(IConfiguration section) => new()
    {
        OperatorName = section[nameof(OperatorName)] ?? "",
        Street = section[nameof(Street)] ?? "",
        ZipCode = section[nameof(ZipCode)] ?? "",
        City = section[nameof(City)] ?? "",
        Country = section[nameof(Country)] ?? "",
        Email = section[nameof(Email)] ?? "",
        Phone = section[nameof(Phone)],
        HostingProvider = section[nameof(HostingProvider)] ?? "",
        EmailProvider = section[nameof(EmailProvider)] ?? ""
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(OperatorName) && !string.IsNullOrWhiteSpace(Email);
}
