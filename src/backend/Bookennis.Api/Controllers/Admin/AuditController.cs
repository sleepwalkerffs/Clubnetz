using Bookennis.Api.Business.Admin.Audit;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Admin;

/// <summary>The audit log, only for application administrators (signed in with a cookie, never with an API key).</summary>
[Authorize(AuthorizationPolicies.ApplicationAdministrator)]
[Route("api/Admin/[controller]")]
public class AuditController(IMediator mediator) : ControllerBase
{
    [HttpGet("Events")]
    public Task<AuditEventPage> GetEvents([FromQuery] AuditEventFilter filter, CancellationToken cancellationToken)
        => mediator.Send(new GetAuditEvents(filter), cancellationToken);

    [HttpGet("Events/Counts")]
    public Task<AuditEventCounts> GetCounts([FromQuery] AuditEventFilter filter, [FromQuery] int utcOffsetMinutes, CancellationToken cancellationToken)
        => mediator.Send(new GetAuditEventCounts(filter, utcOffsetMinutes), cancellationToken);

    [HttpGet("Events/Export")]
    public async Task<IActionResult> Export([FromQuery] AuditEventFilter filter, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportAuditEvents(filter), cancellationToken);
        return File(result.Data, ExportAuditEventsResult.ContentType, result.FileName);
    }

    [HttpGet("Events/{id:long}")]
    public Task<AuditEventDetail> GetEvent(long id, CancellationToken cancellationToken)
        => mediator.Send(new GetAuditEvent(id), cancellationToken);

    [HttpGet("Options")]
    public Task<AuditFilterOptions> GetOptions(CancellationToken cancellationToken)
        => mediator.Send(new GetAuditFilterOptions(), cancellationToken);
}
