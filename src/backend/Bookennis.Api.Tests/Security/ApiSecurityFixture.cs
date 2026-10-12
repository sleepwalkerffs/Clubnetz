using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Identity;
using Bookennis.Domain.Bookings;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.ClubEvents;
using Bookennis.Domain.Clubs;
using Bookennis.Domain.Clubs.ApiKeys;
using Bookennis.Domain.Courts;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Members;
using Bookennis.Domain.User;
using Bookennis.Global.Intervals;
using FluentAssertions;
using Fusonic.Extensions.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;
using Xunit;

namespace Bookennis.Api.Tests.Security;

[CollectionDefinition(Name)]
public class ApiSecurityTestGroup : ICollectionFixture<ApiSecurityFixture>
{
    public const string Name = "API security";
}

/// <summary>
/// Starts the real app (the whole pipeline of <c>Program.cs</c> in the Production environment) in memory against its own database
/// and seeds two clubs. The security tests attack it over HTTP, the way a holder of an API key would.
/// Everything of club B carries <see cref="Canary"/> in its texts: if that text ever shows up in an answer to a key of club A, data leaked.
/// </summary>
public sealed class ApiSecurityFixture : IAsyncLifetime
{
    public const string Canary = "ZZCANARYOFCLUBB";
    public const string Marker = "AAMARKEROFCLUBA";
    public const string Password = "Sec-Test-1234!";

    private string connectionString = null!;
    private string adminConnectionString = null!;
    private string databaseName = null!;
    private Factory factory = null!;
    private readonly Dictionary<int, string> cookies = [];

    public HttpClient Client { get; private set; } = null!;
    public TestServer Server => factory.Server;
    public IServiceProvider Services => factory.Services;

    public SeededClub ClubA { get; private set; } = null!;
    public SeededClub ClubB { get; private set; } = null!;

    /// <summary>A member of club A without any special role.</summary>
    public SeededUser MemberOfA { get; private set; } = null!;

