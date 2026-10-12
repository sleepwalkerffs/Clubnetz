using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Clubs.ApiKeys;

/// <summary>
/// Lets a script or another system use the API of one club without signing in. The key acts as the club admin who created it
/// (<see cref="CreatedByUserId"/>): it can do what that admin can do in this club, and nothing outside of it.
/// Only a hash of the key is stored, the key itself is shown once when it is created.
/// </summary>
public class ClubApiKey : TenantDomainEntity, IAggregateRoot
{
    public const int NameMaxLength = 100;
    public const int MaxExpiryInDays = 3650;
    public const int MaxKeysPerClub = 20;

    /// <summary>Every key starts with this, so leaked keys can be recognized (e.g. by secret scanners).</summary>
    public const string TokenPrefix = "cnz_";

    private const int SecretSizeInBytes = 32;
    private const int DisplayedSecretCharacters = 6;

#pragma warning disable CS8618
    private ClubApiKey() { }
#pragma warning restore CS8618

    private ClubApiKey(int clubId, int createdByUserId, string name, bool isReadOnly, DateTimeOffset? expiresAt, string token)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));
        if (name.Length > NameMaxLength)
            throw new ArgumentException($"Name must not exceed {NameMaxLength} characters.", nameof(name));

        ClubId = clubId;
        CreatedByUserId = createdByUserId;
        Name = name;
        IsReadOnly = isReadOnly;
        ExpiresAt = expiresAt;
        KeyHash = Hash(token);
        KeyPrefix = token[..(TokenPrefix.Length + DisplayedSecretCharacters)];
    }

    public string Name { get; private set; }

    /// <summary>SHA-256 of the key (hex). The key is random with 256 bits, so a fast hash is enough.</summary>
    public string KeyHash { get; private set; }

    /// <summary>The first characters of the key, to tell the keys apart in the list.</summary>
    public string KeyPrefix { get; private set; }

    /// <summary>A read-only key may only send GET requests.</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>The user the key acts as. The key only works while this user is an admin of the club.</summary>
    public int CreatedByUserId { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>Creates a key and returns it together with the token, which can't be read again afterwards.</summary>
    public static (ClubApiKey Key, string Token) Create(int clubId, int createdByUserId, string name, bool isReadOnly, DateTimeOffset? expiresAt)
    {
        var token = TokenPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretSizeInBytes));
        return (new ClubApiKey(clubId, createdByUserId, name, isReadOnly, expiresAt, token), token);
    }

    public static bool IsToken(string? value) => value is not null && value.StartsWith(TokenPrefix, StringComparison.Ordinal);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;
}
