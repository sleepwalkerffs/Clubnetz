using Bookennis.Api.Data.Migrations;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.EmailTemplates;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.CourtSales;

/// <summary>
/// The data part of the migration <c>AddCourtSales</c> on seeded guest data. The test database is already migrated, so the dropped
/// <c>GuestCards</c> table is created again for the test exactly as it was.
/// </summary>
public class GuestCardMigrationSqlTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task RemainingGuestBookings_BecomeCreditsOnAClubMembership()
    {
        var seeded = await QueryAsync(async ctx =>
        {
            await CreateGuestCardsTable(ctx);
            var otherClubId = await AddClub(ctx);

            // 5 bookings bought, 2 played, no club membership: gets one with 3 credits
            var newGuest = await AddUser(ctx, "new-guest@test.com");
            await Guest(ctx, newGuest, TestDataSeed.ClubId, purchased: [3, 2], played: 2);

            // Also a member of the club: the credits go to the existing membership
            var memberGuest = await AddUser(ctx, "member-guest@test.com");
            var existingMember = await ClubMember(ctx, memberGuest, TestDataSeed.ClubId);
            await Guest(ctx, memberGuest, TestDataSeed.ClubId, purchased: [4], played: 1);

            // Two guest members of the same user in one club: one membership, the remaining bookings of both (2 + 1)
            var twoGuests = await AddUser(ctx, "two-guests@test.com");
            await Guest(ctx, twoGuests, TestDataSeed.ClubId, purchased: [3], played: 1);
            await Guest(ctx, twoGuests, TestDataSeed.ClubId, purchased: [1], played: 0);

            // The same user as guest in two clubs: a membership and credits in each club
            var twoClubs = await AddUser(ctx, "two-clubs@test.com");
            await Guest(ctx, twoClubs, TestDataSeed.ClubId, purchased: [2], played: 0);
            await Guest(ctx, twoClubs, otherClubId, purchased: [5], played: 1);

            // Used up (0) or played more than bought (-1): nothing
            var usedUp = await AddUser(ctx, "used-up@test.com");
            await Guest(ctx, usedUp, TestDataSeed.ClubId, purchased: [2], played: 2);
            var overplayed = await AddUser(ctx, "overplayed@test.com");
            await Guest(ctx, overplayed, TestDataSeed.ClubId, purchased: [1], played: 2);

            ctx.Add(new ClubEmailTemplate(TestDataSeed.ClubId, (ClubEmailType)3, Language.German, "Gast", "Gast"));
            ctx.Add(new ClubEmailTemplate(otherClubId, (ClubEmailType)3, Language.English, "Guest", "Guest"));
            ctx.Add(new ClubEmailTemplate(TestDataSeed.ClubId, ClubEmailType.Welcome, Language.German, "Hallo", "Hallo"));
            await ctx.SaveChangesAsync();
            return new { otherClubId, newGuest, memberGuest, existingMember, twoGuests, twoClubs, usedUp, overplayed };
        });

        await Migrate();
        var membersAfterFirstRun = await QueryAsync(ctx => ctx.ClubMembers.CountAsync());
        var grantsAfterFirstRun = await QueryAsync(ctx => ctx.BookingCreditGrants.CountAsync());
        await Migrate();

        await QueryAsync(async ctx =>
        {
            (await ctx.ClubMembers.CountAsync()).Should().Be(membersAfterFirstRun, "a second run changes nothing");
            (await ctx.BookingCreditGrants.CountAsync()).Should().Be(grantsAfterFirstRun);

            async Task<int?> Credits(int userId, int clubId)
            {
                var members = await ctx.ClubMembers.Where(m => m.UserId == userId && m.ClubId == clubId).ToListAsync();
                if (members.Count == 0)
                    return null;

                members.Should().ContainSingle($"user {userId} has one membership in club {clubId}");
                members[0].UserRoles.Should().Equal(MemberRole.User);
                members[0].IsAllowedToBook.Should().BeFalse();
                var grants = await ctx.BookingCreditGrants.Where(g => g.MemberId == members[0].Id).ToListAsync();
                grants.Should().OnlyContain(g => g.Reason == BookingCreditReason.GuestCardMigration && g.ClubId == clubId && g.GrantedByMemberId == null && g.Note == null);
                return grants.Count == 0 ? 0 : grants.Single().Count;
            }

            (await Credits(seeded.newGuest, TestDataSeed.ClubId)).Should().Be(3);
            (await Credits(seeded.memberGuest, TestDataSeed.ClubId)).Should().Be(3);
            (await ctx.BookingCreditGrants.SingleAsync(g => g.MemberId == seeded.existingMember)).Count.Should().Be(3);
            (await Credits(seeded.twoGuests, TestDataSeed.ClubId)).Should().Be(3);
            (await Credits(seeded.twoClubs, TestDataSeed.ClubId)).Should().Be(2);
            (await Credits(seeded.twoClubs, seeded.otherClubId)).Should().Be(4);
            (await Credits(seeded.usedUp, TestDataSeed.ClubId)).Should().BeNull("nothing left, no membership");
            (await Credits(seeded.overplayed, TestDataSeed.ClubId)).Should().BeNull();

            (await ctx.GuestMembers.AnyAsync(g => g.IsAllowedToBook)).Should().BeFalse();
            (await ctx.GuestMembers.CountAsync()).Should().Be(8, "the guest members stay for their bookings");
            (await ctx.ClubEmailTemplates.Select(t => t.Type).ToListAsync()).Should().Equal(ClubEmailType.Welcome);

            // The new ids don't collide with what EF hands out next
            ctx.Add(new ClubMember(seeded.usedUp, TestDataSeed.ClubId, [MemberRole.User]));
            ctx.Add(new BookingCreditGrant(TestDataSeed.ClubId, seeded.existingMember, 1, BookingCreditReason.GrantedByClub, null, null));
            await ctx.SaveChangesAsync();
            return 0;
        });
    }

    private Task Migrate() => QueryAsync(async ctx =>
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync();
        await ctx.Database.ExecuteSqlRawAsync(GuestCardMigrationSql.Sql);
        await transaction.CommitAsync();
        return 0;
    });

    private static Task CreateGuestCardsTable(Data.AppDbContext ctx) => ctx.Database.ExecuteSqlRawAsync("""
        CREATE TABLE "GuestCards" (
            "Id" integer NOT NULL PRIMARY KEY, "ClubId" integer NOT NULL, "Code" uuid NOT NULL, "GuestMemberId" integer NOT NULL,
            "PurchasedBookings" integer NOT NULL, "Metadata_Created" timestamp with time zone NOT NULL DEFAULT NOW(),
            "Metadata_CreationUserId" integer NULL, "Metadata_Modified" timestamp with time zone NULL, "Metadata_ModificationUserId" integer NULL)
        """);

    private static async Task<int> AddClub(Data.AppDbContext ctx)
    {
        var club = new Club("Other club", new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)), new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.User], System.Drawing.Color.Brown, null, false, "Single", null, false, false)]);
        ctx.Add(club);
        await ctx.SaveChangesAsync();
        ctx.Add(new Domain.Courts.Court(club.Id, "Court A", "A"));
        await ctx.SaveChangesAsync();
        return club.Id;
    }

    private static async Task<int> AddUser(Data.AppDbContext ctx, string email)
    {
        var user = new User(email, email, "Guest", email.Split('@')[0], new DateOnly(1990, 1, 1), Gender.Female);
        ctx.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<int> ClubMember(Data.AppDbContext ctx, int userId, int clubId)
    {
        var member = new ClubMember(userId, clubId, [MemberRole.User]);
        ctx.Add(member);
        await ctx.SaveChangesAsync();
        return member.Id;
    }

    private static async Task Guest(Data.AppDbContext ctx, int userId, int clubId, int[] purchased, int played)
    {
        var guest = new GuestMember(userId, clubId);
        guest.AllowBooking();
        ctx.Add(guest);
        await ctx.SaveChangesAsync();

        var id = guest.Id * 100;
        foreach (var bookings in purchased)
            await ctx.Database.ExecuteSqlAsync($"""INSERT INTO "GuestCards" ("Id", "ClubId", "Code", "GuestMemberId", "PurchasedBookings") VALUES ({id++}, {clubId}, {Guid.NewGuid()}, {guest.Id}, {bookings})""");

        var court = await ctx.Courts.FirstAsync(c => c.ClubId == clubId);
        var playMode = await ctx.PlayModes.FirstAsync(p => p.ClubId == clubId);
        for (var i = 0; i < played; i++)
        {
            var from = DateTimeOffset.UtcNow.Date.AddDays(-10 - i).AddHours(10);
            ctx.Add(new Domain.Bookings.Booking(clubId, court.Id, playMode.Id, new DateTimeOffsetInterval(new DateTimeOffset(from, TimeSpan.Zero), new DateTimeOffset(from.AddHours(1), TimeSpan.Zero)), "UTC", [guest.Id]));
            await ctx.SaveChangesAsync();
        }
    }
}
