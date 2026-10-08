using System.Collections.Concurrent;
using Bookennis.Client.Services.HttpClients;

namespace Bookennis.Client.Services.Store.Base;

public abstract class SemaphoreStore : ISemaphoreStore, IDisposable
{
    private readonly ConcurrentDictionary<string, ExecutionContext> savingExecutionContexts = new();
    private readonly ConcurrentDictionary<string, ExecutionContext> loadingExecutionContexts = new();

    public bool IsLoading => loadingExecutionContexts.Any(i => i.Value.IsExecuting);
    public bool IsSaving => savingExecutionContexts.Any(i => i.Value.IsExecuting);

    public async Task<bool> WaitUntilLoaded()
    {
        var tasks = new List<Task<bool>>();
        foreach (var savingExecutionContext in loadingExecutionContexts.Values)
        {
            tasks.Add(WaitForExecutionContext(savingExecutionContext));
        }

        var results = await Task.WhenAll(tasks);
        return results.All(i => i);
    }

    public async Task<bool> WaitUntilSaved()
    {
        var tasks = new List<Task<bool>>();
        foreach (var savingExecutionContext in savingExecutionContexts.Values)
        {
            tasks.Add(WaitForExecutionContext(savingExecutionContext));
        }

        var results = await Task.WhenAll(tasks);
        return results.All(i => i);
    }

    public virtual void Dispose()
    {
        foreach (var executionContext in loadingExecutionContexts.Values)
        {
            executionContext.Dispose();
        }

        foreach (var executionContext in savingExecutionContexts.Values)
        {
            executionContext.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    protected Task RunInLoadingContextAsync(Action action, string? contextName)
        => RunInLoadingContextAsync(_ =>
        {
            action();
            return Task.CompletedTask;
        }, contextName);

    protected async Task RunInLoadingContextAsync(Func<CancellationToken, Task> action, string? contextName)
    {
        var context = GetOrCreateLoadingContext(contextName ?? "default");
        await ExecuteSaveContext(context, action);
    }

    protected Task RunInSavingContextAsync(Action action, string? contextName)
        => RunInSavingContextAsync(_ =>
        {
            action();
            return Task.CompletedTask;
        }, contextName);

    protected async Task RunInSavingContextAsync(Func<CancellationToken, Task> action, string? contextName)
        => await RunInSavingContextAsync(async (cancellationToken)
            =>
            {
                await action(cancellationToken);
                return HttpResult.OkResult;
            }, contextName);

    protected async Task<HttpResult> RunInSavingContextAsync(Func<CancellationToken, Task<HttpResult>> action, string? contextName)
    {
        var context = GetOrCreateSavingContext(contextName ?? "default");
        return await ExecuteSaveContext(context, action);
    }

    private static async Task ExecuteSaveContext(ExecutionContext context, Func<CancellationToken, Task> action)
        => await ExecuteSaveContext(context, async (cancellationToken) =>
        {
            await action(cancellationToken);
            return HttpResult.OkResult;
        });

    private static async Task<HttpResult> ExecuteSaveContext(ExecutionContext context, Func<CancellationToken, Task<HttpResult>> action)
    {
        try
        {
            context.IsExecuting = true;
            await context.CancelAsync();
            await context.WaitAsync();
            return await action(context.GetCancellationToken());
        }
        finally
        {
            context.ResetCancellation();
            context.IsExecuting = false;
            context.Release();
        }
    }

    private ExecutionContext GetOrCreateLoadingContext(string context) => loadingExecutionContexts.GetOrAdd(context, new ExecutionContext());
    private ExecutionContext GetOrCreateSavingContext(string context) => savingExecutionContexts.GetOrAdd(context, new ExecutionContext());

    private static async Task<bool> WaitForExecutionContext(ExecutionContext context)
    {
        await context.WaitAsync();
        context.Release();
        return context.Executed;
    }

    private sealed class ExecutionContext : IDisposable
    {
        private readonly SemaphoreSlim semaphore = new(initialCount: 1);
        private CancellationTokenSource? tokenSource;
        public bool IsExecuting { get; set; }
        public bool Executed { get; set; }

        public CancellationToken GetCancellationToken()
        {
            tokenSource ??= new CancellationTokenSource();

            return tokenSource.Token;
        }

        public Task CancelAsync()
        {
            if (tokenSource is not null)
                return tokenSource.CancelAsync();

            return Task.CompletedTask;
        }

        public Task WaitAsync() => semaphore.WaitAsync();
        public void Release() => semaphore.Release();

        public void Dispose()
        {
            semaphore.Dispose();
            tokenSource?.Dispose();
        }

        public void ResetCancellation() => tokenSource = null;
    }
}