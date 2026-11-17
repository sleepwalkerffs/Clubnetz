using Bookennis.Shared.Controller.Memberships;
using Bookennis.Shared.Controller.Shared;

namespace Bookennis.Shared.Controller.Members;

public record MembersDetailResult : MemberResult
{
    /// <summary>How the member got into each season they are enrolled in.</summary>
    public List<MemberSeasonEnrollment> SeasonEnrollments { get; set; } = [];
}

/// <summary><c>Sports</c>: what the enrollment books for free; empty = every sport.</summary>
public record MemberSeasonEnrollment(int SeasonId, MembershipEnrollmentSource Source)
{
    public List<Sport> Sports { get; init; } = [];
}