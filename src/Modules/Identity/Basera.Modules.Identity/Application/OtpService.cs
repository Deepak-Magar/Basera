using System.Security.Cryptography;
using System.Text;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Messaging;
using Basera.Shared.Results;
using Basera.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Basera.Modules.Identity.Application;

/// <summary><c>Identity:Otp</c>. <see cref="HashKey"/> is base64 of at least 32 random bytes, kept secret like a password.</summary>
public sealed class OtpOptions
{
    public string? HashKey { get; set; }
}

/// <summary>
/// Sends and checks SMS codes with the limits of ARCHITECTURE §7: 6 digits, valid 5 minutes, 5 attempts per
/// code, at most 3 codes per phone in any 10 minutes (all purposes together, because each one is a paid SMS).
/// Codes are stored as HMAC-SHA256 under a server key: six digits are a million possibilities, so a plain
/// hash could be reversed from a database copy in a moment, but without the key it can't.
/// </summary>
public sealed class OtpService(IdentityDbContext db, ISmsSender sms, IClock clock, IOptions<OtpOptions> options)
{
    public async Task<Result> SendAsync(PhoneNumber phone, OtpPurpose purpose, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var windowStart = now - OtpChallenge.SendWindow;
        var recent = await db.OtpChallenges
            .Where(c => c.Phone == phone.E164 && c.CreatedAt > windowStart)
            .ToListAsync(cancellationToken);
        if (recent.Count >= OtpChallenge.MaxSendsPerWindow)
        {
            return DomainError.RuleViolation("Too many codes sent to this number. Wait 10 minutes and try again.", "phone");
        }

        foreach (var older in recent.Where(c => c.Purpose == purpose))
        {
            older.Supersede(now);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.OtpChallenges.Add(OtpChallenge.Issue(phone, purpose, Hash(phone, purpose, code), now));
        await db.SaveChangesAsync(cancellationToken);

        // English keeps it to one GSM-7 SMS; Nepali would be UCS-2 and could cost two.
        await sms.SendAsync(
            new SmsMessage(phone, $"Basera code: {code}. Valid for 5 minutes. Do not share it with anyone.", "otp"),
            cancellationToken);
        return Result.Success();
    }

    /// <summary>Checks <paramref name="code"/> against the newest unused code for this phone and purpose.</summary>
    public async Task<Result> VerifyAsync(PhoneNumber phone, OtpPurpose purpose, string? code, CancellationToken cancellationToken = default)
    {
        var challenge = await db.OtpChallenges
            .Where(c => c.Phone == phone.E164 && c.Purpose == purpose && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (challenge is null)
        {
            return DomainError.Validation("This code is not valid any more. Request a new one.", "code");
        }

        var normalized = NepaliDigits.ToAscii(code?.Trim() ?? "");
        var matches = normalized.Length == OtpChallenge.CodeLength && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(phone, purpose, normalized)), Encoding.ASCII.GetBytes(challenge.CodeHash));
        var check = challenge.Check(matches, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return check switch
        {
            OtpCheck.Verified => Result.Success(),
            OtpCheck.WrongCode => DomainError.Validation("That code is not right. Check the SMS and try again.", "code"),
            OtpCheck.TooManyAttempts => DomainError.Validation("Too many wrong tries. Request a new code.", "code"),
            _ => DomainError.Validation("This code is not valid any more. Request a new one.", "code"),
        };
    }

    // The purpose and phone are part of the MAC, so a code can't be moved to another number or flow.
    private string Hash(PhoneNumber phone, OtpPurpose purpose, string code)
    {
        var key = Convert.FromBase64String(options.Value.HashKey ?? throw MissingKey());
        if (key.Length < 32)
        {
            throw MissingKey();
        }

        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{phone.E164}|{purpose}|{code}"));
        return Convert.ToHexString(mac);
    }

    private static InvalidOperationException MissingKey() => new(
        "Identity:Otp:HashKey must be base64 of at least 32 random bytes. Set it as a secret, e.g. " +
        "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).");
}
