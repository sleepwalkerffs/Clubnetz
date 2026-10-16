using Bookennis.Api.Business.Members;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Booking.Shared;

namespace Bookennis.Api.Business.Bookings;

/// <summary>
/// The calendar for members who are not active: a booking they (or a child of their family) don't play in only shows that the court
/// is booked. No players, no comment, no play mode, no series.
/// </summary>
public static class BookingAnonymizer
{
    /// <summary>Gray, like a court that is simply taken.</summary>
    public const int NeutralColor = unchecked((int)0xFF9E9E9E);

    public static readonly PlayModeDto NeutralPlayMode = new()
    {
        Id = 0,
        AllowedRoles = [],
        Color = NeutralColor,
        FixedPlayerCount = null,
        IsChargingBookingSubscription = false,
        Name = "",
        FixedDuration = null,
        CanOverbook = false,
        CommentAllowed = false,
        MaxBookingsPerSeason = null,
        AllowRecurring = false,
    };

    public static Task<HashSet<int>> VisibleMemberIds(AppDbContext context, int clubId, int userId, CancellationToken cancellationToken)
        => ActiveMembership.OwnAndChildMemberIds(context, clubId, userId, cancellationToken);

    public static BookingResult Anonymize(BookingResult booking, IReadOnlySet<int> visibleMemberIds)
        => booking.Players.Any(p => visibleMemberIds.Contains(p.MemberId))
            ? booking
            : booking with
            {
                Players = [],
                Comment = null,
                PlayMode = NeutralPlayMode,
                RecurringBookingSeriesId = null,
                IsExcludedFromSeries = false,
                IsAnonymized = true,
            };
}
