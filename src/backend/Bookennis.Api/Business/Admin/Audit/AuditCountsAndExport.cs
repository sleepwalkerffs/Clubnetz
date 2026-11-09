using System.Globalization;
using System.Text;
using Bookennis.Api.Data;
using Bookennis.Api.Infrastructure.Audit;
using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.EntityFrameworkCore;

namespace Bookennis.Api.Business.Admin.Audit;

/// <summary>
/// How many entries match the filter over time, by result, for the chart of the explorer. The bucket size follows the time span
/// (5 minutes up to 2 hours, hours up to 2 days, days up to 120 days, months beyond); the buckets are in the local time of the viewer.
/// </summary>
/// <param name="Filter">The filter of the list (the cursor is ignored).</param>
/// <param name="UtcOffsetMinutes">The offset of the viewer's time zone (e.g. 60 for CET).</param>
public record GetAuditEventCounts(AuditEventFilter Filter, int UtcOffsetMinutes) : IQuery<AuditEventCounts>
{
    public const int MaxBuckets = 400;

    public class Handler(AppDbContext context) : IRequestHandler<GetAuditEventCounts, AuditEventCounts>
    {
        public async Task<AuditEventCounts> Handle(GetAuditEventCounts request, CancellationToken cancellationToken)
        {
            var offset = Math.Clamp(request.UtcOffsetMinutes, -14 * 60, 14 * 60);
            var query = AuditQueryHelpers.Apply(context.AuditEvents.AsNoTracking(), request.Filter);

            var to = request.Filter.To is { } filterTo ? DateTime.SpecifyKind(filterTo, DateTimeKind.Utc) : DateTime.UtcNow;
            var from = request.Filter.From is { } filterFrom
                ? DateTime.SpecifyKind(filterFrom, DateTimeKind.Utc)
                : await query.MinAsync(e => (DateTime?)e.OccurredAt, cancellationToken) ?? to;

            var span = to - from;
            var size = span <= TimeSpan.FromHours(2) ? AuditBucketSize.FiveMinutes
                : span <= TimeSpan.FromHours(48) ? AuditBucketSize.Hour
                : span <= TimeSpan.FromDays(120) ? AuditBucketSize.Day
                : AuditBucketSize.Month;

            var rows = await query
                .Select(e => new { At = e.OccurredAt.AddMinutes(offset), e.Outcome })
                .GroupBy(x => new
                {
                    x.At.Year,
                    x.At.Month,
                    Day = size == AuditBucketSize.Month ? 1 : x.At.Day,
                    Hour = size == AuditBucketSize.FiveMinutes || size == AuditBucketSize.Hour ? x.At.Hour : 0,
                    Minute = size == AuditBucketSize.FiveMinutes ? x.At.Minute / 5 * 5 : 0,
                    x.Outcome
                })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Minute, g.Key.Outcome, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var counts = rows.GroupBy(r => new DateTime(r.Year, r.Month, r.Day, r.Hour, r.Minute, 0, DateTimeKind.Unspecified))
                             .ToDictionary(g => g.Key, g => (
                                 Succeeded: g.Where(r => r.Outcome == AuditOutcome.Succeeded).Sum(r => r.Count),
                                 Failed: g.Where(r => r.Outcome == AuditOutcome.Failed).Sum(r => r.Count),
                                 Denied: g.Where(r => r.Outcome == AuditOutcome.Denied).Sum(r => r.Count)));

            // Every bucket from the start to the end, also the empty ones: a gap in the chart is information
            var buckets = new List<AuditCountBucket>();
            var end = to.AddMinutes(offset);
            for (var start = Floor(from.AddMinutes(offset), size); start <= end && buckets.Count < MaxBuckets; start = Next(start, size))
            {
                var (succeeded, failed, denied) = counts.GetValueOrDefault(start);
                buckets.Add(new AuditCountBucket(start, succeeded, failed, denied));
            }

            return new AuditEventCounts { BucketSize = size, Buckets = buckets };
        }

