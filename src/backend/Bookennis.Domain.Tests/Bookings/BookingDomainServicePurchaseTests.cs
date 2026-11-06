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

/// <summary>The booking of a bought slot (<see cref="BookingDomainService.Context.IsPurchase"/>).</summary>
public class BookingDomainServicePurchaseTests
{
    /// <summary>A day without prime time (Sunday) and a weekday at 18:30 club time, inside the default prime time 18-20 (Mon-Fri).</summary>
    private static readonly DateTimeOffsetInterval Sunday = Next(DayOfWeek.Sunday, 10);
    private static readonly DateTimeOffsetInterval WeekdayPrimeTime = Next(DayOfWeek.Wednesday, 18);

    [Fact]
    public void Purchase_BuyerNotEnrolledAndNoActiveSeason_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(isPurchase: true, activeSeasonId: null), Sunday, "UTC", null);

        booking.Players.Should().ContainSingle().Which.MemberId.Should().Be(7);
    }

    [Fact]
    public void NormalBooking_PlayerNotEnrolled_IsNotAllowedToPlay()
    {
        var act = () => new BookingDomainService().BookCourt(Context(isPurchase: false), Sunday, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.PlayerIsNotAllwedToPlay));
    }

    [Fact]
    public void NormalBooking_NoActiveSeason_IsRejected()
    {
        var act = () => new BookingDomainService().BookCourt(Context(isPurchase: false, activeSeasonId: null), Sunday, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.NoActiveSeasonForClub));
    }

    [Fact]
    public void Purchase_FixedPlayerCountOfThePlayMode_IsNotEnforced()
    {
        var booking = new BookingDomainService().BookCourt(Context(isPurchase: true, fixedPlayerCount: 2), Sunday, "UTC", null);

        booking.Should().NotBeNull();
    }

    [Fact]
    public void Purchase_InPrimeTimeWhenClosedForBoughtSlots_IsRejected()
    {
        var act = () => new BookingDomainService().BookCourt(Context(isPurchase: true, restrictGuests: true), WeekdayPrimeTime, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CannotBuyWithinPrimeTime));
    }

    [Fact]
    public void Purchase_InPrimeTimeWhenOpenForBoughtSlots_CreatesBooking()
    {
        var booking = new BookingDomainService().BookCourt(Context(isPurchase: true, restrictGuests: false), WeekdayPrimeTime, "UTC", null);

        booking.Should().NotBeNull();
    }

    [Fact]
    public void Purchase_TheRulesOfEveryBookingStillApply()
    {
        var service = new BookingDomainService();
        var court = new Court(1, "Court 1", "C1");
        court.SetInactive(Sunday);

        var occupied = () => service.BookCourt(Context(isPurchase: true, intersecting: [new Booking(1, 1, 1, Sunday, "UTC", [])]), Sunday, "UTC", null);
        var blocked = () => service.BookCourt(Context(isPurchase: true, blockings: ["Tournament"]), Sunday, "UTC", null);
        var inactive = () => service.BookCourt(Context(isPurchase: true, court: court), Sunday, "UTC", null);
        var past = new DateTimeOffsetInterval(DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2));
        var inThePast = () => service.BookCourt(Context(isPurchase: true), past, "UTC", null);

        occupied.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtOccupied));
        blocked.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtBlocked));
        inactive.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CourtInactive));
        inThePast.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.BookingInThePast));
    }

    [Fact]
    public void Purchase_OutsideOpeningHours_IsRejected()
    {
        var early = Next(DayOfWeek.Sunday, 5);

        var act = () => new BookingDomainService().BookCourt(Context(isPurchase: true), early, "UTC", null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.OutsideClubOpeningHours));
    }

    [Theory]
    [InlineData(1)] // winter time, UTC+1
    [InlineData(7)] // summer time, UTC+2
    public void Purchase_OpeningHoursAreWallClockTimesOfTheClub_InSummerAndWinterTime(int month)
    {
        var date = FirstOfNextYear(month, DayOfWeek.Sunday);

        var atOpening = () => new BookingDomainService().BookCourt(Context(isPurchase: true), At(date, 7, 0), "UTC", null);
        var beforeOpening = () => new BookingDomainService().BookCourt(Context(isPurchase: true), At(date, 6, 30), "UTC", null);
        var untilClosing = () => new BookingDomainService().BookCourt(Context(isPurchase: true), At(date, 21, 0), "UTC", null);

        atOpening.Should().NotThrow();
        untilClosing.Should().NotThrow();
        beforeOpening.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.OutsideClubOpeningHours));
    }

    [Theory]
    [InlineData(1)] // winter time, UTC+1
    [InlineData(7)] // summer time, UTC+2
    public void Purchase_PrimeTimeIsWallClockTimeOfTheClub_InSummerAndWinterTime(int month)
    {
        var date = FirstOfNextYear(month, DayOfWeek.Wednesday);

        var inPrimeTime = () => new BookingDomainService().BookCourt(Context(isPurchase: true), At(date, 18, 0), "UTC", null);
        var beforePrimeTime = () => new BookingDomainService().BookCourt(Context(isPurchase: true), At(date, 17, 0), "UTC", null);

        inPrimeTime.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.CannotBuyWithinPrimeTime));
        beforePrimeTime.Should().NotThrow();
    }

    private static DateTimeOffsetInterval Next(DayOfWeek day, int hour)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != day)
            date = date.AddDays(1);

        return At(date, hour, 30);
    }

    private static DateOnly FirstOfNextYear(int month, DayOfWeek day)
    {
        var date = new DateOnly(DateTime.UtcNow.Year + 1, month, 1);
        while (date.DayOfWeek != day)
            date = date.AddDays(1);

        return date;
    }

    /// <summary>One hour from a wall-clock time of the club on <paramref name="date"/>.</summary>
    private static DateTimeOffsetInterval At(DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));
        var from = new DateTimeOffset(local, ClubTimeZone.Instance.GetUtcOffset(local));
        return new DateTimeOffsetInterval(from, from.AddHours(1));
    }

    private static BookingDomainService.Context Context(
        bool isPurchase,
        int? activeSeasonId = 1,
        int? fixedPlayerCount = null,
        bool restrictGuests = true,
        List<Booking>? intersecting = null,
        IReadOnlyList<string>? blockings = null,
        Court? court = null)
    {
        var club = new Club(
            "Club",
            new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.User], Color.Gray, fixedPlayerCount, false, "Bought", null, false, false)],
            primeTimeSettings: new PrimeTimeSettings(
                true,
                new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                restrictChildren: true,
                restrictGuests: restrictGuests,
                childAgeThreshold: 18));

        var buyer = new ClubMember(70, 1, [MemberRole.User]);
        typeof(Base.DomainEntity).GetProperty(nameof(buyer.Id))!.SetValue(buyer, 7);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            Court = court ?? new Court(1, "Court 1", "C1"),
            OpeningHours = club.OpeningHours,
            PrimeTimeSettings = club.PrimeTimeSettings,
            BookingMemberRole = [MemberRole.User],
            Participants =
            [
                new BookingDomainService.Context.PlayerContext
                {
                    Member = buyer,
                    FullName = "Buyer",
                    IsChild = false,
                    UpcomingBookingsCount = 0,
                    ActiveBookingsAtTimeOfReservation = 0,
                    BookingsOnThatDay = 0,
                    IsAllowedInCurrentSeason = false,
                    IsAtpParticipant = false,
                    PlayModeBookingsInSeasonCount = 0,
                },
            ],
            IntersectingBookings = intersecting ?? [],
            PlayMode = club.PlayModes[0],
            BookingGracePeriodInMinutes = null,
            ActiveSeasonId = activeSeasonId,
            CourtBlockingTitles = blockings ?? [],
            IsPurchase = isPurchase,
        };
    }
}
