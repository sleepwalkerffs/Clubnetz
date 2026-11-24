using Bookennis.Api.Config;
using Fusonic.Extensions.Email;

namespace Bookennis.Api.Business.Demo;

/// <summary>
/// Every email of the app goes through <see cref="SendEmail"/>. In the public demo only demo addresses get one: whatever a visitor
/// types (an invoice to an address of their choice, an event guest, ...) never reaches a real person, even when the demo's SMTP server
/// were a real one by mistake. Outside the demo it does nothing.
/// </summary>
public class DemoEmailGuard(IRequestHandler<SendEmail, Unit> inner, AppSettings appSettings, ILogger<DemoEmailGuard> logger) : IRequestHandler<SendEmail, Unit>
{
    public async Task<Unit> Handle(SendEmail request, CancellationToken cancellationToken)
    {
        if (!appSettings.Demo.Enabled)
            return await inner.Handle(request, cancellationToken);

        if (!DemoAccounts.IsDemoEmail(request.Recipient))
        {
            logger.LogInformation("Demo: dropped an email to an address outside the demo");
            return Unit.Value;
        }

        return await inner.Handle(DemoAccounts.IsDemoEmail(request.BccRecipient) ? request : request with { BccRecipient = null }, cancellationToken);
    }
}