    /// <summary>
    /// An admin of club A (user and member id of club A) who is also a plain member of club B and has a booking there.
    /// What this user does or sees in club B must not show up through club A.
    /// </summary>
    public SeededUser AdminOfAWhoPlaysInB { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = TestConfigurationHelper.GetDefaultConfiguration(Directory.GetCurrentDirectory(), typeof(ApiSecurityFixture).Assembly);
        var templateConnectionString = configuration.GetConnectionString("Npgsql") ?? throw new PreconditionException("No connection string configured");

        // A copy of the migrated test database template (created by DatabaseAssemblyFixture), only for the app started here
        var template = new NpgsqlConnectionStringBuilder(templateConnectionString);
        databaseName = $"{template.Database}_security_{Guid.NewGuid():N}";
        adminConnectionString = new NpgsqlConnectionStringBuilder(templateConnectionString) { Database = "postgres", Pooling = false }.ConnectionString;
        connectionString = new NpgsqlConnectionStringBuilder(templateConnectionString) { Database = databaseName }.ConnectionString;

        await ExecuteAdmin($"""CREATE DATABASE "{databaseName}" TEMPLATE "{template.Database}" """);

        factory = new Factory(connectionString);
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
        });

        await Seed();

        // Signing in is limited per client address and the sweeps also hit the sign-in endpoints, so the tests sign in once up front
        foreach (var user in new[] { ClubA.Admin, ClubB.Admin, MemberOfA })
            cookies[user.UserId] = await SignIn(user);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdmin($"""DROP DATABASE IF EXISTS "{databaseName}" WITH (FORCE)""");
    }

    // ---------- database ----------

    /// <summary>A context without tenant filter and without user, for seeding and for looking at what the app did.</summary>
    public AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options, null, null, null);

    public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var context = CreateDbContext();
        return await action(context);
    }

    public Task Db(Func<AppDbContext, Task> action) => Db(async context =>
    {
        await action(context);
        return 0;
    });

    public async Task<T> Scalar<T>(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// A fingerprint of everything a club owns: the content of every row with its <c>ClubId</c> (all tables that have one), the club itself
    /// and its users. API keys are left out because using a key updates its last use.
    /// </summary>
    public async Task<string> Fingerprint(SeededClub club)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var command = new NpgsqlCommand(
                         """SELECT table_name FROM information_schema.columns WHERE table_schema = 'public' AND column_name = 'ClubId' AND table_name <> 'ClubApiKeys' ORDER BY table_name""",
                         connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        tables.Count.Should().BeGreaterThan(10, "the fingerprint has to cover the tenant tables");

        var result = new StringBuilder();
        foreach (var table in tables)
            result.Append(table).Append('=').Append(await Hash($"""SELECT t::text AS row FROM "{table}" t WHERE "ClubId" = {club.Id}""")).Append(';');

        result.Append("Clubs=").Append(await Hash($"""SELECT t::text AS row FROM "Clubs" t WHERE "Id" = {club.Id}""")).Append(';');
        result.Append("Users=").Append(await Hash($"""SELECT t::text AS row FROM "AspNetUsers" t WHERE "Id" IN ({string.Join(',', club.UserIds)})""")).Append(';');
        return result.ToString();

        async Task<string> Hash(string rows)
        {
            await using var command = new NpgsqlCommand($"SELECT coalesce(md5(string_agg(row, '|' ORDER BY row)), 'empty') || ':' || count(*) FROM ({rows}) x", connection);
            return (string)(await command.ExecuteScalarAsync())!;
        }
    }

    private async Task ExecuteAdmin(string sql)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // ---------- keys ----------

    /// <summary>Stores a new key directly (not limited to 20 keys per club like the app is) and returns the key.</summary>
    public async Task<SeededKey> NewKey(SeededClub club, int? userId = null, bool isReadOnly = false, DateTimeOffset? expiresAt = null)
    {
        var (key, token) = ClubApiKey.Create(club.Id, userId ?? club.Admin.UserId, $"Security test {Guid.NewGuid():N}", isReadOnly, expiresAt);
        await Db(async context =>
        {
            context.ClubApiKeys.Add(key);
            await context.SaveChangesAsync();
        });
        return new SeededKey(key.Id, token, club.Id, key.CreatedByUserId);
    }

    /// <summary>A new club with the usual resources; <paramref name="sharesPlayers"/> makes it an ATP club.</summary>
    public Task<SeededClub> NewClub(string text, bool sharesPlayers)
        => Db(context => AddClub(context, text, administrator: false, sharesPlayers));

    /// <summary>A new admin of the club, for tests that take the admin role away again.</summary>
    public Task<SeededUser> NewAdmin(SeededClub club)
        => Db(context => AddUser(context, club, $"temp-admin-{Guid.NewGuid():N}", "Temp", [MemberRole.User, MemberRole.Admin]));

    // ---------- requests ----------

    public async Task<ApiResponse> Send(HttpMethod method, string url, string? authorization = null, string? body = null, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (authorization is not null)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        configure?.Invoke(request);

        using var response = await Client.SendAsync(request);
        return await ApiResponse.From(response);
    }

    /// <summary>Calls an endpoint with an empty JSON body, or with a small file where the endpoint takes an upload.</summary>
    public Task<ApiResponse> Send(ApiEndpoint endpoint, string url, SeededKey? key)
        => Send(new HttpMethod(endpoint.Method), url, key?.Bearer, endpoint.HasBody && !endpoint.TakesUpload ? "{}" : null, request =>
        {
            if (!endpoint.TakesUpload)
                return;

            var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            request.Content = new MultipartFormDataContent { { file, "file", "attack.png" } };
        });

    /// <summary>The cookie header of a user who signed in with email and password like in the app.</summary>
    public Task<string> Login(SeededUser user) => Task.FromResult(cookies[user.UserId]);

    private async Task<string> SignIn(SeededUser user)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Account/Login");
        request.Content = new StringContent($$"""{"email":"{{user.Email}}","password":"{{Password}}","rememberMe":false}""", Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the seeded user must be able to sign in");

        var cookies = response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]);
        return string.Join("; ", cookies);
    }

    // ---------- seed ----------

    private async Task Seed()
    {
        await using var context = CreateDbContext();

        ClubA = await AddClub(context, Marker, administrator: true);
        ClubB = await AddClub(context, Canary, administrator: false);
        MemberOfA = await AddUser(context, ClubA, "member-a", Marker, [MemberRole.User]);

        AdminOfAWhoPlaysInB = await AddUser(context, ClubA, "admin-in-both", Marker, [MemberRole.User, MemberRole.Admin]);
        var memberInB = new ClubMember(AdminOfAWhoPlaysInB.UserId, ClubB.Id, [MemberRole.User]);
        context.Add(memberInB);
        await context.SaveChangesAsync();

        var start = new DateTimeOffset(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(6).ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero);
        context.Add(new Booking(ClubB.Id, ClubB.CourtId, ClubB.PlayModeId, new DateTimeOffsetInterval(start, start.AddHours(1)), TimeZoneInfo.Utc.Id, [memberInB.Id, ClubB.Member.MemberId], $"Comment {Canary}"));
        await context.SaveChangesAsync();
    }

    private static async Task<SeededClub> AddClub(AppDbContext context, string text, bool administrator, bool sharesPlayers = false)
    {
        var club = new Club(
            $"Club {text}",
            new TimeOnlyInterval(new TimeOnly(7, 0), new TimeOnly(22, 0)),
            new TimeOnlyInterval(new TimeOnly(18, 0), new TimeOnly(20, 0)),
            [new Club.PlayModeDto([MemberRole.User], System.Drawing.Color.Brown, 2, true, $"Single {text}", TimeSpan.FromHours(1), false, true)]);
        club.SetIsAtpClub(sharesPlayers);
        context.Add(club);
        await context.SaveChangesAsync();

        var seeded = new SeededClub { Id = club.Id, PlayModeId = club.PlayModes[0].Id };

        // The admin of club A is also an administrator of the whole installation: the strongest account a key can be created by
        seeded.Admin = await AddUser(context, seeded, $"admin-{text}", text, [MemberRole.User, MemberRole.Admin], administrator);
        seeded.Member = await AddUser(context, seeded, $"player-{text}", text, [MemberRole.User]);

        var court = new Court(club.Id, $"Court {text}", "C1");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var season = new Season(club.Id, new DateOnlyInterval(today.AddDays(-30), today.AddDays(300)));
        context.AddRange(court, season);
        await context.SaveChangesAsync();
        seeded.CourtId = court.Id;
        seeded.SeasonId = season.Id;

        var start = new DateTimeOffset(today.AddDays(5).ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        var booking = new Booking(club.Id, court.Id, seeded.PlayModeId, new DateTimeOffsetInterval(start, start.AddHours(1)), TimeZoneInfo.Utc.Id, [seeded.Admin.MemberId, seeded.Member.MemberId], $"Comment {text}");
        var clubEvent = new ClubEvent(club.Id, seeded.Admin.MemberId, new ClubEvent.EventData(
            $"Event {text}", $"Description {text}", $"Location {text}", ClubEventCategory.Social, today.AddDays(7), null, null, null, false, null, null, [], NotifyMembers: false));
        var announcement = new ClubAnnouncement(club.Id, seeded.Admin.MemberId, new ClubAnnouncement.AnnouncementData($"News {text}", $"Body {text}", false, null), DateTimeOffset.UtcNow);
        var blocking = new CourtBlocking(club.Id, new CourtBlocking.BlockingData(
            $"Blocking {text}", [court.Id], today.AddDays(9), today.AddDays(9), new TimeOnly(10, 0), new TimeOnly(14, 0), TimeZoneInfo.Utc.Id, null, null));
        context.AddRange(booking, clubEvent, announcement, blocking);
        await context.SaveChangesAsync();

        seeded.BookingId = booking.Id;
        seeded.ClubEventId = clubEvent.Id;
        seeded.ClubAnnouncementId = announcement.Id;
        seeded.CourtBlockingId = blocking.Id;
        return seeded;
    }

    private static async Task<SeededUser> AddUser(AppDbContext context, SeededClub club, string name, string text, MemberRole[] roles, bool administrator = false)
    {
        var email = $"{name}-{club.Id}@security.test".ToLowerInvariant();
        var user = new User(email, email, $"First{text}", $"Last{text}", new DateOnly(1990, 1, 1), Gender.Female, $"Street {text}", $"City {text}", "1010")
        {
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
        context.Add(user);
        await context.SaveChangesAsync();

        context.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = (int)UserRoles.User });
        if (administrator)
            context.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = (int)UserRoles.Administrator });

        var member = new ClubMember(user.Id, club.Id, roles);
        context.Add(member);
        await context.SaveChangesAsync();

        club.UserIds.Add(user.Id);
        return new SeededUser(user.Id, member.Id, email);
    }

    // ---------- host ----------

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Production: what is only switched on for development (Swagger UI, the document with all endpoints) must not be reachable
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionString", connectionString);
            builder.UseSetting("AppUrl", "https://localhost");
            builder.UseSetting("Email:SenderAddress", "noreply@security.test");
            builder.UseSetting("Email:SenderName", "Security tests");
            builder.UseSetting("Email:SmtpServer", "localhost");
            builder.UseSetting("Email:SmtpPort", "1");

            builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, ProbeStartupFilter>());
        }
    }

    /// <summary>
    /// Reports with every response who the request ran as, after the whole pipeline has decided it. This lets the tests check the
    /// identity itself and not only infer it from status codes.
    /// </summary>
    private sealed class ProbeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Response.OnStarting(() =>
                {
                    var user = context.User;
                    context.Response.Headers[ApiResponse.KeyHeader] = user.GetClubApiKeyId() ?? "";
                    context.Response.Headers[ApiResponse.UserHeader] = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";
                    context.Response.Headers[ApiResponse.RolesHeader] = string.Join(',', user.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value));
                    return Task.CompletedTask;
                });

                // Like the web server: an error nobody handled is answered with 500 (the test server would throw it at the test instead)
                try
                {
                    await nextMiddleware();
                }
                catch (Exception) when (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                }
            });
            next(app);
        };
    }
}

