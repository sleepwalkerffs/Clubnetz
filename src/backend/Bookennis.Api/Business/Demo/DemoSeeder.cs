using System.Globalization;
using System.Drawing;
using Bookennis.Api.Business.Badges;
using Bookennis.Api.Data;
using Bookennis.Domain.Accounting;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubhouse;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Courts;
using Bookennis.Domain.CourtSales;
using Bookennis.Domain.Families;
using Bookennis.Domain.Members;
using Bookennis.Domain.Memberships;
using Bookennis.Domain.PlayRequests;
using Bookennis.Domain.User;
using Bookennis.Domain.Weather;
using Bookennis.Global.Intervals;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DemoUser = Bookennis.Domain.User.User;

namespace Bookennis.Api.Business.Demo;

/// <summary>
/// Fills an empty database with the two demo clubs (<see cref="DemoContent"/>). Every date is relative to <c>now</c>, so the demo always
/// shows a busy week, a season that is running and a finished one with its Wrapped. The random numbers have a fixed seed: the same day
/// gives the same data.
/// <para>
/// Writes the entities directly (no handlers): nothing is mailed or pushed, no audit entries. Run it through <see cref="DemoReset"/>,
/// which checks that the database holds nothing but demo data.
/// </para>
/// </summary>
public sealed class DemoSeeder(AppDbContext context, DateTimeOffset now)
{
    public async Task Seed(CancellationToken cancellationToken)
    {
        var passwordHash = new PasswordHasher<DemoUser>().HashPassword(null!, DemoAccounts.Password);
        var users = new Dictionary<string, DemoUser>(StringComparer.OrdinalIgnoreCase);
        var twoClubs = DemoContent.Europe.Personas.Single(p => p.Persona == DemoPersona.TwoClubs);

        foreach (var content in DemoContent.Clubs)
        {
            var extra = content == DemoContent.Europe ? [] : new[] { twoClubs };
            await new ClubSeeder(context, content, now, passwordHash, users, extra).Seed(cancellationToken);
        }
    }

    private sealed class SeedMember(ClubMember member, DemoUser user, double activity, Sport[]? sports, bool lastSeason, bool currentSeason, bool isChild, DemoPersona? persona)
    {
        public ClubMember Member { get; } = member;
        public DemoUser User { get; } = user;
        public double Activity { get; } = activity;

        /// <summary><c>null</c> = every sport.</summary>
        public Sport[]? Sports { get; } = sports;

        public bool LastSeason { get; } = lastSeason;
        public bool CurrentSeason { get; set; } = currentSeason;
        public bool IsChild { get; } = isChild;
        public DemoPersona? Persona { get; } = persona;
        public int Id => Member.Id;
        public int Upcoming { get; set; }

        public bool Plays(Sport sport) => Sports is null || Sports.Contains(sport);

        public bool EnrolledIn(bool current) => current ? CurrentSeason : LastSeason;
    }

    private sealed class ClubSeeder(AppDbContext context, DemoClubContent content, DateTimeOffset now, string passwordHash, Dictionary<string, DemoUser> users, DemoPerson[] extraPersonas)
    {
        private static readonly TimeOnly Opening = new(7, 0);
        private static readonly TimeOnly Closing = new(22, 0);

        private readonly Random random = new(content.Key == "eu" ? 2026 : 1776);
        private readonly TimeZoneInfo timeZone = ClubTimeZone.Get(content.TimeZoneId);
        private readonly List<SeedMember> members = [];
        private readonly List<Court> courts = [];

        /// <summary>Court, local day and hour that are taken (bookings, blockings, slots kept free for play requests).</summary>
        private readonly HashSet<(int Court, DateOnly Day, int Hour)> takenSlots = [];

        /// <summary>Member, local day and hour: nobody plays on two courts at once.</summary>
        private readonly HashSet<(int Member, DateOnly Day, int Hour)> busyMembers = [];

        private Club club = null!;
        private Season lastSeason = null!;
        private Season currentSeason = null!;
        private Season? nextSeason;
        private PlayMode singles = null!;
        private PlayMode doubles = null!;
        private PlayMode padel = null!;
        private PlayMode training = null!;
        private PlayMode teamMatch = null!;
        private DateOnly today;

        /// <summary>The last day with bookings: two weeks ahead, like a member books.</summary>
        private DateOnly LastDay => today.AddDays(14);

        private SeedMember Persona(DemoPersona persona) => members.Single(m => m.Persona == persona);

        private IEnumerable<Court> CourtsOf(Sport sport, bool? indoor = null) => courts.Where(c => c.Sport == sport && (indoor is null || c.IsIndoor == indoor));

