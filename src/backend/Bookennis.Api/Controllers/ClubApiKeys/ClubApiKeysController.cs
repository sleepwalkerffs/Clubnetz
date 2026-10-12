using Bookennis.Api.Business.ClubApiKeys;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Shared.Controller.ClubApiKeys;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.ClubApiKeys;

/// <summary>
/// API keys of the club, managed by its admins. These endpoints only accept a signed-in user:
/// an API key can't create or revoke keys.
/// </summary>
[Authorize(AuthorizationPolicies.ClubApiKeyManager)]
public class ClubApiKeysController(IMediator mediator, IUserAccessor userAccessor) : ClubControllerBase
{
    [HttpGet]
    public Task<GetClubApiKeysResult> GetClubApiKeys(int clubId, CancellationToken cancellationToken)
        => mediator.Send(new GetClubApiKeys(clubId), cancellationToken);

    [HttpPost]
    public Task<CreateClubApiKeyResult> CreateClubApiKey(int clubId, [FromBody] CreateClubApiKeyModel model, CancellationToken cancellationToken)
        => mediator.Send(new CreateClubApiKey(clubId, userAccessor.GetUserId(), model.Name, model.IsReadOnly, model.ExpiresInDays), cancellationToken);

    [HttpDelete("{clubApiKeyId:int}")]
    public Task DeleteClubApiKey(int clubId, int clubApiKeyId, CancellationToken cancellationToken)
        => mediator.Send(new DeleteClubApiKey(clubId, clubApiKeyId), cancellationToken);
}
