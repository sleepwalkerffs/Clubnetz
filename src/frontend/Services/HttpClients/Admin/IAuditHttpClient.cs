using Bookennis.Shared.Controller.Admin.Audit;

namespace Bookennis.Client.Services.HttpClients.Admin;

public interface IAuditHttpClient
{
    public Task<HttpResult<AuditEventPage>> GetEvents(AuditEventFilter filter, CancellationToken cancellationToken = default);

    public Task<HttpResult<AuditEventDetail>> GetEvent(long id, CancellationToken cancellationToken = default);

    public Task<HttpResult<AuditFilterOptions>> GetOptions(CancellationToken cancellationToken = default);
}
