namespace Basera.Shared.Domain;

/// <summary>
/// Marks a domain event whose handlers must not be skipped, such as raising an invoice or sending a payment SMS.
/// Instead of running in-process, it is written to the outbox in the same transaction as the change that
/// raised it, and delivered by the outbox processor with retries. Its handlers must be idempotent, because a
/// handler that fails is retried together with the other handlers of the same event.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ReliableAttribute : Attribute
{
}
