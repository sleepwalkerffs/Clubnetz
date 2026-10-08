using System.ComponentModel.DataAnnotations;
using Fusonic.Extensions.Common.Entities;

namespace Bookennis.Domain.Base;

public interface IEntity : IEntity<int>
{
    public EntityMetadata Metadata { get; }

    public List<IDomainEvent> Events { get; }

    public void AddDomainEvent(IDomainEvent domainEvent);
}

public class EntityMetadata
{
    public DateTime Created { get; set; }
    public int? CreationUserId { get; set; }

    public DateTime? Modified { get; set; }
    public int? ModificationUserId { get; set; }
}

public class DomainEntity : IEntity
{
    [Key]
    public int Id { get; protected set; }
    public EntityMetadata Metadata { get; private set; } = new();

    public List<IDomainEvent> Events { get; } = new();

    public void AddDomainEvent(IDomainEvent domainEvent) => Events.Add(domainEvent);
}

public class TenantDomainEntity : DomainEntity
{
    public int ClubId { get; protected set; }
}

public class EntityCreated<TEntity> : IDomainEvent
    where TEntity : IEntity
{ }

public class EntityModified<TEntity>(IReadOnlyList<string> modifiedProperties) : IDomainEvent
    where TEntity : IEntity
{
    public IReadOnlyList<string> ModifiedProperties { get; } = modifiedProperties;
}