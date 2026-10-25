using System.Security.Cryptography;
using System.Text;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Messaging;
using Basera.Shared.Results;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Application;

/// <summary>What the invitee sees before accepting.</summary>
public sealed record InvitationPreview(string HostelName, Role Role, PhoneNumber Phone);

/// <summary>
/// Staff invitations (PRODUCT_SPEC §4.1): the owner invites a phone with a role, the SMS carries a link, and
/// the invitee sets a name and password, which creates the membership.
/// <para>
/// The link's token is <c>&lt;tenant id, 32 hex&gt;&lt;secret, 43 base64url&gt;</c>. Opening it sets the tenant
/// from the token and then finds the invitation by the secret's hash with the normal tenant filter, so the
/// anonymous accept path needs no <c>IgnoreQueryFilters</c>. A forged tenant id finds nothing.
/// </para>
/// </summary>
public sealed class InvitationService(
    IdentityDbContext db,
    TenantContext tenantContext,
    SignInLookup lookup,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IUserStore<ApplicationUser> userStore,
    ISmsSender sms,
    IClock clock)
{
    private const int SecretBytes = 32;
    private const int TokenLength = 32 + 43;

    /// <summary>Invites <paramref name="phoneInput"/>; <paramref name="linkFor"/> turns a token into the absolute URL sent by SMS.</summary>
    public async Task<Result> InviteAsync(string? phoneInput, Role role, Func<string, string> linkFor, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumber.Create(phoneInput);
        if (phone.IsFailure)
        {
            return phone.Error!;
        }

        var tenantId = tenantContext.RequiredTenantId;
        var existing = await lookup.FindAsync(phone.Value.E164, cancellationToken);
        if (existing is not null && existing.TenantId != tenantId)
        {
            return DomainError.Validation("This number already has a Basera account with another hostel.", "phone");
        }

        if (existing is not null && await db.Memberships.AnyAsync(m => m.UserId == existing.UserId, cancellationToken))
        {
            return DomainError.Validation("This number is already on your staff.", "phone");
        }

        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var now = clock.UtcNow;
        var invitation = StaffInvitation.Create(phone.Value, role, Hash(secret), now);
        if (invitation.IsFailure)
        {
            return invitation.Error!;
        }

        // Only the newest invitation to a phone works.
        var older = await db.Invitations
            .Where(i => i.Phone == phone.Value.E164 && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(cancellationToken);
        older.ForEach(i => i.Revoke(now));

        db.Invitations.Add(invitation.Value);
        await db.SaveChangesAsync(cancellationToken);

        var hostel = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).SingleAsync(cancellationToken);
        var link = linkFor($"{tenantId:N}{secret}");
        await sms.SendAsync(
            new SmsMessage(phone.Value, $"{hostel} invited you to Basera as {role}. Set your password within 7 days: {link}", "invite"),
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeAsync(StaffInvitation invitation, CancellationToken cancellationToken = default)
    {
        invitation.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<StaffInvitation?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Invitations.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <summary>The pending invitation behind a link, or null for a malformed, used, revoked or expired one.</summary>
    public async Task<InvitationPreview?> PreviewAsync(string? token, CancellationToken cancellationToken = default)
    {
        var invitation = await FindPendingAsync(token, cancellationToken);
        if (invitation is null)
        {
            return null;
        }

        var hostel = await db.Tenants.Where(t => t.Id == invitation.TenantId).Select(t => t.Name).SingleAsync(cancellationToken);
        return new InvitationPreview(hostel, invitation.Role, PhoneNumber.Create(invitation.Phone).Value);
    }

    /// <summary>
    /// Creates the user (or reuses the tenant's former staff user with this phone), sets the password, adds the
    /// membership and marks the invitation used, all in one <c>SaveChanges</c>, then signs the new staff member in.
    /// The link reached the phone by SMS, so the phone counts as verified.
    /// </summary>
    public async Task<Result<Role>> AcceptAsync(string? token, string? displayName, string? password, CancellationToken cancellationToken = default)
    {
        var invitation = await FindPendingAsync(token, cancellationToken);
        if (invitation is null)
        {
            return DomainError.RuleViolation("This invitation is not valid any more. Ask the owner to send a new one.");
        }

        var now = clock.UtcNow;
        var candidate = await lookup.FindAsync(invitation.Phone, cancellationToken);
        if (candidate is not null && candidate.TenantId != invitation.TenantId)
        {
            return DomainError.RuleViolation("This number already has a Basera account with another hostel.");
        }

        userStore.HoldChanges();
        ApplicationUser user;
        if (candidate is null)
        {
            user = new ApplicationUser { UserName = invitation.Phone, PhoneNumber = invitation.Phone };
            user.DisplayName = displayName?.Trim() ?? "";
            user.PhoneNumberConfirmed = true;
            user.PhoneVerifiedAt = now;
            var created = await users.CreateAsync(user, password ?? "");
            if (!created.Succeeded)
            {
                return PasswordError(created);
            }
        }
        else
        {
            user = (await users.FindByIdAsync(candidate.UserId.ToString()))!;
            if (await db.Memberships.AnyAsync(m => m.UserId == user.Id, cancellationToken))
            {
                return DomainError.RuleViolation("This number is already on the staff. Sign in instead.");
            }

            var valid = await ValidatePasswordAsync(user, password ?? "");
            if (valid.IsFailure)
            {
                return valid.Error!;
            }

            user.DisplayName = displayName?.Trim() ?? "";
            user.PhoneVerifiedAt ??= now;
            user.PasswordHash = users.PasswordHasher.HashPassword(user, password!);
            // One store update (a new security stamp signs out any old session) saved with the membership below.
            await users.UpdateSecurityStampAsync(user);
        }

        invitation.Accept(now);
        db.Memberships.Add(new TenantMembership(user.Id, invitation.Role));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return DomainError.Conflict("This invitation was just used. Sign in instead.");
        }

        await signIn.SignInAsync(user, isPersistent: false);
        return invitation.Role;
    }

    private async Task<StaffInvitation?> FindPendingAsync(string? token, CancellationToken cancellationToken)
    {
        if (token is not { Length: TokenLength } || !Guid.TryParseExact(token[..32], "N", out var tenantId) || tenantId == Guid.Empty)
        {
            return null;
        }

        if (tenantContext.TenantId is { } current && current != tenantId)
        {
            return null;
        }

        tenantContext.Set(tenantId);
        var hash = Hash(token[32..]);
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        return invitation?.StateAt(clock.UtcNow) == InvitationState.Pending ? invitation : null;
    }

    private async Task<Result> ValidatePasswordAsync(ApplicationUser user, string password)
    {
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded)
            {
                return PasswordError(result);
            }
        }

        return Result.Success();
    }

    private static DomainError PasswordError(IdentityResult result) =>
        DomainError.Validation(string.Join(" ", result.Errors.Select(e => e.Description)), "password");

    // The secret has 256 random bits, so a plain SHA-256 can't be reversed; no key is needed (unlike 6-digit OTPs).
    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
}
