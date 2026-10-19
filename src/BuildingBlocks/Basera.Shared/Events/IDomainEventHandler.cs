using Basera.Shared.Domain;

namespace Basera.Shared.Events;

/// <summary>
/// Reacts to one event type, usually in another module (ARCHITECTURE §3.3). Runs in its own DI scope with
/// the tenant of the change that raised the event. Register with
/// <see cref="EventsServiceCollectionExtensions.AddDomainEventHandlers"/>.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
