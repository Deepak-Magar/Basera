using Basera.Shared.Domain;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Basera.IntegrationTests.Tenancy;

/// <summary>
/// A module-shaped DbContext that exists only in the tests, so the TenantDbContext building block can be
/// proven against PostgreSQL before any real module owns tables. Its tables are created directly, not migrated.
/// </summary>
public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public const string SchemaName = "tenancy_probe";
    public const string Table = $"{SchemaName}.probe_note";

    public DbSet<ProbeNote> Notes => Set<ProbeNote>();

    protected override string Schema => SchemaName;

    /// <summary>Creates the probe table once per test database.</summary>
    public static async Task EnsureCreatedAsync(IServiceProvider services, string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var exists = new NpgsqlCommand($"SELECT to_regclass('{Table}') IS NOT NULL", connection);
        if ((bool)(await exists.ExecuteScalarAsync())!)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

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

/// <summary>Tenant-owned, audited, soft-deletable and raising events, like a Resident.</summary>
public sealed class ProbeNote : Entity, ITenantOwned, IAuditable, ISoftDeletable
{
    public ProbeNote(string text, Money amount)
    {
        Text = text;
        Amount = amount;
    }

    public Guid TenantId { get; private set; }

    public string Text { get; set; }

    public Money Amount { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Raises a reliable event (goes through the outbox).</summary>
    public void Publish() => Raise(new ProbeNotePublished(Id, Text, Amount));

    /// <summary>Raises an in-process event (handled right after the save).</summary>
    public void Rename(string text)
    {
        Text = text;
        Raise(new ProbeNoteRenamed(Id, text));
    }
}

[Reliable]
public sealed record ProbeNotePublished(Guid NoteId, string Text, Money Amount) : IDomainEvent;

public sealed record ProbeNoteRenamed(Guid NoteId, string Text) : IDomainEvent;
