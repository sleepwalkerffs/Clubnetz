namespace Bookennis.Shared.Controller.Legal;

/// <summary>Details of the operator of this installation, shown on the imprint and privacy policy pages.</summary>
public record GetLegalSettingsResult
{
    public string OperatorName { get; init; } = "";
    public string Street { get; init; } = "";
    public string ZipCode { get; init; } = "";
    public string City { get; init; } = "";
    public string Country { get; init; } = "";
    public string Email { get; init; } = "";
    public string? Phone { get; init; }
    public string HostingProvider { get; init; } = "";
    public string EmailProvider { get; init; } = "";
}
