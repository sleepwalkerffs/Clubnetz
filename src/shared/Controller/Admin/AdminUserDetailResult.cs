using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Admin;

public record AdminUserDetailResult
{
    public required int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }
    public required string? Email { get; init; }
    public required string? UserName { get; init; }
    public required bool EmailConfirmed { get; init; }
    public required DateOnly Birthday { get; init; }
    public required Gender Gender { get; init; }
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
    public required Country Country { get; init; }
    public DateTime RegisteredAt { get; init; }

    /// <summary>The clubs the user belongs to, as club member or as guest.</summary>
    public List<AdminUserClubResult> Clubs { get; init; } = [];
}

public record AdminUserClubResult
{
    public required int ClubId { get; init; }
    public required string ClubName { get; init; }
    public required int MemberId { get; init; }
    public required MemberRole[] Roles { get; init; }
    public required bool IsGuest { get; init; }
}
