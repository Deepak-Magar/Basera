using System.Collections.Concurrent;
using Basera.Shared.Time;
using Microsoft.Extensions.Logging;

namespace Basera.Shared.Messaging;

/// <summary>An SMS the mock "sent", as listed on <c>/dev/sms</c>.</summary>
public sealed record SentSms(DateTimeOffset SentAt, SmsMessage Message);

/// <summary>
/// The mock's outbox, newest first, in memory and capped. It is a development tool, so a restart empties it.
/// </summary>
public sealed class MockSmsInbox
{
    public const int Capacity = 500;
    private readonly ConcurrentQueue<SentSms> messages = new();

    public IReadOnlyList<SentSms> Messages => messages.Reverse().ToList();

    internal void Add(SentSms sms)
    {
        messages.Enqueue(sms);
        while (messages.Count > Capacity && messages.TryDequeue(out _))
        {
        }
    }
}

/// <summary>Sends nothing: records the SMS in <see cref="MockSmsInbox"/> and logs it.</summary>
public sealed class MockSmsSender(MockSmsInbox inbox, IClock clock, ILogger<MockSmsSender> logger) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        inbox.Add(new SentSms(clock.UtcNow, message));
        logger.LogInformation("Mock SMS ({Purpose}, {Segments} part(s)) to {Phone}: {Text}",
            message.Purpose, message.Segments, message.To.E164, message.Text);
        return Task.CompletedTask;
    }
}
