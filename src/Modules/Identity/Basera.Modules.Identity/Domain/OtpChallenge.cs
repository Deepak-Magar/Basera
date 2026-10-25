using Basera.Shared.Domain;

namespace Basera.Modules.Identity.Domain;

/// <summary>What a one-time code is for; a code issued for one purpose never works for another.</summary>
public enum OtpPurpose
{
    /// <summary>Resident or parent sign-in (Feature 4).</summary>
    Login,

    /// <summary>Forgotten password (owners and staff).</summary>
    PasswordReset,
}

public enum OtpCheck
{
    Verified,
    WrongCode,
    Expired,
    TooManyAttempts,
    AlreadyUsed,
}

/// <summary>
/// One SMS code sent to one phone (ARCHITECTURE §7: 6 digits, 5 minutes, 5 attempts). Only a keyed hash of
/// the code is stored. Not tenant-owned: codes are sent and checked before anyone is signed in, and they are
/// keyed by phone, which is unique across tenants.
/// </summary>
public sealed class OtpChallenge : Entity
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public const int MaxSendsPerWindow = 3;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SendWindow = TimeSpan.FromMinutes(10);

    private OtpChallenge(string phone, OtpPurpose purpose, string codeHash, DateTimeOffset now)
    {
        Phone = phone;
        Purpose = purpose;
        CodeHash = codeHash;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
    }

    // For EF Core.
    private OtpChallenge()
    {
        Phone = "";
        CodeHash = "";
    }

    /// <summary>E.164.</summary>
    public string Phone { get; private set; }

    public OtpPurpose Purpose { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Set when the code was used, or replaced by a newer one.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static OtpChallenge Issue(PhoneNumber phone, OtpPurpose purpose, string codeHash, DateTimeOffset now) =>
        new(phone.E164, purpose, codeHash, now);

    /// <summary>
    /// Counts one attempt. The caller compares hashes; this decides what the comparison means. An attempt
    /// on a spent, expired or exhausted code is refused without counting, and a correct code after five wrong
    /// ones is still refused.
    /// </summary>
    public OtpCheck Check(bool codeMatches, DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            return OtpCheck.AlreadyUsed;
        }

        if (now >= ExpiresAt)
        {
            return OtpCheck.Expired;
        }

        if (Attempts >= MaxAttempts)
        {
            return OtpCheck.TooManyAttempts;
        }

        Attempts++;
        if (!codeMatches)
        {
            return Attempts >= MaxAttempts ? OtpCheck.TooManyAttempts : OtpCheck.WrongCode;
        }

        ConsumedAt = now;
        return OtpCheck.Verified;
    }

    /// <summary>A newer code was sent to the same phone for the same purpose; only the newest one works.</summary>
    public void Supersede(DateTimeOffset now) => ConsumedAt ??= now;
}
