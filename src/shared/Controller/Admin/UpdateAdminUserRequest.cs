using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record UpdateAdminUserRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly Birthday { get; init; }
    public required Gender Gender { get; init; }
    // The address is optional. Nullable, because MVC rejects empty values of non-nullable strings
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? ZipCode { get; init; }
    public required Country Country { get; init; }
}
