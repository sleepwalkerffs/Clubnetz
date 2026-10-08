using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Members;

public record MemberResult
{
    public required int MemberId { get; set; }
    public required int? FamilyId { get; set; }
    public required string? Email { get; set; }
    public required string FullName { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required Gender Gender { get; set; }
    public required DateOnly Birthday { get; set; }
    public required MemberRole[] Roles { get; set; }
    public required int[] AllowedSeasonIds { get; set; }
    public required int BookingsPerWeek { get; set; }

    /// <summary>The member's email or, for children without an email, the email of a parent.</summary>
    public string? ContactEmail { get; set; }
    public string? ProfilePictureUrl { get; set; }

    /// <summary>Start of the last booking that already took place.</summary>
    public DateTimeOffset? LastPlayed { get; set; }
}