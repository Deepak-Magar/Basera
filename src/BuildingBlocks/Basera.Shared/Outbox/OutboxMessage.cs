using Basera.Shared.Domain;
using Basera.Shared.Events;

namespace Basera.Shared.Outbox;

/// <summary>
/// One reliable domain event waiting for delivery (table <c>outbox.outbox_message</c>). Written by the module's
/// own DbContext in the same transaction as the change; read and updated only by <see cref="OutboxProcessor"/>.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxAttempts = 10;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private OutboxMessage()
    {
        Type = "";
        Payload = "";
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    /// <summary>The event as JSON (<c>jsonb</c>).</summary>
    public string Payload { get; private set; }

    /// <summary>The tenant the change was made for; the handlers run as that tenant. Null for platform events.</summary>
    public Guid? TenantId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Set after <see cref="MaxAttempts"/> failures; the processor stops trying and someone must look.</summary>
    public bool IsDeadLettered { get; private set; }

    public static OutboxMessage Create(IDomainEvent domainEvent, Guid? tenantId, DateTimeOffset now) => new()
    {
        Id = GuidV7.New(now),
        Type = EventSerializer.TypeName(domainEvent.GetType()),
        Payload = EventSerializer.Serialize(domainEvent),
        TenantId = tenantId,
        OccurredAt = now,
        NextAttemptAt = now,
    };

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    /// <summary>Waits 10 s, 20 s, 40 s ... (capped at an hour) before the next try; dead-letters after the last.</summary>
    public void RecordFailure(Exception exception, DateTimeOffset now)
    {
        Attempts++;
        LastError = Truncate($"{exception.GetType().Name}: {exception.Message}", 2000);
        if (Attempts >= MaxAttempts)
        {
            IsDeadLettered = true;
            return;
        }

        var backoff = TimeSpan.FromSeconds(10 * Math.Pow(2, Attempts - 1));
        NextAttemptAt = now + (backoff < MaxBackoff ? backoff : MaxBackoff);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
