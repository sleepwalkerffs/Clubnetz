using System.Transactions;
using Bookennis.Api.Business.Clubhouse.SumUp;
using Bookennis.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bookennis.Api.Tests.Business.Clubhouse;

/// <summary>
/// <see cref="SumUpTokenRefresher"/> on the database of the test: the app's refresher connects with the connection string of the
/// installation, which is not the database of the test. Like the real one, it saves outside of the transaction of the command, so a
/// command that fails afterwards does not take it back.
/// </summary>
public class InlineSumUpTokenRefresher(AppDbContext context, ISumUpGateway gateway, SumUpTokenProtector protector) : ISumUpTokenRefresher
{
    public Task Refresh(int accountId, CancellationToken cancellationToken)
        => Outside(async own =>
        {
            await SumUpTokenRefresher.Lock(own, accountId, cancellationToken);
            await SumUpTokenRefresher.RefreshLocked(own, gateway, protector, NullLogger.Instance, accountId, cancellationToken);
        });

    public Task MarkNeedsReconnect(int accountId, CancellationToken cancellationToken)
        => Outside(own => SumUpTokenRefresher.MarkNeedsReconnectLocked(own, accountId, cancellationToken));

    private async Task Outside(Func<AppDbContext, Task> action)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var own = new AppDbContext(context.Database.GetConnectionString()!);
        await action(own);
        scope.Complete();
    }
}