public sealed class SeededClub
{
    public int Id { get; init; }
    public int PlayModeId { get; init; }
    public SeededUser Admin { get; set; } = null!;
    public SeededUser Member { get; set; } = null!;
    public int CourtId { get; set; }
    public int SeasonId { get; set; }
    public int BookingId { get; set; }
    public int ClubEventId { get; set; }
    public int ClubAnnouncementId { get; set; }
    public int CourtBlockingId { get; set; }
    public List<int> UserIds { get; } = [];
}

public sealed record SeededUser(int UserId, int MemberId, string Email);

public sealed record SeededKey(int Id, string Token, int ClubId, int UserId)
{
    public string Bearer => $"Bearer {Token}";
}

/// <summary>An answer of the app together with what the probe reported about the identity of the request.</summary>
public sealed record ApiResponse(HttpStatusCode Status, string Body, string? KeyId, string? UserId, string Roles, HttpResponseHeaders Headers)
{
    public const string KeyHeader = "X-Probe-Club-Api-Key";
    public const string UserHeader = "X-Probe-User";
    public const string RolesHeader = "X-Probe-Roles";

    /// <summary>The request ran with the identity of an API key.</summary>
    public bool RanAsKey => !string.IsNullOrEmpty(KeyId);

    public static async Task<ApiResponse> From(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return new ApiResponse(response.StatusCode, body, Header(KeyHeader), Header(UserHeader), Header(RolesHeader) ?? "", response.Headers);

        string? Header(string name) => response.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{(int)Status} (key '{KeyId}', user '{UserId}', roles '{Roles}')");
}
