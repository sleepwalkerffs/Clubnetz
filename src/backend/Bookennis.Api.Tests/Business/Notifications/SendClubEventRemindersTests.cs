using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Data;
using Bookennis.Api.Tests.Business.ClubEvents;
using Bookennis.Domain.ClubEvents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bookennis.Api.Tests.Business.Notifications;

public class SendClubEventRemindersTests(TestFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task EventStartsWithinADay_RemindsOnce()
    {
        var eventId = await SeedEvent(StartingIn(TimeSpan.FromHours(12)), backdate: true);

        var notifier = Substitute.For<IClubEventNotifier>();
        await Run(notifier);
        await Run(notifier);

        await notifier.Received(1).EventReminder(eventId, Arg.Any<CancellationToken>());
        await notifier.DidNotReceive().RegistrationDeadlineReminder(Arg.Any<int>(), Arg.Any<CancellationToken>());
        (await GetEvent(eventId)).ReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EventPlannedOnShortNotice_IsMarkedWithoutAReminder()
    {
        var eventId = await SeedEvent(StartingIn(TimeSpan.FromHours(12)), backdate: false);

        var notifier = Substitute.For<IClubEventNotifier>();
        await Run(notifier);

        await notifier.DidNotReceive().EventReminder(Arg.Any<int>(), Arg.Any<CancellationToken>());
        (await GetEvent(eventId)).ReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EventLaterOrAlreadyStarted_IsLeftAlone()
    {
        var laterId = await SeedEvent(StartingIn(TimeSpan.FromHours(30)), backdate: true);
        var startedId = await SeedEvent(StartingIn(TimeSpan.FromHours(-2)), backdate: true);

        var notifier = Substitute.For<IClubEventNotifier>();
        await Run(notifier);

        notifier.ReceivedCalls().Should().BeEmpty();
        (await GetEvent(laterId)).ReminderSentAt.Should().BeNull();
        (await GetEvent(startedId)).ReminderSentAt.Should().BeNull();
    }

    [Fact]
    public async Task RegistrationDeadlineWithinADay_RemindsOnce()
    {
        var eventId = await SeedEvent(ClubEventSeed.Data() with { RegistrationDeadline = DateTimeOffset.UtcNow.AddHours(12) }, backdate: true);

        var notifier = Substitute.For<IClubEventNotifier>();
        await Run(notifier);
        await Run(notifier);

        await notifier.Received(1).RegistrationDeadlineReminder(eventId, Arg.Any<CancellationToken>());
        await notifier.DidNotReceive().EventReminder(Arg.Any<int>(), Arg.Any<CancellationToken>());
        (await GetEvent(eventId)).DeadlineReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RegistrationDeadline_MembersNotNotifiedOrEventFullOrDeadlineLater_SendsNoReminder()
    {
        var soon = DateTimeOffset.UtcNow.AddHours(12);
        var silentId = await SeedEvent(ClubEventSeed.Data() with { RegistrationDeadline = soon, NotifyMembers = false }, backdate: true);
        var laterId = await SeedEvent(ClubEventSeed.Data() with { RegistrationDeadline = DateTimeOffset.UtcNow.AddDays(3) }, backdate: true);
        var fullId = await SeedEvent(ClubEventSeed.Data(maxParticipants: 1, withQuestions: false) with { RegistrationDeadline = soon }, backdate: true);
        await QueryAsync(async ctx =>
        {
            var full = await ctx.ClubEvents.Include(e => e.Registrations).SingleAsync(e => e.Id == fullId);
            full.Register(TestDataSeed.Member1Id, 1, [], null, DateTimeOffset.UtcNow);
            await ctx.SaveChangesAsync();
        });

        var notifier = Substitute.For<IClubEventNotifier>();
        await Run(notifier);

        notifier.ReceivedCalls().Should().BeEmpty();
        (await GetEvent(silentId)).DeadlineReminderSentAt.Should().NotBeNull();
        (await GetEvent(fullId)).DeadlineReminderSentAt.Should().NotBeNull();
        (await GetEvent(laterId)).DeadlineReminderSentAt.Should().BeNull();
    }

    /// <summary>An event that starts the given time from now, in the club-local time of the events.</summary>
    private static ClubEvent.EventData StartingIn(TimeSpan timeSpan)
    {
        var localStart = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow.Add(timeSpan), ClubEventInfo.TimeZone).DateTime;
        return ClubEventSeed.Data(DateOnly.FromDateTime(localStart)) with { StartTime = TimeOnly.FromDateTime(localStart), EndTime = null };
    }

    private Task<int> SeedEvent(ClubEvent.EventData data, bool backdate)
        => QueryAsync(async ctx =>
        {
            var clubEvent = await ClubEventSeed.SeedEvent(ctx, data);

            // Events are created "now"; moves the creation into the past so they count as planned in advance
            if (backdate)
                await ctx.Database.ExecuteSqlAsync($"""UPDATE "ClubEvents" SET "Metadata_Created" = NOW() - interval '30 days' WHERE "Id" = {clubEvent.Id}""");

            return clubEvent.Id;
        });

    private Task<ClubEvent> GetEvent(int eventId) => QueryAsync(ctx => ctx.ClubEvents.SingleAsync(e => e.Id == eventId));

    private Task Run(IClubEventNotifier notifier)
        => ScopedAsync(() => new SendClubEventReminders.Handler(GetInstance<AppDbContext>(), notifier)
            .Handle(new SendClubEventReminders(), CancellationToken.None));
}
