using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.Modules.Identity.Domain;

public enum InvitationState
{
    Pending,
    Accepted,
    Revoked,
    Expired,
}

/// <summary>
/// An owner's invitation for a phone to join the tenant as Manager, Warden or Cook (PRODUCT_SPEC §4.1). The
/// SMS carries a link with a random secret; only its SHA-256 is stored. Valid for 7 days, once.
/// </summary>
public sealed class StaffInvitation : Entity, ITenantOwned, IAuditable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>Roles an owner can invite. Owners are created by registration; residents and parents by OTP.</summary>
    public static readonly IReadOnlyList<Role> InvitableRoles = [Role.Manager, Role.Warden, Role.Cook];

    private StaffInvitation(PhoneNumber phone, Role role, string tokenHash, DateTimeOffset now)
    {
        Phone = phone.E164;
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = now + Lifetime;
    }

    // For EF Core.
    private StaffInvitation()
    {
        Phone = "";
        TokenHash = "";
    }

    public Guid TenantId { get; private set; }

    /// <summary>E.164.</summary>
    public string Phone { get; private set; }

    public Role Role { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<StaffInvitation> Create(PhoneNumber phone, Role role, string tokenHash, DateTimeOffset now) =>
        InvitableRoles.Contains(role)
            ? new StaffInvitation(phone, role, tokenHash, now)
            : DomainError.Validation("Staff can be invited as Manager, Warden or Cook.", "role");

    public InvitationState StateAt(DateTimeOffset now) =>
        AcceptedAt is not null ? InvitationState.Accepted
        : RevokedAt is not null ? InvitationState.Revoked
        : now >= ExpiresAt ? InvitationState.Expired
        : InvitationState.Pending;

    public Result Accept(DateTimeOffset now)
    {
        if (StateAt(now) != InvitationState.Pending)
        {
            return Result.Failure(DomainError.RuleViolation("This invitation is not valid any more. Ask the owner to send a new one."));
        }

        AcceptedAt = now;
        return Result.Success();
    }

    /// <summary>Withdrawn by the owner, or replaced by a newer invitation to the same phone. A used one stays used.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAt is null)
        {
            RevokedAt ??= now;
        }
    }
}
