using System.Text.Json;
using Basera.IntegrationTests.Infrastructure;
using Basera.IntegrationTests.Tenancy;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Events;
using Basera.Shared.Outbox;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Events;

/// <summary>
/// ADR-011 through the real host: a [Reliable] event is written to outbox.outbox_message in the transaction of
/// the change that raised it, and OutboxProcessor delivers it as the right tenant, retrying and dead-lettering.
/// The processor is called directly here; <see cref="HangfireTests"/> shows Hangfire running it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OutboxTests(TestDatabase database) : IAsyncLifetime
{
    private readonly Guid tenant = GuidV7.New();
    private readonly MutableClock clock = new(DateTimeOffset.UtcNow);
    private BaseraWebFactory factory = null!;

    private EventRecorder Recorder => factory.Services.GetRequiredService<EventRecorder>();

    public async Task InitializeAsync()
    {
        factory = new BaseraWebFactory(database.ConnectionString, services =>
        {
            services.AddModuleDbContext<ProbeDbContext>(database.Configuration);
            services.AddDomainEventHandlers(typeof(RecordingHandler).Assembly);
            services.AddSingleton<EventRecorder>();
            services.AddSingleton<IClock>(clock);
        });
        await ProbeDbContext.EnsureCreatedAsync(factory.Services, database.ConnectionString);
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Reliable_event_is_written_to_the_outbox_with_the_change_and_not_handled_yet()
    {
        var note = await SaveNoteAsync("rent due", Money.FromRupees(12_500m), publish: true);

        var message = await SingleMessageForAsync(note.Id);
        message.Type.Should().Be(EventSerializer.TypeName(typeof(ProbeNotePublished)));
        message.TenantId.Should().Be(tenant);
        message.ProcessedAt.Should().BeNull();
        // jsonb normalises spacing and key order, so read the value rather than match text.
        JsonDocument.Parse(message.Payload).RootElement.GetProperty("amount").GetInt64().Should().Be(1_250_000);
        Recorder.Saw(e => e is ProbeNotePublished p && p.NoteId == note.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Rolled_back_change_leaves_no_outbox_row()
    {
        var note = new ProbeNote("never", Money.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            note.Publish();
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        (await MessagesForAsync(note.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Processor_delivers_the_event_as_the_raising_tenant_and_marks_it_processed()
    {
        var note = await SaveNoteAsync("rent due", Money.FromRupees(12_500m), publish: true);

        await ProcessAsync();

        Recorder.Handled.Should().ContainSingle(entry => entry.Event is ProbeNotePublished && ((ProbeNotePublished)entry.Event).NoteId == note.Id)
            .Which.TenantId.Should().Be(tenant);
        Recorder.Handled.Select(entry => entry.Event).OfType<ProbeNotePublished>().Single(p => p.NoteId == note.Id)
            .Should().Be(new ProbeNotePublished(note.Id, "rent due", Money.FromRupees(12_500m)));
        var message = await SingleMessageForAsync(note.Id);
        message.ProcessedAt.Should().BeCloseTo(clock.UtcNow, TimeSpan.FromMilliseconds(1));
        message.Attempts.Should().Be(0);

        await ProcessAsync();
        Recorder.Handled.Count(entry => entry.Event is ProbeNotePublished p && p.NoteId == note.Id).Should().Be(1, "a processed message is not delivered again");
    }

    [Fact]
    public async Task Failing_handler_is_retried_with_backoff_and_dead_lettered_after_the_last_attempt()
    {
        var note = await SaveNoteAsync("fail always", Money.Zero, publish: true);

        await ProcessAsync();
        var first = await SingleMessageForAsync(note.Id);
        first.Attempts.Should().Be(1);
        first.LastError.Should().Contain("Handler refused fail always");
        first.NextAttemptAt.Should().BeCloseTo(clock.UtcNow.AddSeconds(10), TimeSpan.FromMilliseconds(1));
        first.IsDeadLettered.Should().BeFalse();

        await ProcessAsync();
        (await SingleMessageForAsync(note.Id)).Attempts.Should().Be(1, "it is not due again for 10 seconds");

        for (var attempt = 2; attempt <= OutboxMessage.MaxAttempts; attempt++)
        {
            clock.UtcNow = clock.UtcNow.AddHours(1);
            await ProcessAsync();
        }

        var last = await SingleMessageForAsync(note.Id);
        last.Attempts.Should().Be(OutboxMessage.MaxAttempts);
        last.IsDeadLettered.Should().BeTrue();
        last.ProcessedAt.Should().BeNull();

        clock.UtcNow = clock.UtcNow.AddDays(1);
        await ProcessAsync();
        (await SingleMessageForAsync(note.Id)).Attempts.Should().Be(OutboxMessage.MaxAttempts, "dead-lettered messages are left alone");
    }

    [Fact]
    public async Task In_process_event_is_handled_right_after_the_save_and_not_written_to_the_outbox()
    {
        var note = await SaveNoteAsync("draft", Money.Zero, publish: false);

        await AsTenantAsync(async db => (await db.Notes.SingleAsync(n => n.Id == note.Id)).Rename("final"));

        Recorder.Handled.Should().ContainSingle(entry => entry.Event is ProbeNoteRenamed && ((ProbeNoteRenamed)entry.Event).NoteId == note.Id)
            .Which.TenantId.Should().Be(tenant);
        (await MessagesForAsync(note.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Events_are_cleared_after_a_successful_save_so_they_are_not_raised_twice()
    {
        var note = new ProbeNote("once", Money.Zero);
        await AsTenantAsync(async db =>
        {
            note.Publish();
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            note.DomainEvents.Should().BeEmpty();
            note.Text = "once, edited";
        });

        (await MessagesForAsync(note.Id)).Should().ContainSingle();
    }

    private async Task<ProbeNote> SaveNoteAsync(string text, Money amount, bool publish)
    {
        var note = new ProbeNote(text, amount);
        if (publish)
        {
            note.Publish();
        }

        await AsTenantAsync(db =>
        {
            db.Notes.Add(note);
            return Task.CompletedTask;
        });
        return note;
    }

    private async Task AsTenantAsync(Func<ProbeDbContext, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
        await work(db);
        await db.SaveChangesAsync();
    }

    private async Task ProcessAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessAsync(CancellationToken.None);
    }

    private async Task<List<OutboxMessage>> MessagesForAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        return await outbox.Messages.AsNoTracking()
            .Where(m => EF.Functions.JsonContains(m.Payload, $"{{\"noteId\":\"{noteId}\"}}"))
            .ToListAsync();
    }

    private async Task<OutboxMessage> SingleMessageForAsync(Guid noteId) => (await MessagesForAsync(noteId)).Single();

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public DateTimeOffset NepalNow => UtcNow.ToOffset(SystemClock.NepalOffset);

        public DateOnly Today => DateOnly.FromDateTime(NepalNow.DateTime);

        public NepaliDate TodayBs => NepaliDate.FromDateOnly(Today);
    }
}
