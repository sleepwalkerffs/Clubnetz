using Bookennis.Api.Infrastructure.Authorization.Requirements;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Members;
using Microsoft.AspNetCore.Authorization;

namespace Bookennis.Api.Infrastructure.Authorization.Handler;

public class CanEditBookingAuthorizationHandler(
    AuthorizationModelService modelService,
    AuthorizationHandlerService handlerService) : AuthorizationHandler<CanEditBookingRequirement>
{
    private static readonly MemberRole[] ElevatedRoles =
    [
        MemberRole.Maintainer,
        MemberRole.Admin,
        MemberRole.SportsDirector,
        MemberRole.YouthSportsDirector,
        MemberRole.Trainer,
    ];

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CanEditBookingRequirement requirement)
    {
        if (!context.User.IsAuthenticated())
            return;

        if (context.User.IsAdministrator())
        {
            context.Succeed(requirement);
            return;
        }

        var clubId = (await modelService.GetClubIds(context)).Single();
        var (memberId, role) = await handlerService.GetMember(context.User.GetId(), clubId, context.User);

        if (role is null)
            return;

        if (role.Any(r => ElevatedRoles.Contains(r)))
        {
            context.Succeed(requirement);
            return;
        }

        var isInPast = await modelService.IsBookingInPast(context);
        if (isInPast)
            return;

        var playerIds = await modelService.GetPlayerIds(context);

        if (playerIds.Any(id => id == memberId))
        {
            context.Succeed(requirement);
            return;
        }

        if (memberId is null)
            return;

        var childrenIds = await handlerService.GetChildMemberIds(memberId.Value);
        if (childrenIds is null)
            return;

        if (playerIds.Any(childrenIds.Contains))
            context.Succeed(requirement);
    }
}
