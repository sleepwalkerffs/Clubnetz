using Bookennis.Api.Data;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Fusonic.Extensions.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Bookings;

/// <summary>
/// Who may change an existing booking when: shared by <see cref="EditBooking"/> and the single edit of <see cref="EditRecurringBooking"/>.
/// </summary>
internal static class BookingEditRules
{
    /// <summary>Roles that may change bookings that already started and move bookings into the past (e.g. to record a tournament).</summary>
    public static readonly MemberRole[] ElevatedRoles =
    [
        MemberRole.Maintainer,
        MemberRole.Admin,
        MemberRole.SportsDirector,
        MemberRole.YouthSportsDirector,
        MemberRole.Trainer,
    ];

    /// <summary>
    /// Rejects changing a booking that started (minus the grace period of the club) unless the user has an elevated role.
    /// Returns whether the user has an elevated role; then the past booking validation of the domain is skipped as well.
    /// </summary>
    /// <exception cref="PreconditionException"><see cref="EditBooking.ErrorCode.CannotEditBookingFromThePast"/></exception>
    public static async Task<bool> EnsureMayEdit(AppDbContext context, Booking existingBooking, int userId, CancellationToken cancellationToken)
    {
        var gracePeriod = await context.Clubs
            .Where(c => c.Id == existingBooking.ClubId)
            .Select(c => c.BookingGracePeriodInMinutes)
            .SingleRequiredAsync(cancellationToken);

        var memberRoles = await context.Set<Member>()
            .Where(m => m.UserId == userId && m.ClubId == existingBooking.ClubId)
            .OrderBy(m => m.MemberType) // ClubMember (0) before GuestMember (1)
            .Select(m => m.UserRoles)
            .FirstOrDefaultAsync(cancellationToken);

        var hasElevatedRole = memberRoles?.Any(r => ElevatedRoles.Contains(r)) ?? false;

        if (!hasElevatedRole && existingBooking.Interval.From < DateTimeOffset.UtcNow.AddMinutes((-gracePeriod) ?? 0))
            throw new PreconditionException(EditBooking.ErrorCode.CannotEditBookingFromThePast, "Cannot edit booking in the past");

        return hasElevatedRole;
    }
}
