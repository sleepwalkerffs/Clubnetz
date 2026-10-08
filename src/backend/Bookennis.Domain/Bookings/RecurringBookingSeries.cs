using System.Runtime.InteropServices;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Bookings;

[Guid("A7C3E1D4-5F82-4B9A-8E61-2D7F0C4A9B35")]
public class RecurringBookingSeries : TenantDomainEntity, IAggregateRoot
{
    private readonly List<RecurringBookingSeriesPlayer> seriesPlayers = new();

#pragma warning disable 8618
    private RecurringBookingSeries() { }
#pragma warning restore 8618

    public RecurringBookingSeries(
        int clubId,
        int courtId,
        int playModeId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        string timeZoneInfoId,
        int recurrenceIntervalWeeks,
        DateOnly startDate,
        DateOnly? endDate,
        string? comment,
        List<int>? playerIds = null)
    {
        ClubId = clubId;
        CourtId = courtId;
        PlayModeId = playModeId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        TimeZoneInfoId = timeZoneInfoId;
        RecurrenceIntervalWeeks = recurrenceIntervalWeeks;
        StartDate = startDate;
        EndDate = endDate;
        Comment = comment;
        AssignPlayers(playerIds);
    }

    public int CourtId { get; private set; }
    public int PlayModeId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string TimeZoneInfoId { get; private set; }
    public int RecurrenceIntervalWeeks { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? Comment { get; private set; }
    public IReadOnlyList<RecurringBookingSeriesPlayer> SeriesPlayers => seriesPlayers.AsReadOnly();

    public void Update(
        int courtId,
        int playModeId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        string timeZoneInfoId,
        int recurrenceIntervalWeeks,
        DateOnly startDate,
        DateOnly? endDate,
        string? comment,
        List<int>? playerIds = null)
    {
        CourtId = courtId;
        PlayModeId = playModeId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        TimeZoneInfoId = timeZoneInfoId;
        RecurrenceIntervalWeeks = recurrenceIntervalWeeks;
        StartDate = startDate;
        EndDate = endDate;
        Comment = comment;
        AssignPlayers(playerIds);
    }

    private void AssignPlayers(List<int>? playerIds)
    {
        seriesPlayers.Clear();
        if (playerIds is not null)
        {
            foreach (var playerId in playerIds)
            {
                seriesPlayers.Add(new RecurringBookingSeriesPlayer(this, playerId));
            }
        }
    }
}
