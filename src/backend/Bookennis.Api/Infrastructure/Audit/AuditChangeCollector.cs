using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bookennis.Domain.Base;
using Bookennis.Domain.Memberships;
using Bookennis.Domain.User;
using Bookennis.Domain.Weather;
using Bookennis.Shared.Controller.Admin.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>
/// Turns the pending changes of a <see cref="DbContext"/> into audit entries, one per changed row, before it saves them
/// (<c>DbContextHelpers.WrapSaveChanges</c>), so they are written in the same transaction.
/// Owned types are folded into their owner (<c>PrimeTimeSettings.ChildAgeThreshold</c>), the <see cref="EntityMetadata"/> is left out.
/// </summary>
public static class AuditChangeCollector
{
    public const string Created = nameof(Created);
    public const string Modified = nameof(Modified);
    public const string Deleted = nameof(Deleted);

    /// <summary>Rows that are not worth an entry or hold secrets as a whole.</summary>
    private static readonly HashSet<Type> IgnoredTypes =
    [
        typeof(AuditEvent),
        typeof(WeatherForecast), // refreshed every 15 minutes
        typeof(StripeWebhookEvent), // the raw payload of Stripe, the effects are logged on their own
        typeof(PushSubscription), // keys of the browser
        typeof(IdentityUserToken<int>) // tokens
    ];

    /// <summary>Changes of these are logged, their values are not.</summary>
    private static readonly HashSet<string> RedactedProperties = ["PasswordHash", "SecurityStamp", "KeyHash", "AccessToken", "RefreshToken"];

    /// <summary>Noise: they change together with something that is logged anyway.</summary>
    private static readonly HashSet<string> IgnoredProperties = ["ConcurrencyStamp", "NormalizedEmail", "NormalizedUserName"];

