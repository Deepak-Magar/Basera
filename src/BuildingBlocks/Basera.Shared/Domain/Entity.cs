namespace Basera.Shared.Domain;

/// <summary>
/// Base class for entities: a <see cref="GuidV7"/> id and the domain events raised since the last save.
/// The DbContext collects the events when <c>SaveChanges</c> runs and clears them once it has succeeded.
/// </summary>
public abstract class Entity : IHasDomainEvents
{
    private readonly List<IDomainEvent> domainEvents = [];

    public Guid Id { get; protected set; } = GuidV7.New();

    public IReadOnlyList<IDomainEvent> DomainEvents => domainEvents;

    public void ClearDomainEvents() => domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => domainEvents.Add(domainEvent);
}
