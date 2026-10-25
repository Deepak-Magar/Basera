using Basera.Modules.Identity.Domain;
using Basera.Shared.Domain;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// The <c>identity</c> schema: tenants, users, memberships and SMS codes. It is a <see cref="TenantDbContext"/>, so
/// users and memberships are filtered by the current tenant like any module's rows. Because it cannot
/// also derive from Identity's <c>IdentityUserContext</c>, it maps the user, claim, login and token tables
/// itself (the same shape as <c>IdentityUserContext.OnModelCreating</c>), and <c>AddEntityFrameworkStores</c>
/// picks <c>UserOnlyStore</c> for it. Identity roles are not used: the role is on <see cref="TenantMembership"/>.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<TenantMembership> Memberships => Set<TenantMembership>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    protected override string Schema => "identity";

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(tenant =>
        {
            tenant.ToTable("tenant");
            tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
            tenant.Property(t => t.LegalName).HasMaxLength(Tenant.NameMaxLength);
            tenant.Property(t => t.Pan).HasMaxLength(9);
            tenant.Property(t => t.Address).HasMaxLength(300);
            tenant.Property(t => t.Phone).HasMaxLength(16)
                .HasConversion(phone => phone.E164, e164 => PhoneNumber.Create(e164).Value);
            tenant.Property(t => t.LogoPath).HasMaxLength(300);
            tenant.Property(t => t.Plan).HasConversion<string>().HasMaxLength(20);
            tenant.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ApplicationUser>(user =>
        {
            user.ToTable("application_user");
            user.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
            user.Property(u => u.UserName).HasMaxLength(256);
            user.Property(u => u.NormalizedUserName).HasMaxLength(256);
            user.Property(u => u.Email).HasMaxLength(256);
            user.Property(u => u.NormalizedEmail).HasMaxLength(256);
            user.Property(u => u.PhoneNumber).HasMaxLength(16);
            user.Property(u => u.DisplayName).HasMaxLength(ApplicationUser.DisplayNameMaxLength);
            user.Property(u => u.PreferredLanguage).HasMaxLength(5);

            // Unique across tenants, not per tenant: a login (phone or email) must name exactly one user.
            user.HasIndex(u => u.NormalizedUserName).IsUnique();
            user.HasIndex(u => u.NormalizedEmail).IsUnique();
            user.HasIndex(u => u.TenantId);
            user.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);

            user.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(claim => claim.UserId).IsRequired();
            user.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(login => login.UserId).IsRequired();
            user.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(token => token.UserId).IsRequired();
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(claim =>
        {
            claim.ToTable("user_claim");
            claim.HasKey(c => c.Id);
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(login =>
        {
            login.ToTable("user_login");
            login.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(token =>
        {
            token.ToTable("user_token");
            token.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        });

        modelBuilder.Entity<TenantMembership>(membership =>
        {
            membership.ToTable("tenant_membership");
            membership.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            membership.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
            membership.HasIndex(m => m.UserId);
            membership.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
            membership.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OtpChallenge>(otp =>
        {
            otp.ToTable("otp_challenge");
            otp.Property(o => o.Phone).HasMaxLength(16);
            otp.Property(o => o.Purpose).HasConversion<string>().HasMaxLength(20);
            otp.Property(o => o.CodeHash).HasMaxLength(64);
            // The send limit counts a phone's recent codes; verification takes its newest one.
            otp.HasIndex(o => new { o.Phone, o.CreatedAt });
        });
    }
}
