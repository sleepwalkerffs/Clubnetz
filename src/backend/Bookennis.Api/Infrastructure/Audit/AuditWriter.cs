using System.Transactions;
using Bookennis.Api.Data;
using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>
/// Writes audit entries that must not depend on the transaction of a command: a failed command rolls its transaction back, but the
/// entry that it failed (a wrong password, access denied) has to stay. Uses an own <see cref="AppDbContext"/> outside the ambient transaction.
/// </summary>
public interface IAuditWriter
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>Replaces name and email of a deleted user in all entries, and the values of the changed <c>User</c> rows. The id stays.</summary>
    public Task PseudonymizeUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes entries older than the retention period. Returns how many were deleted.</summary>
    public Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default);
}

public sealed class AuditWriter(DbContextOptions<AppDbContext> options) : IAuditWriter
{
    /// <summary>Data changes, commands and reads.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(3 * 365);

    /// <summary>Sign-ins, password and email changes, impersonation, access denied.</summary>
    public static readonly TimeSpan SecurityRetention = TimeSpan.FromDays(2 * 365);

    public const string DeletedUserName = "Deleted user";

    /// <summary>Set in a transaction, it lets the trigger of the table pass UPDATE and DELETE (see the migration <c>AddAuditLog</c>).</summary>
    private const string MaintenanceSql = "SET LOCAL clubnetz.audit_maintenance = 'on'";

    private const int DeleteBatchSize = 5000;

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        using var suppress = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var context = CreateContext();
        context.Add(auditEvent);
        await context.SaveChangesAsync(CancellationToken.None); // the entry is written even when the request was cancelled
    }

    public Task PseudonymizeUserAsync(int userId, CancellationToken cancellationToken = default) =>
        InMaintenance(async context =>
        {
            var key = userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await context.Set<AuditEvent>().Where(e => e.ActorUserId == userId)
                         .ExecuteUpdateAsync(s => s.SetProperty(e => e.ActorName, DeletedUserName).SetProperty(e => e.ActorEmail, (string?)null), cancellationToken);
            await context.Set<AuditEvent>().Where(e => e.SubjectUserId == userId)
                         .ExecuteUpdateAsync(s => s.SetProperty(e => e.SubjectName, DeletedUserName), cancellationToken);
            await context.Set<AuditEvent>().Where(e => e.ImpersonatorUserId == userId)
                         .ExecuteUpdateAsync(s => s.SetProperty(e => e.ImpersonatorName, DeletedUserName), cancellationToken);
            // The old values of the user row hold address, birthday and email: only which fields changed stays
            await context.Set<AuditEvent>().Where(e => e.EntityType == nameof(Domain.User.User) && e.EntityId == key)
                         .ExecuteUpdateAsync(s => s.SetProperty(e => e.Changes, (string?)null), cancellationToken);
            return 0;
        }, cancellationToken);

    public async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var before = now - Retention;
        var securityBefore = now - SecurityRetention;
        var total = 0;

        while (true)
        {
            var deleted = await InMaintenance(context =>
                context.Set<AuditEvent>()
                       .Where(e => e.Kind == AuditEventKind.Security ? e.OccurredAt < securityBefore : e.OccurredAt < before)
                       .OrderBy(e => e.Id)
                       .Take(DeleteBatchSize)
                       .ExecuteDeleteAsync(cancellationToken), cancellationToken);

            total += deleted;
            if (deleted < DeleteBatchSize)
                return total;
        }
    }

    private async Task<T> InMaintenance<T>(Func<AppDbContext, Task<T>> action, CancellationToken cancellationToken)
    {
        using var suppress = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(MaintenanceSql, cancellationToken);
        var result = await action(context);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private AppDbContext CreateContext() => new(options, null, null, null);
}
