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
    public Task<GetClubProfileResult> GetClubProfile([FromRoute] int clubId, CancellationToken cancellationToken)
    {
        var supportClubId = HttpContext.User.IsAdministrator() ? clubId : (int?)null;
        return mediator.Send(new GetClubProfile(userAccessor.GetUserId(), supportClubId), cancellationToken);
    }

    [HttpGet("BookingOptions")]
    public Task<GetBookingOptionsResult> GetBookingOptions([FromRoute] int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetBookingOptions(userAccessor.GetUserId(), clubId), cancellationToken);

}