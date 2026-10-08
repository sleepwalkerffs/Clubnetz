using System.Drawing;
using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Members;

namespace Bookennis.Domain.Clubs;

[Guid("2355BF70-9389-4FEB-AE70-4816DCE007C0")]
public class PlayMode : TenantDomainEntity
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private PlayMode() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    internal PlayMode(Club club, MemberRole[] allowedRoles, Color color, int? fixedPlayerCount, bool isChargingBookingSubscription, string name, TimeSpan? fixedDuration, bool canOverbook, bool commentAllowed, int? maxBookingsPerSeason = null, bool allowRecurring = false)
    {
        Club = club;
        ClubId = club.Id;
        AllowedRoles = allowedRoles;
        Color = color;
        FixedPlayerCount = fixedPlayerCount;
        IsChargingBookingSubscription = isChargingBookingSubscription;
        Name = name;
        FixedDuration = fixedDuration;
        CanOverbook = canOverbook;
        CommentAllowed = commentAllowed;
        MaxBookingsPerSeason = maxBookingsPerSeason;
        AllowRecurring = allowRecurring;
    }
    public Club Club { get; private set; }
    public MemberRole[] AllowedRoles { get; private set; }
    public bool CanOverbook { get; private set; }
    public Color Color { get; private set; }
    public int? FixedPlayerCount { get; private set; }
    public bool IsChargingBookingSubscription { get; private set; }
    public string Name { get; private set; }
    public TimeSpan? FixedDuration { get; private set; }
    public bool CommentAllowed { get; private set; }
    public int? MaxBookingsPerSeason { get; private set; }
    public bool AllowRecurring { get; private set; }

    public void Update(MemberRole[] allowedRoles, Color color, int? fixedPlayerCount, bool isChargingBookingSubscription, string name, TimeSpan? fixedDuration, bool canOverbook, bool commentAllowed, int? maxBookingsPerSeason = null, bool allowRecurring = false)
    {
        AllowedRoles = allowedRoles;
        Color = color;
        FixedPlayerCount = fixedPlayerCount;
        IsChargingBookingSubscription = isChargingBookingSubscription;
        Name = name;
        FixedDuration = fixedDuration;
        CanOverbook = canOverbook;
        CommentAllowed = commentAllowed;
        MaxBookingsPerSeason = maxBookingsPerSeason;
        AllowRecurring = allowRecurring;
    }
}
