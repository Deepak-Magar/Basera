using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Basera.Shared.Modules;

public static class DevSeeding
{
    /// <summary>The host argument that runs the seeders and exits instead of serving requests.</summary>
    public const string Argument = "seed";

    /// <summary>
    /// Runs every registered <see cref="IDevSeeder"/> in order, each in its own scope. Development only:
    /// demo users with a published password must never reach a real database.
    /// </summary>
    public static async Task RunDevSeedersAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            throw new InvalidOperationException("Dev seed data is only for the Development environment.");
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevSeeding));
        foreach (var seeder in services.GetServices<IDevSeeder>().OrderBy(s => s.Order))
        {
            logger.LogInformation("Dev seed: running {Seeder}.", seeder.GetType().Name);
            await using var scope = services.CreateAsyncScope();
            await seeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }
    }
}
