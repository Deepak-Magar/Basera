namespace Basera.Shared.Domain;

/// <summary>An object that raises domain events. <see cref="Entity"/> implements it; the save pipeline reads it.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
