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

/// <summary>Opening hours are wall-clock times of the club's own time zone (<see cref="BookingDomainService.Context.TimeZone"/>).</summary>
public class BookingDomainServiceTimeZoneTests
{
    private static readonly TimeZoneInfo NewYork = ClubTimeZone.Get("America/New_York");

    [Fact]
    public void NewYorkClub_EveningInNewYork_IsInsideTheOpeningHours()
    {
        // 20:00-21:00 in New York is 01:00-02:00 UTC (and 02:00-03:00 in Vienna) on the next day
        var interval = At(NextSunday(), new TimeOnly(20, 0), NewYork);

        var booking = new BookingDomainService().BookCourt(Context(NewYork), interval, NewYork.Id, null);

        booking.TimeZoneInfoId.Should().Be(NewYork.Id);
    }

    [Fact]
    public void ViennaClub_SameMoment_IsOutsideTheOpeningHours()
    {
        var interval = At(NextSunday(), new TimeOnly(20, 0), NewYork);

        var act = () => new BookingDomainService().BookCourt(Context(ClubTimeZone.Default), interval, ClubTimeZone.DefaultId, null);

        act.Should().Throw<PreconditionException>().Which.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.OutsideClubOpeningHours));
    }

    [Fact]
    public void NewYorkClub_MorningInVienna_IsOutsideTheOpeningHoursOfNewYork()
    {
        // 10:00 in Vienna is 04:00 in New York, before the club opens at 07:00
        var interval = At(NextSunday(), new TimeOnly(10, 0), ClubTimeZone.Default);

        var act = () => new BookingDomainService().BookCourt(Context(NewYork) with { FormatTime = time => time.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) }, interval, NewYork.Id, null);

        var error = act.Should().Throw<PreconditionException>().Which;
        error.ErrorCode.Should().Be(nameof(BookingDomainService.ErrorCode.OutsideClubOpeningHours));
        error.ErrorDetails.Should().Equal("7:00 AM", "10:00 PM");
    }

    private static DateOnly NextSunday()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        while (date.DayOfWeek != DayOfWeek.Sunday)
            date = date.AddDays(1);
        return date;
    }

    private static DateTimeOffsetInterval At(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var from = ClubTimeZone.At(date, time, timeZone);
        return new DateTimeOffsetInterval(from, from.AddHours(1));
    }

    private static BookingDomainService.Context Context(TimeZoneInfo timeZone)
    {
        var club = new Club(
            "Club",
            new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            []);
        var playMode = club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], Color.Gray, null, false, "Singles", null, false, false));

        var court = new Court(1, "Court 1", "C1");
        typeof(Base.DomainEntity).GetProperty(nameof(court.Id))!.SetValue(court, 5);

        var player = new ClubMember(70, 1, [MemberRole.User]);
        typeof(Base.DomainEntity).GetProperty(nameof(player.Id))!.SetValue(player, 7);

        return new BookingDomainService.Context
        {
            ClubId = 1,
            Court = court,
            OpeningHours = new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            TimeZone = timeZone,
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
