namespace Bookennis.Api.Infrastructure.Tenant;

public class TenantService : ITenantService
{
    private int? tenantId;

    public bool TryGetTenantId(out int tenantId)
    {
        tenantId = this.tenantId ?? 0;
        return this.tenantId is not null;
    }

    public int? GetTenantId() => tenantId;

    public void SetTenantId(int tenantId) => this.tenantId = tenantId;
}