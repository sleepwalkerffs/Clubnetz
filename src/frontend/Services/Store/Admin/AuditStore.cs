using Bookennis.Client.Services.HttpClients.Admin;
using Bookennis.Client.Services.HttpClients;
using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Admin.Audit;

namespace Bookennis.Client.Services.Store.Admin;

public class AuditStore(IAuditHttpClient auditHttpClient) : SemaphoreStore, IAuditStore
{
    private AuditEventFilter? filter;
    private string? nextCursor;

    public event Action? OnEventsChanged;

    public List<AuditEventListItem> Events { get; private set; } = [];
    public bool EventsLoaded { get; private set; }
    public bool HasMore => nextCursor is not null;
    public AuditFilterOptions? Options { get; private set; }

    public Task LoadOptions()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await auditHttpClient.GetOptions(cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Options = result.Dto;
                OnEventsChanged?.Invoke();
            }
        }, nameof(LoadOptions));

    public Task LoadEvents(AuditEventFilter newFilter)
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            filter = newFilter;
            newFilter.Cursor = null;
            EventsLoaded = false;
            var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
            var countsTask = auditHttpClient.GetCounts(newFilter, offset, cancellationToken);
            var result = await auditHttpClient.GetEvents(newFilter, cancellationToken);
            var counts = await countsTask;
            Counts = counts is { Success: true } ? counts.Dto : null;
            if (result is { Success: true, Dto: not null })
            {
                Events = result.Dto.Items;
                nextCursor = result.Dto.NextCursor;
                EventsLoaded = true;
                OnEventsChanged?.Invoke();
            }
        }, nameof(LoadEvents));

    public Task LoadMore()
        => RunInLoadingContextAsync(async cancellationToken =>
        {
            if (filter is null || nextCursor is null)
                return;

            filter.Cursor = nextCursor;
            var result = await auditHttpClient.GetEvents(filter, cancellationToken);
            if (result is { Success: true, Dto: not null })
            {
                Events = [.. Events, .. result.Dto.Items];
                nextCursor = result.Dto.NextCursor;
                OnEventsChanged?.Invoke();
            }
        }, nameof(LoadMore));

    public AuditEventCounts? Counts { get; private set; }

    public Task<HttpResult<FileDownload>> Export(AuditEventFilter exportFilter)
        => auditHttpClient.Export(exportFilter);

    public async Task<AuditEventDetail?> LoadEvent(long id)
    {
        AuditEventDetail? detail = null;
        await RunInLoadingContextAsync(async cancellationToken =>
        {
            var result = await auditHttpClient.GetEvent(id, cancellationToken);
            if (result is { Success: true, Dto: not null })
                detail = result.Dto;
        }, nameof(LoadEvent));
        return detail;
    }
}
