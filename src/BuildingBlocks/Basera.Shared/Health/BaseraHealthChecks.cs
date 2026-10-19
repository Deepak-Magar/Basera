using System.Text.Json;
using Basera.Shared.Files;
using Basera.Shared.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Basera.Shared.Health;

/// <summary><c>/health</c> checks of ARCHITECTURE §10: database, file storage, Hangfire.</summary>
public static class BaseraHealthChecks
{
    public static IHealthChecksBuilder AddBaseraHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<FileStorageHealthCheck>("storage")
            .AddCheck<HangfireHealthCheck>("hangfire");

    /// <summary>
    /// <c>{"status":"Healthy","checks":[{"name":"database","status":"Healthy","durationMs":3}]}</c>.
    /// No exception text: /health is anonymous, and the details are in the log.
    /// </summary>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = (int)entry.Value.Duration.TotalMilliseconds,
            }),
        }));
    }
}

internal sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(PersistenceServiceCollectionExtensions.GetConnectionString(configuration));
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", exception);
        }
    }
}

/// <summary>Writes, reads back and deletes a probe file under a reserved tenant id.</summary>
internal sealed class FileStorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    private static readonly Guid ProbeTenant = new("00000000-0000-7000-8000-00000000beef");

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var key = new FileKey(ProbeTenant, "health", Guid.NewGuid());
        try
        {
            await using (var content = new MemoryStream("ok"u8.ToArray()))
            {
                await storage.SaveAsync(key, content, "text/plain", cancellationToken);
            }

            var exists = await storage.ExistsAsync(key, cancellationToken);
            await storage.DeleteAsync(key, cancellationToken);
            return exists ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("A probe file could not be read back.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("File storage is not writable.", exception);
        }
    }
}

internal sealed class HangfireHealthCheck(JobStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = storage.GetMonitoringApi().GetStatistics();
            return Task.FromResult(HealthCheckResult.Healthy(data: new Dictionary<string, object>
            {
                ["servers"] = statistics.Servers,
                ["failed"] = statistics.Failed,
            }));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire storage is not reachable.", exception));
        }
    }
}
