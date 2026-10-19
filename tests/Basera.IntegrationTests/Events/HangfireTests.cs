using System.Diagnostics;
using System.Net;
using Basera.IntegrationTests.Infrastructure;
using Basera.IntegrationTests.Tenancy;
using Basera.Shared.Domain;
using Basera.Shared.Events;
using Basera.Shared.Jobs;
using Basera.Shared.Outbox;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace Basera.IntegrationTests.Events;

/// <summary>Hangfire itself: storage, the recurring outbox job, and the dashboard policy.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class HangfireTests(TestDatabase database, ITestOutputHelper output)
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Outbox_job_is_registered_every_ten_seconds()
    {
        await using var factory = new BaseraWebFactory(database.ConnectionString);

        using var connection = factory.Services.GetRequiredService<JobStorage>().GetConnection();
        connection.GetRecurringJobs().Should().ContainSingle(job => job.Id == OutboxProcessor.JobId)
            .Which.Cron.Should().Be("*/10 * * * * *");
    }

    [Fact]
    public async Task Hangfire_server_delivers_a_reliable_event_right_after_the_save()
    {
        await using var factory = CreateFactoryWithServer();
        var note = new ProbeNote("delivered by hangfire", Money.Zero);
        note.Publish();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(GuidV7.New());
            var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            db.Notes.Add(note);
            await db.SaveChangesAsync();
        }

        await WaitUntilHandledAsync(factory, note.Id);
    }

    [Fact]
    public async Task Recurring_job_picks_up_messages_nobody_signalled()
    {
        // Jobs enqueued by earlier tests would deliver this message too; only the schedule may find it.
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var purge = new NpgsqlCommand($"DELETE FROM {JobsServiceCollectionExtensions.HangfireSchema}.jobqueue", connection);
            await purge.ExecuteNonQueryAsync();
        }

        await using var factory = CreateFactoryWithServer();
        var noteId = GuidV7.New();

        // Written without the post-save signal, as if the process had died just after committing.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
            outbox.Messages.Add(OutboxMessage.Create(new ProbeNotePublished(noteId, "found by the poll", Money.Zero), GuidV7.New(), DateTimeOffset.UtcNow));
            await outbox.SaveChangesAsync();
        }

        await WaitUntilHandledAsync(factory, noteId);
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.Unauthorized)]
    public async Task Dashboard_is_open_only_in_development_until_platform_admins_exist(string environment, HttpStatusCode expected)
    {
        await using var factory = new BaseraWebFactory(database.ConnectionString, environment: environment);

        var response = await factory.CreateClient().GetAsync(JobsServiceCollectionExtensions.DashboardPath);

        response.StatusCode.Should().Be(expected);
    }

    private BaseraWebFactory CreateFactoryWithServer()
    {
        var factory = new BaseraWebFactory(database.ConnectionString, services =>
        {
            services.AddModuleDbContext<ProbeDbContext>(database.Configuration);
            services.AddDomainEventHandlers(typeof(RecordingHandler).Assembly);
            services.AddSingleton<EventRecorder>();
        }, jobsServer: true);
        ProbeDbContext.EnsureCreatedAsync(factory.Services, database.ConnectionString).GetAwaiter().GetResult();
        return factory;
    }

    private async Task WaitUntilHandledAsync(BaseraWebFactory factory, Guid noteId)
    {
        var recorder = factory.Services.GetRequiredService<EventRecorder>();
        var watch = Stopwatch.StartNew();
        while (!recorder.Saw(e => e is ProbeNotePublished p && p.NoteId == noteId))
        {
            watch.Elapsed.Should().BeLessThan(DeliveryTimeout, "Hangfire should run the outbox processor");
            await Task.Delay(200);
        }

        output.WriteLine($"Delivered after {watch.Elapsed.TotalSeconds:F1} s.");

        await using var scope = factory.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var message = await outbox.Messages.AsNoTracking()
            .SingleAsync(m => EF.Functions.JsonContains(m.Payload, $"{{\"noteId\":\"{noteId}\"}}"));
        message.ProcessedAt.Should().NotBeNull();
    }
}
