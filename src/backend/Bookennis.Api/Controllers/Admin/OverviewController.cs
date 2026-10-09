using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Admin;

[Authorize(AuthorizationPolicies.ApplicationAdministrator)]
[Route("api/Admin/[controller]")]
public class OverviewController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<AdminOverviewResult> GetOverview(CancellationToken cancellationToken)
        => mediator.Send(new GetAdminOverview(), cancellationToken);
}
