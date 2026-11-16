using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Clubs;
using Bookennis.Api.Business.CourtBlockings;
using Bookennis.Api.Business.CourtSales;
using Bookennis.Api.Business.Memberships;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure;
using Bookennis.Api.Tests.Business.CourtBlockings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using Bookennis.Shared.Controller.CourtSales;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Clubs;

/// <summary>
/// A club in Ashburn, Virginia (America/New_York, United States, US dollars) next to the Austrian clubs: the server works in the club's time
/// zone, writes dates, times and amounts the club's way and charges the club's currency. Austrian clubs look exactly as before.
/// </summary>
public class ClubRegionTests : TestBase
{
    private const string NewYorkId = "America/New_York";
    private const string UserEmail = "user@bookennis.com";

    // Tuesday, 16 June 2037, 16:00 UTC: 18:00 in Vienna, 12:00 PM in New York
    private static readonly DateTimeOffset Noon = new(2037, 6, 16, 16, 0, 0, TimeSpan.Zero);

    private readonly IBookingDomainService bookingService;

    public ClubRegionTests(TestFixture fixture) : base(fixture)
    {
        bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
    }

    [Fact]
    public void Today_At9PmInNewYork_IsStillTheDayOfNewYork()
    {
        // 21:00 in New York on Monday 16 November 2026 is already Tuesday 02:00 in UTC and 03:00 in Vienna
        var now = new DateTimeOffset(2026, 11, 17, 2, 0, 0, TimeSpan.Zero);

        NewYork().Today(now).Should().Be(new DateOnly(2026, 11, 16));
        ClubRegion.Default().Today(now).Should().Be(new DateOnly(2026, 11, 17));
        NewYork().StartOfDay(new DateOnly(2026, 11, 16)).Should().Be(new DateTimeOffset(2026, 11, 16, 5, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ClubEvent_IsOverAfterItsLastLocalDayOfTheClub()
    {
        var clubEvent = new Domain.ClubEvents.ClubEvent(TestDataSeed.ClubId, null, Api.Tests.Business.ClubEvents.ClubEventSeed.Data(new DateOnly(2026, 11, 16)));
        var now = new DateTimeOffset(2026, 11, 17, 2, 0, 0, TimeSpan.Zero); // 21:00 in New York, 03:00 in Vienna

        clubEvent.IsOver(now, NewYork().TimeZone).Should().BeFalse();
        clubEvent.IsOver(now, ClubTimeZone.Default).Should().BeTrue();
    }

    [Theory]
    [InlineData(Country.UnitedStates, "AT", "US")]
    [InlineData(Country.Austria, "DE", "DE")]
    [InlineData(Country.Austria, "US", "AT")]
    [InlineData(Country.Switzerland, null, "CH")]
    public void StripeAccountCountry_IsUsForAUsClub_AndTheEuropeanCountryOtherwise(Country clubCountry, string? requested, string expected)
        => ConnectPaymentAccountCountry(clubCountry, requested).Should().Be(expected);

    [Fact]
    public void Weeks_StartOnSundayInTheUnitedStates_AndOnMondayInAustria()
    {
        var wednesday = new DateOnly(2026, 11, 18);

        NewYork().StartOfWeek(wednesday).Should().Be(new DateOnly(2026, 11, 15));
        NewYork().WeekDays[0].Should().Be(DayOfWeek.Sunday);
        ClubRegion.Default().StartOfWeek(wednesday).Should().Be(new DateOnly(2026, 11, 16));
        ClubRegion.Default().WeekDays[0].Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Texts_OfAUsClub_HaveThe12HourClockMonthFirstAndDollars()
    {
        var english = NewYork().Culture(Language.English);

        NotificationTexts.Time(english, new TimeOnly(18, 0)).Should().Be("6:00 PM");
        NotificationTexts.Date(english, new DateOnly(2037, 6, 16)).Should().Be("Tuesday, June 16, 2037");
        english.Date(new DateOnly(2037, 6, 16)).Should().Be("6/16/2037");
        MembershipTexts.Amount(21550, english).Should().Be("$215.50");
        BadgeSeasonLabelOf(english).Should().Be("4/1/2026 - 10/31/2026");
        ClubEmailCatalog.FormatSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 10, 31), english).Period.Should().Contain("4/1/2026");
    }

    [Fact]
    public void Texts_OfAnAustrianClub_LookAsBefore()
    {
        var german = ClubRegion.Default().Culture(Language.German);
        var english = ClubRegion.Default().Culture(Language.English);

        NotificationTexts.Time(german, new TimeOnly(18, 0)).Should().Be("18:00");
        NotificationTexts.Time(english, new TimeOnly(18, 0)).Should().Be("18:00");
        NotificationTexts.Date(german, new DateOnly(2037, 6, 16)).Should().Be("Dienstag, 16. Juni 2037");
        MembershipTexts.Amount(21550, german).Should().Be("€ 215,50");
        MembershipTexts.Amount(21550, english).Should().Be("€215.50");
        BadgeSeasonLabelOf(german).Should().Be("01.04.2026 - 31.10.2026");

        // A plain culture (no club) is an Austrian club
        MembershipTexts.Amount(21550, Language.German.ToCultureInfo()).Should().Be("€ 215,50");
        MembershipTexts.Amount(21550, Language.English.ToCultureInfo()).Should().Be("€215.50");
    }

    [Fact]
    public async Task GetClubRegion_ReadsTheClubOnce_AndToday()
    {
        await SetNewYork();

        await ScopedAsync(async () =>
        {
            var context = GetInstance<AppDbContext>();
            var region = await context.GetClubRegion(TestDataSeed.ClubId, CancellationToken.None);

            region.TimeZoneId.Should().Be(NewYorkId);
            region.Country.Should().Be(Country.UnitedStates);
            region.Currency.Should().Be("usd");
            (await context.GetClubToday(TestDataSeed.ClubId, CancellationToken.None)).Should().Be(ClubTimeZone.Today(ClubTimeZone.Get(NewYorkId)));
            (await context.GetClubRegion(987654, CancellationToken.None)).Should().Be(ClubRegion.Default(987654));
        });
    }

    [Fact]
    public async Task BookCourt_UsClub_ChecksInNewYorkTime_AndIgnoresTheTimeZoneOfTheBrowser()
    {
        await SetNewYork();
        var (playModeId, userId) = await QueryAsync(ctx => Task.FromResult((ctx.TestData().Club.PlayModes[0].Id, ctx.TestData().Member1.UserId)));

        BookingDomainService.Context? bookingContext = null;
        string? timeZoneId = null;
        var interval = new DateTimeOffsetInterval(Noon, Noon.AddHours(1));
        bookingService.BookCourt(Arg.Do<BookingDomainService.Context>(c => bookingContext = c), Arg.Any<DateTimeOffsetInterval>(), Arg.Do<string>(id => timeZoneId = id), Arg.Any<string?>())
            .Returns(_ => new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, interval, NewYorkId));

        // A wrong id of the browser is ignored instead of failing
        await SendAsync(new BookCourt(TestDataSeed.Court1Id, interval, "Not/AZone", playModeId, [TestDataSeed.Member1Id], null, userId));

        bookingContext!.TimeZone.Id.Should().Be(NewYorkId);
        bookingContext.FormatTime!(new TimeOnly(19, 30)).Should().Be("7:30 PM");
        timeZoneId.Should().Be(NewYorkId);
    }

    [Fact]
    public async Task BookCourt_AustrianClub_UsesVienna()
    {
        var (playModeId, userId) = await QueryAsync(ctx => Task.FromResult((ctx.TestData().Club.PlayModes[0].Id, ctx.TestData().Member1.UserId)));

        BookingDomainService.Context? bookingContext = null;
        string? timeZoneId = null;
        var interval = new DateTimeOffsetInterval(Noon, Noon.AddHours(1));
        bookingService.BookCourt(Arg.Do<BookingDomainService.Context>(c => bookingContext = c), Arg.Any<DateTimeOffsetInterval>(), Arg.Do<string>(id => timeZoneId = id), Arg.Any<string?>())
            .Returns(_ => new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, interval, ClubTimeZone.DefaultId));

        await SendAsync(new BookCourt(TestDataSeed.Court1Id, interval, "America/Los_Angeles", playModeId, [TestDataSeed.Member1Id], null, userId));

        bookingContext!.TimeZone.Id.Should().Be(ClubTimeZone.DefaultId);
        bookingContext.FormatTime!(new TimeOnly(19, 30)).Should().Be("19:30");
        timeZoneId.Should().Be(ClubTimeZone.DefaultId);
    }

