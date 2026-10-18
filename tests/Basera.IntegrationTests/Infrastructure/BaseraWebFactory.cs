using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Basera.IntegrationTests.Infrastructure;

/// <summary>The real host, pointed at a test database instead of the one in user secrets.</summary>
public sealed class BaseraWebFactory(string connectionString) : WebApplicationFactory<Program>
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
        // UseSetting wins over appsettings and user secrets.
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.ConfigureServices(services => dbContextTypes = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsSubclassOf(typeof(DbContext)))
            .Distinct()
            .ToArray());
    }
}
