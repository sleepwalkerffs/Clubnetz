using Bookennis.Api.Business.Members;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Infrastructure.Utils.Sorting;
using Bookennis.Domain.Members;
using Bookennis.Shared.Controller.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Members;

[Authorize(AuthorizationPolicies.ApplicationUser)]
public class MembersController(IMediator mediator, IAuthorizationService authorizationService) : ClubControllerBase
{
    [HttpGet]
    [Authorize(AuthorizationPolicies.ClubMemberViewer)]
    public async Task<GetMembersResult> GetMembers([FromQuery] PaginationParameters pagination, [FromQuery] SortParameters? sort, [FromQuery] MemberFilter filter, CancellationToken cancellationToken)
        => await mediator.Send(new GetMembers(pagination, sort, filter), cancellationToken);

    [HttpGet("emails")]
    [Authorize(AuthorizationPolicies.ClubMemberViewer)]
    public async Task<GetMemberEmailsResult> GetMemberEmails([FromQuery] MemberFilter filter, CancellationToken cancellationToken)
        => await mediator.Send(new GetMemberEmails(filter), cancellationToken);

    [HttpGet("summary")]
    [Authorize(AuthorizationPolicies.ClubMemberViewer)]
    public async Task<GetMembersSummaryResult> GetMembersSummary(CancellationToken cancellationToken)
        => await mediator.Send(new GetMembersSummary(), cancellationToken);

    [HttpGet("{memberId:int}/overview")]
    [Authorize(AuthorizationPolicies.ClubMemberViewer)]
    public async Task<GetMemberOverviewResult> GetMemberOverview([FromRoute] int clubId, int memberId, CancellationToken cancellationToken)
        => await mediator.Send(new GetMemberOverview(clubId, memberId), cancellationToken);

    [HttpGet("{memberId:int}")]
    [Authorize(AuthorizationPolicies.Member)]
    public async Task<MembersDetailResult> GetMember(int memberId, CancellationToken cancellationToken)
        => await mediator.Send(new GetMember(memberId), cancellationToken);

    [HttpPatch("{memberId:int}/BookingOptions")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public async Task EditMember(int memberId, UpdateMemberBookingOptionsRequest editMember, CancellationToken cancellationToken)
        => await mediator.Send(new UpdateMemberBookingOptions(memberId, editMember.AllowedSeasonIds, editMember.BookingsPerWeek), cancellationToken);

    [HttpPatch("{memberId:int}/Roles")]
    [Authorize(AuthorizationPolicies.ClubAdministrator)]
    public async Task UpdateMemberRoles(int memberId, UpdateMemberRolesRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new UpdateMemberRoles(memberId, request.Roles.Select(r => (MemberRole)r).ToArray()), cancellationToken);

    [HttpGet("{memberId:int}/BookingHistory")]
    [Authorize(AuthorizationPolicies.AnyMember)]
    public async Task<GetMemberBookingHistoryResult> GetBookingHistory(int memberId, [FromQuery] int seasonId, CancellationToken cancellationToken)
    {
        // Members read their own history, member viewers the history of everybody
        if (!(await authorizationService.AuthorizeAsync(HttpContext.User, AuthorizationPolicies.ClubMemberViewer)).Succeeded)
            (await authorizationService.AuthorizeAsync(HttpContext.User, new MemberAuthorizationModel(memberId), AuthorizationPolicies.Member)).EnsureSucceeded();

        return await mediator.Send(new GetMemberBookingHistory(memberId, seasonId), cancellationToken);
    }
}