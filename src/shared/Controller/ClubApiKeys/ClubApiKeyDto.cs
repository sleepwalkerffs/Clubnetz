namespace Bookennis.Shared.Controller.ClubApiKeys;

public record GetClubApiKeysResult
{
    public required List<ClubApiKeyDto> ApiKeys { get; init; }
}

/// <summary>An API key of the club. The key itself is never returned again, only its first characters.</summary>
public record ClubApiKeyDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string KeyPrefix { get; init; }
    public required bool IsReadOnly { get; init; }
    public required string CreatedByName { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTimeOffset? ExpiresAt { get; init; }
    public required DateTimeOffset? LastUsedAt { get; init; }
    public required ClubApiKeyStatus Status { get; init; }
}

public enum ClubApiKeyStatus
{
    Active = 0,
    Expired = 1,

    /// <summary>The key acts as the admin who created it. That user is no longer an admin of the club, so the key is rejected.</summary>
    CreatorNotAdmin = 2
}

public record CreateClubApiKeyResult
{
    public required int Id { get; init; }

    /// <summary>The key. It is only returned here and can't be read again.</summary>
    public required string Token { get; init; }
}
