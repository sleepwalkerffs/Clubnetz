using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Profile;

public record GetUserProfileResult
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly Birthday { get; init; }
    public required string Email { get; init; }
    public required string UserName { get; init; }
    public required Gender Gender { get; init; }
    public required Language Language { get; init; }
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
    public required Country Country { get; init; }
    public required List<int> AvailableClubIds { get; init; }
    public required int FavoriteClubId { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public bool ProfileCompletionBannerDismissed { get; init; }
    /// <summary>Signed in via a guest link. The email and password cannot be changed in such a session.</summary>
    public bool IsGuestSession { get; init; }
}