    private const string RedactedValue = "***";
    private const int MaxStringLength = 1000;
    private const int MaxPropertiesInSummary = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 8
    };

    public static List<AuditEvent> Collect(DbContext context, IAuditContext auditContext)
    {
        var entries = context.ChangeTracker.Entries()
                             .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                             .ToList();
        if (entries.Count == 0)
            return [];

        var builders = new Dictionary<object, Builder>(ReferenceEqualityComparer.Instance);
        List<EntityEntry>? allEntries = null;

        foreach (var entry in entries)
        {
            var root = entry;
            var prefix = "";

            if (entry.Metadata.IsOwned())
            {
                if (entry.Metadata.ClrType == typeof(EntityMetadata))
                    continue;

                allEntries ??= context.ChangeTracker.Entries().ToList();
                (root, prefix) = ResolveOwner(entry, allEntries);
                if (root is null)
                    continue;
            }

            if (IsIgnored(root.Metadata.ClrType))
                continue;

            if (!builders.TryGetValue(root.Entity, out var builder))
                builders[root.Entity] = builder = new Builder(root);

            builder.AddChanges(entry, prefix);
        }

        var actor = auditContext.GetActor();
        var now = DateTime.UtcNow;
        var result = new List<AuditEvent>();

        foreach (var builder in builders.Values)
        {
            if (builder.Action == Modified && builder.Changes.Count == 0)
                continue;

            var e = new AuditEvent
            {
                OccurredAt = now,
                CorrelationId = auditContext.CorrelationId,
                Kind = AuditEventKind.DataChange,
                Action = builder.Action,
                Outcome = AuditOutcome.Succeeded,
                ClubId = builder.ClubId() ?? auditContext.GetClubId(),
                EntityType = builder.Root.Metadata.ShortName(),
                EntityId = builder.EntityId(),
                Summary = builder.Summary(),
                Changes = builder.ChangesJson()
            };
            (e.SubjectUserId, e.SubjectName) = builder.Subject();
            actor.ApplyTo(e);
            result.Add(e);
        }

        return result;
    }

    private static bool IsIgnored(Type type) => IgnoredTypes.Any(t => t.IsAssignableFrom(type));

    private static (EntityEntry? Root, string Prefix) ResolveOwner(EntityEntry entry, List<EntityEntry> allEntries)
    {
        var prefix = "";
        var current = entry;

        while (current.Metadata.IsOwned())
        {
            var ownership = current.Metadata.FindOwnership();
            if (ownership is null)
                return (null, "");

            var foreignKey = ownership.Properties.Select(p => Value(current, p)).ToArray();
            var owner = allEntries.FirstOrDefault(candidate =>
                candidate != current
                && ownership.PrincipalEntityType.IsAssignableFrom(candidate.Metadata)
                && ownership.PrincipalKey.Properties.Select(p => Value(candidate, p)).SequenceEqual(foreignKey));

            if (owner is null)
                return (null, "");

            var navigation = ownership.PrincipalToDependent?.Name ?? current.Metadata.ShortName();
            prefix = prefix.Length == 0 ? navigation : $"{navigation}.{prefix}";
            current = owner;
        }

        return (current, prefix);
    }

    private static object? Value(EntityEntry entry, IProperty property)
    {
        var p = entry.Property(property.Name);
        return entry.State == EntityState.Deleted ? p.OriginalValue : p.CurrentValue;
    }

    private sealed class Builder(EntityEntry root)
    {
        public EntityEntry Root { get; } = root;

        public string Action { get; } = root.State switch
        {
            EntityState.Added => Created,
            EntityState.Deleted => Deleted,
            _ => Modified
        };

        public SortedDictionary<string, Change> Changes { get; } = new(StringComparer.Ordinal);

        public void AddChanges(EntityEntry entry, string prefix)
        {
            var key = entry.Metadata.FindPrimaryKey();

            foreach (var property in entry.Properties)
            {
                var metadata = property.Metadata;
                if (key?.Properties.Contains(metadata) == true || metadata.IsConcurrencyToken || IgnoredProperties.Contains(metadata.Name))
                    continue;

                var name = prefix.Length == 0 ? metadata.Name : $"{prefix}.{metadata.Name}";
                var redacted = RedactedProperties.Contains(metadata.Name);

                switch (entry.State)
                {
                    case EntityState.Added when property.CurrentValue is not null:
                        Merge(name, newValue: Format(property.CurrentValue, redacted), hasNew: true);
                        break;
                    case EntityState.Deleted when property.OriginalValue is not null:
                        Merge(name, oldValue: Format(property.OriginalValue, redacted), hasOld: true);
                        break;
                    case EntityState.Modified when property.IsModified && !AreEqual(property.OriginalValue, property.CurrentValue):
                        Merge(name, Format(property.OriginalValue, redacted), Format(property.CurrentValue, redacted), hasOld: true, hasNew: true);
                        break;
                }
            }
        }

        /// <summary>An owned value that is replaced shows up as a deleted and an added row: they become one change.</summary>
        private void Merge(string name, JsonNode? oldValue = null, JsonNode? newValue = null, bool hasOld = false, bool hasNew = false)
        {
            Changes.TryGetValue(name, out var change);
            change ??= new Change();
            if (hasOld)
            {
                change.Old = oldValue;
                change.HasOld = true;
            }

            if (hasNew)
            {
                change.New = newValue;
                change.HasNew = true;
            }

            if (change is { HasOld: true, HasNew: true } && JsonNode.DeepEquals(change.Old, change.New) && Action == Modified)
                Changes.Remove(name);
            else
                Changes[name] = change;
        }

        public string? EntityId()
        {
            var key = Root.Metadata.FindPrimaryKey();
            if (key is null)
                return null;

            var values = new List<string>();
            foreach (var property in key.Properties)
            {
                var p = Root.Property(property.Name);
                if (p.IsTemporary)
                    return null;
                values.Add(Convert.ToString(Value(Root, property), System.Globalization.CultureInfo.InvariantCulture) ?? "");
            }

            return string.Join(",", values);
        }

        public int? ClubId()
        {
            if (Root.Entity is Domain.Clubs.Club club)
                return club.Id;

            var property = Root.Metadata.FindProperty("ClubId");
            return property is null ? null : Value(Root, property) as int?;
        }

        public (int? UserId, string? Name) Subject()
        {
            if (Root.Entity is Domain.User.User user)
                return (user.Id, user.FullName);

            var property = Root.Metadata.FindProperty("UserId");
            return property is null ? (null, null) : (Value(Root, property) as int?, null);
        }

        public string Summary() => Action switch
        {
            Modified => $"{Modified}: {string.Join(", ", Changes.Keys.Take(MaxPropertiesInSummary))}{(Changes.Count > MaxPropertiesInSummary ? ", …" : "")}",
            _ => Action
        };

        public string? ChangesJson()
        {
            if (Changes.Count == 0)
                return null;

            var json = new JsonObject();
            foreach (var (name, change) in Changes)
            {
                var value = new JsonObject();
                if (change.HasOld)
                    value["old"] = change.Old?.DeepClone();
                if (change.HasNew)
                    value["new"] = change.New?.DeepClone();
                json[name] = value;
            }

            return json.ToJsonString(JsonOptions);
        }
    }

    private sealed class Change
    {
        public JsonNode? Old { get; set; }
        public JsonNode? New { get; set; }
        public bool HasOld { get; set; }
        public bool HasNew { get; set; }
    }

    private static bool AreEqual(object? a, object? b) =>
        a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : Equals(a, b);

    private static JsonNode? Format(object? value, bool redacted)
    {
        if (value is null)
            return null;
        if (redacted)
            return JsonValue.Create(RedactedValue);

        switch (value)
        {
            case byte[] bytes:
                return JsonValue.Create($"<{bytes.Length} bytes>");
            case string s:
                return JsonValue.Create(s.Length <= MaxStringLength ? s : s[..MaxStringLength] + "…");
        }

        try
        {
            return JsonSerializer.SerializeToNode(value, value.GetType(), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return JsonValue.Create(value.ToString());
        }
    }
}
