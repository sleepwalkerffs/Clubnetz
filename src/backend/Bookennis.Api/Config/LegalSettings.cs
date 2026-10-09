namespace Bookennis.Api.Config;

/// <summary>
/// Operator details shown on the imprint and privacy policy pages. They differ per installation, so they are
/// configured on the server (section <c>Legal</c>, e.g. the environment variable <c>Legal__OperatorName</c>)
/// and never committed.
/// </summary>
public class LegalSettings
{
    /// <summary>Name of the person or organisation operating this installation.</summary>
    public string? OperatorName { get; set; }

    public string? Street { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Where the app and the database run, e.g. "Hetzner Online GmbH, Germany".</summary>
    public string? HostingProvider { get; set; }

    /// <summary>The service that delivers the emails, e.g. the SMTP provider.</summary>
    public string? EmailProvider { get; set; }
}
