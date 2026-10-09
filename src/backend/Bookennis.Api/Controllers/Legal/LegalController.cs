using Bookennis.Api.Business.Legal;
using Bookennis.Shared.Controller.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Legal;

/// <summary>Operator details for the imprint and privacy policy pages, which are public.</summary>
[AllowAnonymous]
public class LegalController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<GetLegalSettingsResult> Get(CancellationToken cancellationToken)
        => mediator.Send(new GetLegalSettings(), cancellationToken);
}
