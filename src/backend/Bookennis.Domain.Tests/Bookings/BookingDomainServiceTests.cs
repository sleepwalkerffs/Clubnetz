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

public class BookingDomainServiceTests
{
    private static readonly DateTimeOffsetInterval Tomorrow = new(
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(10), TimeSpan.Zero),
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(11), TimeSpan.Zero));

    [Fact]
    public void BookCourt_CourtIsFree_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(), Tomorrow, "UTC", null);

        booking.Interval.Should().Be(Tomorrow);
    }

    [Fact]
    public void BookCourt_CourtIsBlocked_ThrowsWithTheReason()
    {
        var act = () => new BookingDomainService().BookCourt(Context(courtBlockingTitles: ["Club championship", "Maintenance"]), Tomorrow, "UTC", null);

        var exception = act.Should().Throw<PreconditionException>().Which;
        exception.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtBlocked));
        exception.ErrorDetails.Should().Equal("Club championship, Maintenance");
    }

    [Fact]
    public void BookCourt_CourtIsBlocked_AlsoBlocksOverbookingPlayModes()
    {
        var act = () => new BookingDomainService().BookCourt(Context(canOverbook: true, courtBlockingTitles: ["Maintenance"]), Tomorrow, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtBlocked));
    }

    private static BookingDomainService.Context Context(bool canOverbook = false, IReadOnlyList<string>? courtBlockingTitles = null)
    {
        var club = new Club(
            "Club",
            new TimeOnlyInterval(new TimeOnly(0, 0), new TimeOnly(23, 59)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.Trainer], Color.Brown, null, false, "Training", null, canOverbook, false)]);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            Court = new Court(1, "Court 1", "C1"),
            OpeningHours = club.OpeningHours,
            PrimeTimeSettings = club.PrimeTimeSettings,
            BookingMemberRole = [MemberRole.Trainer],
            Participants = [],
            IntersectingBookings = [],
            PlayMode = club.PlayModes[0],
            ClientTimeZoneOffset = null,
            BookingGracePeriodInMinutes = null,
            ActiveSeasonId = 1,
            CourtBlockingTitles = courtBlockingTitles ?? [],
        };
    }
}
