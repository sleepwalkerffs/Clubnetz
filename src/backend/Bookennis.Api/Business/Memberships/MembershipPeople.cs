using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Families;
using Bookennis.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Memberships;

/// <summary>What the membership rules need to know about a club member in a season.</summary>
public record MembershipPerson(
    int MemberId,
    int UserId,
    string FirstName,
    string LastName,
    int? BirthYear,
    MembershipEligibility.FamilyRole Role,
    int? FamilyId,
    bool EnrolledInEarlierSeason,
    int? EnrolledByOrderId,
    bool IsEnrolled,
    IReadOnlyList<MembershipActiveOrder> ActiveOrders)
{
    public bool IsChild => Role == MembershipEligibility.FamilyRole.Child;

    /// <summary>The first order of the season that covers the member and is not cancelled, failed or refunded.</summary>
    public int? ActiveOrderId => ActiveOrders.Count > 0 ? ActiveOrders[0].OrderId : null;

    public MembershipOrderStatus? ActiveOrderStatus => ActiveOrders.Count > 0 ? ActiveOrders[0].Status : null;

    /// <summary>Covered by an order of the season that is not cancelled, failed or refunded (for any sport).</summary>
    public bool HasMembership => ActiveOrders.Count > 0;

    /// <summary>
    /// <b>One membership per sport and season</b>: covered by an active order (other than <paramref name="ignoreOrderId"/>)
    /// whose plan shares a sport with <paramref name="sports"/>. A plan without sports is for every sport.
    /// </summary>
    public bool HasMembershipFor(IReadOnlyCollection<Sport> sports, int? ignoreOrderId = null)
        => ActiveOrders.Any(o => o.OrderId != ignoreOrderId && Sports.Overlap(o.Sports, sports));

    /// <summary>Counts as a member of the season for the family discount: enrolled, or covered by an order that is paid or being paid.</summary>
    public bool HasPaidMembership => IsEnrolled || ActiveOrderStatus is MembershipOrderStatus.Paid or MembershipOrderStatus.Processing;

    public MembershipEligibility.Candidate ToCandidate(IReadOnlyCollection<Sport> sports, int? ignoreOrderId = null)
        => new(MemberId, BirthYear, Role, FamilyId, EnrolledInEarlierSeason, HasMembershipFor(sports, ignoreOrderId));
}

/// <summary>An active order of the season that covers a member, with the sports of its plan (empty = every sport).</summary>
public record MembershipActiveOrder(int OrderId, MembershipOrderStatus Status, Sport[] Sports);

/// <summary>The club members of a club (or some of them) with their family and what they have in a season.</summary>
public class MembershipPeople(IReadOnlyList<MembershipPerson> people)
{
    private readonly Dictionary<int, MembershipPerson> byMemberId = people.ToDictionary(p => p.MemberId);

    public IReadOnlyList<MembershipPerson> All => people;

    public MembershipPerson? Find(int memberId) => byMemberId.GetValueOrDefault(memberId);

    /// <summary>The parents of the family the members belong to who are not among them.</summary>
    public List<MembershipEligibility.FamilyAdult> OtherFamilyAdults(IReadOnlyCollection<MembershipPerson> members)
    {
        var familyIds = members.Where(m => m.FamilyId is not null).Select(m => m.FamilyId).ToHashSet();
        var memberIds = members.Select(m => m.MemberId).ToHashSet();

        return people
            .Where(p => p.Role == MembershipEligibility.FamilyRole.Parent && familyIds.Contains(p.FamilyId) && !memberIds.Contains(p.MemberId))
            .Select(p => new MembershipEligibility.FamilyAdult(p.MemberId, p.HasPaidMembership))
            .ToList();
    }

    public IReadOnlyList<MembershipEligibility.ErrorCode> Check(MembershipPlan plan, DateOnly today, IReadOnlyCollection<MembershipPerson> members, int? ignoreOrderId = null)
        => MembershipEligibility.Check(plan, today, members.Select(m => m.ToCandidate(plan.Sports, ignoreOrderId)).ToList(), OtherFamilyAdults(members));

