using FactoryERP.SharedKernel.SeedWork;

namespace FactoryERP.SharedKernel.SeedWork;

/// <summary>
/// Base entity with identity, domain events, and optimistic concurrency.
/// All module entities should inherit from this.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Primary key.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>Concurrency token for optimistic locking (EF Core RowVersion).</summary>
    public byte[] RowVersion { get; set; } = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
