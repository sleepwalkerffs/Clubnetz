using System.Text.Json;
using System.Text.Json.Serialization;
using Bookennis.Api.Business.Profile;
using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Profile;
using Fusonic.Extensions.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookennis.Api.Controllers.Profile;

[Authorize(AuthorizationPolicies.ApplicationUser)]
public class ProfileController(IMediator mediator, IUserAccessor userAccessor) : ControllerBase
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpGet]
    public async Task<GetUserProfileResult> GetUserProfile(CancellationToken cancellationToken)
        => await mediator.Send(new GetUserProfile(userAccessor.GetUserId()), cancellationToken) with
        {
            IsGuestSession = HttpContext.User.FindFirst(GuestSessionClaims.GuestSession)?.Value == "true"
        };

    [HttpPost("Update")]
    public async Task UpdateUserProfile(UpdateUserProfileModel model, CancellationToken cancellationToken) =>
        await mediator.Send(new UpdateUserProfile(model.FirstName, model.LastName, model.Birthday, (Gender)model.Gender, (Language)model.Language, model.Street, model.City, model.ZipCode, (Domain.User.Country)model.Country), cancellationToken);

    [HttpPost("ChangeEmail")]
    [Authorize(AuthorizationPolicies.AccountOwner)]
    [EnableRateLimiting(RateLimitingConfiguration.EmailPolicy)]
    public async Task RequestEmailChange(ChangeEmailModel model, CancellationToken cancellationToken)
        => await mediator.Send(new RequestEmailChange(model.CurrentPassword, model.NewEmail), cancellationToken);

    [HttpPost("ChangePassword")]
    [Authorize(AuthorizationPolicies.AccountOwner)]
    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    public async Task ChangePassword(ChangePasswordModel model, CancellationToken cancellationToken)
        => await mediator.Send(new ChangePassword(model.CurrentPassword, model.NewPassword), cancellationToken);

    [HttpPost("DismissCompletionBanner")]
    public async Task DismissProfileCompletionBanner(CancellationToken cancellationToken)
        => await mediator.Send(new DismissProfileCompletionBanner(), cancellationToken);

    [HttpPost("BecomeClubMember")]
    public async Task BecomeClubMember(BecomeClubMemberModel model, CancellationToken cancellationToken)
        => await mediator.Send(new BecomeClubMember(model.ClubId), cancellationToken);

    [HttpPost("LeaveClub")]
    [Authorize(AuthorizationPolicies.AccountOwner)]
    public async Task LeaveClub(LeaveClubModel model, CancellationToken cancellationToken)
        => await mediator.Send(new LeaveClub(model.ClubId), cancellationToken);

    /// <summary>Downloads all personal data of the current user as JSON (GDPR Art. 15 / 20).</summary>
    [HttpGet("export")]
    [Authorize(AuthorizationPolicies.AccountOwner)]
    public async Task<IActionResult> ExportPersonalData(CancellationToken cancellationToken)
    {
        var export = await mediator.Send(new ExportPersonalData(), cancellationToken);
        var json = JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions);
        return File(json, "application/json", $"clubnetz-personal-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
    }

    /// <summary>Deletes the account of the current user with all personal data (GDPR Art. 17) and signs them out.</summary>
    [HttpPost("DeleteAccount")]
    [Authorize(AuthorizationPolicies.AccountOwner)]
    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    public async Task DeleteAccount(DeleteAccountModel model, CancellationToken cancellationToken)
        => await mediator.Send(new DeleteOwnAccount(model.CurrentPassword), cancellationToken);

    [HttpGet("AvailableClubs")]
    public async Task<GetAvailableClubsResult> GetAvailableClubs(CancellationToken cancellationToken)
        => await mediator.Send(new GetAvailableClubs(), cancellationToken);

    [HttpPost("picture")]
    [Consumes("multipart/form-data")]
    public async Task UploadProfilePicture(IFormFile file, CancellationToken cancellationToken)
        => await mediator.Send(new UploadProfilePicture(file), cancellationToken);

    [HttpGet("picture/{userId:int}")]
    public async Task<IActionResult> GetProfilePicture(int userId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfilePicture(userId), cancellationToken);
        if (result is null)
            return NotFound();

        Response.Headers.CacheControl = "no-cache";
        return File(result.Data, result.ContentType);
    }
}
