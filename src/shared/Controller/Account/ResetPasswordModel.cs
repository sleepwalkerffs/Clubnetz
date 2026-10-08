namespace Bookennis.Shared.Controller.Account;
public record ResetPasswordModel(string Email, string Token, string NewPassword)
{
    public string Email { get; init; } = Email.Trim();
}
