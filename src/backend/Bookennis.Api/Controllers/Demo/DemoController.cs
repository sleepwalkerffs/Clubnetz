using Bookennis.Api.Business.Demo;
using Bookennis.Shared.Controller.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Demo;

/// <summary>Whether this is the public demo and which accounts it offers. Public: the login page needs it.</summary>
[AllowAnonymous]
public class DemoController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<GetDemoInfoResult> Get(CancellationToken cancellationToken)
        => mediator.Send(new GetDemoInfo(), cancellationToken);
}
