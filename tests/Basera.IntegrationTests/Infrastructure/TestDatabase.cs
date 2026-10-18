using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Basera.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL database for the whole test run, shared by every class in <see cref="DatabaseCollection"/>.
/// With <c>BASERA_TEST_PG</c> set (a connection string to a server you run), a fresh
/// <c>basera_test_&lt;short guid&gt;</c> database is created on it and dropped at the end.
/// Otherwise a Testcontainers PostgreSQL container is started, which needs Docker.
/// Either way every module's migrations are applied before the first test.
/// </summary>
public sealed class TestDatabase(IMessageSink diagnostics) : IAsyncLifetime
{
    public const string ServerVariable = "BASERA_TEST_PG";
    private const string ContainerImage = "postgres:16";

    private PostgreSqlContainer? container;
    private string? maintenanceConnectionString;

    /// <summary>Connection string to the test database; give it to <see cref="BaseraWebFactory"/>.</summary>
    public string ConnectionString { get; private set; } = "";

    public string DatabaseName { get; private set; } = "";

    /// <summary>True when running on the server named by <c>BASERA_TEST_PG</c> rather than a container.</summary>
    public bool IsLocalServer => maintenanceConnectionString is not null;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            await StartContainerAsync();
        }
        else
        {
            await CreateOnLocalServerAsync(server);
        }

        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
            Log($"Stopped PostgreSQL container ({ContainerImage}).");
            return;
        }

        if (maintenanceConnectionString is null)
        {
            return;
        }

        // Pooled connections would keep the database busy; WITH (FORCE) (PostgreSQL 13+) ends any that remain.
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(maintenanceConnectionString, $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)");
        Log($"Dropped test database {DatabaseName}.");
    }

    private async Task CreateOnLocalServerAsync(string server)
    {
        var builder = new NpgsqlConnectionStringBuilder(server);

        // Connect to the database named in the variable (normally "postgres") to create and drop ours.
        builder.Database = string.IsNullOrEmpty(builder.Database) ? "postgres" : builder.Database;
        maintenanceConnectionString = builder.ConnectionString;

        DatabaseName = $"basera_test_{Guid.NewGuid():N}"[..20];
        await ExecuteAsync(maintenanceConnectionString, $"CREATE DATABASE \"{DatabaseName}\" ENCODING 'UTF8'");

        builder.Database = DatabaseName;
        ConnectionString = builder.ConnectionString;
        Log($"Created test database {DatabaseName} on {builder.Host}:{builder.Port} ({ServerVariable}).");
    }

    private async Task StartContainerAsync()
    {
        try
        {
            container = new PostgreSqlBuilder(ContainerImage).Build();
            await container.StartAsync();
        }
        catch (DockerUnavailableException ex)
        {
            throw new InvalidOperationException(
                $"Integration tests need PostgreSQL. Either set {ServerVariable} to a connection string for a local " +
                "server (see docs/ARCHITECTURE.md section 11) or install and start Docker for Testcontainers.", ex);
        }

        ConnectionString = container.GetConnectionString();
        DatabaseName = new NpgsqlConnectionStringBuilder(ConnectionString).Database!;
        Log($"Started PostgreSQL container ({ContainerImage}), database {DatabaseName}.");
    }

    /// <summary>
    /// Boots the host against the test database and migrates every DbContext the modules registered,
    /// so a new module's migrations are picked up without touching this class.
    /// </summary>
    private async Task ApplyMigrationsAsync()
    {
        await using var factory = new BaseraWebFactory(ConnectionString);
        using var scope = factory.Services.CreateScope();
        foreach (var contextType in factory.DbContextTypes)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            await context.Database.MigrateAsync();
            Log($"Migrated {contextType.Name}.");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // Shown in `dotnet test` output because xunit.runner.json enables diagnostic messages.
    private void Log(string message) => diagnostics.OnMessage(new DiagnosticMessage($"[TestDatabase] {message}"));
}
