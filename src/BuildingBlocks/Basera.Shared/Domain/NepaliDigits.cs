using System.Text;

namespace Basera.Shared.Domain;

/// <summary>Converts between ASCII digits (0–9) and Devanagari digits (०–९).</summary>
public static class NepaliDigits
{
    private const char NepaliZero = '०';

    /// <summary>Replaces every ASCII digit with its Devanagari digit; other characters are kept.</summary>
    public static string Convert(string text) => Map(text, '0', '9', NepaliZero);

    /// <summary>Replaces every Devanagari digit with its ASCII digit; other characters are kept.</summary>
    public static string ToAscii(string text) => Map(text, NepaliZero, (char)(NepaliZero + 9), '0');

    private static string Map(string text, char from, char to, char targetZero)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c >= from && c <= to ? (char)(targetZero + (c - from)) : c);
        }

        return builder.ToString();
    }
}
