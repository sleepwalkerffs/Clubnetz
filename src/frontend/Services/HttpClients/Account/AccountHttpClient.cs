using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Bookennis.Shared.Controller.Account;

namespace Bookennis.Client.Services.HttpClients.Account;

public class AccountHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IAccountHttpClient
{
    public async Task<HttpResult> ConfirmEmail(string email, string token, CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["email"] = email;
        query["token"] = token;

        return await (await httpClient.GetAsync("ConfirmEmail?" + query, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> ConfirmEmailChange(int userId, string email, string token, CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["userId"] = userId.ToString();
        query["email"] = email;
        query["token"] = token;

        return await (await httpClient.GetAsync("ConfirmEmailChange?" + query, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult> ForgotPassword(ForgotPasswordModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("Password/Reset", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> ResetPassword(ResetPasswordModel model, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("Password/Reset/Confirm", model, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<GetAuthenticatedUserResult>> GetAuthenticatedUser(CancellationToken cancellationToken)
        => await (await httpClient.GetAsync("User", cancellationToken)).AsHttpResult<GetAuthenticatedUserResult>(jsonOptions, cancellationToken);

    public async Task<HttpResult> LoginUser(LoginUserModel userModel, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("Login", userModel, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> LogoutUser(CancellationToken cancellationToken)
        => await (await httpClient.PostAsync("Logout", null, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult> RegisterUser(RegisterUserModel userModel, CancellationToken cancellationToken)
        => await (await httpClient.PostAsJsonAsync("Register", userModel, jsonOptions, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
    public async Task<HttpResult> LoginGuest(LoginGuestRequest request, CancellationToken cancellationToken = default)
        => await (await httpClient.PostAsJsonAsync("Guest/Login?", request, cancellationToken)).AsHttpResult(jsonOptions, cancellationToken);
}