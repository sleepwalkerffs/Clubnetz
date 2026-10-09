using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Profile.Models;

public record ClubProfileModel
{
    public required string ClubName { get; set; }
    public required int ClubId { get; init; }
    public required int MemberId { get; init; }
    public required MemberRole[] MemberRole { get; init; }

    /// <summary>An application administrator who opened a club they are not a member of: <see cref="MemberId"/> is 0.</summary>
    public bool IsSupportMode { get; init; }
}