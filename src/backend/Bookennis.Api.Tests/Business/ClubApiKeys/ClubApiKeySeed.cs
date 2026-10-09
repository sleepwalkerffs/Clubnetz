using Bookennis.Api.Data;
using Bookennis.Domain.Clubs.ApiKeys;

namespace Bookennis.Api.Tests.Business.ClubApiKeys;

internal static class ClubApiKeySeed
{
    /// <summary>Adds a key to the test club that acts as the club admin of the test data, and returns it with its token.</summary>
    public static async Task<(int Id, string Token)> Seed(
        AppDbContext context,
        string name = "Website",
        bool isReadOnly = false,
        DateTimeOffset? expiresAt = null,
        int userId = TestDataSeed.AdminId)
    {
        var (key, token) = ClubApiKey.Create(TestDataSeed.ClubId, userId, name, isReadOnly, expiresAt);
        context.ClubApiKeys.Add(key);
        await context.SaveChangesAsync();
        return (key.Id, token);
    }
}
