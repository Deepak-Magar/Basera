using Basera.Shared.Domain;

namespace Basera.Shared.Messaging;

/// <summary>
/// Sends one SMS (OTP, receipt, reminder: PRODUCT_SPEC §4.8). Development and tests use
/// <see cref="MockSmsSender"/>; the Sparrow gateway adapter comes with "real SMS" in phase two.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

/// <summary><paramref name="Purpose"/> is a short tag for logs and cost reports, e.g. "otp" or "receipt".</summary>
public sealed record SmsMessage(PhoneNumber To, string Text, string Purpose)
{
    /// <summary>How many SMS the gateway will bill for this text.</summary>
    public int Segments => SmsSegments.Count(Text);
}

/// <summary>
/// SMS length rules: GSM-7 text fits 160 characters in one SMS (153 per part when split); anything else,
/// including Devanagari, is sent as UCS-2 at 70 (67 per part). A Nepali reminder costs more than an English one.
/// </summary>
public static class SmsSegments
{
    private const string Gsm7 =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?" +
        "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    private const string Gsm7Extended = "^{}\\[~]|€";

    public static int Count(string text)
    {
        if (text.Length == 0)
        {
            return 1;
        }

        if (text.All(c => Gsm7.Contains(c) || Gsm7Extended.Contains(c)))
        {
            // Extended characters take two septets.
            var septets = text.Sum(c => Gsm7Extended.Contains(c) ? 2 : 1);
            return septets <= 160 ? 1 : (int)Math.Ceiling(septets / 153.0);
        }

        return text.Length <= 70 ? 1 : (int)Math.Ceiling(text.Length / 67.0);
    }
}
