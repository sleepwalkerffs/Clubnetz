using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.Utils.Paging;
using Bookennis.Api.Infrastructure.Utils.Sorting;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Admin;

[Authorize(AuthorizationPolicies.ApplicationAdministrator)]
[Route("api/Admin/[controller]")]
public class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<GetAdminUsersResult> GetUsers([FromQuery] PaginationParameters pagination, [FromQuery] SortParameters? sort, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminUsers(pagination, sort, searchTerm), cancellationToken);

    [HttpGet("{userId:int}")]
    public Task<AdminUserDetailResult> GetUser(int userId, CancellationToken cancellationToken)
        => mediator.Send(new GetAdminUser(userId), cancellationToken);

    [HttpPut("{userId:int}")]
    public Task UpdateUser(int userId, UpdateAdminUserRequest request, CancellationToken cancellationToken)
        => mediator.Send(new UpdateAdminUser(userId, request.FirstName, request.LastName, request.Birthday, (Gender)request.Gender, request.Street, request.City, request.ZipCode, (Domain.User.Country)request.Country), cancellationToken);

    [HttpDelete("{userId:int}")]
    public Task DeleteUser(int userId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteAdminUser(userId), cancellationToken);

    [HttpPost("{userId:int}/ResendConfirmationEmail")]
    public Task ResendConfirmationEmail(int userId, CancellationToken cancellationToken)
        => mediator.Send(new ResendConfirmationEmail(userId), cancellationToken);

    [HttpPost("{userId:int}/ResetPassword")]
    public Task ResetPassword(int userId, CancellationToken cancellationToken)
        => mediator.Send(new AdminResetPassword(userId), cancellationToken);

    [HttpPost("{userId:int}/ChangeEmail")]
    public Task ChangeEmail(int userId, ChangeUserEmailRequest request, CancellationToken cancellationToken)
        => mediator.Send(new ChangeUserEmail(userId, request.NewEmail), cancellationToken);

    [HttpPost("{userId:int}/Impersonate")]
    public Task Impersonate(int userId, CancellationToken cancellationToken)
        => mediator.Send(new ImpersonateUser(userId), cancellationToken);
}
