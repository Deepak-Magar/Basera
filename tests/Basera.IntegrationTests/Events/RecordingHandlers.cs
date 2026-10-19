using System.Collections.Concurrent;
using Basera.IntegrationTests.Tenancy;
using Basera.Shared.Domain;
using Basera.Shared.Events;
using Basera.Shared.Tenancy;

namespace Basera.IntegrationTests.Events;

/// <summary>What the handlers saw, and as which tenant. A singleton shared with the test.</summary>
public sealed class EventRecorder
{
    public ConcurrentQueue<(IDomainEvent Event, Guid? TenantId)> Handled { get; } = new();

    public bool Saw(Func<IDomainEvent, bool> match) => Handled.Any(entry => match(entry.Event));
}

/// <summary>Records events; fails on purpose for notes whose text starts with "fail".</summary>
public sealed class RecordingHandler(EventRecorder recorder, ITenantContext tenant)
    : IDomainEventHandler<ProbeNotePublished>, IDomainEventHandler<ProbeNoteRenamed>
{
    public Task HandleAsync(ProbeNotePublished domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Text.StartsWith("fail", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Handler refused {domainEvent.Text}.");
        }

        recorder.Handled.Enqueue((domainEvent, tenant.TenantId));
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProbeNoteRenamed domainEvent, CancellationToken cancellationToken)
    {
        recorder.Handled.Enqueue((domainEvent, tenant.TenantId));
        return Task.CompletedTask;
    }
}
