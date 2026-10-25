using Basera.Shared.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.DataProtection;

public static class BaseraDataProtection
{
    /// <summary>
    /// Every Basera instance shares this name, so they all read the same purpose-isolated keys. Without it
    /// the name defaults to the content root path, and two hosts in different folders could not read each
    /// other's cookies. Changing it signs everyone out.
    /// </summary>
    public const string ApplicationName = "Basera";

    /// <summary>
    /// Data Protection with its key ring in <c>dataprotection.data_protection_key</c> (ARCHITECTURE §6, §12).
    /// The table is created by this context's migration, like every other schema; keys are read and written
    /// on first use, not at startup.
    /// </summary>
    public static IServiceCollection AddBaseraDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DataProtectionKeysDbContext>(options => options
            .UseNpgsql(
                PersistenceServiceCollectionExtensions.GetConnectionString(configuration),
                npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, DataProtectionKeysDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<DataProtectionKeysDbContext>();
        return services;
    }
}
