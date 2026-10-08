using System.Text.RegularExpressions;

namespace Bookennis.Api.Infrastructure.Tenant;

public partial interface ITenantService
{
    [GeneratedRegex("""^\/api\/Clubs\/(\d*)""", RegexOptions.IgnoreCase, "de-AT")]
    private static partial Regex ClubIdRegex();

    public bool TryGetTenantId(out int tenantId);

    public int? GetTenantId();

    public void SetTenantId(int tenantId);

    public static int? GetTenantIdFromPath(string path)
    {
        var match = ClubIdRegex().Matches(path);
        if (match.Count != 0 && int.TryParse(match[0].Groups[1].Value, out var tenentId))
        {
            return tenentId;
        }

        return null;
    }
}