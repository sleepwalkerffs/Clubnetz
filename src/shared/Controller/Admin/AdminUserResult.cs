using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record AdminUserResult
{
    public required int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }
    public required string? Email { get; init; }
    public required bool EmailConfirmed { get; init; }
    public required DateOnly Birthday { get; init; }
    public required Gender Gender { get; init; }
}
