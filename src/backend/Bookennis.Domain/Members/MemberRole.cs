namespace Bookennis.Domain.Members;

public enum MemberRole
{
    User = 0,
    Maintainer = 1,
    Admin = 2,
    SportsDirector = 3,
    YouthSportsDirector = 4,
    Trainer = 5,
    Guest = 6,
    Treasurer = 7,

    /// <summary>Runs the clubhouse ("Wirt"): sells food and drinks and keeps the guests' tabs. Not a staff role of the club.</summary>
    ClubhouseHost = 8,
}