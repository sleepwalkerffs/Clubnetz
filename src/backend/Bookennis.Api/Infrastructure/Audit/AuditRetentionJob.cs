namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>Deletes audit entries older than the retention period (<see cref="AuditWriter.Retention"/>, <see cref="AuditWriter.SecurityRetention"/>), once a night.</summary>
public class AuditRetentionJob(IAuditWriter auditWriter, ILogger<AuditRetentionJob> logger)
{
    public const string JobId = "audit-retention";
    public const string Schedule = "17 3 * * *";

    public async Task Run()
    {
        var deleted = await auditWriter.DeleteExpiredAsync(DateTime.UtcNow);
        if (deleted > 0)
            logger.LogInformation("Deleted {Count} expired audit entries", deleted);
    }
}
