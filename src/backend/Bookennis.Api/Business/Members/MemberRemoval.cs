using Bookennis.Api.Data;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Members;

/// <summary>
/// Cleans up everything that refers to members which are about to be deleted (a member leaves a club, a user deletes their account).
/// The database cascades most of it (booking players, badges, seasons, event registrations, ...), this handles what it can't:
/// <list type="bullet">
/// <item>Family links: <c>FamilyMember.MemberId</c> has no foreign key. Families without a parent left are deleted.</item>
/// <item>Upcoming bookings (and recurring series) that nobody else plays in would stay as empty bookings that block the court.</item>
/// </list>
/// Changes are only tracked, the caller saves them together with the removal of the members/users.
/// </summary>
public static class MemberRemoval
{
    public static async Task PrepareRemoval(AppDbContext context, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
            return;

        await RemoveFamilyLinks(context, memberIds, cancellationToken);
        await RemoveBookingsWithoutRemainingPlayers(context, memberIds, cancellationToken);
    }

    /// <summary>
    /// Returns the name of a club that would be left without an admin if the given members were removed, <c>null</c> if there is none.
    /// </summary>
    public static async Task<string?> FindClubLosingItsLastAdmin(AppDbContext context, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
        => await (
            from member in context.ClubMembers.IgnoreQueryFilters()
            join club in context.Clubs.IgnoreQueryFilters() on member.ClubId equals club.Id
            where memberIds.Contains(member.Id)
               && member.UserRoles.Contains(MemberRole.Admin)
               && !context.ClubMembers.IgnoreQueryFilters().Any(other => other.ClubId == member.ClubId
                                                                     && !memberIds.Contains(other.Id)
                                                                     && other.UserRoles.Contains(MemberRole.Admin))
            orderby club.Name
            select club.Name
        ).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The users the given user created for their children (<see cref="User.BelongsToUserId"/>). A child that still has another parent in one
    /// of its families is handed over to that parent, the others are returned so they can be deleted together with the user.
    /// </summary>
    public static async Task<List<User>> HandOverOrCollectOwnedChildren(AppDbContext context, int userId, CancellationToken cancellationToken)
    {
        var children = await context.Users.Where(u => u.BelongsToUserId == userId).ToListAsync(cancellationToken);
        var childrenToDelete = new List<User>();

        foreach (var child in children)
        {
            var otherParentUserId = await (
                from childLink in context.FamilyMembers.OfType<ChildFamilyMember>()
                join childMember in context.Set<Member>().IgnoreQueryFilters() on childLink.MemberId equals childMember.Id
                where childMember.UserId == child.Id
                from parentLink in context.FamilyMembers.OfType<ParentFamilyMember>().Where(p => p.ParentFamilyId == childLink.ChildFamilyId)
                join parentMember in context.Set<Member>().IgnoreQueryFilters() on parentLink.MemberId equals parentMember.Id
                where parentMember.UserId != userId
                orderby parentMember.Id
                select (int?)parentMember.UserId
            ).FirstOrDefaultAsync(cancellationToken);

            if (otherParentUserId is { } newOwnerUserId)
                child.TransferTo(newOwnerUserId);
            else
                childrenToDelete.Add(child);
        }

        return childrenToDelete;
    }

    private static async Task RemoveFamilyLinks(AppDbContext context, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
    {
        var familyIds = await context.FamilyMembers
            .IgnoreQueryFilters()
            .Where(f => memberIds.Contains(f.MemberId))
            .Select(f => f.FamilyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Parents, children and family members are auto-included
        var families = await context.Families
            .IgnoreQueryFilters()
            .Where(f => familyIds.Contains(f.Id))
            .ToListAsync(cancellationToken);

        foreach (var family in families)
        {
            foreach (var link in family.FamilyMembers.Where(f => memberIds.Contains(f.MemberId)).ToList())
                context.Remove(link);

            if (family.Parents.All(p => memberIds.Contains(p.MemberId)))
                context.Remove(family);
        }
    }

    private static async Task RemoveBookingsWithoutRemainingPlayers(AppDbContext context, IReadOnlyCollection<int> memberIds, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var bookings = await context.Bookings
            .IgnoreQueryFilters()
            .Where(b => b.Interval.From > now
                     && b.Players.Any(p => memberIds.Contains(p.MemberId))
                     && b.Players.All(p => memberIds.Contains(p.MemberId)))
            .ToListAsync(cancellationToken);
        context.Bookings.RemoveRange(bookings);

        var series = await context.RecurringBookingSeries
            .IgnoreQueryFilters()
            .Where(s => s.SeriesPlayers.Any(p => memberIds.Contains(p.MemberId))
                     && s.SeriesPlayers.All(p => memberIds.Contains(p.MemberId)))
            .ToListAsync(cancellationToken);
        context.RecurringBookingSeries.RemoveRange(series);
    }
}
