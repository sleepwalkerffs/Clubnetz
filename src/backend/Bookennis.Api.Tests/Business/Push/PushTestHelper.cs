using System.Globalization;
using Bookennis.Api.Business.Push;
using Bookennis.Api.Config;
using Bookennis.Api.Data;
using Bookennis.Domain.User;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Tests.Business.Push;

internal static class PushTestHelper
{
    public const string Endpoint = "https://fcm.googleapis.com/fcm/send/device-1";

    public static AppSettings ConfiguredSettings(int reminderLeadMinutes = 120) => new()
    {
        Push = new PushSettings
        {
            PublicKey = "public",
            PrivateKey = "private",
            Subject = "mailto:test@clubnetz.app",
            BookingReminderLeadMinutes = reminderLeadMinutes
        }
    };

    public static async Task<int> AddSubscription(AppDbContext context, int userId, string endpoint = Endpoint)
    {
        var subscription = new PushSubscription(userId, endpoint, "p256dh", "auth");
        context.Add(subscription);
        await context.SaveChangesAsync();
        return subscription.Id;
    }

    /// <summary>Bookings are created "now"; moves the creation into the past so they count as booked in advance.</summary>
    public static Task BackdateBooking(AppDbContext context, int bookingId)
        => context.Database.ExecuteSqlAsync($"""UPDATE "Bookings" SET "Metadata_Created" = NOW() - interval '7 days' WHERE "Id" = {bookingId}""");
}

/// <summary>Records what would be sent instead of handing it to the background job.</summary>
internal sealed class RecordingPushNotificationService : IPushNotificationService
{
    public List<SentNotification> Sent { get; } = [];

    public Task Notify(IReadOnlyCollection<PushRecipient> recipients, Func<CultureInfo, PushNotification> createNotification, CancellationToken cancellationToken)
    {
        if (recipients.Count > 0)
        {
            Sent.Add(new SentNotification(
                recipients.Select(r => r.UserId).Order().ToList(),
                createNotification(new CultureInfo("en-AT")),
                createNotification(new CultureInfo("de-AT"))));
        }

        return Task.CompletedTask;
    }

    public sealed record SentNotification(List<int> UserIds, PushNotification English, PushNotification German);
}
