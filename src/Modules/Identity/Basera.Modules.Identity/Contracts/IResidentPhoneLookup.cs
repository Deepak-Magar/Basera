using Basera.Shared.Domain;

namespace Basera.Modules.Identity.Contracts;

/// <summary>Whether a phone number belongs to a resident or to a guardian.</summary>
public enum PhoneHolderKind
{
    Resident,
    Parent,
}

/// <summary>
/// A resident or guardian whose phone a hostel has recorded: who may sign in with an SMS code, in which
/// tenant, and as what. <paramref name="PersonId"/> is the Resident or Guardian record's id.
/// </summary>
public sealed record PhoneHolder(Guid TenantId, Guid PersonId, PhoneHolderKind Kind, string DisplayName);

/// <summary>
/// What Identity needs to know at OTP sign-in (ARCHITECTURE §7: "linked to the Resident or Guardian record by
/// phone"). Identity owns this contract because Identity is the one asking; the Residents module implements
/// it and registers its implementation, replacing Identity's stub (<c>services.Replace</c> or a plain
/// <c>AddScoped</c>: modules are added in name order, so Identity's <c>TryAdd</c> has already run). Asked
/// before anyone is signed in, so the implementation searches across tenants; a phone belongs to at most
/// one person, as it does to at most one user.
/// </summary>
public interface IResidentPhoneLookup
{
    Task<PhoneHolder?> FindAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
}
