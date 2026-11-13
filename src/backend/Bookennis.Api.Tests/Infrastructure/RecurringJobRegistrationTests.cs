using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Infrastructure.Configuration.ContainerRegistrations;
using System.Text.RegularExpressions;
using FluentAssertions;
using SimpleInjector;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

/// <summary>
/// Hangfire creates the recurring jobs (scheduled in Program.cs) through the container, which does not create unregistered concrete types.
/// A job that is not registered only fails when Hangfire runs it, and the test fixture has its own registrations, so the app's are checked here.
/// </summary>
public partial class RecurringJobRegistrationTests
{
    [Theory]
    [InlineData(typeof(BookingReminderJob))]
    [InlineData(typeof(ClubEventReminderJob))]
    [InlineData(typeof(StripeEventRecoveryJob))]
    [InlineData(typeof(Bookennis.Api.Business.CourtSales.CourtPurchaseHoldReleaseJob))]
    [InlineData(typeof(Bookennis.Api.Business.Weather.WeatherRefreshJob))]
    [InlineData(typeof(Bookennis.Api.Business.Clubhouse.ClubhouseTabPaymentJob))]
    [InlineData(typeof(Bookennis.Api.Business.Accounting.AccountingAutomationJob))]
    [InlineData(typeof(Bookennis.Api.Infrastructure.Audit.AuditRetentionJob))]
    public void RecurringJob_IsRegisteredInTheContainer(Type jobType)
    {
        var container = new Container();
        container.AddServices();

        container.GetRegistration(jobType).Should().NotBeNull();
    }

    /// <summary>The list above was not kept up to date once (the automatic accounting entries never ran): every job scheduled in Program.cs counts.</summary>
    [Fact]
    public void EveryJobScheduledInProgram_IsRegisteredInTheContainer()
    {
        var program = File.ReadAllText(FindProgram());
        var jobNames = ScheduledJob().Matches(program).Select(m => m.Groups["type"].Value.Split('.').Last()).Distinct().ToList();
        var container = new Container();
        container.AddServices();

        jobNames.Should().NotBeEmpty();
        var missing = jobNames
            .Select(name => typeof(Program).Assembly.GetTypes().SingleOrDefault(t => t.Name == name) ?? throw new InvalidOperationException($"Job {name} not found"))
            .Where(type => container.GetRegistration(type) is null)
            .Select(type => type.Name);
        missing.Should().BeEmpty("Hangfire creates recurring jobs through the container: register them in ServicesConfiguration");
    }

    private static string FindProgram()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Bookennis.Api", "Program.cs");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Bookennis.Api/Program.cs not found above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"recurringJobs\.AddOrUpdate<(?<type>[\w.]+)>")]
    private static partial Regex ScheduledJob();
}
