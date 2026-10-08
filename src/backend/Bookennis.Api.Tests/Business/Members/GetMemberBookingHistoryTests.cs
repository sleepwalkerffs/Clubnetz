using Bookennis.Api.Business.Members;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Members;

public class GetMemberBookingHistoryTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task GetMemberBookingHistory_ReturnsBookingsForMemberInSeason()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2025, 4, 1), new DateOnly(2025, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var bookingDate = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(bookingDate, bookingDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        result.Bookings.Should().HaveCount(1);
        result.Bookings[0].Players.Should().Contain(p => p.MemberId == TestDataSeed.Member1Id);
    }

    [Fact]
    public async Task GetMemberBookingHistory_DoesNotReturnBookingsOutsideSeason()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2026, 4, 1), new DateOnly(2026, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var outsideDate = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(outsideDate, outsideDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        result.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberBookingHistory_DoesNotReturnBookingsOfOtherMembers()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2027, 4, 1), new DateOnly(2027, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            var bookingDate = new DateTimeOffset(2027, 6, 15, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(bookingDate, bookingDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member2.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        result.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberBookingHistory_ReturnsBookingsOrderedByDateDescending()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2028, 4, 1), new DateOnly(2028, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            var earlyDate = new DateTimeOffset(2028, 5, 1, 10, 0, 0, TimeSpan.Zero);
            var lateDate = new DateTimeOffset(2028, 9, 1, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(earlyDate, earlyDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(lateDate, lateDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        result.Bookings.Should().HaveCount(2);
        result.Bookings[0].Interval.From.Should().BeAfter(result.Bookings[1].Interval.From);
    }

    [Fact]
    public async Task GetMemberBookingHistory_ReturnsBookingsWhereUserWasAddedAsParticipant()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2029, 4, 1), new DateOnly(2029, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            // Member2 books a court and adds Member1 as a participant
            var bookingDate = new DateTimeOffset(2029, 6, 15, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(bookingDate, bookingDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member2.Id, member1.Id]));
            await ctx.SaveChangesAsync();
        });

        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        result.Bookings.Should().HaveCount(1);
        result.Bookings[0].Players.Should().Contain(p => p.MemberId == TestDataSeed.Member1Id);
    }

    [Fact]
    public async Task GetMemberBookingHistory_IncludesBookingsMadeUnderGuestMembership()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2030, 4, 1), new DateOnly(2030, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        int guestMemberId = 0;
        await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var playMode = club.PlayModes[0];

            // Create a GuestMember for the same user as Member1
            var guestMember = new GuestMember(member1.UserId, club.Id);
            ctx.Add(guestMember);
            await ctx.SaveChangesAsync();
            guestMemberId = guestMember.Id;

            // Booking made under the guest membership
            var bookingDate = new DateTimeOffset(2030, 6, 15, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(bookingDate, bookingDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [guestMemberId]));
            await ctx.SaveChangesAsync();
        });

        // Query using the ClubMember ID — should still find the booking made under the GuestMember
        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        result.Bookings.Should().HaveCount(1);
        result.Bookings[0].Players.Should().Contain(p => p.MemberId == guestMemberId);
    }

    [Fact]
    public async Task GetMemberBookingHistory_ReturnsProfilePictureUrlOfPlayers()
    {
        var seasonId = await QueryAsync(async ctx =>
        {
            var club = await ctx.Clubs.Include(c => c.Seasons).SingleAsync();
            var season = club.AddSeason(new DateOnlyInterval(new DateOnly(2031, 4, 1), new DateOnly(2031, 11, 30)));
            await ctx.SaveChangesAsync();
            return season.Id;
        });

        var member2UserId = await QueryAsync(async ctx =>
        {
            var club = ctx.TestData().Club;
            var court = ctx.TestData().Court1;
            var member1 = ctx.TestData().Member1;
            var member2 = ctx.TestData().Member2;
            var playMode = club.PlayModes[0];

            ctx.Add(new UserProfilePicture(member2.UserId, [1, 2, 3], "image/jpeg"));

            var bookingDate = new DateTimeOffset(2031, 6, 15, 10, 0, 0, TimeSpan.Zero);
            ctx.Add(new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(bookingDate, bookingDate.AddMinutes(90)), TimeZoneInfo.Local.Id, [member1.Id, member2.Id]));
            await ctx.SaveChangesAsync();
            return member2.UserId;
        });

        var result = await SendAsync(new GetMemberBookingHistory(TestDataSeed.Member1Id, seasonId));

        var players = result.Bookings.Should().ContainSingle().Subject.Players;
        players.Single(p => p.MemberId == TestDataSeed.Member1Id).ProfilePictureUrl.Should().BeNull();
        players.Single(p => p.MemberId != TestDataSeed.Member1Id).ProfilePictureUrl.Should().Be($"/api/Profile/picture/{member2UserId}");
    }
}
