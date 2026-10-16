using Bookennis.Api.Business.Families;
using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Authorization.Models;
using Bookennis.Api.Infrastructure.Exceptions;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Families;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Families;

/// <summary>
/// Families of the club. A member manages the own family: only its members read it and only its parents change it
/// (<see cref="AuthorizationPolicies.FamilyMember"/>, <see cref="AuthorizationPolicies.FamilyParent"/>). Admins and treasurers of the club may do both.
/// </summary>
[Authorize(AuthorizationPolicies.ClubMember)]
public class FamiliesController(IMediator mediator, IAuthorizationService authorizationService) : ClubControllerBase
{
    /// <summary>All families of the club, for those who manage the members.</summary>
    [HttpGet]
    [Authorize(AuthorizationPolicies.ClubMemberViewer)]
    public async Task<GetFamiliesResult> GetFamilies(CancellationToken cancellationToken)
        => await mediator.Send(new GetFamilies(), cancellationToken);

    [HttpGet("{familyId:int}")]
    public async Task<GetFamilyResult> GetFamily(int familyId, CancellationToken cancellationToken)
    {
        await EnsureFamilyMember(familyId);
        return await mediator.Send(new GetFamily(familyId), cancellationToken);
    }

    /// <summary>The members of the club without a family (names only), to pick a partner or children from.</summary>
    [HttpGet("AvailableFamilyMembers/{memberId:int}")]
    public async Task<GetAvailableFamilyMembersResult> GetAvailableFamilyMembers(int memberId, CancellationToken cancellationToken)
    {
        // Members who are not active don't get names of other members; treasurers put their families together
        if (!await authorizationService.IsActiveMember(User))
            return new GetAvailableFamilyMembersResult { AvailableMembers = [] };

        return await mediator.Send(new GetAvailableFamilyMembers(memberId), cancellationToken);
    }

    [HttpPost]
    public async Task<int> CreateFamily(CreateFamilyRequest request, CancellationToken cancellationToken)
    {
        // A member founds the own family: they have to be one of its parents (admins and treasurers may found any)
        var isOneOfTheParents = (await authorizationService.AuthorizeAsync(HttpContext.User, AuthorizationPolicies.ClubTreasurer)).Succeeded;
        foreach (var parentMemberId in request.ParentContractUserIds)
            isOneOfTheParents |= (await authorizationService.AuthorizeAsync(HttpContext.User, new MemberAuthorizationModel(parentMemberId), AuthorizationPolicies.Member)).Succeeded;

        if (!isOneOfTheParents)
            throw new AccessDeniedException("A family can only be created by one of its parents.");

        return await mediator.Send(new CreateFamily(
            request.ParentContractUserIds,
            request.ChildrenUserContractIds,
            request.NewChildUsers.ConvertAll(u => new ChildUserModel(u.FirstName, u.LastName, u.Birthday, (Gender)u.Gender)),
            HttpContext.User.GetId()), cancellationToken);
    }

    [HttpPost("{familyId:int}/Parent")]
    public async Task AddParent(int familyId, AddParentRequest request, CancellationToken cancellationToken)
    {
        await EnsureFamilyParent(familyId);
        await mediator.Send(new AddParent(familyId, request.MemberId), cancellationToken);
    }

    [HttpPost("{familyId:int}/Child")]
    public async Task AddChild(int familyId, AddChildRequest request, CancellationToken cancellationToken)
    {
        await EnsureFamilyParent(familyId);
        await mediator.Send(new AddChild(familyId, request.MemberIds), cancellationToken);
    }

    [HttpPost("{familyId:int}/NewChild")]
    public async Task AddNewChild(int familyId, AddNewChildRequest request, CancellationToken cancellationToken)
    {
        await EnsureFamilyParent(familyId);
        await mediator.Send(new AddNewChild(familyId, new ChildUserModel(request.FirstName, request.LastName, request.Birthday, (Gender)request.Gender), HttpContext.User.GetId()), cancellationToken);
    }

    [HttpDelete("{familyId:int}")]
    public async Task DeleteFamily(int familyId, CancellationToken cancellationToken)
    {
        await EnsureFamilyParent(familyId);
        await mediator.Send(new DeleteFamily(familyId), cancellationToken);
    }

    [HttpPut("{familyId:int}/Child/{memberId:int}")]
    public async Task UpdateOwnedChild(int familyId, int memberId, UpdateOwnedChildRequest request, CancellationToken cancellationToken)
    {
        await EnsureFamilyParent(familyId);
        await mediator.Send(new UpdateOwnedChild(familyId, memberId, request), cancellationToken);
    }

    [HttpDelete("{familyId:int}/Member/{memberId:int}")]
    public async Task DeleteFamily(int familyId, int memberId, CancellationToken cancellationToken)
    {
        // Parents remove anybody, every member may take themself out
        await authorizationService.AuthorizeAsync(HttpContext.User, new FamilyAuthorizationModel(familyId, LeavingMemberId: memberId), AuthorizationPolicies.FamilyParent).EnsureSucceeded();
        await mediator.Send(new DeleteFamilyMember(familyId, memberId), cancellationToken);
    }

    private Task EnsureFamilyMember(int familyId)
        => authorizationService.AuthorizeAsync(HttpContext.User, new FamilyAuthorizationModel(familyId), AuthorizationPolicies.FamilyMember).EnsureSucceeded();

    private Task EnsureFamilyParent(int familyId)
        => authorizationService.AuthorizeAsync(HttpContext.User, new FamilyAuthorizationModel(familyId), AuthorizationPolicies.FamilyParent).EnsureSucceeded();
}
