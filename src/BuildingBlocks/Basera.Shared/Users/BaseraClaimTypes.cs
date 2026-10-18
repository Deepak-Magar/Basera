namespace Basera.Shared.Users;

/// <summary>Claim names Basera puts in the auth cookie (ARCHITECTURE §7).</summary>
public static class BaseraClaimTypes
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string Name = "name";
    public const string Language = "lang";
}
