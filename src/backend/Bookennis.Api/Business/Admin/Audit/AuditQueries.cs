using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Shared.Controller.Admin.Audit;
using Fusonic.Extensions.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin.Audit;

/// <summary>
/// The entries of the audit log for the explorer, newest first, one page at a time. Keyset paging on (<c>OccurredAt</c>, <c>Id</c>) instead of
/// page numbers, so a page stays fast however big the table gets; there is no total count for the same reason.
/// </summary>
public record GetAuditEvents(AuditEventFilter Filter) : IQuery<AuditEventPage>
{
    public class Handler(AppDbContext context) : IRequestHandler<GetAuditEvents, AuditEventPage>
    {
        public async Task<AuditEventPage> Handle(GetAuditEvents request, CancellationToken cancellationToken)
        {
            var filter = request.Filter;
            var pageSize = Math.Clamp(filter.PageSize, 1, AuditEventFilter.MaxPageSize);

            var query = AuditQueryHelpers.Apply(context.AuditEvents.AsNoTracking(), filter);

            if (AuditCursor.TryParse(filter.Cursor, out var at, out var id))
                query = query.Where(e => e.OccurredAt < at || (e.OccurredAt == at && e.Id < id));

            var events = await query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
                                    .Take(pageSize + 1)
                                    .Select(AuditQueryHelpers.ToListItem)
                                    .ToListAsync(cancellationToken);

            var hasMore = events.Count > pageSize;
            if (hasMore)
                events.RemoveAt(events.Count - 1);

            return new AuditEventPage
            {
                Items = await AuditQueryHelpers.WithClubNames(context, events, cancellationToken),
                NextCursor = hasMore ? AuditCursor.Format(events[^1].OccurredAt, events[^1].Id) : null
            };
        }
    }
}

/// <summary>One entry with the changed fields and everything else that happened in the same request or job.</summary>
public record GetAuditEvent(long Id) : IQuery<AuditEventDetail>
{
    private const int MaxRelated = 200;

    public class Handler(AppDbContext context) : IRequestHandler<GetAuditEvent, AuditEventDetail>
    {
        public async Task<AuditEventDetail> Handle(GetAuditEvent request, CancellationToken cancellationToken)
        {
            var e = await context.AuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                    ?? throw new EntityNotFoundException(typeof(AuditEvent), request.Id);

            var related = await context.AuditEvents.AsNoTracking()
                                       .Where(x => x.CorrelationId == e.CorrelationId && x.Id != e.Id)
                                       .OrderBy(x => x.Id)
                                       .Take(MaxRelated)
                                       .Select(AuditQueryHelpers.ToListItem)
                                       .ToListAsync(cancellationToken);

            var items = await AuditQueryHelpers.WithClubNames(context, [AuditQueryHelpers.ToListItem.Compile()(e), .. related], cancellationToken);

            return new AuditEventDetail
            {
                Event = items[0],
                ActorEmail = e.ActorEmail,
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                DurationMs = e.DurationMs,
                Changes = AuditQueryHelpers.ParseChanges(e.Changes),
                Related = items.Skip(1).ToList()
            };
        }
    }
}

/// <summary>What the filters of the explorer offer: the clubs, the entity types of the model and the actions of the app. Taken from the code, not the table.</summary>
public record GetAuditFilterOptions : IQuery<AuditFilterOptions>
{
    private static readonly string[] DataActions = [AuditChangeCollector.Created, AuditChangeCollector.Modified, AuditChangeCollector.Deleted, AccessDeniedAudit.Action];

    public class Handler(AppDbContext context) : IRequestHandler<GetAuditFilterOptions, AuditFilterOptions>
    {
        public async Task<AuditFilterOptions> Handle(GetAuditFilterOptions request, CancellationToken cancellationToken)
        {
            var entityTypes = context.Model.GetEntityTypes()
                                     .Where(t => !t.IsOwned() && t.ClrType != typeof(AuditEvent))
                                     .Select(t => t.ShortName())
                                     .Distinct()
                                     .Order(StringComparer.Ordinal)
                                     .ToList();

            var clubs = await context.Clubs.IgnoreQueryFilters()
                                     .OrderBy(c => c.Name)
                                     .Select(c => new AuditClubOption(c.Id, c.Name))
                                     .ToListAsync(cancellationToken);

            return new AuditFilterOptions
            {
                EntityTypes = entityTypes,
                Actions = [.. DataActions, .. AuditRequestKinds.LoggedActions.Except(DataActions)],
                Clubs = clubs
            };
        }
    }
}

