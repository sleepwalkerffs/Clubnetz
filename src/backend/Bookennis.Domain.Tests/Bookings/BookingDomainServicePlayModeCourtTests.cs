using System.Drawing;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Xunit;

namespace Bookennis.Domain.Tests.Bookings;

/// <summary>Play modes limited to some courts (<see cref="PlayMode.CourtIds"/>).</summary>
public class BookingDomainServicePlayModeCourtTests
{
    private const int CourtId = 5;

    [Fact]
    public void PlayModeWithoutCourts_CanBeBookedOnEveryCourt()
    {
        var booking = new BookingDomainService().BookCourt(Context([]), Sunday(), "UTC", null);

        booking.CourtId.Should().Be(CourtId);
    }

    [Fact]
    public void PlayModeLimitedToTheCourt_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context([3, CourtId]), Sunday(), "UTC", null);

        booking.CourtId.Should().Be(CourtId);
    }

    [Fact]
    public void PlayModeLimitedToOtherCourts_IsRejected()
    {
        var act = () => new BookingDomainService().BookCourt(Context([3, 4]), Sunday(), "UTC", null);

        var error = act.Should().Throw<PreconditionException>().Which;
        error.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.PlayModeNotAllowedOnCourt));
        error.ErrorDetails.Should().Equal("Doubles", "Court 5");
    }

    [Fact]
    public void PlayModeLimitedToOtherCourts_EditIsRejected()
    {
        var existing = new Booking(1, 3, 1, Sunday(), "UTC", [7]);

        var act = () => new BookingDomainService().EditBooking(Context([3]), existing, Sunday(), "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.PlayModeNotAllowedOnCourt));
    }

    [Fact]
    public void RestrictToCourts_RemovesDuplicatesAndEmptyMeansEveryCourt()
    {
        var playMode = NewPlayMode();

        playMode.RestrictToCourts([4, 2, 4]);
        playMode.CourtIds.Should().Equal(2, 4);
        playMode.IsAllowedOnCourt(4).Should().BeTrue();
        playMode.IsAllowedOnCourt(3).Should().BeFalse();

        playMode.RestrictToCourts([]);
        playMode.IsAllowedOnCourt(3).Should().BeTrue();
    }

    private static DateTimeOffsetInterval Sunday()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != DayOfWeek.Sunday)
            date = date.AddDays(1);

        var local = date.ToDateTime(new TimeOnly(10, 0));
        var from = new DateTimeOffset(local, ClubTimeZone.Instance.GetUtcOffset(local));
        return new DateTimeOffsetInterval(from, from.AddHours(1));
    }

    private static PlayMode NewPlayMode()
    {
        var club = new Club(
            "Club",
            new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            []);
        return club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], Color.Gray, null, false, "Doubles", null, false, false));
    }

    private static BookingDomainService.Context Context(int[] playModeCourtIds)
    {
        var playMode = NewPlayMode();
        playMode.RestrictToCourts(playModeCourtIds);

        var court = new Court(1, "Court 5", "C5");
        typeof(Base.DomainEntity).GetProperty(nameof(court.Id))!.SetValue(court, CourtId);

        var player = new ClubMember(70, 1, [MemberRole.User]);
        typeof(Base.DomainEntity).GetProperty(nameof(player.Id))!.SetValue(player, 7);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            Court = court,
            OpeningHours = new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            PrimeTimeSettings = new PrimeTimeSettings(
                false,
                new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
                [],
                restrictChildren: false,
                restrictGuests: false,
                childAgeThreshold: 18),
            BookingMemberRole = [MemberRole.User],
            Participants =
            [
                new BookingDomainService.Context.PlayerContext
                {
                    Member = player,
                    FullName = "Player",
                    IsChild = false,
                    UpcomingBookingsCount = 0,
                    ActiveBookingsAtTimeOfReservation = 0,
                    BookingsOnThatDay = 0,
                    IsAllowedInCurrentSeason = true,
                    IsAtpParticipant = false,
                    PlayModeBookingsInSeasonCount = 0,
                },
            ],
            IntersectingBookings = [],
            PlayMode = playMode,
            BookingGracePeriodInMinutes = null,
            ActiveSeasonId = 1,
        };
    }
}