    /// <summary>
    /// The members a member may order for: themself and, as a parent, everybody in the family.
    /// It is the same right that lets a parent book for the children.
    /// </summary>
    public List<MembershipPerson> OrderableBy(int memberId)
    {
        var me = Find(memberId);
        if (me is null)
            return [];

        if (me.Role != MembershipEligibility.FamilyRole.Parent)
            return [me];

        return people
            .Where(p => p.FamilyId == me.FamilyId)
            .OrderBy(p => p.MemberId == memberId ? 0 : 1).ThenBy(p => p.IsChild).ThenBy(p => p.FirstName).ThenBy(p => p.LastName)
            .ToList();
    }

    /// <summary>
    /// Loads the club members of the club. With <paramref name="aroundMemberIds"/> only these members and the members of their families
    /// (the families are needed for the rules), otherwise all members of the club.
    /// </summary>
    public static async Task<MembershipPeople> Load(AppDbContext context, int clubId, Season season, IReadOnlyCollection<int>? aroundMemberIds, CancellationToken cancellationToken)
    {
        var members = context.ClubMembers.Where(m => m.ClubId == clubId);

        if (aroundMemberIds is not null)
        {
            var familyIds = context.FamilyMembers.Where(fm => aroundMemberIds.Contains(fm.MemberId)).Select(fm => fm.FamilyId);
            members = members.Where(m => aroundMemberIds.Contains(m.Id)
                                      || context.FamilyMembers.Any(fm => fm.MemberId == m.Id && familyIds.Contains(fm.FamilyId)));
        }

        var rows = await (
            from member in members
            join user in context.Users on member.UserId equals user.Id
            orderby user.LastName, user.FirstName, member.Id
            select new { member.Id, member.UserId, user.FirstName, user.LastName, user.Birthday }
        ).ToListAsync(cancellationToken);

        var memberIds = rows.ConvertAll(r => r.Id);

        var familyLinks = await context.FamilyMembers
            .Where(fm => memberIds.Contains(fm.MemberId))
            .Select(fm => new { fm.MemberId, fm.FamilyId, IsParent = fm is ParentFamilyMember })
            .ToListAsync(cancellationToken);
        var familyByMember = familyLinks.GroupBy(l => l.MemberId).ToDictionary(g => g.Key, g => g.First());

        var enrollments = await (
            from memberSeason in context.MemberSeasons
            join enrolledSeason in context.Seasons on memberSeason.SeasonId equals enrolledSeason.Id
            where memberIds.Contains(memberSeason.MemberId) && enrolledSeason.ClubId == clubId
            select new { memberSeason.MemberId, memberSeason.SeasonId, memberSeason.MembershipOrderId, enrolledSeason.Period.From }
        ).ToListAsync(cancellationToken);
        var enrollmentsByMember = enrollments.ToLookup(e => e.MemberId);

        var activeOrders = await (
            from orderMember in context.MembershipOrderMembers
            join order in context.MembershipOrders on orderMember.MembershipOrderId equals order.Id
            where order.ClubId == clubId
               && order.SeasonId == season.Id
               && MembershipOrder.ActiveStatuses.Contains(order.Status)
               && memberIds.Contains(orderMember.MemberId)
            orderby order.Id
            select new { orderMember.MemberId, OrderId = order.Id, order.Status, order.Sports }
        ).ToListAsync(cancellationToken);
        var activeOrdersByMember = activeOrders.ToLookup(o => o.MemberId, o => new MembershipActiveOrder(o.OrderId, o.Status, o.Sports));

        var people = rows.ConvertAll(row =>
        {
            var family = familyByMember.GetValueOrDefault(row.Id);
            var memberEnrollments = enrollmentsByMember[row.Id].ToList();
            var current = memberEnrollments.FirstOrDefault(e => e.SeasonId == season.Id);

            return new MembershipPerson(
                row.Id,
                row.UserId,
                row.FirstName,
                row.LastName,
                // Users that were created without a birthday carry the default date
                row.Birthday.Year >= MembershipPlan.MinBirthYear ? row.Birthday.Year : null,
                family is null ? MembershipEligibility.FamilyRole.None : family.IsParent ? MembershipEligibility.FamilyRole.Parent : MembershipEligibility.FamilyRole.Child,
                family?.FamilyId,
                memberEnrollments.Any(e => e.From < season.Period.From),
                current?.MembershipOrderId,
                current is not null,
                activeOrdersByMember[row.Id].ToList());
        });

        return new MembershipPeople(people);
    }
}
