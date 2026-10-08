using Bookennis.Api.Business.Families;
using Bookennis.Api.Business.Families.Models;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Families;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Families;

[Authorize(AuthorizationPolicies.ClubMember)]
public class FamiliesController(IMediator mediator) : ClubControllerBase
{
    [HttpGet]
    public async Task<GetFamiliesResult> GetFamilies(CancellationToken cancellationToken)
        => await mediator.Send(new GetFamilies(), cancellationToken);

    [HttpGet("{familyId:int}")]
    public async Task<GetFamilyResult> GetFamily(int familyId, CancellationToken cancellationToken)
        => await mediator.Send(new GetFamily(familyId), cancellationToken);

    [HttpGet("AvailableFamilyMembers/{memberId:int}")]
    public async Task<GetAvailableFamilyMembersResult> GetAvailableFamilyMembers(int memberId, CancellationToken cancellationToken)
        => await mediator.Send(new GetAvailableFamilyMembers(memberId), cancellationToken);

    [HttpPost]
    public async Task<int> CreateFamily(CreateFamilyRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new CreateFamily(
               request.ParentContractUserIds,
               request.ChildrenUserContractIds,
               request.NewChildUsers.ConvertAll(u => new ChildUserModel(u.FirstName, u.LastName, u.Birthday, (Gender)u.Gender)),
               HttpContext.User.GetId()), cancellationToken);

    [HttpPost("{familyId:int}/Parent")]
    public async Task AddParent(int familyId, AddParentRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new AddParent(familyId, request.MemberId), cancellationToken);

    [HttpPost("{familyId:int}/Child")]
    public async Task AddChild(int familyId, AddChildRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new AddChild(familyId, request.MemberIds), cancellationToken);

    [HttpPost("{familyId:int}/NewChild")]
    public async Task AddNewChild(int familyId, AddNewChildRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new AddNewChild(familyId, new ChildUserModel(request.FirstName, request.LastName, request.Birthday, (Gender)request.Gender), HttpContext.User.GetId()), cancellationToken);

    [HttpDelete("{familyId:int}")]
    public async Task DeleteFamily(int familyId, CancellationToken cancellationToken)
        => await mediator.Send(new DeleteFamily(familyId), cancellationToken);

    [HttpPut("{familyId:int}/Child/{memberId:int}")]
    public async Task UpdateOwnedChild(int familyId, int memberId, UpdateOwnedChildRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new UpdateOwnedChild(familyId, memberId, request), cancellationToken);

    [HttpDelete("{familyId:int}/Member/{memberId:int}")]
    public async Task DeleteFamily(int familyId, int memberId, CancellationToken cancellationToken)
        => await mediator.Send(new DeleteFamilyMember(familyId, memberId), cancellationToken);
}