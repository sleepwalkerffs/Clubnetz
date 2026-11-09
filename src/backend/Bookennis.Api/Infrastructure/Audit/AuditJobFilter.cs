using Hangfire.Server;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>Names the background job in the audit entries written while it runs (<see cref="AuditJobContext"/>).</summary>
public sealed class AuditJobFilter : IServerFilter
{
    public void OnPerforming(PerformingContext context) => AuditJobContext.Current = context.BackgroundJob.Job?.Type.Name ?? "Job";

    public void OnPerformed(PerformedContext context) => AuditJobContext.Current = null;
}
