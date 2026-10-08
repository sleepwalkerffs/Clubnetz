using Bookennis.Api.Business.Notifications;
using Bookennis.Api.Infrastructure.Authorization;
using Bookennis.Shared.Controller.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Controllers.Notifications;

/// <summary>On which channels (push, email) the current user is notified about what.</summary>
[Authorize(AuthorizationPolicies.ApplicationUser)]
public class NotificationPreferencesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<GetNotificationPreferencesResult> Get(CancellationToken cancellationToken)
        => mediator.Send(new GetNotificationPreferences(), cancellationToken);

    [HttpPut]
    public async Task Update(UpdateNotificationPreferencesModel model, CancellationToken cancellationToken)
        => await mediator.Send(
            new UpdateNotificationPreferences(model.Preferences
                .Select(p => new UpdateNotificationPreferences.Preference((Domain.Notifications.NotificationType)(int)p.Type, p.Push, p.Email))
                .ToList()),
            cancellationToken);
}
