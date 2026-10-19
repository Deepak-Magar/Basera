using Basera.Shared.Outbox;
using Hangfire;

namespace Basera.Shared.Jobs;

/// <summary>Starts an outbox run right away after a save that wrote messages (ARCHITECTURE §4.6, "plus immediate enqueue").</summary>
public sealed class HangfireOutboxSignal(IBackgroundJobClient jobs) : IOutboxSignal
{
    public void MessagesWritten() => jobs.Enqueue<OutboxProcessor>(processor => processor.ProcessAsync(CancellationToken.None));
}
