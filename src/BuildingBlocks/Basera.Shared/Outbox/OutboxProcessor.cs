using Basera.Shared.Events;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Basera.Shared.Outbox;

/// <summary>
/// Delivers outbox messages to their handlers: a Hangfire recurring job every 10 s plus an immediate run after
/// each save that wrote messages. Each message is claimed with <c>FOR UPDATE SKIP LOCKED</c> in its own
/// transaction, so overlapping runs never deliver the same message twice at once, and one failing message
/// does not hold up the rest. Delivery is at least once: handlers must be idempotent.
/// </summary>
public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, IClock clock, ILogger<OutboxProcessor> logger)
{
    /// <summary>ARCHITECTURE §8.</summary>
    public const string JobId = "notifications:outbox";
    public const string Schedule = "*/10 * * * * *";
    public const int BatchSize = 100;

    /// <summary>Delivers up to <see cref="BatchSize"/> due messages and returns how many it handled.</summary>
    [AutomaticRetry(Attempts = 0)] // retries are per message (OutboxMessage.RecordFailure), not per run
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var handled = 0;
        while (handled < BatchSize && await ProcessNextAsync(cancellationToken))
        {
            handled++;
        }

        return handled;
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = clock.UtcNow;
        var message = (await db.Messages
            .FromSql($"""
                SELECT * FROM outbox.outbox_message
                WHERE processed_at IS NULL AND NOT is_dead_lettered AND next_attempt_at <= {now}
                ORDER BY next_attempt_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken))
            .SingleOrDefault();
        if (message is null)
        {
            return false;
        }

        try
        {
            await DeliverAsync(message, cancellationToken);
            message.MarkProcessed(clock.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.RecordFailure(exception, clock.UtcNow);
            if (message.IsDeadLettered)
            {
                logger.LogError(exception, "Outbox message {MessageId} ({Type}) dead-lettered after {Attempts} attempts",
                    message.Id, message.Type, message.Attempts);
            }
            else
            {
                logger.LogWarning(exception, "Outbox message {MessageId} ({Type}) failed attempt {Attempts}; next at {NextAttemptAt}",
                    message.Id, message.Type, message.Attempts, message.NextAttemptAt);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // A fresh scope per message, acting as the tenant that raised the event.
    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var domainEvent = EventSerializer.Deserialize(message.Type, message.Payload);
        await using var scope = scopeFactory.CreateAsyncScope();
        if (message.TenantId is { } tenantId)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        }

        await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(domainEvent, cancellationToken);
    }
}
