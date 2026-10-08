using Bookennis.Api.Business.ClubAnnouncements;
using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Tests.Business.Push;
using Bookennis.Api.Tests.TestUtils;
using Bookennis.Domain.ClubAnnouncements;
using Bookennis.Domain.Exceptions;
using Bookennis.Domain.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookennis.Api.Tests.Business.Notifications;

public class NotificationPreferencesTests(TestFixture fixture) : TestBase(fixture)
{
    private const string UserEmail = "user@bookennis.com";
    private const string AdminEmail = "admin@bookennis.com";

    [Fact]
    public async Task GetNotificationPreferences_NothingSaved_ReturnsDefaultsForEveryType()
    {
        var result = await Get(TestDataSeed.UserId);

        result.Preferences.Select(p => (int)p.Type).Should().BeEquivalentTo(Enum.GetValues<NotificationType>().Select(t => (int)t));
        result.Preferences.Should().OnlyContain(p => p.Push && p.Email);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_SavesChannelsPerTypeAndUser()
    {
        await Update(TestDataSeed.UserId, new(NotificationType.BookingReminder, Push: true, Email: false), new(NotificationType.ClubEventCreated, Push: false, Email: false));
        await Update(TestDataSeed.UserId, new UpdateNotificationPreferences.Preference(NotificationType.ClubEventCreated, Push: false, Email: true));

        var result = await Get(TestDataSeed.UserId);
        var byType = result.Preferences.ToDictionary(p => (NotificationType)(int)p.Type);

        byType[NotificationType.BookingReminder].Should().BeEquivalentTo(new { Push = true, Email = false });
        byType[NotificationType.ClubEventCreated].Should().BeEquivalentTo(new { Push = false, Email = true });
        byType[NotificationType.BookingAdded].Should().BeEquivalentTo(new { Push = true, Email = true });

        (await QueryAsync(ctx => ctx.NotificationPreferences.CountAsync())).Should().Be(2);
        (await Get(TestDataSeed.AdminId)).Preferences.Should().OnlyContain(p => p.Push && p.Email);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_UnknownType_ThrowsPreconditionException()
    {
        var act = () => Update(TestDataSeed.UserId, new UpdateNotificationPreferences.Preference((NotificationType)999, true, true));

        await act.Should().ThrowAsync<PreconditionException>();
    }

    [Fact]
    public async Task EmailRecipients_ForMembers_ChildWithoutEmailIsReachedViaParent_OneEmailPerAddress()
    {
        var childMemberId = await SeedChildOfUser();

        var childOnly = await QueryAsync(ctx => EmailRecipients.ForMembers(ctx, NotificationType.BookingAdded, [childMemberId], null, CancellationToken.None));
        var both = await QueryAsync(ctx => EmailRecipients.ForMembers(ctx, NotificationType.BookingAdded, [TestDataSeed.Member1Id, childMemberId], null, CancellationToken.None));

        var child = childOnly.Should().ContainSingle().Subject;
        child.Email.Should().Be(UserEmail);
        child.UserId.Should().Be(TestDataSeed.UserId);
        child.FirstName.Should().Be("Kid");

        // The parent is addressed, not the child that shares the address
        both.Should().ContainSingle().Which.MemberId.Should().Be(TestDataSeed.Member1Id);
    }

    [Fact]
    public async Task EmailRecipients_ForMembers_SkipsUsersWhoSwitchedTheEmailOff()
    {
        var childMemberId = await SeedChildOfUser();
        await Update(TestDataSeed.UserId, new UpdateNotificationPreferences.Preference(NotificationType.BookingAdded, Push: true, Email: false));

        int[] memberIds = [TestDataSeed.Member1Id, TestDataSeed.Member2Id, childMemberId];
        var bookingAdded = await QueryAsync(ctx => EmailRecipients.ForMembers(ctx, NotificationType.BookingAdded, memberIds, null, CancellationToken.None));
        var bookingDeleted = await QueryAsync(ctx => EmailRecipients.ForMembers(ctx, NotificationType.BookingDeleted, memberIds, null, CancellationToken.None));

        // The parent's preference also applies to the emails they would get for their child
        bookingAdded.Select(r => r.Email).Should().Equal(AdminEmail);
        bookingDeleted.Select(r => r.Email).Should().BeEquivalentTo([UserEmail, AdminEmail]);
    }

    [Fact]
    public async Task EmailRecipients_ForClub_ReturnsClubMembersExceptTheGivenUsers()
    {
        var all = await QueryAsync(ctx => EmailRecipients.ForClub(ctx, NotificationType.ClubEventCreated, TestDataSeed.ClubId, null, CancellationToken.None));
        var withoutAdmin = await QueryAsync(ctx => EmailRecipients.ForClub(ctx, NotificationType.ClubEventCreated, TestDataSeed.ClubId, [TestDataSeed.AdminId], CancellationToken.None));
        var otherClub = await QueryAsync(ctx => EmailRecipients.ForClub(ctx, NotificationType.ClubEventCreated, TestDataSeed.ClubId + 1, null, CancellationToken.None));

        all.Select(r => r.Email).Should().BeEquivalentTo([UserEmail, AdminEmail]);
        withoutAdmin.Select(r => r.Email).Should().Equal(UserEmail);
        otherClub.Should().BeEmpty();
    }

    [Fact]
    public async Task PushRecipients_SkipUsersWhoSwitchedThePushNotificationOff()
    {
        await QueryAsync(async ctx =>
        {
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.UserId, "https://fcm.googleapis.com/fcm/send/user");
            await PushTestHelper.AddSubscription(ctx, TestDataSeed.AdminId, "https://fcm.googleapis.com/fcm/send/admin");
        });
        await Update(TestDataSeed.UserId, new UpdateNotificationPreferences.Preference(NotificationType.BookingAdded, Push: false, Email: true));

        int[] memberIds = [TestDataSeed.Member1Id, TestDataSeed.Member2Id];
        var bookingAdded = await QueryAsync(ctx => PushRecipients.ForMembers(ctx, NotificationType.BookingAdded, memberIds, null, CancellationToken.None));
        var bookingDeleted = await QueryAsync(ctx => PushRecipients.ForMembers(ctx, NotificationType.BookingDeleted, memberIds, null, CancellationToken.None));
        var club = await QueryAsync(ctx => PushRecipients.ForClub(ctx, NotificationType.BookingAdded, TestDataSeed.ClubId, null, CancellationToken.None));
        var withoutType = await QueryAsync(ctx => PushRecipients.ForUsers(ctx, null, [TestDataSeed.UserId], null, CancellationToken.None));

        bookingAdded.Select(r => r.UserId).Should().Equal(TestDataSeed.AdminId);
        bookingDeleted.Select(r => r.UserId).Should().BeEquivalentTo([TestDataSeed.UserId, TestDataSeed.AdminId]);
        club.Select(r => r.UserId).Should().Equal(TestDataSeed.AdminId);
        withoutType.Select(r => r.UserId).Should().Equal(TestDataSeed.UserId);
    }

    [Fact]
    public async Task ClubAnnouncementRecipients_SkipUsersWhoSwitchedAnnouncementEmailsOff()
    {
        await Update(TestDataSeed.UserId, new UpdateNotificationPreferences.Preference(NotificationType.ClubAnnouncement, Push: true, Email: false));

        var recipients = await QueryAsync(ctx => ClubAnnouncementRecipients.Resolve(ctx, TestDataSeed.ClubId, ClubAnnouncementAudience.AllMembers, [], CancellationToken.None));

        recipients.Select(r => r.Email).Should().Equal(AdminEmail);
    }

    private async Task<int> SeedChildOfUser()
        => await QueryAsync(async ctx =>
        {
            var (_, childMemberId) = await MemberSeed.AddChild(ctx, TestDataSeed.UserId, "Kid");
            await MemberSeed.AddFamily(ctx, [TestDataSeed.Member1Id], [childMemberId]);
            return childMemberId;
        });

    private Task<Bookennis.Shared.Controller.Notifications.GetNotificationPreferencesResult> Get(int userId)
        => QueryAsync(ctx => new GetNotificationPreferences.Handler(ctx, IdentityTestHelper.CreateUserAccessor(userId))
            .Handle(new GetNotificationPreferences(), CancellationToken.None));

    private Task Update(int userId, params UpdateNotificationPreferences.Preference[] preferences)
        => QueryAsync(ctx => new UpdateNotificationPreferences.Handler(ctx, IdentityTestHelper.CreateUserAccessor(userId))
            .Handle(new UpdateNotificationPreferences(preferences), CancellationToken.None));
}
