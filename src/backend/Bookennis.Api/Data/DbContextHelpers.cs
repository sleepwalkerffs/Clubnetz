using System.Linq.Expressions;
using Bookennis.Api.Business.Events;
using Bookennis.Api.Infrastructure.User;
using Bookennis.Domain.Base;
using Fusonic.Extensions.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;

namespace Bookennis.Api.Data;

public static class DbContextHelpers
{
#pragma warning disable EF1001 // Internal EF Core API usage.
    public static void ApplyDefaultConventions(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().OrderBy(x => x.BaseType?.Name).ToList()) // oder by is needed because base types need to be configured before derived types
        {
            if (typeof(IEntity).IsAssignableFrom(entityType.ClrType))
            {
                var b = new EntityTypeBuilder(entityType);

                builder
                   .HasSequence<int>(entityType.GetTableName() + "_Id_Sequence")
                   .StartsAt(10) // place for seed data
                   .IncrementsBy(10);

                var propertyBuilder = new PropertyBuilder<int>(entityType.FindProperty(nameof(IEntity.Id))!);
                propertyBuilder.UseHiLo(entityType.GetTableName() + "_Id_Sequence");

                var m = b.OwnsOne(typeof(EntityMetadata), nameof(DomainEntity.Metadata));
                m.WithOwner().HasForeignKey("EntityId");
                m.Property(nameof(EntityMetadata.Created)).HasDefaultValueSql("NOW()");
                m.Property(nameof(EntityMetadata.Modified)).IsConcurrencyToken();
            }

            foreach (var index in entityType.GetIndexes())
            {
                var name = index.GetDatabaseName();
                if (!name!.StartsWith("IX_") && !name.StartsWith("UX_"))
                    name = "IX_" + name;

                //unique indexes get prefixed with UX_ instead of IX_
                if (index.IsUnique)
                {
                    name = "UX" + name[2..];
                    index.SetDatabaseName(name);
                }
            }
        }
    }
#pragma warning restore EF1001
    public static async Task<int> WrapSaveChanges(DbContext context, IDomainEventDispatcher? dispatcher, Func<Task<int>> saveChanges, IUserAccessor? userAccessor, CancellationToken cancellationToken)
    {
        foreach (var entry in context.ChangeTracker.Entries<IEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                var createdEvent = (IDomainEvent)Activator.CreateInstance(typeof(EntityCreated<>).MakeGenericType(entry.Entity.GetType()))!;
                entry.Entity.Events.Insert(0, createdEvent);
            }
            else if (IsModified(entry, context))
            {
                var modifiedEvent = (IDomainEvent)
                    Activator.CreateInstance(typeof(EntityModified<>).MakeGenericType(entry.Entity.GetType()), entry.Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name).ToList())!;

                entry.Entity.Events.Insert(0, modifiedEvent);
            }
        }

        var eventEntities = (from entry in context.ChangeTracker.Entries<IEntity>()
                             where entry.Entity.Events.Count != 0
                             select entry.Entity).ToList();

        UpdateMetadata(context, userAccessor);
        var value = await saveChanges();
        await DispatchEvents(dispatcher, eventEntities, cancellationToken);
        return value;
    }

    private static void UpdateMetadata(DbContext context, IUserAccessor? userAccessor)
    {
        var now = DateTime.Now.ToUniversalTime();

        foreach (var change in context.ChangeTracker.Entries())
        {
            if (change.Entity is not IEntity entity)
                continue;

            int? userId = null;
            if (userAccessor.TryGetUserId(out var id))
                userId = id;

            if (change.State == EntityState.Added)
            {
                entity.Metadata.Created = now;
                entity.Metadata.CreationUserId = userId;
            }
            else if (IsModified(change, context))
            {
                entity.Metadata.Modified = now;
                entity.Metadata.ModificationUserId = userId;
            }
        }
    }

    private static bool IsModified(EntityEntry entry, DbContext context)
    {
        if (entry.State is EntityState.Added or EntityState.Modified)
            return true;

        // any children modified
        foreach (var navigation in entry.Navigations)
        {
            if (navigation.CurrentValue is null)
                continue;

            if (navigation.CurrentValue is not IEnumerable<object> children)
                children = new[] { navigation.CurrentValue };

            if (children.Select(context.Entry).Any(c => c.Metadata.IsOwned() && IsModified(c, context)))
                return true;
        }

        return false;
    }

    private static async Task DispatchEvents(IDomainEventDispatcher? dispatcher, IEnumerable<IEntity> eventEntities, CancellationToken cancellationToken)
    {
        if (dispatcher is null)
            return;

        foreach (var entity in eventEntities)
        {
            var entityType = entity.GetType();
            var events = entity.Events.ToList();
            entity.Events.Clear();
            foreach (var @event in events)
            {
                var notificationType = typeof(DomainEvent<>).MakeGenericType(@event.GetType());
                var notification = Activator.CreateInstance(notificationType, entityType.GUID, entity.Id, @event);
                if (notification != null)
                    await dispatcher.Dispatch((INotification)notification, cancellationToken);
            }
        }
    }

    // https://github.com/dotnet/efcore/issues/10275#issuecomment-670866212
    public static void AddQueryFilterToAllEntitiesAssignableFrom<T>(
        this ModelBuilder modelBuilder,
        Expression<Func<T, bool>> expression,
        List<Type> except)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(T).IsAssignableFrom(entityType.ClrType) || except.Contains(entityType.ClrType))
                continue;

            var parameterType = Expression.Parameter(entityType.ClrType);
            var expressionFilter = ReplacingExpressionVisitor.Replace(
                expression.Parameters.Single(), parameterType, expression.Body);

            var currentQueryFilter = entityType.GetDeclaredQueryFilters().LastOrDefault();
            if (currentQueryFilter?.Expression != null)
            {
                var filterExpression = currentQueryFilter.Expression;
                var currentExpressionFilter = ReplacingExpressionVisitor.Replace(
                    filterExpression.Parameters.Single(), parameterType, filterExpression.Body);

                expressionFilter = Expression.AndAlso(currentExpressionFilter, expressionFilter);
            }

            var lambdaExpression = Expression.Lambda(expressionFilter, parameterType);
            entityType.SetQueryFilter(lambdaExpression);
        }
    }
}