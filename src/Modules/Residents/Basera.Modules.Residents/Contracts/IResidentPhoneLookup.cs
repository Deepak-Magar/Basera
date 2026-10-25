using Basera.Shared.Domain;

namespace Basera.Modules.Residents.Contracts;

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
/// Identity's question to Residents at OTP sign-in (ARCHITECTURE §7: "linked to the Resident or Guardian
/// record by phone"). Asked before anyone is signed in, so the implementation searches across tenants;
/// a phone belongs to at most one person, as it does to at most one user.
/// </summary>
public interface IResidentPhoneLookup
{
    Task<PhoneHolder?> FindAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
}
