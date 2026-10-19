using Basera.Shared;
using Basera.Shared.Domain;
using Basera.Shared.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.UnitTests.Shared.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task Runs_every_handler_registered_for_the_event_type_in_order()
    {
        var log = new List<string>();
        await using var services = Build(log, s => s
            .AddScoped<IDomainEventHandler<RoomAdded>>(_ => new Recording<RoomAdded>(log, "first"))
            .AddScoped<IDomainEventHandler<RoomAdded>>(_ => new Recording<RoomAdded>(log, "second")));

        await Dispatch(services, new RoomAdded("B1-101"));

        log.Should().Equal("first:RoomAdded", "second:RoomAdded");
    }

    [Fact]
    public async Task Does_not_run_handlers_of_other_event_types()
    {
        var log = new List<string>();
        await using var services = Build(log, s => s
            .AddScoped<IDomainEventHandler<RoomAdded>>(_ => new Recording<RoomAdded>(log, "room"))
            .AddScoped<IDomainEventHandler<BedFreed>>(_ => new Recording<BedFreed>(log, "bed")));

        await Dispatch(services, new BedFreed("B1-101-A"));

        log.Should().Equal("bed:BedFreed");
    }

    [Fact]
    public async Task An_event_without_handlers_is_fine()
    {
        await using var services = Build([], _ => { });

        var act = () => Dispatch(services, new RoomAdded("B1-101"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_failing_handler_stops_dispatch_and_its_exception_propagates()
    {
        var log = new List<string>();
        await using var services = Build(log, s => s
            .AddScoped<IDomainEventHandler<RoomAdded>>(_ => new Failing())
            .AddScoped<IDomainEventHandler<RoomAdded>>(_ => new Recording<RoomAdded>(log, "after")));

        var act = () => Dispatch(services, new RoomAdded("B1-101"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        log.Should().BeEmpty("the outbox retries the whole event, so later handlers wait for the retry");
    }

    [Fact]
    public async Task Handlers_are_resolved_from_the_dispatching_scope()
    {
        await using var services = Build([], s => s
            .AddScoped<ScopeMarker>()
            .AddScoped<IDomainEventHandler<RoomAdded>, ScopeCapturing>());

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(new RoomAdded("B1-101"));

        scope.ServiceProvider.GetRequiredService<ScopeMarker>().Seen.Should().BeTrue();
    }

    [Fact]
    public void Assembly_scan_registers_each_handler_interface()
    {
        var services = new ServiceCollection().AddDomainEventHandlers(typeof(DomainEventDispatcherTests).Assembly);

        services.Should().Contain(d => d.ServiceType == typeof(IDomainEventHandler<RoomAdded>) && d.ImplementationType == typeof(ScopeCapturing));
        services.Should().Contain(d => d.ServiceType == typeof(IDomainEventHandler<RoomAdded>) && d.ImplementationType == typeof(TwoEvents));
        services.Should().Contain(d => d.ServiceType == typeof(IDomainEventHandler<BedFreed>) && d.ImplementationType == typeof(TwoEvents));
    }

    private static ServiceProvider Build(List<string> log, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection().AddBaseraShared();
        services.AddSingleton(log);
        configure(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task Dispatch(ServiceProvider services, IDomainEvent domainEvent)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(domainEvent);
    }

    public sealed record RoomAdded(string Code) : IDomainEvent;

    public sealed record BedFreed(string Code) : IDomainEvent;

    private sealed class Recording<TEvent>(List<string> log, string name) : IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        public Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken)
        {
            log.Add($"{name}:{typeof(TEvent).Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class Failing : IDomainEventHandler<RoomAdded>
    {
        public Task HandleAsync(RoomAdded domainEvent, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    public sealed class ScopeMarker
    {
        public bool Seen { get; set; }
    }

    public sealed class ScopeCapturing(ScopeMarker marker) : IDomainEventHandler<RoomAdded>
    {
        public Task HandleAsync(RoomAdded domainEvent, CancellationToken cancellationToken)
        {
            marker.Seen = true;
            return Task.CompletedTask;
        }
    }

    public sealed class TwoEvents : IDomainEventHandler<RoomAdded>, IDomainEventHandler<BedFreed>
    {
        public Task HandleAsync(RoomAdded domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HandleAsync(BedFreed domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
