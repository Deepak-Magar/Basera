namespace Basera.Shared.Tenancy;

/// <summary>
/// Tenant-owned data was read or written with no tenant in scope. This is a bug (a missing claim, or a
/// job that forgot to set the tenant), so it is an exception rather than a <c>Result</c>.
/// </summary>
public sealed class TenantNotResolvedException()
    : InvalidOperationException("No tenant is set for this request or job, so tenant-owned data cannot be used.");
