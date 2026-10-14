using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Payments;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Infrastructure.Configuration.ContainerRegistrations;
using FluentAssertions;
using SimpleInjector;
using Xunit;

namespace Bookennis.Api.Tests.Infrastructure;

/// <summary>
/// Hangfire creates the recurring jobs (scheduled in Program.cs) through the container, which does not create unregistered concrete types.
/// A job that is not registered only fails when Hangfire runs it, and the test fixture has its own registrations, so the app's are checked here.
/// </summary>
public class RecurringJobRegistrationTests
{
    [Theory]
    [InlineData(typeof(BookingReminderJob))]
    [InlineData(typeof(ClubEventReminderJob))]
    [InlineData(typeof(StripeEventRecoveryJob))]
    public void RecurringJob_IsRegisteredInTheContainer(Type jobType)
    {
        var container = new Container();
        container.AddServices();

        container.GetRegistration(jobType).Should().NotBeNull();
    }
}
