using System.ComponentModel.DataAnnotations;

namespace Bookennis.Shared.Controller.Account;

public record LoginUserModel([Required, EmailAddress] string Email, [Required] string Password, [Required] bool RememberMe)
{
    public string Email { get; init; } = Email.Trim();
}
