namespace Bookennis.Client.Services;

public interface ITenantProvider
{
    int CurrentClubId { get; }
    bool HasClubSelected { get; }
    void SetCurrentClub(int clubId);
}

public class TenantProvider : ITenantProvider
{
    private int? currentClubId;

    public int CurrentClubId => currentClubId ?? throw new InvalidOperationException("No club/tenant has been selected yet.");
    public bool HasClubSelected => currentClubId.HasValue;

    public void SetCurrentClub(int clubId) => currentClubId = clubId;
}
