using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record UpdateAdminUserRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly Birthday { get; init; }
    public required Gender Gender { get; init; }
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
    public required Country Country { get; init; }
}
