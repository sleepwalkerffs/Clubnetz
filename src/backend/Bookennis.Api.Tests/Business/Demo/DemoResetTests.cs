using Bookennis.Api.Business.Demo;
using Bookennis.Api.Data;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Members;
using Bookennis.Domain.PlayRequests;
using Bookennis.Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Demo;

public class DemoResetTests(TestFixture fixture) : TestBase(fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    /// <summary>A context of the database of this test without a club: the demo works across both clubs.</summary>
    private AppDbContext CreateContext() => DemoReset.CreateContext(Query(ctx => ctx.Database.GetConnectionString())!);

    [Fact]
    public async Task Run_RefusesADatabaseWithRealUsers()
    {
        await using var context = CreateContext();
        var users = await context.Users.CountAsync();

        var reset = () => DemoReset.Run(context, Now, CancellationToken.None);

        await reset.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not a demo database*");
        await using var check = CreateContext();
        (await check.Users.CountAsync()).Should().Be(users);
        (await check.Clubs.AnyAsync(c => c.Name == DemoContent.Europe.Name)).Should().BeFalse();
    }

    [Fact]
    public async Task Run_FillsAnEmptyDatabaseWithBothClubs_AndAgainEveryTime()
    {
        await using (var context = CreateContext())
            await DemoReset.Wipe(context, CancellationToken.None);

        await using (var context = CreateContext())
            await DemoReset.Run(context, Now, CancellationToken.None);

        var first = await Counts();
        first.Clubs.Should().Be(2);
        first.Bookings.Should().BeGreaterThan(2000);

        // The nightly reset gives the same picture again, nothing is doubled
        await using (var context = CreateContext())
            await DemoReset.Run(context, Now, CancellationToken.None);

        (await Counts()).Should().Be(first);
    }

    [Fact]
    public async Task Seed_GivesEveryPersonaAWorkingAccountAndSomethingToSee()
    {
        await Seed();
        await using var context = CreateContext();
        var hasher = new PasswordHasher<User>();

        var personaEmails = DemoContent.Clubs.SelectMany(c => c.Personas).Select(p => p.Email).Distinct().ToList();
        var personas = await context.Users.Where(u => personaEmails.Contains(u.Email!)).ToListAsync();
        personas.Should().HaveCount(personaEmails.Count);
        foreach (var user in personas)
        {
            user.EmailConfirmed.Should().BeTrue();
            user.LockoutEnabled.Should().BeFalse("the accounts are public, a visitor must not lock them for everybody");
            hasher.VerifyHashedPassword(user, user.PasswordHash!, DemoAccounts.Password).Should().NotBe(PasswordVerificationResult.Failed);
        }

        var demoClubIds = await DemoClubIds(context);
        var demoUsers = await context.ClubMembers.Where(m => demoClubIds.Contains(m.ClubId)).Select(m => m.UserId).Distinct().ToListAsync();
        var emails = await context.Users.Where(u => demoUsers.Contains(u.Id) && u.Email != null).Select(u => u.Email).ToListAsync();
        emails.Should().OnlyContain(e => DemoAccounts.IsDemoEmail(e));

        // One person is a member of both clubs
        var twoClubs = personas.Single(u => u.Email == DemoContent.Europe.Personas.Single(p => p.Persona == DemoPersona.TwoClubs).Email);
        (await context.ClubMembers.CountAsync(m => m.UserId == twoClubs.Id)).Should().Be(2);

        foreach (var content in DemoContent.Clubs)
        {
            var club = await context.Clubs.SingleAsync(c => c.Name == content.Name);
            club.Country.Should().Be(content.Country);
            club.Currency.Should().Be(content.Currency);

            var memberEmail = content.Personas.Single(p => p.Persona == DemoPersona.Member).Email;
            var member = await context.ClubMembers.SingleAsync(m => m.ClubId == club.Id && context.Users.Any(u => u.Id == m.UserId && u.Email == memberEmail));

            // Upcoming matches, an open request of their own and one they were asked for, badges of both seasons, an open clubhouse tab
            var upcoming = await context.BookingPlayers.CountAsync(p => p.MemberId == member.Id && p.Booking.Interval.From > Now);
            upcoming.Should().BeInRange(1, 3);
            (await context.PlayRequests.CountAsync(r => r.ClubId == club.Id && r.Status == PlayRequestStatus.Open)).Should().Be(2);
            (await context.PlayRequests.CountAsync(r => r.RequesterMemberId == member.Id)).Should().Be(1);
            (await context.MemberBadges.CountAsync(b => b.MemberId == member.Id)).Should().BeGreaterThan(4);
            (await context.ClubhouseTabs.CountAsync(t => t.MemberId == member.Id && t.Status == Domain.Clubhouse.ClubhouseTabStatus.Open)).Should().Be(1);

            var seasons = await context.Seasons.Where(s => s.ClubId == club.Id).OrderBy(s => s.Period.From).ToListAsync();
            seasons.Should().HaveCount(2);
            seasons[0].IsWrappedPublished.Should().BeTrue();
            seasons[1].IsActiveOn(ClubTimeZone.Today(club.TimeZone, Now)).Should().BeTrue();

            (await context.ClubEvents.CountAsync(e => e.ClubId == club.Id && e.StartDate > ClubTimeZone.Today(club.TimeZone, Now))).Should().BeGreaterThan(3);
            (await context.ClubAnnouncements.CountAsync(a => a.ClubId == club.Id)).Should().Be(content.Announcements.Length);
            (await context.MembershipOrders.CountAsync(o => o.ClubId == club.Id)).Should().BeGreaterThan(10);
            (await context.AccountingEntries.CountAsync(e => e.ClubId == club.Id)).Should().BeGreaterThan(10);
            (await context.Families.CountAsync(f => f.ClubId == club.Id)).Should().Be(1);
        }
    }

    [Theory]
    [InlineData("2026-11-23T10:00:00Z")] // a Monday: the doubles request lands on the Wednesday of the team training
    [InlineData("2026-12-24T16:00:00Z")] // the bookings reach into the next year
    [InlineData("2027-03-27T09:00:00Z")] // daylight saving starts in Europe the next day
    public async Task Seed_NeverPutsTwoBookingsOnACourtAtOnce_NorAPlayerOnTwoCourts(string at)
    {
        var now = DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture);
        await using (var seedContext = CreateContext())
            await new DemoSeeder(seedContext, now).Seed(CancellationToken.None);
        await using var context = CreateContext();
        var demoClubIds = await DemoClubIds(context);

        var bookings = await context.Bookings
            .Where(b => demoClubIds.Contains(b.ClubId))
            .Select(b => new { b.CourtId, b.Interval.From, b.Interval.To, Players = b.Players.Select(p => p.MemberId).ToList() })
            .ToListAsync();

        foreach (var court in bookings.GroupBy(b => b.CourtId))
        {
            var ordered = court.OrderBy(b => b.From).ToList();
            for (var i = 1; i < ordered.Count; i++)
                ordered[i].From.Should().BeOnOrAfter(ordered[i - 1].To, "court {0} is booked twice", court.Key);
        }

        foreach (var player in bookings.SelectMany(b => b.Players.Select(p => (Player: p, b.From, b.To))).GroupBy(p => p.Player))
        {
            var ordered = player.OrderBy(b => b.From).ToList();
            for (var i = 1; i < ordered.Count; i++)
                ordered[i].From.Should().BeOnOrAfter(ordered[i - 1].To, "member {0} plays twice at once", player.Key);
        }

        // The open requests can be filled: their courts are free and the players who said yes play nowhere else then
        var requests = await context.PlayRequests.Where(r => demoClubIds.Contains(r.ClubId)).Include(r => r.Invitees).ToListAsync();
        foreach (var request in requests)
        {
            bookings.Should().NotContain(b => request.CourtIds.Contains(b.CourtId) && b.From < request.EndsAt && request.StartsAt < b.To);
            var players = request.Invitees.Where(i => i.Status == PlayRequestInviteeStatus.Accepted).Select(i => i.MemberId).Append(request.RequesterMemberId);
            bookings.Should().NotContain(b => b.Players.Any(p => players.Contains(p)) && b.From < request.EndsAt && request.StartsAt < b.To);
        }

        // Every booking lies in a season of its club
        var seasons = await context.Seasons.AsNoTracking().Where(s => demoClubIds.Contains(s.ClubId)).Select(s => s.Period).ToListAsync();
        var lastSeasonEnd = seasons.Max(p => p.To);
        bookings.Should().OnlyContain(b => DateOnly.FromDateTime(b.From.UtcDateTime) <= lastSeasonEnd.AddDays(1));
    }

    [Fact]
    public async Task EnsureOnlyDemoData_KnowsDemoAddressesInAnyCase_AndRefusesEverythingElse()
    {
        await using (var context = CreateContext())
            await DemoReset.Wipe(context, CancellationToken.None);
        await Seed();

        await using (var context = CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE \"AspNetUsers\" SET \"Email\" = upper(\"Email\") WHERE \"Email\" IS NOT NULL");
            await DemoReset.EnsureOnlyDemoData(context, CancellationToken.None);
        }

        // A user without an address who belongs to nobody is not a child of a demo user
        await using (var context = CreateContext())
        {
            var id = await context.Users.Where(u => u.Email == null).Select(u => u.Id).FirstAsync();
            await context.Database.ExecuteSqlRawAsync("UPDATE \"AspNetUsers\" SET \"BelongsToUserId\" = NULL WHERE \"Id\" = {0}", id);
            var check = () => DemoReset.EnsureOnlyDemoData(context, CancellationToken.None);
            await check.Should().ThrowAsync<InvalidOperationException>();
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUsers\" WHERE \"Id\" = {0}", id);
        }

        // Members of a club that is not a demo club, even without any real user
        await using (var context = CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE \"Clubs\" SET \"Name\" = 'TC Real' WHERE \"Name\" = {0}", DemoContent.UnitedStates.Name);
            var check = () => DemoReset.EnsureOnlyDemoData(context, CancellationToken.None);
            await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*clubs*");
        }
    }

    [Fact]
    public async Task Seed_OnlyLetsPlayersOnCourtsOfTheirSportAndOutdoorCourtsStayEmptyInWinter()
    {
        await Seed();
        await using var context = CreateContext();
        var demoClubIds = await DemoClubIds(context);

        var courts = await context.Courts.Where(c => demoClubIds.Contains(c.ClubId)).ToDictionaryAsync(c => c.Id);
        var sports = await context.MemberSeasons
            .Where(s => context.ClubMembers.Any(m => m.Id == s.MemberId && demoClubIds.Contains(m.ClubId)))
            .Select(s => new { s.MemberId, s.Sports })
            .ToListAsync();
        var sportsOf = sports.GroupBy(s => s.MemberId).ToDictionary(g => g.Key, g => g.First().Sports);

        var bookings = await context.Bookings
            .Where(b => demoClubIds.Contains(b.ClubId))
            .Select(b => new { b.CourtId, b.Interval.From, b.TimeZoneInfoId, Players = b.Players.Select(p => p.MemberId).ToList() })
            .ToListAsync();

        foreach (var booking in bookings)
        {
            var court = courts[booking.CourtId];
            foreach (var player in booking.Players)
                Sports.Covers(sportsOf[player], court.Sport).Should().BeTrue();

            if (!court.IsIndoor)
                ClubTimeZone.ToLocal(booking.From, ClubTimeZone.Get(booking.TimeZoneInfoId)).Month.Should().BeInRange(4, 10);
        }
    }

    private async Task Seed()
    {
        await using var context = CreateContext();
        await new DemoSeeder(context, Now).Seed(CancellationToken.None);
    }

    private static Task<List<int>> DemoClubIds(AppDbContext context)
    {
        var names = DemoContent.Clubs.Select(c => c.Name).ToList();
        return context.Clubs.Where(c => names.Contains(c.Name)).Select(c => c.Id).ToListAsync();
    }

    private async Task<(int Clubs, int Users, int Bookings, int Events, int Tabs)> Counts()
    {
        await using var context = CreateContext();
        return (await context.Clubs.CountAsync(), await context.Users.CountAsync(), await context.Bookings.CountAsync(), await context.ClubEvents.CountAsync(), await context.ClubhouseTabs.CountAsync());
    }
}
