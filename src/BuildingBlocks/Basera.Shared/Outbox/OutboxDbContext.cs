using Microsoft.EntityFrameworkCore;

namespace Basera.Shared.Outbox;

/// <summary>
/// Owns the <c>outbox</c> schema and its migrations, and is what <see cref="OutboxProcessor"/> reads with.
/// Not a TenantDbContext: the processor works across tenants and sets each message's tenant for its handlers.
/// </summary>
public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> Messages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(OutboxMessageConfiguration.Schema);
        OutboxMessageConfiguration.Map(modelBuilder, excludeFromMigrations: false);
    }
}