        public async Task Seed(CancellationToken cancellationToken)
        {
            today = ClubTimeZone.Today(timeZone, now);

            CreateClub();
            await context.SaveChangesAsync(cancellationToken);

            CreateMembers();
            await context.SaveChangesAsync(cancellationToken);

            CreateFamily();
            CreateCourtBlockings();
            CreateRecurringSeries();
            CreatePlayRequests();
            CreateHighlightBookings();
            CreateBookings();
            await context.SaveChangesAsync(cancellationToken);

            // Tens of thousands of bookings: every later save would compare all of them again. Only the ids are used from here on.
            context.ChangeTracker.Clear();

            await CreateBadges(cancellationToken);
            await CreateEvents(cancellationToken);
            CreateAnnouncements();
            CreateMemberships();
            CreateClubhouse();
            CreateCourtSales();
            CreateAccounting();
            (await context.Seasons.SingleAsync(s => s.Id == lastSeason.Id, cancellationToken)).PublishWrapped(now, today);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        /// <summary>A local time of the club, in UTC (Npgsql only writes UTC offsets).</summary>
        private DateTimeOffset At(DateOnly day, TimeOnly time) => ClubTimeZone.At(day, time, timeZone).ToUniversalTime();

        private DateTimeOffset At(DateOnly day, int hour, int minute = 0) => At(day, new TimeOnly(hour, minute));

        private void CreateClub()
        {
            club = new Club(content.Name, new TimeOnlyInterval(Opening, Closing), new TimeOnlyInterval(new TimeOnly(17, 0), new TimeOnly(20, 0)), []);
            club.SetRegion(content.Country, content.TimeZoneId, content.Currency);
            club.SetLocation(new GeoLocation(content.Latitude, content.Longitude));
            club.UpdateContactSettings(content.WebsiteUrl, null);
            context.Clubs.Add(club);

            for (var i = 0; i < content.Courts.Length; i++)
            {
                var data = content.Courts[i];
                var court = new Court(club.Id, data.Name, data.Alias, i);
                court.SetSport(data.Sport);
                court.SetIndoor(data.IsIndoor);
                courts.Add(court);
            }

            context.Courts.AddRange(courts);

            var names = content.PlayModes;
            MemberRole[] staff = [MemberRole.Admin, MemberRole.Trainer, MemberRole.SportsDirector, MemberRole.YouthSportsDirector];
            singles = club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], Color.FromArgb(0x2E, 0x7D, 0x32), 2, true, names.Singles, null, false, true));
            doubles = club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], Color.FromArgb(0x15, 0x65, 0xC0), 4, true, names.Doubles, null, false, true));
            padel = club.AddPlayMode(new Club.PlayModeDto([MemberRole.User], Color.FromArgb(0xEF, 0x6C, 0x00), 4, true, names.Padel, null, false, true));
            training = club.AddPlayMode(new Club.PlayModeDto(staff, Color.FromArgb(0x6A, 0x1B, 0x9A), null, false, names.Training, null, false, true, AllowRecurring: true));
            teamMatch = club.AddPlayMode(new Club.PlayModeDto(staff, Color.FromArgb(0xC6, 0x28, 0x28), null, false, names.TeamMatch, null, false, true, AllowRecurring: true));

            // The play modes get their ids when the club is saved, the courts already have theirs (HiLo)
            var tennisCourts = CourtsOf(Sport.Tennis).Select(c => c.Id).ToList();
            singles.RestrictToCourts(tennisCourts);
            doubles.RestrictToCourts(tennisCourts);
            teamMatch.RestrictToCourts(tennisCourts);
            padel.RestrictToCourts(CourtsOf(Sport.Padel).Select(c => c.Id));

            lastSeason = club.AddSeason(new DateOnlyInterval(new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)));
            currentSeason = club.AddSeason(new DateOnlyInterval(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)));

            // In the last two weeks of the year the bookings reach into January: the members are enrolled there already
            if (LastDay.Year > today.Year)
                nextSeason = club.AddSeason(new DateOnlyInterval(new DateOnly(today.Year + 1, 1, 1), new DateOnly(today.Year + 1, 12, 31)));
        }

        private DemoUser GetOrCreateUser(DemoPerson person)
        {
            if (users.TryGetValue(person.Email, out var existing))
                return existing;

            var user = new DemoUser(person.Email, person.Email, person.FirstName, person.LastName, person.Birthday, person.Gender, content.Street, content.City, content.ZipCode, content.Country, content.Language);
            PrepareAccount(user, person.Email);
            context.Users.Add(user);
            context.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = (int)UserRoles.User });
            users[person.Email] = user;
            return user;
        }

        private void PrepareAccount(DemoUser user, string? email)
        {
            user.Email = email;
            user.UserName = email ?? $"demo-child-{Guid.NewGuid():N}";
            user.NormalizedEmail = email?.ToUpperInvariant();
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.EmailConfirmed = email is not null;
            user.PasswordHash = email is null ? null : passwordHash;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            // The accounts are public: five wrong passwords of one visitor must not lock everybody else out
            user.LockoutEnabled = false;
            user.AcceptPrivacyPolicy();
            user.DismissProfileCompletionBanner();
        }

        private SeedMember AddMember(DemoUser user, MemberRole[] roles, double activity, Sport[]? sports, bool lastSeason, bool currentSeason, bool isChild = false, DemoPersona? persona = null)
        {
            var member = new ClubMember(user.Id, club.Id, roles);
            context.ClubMembers.Add(member);
            var seed = new SeedMember(member, user, activity, sports, lastSeason, currentSeason, isChild, persona);
            members.Add(seed);
            return seed;
        }

        private void CreateMembers()
        {
            foreach (var person in content.Personas.Concat(extraPersonas))
            {
                var user = GetOrCreateUser(person);
                var plays = person.Persona != DemoPersona.ClubhouseHost;
                var activity = person.Persona switch
                {
                    DemoPersona.Member => 1.9,
                    DemoPersona.Admin => 1.0,
                    DemoPersona.TwoClubs => 0.9,
                    DemoPersona.Parent => 0.8,
                    _ => 0.6,
                };
                AddMember(user, person.Roles, plays ? activity : 0, null, plays, plays, persona: person.Persona);
            }

            AddMember(GetOrCreateUser(content.SecondParent), content.SecondParent.Roles, 0.7, null, true, true);

            var usedNames = new HashSet<string>(members.Select(m => m.User.FullName));
            var count = content.Country == Country.UnitedStates ? 85 : 115;
            while (members.Count < count + content.Personas.Length)
            {
                var female = random.Next(100) < 42;
                var first = Pick(female ? content.FemaleFirstNames : content.MaleFirstNames);
                var last = Pick(content.LastNames);
                if (!usedNames.Add($"{first} {last}"))
                    continue;

                var birthday = new DateOnly(random.Next(1952, 2006), random.Next(1, 13), random.Next(1, 29));
                var person = new DemoPerson(first, last, female ? Gender.Female : Gender.Male, birthday, [MemberRole.User]);
                var sports = random.Next(100) switch
                {
                    < 68 => null,
                    < 84 => new[] { Sport.Tennis },
                    _ => [Sport.Padel],
                };
                var enrollment = random.Next(100);
                var roles = members.Count % 13 == 0 ? new[] { MemberRole.User, MemberRole.SportsDirector } : person.Roles;
                AddMember(GetOrCreateUser(person), roles, 0.3 + random.NextDouble() * 1.3, sports, lastSeason: enrollment >= 9, currentSeason: enrollment is < 9 or >= 16);
            }
        }

        private void CreateFamily()
        {
            var parent = Persona(DemoPersona.Parent);
            var secondParent = members.Single(m => m.User.Email == content.SecondParent.Email);
            var children = new List<SeedMember>();
            foreach (var child in content.Children)
            {
                var user = new DemoUser(child.FirstName, child.LastName, child.Birthday, child.Gender, parent.User.Id, content.Street, content.City, content.ZipCode, content.Country, content.Language);
                PrepareAccount(user, null);
                context.Users.Add(user);
                children.Add(AddMember(user, child.Roles, 0, null, true, true, isChild: true));
            }

            context.Families.Add(new Family(club.Id, [parent.Id, secondParent.Id], children.ConvertAll(c => c.Id)));

            foreach (var member in members)
            {
                if (member.LastSeason)
                    context.MemberSeasons.Add(new MemberSeason(member.Id, lastSeason.Id, sports: member.Sports));
                if (member.CurrentSeason)
                {
                    context.MemberSeasons.Add(new MemberSeason(member.Id, currentSeason.Id, sports: member.Sports));
                    if (nextSeason is not null)
                        context.MemberSeasons.Add(new MemberSeason(member.Id, nextSeason.Id, sports: member.Sports));
                }
            }
        }

        private void CreateCourtBlockings()
        {
            var outdoor = courts.Where(c => !c.IsIndoor).ToList();
            for (var year = today.Year - 2; year <= today.Year; year++)
            {
                var data = new CourtBlocking.BlockingData(content.WinterBreakTitle, outdoor.ConvertAll(c => c.Id), new DateOnly(year, 11, 1), new DateOnly(year + 1, 3, 31), null, null, content.TimeZoneId, null, null);
                context.CourtBlockings.Add(new CourtBlocking(club.Id, data));
            }
        }

        private static bool IsWinter(DateOnly day) => day.Month is >= 11 or <= 3;

        private bool IsFree(Court court, DateOnly day, int fromHour, int hours)
        {
            if (!court.IsIndoor && IsWinter(day))
                return false;

            for (var hour = fromHour; hour < fromHour + hours; hour++)
            {
                if (hour >= Closing.Hour || takenSlots.Contains((court.Id, day, hour)))
                    return false;
            }

            return true;
        }

        private void Take(Court court, DateOnly day, int fromHour, int hours)
        {
            for (var hour = fromHour; hour < fromHour + hours; hour++)
                takenSlots.Add((court.Id, day, hour));
        }

        private bool IsAvailable(SeedMember member, DateOnly day, int fromHour, int hours)
        {
            for (var hour = fromHour; hour < fromHour + hours; hour++)
            {
                if (busyMembers.Contains((member.Id, day, hour)))
                    return false;
            }

            return true;
        }

        private Booking Book(Court court, PlayMode playMode, DateOnly day, TimeOnly from, TimeOnly to, IReadOnlyList<SeedMember> players, string? comment = null, int? seriesId = null)
        {
            var hours = (int)Math.Ceiling((to - from).TotalHours);
            Take(court, day, from.Hour, hours);
            foreach (var player in players)
            {
                for (var hour = from.Hour; hour < from.Hour + hours; hour++)
                    busyMembers.Add((player.Id, day, hour));

                if (At(day, from) > now)
                    player.Upcoming++;
            }

            var booking = new Booking(club.Id, court.Id, playMode.Id, new DateTimeOffsetInterval(At(day, from), At(day, to)), content.TimeZoneId, players.Select(p => p.Id).ToList(), comment, seriesId);
            context.Bookings.Add(booking);
            return booking;
        }

        private void CreateRecurringSeries()
        {
            var indoorTennis = CourtsOf(Sport.Tennis, indoor: true).ToList();
            var trainer = Persona(DemoPersona.Trainer);
            var children = members.Where(m => m.IsChild).ToList();
            var teamPlayers = members.Where(m => m.Persona is null && !m.IsChild && m.User.Gender == Gender.Male && m.CurrentSeason && m.Plays(Sport.Tennis)).Take(5).ToList();
            var padelPlayers = new[] { Persona(DemoPersona.TwoClubs) }
                .Concat(members.Where(m => m.Persona is null && !m.IsChild && m.CurrentSeason && m.Plays(Sport.Padel)).OrderByDescending(m => m.Sports is not null).Take(3))
                .ToList();

            CreateSeries(indoorTennis[0], training, DayOfWeek.Tuesday, new(16, 0), new(17, 0), [trainer, .. children], content.KidsTrainingComment);
            CreateSeries(indoorTennis[0], training, DayOfWeek.Thursday, new(16, 0), new(17, 0), [trainer, .. children], content.KidsTrainingComment);
            CreateSeries(indoorTennis[^1], training, DayOfWeek.Wednesday, new(19, 0), new(21, 0), [trainer, .. teamPlayers], content.TeamTrainingComment);
            CreateSeries(CourtsOf(Sport.Padel).First(), padel, DayOfWeek.Friday, new(18, 0), new(19, 30), padelPlayers.Take(4).ToList(), content.PadelMeetupComment);
        }

        private void CreateSeries(Court court, PlayMode playMode, DayOfWeek dayOfWeek, TimeOnly from, TimeOnly to, IReadOnlyList<SeedMember> players, string comment)
        {
            var start = currentSeason.Period.From;
            while (start.DayOfWeek != dayOfWeek)
                start = start.AddDays(1);

            var series = new RecurringBookingSeries(club.Id, court.Id, playMode.Id, dayOfWeek, from, to, content.TimeZoneId, 1, start, currentSeason.Period.To, comment, players.Select(p => p.Id).ToList());
            context.RecurringBookingSeries.Add(series);

            for (var day = start; day <= currentSeason.Period.To; day = day.AddDays(7))
            {
                if (IsFree(court, day, from.Hour, (int)Math.Ceiling((to - from).TotalHours)))
                    Book(court, playMode, day, from, to, players, comment, series.Id);
            }
        }

        /// <summary>Two open play requests, kept free in the booking grid so the last "yes" can book the court.</summary>
        private void CreatePlayRequests()
        {
            var member = Persona(DemoPersona.Member);
            var indoor = CourtsOf(Sport.Tennis, indoor: true).ToList();
            var others = members.Where(m => m.Persona is null && !m.IsChild && m.CurrentSeason && m.Plays(Sport.Tennis)).ToList();

            // The member asks for a doubles: one yes, one no, the rest still open
            var doublesDay = today.AddDays(2);
            var doublesInvitees = others.Where(m => IsAvailable(m, doublesDay, 19, 2)).Take(6).ToList();
            var doublesCourts = indoor.Where(c => IsFree(c, doublesDay, 19, 2)).ToList();
            foreach (var court in doublesCourts)
                Take(court, doublesDay, 19, 2);
            KeepFree([member, doublesInvitees[0]], doublesDay, 19, 2);

            var doublesRequest = PlayRequest.Create(club.Id, member.Id, new PlayRequest.RequestData(
                doubles.Id,
                new DateTimeOffsetInterval(At(doublesDay, 19), At(doublesDay, 20, 30)),
                content.TimeZoneId,
                doublesCourts.ConvertAll(c => c.Id),
                4,
                doublesInvitees.ConvertAll(m => m.Id),
                content.PlayRequestComment,
                null), now.AddHours(-20));
            doublesRequest.Accept(doublesInvitees[0].Id, now.AddHours(-8));
            doublesRequest.Decline(doublesInvitees[1].Id, now.AddHours(-5));
            context.PlayRequests.Add(doublesRequest);

            // Somebody else asks the member for a singles tomorrow: the member's "yes" books the court
            var singlesDay = today.AddDays(1);
            var singlesPlayers = others.Where(m => IsAvailable(m, singlesDay, 19, 1) && !doublesInvitees.Contains(m)).ToList();
            var requester = singlesPlayers[^1];
            var singlesCourts = indoor.Where(c => IsFree(c, singlesDay, 19, 1)).ToList();
            foreach (var court in singlesCourts)
                Take(court, singlesDay, 19, 1);
            KeepFree([member, requester], singlesDay, 19, 1);

            context.PlayRequests.Add(PlayRequest.Create(club.Id, requester.Id, new PlayRequest.RequestData(
                singles.Id,
                new DateTimeOffsetInterval(At(singlesDay, 19), At(singlesDay, 20)),
                content.TimeZoneId,
                singlesCourts.ConvertAll(c => c.Id),
                2,
                [member.Id, singlesPlayers[^2].Id, singlesPlayers[^3].Id],
                content.OtherPlayRequestComment,
                null), now.AddHours(-3)));

            context.PlayerGroups.Add(new PlayerGroup(club.Id, member.Id, content.PlayerGroupName, others.Skip(2).Take(6).Select(m => m.Id).ToList()));
        }

        private void KeepFree(IEnumerable<SeedMember> players, DateOnly day, int fromHour, int hours)
        {
            foreach (var player in players)
            {
                for (var hour = fromHour; hour < fromHour + hours; hour++)
                    busyMembers.Add((player.Id, day, hour));
            }
        }

        /// <summary>What the personas see on "My club" right away: an upcoming match of the member, a parent playing with a child.</summary>
        private void CreateHighlightBookings()
        {
            var member = Persona(DemoPersona.Member);
            var indoor = CourtsOf(Sport.Tennis, indoor: true).First();
            var partner = members.First(m => m.Persona is null && !m.IsChild && m.CurrentSeason && m.Plays(Sport.Tennis));
            Book(indoor, singles, today.AddDays(1), new(18, 0), new(19, 0), [member, partner]);

            var saturday = today.AddDays(1);
            while (saturday.DayOfWeek != DayOfWeek.Saturday)
                saturday = saturday.AddDays(1);

            var parent = Persona(DemoPersona.Parent);
            var child = members.First(m => m.IsChild);
            Book(indoor, singles, saturday, new(10, 0), new(11, 0), [parent, child]);
        }

        private double Demand(Court court, DateOnly day, int hour)
        {
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var byHour = weekend
                ? hour switch { < 8 => 0.05, < 12 => 0.55, < 18 => 0.42, _ => 0.18 }
                : hour switch { < 9 => 0.1, < 12 => 0.16, < 14 => 0.14, < 17 => 0.24, < 20 => 0.72, _ => 0.32 };

            var bySeason = court.Sport == Sport.Padel ? 0.75
                : !court.IsIndoor ? day.Month switch { 4 or 10 => 0.55, 5 or 9 => 0.8, _ => 1.0 }
                : IsWinter(day) ? 1.0 : 0.22;

            var daysAhead = day.DayNumber - today.DayNumber;
            var byLeadTime = daysAhead <= 0 ? 1.0 : Math.Max(0.06, 0.75 * Math.Pow(0.8, daysAhead - 1));
            return 0.6 * byHour * bySeason * byLeadTime;
        }

        private void CreateBookings()
        {
            for (var day = lastSeason.Period.From; day <= LastDay; day = day.AddDays(1))
            {
                var current = currentSeason.Period.From <= day;
                foreach (var court in courts)
                {
                    for (var hour = Opening.Hour; hour < Closing.Hour;)
                    {
                        if (!IsFree(court, day, hour, 1) || random.NextDouble() > Demand(court, day, hour))
                        {
                            hour++;
                            continue;
                        }

                        var playMode = court.Sport == Sport.Padel ? padel : random.Next(100) < 70 ? singles : doubles;
                        var hours = playMode == singles ? 1 : random.Next(100) < 55 ? 2 : 1;
                        if (!IsFree(court, day, hour, hours))
                            hours = 1;

                        var start = At(day, hour);
                        var players = PickPlayers(playMode == singles ? 2 : 4, court.Sport, day, hour, hours, current, upcoming: start > now);
                        if (players is null)
                        {
                            hour++;
                            continue;
                        }

                        var comment = random.Next(100) < 4 ? Pick(content.BookingComments) : null;
                        var end = hours == 2 && playMode == padel ? new TimeOnly(hour + 1, 30) : new TimeOnly(hour + hours, 0);
                        Book(court, playMode, day, new TimeOnly(hour, 0), end, players, comment);
                        hour += hours;
                    }
                }
            }
        }

        private List<SeedMember>? PickPlayers(int count, Sport sport, DateOnly day, int hour, int hours, bool currentSeason, bool upcoming)
        {
            var candidates = members
                .Where(m => !m.IsChild && m.Activity > 0 && m.EnrolledIn(currentSeason) && m.Plays(sport) && IsAvailable(m, day, hour, hours))
                .Where(m => !upcoming || m.Upcoming < (m.Persona is null ? 3 : 2))
                .ToList();

            var players = new List<SeedMember>();
            while (players.Count < count && candidates.Count > 0)
            {
                var total = candidates.Sum(c => c.Activity);
                var pick = random.NextDouble() * total;
                var index = 0;
                for (; index < candidates.Count - 1; index++)
                {
                    pick -= candidates[index].Activity;
                    if (pick <= 0)
                        break;
                }

                players.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return players.Count == count ? players : null;
        }

        private async Task CreateBadges(CancellationToken cancellationToken)
        {
            int[] matches = [10, 25, 50, 80];
            foreach (var season in new[] { lastSeason, currentSeason })
            {
                for (var level = 0; level < matches.Length; level++)
                {
                    var name = content.BadgeTierNames[level];
                    context.BadgeTiers.Add(new BadgeTier(club.Id, season.Id, level + 1, name, string.Format(content.BadgeTierDescription, matches[level]), matches[level], level + 1));
                }
            }

            foreach (var badge in content.OneTimeBadges)
            {
                var season = badge.LastSeason ? lastSeason : currentSeason;
                var oneTimeBadge = new OneTimeBadge(club.Id, season.Id, badge.Name, badge.Description);
                context.OneTimeBadges.Add(oneTimeBadge);

                var awardees = members.Where(m => m.Persona is null && !m.IsChild && m.EnrolledIn(!badge.LastSeason)).OrderBy(_ => random.Next()).Take(badge.AwardedToOthers).ToList();
                if (badge.AwardedTo is { } persona)
                    awardees.Insert(0, Persona(persona));

                foreach (var awardee in awardees)
                    context.MemberOneTimeBadges.Add(new MemberOneTimeBadge(awardee.Id, oneTimeBadge.Id));
            }

            await context.SaveChangesAsync(cancellationToken);

            var badges = new BadgeProgressionService(context);
            foreach (var member in members.Where(m => !m.IsChild))
            {
                if (member.LastSeason)
                    await badges.CheckAndAwardBadges(member.Id, club.Id, lastSeason.Id, cancellationToken);
                if (member.CurrentSeason)
                    await badges.CheckAndAwardBadges(member.Id, club.Id, currentSeason.Id, cancellationToken);
            }
        }

        private async Task CreateEvents(CancellationToken cancellationToken)
        {
            var organizer = Persona(DemoPersona.Admin);
            var registered = new List<(ClubEvent Event, DemoEvent Data)>();
            foreach (var data in content.Events)
            {
                var start = today.AddDays(data.DayOffset);
                var deadline = data.DeadlineDaysBefore is { } days ? At(start.AddDays(-days), 23, 59) : (DateTimeOffset?)null;
                var questions = data.Questions
                    .Select(q => new ClubEvent.QuestionData(null, q.Text, q.Mode, q.IsRequired, q.AllowQuantities, q.Options.Select(o => new ClubEvent.OptionData(null, o)).ToList()))
                    .ToList();
                var clubEvent = new ClubEvent(club.Id, organizer.Id, new ClubEvent.EventData(
                    data.Title,
                    data.Description,
                    data.Location,
                    data.Category,
                    start,
                    data.Days is { } length ? start.AddDays(length - 1) : null,
                    data.Start,
                    data.End,
                    data.Registration,
                    data.MaxParticipants,
                    deadline,
                    questions));
                context.ClubEvents.Add(clubEvent);
                registered.Add((clubEvent, data));
            }

            // The options get their ids on saving
            await context.SaveChangesAsync(cancellationToken);

            foreach (var (clubEvent, data) in registered.Where(e => e.Data.Registrations > 0))
            {
                var registeredAt = data.DayOffset < 0 ? At(today.AddDays(data.DayOffset - 14), 12) : now.AddDays(-1);
                var people = members.Where(m => !m.IsChild && m.Persona is not DemoPersona.ClubhouseHost && m.CurrentSeason).OrderBy(_ => random.Next()).Take(data.Registrations).ToList();
                foreach (var person in people)
                {
                    var headCount = person.Persona == DemoPersona.Parent ? 4 : random.Next(100) < 70 ? 1 : 2;
                    if (clubEvent.MaxParticipants is { } max && clubEvent.TotalHeadCount + headCount > max)
                        break;

                    var answers = new List<ClubEvent.AnswerData>();
                    foreach (var question in clubEvent.Questions)
                    {
                        if (!question.IsRequired && random.Next(100) < 60)
                            continue;

                        var option = question.Options[random.Next(question.Options.Count)];
                        answers.Add(new ClubEvent.AnswerData(option.Id, question.AllowQuantities ? headCount : 1));
                    }

                    var comment = random.Next(100) < 25 && data.RegistrationComments.Length > 0 ? Pick(data.RegistrationComments) : null;
                    clubEvent.Register(person.Id, headCount, answers, string.IsNullOrEmpty(comment) ? null : comment, registeredAt, timeZone);
                }
            }
        }

        private void CreateAnnouncements()
        {
            var author = Persona(DemoPersona.Admin);
            foreach (var data in content.Announcements)
            {
                var expires = data.ExpiresInDays is { } days ? today.AddDays(days) : (DateOnly?)null;
                var title = string.Format(CultureInfo.InvariantCulture, data.Title, today.Year + 1);
                context.ClubAnnouncements.Add(new ClubAnnouncement(club.Id, author.Id, new ClubAnnouncement.AnnouncementData(title, data.Body, data.IsPinned, expires), now.AddDays(-data.DaysAgo).AddHours(-random.Next(1, 8))));
            }
        }

        private void CreateMemberships()
        {
            var treasurer = Persona(DemoPersona.Treasurer);
            var plans = new List<(MembershipPlan Plan, DemoPlan Data)>();
            for (var i = 0; i < content.Plans.Length; i++)
            {
                var data = content.Plans[i];
                var plan = new MembershipPlan(club.Id, currentSeason.Id, new MembershipPlan.PlanData(
                    data.Name,
                    data.Description,
                    data.PriceCents,
                    data.MaxAdults,
                    data.IncludesChildren,
                    data.MaxAge is { } age ? today.Year - age : null,
                    null,
                    data.RequiresApproval,
                    data.NewMembersOnly,
                    false,
                    true,
                    null,
                    null,
                    data.IsSelfService,
                    data.Sports), i, content.Currency);
                context.MembershipPlans.Add(plan);
                plans.Add((plan, data));
            }

            var single = plans.First(p => p.Data.MaxAdults == 1 && !p.Data.IncludesChildren && p.Data.Sports is null && !p.Data.NewMembersOnly).Plan;
            var family = plans.First(p => p.Data.IncludesChildren && p.Data.MaxAdults == 2).Plan;
            var padelOnly = plans.FirstOrDefault(p => p.Data.Sports is [Sport.Padel]).Plan;
            var trial = plans.FirstOrDefault(p => p.Data.RequiresApproval).Plan;

            var paidAt = new DateOnly(today.Year, 1, 10);
            var parent = Persona(DemoPersona.Parent);
            var familyMembers = members.Where(m => m.IsChild || m.User.Email == content.SecondParent.Email).Prepend(parent).ToList();
            Pay(new MembershipOrder(family, parent.Id, familyMembers.ConvertAll(m => m.Id)), paidAt.AddDays(3), familyMembers);

            var adults = members.Where(m => m.Persona is null && !m.IsChild && m.CurrentSeason && m.Sports is null).Take(12).ToList();
            foreach (var member in adults)
                Pay(new MembershipOrder(single, member.Id, [member.Id]), paidAt.AddDays(random.Next(0, 60)), [member]);

            if (padelOnly is not null)
            {
                foreach (var member in members.Where(m => m.CurrentSeason && m.Sports is [Sport.Padel]).Take(3))
                    Pay(new MembershipOrder(padelOnly, member.Id, [member.Id]), paidAt.AddDays(random.Next(0, 60)), [member]);
            }

            // Members of last season who have not paid yet, and a newcomer waiting for the treasurer
            var notYetEnrolled = members.Where(m => !m.IsChild && m.Persona is null && !m.CurrentSeason).ToList();
            foreach (var member in notYetEnrolled.Take(2))
                context.MembershipOrders.Add(new MembershipOrder(single, member.Id, [member.Id]));

            if (trial is not null && notYetEnrolled.Count > 2)
                context.MembershipOrders.Add(new MembershipOrder(trial, notYetEnrolled[2].Id, [notYetEnrolled[2].Id]));

            void Pay(MembershipOrder order, DateOnly day, IReadOnlyCollection<SeedMember> covered)
            {
                day = day <= today ? day : today;
                var paid = At(day, 10);
                order.MarkPaid(MembershipPaymentMethod.BankTransfer, paid <= now ? paid : now, treasurer.Id);
                if (random.Next(100) < 30)
                    order.SetNote(Pick(content.MembershipNotes));
                context.MembershipOrders.Add(order);
                paidMemberships.Add((order, covered.First(), day));
            }
        }

        private readonly List<(MembershipOrder Order, SeedMember Payer, DateOnly Day)> paidMemberships = [];

        private void CreateClubhouse()
        {
            var host = Persona(DemoPersona.ClubhouseHost);
            var items = content.ClubhouseItems.Select((data, i) => new ClubhouseItem(club.Id, new ClubhouseItem.ItemData(data.Name, data.Category, data.PriceCents, true, i), content.Currency)).ToList();
            context.ClubhouseItems.AddRange(items);

            var drinks = items.Where(i => i.Category == ClubhouseItemCategory.Drink).ToList();
            var guests = members.Where(m => !m.IsChild && m.Activity > 0).ToList();

            for (var day = today.AddDays(-45); day < today; day = day.AddDays(1))
            {
                var tabs = day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? random.Next(2, 6) : random.Next(0, 3);
                foreach (var guest in guests.OrderBy(_ => random.Next()).Take(tabs))
                {
                    var opened = At(day, random.Next(16, 21), random.Next(0, 60));
                    var tab = new ClubhouseTab(club.Id, guest.Id, null, host.Id, opened, content.Currency);
                    AddRounds(tab, opened, random.Next(1, 4));
                    tab.MarkPaid(random.Next(100) switch { < 60 => ClubhousePaymentMethod.Cash, < 95 => ClubhousePaymentMethod.Card, _ => ClubhousePaymentMethod.Other }, host.Id, opened.AddMinutes(random.Next(45, 180)));
                    context.ClubhouseTabs.Add(tab);
                }
            }

            // Open tabs right now: the member, two others and guests from outside
            var openedToday = At(today, 17) < now ? At(today, 17) : At(today.AddDays(-1), 19);
            foreach (var guest in new[] { Persona(DemoPersona.Member) }.Concat(guests.Where(g => g.Persona is null).Take(2)))
            {
                var tab = new ClubhouseTab(club.Id, guest.Id, null, host.Id, openedToday, content.Currency);
                AddRounds(tab, openedToday, 2);
                context.ClubhouseTabs.Add(tab);
            }

            foreach (var walkIn in content.WalkInGuests)
            {
                var tab = new ClubhouseTab(club.Id, null, walkIn, host.Id, openedToday, content.Currency);
                AddRounds(tab, openedToday, 1);
                context.ClubhouseTabs.Add(tab);
            }

            void AddRounds(ClubhouseTab tab, DateTimeOffset start, int rounds)
            {
                for (var round = 0; round < rounds; round++)
                {
                    var at = start.AddMinutes(round * 25);
                    tab.Add(Pick(drinks), random.Next(1, 3), host.Id, at);
                    if (random.Next(100) < 35)
                        tab.Add(Pick(items), 1, host.Id, at);
                }
            }
        }

        private void CreateCourtSales()
        {
            var settings = new CourtSaleSettings(club.Id);
            settings.Update(new CourtSaleSettings.SettingsData(true, singles.Id, content.TimeZoneId, 120, 14, 2));
            context.CourtSaleSettings.Add(settings);

            DayOfWeek[] weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
            var rules = new[]
            {
                new CourtPriceRule.RuleData([], weekdays, Opening, new TimeOnly(17, 0), content.PriceRuleCentsPerHour[0], content.PriceRuleNames[0]),
                new CourtPriceRule.RuleData([], weekdays, new TimeOnly(17, 0), Closing, content.PriceRuleCentsPerHour[1], content.PriceRuleNames[1]),
                new CourtPriceRule.RuleData([], [DayOfWeek.Saturday, DayOfWeek.Sunday], Opening, Closing, content.PriceRuleCentsPerHour[2], content.PriceRuleNames[2]),
            };
            context.CourtPriceRules.AddRange(rules.Select(r => new CourtPriceRule(club.Id, r, content.Currency)));
        }

        private void CreateAccounting()
        {
            var german = content.Language == Language.German;
            var unitedStates = content.Country == Country.UnitedStates;
            var settings = new AccountingSettings(club.Id, 1);
            var year = FiscalYear.First(club.Id, 1, today.Year, today);
            var accounts = AccountingTemplate.CreateMoneyAccounts(club.Id, german, unitedStates).ToList();
            var categories = AccountingTemplate.CreateCategories(club.Id, german, unitedStates).ToList();
            context.Add(settings);
            context.Add(year);
            context.AddRange(accounts);
            context.AddRange(categories);

            var bank = accounts.First(a => a.Kind == MoneyAccountKind.Bank);
            var cash = accounts.First(a => a.Kind == MoneyAccountKind.Cash);
            var treasurer = Persona(DemoPersona.Treasurer);
            var entries = new List<(DateOnly Day, AccountingEntry.EntryData Data)>();

            foreach (var entry in content.AccountingEntries)
            {
                var category = categories.First(c => c.Number == entry.Category);
                var months = entry.Month is { } once ? [once] : Enumerable.Range(1, 12);
                foreach (var month in months)
                {
                    var day = new DateOnly(today.Year, month, Math.Min(entry.Day, DateTime.DaysInMonth(today.Year, month)));
                    if (day > today)
                        continue;

                    entries.Add((day, new AccountingEntry.EntryData(day, entry.Text, AccountingEntryKind.Booking, (entry.Cash ? cash : bank).Id, null, null,
                        [new AccountingEntry.LineData(category.Id, null, entry.AmountCents)], null, entry.Counterparty)));
                }
            }

            var fees = categories.First(c => c.Number == content.MembershipFeeCategory);
            foreach (var (order, payer, day) in paidMemberships)
            {
                entries.Add((day, new AccountingEntry.EntryData(day, string.Format(content.MembershipFeeText, order.PlanName, payer.User.FullName), AccountingEntryKind.Booking, bank.Id, null, null,
                    [new AccountingEntry.LineData(fees.Id, null, order.AmountCents)], payer.Id, null)));
            }

            foreach (var (day, data) in entries.OrderBy(e => e.Day))
                context.Add(new AccountingEntry(year, data, [], treasurer.Id, At(day, 18)));
        }

        private T Pick<T>(IReadOnlyList<T> values) => values[random.Next(values.Count)];
    }
}
