using System.ComponentModel.DataAnnotations;
using Bookennis.Global;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Profile;

public record UpdateUserProfileModel([Required] string FirstName, [Required] string LastName, [Required] DateOnly Birthday, [Required] Gender Gender, [Required] Language Language, string? Street, string? City, string? ZipCode, [Required] Country Country)
{
    public string FirstName { get; init; } = FirstName.Clean();
    public string LastName { get; init; } = LastName.Clean();
    public string Street { get; init; } = Street?.Trim() ?? string.Empty;
    public string City { get; init; } = City?.Trim() ?? string.Empty;
    public string ZipCode { get; init; } = ZipCode?.Trim() ?? string.Empty;
}
