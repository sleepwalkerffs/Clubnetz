using Bookennis.Domain.User;

namespace Bookennis.Api.Business.Push;

/// <summary>
/// What the device shows. <c>Url</c> is the page of the app that opens on tap (relative),
/// notifications with the same <c>Tag</c> replace each other on the device.
/// </summary>
public record PushNotification(string Title, string Body, string Url, string? Tag = null);

/// <summary>A user that gets a notification, in their language.</summary>
public record PushRecipient(int UserId, Language Language);
