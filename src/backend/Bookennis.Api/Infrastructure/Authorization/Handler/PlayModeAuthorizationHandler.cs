using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

public class PlayModeAuthorizationHandler(
    AuthorizationModelService modelService,
    AuthorizationHandlerService handlerService) : AuthorizationHandler<CanBookPlayModeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CanBookPlayModeRequirement requirement)
    {
        if (!context.User.IsAuthenticated())
            return;

        if (context.User.IsAdministrator())
        {
            context.Succeed(requirement);
            return;
        }

        // The tenant filter hides play modes of other clubs: an unknown play mode fails instead of throwing (500)
        var clubIds = await modelService.GetClubIds(context);
        if (clubIds.Length != 1)
            return;

        var clubId = clubIds[0];
        var (_, role) = await handlerService.GetMember(context.User.GetId(), clubId, context.User);

        if (role is null)
            return;

        if (role.Contains(MemberRole.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        var playModes = await handlerService.GetPlayModes(clubId);
        var playModeId = modelService.GetPlayModeId(context);

        // A play mode of another club (or one that does not exist) can't be booked: fail instead of throwing (500)
        var playMode = playModes.SingleOrDefault(p => p.Id == playModeId);
        if (playMode is not null && playMode.AllowedRoles.Any(role.Contains))
        {
            context.Succeed(requirement);
            return;
        }
    }
}