public static class AuditQueryHelpers
{
    public static IQueryable<AuditEvent> Apply(IQueryable<AuditEvent> query, AuditEventFilter filter)
    {
        if (filter.From is { } from)
            query = query.Where(e => e.OccurredAt >= DateTime.SpecifyKind(from, DateTimeKind.Utc));
        if (filter.To is { } to)
            query = query.Where(e => e.OccurredAt < DateTime.SpecifyKind(to, DateTimeKind.Utc));
        if (filter.ClubId is { } clubId)
            query = query.Where(e => e.ClubId == clubId);
        if (filter.UserId is { } userId)
            query = query.Where(e => e.ActorUserId == userId || e.SubjectUserId == userId || e.ImpersonatorUserId == userId);
        if (filter.Kinds is { Count: > 0 } kinds)
            query = query.Where(e => kinds.Contains(e.Kind));
        if (filter.Outcomes is { Count: > 0 } outcomes)
            query = query.Where(e => outcomes.Contains(e.Outcome));
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(e => e.EntityType == filter.EntityType);
        if (!string.IsNullOrWhiteSpace(filter.EntityId))
            query = query.Where(e => e.EntityId == filter.EntityId.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(e => e.Action == filter.Action);
        if (filter.ImpersonatedOnly)
            query = query.Where(e => e.ImpersonatorUserId != null);
        if (filter.ApiKeyOnly)
            query = query.Where(e => e.ApiKeyId != null);
        if (filter.CorrelationId is { } correlationId)
            query = query.Where(e => e.CorrelationId == correlationId);
        if (QueryUtil.Escape(filter.Search) is { } search)
            query = query.Where(e => EF.Functions.ILike(e.SearchText, search, QueryUtil.EscapeCharacter));

        return query;
    }

    public static readonly Expression<Func<AuditEvent, AuditEventListItem>> ToListItem = e => new AuditEventListItem
    {
        Id = e.Id,
        OccurredAt = e.OccurredAt,
        CorrelationId = e.CorrelationId,
        Kind = e.Kind,
        Action = e.Action,
        Outcome = e.Outcome,
        ErrorCode = e.ErrorCode,
        ClubId = e.ClubId,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        SubjectUserId = e.SubjectUserId,
        SubjectName = e.SubjectName,
        ActorUserId = e.ActorUserId,
        ActorName = e.ActorName,
        ImpersonatorUserId = e.ImpersonatorUserId,
        ImpersonatorName = e.ImpersonatorName,
        ApiKeyId = e.ApiKeyId,
        System = e.System,
        Request = e.Request,
        Summary = e.Summary
    };

    /// <summary>The names of the clubs on the page. A deleted club has none, the explorer shows its id.</summary>
    public static async Task<List<AuditEventListItem>> WithClubNames(AppDbContext context, List<AuditEventListItem> items, CancellationToken cancellationToken)
    {
        var ids = items.Where(i => i.ClubId != null).Select(i => i.ClubId!.Value).Distinct().ToList();
        if (ids.Count == 0)
            return items;

        var names = await context.Clubs.IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return items.Select(i => i.ClubId is { } id && names.TryGetValue(id, out var name) ? i with { ClubName = name } : i).ToList();
    }

    public static List<AuditFieldChange> ParseChanges(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];

        using var document = JsonDocument.Parse(json);
        var result = new List<AuditFieldChange>();
        foreach (var field in document.RootElement.EnumerateObject())
        {
            var hasOld = field.Value.TryGetProperty("old", out var old);
            var hasNew = field.Value.TryGetProperty("new", out var @new);
            result.Add(new AuditFieldChange(field.Name, hasOld ? Text(old) : null, hasNew ? Text(@new) : null, hasOld, hasNew));
        }

        return result.OrderBy(c => c.Field, StringComparer.Ordinal).ToList();

        static string? Text(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => value.GetRawText()
        };
    }
}

/// <summary>The position after the last entry of a page: <c>{ticks}_{id}</c>.</summary>
public static class AuditCursor
{
    public static string Format(DateTime occurredAt, long id) => string.Create(CultureInfo.InvariantCulture, $"{occurredAt.Ticks}_{id}");

    public static bool TryParse(string? cursor, out DateTime occurredAt, out long id)
    {
        occurredAt = default;
        id = 0;
        var parts = cursor?.Split('_');
        if (parts is not { Length: 2 }
            || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks)
            || !long.TryParse(parts[1], CultureInfo.InvariantCulture, out id)
            || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            return false;

        occurredAt = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }
}
