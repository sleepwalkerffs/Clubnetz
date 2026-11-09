using System.Globalization;
using System.Text.Json;
using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.AspNetCore.WebUtilities;

namespace Bookennis.Client.Services.HttpClients.Admin;

public class AuditHttpClient(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IAuditHttpClient
{
    public async Task<HttpResult<AuditEventPage>> GetEvents(AuditEventFilter filter, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(QueryHelpers.AddQueryString("Events", ToQuery(filter)), cancellationToken)).AsHttpResult<AuditEventPage>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AuditEventCounts>> GetCounts(AuditEventFilter filter, int utcOffsetMinutes, CancellationToken cancellationToken = default)
    {
        var query = ToQuery(filter);
        query.Add(new("utcOffsetMinutes", utcOffsetMinutes.ToString(CultureInfo.InvariantCulture)));
        return await (await httpClient.GetAsync(QueryHelpers.AddQueryString("Events/Counts", query), cancellationToken)).AsHttpResult<AuditEventCounts>(jsonOptions, cancellationToken);
    }

    public async Task<HttpResult<FileDownload>> Export(AuditEventFilter filter, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(QueryHelpers.AddQueryString("Events/Export", ToQuery(filter)), cancellationToken)).AsFileHttpResult(jsonOptions, cancellationToken);

    public async Task<HttpResult<AuditEventDetail>> GetEvent(long id, CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync(string.Create(CultureInfo.InvariantCulture, $"Events/{id}"), cancellationToken)).AsHttpResult<AuditEventDetail>(jsonOptions, cancellationToken);

    public async Task<HttpResult<AuditFilterOptions>> GetOptions(CancellationToken cancellationToken = default)
        => await (await httpClient.GetAsync("Options", cancellationToken)).AsHttpResult<AuditFilterOptions>(jsonOptions, cancellationToken);

    private static List<KeyValuePair<string, string?>> ToQuery(AuditEventFilter filter)
    {
        var query = new List<KeyValuePair<string, string?>>();

        Add(nameof(filter.From), filter.From?.ToString("O", CultureInfo.InvariantCulture));
        Add(nameof(filter.To), filter.To?.ToString("O", CultureInfo.InvariantCulture));
        Add(nameof(filter.ClubId), filter.ClubId);
        Add(nameof(filter.UserId), filter.UserId);
        foreach (var kind in filter.Kinds ?? [])
            Add(nameof(filter.Kinds), kind);
        foreach (var outcome in filter.Outcomes ?? [])
            Add(nameof(filter.Outcomes), outcome);
        Add(nameof(filter.EntityType), filter.EntityType);
        Add(nameof(filter.EntityId), filter.EntityId);
        Add(nameof(filter.Action), filter.Action);
        if (filter.ImpersonatedOnly)
            Add(nameof(filter.ImpersonatedOnly), "true");
        if (filter.ApiKeyOnly)
            Add(nameof(filter.ApiKeyOnly), "true");
        Add(nameof(filter.Search), filter.Search);
        Add(nameof(filter.CorrelationId), filter.CorrelationId);
        Add(nameof(filter.Cursor), filter.Cursor);
        Add(nameof(filter.PageSize), filter.PageSize);
        return query;

        void Add(string key, object? value)
        {
            if (value is not null)
                query.Add(new(key, Convert.ToString(value, CultureInfo.InvariantCulture)));
        }
    }
}
