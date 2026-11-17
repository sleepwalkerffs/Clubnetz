using Bookennis.Shared.Controller.Shared;
namespace Bookennis.Shared.Controller.ClubProfile;

public record GetBookingOptionsResult
{
    public bool IsAllowedToBook { get; set; }
    public bool IsBookingOnlyForChildren { get; set; }
    public List<int> EligibleChildMemberIds { get; set; } = [];

    /// <summary>See <c>GetClubProfileResult.IsActiveMember</c>. Members who are not active may buy court time (<c>CourtSales/options</c>).</summary>
    public bool IsActiveMember { get; set; }

    /// <summary>
    /// The sports <see cref="IsAllowedToBook"/> holds for (the member's enrollment, or the children's together); empty = every sport.
    /// A court of another sport is booked like by a member who is not enrolled: bought (<c>CourtSales</c>).
    /// </summary>
    public List<Sport> EnrolledSports { get; set; } = [];
}