        private static DateTime Floor(DateTime at, AuditBucketSize size) => size switch
        {
            AuditBucketSize.FiveMinutes => new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute / 5 * 5, 0, DateTimeKind.Unspecified),
            AuditBucketSize.Hour => new DateTime(at.Year, at.Month, at.Day, at.Hour, 0, 0, DateTimeKind.Unspecified),
            AuditBucketSize.Day => new DateTime(at.Year, at.Month, at.Day, 0, 0, 0, DateTimeKind.Unspecified),
            _ => new DateTime(at.Year, at.Month, 1, 0, 0, 0, DateTimeKind.Unspecified)
        };

        private static DateTime Next(DateTime at, AuditBucketSize size) => size switch
        {
            AuditBucketSize.FiveMinutes => at.AddMinutes(5),
            AuditBucketSize.Hour => at.AddHours(1),
            AuditBucketSize.Day => at.AddDays(1),
            _ => at.AddMonths(1)
        };
    }
}

/// <summary>The entries of the filter as CSV (newest first, at most <see cref="MaxRows"/>). Logged itself: it hands out personal data.</summary>
[AuditedRead]
public record ExportAuditEvents(AuditEventFilter Filter) : IQuery<ExportAuditEventsResult>
{
    public const int MaxRows = 10_000;

    private static readonly string[] Header =
    [
        "OccurredAtUtc", "Kind", "Action", "Outcome", "ErrorCode", "ClubId", "Club", "EntityType", "EntityId", "ActorUserId", "Actor", "ActorEmail",
        "ImpersonatorUserId", "ImpersonatedBy", "ApiKeyId", "System", "SubjectUserId", "Subject", "IpAddress", "Request", "DurationMs", "Summary", "Changes", "CorrelationId"
    ];

    public class Handler(AppDbContext context) : IRequestHandler<ExportAuditEvents, ExportAuditEventsResult>
    {
        public async Task<ExportAuditEventsResult> Handle(ExportAuditEvents request, CancellationToken cancellationToken)
        {
            var events = await AuditQueryHelpers.Apply(context.AuditEvents.AsNoTracking(), request.Filter)
                                                .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
                                                .Take(MaxRows)
                                                .ToListAsync(cancellationToken);

            var clubIds = events.Where(e => e.ClubId != null).Select(e => e.ClubId!.Value).Distinct().ToList();
            var clubs = await context.Clubs.IgnoreQueryFilters().Where(c => clubIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', Header));
            foreach (var e in events)
            {
                object?[] values =
                [
                    e.OccurredAt.ToString("O", CultureInfo.InvariantCulture), e.Kind, e.Action, e.Outcome, e.ErrorCode, e.ClubId,
                    e.ClubId is { } clubId ? clubs.GetValueOrDefault(clubId) : null, e.EntityType, e.EntityId, e.ActorUserId, e.ActorName, e.ActorEmail,
                    e.ImpersonatorUserId, e.ImpersonatorName, e.ApiKeyId, e.System, e.SubjectUserId, e.SubjectName, e.IpAddress, e.Request, e.DurationMs,
                    e.Summary, e.Changes, e.CorrelationId
                ];
                csv.AppendLine(string.Join(',', values.Select(Field)));
            }

            var fileName = string.Create(CultureInfo.InvariantCulture, $"audit-log-{DateTime.UtcNow:yyyy-MM-dd-HHmm}.csv");
            // With a byte order mark, so Excel opens umlauts right
            return new ExportAuditEventsResult(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), fileName);
        }

        /// <summary>Quoted when needed. A leading = + - @ is defused, so a spreadsheet doesn't run a name or a summary as a formula.</summary>
        public static string Field(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                text = "'" + text;
            return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
        }
    }
}

public record ExportAuditEventsResult(byte[] Data, string FileName)
{
    public const string ContentType = "text/csv";
}