    [Fact]
    public async Task CreateCourtBlocking_UsClub_TimesAreNewYorkTimes_WhateverTheBrowserSent()
    {
        await SetNewYork();
        var day = CourtBlockingSeed.Day;
        var data = CourtBlockingSeed.Data(day) with { TimeZoneInfoId = "Europe/Vienna" };

        var id = await SendAsync(new CreateCourtBlocking(TestDataSeed.ClubId, data, DeleteConflictingBookings: false));

        var blocking = await QueryAsync(ctx => ctx.CourtBlockings.Include(b => b.Occurrences).SingleAsync(b => b.Id == id));
        blocking.TimeZoneInfoId.Should().Be(NewYorkId);
        blocking.Occurrences.Should().ContainSingle().Which.Interval.From.Should().Be(ClubTimeZone.At(day, new TimeOnly(10, 0), ClubTimeZone.Get(NewYorkId)));

        // An unknown id of the browser does not matter either
        await SendAsync(new GetCourtBlockingConflicts(TestDataSeed.ClubId, data with { TimeZoneInfoId = "garbage" }));
    }

    [Fact]
    public async Task CourtSales_UsClub_PricesInDollarsAndNewYorkTime()
    {
        await SetNewYork();

        var settings = await SendAsync(new UpdateCourtSaleSettings(TestDataSeed.ClubId, new UpdateCourtSaleSettingsModel
        {
            IsEnabled = false,
            TimeZoneId = "Europe/Vienna",
            MaxBookingMinutes = 60,
            MaxDaysInAdvance = 7,
            MaxUpcomingBookings = 1,
        }));
        settings.TimeZoneId.Should().Be(NewYorkId);

        var rule = await SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, new SaveCourtPriceRuleModel
        {
            Weekdays = [DayOfWeek.Saturday],
            From = TimeSpan.FromHours(8),
            To = TimeSpan.FromHours(12),
            PricePerHourCents = 2000,
            Name = "Morning",
        }));
        rule.Currency.Should().Be("usd");

        var overlapping = () => SendAsync(new CreateCourtPriceRule(TestDataSeed.ClubId, new SaveCourtPriceRuleModel
        {
            Weekdays = [DayOfWeek.Saturday],
            From = TimeSpan.FromHours(11),
            To = TimeSpan.FromHours(13),
            PricePerHourCents = 2000,
        }));
        (await overlapping.Should().ThrowAsync<PreconditionException>()).Which.ErrorDetails.Should().Equal("Morning", "8:00 AM", "12:00 PM");
    }

    [Fact]
    public async Task BookingEmail_UsClub_IsInNewYorkTimeWithThe12HourClock()
    {
        await SetNewYork();
        await QueryAsync(async ctx =>
        {
            var user = await ctx.Users.SingleAsync(u => u.Id == TestDataSeed.UserId);
            user.Update(user.FirstName, user.LastName, user.Birthday, user.Gender, Language.English, user.Street, user.City, user.ZipCode, user.Country);
            await ctx.SaveChangesAsync();
            return 0;
        });
        var bookingId = await QueryAsync(ctx => MemberSeed.AddBooking(ctx, Noon, TestDataSeed.Member1Id, TestDataSeed.Member2Id));

        var email = (await RunBookingEmails(notifier => notifier.BookingAdded(bookingId, CancellationToken.None))).Should().ContainSingle().Subject;

        email.Recipient.Should().Be(UserEmail);
        var variables = email.Variables.Should().BeOfType<BookingAddedEmailVariables>().Subject;
        variables.Booking.Date.Should().Be("Tuesday, June 16, 2037");
        variables.Booking.Time.Should().Be("12:00 PM – 1:00 PM");
    }

    [Fact]
    public async Task BookingEmail_AustrianClub_IsInViennaTimeAsBefore()
    {
        var bookingId = await QueryAsync(ctx => MemberSeed.AddBooking(ctx, Noon, TestDataSeed.Member1Id, TestDataSeed.Member2Id));

        var email = (await RunBookingEmails(notifier => notifier.BookingAdded(bookingId, CancellationToken.None))).Should().ContainSingle().Subject;

        var variables = email.Variables.Should().BeOfType<BookingAddedEmailVariables>().Subject;
        variables.Booking.Date.Should().Be("Dienstag, 16. Juni 2037");
        variables.Booking.Time.Should().Be("18:00 – 19:00");
    }

    private static ClubRegion NewYork() => new(TestDataSeed.ClubId, NewYorkId, Country.UnitedStates, "usd");

    private static string ConnectPaymentAccountCountry(Country clubCountry, string? requested)
        => Api.Business.Payments.ConnectPaymentAccount.AccountCountry(clubCountry, requested);

    private static string BadgeSeasonLabelOf(System.Globalization.CultureInfo culture)
        => Api.Business.Badges.BadgeSeasonLabel.Of(new DateOnly(2026, 4, 1), new DateOnly(2026, 10, 31), culture);

    private Task SetNewYork()
        => QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.SingleAsync(c => c.Id == TestDataSeed.ClubId);
            club.SetRegion(Country.UnitedStates, NewYorkId, "usd");
            await ctx.SaveChangesAsync();
            return 0;
        });

    /// <summary>Runs the booking email notifier as the admin (Member2) and returns the club emails it sent.</summary>
    private async Task<List<SendClubEmail>> RunBookingEmails(Func<BookingEmailNotifier, Task> act)
    {
        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(() => act(new BookingEmailNotifier(GetInstance<AppDbContext>(), mediator, IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId), new AppSettings())));
        return mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEmail>().ToList();
    }
}
