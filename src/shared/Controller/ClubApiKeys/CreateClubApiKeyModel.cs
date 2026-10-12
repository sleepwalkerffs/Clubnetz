namespace Bookennis.Shared.Controller.ClubApiKeys;

/// <summary>The name is validated by the handler, so an empty name results in a localized error instead of a generic bad request.</summary>
public record CreateClubApiKeyModel
{
    public string? Name { get; init; }

    /// <summary>A read-only key may only send GET requests.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary><c>null</c> for a key that never expires.</summary>
    public int? ExpiresInDays { get; init; }
}

public static class ClubApiKeyLimits
{
    public const int NameMaxLength = 100;
    public const int MaxExpiryInDays = 3650;
}
