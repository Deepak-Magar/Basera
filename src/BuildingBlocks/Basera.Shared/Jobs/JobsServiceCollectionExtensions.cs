using Basera.Shared.Authorization;
using Basera.Shared.Outbox;
using Basera.Shared.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Basera.Shared.Jobs;

public static class JobsServiceCollectionExtensions
{
    public const string HangfireSchema = "hangfire";
    public const string DashboardPath = "/jobs";

    /// <summary>
    /// Hangfire on PostgreSQL (ADR-006) in the <c>hangfire</c> schema. Set <c>Jobs:ServerEnabled</c> to false
    /// to keep storage and the dashboard but run no jobs in this process (integration tests do this).
    /// </summary>
    public static IServiceCollection AddBaseraJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                storage => storage.UseNpgsqlConnection(() => PersistenceServiceCollectionExtensions.GetConnectionString(configuration)),
                new PostgreSqlStorageOptions
                {
                    SchemaName = HangfireSchema,
                    // Hangfire creates and upgrades its own tables; they are not EF migrations.
                    PrepareSchemaIfNecessary = true,
                    // Enqueued jobs (the immediate outbox run) start within a second instead of fifteen.
                    QueuePollInterval = TimeSpan.FromSeconds(1),
                }));

        if (configuration.GetValue("Jobs:ServerEnabled", true))
        {
            services.AddHangfireServer(server =>
            {
                server.WorkerCount = Math.Clamp(Environment.ProcessorCount, 2, 8);
                // Recurring and delayed jobs are checked every second, so the 10-second outbox schedule holds.
                server.SchedulePollingInterval = TimeSpan.FromSeconds(1);
            });
        }

        services.Replace(ServiceDescriptor.Scoped<IOutboxSignal, HangfireOutboxSignal>());
        services.AddBaseraPolicies();
        return services;
    }

    /// <summary>Maps the dashboard behind <see cref="BaseraPolicies.PlatformAdmin"/> and registers Shared's recurring jobs.</summary>
    public static IEndpointRouteBuilder MapBaseraJobs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHangfireDashboard(DashboardPath, new DashboardOptions
        {
            DashboardTitle = "Basera jobs",
            Authorization = [],
            AsyncAuthorization = [new PolicyDashboardAuthorizationFilter(BaseraPolicies.PlatformAdmin)],
        });

        endpoints.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxProcessor>(
            OutboxProcessor.JobId,
            processor => processor.ProcessAsync(CancellationToken.None),
            OutboxProcessor.Schedule);
        return endpoints;
    }
}
