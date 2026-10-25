namespace Basera.Modules.Identity.Contracts;

/// <summary>A tenant as other modules and the host's layouts see it.</summary>
public sealed record TenantSummary(Guid Id, string Name);

/// <summary>
/// Reads a tenant's display details (the shell's header, and later document headers in PDFs). Cached
/// briefly, because every page of every area shows the tenant name.
/// </summary>
public interface ITenantDirectory
{
    Task<TenantSummary?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
