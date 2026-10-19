namespace Basera.Shared.Outbox;

/// <summary>
/// Told when new outbox rows have been committed, so delivery can start now instead of at the next 10-second
/// poll. The Hangfire implementation enqueues the processor; without Hangfire nothing happens and the rows
/// wait for the recurring job.
/// </summary>
public interface IOutboxSignal
{
    void MessagesWritten();
}

public sealed class NullOutboxSignal : IOutboxSignal
{
    public void MessagesWritten()
    {
    }
}
