namespace Bookennis.Domain.Members;

public class MemberBadgeSettings
{
    private MemberBadgeSettings() { }

    public MemberBadgeSettings(int memberId)
    {
        MemberId = memberId;
        TrophyCasePublic = true;
    }

    public int MemberId { get; private set; }
    public int? DisplayBadgeId { get; private set; }
    public int? DisplayOneTimeBadgeId { get; private set; }
    public bool TrophyCasePublic { get; private set; }

    public void SetDisplayBadge(int? memberBadgeId)
    {
        DisplayBadgeId = memberBadgeId;
        if (memberBadgeId is not null)
            DisplayOneTimeBadgeId = null;
    }

    public void SetDisplayOneTimeBadge(int? memberOneTimeBadgeId)
    {
        DisplayOneTimeBadgeId = memberOneTimeBadgeId;
        if (memberOneTimeBadgeId is not null)
            DisplayBadgeId = null;
    }

    public void SetTrophyCaseVisibility(bool isPublic)
    {
        TrophyCasePublic = isPublic;
    }
}
