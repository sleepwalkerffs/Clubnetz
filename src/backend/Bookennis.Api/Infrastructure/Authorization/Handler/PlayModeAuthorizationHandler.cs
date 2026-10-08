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

        var clubId = (await modelService.GetClubIds(context)).Single();
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

        if (playModes.Single(playMode => playMode.Id == playModeId).AllowedRoles.Any(role.Contains))
        {
            context.Succeed(requirement);
            return;
        }
    }
}
