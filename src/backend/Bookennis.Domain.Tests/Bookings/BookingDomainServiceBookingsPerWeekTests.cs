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

public class BookingDomainServiceBookingsPerWeekTests
{
    // Far enough ahead not to be a spontaneous booking (those don't count against the limit)
    private static readonly DateTimeOffsetInterval InThreeDays = new(
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(10), TimeSpan.Zero),
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(11), TimeSpan.Zero));

    [Theory]
    [InlineData(null, 3, 3)]
    [InlineData(2, 3, 2)]
    [InlineData(5, 3, 3)]
    [InlineData(4, null, 4)]
    [InlineData(null, null, null)]
    public void BookingsPerWeekLimit_MemberValueWithinTheClubValue(int? memberValue, int? clubValue, int? expected)
    {
        var member = Member(memberValue);

        BookingDomainService.BookingsPerWeekLimit(member, clubValue).Should().Be(expected);
    }

    [Fact]
    public void BookCourt_MemberWithoutOwnValueReachedTheClubValue_Throws()
    {
        var act = () => new BookingDomainService().BookCourt(Context(Player(null, upcomingBookings: 3), clubBookingsPerWeek: 3), InThreeDays, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.MaximumReservationsReached));
    }

    [Fact]
    public void BookCourt_MemberWithoutOwnValueBelowTheClubValue_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(Player(null, upcomingBookings: 2), clubBookingsPerWeek: 3), InThreeDays, "UTC", null);

        booking.Interval.Should().Be(InThreeDays);
    }

    [Fact]
    public void BookCourt_MemberValueAboveTheClubValue_TheClubValueApplies()
    {
        var act = () => new BookingDomainService().BookCourt(Context(Player(5, upcomingBookings: 3), clubBookingsPerWeek: 3), InThreeDays, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.MaximumReservationsReached));
    }

    [Fact]
    public void BookCourt_LowerMemberValue_TheMemberValueApplies()
    {
        var act = () => new BookingDomainService().BookCourt(Context(Player(1, upcomingBookings: 1), clubBookingsPerWeek: 3), InThreeDays, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.MaximumReservationsReached));
    }

    private static GuestMember Member(int? bookingsPerWeek)
    {
        var member = new GuestMember(1, 1);
        member.AllowBooking();
        if (bookingsPerWeek is { } value)
            member.Update(value);
        return member;
    }

    private static BookingDomainService.Context.PlayerContext Player(int? bookingsPerWeek, int upcomingBookings) => new()
    {
        Member = Member(bookingsPerWeek),
        FullName = "Player",
        IsChild = false,
        UpcomingBookingsCount = upcomingBookings,
        ActiveBookingsAtTimeOfReservation = 0,
        BookingsOnThatDay = 0,
        IsAllowedInCurrentSeason = true,
        IsAtpParticipant = false,
        PlayModeBookingsInSeasonCount = 0,
    };

    private static BookingDomainService.Context Context(BookingDomainService.Context.PlayerContext player, int clubBookingsPerWeek)
    {
        var club = new Club(
            "Club",
            new TimeOnlyInterval(new TimeOnly(0, 0), new TimeOnly(23, 59)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.Trainer], Color.Brown, null, true, "Single", null, false, false)]);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            TimeZone = ClubTimeZone.Default,
            Court = new Court(1, "Court 1", "C1"),
            OpeningHours = club.OpeningHours,
            PrimeTimeSettings = club.PrimeTimeSettings,
            BookingMemberRole = [MemberRole.Trainer],
            Participants = [player],
            IntersectingBookings = [],
            PlayMode = club.PlayModes[0],
            BookingGracePeriodInMinutes = null,
            ActiveSeasonId = 1,
            ClubBookingsPerWeek = clubBookingsPerWeek,
        };
    }
}
