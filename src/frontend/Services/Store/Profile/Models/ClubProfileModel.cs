using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Client.Services.Store.Profile.Models;

public record ClubProfileModel
{
    public required string ClubName { get; set; }
    public required int ClubId { get; init; }
    public required int MemberId { get; init; }
    public required MemberRole[] MemberRole { get; init; }
}