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

/// <summary>Enrollments per sport (<see cref="MemberSeason.Sports"/>, <see cref="Court.Sport"/>).</summary>
public class BookingDomainServiceSportTests
{
    private const int CourtId = 5;

    [Theory]
    [InlineData(Sport.Padel, new Sport[0])]
    [InlineData(Sport.Padel, new[] { Sport.Padel })]
    [InlineData(Sport.Tennis, new[] { Sport.Tennis, Sport.Padel })]
    public void EnrolledForTheSportOfTheCourt_CreatesBooking(Sport courtSport, Sport[] enrolledSports)
    {
        var booking = new BookingDomainService().BookCourt(Context(courtSport, enrolledSports), Sunday(), "UTC", null);

        booking.CourtId.Should().Be(CourtId);
    }

    [Fact]
    public void EnrolledForAnotherSport_IsRejected()
    {
        var act = () => new BookingDomainService().BookCourt(Context(Sport.Padel, [Sport.Tennis]), Sunday(), "UTC", null);

        var error = act.Should().Throw<PreconditionException>().Which;
        error.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.PlayerNotEnrolledForSport));
        error.ErrorDetails.Should().Equal("Player", "Padel");
    }

    [Fact]
    public void EnrolledForAnotherSport_EditIsRejected()
    {
        var existing = new Booking(1, CourtId, 1, Sunday(), "UTC", [7]);

        var act = () => new BookingDomainService().EditBooking(Context(Sport.Tennis, [Sport.Padel]), existing, Sunday(), "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.PlayerNotEnrolledForSport));
    }

    [Fact]
    public void BoughtSlot_NeedsNoEnrollmentForTheSport()
    {
        var booking = new BookingDomainService().BookCourt(Context(Sport.Padel, [Sport.Tennis], isPurchase: true), Sunday(), "UTC", null);

        booking.CourtId.Should().Be(CourtId);
    }

    [Fact]
    public void PlayerOfASharingClub_IsNotCheckedForTheSport()
    {
        var booking = new BookingDomainService().BookCourt(Context(Sport.Padel, [Sport.Tennis], isAtp: true), Sunday(), "UTC", null);

        booking.CourtId.Should().Be(CourtId);
    }

    [Fact]
    public void MemberSeason_NormalizesSportsAndEmptyMeansEverySport()
    {
        var enrollment = new MemberSeason(1, 1, sports: [Sport.Padel, Sport.Tennis, Sport.Padel]);
        enrollment.Sports.Should().Equal(Sport.Tennis, Sport.Padel);

        enrollment.SetSports([Sport.Padel]);
        enrollment.Covers(Sport.Tennis).Should().BeFalse();
        enrollment.Covers(Sport.Padel).Should().BeTrue();

        enrollment.SetSports([]);
        enrollment.Covers(Sport.Tennis).Should().BeTrue();
    }

    [Fact]
    public void Sports_OverlapAndUnion()
    {
        Sports.Overlap([Sport.Tennis], [Sport.Padel]).Should().BeFalse();
        Sports.Overlap([Sport.Tennis], []).Should().BeTrue();
        Sports.Overlap([Sport.Tennis, Sport.Padel], [Sport.Padel]).Should().BeTrue();

        Sports.Union([Sport.Padel], [Sport.Tennis]).Should().Equal(Sport.Tennis, Sport.Padel);
        Sports.Union([Sport.Padel], []).Should().BeEmpty();
    }

    private static DateTimeOffsetInterval Sunday()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != DayOfWeek.Sunday)
            date = date.AddDays(1);

        var local = date.ToDateTime(new TimeOnly(10, 0));
        var from = new DateTimeOffset(local, ClubTimeZone.Default.GetUtcOffset(local));
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

    private static BookingDomainService.Context Context(Sport courtSport, Sport[] enrolledSports, bool isPurchase = false, bool isAtp = false)
    {
        var playMode = NewPlayMode();

        var court = new Court(1, "Court 5", "C5");
        court.SetSport(courtSport);
        typeof(Base.DomainEntity).GetProperty(nameof(court.Id))!.SetValue(court, CourtId);

        var player = new ClubMember(70, 1, [MemberRole.User]);
        typeof(Base.DomainEntity).GetProperty(nameof(player.Id))!.SetValue(player, 7);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            TimeZone = Bookennis.Domain.Clubs.ClubTimeZone.Default,
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
                    EnrolledSports = enrolledSports,
                    IsAtpParticipant = isAtp,
                    PlayModeBookingsInSeasonCount = 0,
                },
            ],
            IntersectingBookings = [],
            PlayMode = playMode,
            BookingGracePeriodInMinutes = null,
            ActiveSeasonId = 1,
            IsPurchase = isPurchase,
        };
    }
}
