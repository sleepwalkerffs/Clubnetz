using Bookennis.Api.Business.Account;
using Bookennis.Api.Business.Admin;
using Bookennis.Api.Infrastructure.Configuration;
using Bookennis.Domain.User;
using Bookennis.Shared.Controller.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookennis.Api.Controllers.Account;

public class AccountController(IMediator mediator) : ControllerBase
{
    [EnableRateLimiting(RateLimitingConfiguration.EmailPolicy)]
    [HttpPost("Register")]
    public Task RegisterUser(RegisterUserModel model, CancellationToken cancellationToken) =>
        mediator.Send(
            new RegisterUser(model.FirstName, model.LastName, model.Birthday, model.Email, model.UserName, model.Password, (Gender)model.Gender, model.Street, model.City, model.ZipCode, (Domain.User.Country)model.Country, model.AcceptPrivacyPolicy),
            cancellationToken
        );

    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    [HttpGet("ConfirmEmail")]
    public Task ConfirmEmail([FromQuery] string token, [FromQuery] string email, CancellationToken cancellationToken) => mediator.Send(new ConfirmEmail(email, token), cancellationToken);

    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    [HttpGet("ConfirmEmailChange")]
    public Task ConfirmEmailChange([FromQuery] int userId, [FromQuery] string email, [FromQuery] string token, CancellationToken cancellationToken)
        => mediator.Send(new ConfirmEmailChange(userId, email, token), cancellationToken);

    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    [HttpPost("Login")]
    public Task Login(LoginUserModel model, CancellationToken cancellationToken) => mediator.Send(new LoginUser(model.Email, model.Password, model.RememberMe), cancellationToken);

    [HttpPost("Logout")]
    public Task Logout(CancellationToken cancellationToken) => mediator.Send(new LogoutUser(), cancellationToken);

    [HttpGet("User")]
    public Task<GetAuthenticatedUserResult> GetUser(CancellationToken cancellationToken) => mediator.Send(new GetAuthenticatedUser(), cancellationToken);

    [EnableRateLimiting(RateLimitingConfiguration.EmailPolicy)]
    [HttpPost("Password/Reset")]
    public Task ForgotPassword(ForgotPasswordModel model, CancellationToken cancellationToken) => mediator.Send(new ForgotPassword(model.Email), cancellationToken);

    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    [HttpPost("Password/Reset/Confirm")]
    public Task ResetPassword(ResetPasswordModel model, CancellationToken cancellationToken) => mediator.Send(new ResetPassword(model.Email, model.Token, model.NewPassword), cancellationToken);

    [EnableRateLimiting(RateLimitingConfiguration.AuthenticationPolicy)]
    [HttpPost("Guest/Login")]
    public async Task LoginUser(LoginGuestRequest request, CancellationToken cancellationToken)
        => await mediator.Send(new LoginGuest(request.GuestCode, request.ClubId), cancellationToken);
}
