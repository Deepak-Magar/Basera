using System.Collections.Concurrent;
using Basera.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Events;

public interface IDomainEventDispatcher
{
    /// <summary>Runs every handler registered for the event's exact type, one after another. The first failure propagates.</summary>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>Finds <see cref="IDomainEventHandler{TEvent}"/> implementations in the current DI scope.</summary>
public sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var invoker = Invokers.GetOrAdd(
            domainEvent.GetType(),
            type => (HandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(type))!);
        return invoker.InvokeAsync(services, domainEvent, cancellationToken);
    }

    // Bridges from the runtime event type to the generic handler interface without reflection per call.
    private abstract class HandlerInvoker
    {
        public abstract Task InvokeAsync(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken);
    }

    private sealed class HandlerInvoker<TEvent> : HandlerInvoker
        where TEvent : IDomainEvent
    {
        public override async Task InvokeAsync(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
