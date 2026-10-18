using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";
    private const string PasswordPlaceholder = "__SET_IN_USER_SECRETS__";

    /// <summary>
    /// Registers a module's <see cref="TenantDbContext"/> on the shared PostgreSQL database with
    /// snake_case names and the tenant and audit interceptors. Modules call this from <c>AddModule</c>.
    /// The connection string is read when the first context is created, so the host starts without one.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : TenantDbContext
    {
        services.AddDbContext<TContext>((provider, options) => options
            .UseNpgsql(GetConnectionString(configuration))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(provider.GetRequiredService<TenantInterceptor>(), provider.GetRequiredService<AuditInterceptor>()));
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
