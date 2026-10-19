using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Infrastructure;

/// <summary>
/// The real host, pointed at a test database instead of the one in user secrets. The Hangfire server is off
/// unless <paramref name="jobsServer"/> is set, so tests decide when jobs run.
/// </summary>
public sealed class BaseraWebFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null,
    bool jobsServer = false,
    string environment = "Development") : WebApplicationFactory<Program>
{
    private Type[] dbContextTypes = [];

    /// <summary>Every DbContext type the host registered; available once <see cref="WebApplicationFactory{T}.Services"/> is built.</summary>
    public IReadOnlyList<Type> DbContextTypes
    {
        get
        {
            _ = Services;
            return dbContextTypes;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        // UseSetting wins over appsettings and user secrets.
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jobs:ServerEnabled", jobsServer ? "true" : "false");
        builder.ConfigureServices(services => dbContextTypes = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsSubclassOf(typeof(DbContext)))
            .Distinct()
            .ToArray());
        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
