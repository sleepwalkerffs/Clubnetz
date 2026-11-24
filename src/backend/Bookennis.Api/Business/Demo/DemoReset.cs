using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Demo;

/// <summary>
/// Wipes the database of the demo and fills it again (<see cref="DemoSeeder"/>): at startup and every night (<see cref="DemoResetJob"/>).
/// Only with <see cref="DemoSettings.Enabled"/>, and only as long as every user of the database has a demo address: a database with one
/// real user is never touched, whatever the settings say.
/// </summary>
public sealed class DemoReset(AppSettings appSettings, ILogger<DemoReset> logger)
{
    /// <summary>Tables that hold what the migrations put in and the app never changes.</summary>
    private static readonly string[] KeptTables = ["__EFMigrationsHistory", "AspNetRoles"];

    public async Task Run(CancellationToken cancellationToken)
    {
        if (!appSettings.Demo.Enabled)
            return;

        await using var context = CreateContext(appSettings.ConnectionString);
        await Run(context, DateTimeOffset.UtcNow, cancellationToken);
        logger.LogInformation("The demo data was reset");
    }

    /// <summary>The reset itself, in one transaction: visitors see the old data until the new one is complete.</summary>
    public static async Task Run(AppDbContext context, DateTimeOffset now, CancellationToken cancellationToken)
    {
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Nobody signs up or changes an account between the check and the wipe (and the wipe does not wait for a lock it can't get)
        await context.Database.ExecuteSqlRawAsync("LOCK TABLE \"AspNetUsers\" IN ACCESS EXCLUSIVE MODE", cancellationToken);
        await EnsureOnlyDemoData(context, cancellationToken);
        await Wipe(context, cancellationToken);
        await new DemoSeeder(context, now).Seed(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        DemoSignIn.MarkReset(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Empties the tables of the app (those of the EF model, nothing else that may live in the database). Never call it without
    /// <see cref="EnsureOnlyDemoData"/> first.
    /// </summary>
    public static async Task Wipe(AppDbContext context, CancellationToken cancellationToken)
    {
        var wiped = context.Model.GetEntityTypes()
            .Where(e => e.GetTableName() is { } table && !KeptTables.Contains(table) && e.GetViewName() is null)
            .Select(e => Quote(e.GetSchema() ?? "public") + "." + Quote(e.GetTableName()!))
            .Distinct()
            .ToList();
        if (wiped.Count > 0)
        {
            var truncate = "TRUNCATE TABLE " + string.Join(", ", wiped) + " CASCADE";
            await context.Database.ExecuteSqlRawAsync(truncate, cancellationToken);
        }

        context.ChangeTracker.Clear();
    }

    // The names come from the EF model, not from a user
    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>Throws when the database holds a user that is not a demo user: then it is not a demo database.</summary>
    public static async Task EnsureOnlyDemoData(AppDbContext context, CancellationToken cancellationToken)
    {
        // Only demo addresses, and children of demo users (no address of their own). Not the normalized email: it can be empty.
        var pattern = "%@" + DemoAccounts.EmailDomain;
        var realUsers = await context.Users.CountAsync(u => u.Email == null ? u.BelongsToUserId == null : !EF.Functions.ILike(u.Email, pattern), cancellationToken);
        if (realUsers > 0)
            throw new InvalidOperationException($"Demo mode is on, but the database holds {realUsers} users without a demo address. It is not a demo database, nothing was reset.");

        // A second key, for a database whose users are gone (a partial restore): members or bookings of clubs that are not the demo's
        var demoClubs = DemoContent.Clubs.Select(c => c.Name).ToList();
        var realClubs = await context.Clubs
            .Where(c => !demoClubs.Contains(c.Name))
            .CountAsync(c => context.ClubMembers.Any(m => m.ClubId == c.Id) || context.Bookings.Any(b => b.ClubId == c.Id), cancellationToken);
        if (realClubs > 0)
            throw new InvalidOperationException($"Demo mode is on, but the database holds {realClubs} clubs with members or bookings that are not demo clubs. It is not a demo database, nothing was reset.");
    }

    public static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).UseProjectables().Options;
        var context = new AppDbContext(options, null, null, null);
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        return context;
    }
}

/// <summary>The nightly reset of the demo, registered only when <see cref="DemoSettings.Enabled"/> is on.</summary>
public class DemoResetJob(DemoReset reset)
{
    public const string JobId = "demo-reset";

    /// <summary>Cron of <see cref="DemoSettings.ResetTime"/> ("HH:mm").</summary>
    public static string Schedule(DemoSettings settings)
    {
        var time = TimeOnly.TryParseExact(settings.ResetTime, "HH:mm", out var parsed) ? parsed : new TimeOnly(4, 0);
        return $"{time.Minute} {time.Hour} * * *";
    }

    public Task Run() => reset.Run(CancellationToken.None);
}
