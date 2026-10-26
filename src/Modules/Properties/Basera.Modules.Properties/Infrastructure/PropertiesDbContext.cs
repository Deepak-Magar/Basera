using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Shared.Domain;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Basera.Modules.Properties.Infrastructure;

/// <summary>
/// The <c>properties</c> schema: properties, floors, rooms, beds, bed occupancy history and rate plans.
/// Property, Room and Bed are soft-deleted and carry <c>xmin</c> as a concurrency token (ARCHITECTURE §6).
/// Removing a property or room through EF cascades to its rooms and beds, which the audit interceptor turns
/// into soft deletes. Floors are plain rows: removing one deletes it, and so does removing its property (rooms
/// keep their floor number, so no history is lost). Rate plans and occupancy history are never cascaded
/// (client no-action), so a soft-deleted parent can't take them with it.
/// </summary>
public sealed class PropertiesDbContext(DbContextOptions<PropertiesDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public DbSet<Property> Properties => Set<Property>();

    public DbSet<Floor> Floors => Set<Floor>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Bed> Beds => Set<Bed>();

    public DbSet<BedOccupancy> Occupancies => Set<BedOccupancy>();

    public DbSet<RatePlan> RatePlans => Set<RatePlan>();

    public DbSet<ManagerPropertyAssignment> ManagerAssignments => Set<ManagerPropertyAssignment>();

    protected override string Schema => "properties";

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>(property =>
        {
            property.ToTable("property");
            property.Property(p => p.Name).HasMaxLength(Property.NameMaxLength);
            property.Property(p => p.Address).HasMaxLength(Property.AddressMaxLength);
            property.Property(p => p.ContactPhone).HasMaxLength(16)
                .HasConversion(phone => phone!.E164, e164 => PhoneNumber.Create(e164).Value);
            property.Property(p => p.Rules).HasMaxLength(Property.RulesMaxLength);
            property.Property(p => p.PaymentQrPath).HasMaxLength(300);
            property.Property(p => p.LogoPath).HasMaxLength(300);
            Xmin(property);
            property.HasIndex(p => new { p.TenantId, p.Name });

            property.HasMany(p => p.Floors).WithOne().HasForeignKey(f => f.PropertyId).OnDelete(DeleteBehavior.Cascade);
            property.HasMany(p => p.Rooms).WithOne().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Cascade);
            property.HasMany(p => p.RatePlans).WithOne().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.ClientNoAction);
            property.Navigation(p => p.Floors).UsePropertyAccessMode(PropertyAccessMode.Field);
            property.Navigation(p => p.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field);
            property.Navigation(p => p.RatePlans).UsePropertyAccessMode(PropertyAccessMode.Field);
            property.Ignore(p => p.Beds);
        });

        modelBuilder.Entity<Floor>(floor =>
        {
            floor.ToTable("floor");
            floor.Property(f => f.Label).HasMaxLength(Floor.LabelMaxLength);
            floor.HasIndex(f => new { f.TenantId, f.PropertyId, f.Number }).IsUnique();
        });

        modelBuilder.Entity<Room>(room =>
        {
            room.ToTable("room");
            room.Property(r => r.Number).HasMaxLength(Room.NumberMaxLength);
            room.Property(r => r.Type).HasConversion<string>().HasMaxLength(10);
            room.Property(r => r.Notes).HasMaxLength(Room.NotesMaxLength);
            Xmin(room);
            room.HasIndex(r => new { r.TenantId, r.PropertyId, r.Number }).IsUnique().HasFilter("deleted_at IS NULL");

            room.HasMany(r => r.Beds).WithOne().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Cascade);
            room.Navigation(r => r.Beds).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Bed>(bed =>
        {
            bed.ToTable("bed");
            bed.Property(b => b.Code).HasMaxLength(20);
            bed.Property(b => b.Status).HasConversion<string>().HasMaxLength(12);
            Xmin(bed);
            bed.HasIndex(b => new { b.TenantId, b.PropertyId, b.Code }).IsUnique().HasFilter("deleted_at IS NULL");
            bed.HasIndex(b => new { b.TenantId, b.PropertyId, b.Status });
            bed.HasOne<Property>().WithMany().HasForeignKey(b => b.PropertyId).OnDelete(DeleteBehavior.ClientNoAction);

            bed.HasMany(b => b.Occupancies).WithOne().HasForeignKey(o => o.BedId).OnDelete(DeleteBehavior.ClientNoAction);
            bed.Navigation(b => b.Occupancies).UsePropertyAccessMode(PropertyAccessMode.Field);
            bed.Ignore(b => b.CurrentOccupancy);
        });

        modelBuilder.Entity<BedOccupancy>(occupancy =>
        {
            occupancy.ToTable("bed_occupancy");
            occupancy.Property(o => o.From).HasColumnName("from_date");
            occupancy.Property(o => o.To).HasColumnName("to_date");
            occupancy.Ignore(o => o.IsOpen);
            occupancy.HasIndex(o => new { o.TenantId, o.BedId, o.From });
            occupancy.HasIndex(o => new { o.TenantId, o.StayId });
            // §6 rule 6 in the database too: two requests racing to fill one bed can't both commit.
            occupancy.HasIndex(o => o.BedId).IsUnique().HasFilter("to_date IS NULL").HasDatabaseName("ix_bed_occupancy_one_open_per_bed");
        });

        modelBuilder.Entity<RatePlan>(plan =>
        {
            plan.ToTable("rate_plan");
            plan.Property(p => p.Name).HasMaxLength(RatePlan.NameMaxLength);
            plan.Ignore(p => p.EffectiveFromBs);
            plan.Ignore(p => p.Rates);
            plan.HasIndex(p => new { p.TenantId, p.PropertyId, p.EffectiveFrom }).IsUnique();
        });

        modelBuilder.Entity<ManagerPropertyAssignment>(assignment =>
        {
            assignment.ToTable("manager_property_assignment");
            assignment.HasIndex(a => new { a.TenantId, a.MembershipId, a.PropertyId }).IsUnique();
            assignment.HasOne<Property>().WithMany().HasForeignKey(a => a.PropertyId).OnDelete(DeleteBehavior.ClientNoAction);
        });

        // Ids are GuidV7s set by Entity's constructor. Without this, a room or bed added to a loaded property is
        // found by DetectChanges with its key already set, taken for an existing row (Modified) and never inserted.
        foreach (var type in new[] { typeof(Property), typeof(Floor), typeof(Room), typeof(Bed), typeof(BedOccupancy), typeof(RatePlan), typeof(ManagerPropertyAssignment) })
        {
            modelBuilder.Entity(type).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }

    /// <summary>
    /// PostgreSQL's <c>xmin</c> system column as the row version (ARCHITECTURE §6): every UPDATE carries
    /// "AND xmin = @original", so a stale edit fails instead of silently overwriting. No column is created.
    /// </summary>
    private static void Xmin<T>(EntityTypeBuilder<T> entity)
        where T : class
    {
        entity.Property<uint>("Version").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
    }
}
