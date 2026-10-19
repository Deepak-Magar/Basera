using Basera.Shared.Domain;
using Basera.Shared.Outbox;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Basera.Shared.Events;

/// <summary>
/// The event part of the save pipeline (ARCHITECTURE §4.5–4.6). Before saving, it collects the events raised
/// by tracked entities and adds an <see cref="OutboxMessage"/> for each <see cref="ReliableAttribute">[Reliable]</see>
/// one, so they commit or roll back together with the change. After a successful save, it runs the handlers of
/// the other events in a fresh scope and nudges the outbox processor.
/// Caveat: inside an explicit transaction "after save" is before commit, so services should not open one.
/// </summary>
public sealed class DomainEventInterceptor(
    ITenantContext tenantContext,
    IClock clock,
    IServiceScopeFactory scopeFactory,
    IOutboxSignal outboxSignal,
    ILogger<DomainEventInterceptor> logger) : SaveChangesInterceptor
{
    // One scoped interceptor serves every DbContext in the scope, so pending work is kept per context.
    private readonly Dictionary<DbContext, PendingEvents> pending = new(ReferenceEqualityComparer.Instance);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        AfterSaveAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await AfterSaveAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        Discard(context);
        var sources = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();
        if (sources.Count == 0)
        {
            return;
        }

        var batch = new PendingEvents(sources);
        foreach (var domainEvent in sources.SelectMany(source => source.DomainEvents))
        {
            if (EventSerializer.IsReliable(domainEvent))
            {
                var message = OutboxMessage.Create(domainEvent, tenantContext.TenantId, clock.UtcNow);
                context.Add(message);
                batch.OutboxMessages.Add(message);
            }
            else
            {
                batch.InProcess.Add(domainEvent);
            }
        }

        pending[context] = batch;
    }

    private async Task AfterSaveAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !pending.Remove(context, out var batch))
        {
            return;
        }

        // Events are cleared only now, so a failed save can be retried without losing them.
        batch.Sources.ForEach(source => source.ClearDomainEvents());
        foreach (var message in batch.OutboxMessages)
        {
            context.Entry(message).State = EntityState.Detached;
        }

        if (batch.OutboxMessages.Count > 0)
        {
            outboxSignal.MessagesWritten();
        }

        foreach (var domainEvent in batch.InProcess)
        {
            await DispatchInProcessAsync(domainEvent, cancellationToken);
        }
    }

    // The change is already committed, so a failing in-process handler is logged rather than thrown:
    // the user's action succeeded. Events that must not be lost are [Reliable].
    private async Task DispatchInProcessAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            if (tenantContext.TenantId is { } tenantId)
            {
                scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
            }

            await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(domainEvent, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "In-process handler for {EventType} failed after the change was saved", domainEvent.GetType().Name);
        }
    }

    // A failed save leaves the outbox rows Added; detach them so a retry does not write them twice.
    private void Discard(DbContext? context)
    {
        if (context is null || !pending.Remove(context, out var batch))
        {
            return;
        }

        foreach (var message in batch.OutboxMessages)
        {
            context.Entry(message).State = EntityState.Detached;
        }
    }

    private sealed record PendingEvents(List<IHasDomainEvents> Sources)
    {
        public List<IDomainEvent> InProcess { get; } = [];

        public List<OutboxMessage> OutboxMessages { get; } = [];
    }
}
