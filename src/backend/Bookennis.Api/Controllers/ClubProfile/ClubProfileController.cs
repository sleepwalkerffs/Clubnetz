using Bookennis.Api.Business.ClubProfile;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.ClubProfile;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.ClubProfile;

[Authorize(AuthorizationPolicies.Member)]
public class ClubProfileController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    public Task<GetClubProfileResult> GetClubProfile(CancellationToken cancellationToken)
    {
        var isGuestSession = HttpContext.User.FindFirst("guest_session")?.Value == "true";
        return mediator.Send(new GetClubProfile(userAccessor.GetUserId(), isGuestSession), cancellationToken);
    }

    [HttpGet("BookingOptions")]
    public Task<GetBookingOptionsResult> GetBookingOptions(CancellationToken cancellationToken)
    {
        var isGuestSession = HttpContext.User.FindFirst("guest_session")?.Value == "true";
        return mediator.Send(new GetBookingOptions(userAccessor.GetUserId(), isGuestSession), cancellationToken);
    }

}