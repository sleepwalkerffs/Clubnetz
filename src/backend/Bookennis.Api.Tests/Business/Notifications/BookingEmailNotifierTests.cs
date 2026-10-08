using Bookennis.Api.Business.ClubEmails;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.Notifications;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.Mediator;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Notifications;

public class BookingEmailNotifierTests(TestFixture fixture) : TestBase(fixture)
{
    private const string UserEmail = "user@bookennis.com";

    [Fact]
    public async Task BookingAdded_SendsEmailToTheOtherPlayersButNotTheBooker()
    {
        var bookingId = await QueryAsync(ctx => MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddDays(2), TestDataSeed.Member1Id, TestDataSeed.Member2Id));

        var emails = await Run(notifier => notifier.BookingAdded(bookingId, CancellationToken.None));

        var email = emails.Should().ContainSingle().Subject;
        email.ClubId.Should().Be(TestDataSeed.ClubId);
        email.Type.Should().Be(ClubEmailType.BookingAdded);
        email.Recipient.Should().Be(UserEmail);

        var variables = email.Variables.Should().BeOfType<BookingAddedEmailVariables>().Subject;
        variables.BookedBy.Should().Be("ad min");
        variables.Booking.Court.Should().Be("Court 1");
        variables.Booking.PlayMode.Should().Be("Single");
        variables.Booking.Players.Should().Contain("ad min").And.Contain(", ");
        variables.Booking.Time.Should().MatchRegex(@"^\d\d:\d\d – \d\d:\d\d$");
        variables.Booking.Url.Should().EndWith($"/clubs/{TestDataSeed.ClubId}/booking/{bookingId}");
    }

    [Fact]
    public async Task BookingAdded_PlayerSwitchedTheEmailOff_SendsNothing()
    {
        var bookingId = await QueryAsync(async ctx =>
        {
            ctx.Add(new NotificationPreference(TestDataSeed.UserId, NotificationType.BookingAdded, push: true, email: false));
            return await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddDays(2), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
        });

        var emails = await Run(notifier => notifier.BookingAdded(bookingId, CancellationToken.None));

        emails.Should().BeEmpty();
    }

    [Fact]
    public async Task BookingAdded_BookingOfASeriesOrInThePast_SendsNothing()
    {
        var (seriesBookingId, pastBookingId) = await QueryAsync(async ctx =>
        {
            var series = await AddSeries(ctx);
            var from = DateTimeOffset.UtcNow.AddDays(2);
            var booking = new Booking(TestDataSeed.ClubId, TestDataSeed.Court1Id, series.PlayModeId, new DateTimeOffsetInterval(from, from.AddHours(1)), TimeZoneInfo.Utc.Id,
                [TestDataSeed.Member1Id, TestDataSeed.Member2Id], recurringBookingSeriesId: series.Id);
            ctx.Add(booking);
            await ctx.SaveChangesAsync();

            var past = await MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddHours(-3), TestDataSeed.Member1Id, TestDataSeed.Member2Id);
            return (booking.Id, past);
        });

        var emails = await Run(async notifier =>
        {
            await notifier.BookingAdded(seriesBookingId, CancellationToken.None);
            await notifier.BookingAdded(pastBookingId, CancellationToken.None);
        });

        emails.Should().BeEmpty();
    }

    [Fact]
    public async Task RecurringBookingAdded_SendsOneEmailDescribingTheSeries()
    {
        var seriesId = await QueryAsync(async ctx => (await AddSeries(ctx)).Id);

        var emails = await Run(notifier => notifier.RecurringBookingAdded(seriesId, CancellationToken.None));

        var email = emails.Should().ContainSingle().Subject;
        email.Type.Should().Be(ClubEmailType.BookingAdded);
        email.Recipient.Should().Be(UserEmail);

        var variables = email.Variables.Should().BeOfType<BookingAddedEmailVariables>().Subject;
        variables.BookedBy.Should().Be("ad min");
        variables.Booking.Date.Should().StartWith("jeden Dienstag ab ");
        variables.Booking.Time.Should().Be("18:00 – 19:00");
        variables.Booking.Url.Should().EndWith($"/clubs/{TestDataSeed.ClubId}/my-club");
    }

    [Fact]
    public async Task BookingDeleted_SendsTheBookingDeletedEmailWithTheCancellationAsReason()
    {
        var bookingId = await QueryAsync(ctx => MemberSeed.AddBooking(ctx, DateTimeOffset.UtcNow.AddDays(2), TestDataSeed.Member1Id, TestDataSeed.Member2Id));

        var single = await Run(notifier => notifier.BookingDeleted(bookingId, CancellationToken.None));
        var series = await Run(notifier => notifier.RecurringBookingDeleted(bookingId, CancellationToken.None));

        var email = single.Should().ContainSingle().Subject;
        email.Type.Should().Be(ClubEmailType.BookingDeleted);
        email.Recipient.Should().Be(UserEmail);

        var variables = email.Variables.Should().BeOfType<BookingDeletedEmailVariables>().Subject;
        variables.Booking.Court.Should().Be("Court 1");
        variables.Booking.DeletedBy.Should().Be("ad min");
        variables.Booking.Reason.Should().Be("storniert");

        series.Should().ContainSingle().Which.Variables.Should().BeOfType<BookingDeletedEmailVariables>()
            .Which.Booking.Reason.Should().Be("samt allen folgenden Terminen der Serie storniert");
    }

    private static async Task<RecurringBookingSeries> AddSeries(AppDbContext ctx)
    {
        var playModeId = ctx.PlayModes.First(p => p.ClubId == TestDataSeed.ClubId).Id;
        var series = new RecurringBookingSeries(TestDataSeed.ClubId, TestDataSeed.Court1Id, playModeId, DayOfWeek.Tuesday, new TimeOnly(18, 0), new TimeOnly(19, 0),
            TimeZoneInfo.Utc.Id, 1, new DateOnly(2037, 10, 6), null, null, [TestDataSeed.Member1Id, TestDataSeed.Member2Id]);
        ctx.Add(series);
        await ctx.SaveChangesAsync();
        return series;
    }

    /// <summary>Runs the notifier as the admin (Member2) and returns the club emails it sent.</summary>
    private async Task<List<SendClubEmail>> Run(Func<BookingEmailNotifier, Task> act)
    {
        var mediator = Substitute.For<IMediator>();
        await ScopedAsync(() => act(new BookingEmailNotifier(GetInstance<AppDbContext>(), mediator, IdentityTestHelper.CreateUserAccessor(TestDataSeed.AdminId), new AppSettings())));

        return mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<SendClubEmail>().ToList();
    }
}
