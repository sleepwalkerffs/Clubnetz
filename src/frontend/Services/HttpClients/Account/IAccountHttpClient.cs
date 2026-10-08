using Bookennis.Shared.Controller.Account;

namespace Bookennis.Client.Services.HttpClients.Account;

public interface IAccountHttpClient
{
    public Task<HttpResult> RegisterUser(RegisterUserModel userModel, CancellationToken cancellationToken = default);
    public Task<HttpResult> ConfirmEmail(string email, string token, CancellationToken cancellationToken = default);
    public Task<HttpResult> ConfirmEmailChange(int userId, string email, string token, CancellationToken cancellationToken = default);
    public Task<HttpResult> ResetPassword(ResetPasswordModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> ForgotPassword(ForgotPasswordModel model, CancellationToken cancellationToken = default);
    public Task<HttpResult> LoginUser(LoginUserModel userModel, CancellationToken cancellationToken = default);
    public Task<HttpResult<GetAuthenticatedUserResult>> GetAuthenticatedUser(CancellationToken cancellationToken = default);
    public Task<HttpResult> LogoutUser(CancellationToken cancellationToken = default);
    public Task<HttpResult> LoginGuest(LoginGuestRequest request, CancellationToken cancellationToken = default);
}
