using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Basera.Shared.DataProtection;

/// <summary>
/// Owns the <c>dataprotection</c> schema: the key ring that signs auth cookies and anti-forgery tokens.
/// Keeping it in PostgreSQL means a restart, a redeploy or a second instance reads the same keys, so
/// nobody is signed out and no open form fails its token check. Not a TenantDbContext: keys are global.
/// </summary>
public sealed class DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "dataprotection";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // Singular table name per ARCHITECTURE §6: dataprotection.data_protection_key.
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_key");
    }
}
