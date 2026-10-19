using Basera.Shared.Events;
using Basera.Shared.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";
    private const string PasswordPlaceholder = "__SET_IN_USER_SECRETS__";

    /// <summary>
    /// Registers a module's <see cref="TenantDbContext"/> on the shared PostgreSQL database with snake_case
    /// names and the tenant, audit and domain event interceptors. Modules call this from <c>AddModule</c>.
    /// The connection string is read when the first context is created, so the host starts without one.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : TenantDbContext
    {
        services.AddDbContext<TContext>((provider, options) => options
            .UseNpgsql(GetConnectionString(configuration))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                provider.GetRequiredService<TenantInterceptor>(),
                provider.GetRequiredService<AuditInterceptor>(),
                provider.GetRequiredService<DomainEventInterceptor>()));
        return services;
    }

    /// <summary>The outbox table and the processor that delivers it. Hangfire (<c>AddBaseraJobs</c>) runs the processor.</summary>
    public static IServiceCollection AddBaseraOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OutboxDbContext>(options => options
            .UseNpgsql(
                GetConnectionString(configuration),
                npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "outbox"))
            .UseSnakeCaseNamingConvention());
        services.AddTransient<OutboxProcessor>();
        return services;
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains(PasswordPlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not configured. In Development set it as a user secret: " +
                $"dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"Host=localhost;Port=5432;Database=basera;" +
                "Username=postgres;Password=<your password>\" --project src/Basera.Web");
        }

        return connectionString;
    }
}
