using System.Runtime.InteropServices;
using Bookennis.Domain.Base;
using Bookennis.Domain.Bookings.Events;
using Bookennis.Global;
using Bookennis.Global.Intervals;

namespace Bookennis.Domain.Bookings;

[Guid("08FE3B51-3557-4271-9970-4DB93A70E71C")]
public class Booking : TenantDomainEntity, IAggregateRoot
{
    private readonly List<BookingPlayer> players = new();

#pragma warning disable 8618
    private Booking() { }
#pragma warning restore 8618

    public Booking(int clubId, int courtId, int playModeId, DateTimeOffsetInterval interval, string timeZoneInfoId, List<int>? playerIds = null, string? comment = null, int? recurringBookingSeriesId = null)
    {
        Interval = interval;
        ClubId = clubId;
        CourtId = courtId;
        TimeZoneInfoId = timeZoneInfoId;
        Comment = comment.CleanNullable();
        RecurringBookingSeriesId = recurringBookingSeriesId;
        AssignPlayers(playModeId, playerIds);
        AddDomainEvent(new BookingAddedDomainEvent());
    }

    public DateTimeOffsetInterval Interval { get; private set; }
    public string TimeZoneInfoId { get; private set; }
    public int CourtId { get; private set; }
    public int PlayModeId { get; private set; }
    public string? Comment { get; private set; }
    public int? RecurringBookingSeriesId { get; private set; }
    public RecurringBookingSeries? RecurringBookingSeries { get; private set; }
    public bool IsExcludedFromSeries { get; private set; }

    /// <summary>When the players were reminded of this booking (push notification and email).</summary>
    public DateTimeOffset? ReminderSentAt { get; private set; }

    public IReadOnlyList<BookingPlayer> Players => players.AsReadOnly();

    public void Update(int courtId, int playModeId, DateTimeOffsetInterval interval, string timeZoneInfoId, List<int> playerIds, string? comment)
    {
        // A booking that was moved gets a new reminder
        if (interval.From != Interval.From)
            ReminderSentAt = null;

        CourtId = courtId;
        Interval = interval;
        TimeZoneInfoId = timeZoneInfoId;
        Comment = comment.CleanNullable();
        AssignPlayers(playModeId, playerIds);
        AddDomainEvent(new BookingUpdatedDomainEvent());
    }

    public void AssignPlayers(int playModeId, List<int>? players)
    {
        PlayModeId = playModeId;

        this.players.Clear();
        if (players is not null)
        {
            foreach (var player in players)
            {
                this.players.Add(new BookingPlayer(this, player));
            }
        }
    }

    public void MarkReminderSent() => ReminderSentAt = DateTimeOffset.UtcNow;

    public void ExcludeFromSeries()
    {
        IsExcludedFromSeries = true;
    }

    public void DetachFromSeries()
    {
        RecurringBookingSeriesId = null;
        IsExcludedFromSeries = false;
    }
}
