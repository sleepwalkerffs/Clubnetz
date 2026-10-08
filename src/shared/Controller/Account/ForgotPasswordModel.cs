using Bookennis.Global;

namespace Bookennis.Shared.Controller.Account;
public record ForgotPasswordModel(string Email)
{
    public string Email { get; init; } = Email.Clean();
}
