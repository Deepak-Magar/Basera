using Basera.Shared.Domain;
using Basera.Shared.Events;
using Basera.Shared.Outbox;

namespace Basera.UnitTests.Shared.Events;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_serializes_the_event_and_is_due_immediately()
    {
        var tenant = GuidV7.New();

        var message = OutboxMessage.Create(new PaymentRecorded(Guid.Empty, Money.FromRupees(500m)), tenant, Now);

        message.Type.Should().Be("Basera.UnitTests.Shared.Events.OutboxMessageTests+PaymentRecorded, Basera.UnitTests");
        message.Payload.Should().Be("""{"receiptId":"00000000-0000-0000-0000-000000000000","amount":50000}""");
        message.TenantId.Should().Be(tenant);
        message.NextAttemptAt.Should().Be(Now);
        message.Attempts.Should().Be(0);
    }

    [Fact]
    public void Failures_back_off_exponentially_up_to_an_hour()
    {
        var message = OutboxMessage.Create(new PaymentRecorded(Guid.Empty, Money.Zero), null, Now);
        var delays = new List<TimeSpan>();

        for (var i = 0; i < OutboxMessage.MaxAttempts - 1; i++)
        {
            message.RecordFailure(new InvalidOperationException("sms gateway down"), Now);
            delays.Add(message.NextAttemptAt - Now);
        }

        delays.Select(d => d.TotalSeconds).Should().Equal(10, 20, 40, 80, 160, 320, 640, 1280, 2560);
        message.IsDeadLettered.Should().BeFalse();
        message.LastError.Should().Be("InvalidOperationException: sms gateway down");
    }

    [Fact]
    public void The_last_failure_dead_letters_the_message()
    {
        var message = OutboxMessage.Create(new PaymentRecorded(Guid.Empty, Money.Zero), null, Now);

        for (var i = 0; i < OutboxMessage.MaxAttempts; i++)
        {
            message.RecordFailure(new InvalidOperationException("no"), Now);
        }

        message.Attempts.Should().Be(OutboxMessage.MaxAttempts);
        message.IsDeadLettered.Should().BeTrue();
        message.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void Long_errors_are_truncated_to_fit_the_column()
    {
        var message = OutboxMessage.Create(new PaymentRecorded(Guid.Empty, Money.Zero), null, Now);

        message.RecordFailure(new InvalidOperationException(new string('x', 5000)), Now);

        message.LastError.Should().HaveLength(2000);
    }

    [Fact]
    public void Serializer_round_trips_events_including_money()
    {
        var original = new PaymentRecorded(GuidV7.New(), Money.FromRupees(12_500.50m));

        var copy = EventSerializer.Deserialize(EventSerializer.TypeName(original.GetType()), EventSerializer.Serialize(original));

        copy.Should().Be(original);
    }

    [Fact]
    public void Serializer_refuses_types_that_are_not_domain_events()
    {
        var act = () => EventSerializer.Deserialize(typeof(Money).AssemblyQualifiedName!, "{}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not a known domain event type*");
    }

    [Fact]
    public void Reliable_attribute_decides_the_path()
    {
        EventSerializer.IsReliable(new PaymentRecorded(Guid.Empty, Money.Zero)).Should().BeTrue();
        EventSerializer.IsReliable(new MenuViewed()).Should().BeFalse();
    }

    [Reliable]
    public sealed record PaymentRecorded(Guid ReceiptId, Money Amount) : IDomainEvent;

    public sealed record MenuViewed : IDomainEvent;
}
