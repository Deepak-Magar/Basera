using Basera.Shared.Domain;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Basera.IntegrationTests.Tenancy;

/// <summary>
/// A module-shaped DbContext that exists only in the tests, so the TenantDbContext building block can be
/// proven against PostgreSQL before any real module owns tables. Its tables are created directly, not migrated.
/// </summary>
public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public const string SchemaName = "tenancy_probe";

    public DbSet<ProbeNote> Notes => Set<ProbeNote>();

    protected override string Schema => SchemaName;

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeNote>(note =>
        {
            note.ToTable("probe_note");
            note.Property(n => n.Text).HasMaxLength(200);
            note.HasIndex(n => new { n.TenantId, n.CreatedAt });
        });
    }
}

/// <summary>Tenant-owned, audited and soft-deletable, like a Resident.</summary>
public sealed class ProbeNote : ITenantOwned, IAuditable, ISoftDeletable
{
    public ProbeNote(string text, Money amount)
    {
        Id = GuidV7.New();
        Text = text;
        Amount = amount;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Text { get; set; }

    public Money Amount { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }
}
