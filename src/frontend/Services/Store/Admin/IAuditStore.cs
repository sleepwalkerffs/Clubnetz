using Bookennis.Client.Services.Store.Base;
using Bookennis.Shared.Controller.Admin.Audit;

namespace Bookennis.Client.Services.Store.Admin;

/// <summary>The audit log of the admin panel: the entries for the current filter, page by page, and one entry in detail.</summary>
public interface IAuditStore : ISemaphoreStore
{
    public event Action? OnEventsChanged;

    public List<AuditEventListItem> Events { get; }
    public bool EventsLoaded { get; }
    public bool HasMore { get; }
    public AuditFilterOptions? Options { get; }

    public Task LoadOptions();

    /// <summary>Loads the first page for the filter (replaces what was loaded).</summary>
    public Task LoadEvents(AuditEventFilter filter);

    /// <summary>Appends the next page of the last filter.</summary>
    public Task LoadMore();

    public Task<AuditEventDetail?> LoadEvent(long id);
}
