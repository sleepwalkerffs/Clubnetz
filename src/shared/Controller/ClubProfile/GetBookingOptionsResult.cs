namespace Bookennis.Shared.Controller.ClubProfile;

public record GetBookingOptionsResult
{
    public bool IsAllowedToBook { get; set; }
    public bool IsBookingOnlyForChildren { get; set; }
    public List<int> EligibleChildMemberIds { get; set; } = [];

    /// <summary>See <c>GetClubProfileResult.IsActiveMember</c>. Members who are not active may buy court time (<c>CourtSales/options</c>).</summary>
    public bool IsActiveMember { get; set; }
}