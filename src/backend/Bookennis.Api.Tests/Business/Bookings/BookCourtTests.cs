using Bookennis.Api.Business.Bookings;
using Bookennis.Api.Tests.Business.CourtBlockings;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Xunit;

namespace Bookennis.Api.Tests.Business.Bookings;

public class BookCourtTests : TestBase
{
    private readonly IBookingDomainService bookingService;

    public BookCourtTests(TestFixture fixture) : base(fixture)
    {
        bookingService = GetInstance<IBookingDomainService>();
        bookingService.ClearSubstitute();
    }

    [Fact]
    public async Task CreateBooking_UseCorrectBookingContext()
    {
        // Arrange

        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var to = from.AddMinutes(90);
        var bookingInterval = new DateTimeOffsetInterval(from, to);

        var (club, court, playMode, member1, user, intersectingBooking) = await QueryAsync(async ctx =>
        {
            var member1 = ctx.TestData().Member1;
            var user1 = ctx.TestData().User;
            var member2 = ctx.TestData().Member2;
            var club = ctx.TestData().Club;
            var playMode = club.PlayModes[0];
            var court = ctx.TestData().Court1;

            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from.AddMinutes(-90), from), TimeZoneInfo.Local.Id, [member1.Id, member2.Id]));
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(to, from.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id, member2.Id]));
            var intersecting = ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(from, from.AddMinutes(1)), TimeZoneInfo.Local.Id, [member1.Id, member2.Id])).Entity;
            await ctx.SaveChangesAsync();

            return (club, court, playMode, member1, user1, intersecting);
        });

        SetAndValidateBookingService(court.Id, playMode.Id, bookingInterval, ValidateContext, ValidateInterval, ValidateTimeZoneId, ValidateComment);

        // Act
        var result = await SendAsync(new BookCourt(court.Id,
                                                   bookingInterval,
                                                   TimeZoneInfo.Local.Id,
                                                   playMode.Id,
                                                   [member1.Id],
                                                   Comment: "newBooking",
                                                   ClientTimeZoneOffset: null,
                                                   member1.UserId));

        // Assert
        bookingService.Received(1).BookCourt(Arg.Any<BookingDomainService.Context>(), Arg.Any<DateTimeOffsetInterval>(), Arg.Any<string>(), Arg.Any<string?>());
        var booking = await QueryAsync(ctx => ctx.Bookings.OrderByDescending(b => b.Id).FirstAsync());
        booking.Comment.Should().Be("newBooking");

        result.Id.Should().Be(booking.Id);

        void ValidateContext(BookingDomainService.Context context)
        {
            context.ClubId.Should().Be(club.Id);
            context.PrimeTimeSettings.PrimeTimeHours.Should().Be(club.PrimeTimeSettings.PrimeTimeHours);
            context.PrimeTimeSettings.Should().BeEquivalentTo(club.PrimeTimeSettings);
            context.OpeningHours.Should().Be(club.OpeningHours);

            context.PlayMode.Id.Should().Be(playMode.Id);
            context.BookingMemberRole.Should().Contain(member1.UserRoles);

            context.ClientTimeZoneOffset.Should().BeNull();
            context.Court.Id.Should().Be(court.Id);
            context.IntersectingBookings.Select(i => i.Id).Should().BeEquivalentTo(new List<int> { intersectingBooking.Id });

            var participant = new BookingDomainService.Context.PlayerContext
            {
                Member = member1,
                FullName = user.FullName,
                IsChild = user.Birthday.AddYears(15) > DateOnly.FromDateTime(DateTime.UtcNow),
                UpcomingBookingsCount = 1,
                ActiveBookingsAtTimeOfReservation = 1,
                BookingsOnThatDay = 3,
                IsAllowedInCurrentSeason = false,
                IsAtpParticipant = false,
                PlayModeBookingsInSeasonCount = 0,
            };

            context.Participants.Should().BeEquivalentTo(new List<BookingDomainService.Context.PlayerContext> { participant });
        }

        void ValidateInterval(DateTimeOffsetInterval interval) => interval.Should().Be(bookingInterval);

        void ValidateTimeZoneId(string timeZoneId) => timeZoneId.Should().Be(TimeZoneInfo.Local.Id);

        void ValidateComment(string? comment) => comment.Should().Be("newBooking");
    }

    [Fact]
    public async Task CreateBooking_CourtIsBlocked_PassesTheBlockingsOfTheCourtAndTimeToTheBookingService()
    {
        var day = CourtBlockingSeed.Day;
        var bookingInterval = CourtBlockingSeed.At(day, 13, 15);

        var (courtId, playModeId, memberId, userId) = await QueryAsync(async ctx =>
        {
            await CourtBlockingSeed.Seed(ctx);
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(courtIds: [TestDataSeed.Court2Id]) with { Title = "Other court" });
            await CourtBlockingSeed.Seed(ctx, CourtBlockingSeed.Data(day.AddDays(1)) with { Title = "Other day" });

            var member = ctx.TestData().Member1;
            return (TestDataSeed.Court1Id, ctx.TestData().Club.PlayModes[0].Id, member.Id, member.UserId);
        });

        IReadOnlyList<string>? courtBlockingTitles = null;
        SetAndValidateBookingService(courtId, playModeId, bookingInterval, context => courtBlockingTitles = context.CourtBlockingTitles);

        await SendAsync(new BookCourt(courtId, bookingInterval, TimeZoneInfo.Utc.Id, playModeId, [memberId], Comment: null, ClientTimeZoneOffset: null, userId));

        courtBlockingTitles.Should().Equal(CourtBlockingSeed.Title);
    }

    private void SetAndValidateBookingService(
        int courtId,
        int playModeId,
        DateTimeOffsetInterval interval,
        Action<BookingDomainService.Context>? validateContext = null,
        Action<DateTimeOffsetInterval>? validateDateTimeInterval = null,
        Action<string>? validateTimeZoneId = null,
        Action<string?>? validateComment = null
    )
    {
        var clubId = Query(ctx => ctx.TestData().Club.Id);
        bookingService.BookCourt(Arg.Do<BookingDomainService.Context>(args => validateContext?.Invoke(args)),
                                 Arg.Do<DateTimeOffsetInterval>(args => validateDateTimeInterval?.Invoke(args)),
                                 Arg.Do<string>(args => validateTimeZoneId?.Invoke(args)),
                                 Arg.Do<string?>(args => validateComment?.Invoke(args)))
                      .Returns(_ => new Booking(clubId, courtId, playModeId, interval, TimeZoneInfo.Local.Id, comment: "newBooking"));
    }
}