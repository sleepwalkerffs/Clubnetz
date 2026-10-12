using Bookennis.Shared.Controller.Memberships;

namespace Bookennis.Shared.Controller.Members;

public record MembersDetailResult : MemberResult
{
    /// <summary>How the member got into each season they are enrolled in.</summary>
    public List<MemberSeasonEnrollment> SeasonEnrollments { get; set; } = [];
}

public record MemberSeasonEnrollment(int SeasonId, MembershipEnrollmentSource Source);