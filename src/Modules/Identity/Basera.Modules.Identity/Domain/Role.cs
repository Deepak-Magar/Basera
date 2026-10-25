using Basera.Shared.Authorization;

namespace Basera.Modules.Identity.Domain;

/// <summary>
/// A user's role in one tenant (ARCHITECTURE §7). Stored as text; the names are the <c>role</c> claim
/// values in <see cref="BaseraRoles"/>, which a unit test keeps in step with this enum.
/// </summary>
public enum Role
{
    Owner,
    Manager,
    Warden,
    Cook,
    Resident,
    Parent,
}
