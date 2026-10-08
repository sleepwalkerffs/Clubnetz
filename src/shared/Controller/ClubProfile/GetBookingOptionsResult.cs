namespace Bookennis.Shared.Controller.ClubProfile;

public record GetBookingOptionsResult
{
    public bool IsAllowedToBook { get; set; }
    public bool IsBookingOnlyForChildren { get; set; }
    public List<int> EligibleChildMemberIds { get; set; } = [];
}