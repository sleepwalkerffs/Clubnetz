using System.ComponentModel.DataAnnotations;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Account;

public record RegisterUserModel(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] DateOnly Birthday,
    [Required] string Email,
    [Required] string UserName,
    [Required] string Password,
    [Required] Gender Gender,
    string? Street,
    string? City,
    string? ZipCode,
    [Required] Country Country,
    bool AcceptPrivacyPolicy
)
{
    public string FirstName { get; init; } = FirstName.Trim();
    public string LastName { get; init; } = LastName.Trim();
    public string Email { get; init; } = Email.Trim();
    public string UserName { get; init; } = UserName.Trim();
    public string Street { get; init; } = Street?.Trim() ?? string.Empty;
    public string City { get; init; } = City?.Trim() ?? string.Empty;
    public string ZipCode { get; init; } = ZipCode?.Trim() ?? string.Empty;
}
