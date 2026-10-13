using Basera.Shared.Results;

namespace Basera.Shared.Domain;

/// <summary>
/// A Nepal mobile number, stored in E.164 form (+9779841234567). Accepts the ways people type it:
/// "9841234567", "984-1234567", "+977 984 1234567", "00977 9841234567", "977-9841234567".
/// Nepal mobile numbers have 10 digits and start with 97 or 98 (ARCHITECTURE §5).
/// </summary>
public sealed record PhoneNumber
{
    public const string CountryCode = "977";

    private PhoneNumber(string nationalNumber)
    {
        NationalNumber = nationalNumber;
    }

    /// <summary>The 10-digit number without country code, e.g. "9841234567".</summary>
    public string NationalNumber { get; }

    /// <summary>"+9779841234567": the form stored and sent to SMS gateways.</summary>
    public string E164 => $"+{CountryCode}{NationalNumber}";

    public static Result<PhoneNumber> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DomainError.Validation("Enter a mobile number.", "phone");
        }

        var text = NepaliDigits.ToAscii(input.Trim());
        var hasPlus = text.StartsWith('+');
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());

        // Anything other than digits, an optional leading +, and spacing/punctuation people type is rejected.
        if (text.Skip(hasPlus ? 1 : 0).Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '.')))
        {
            return Invalid();
        }

        if (digits.StartsWith("00" + CountryCode, StringComparison.Ordinal) && digits.Length == 15)
        {
            digits = digits[5..];
        }
        else if (digits.StartsWith(CountryCode, StringComparison.Ordinal) && digits.Length == 13)
        {
            digits = digits[3..];
        }
        else if (hasPlus)
        {
            // An international number from another country.
            return Invalid();
        }

        if (digits.Length != 10 || !(digits.StartsWith("97", StringComparison.Ordinal) || digits.StartsWith("98", StringComparison.Ordinal)))
        {
            return Invalid();
        }

        return new PhoneNumber(digits);
    }

    /// <summary>"984-1234567": how numbers are usually written in Nepal.</summary>
    public string ToDisplayString() => $"{NationalNumber[..3]}-{NationalNumber[3..]}";

    /// <summary>"+977 984-1234567".</summary>
    public string ToInternationalString() => $"+{CountryCode} {ToDisplayString()}";

    public override string ToString() => E164;

    private static DomainError Invalid() =>
        DomainError.Validation("Enter a 10-digit Nepal mobile number starting with 97 or 98.", "phone");
}
