using Bookennis.Domain.Base;

namespace Bookennis.Domain.Memberships;

/// <summary>
/// An event Stripe reported by webhook. Stored so that an event that arrives twice is only handled once.
/// The payload is not stored: the object is loaded again from Stripe when the event is processed.
/// </summary>
public class StripeWebhookEvent : DomainEntity
{
    public const int MaxIdLength = 255;
    public const int MaxTypeLength = 100;

#pragma warning disable CS8618
    private StripeWebhookEvent() { }
#pragma warning restore CS8618

    public StripeWebhookEvent(string eventId, string type, string? stripeAccountId, string objectId, DateTimeOffset receivedAt)
    {
        EventId = eventId;
        Type = type;
        StripeAccountId = stripeAccountId;
        ObjectId = objectId;
        ReceivedAt = receivedAt;
    }

    public string EventId { get; private set; }
    public string Type { get; private set; }

    /// <summary>The connected account the event belongs to; <c>null</c> for events of the platform itself.</summary>
    public string? StripeAccountId { get; private set; }

    /// <summary>The id of the object the event is about (Checkout Session, charge, dispute, account).</summary>
    public string ObjectId { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public void MarkProcessed(DateTimeOffset processedAt) => ProcessedAt = processedAt;
}
