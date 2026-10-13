using System.Globalization;
using System.Text;

namespace Basera.Shared.Domain;

/// <summary>
/// An amount in Nepali rupees, held as whole paisa (1 rupee = 100 paisa) in a <see cref="long"/>
/// so there is never a rounding question about what is stored (ADR-009). Negative amounts are allowed
/// (reversals, balances). Arithmetic is checked: overflow throws instead of wrapping.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private const string Symbol = "रु";

    private Money(long paisa)
    {
        Paisa = paisa;
    }

    public static Money Zero => default;

    public long Paisa { get; }

    public decimal Rupees => Paisa / 100m;

    public bool IsZero => Paisa == 0;

    public bool IsNegative => Paisa < 0;

    public static Money FromPaisa(long paisa) => new(paisa);

    /// <summary>Creates money from a rupee amount with at most two decimals; more precision is an error, not a silent rounding.</summary>
    public static Money FromRupees(decimal rupees)
    {
        var paisa = rupees * 100m;
        if (paisa != decimal.Truncate(paisa))
        {
            throw new ArgumentException($"{rupees} has fractions of a paisa.", nameof(rupees));
        }

        return new Money(decimal.ToInt64(paisa));
    }

    public Money Add(Money other) => new(checked(Paisa + other.Paisa));

    public Money Subtract(Money other) => new(checked(Paisa - other.Paisa));

    public Money Multiply(long quantity) => new(checked(Paisa * quantity));

    /// <summary>Multiplies by a factor and rounds to the nearest paisa, halves away from zero.</summary>
    public Money Multiply(decimal factor) => new(RoundToLong(Paisa * factor));

    /// <summary><c>Percent(10)</c> is 10% of this amount, rounded to the nearest paisa.</summary>
    public Money Percent(decimal percent) => Multiply(percent / 100m);

    /// <summary>Rounds to the nearest whole rupee, halves away from zero (PRODUCT_SPEC §6 rule 1).</summary>
    public Money RoundToRupee() => new(checked(RoundToLong(Paisa / 100m) * 100));

    /// <summary>
    /// Splits into <paramref name="parts"/> amounts that differ by at most one paisa and add up exactly
    /// to this amount. The first parts receive the leftover paisa: 100.00 / 3 = 33.34, 33.33, 33.33.
    /// </summary>
    public IReadOnlyList<Money> Allocate(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);

        var share = Paisa / parts;
        var remainder = Paisa % parts; // same sign as Paisa
        var step = Math.Sign(remainder);
        var result = new Money[parts];
        for (var i = 0; i < parts; i++)
        {
            result[i] = new Money(share + (i < Math.Abs(remainder) ? step : 0));
        }

        return result;
    }

    /// <summary>
    /// "रु 12,500.00" using Nepali/Indian digit grouping (रु 1,25,000.00), or "रु १२,५००.००" with
    /// <paramref name="nepaliDigits"/>. Negative amounts are written "-रु 500.00".
    /// </summary>
    public string Format(bool nepaliDigits = false)
    {
        var magnitude = Paisa < 0 ? (ulong)-(Paisa + 1) + 1 : (ulong)Paisa;
        var text = $"{Symbol} {GroupDigits(magnitude / 100)}.{magnitude % 100:D2}";
        if (Paisa < 0)
        {
            text = "-" + text;
        }

        return nepaliDigits ? NepaliDigits.Convert(text) : text;
    }

    public override string ToString() => Format();

    public int CompareTo(Money other) => Paisa.CompareTo(other.Paisa);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money money) => new(checked(-money.Paisa));

    public static Money operator *(Money money, long quantity) => money.Multiply(quantity);

    public static bool operator <(Money left, Money right) => left.Paisa < right.Paisa;

    public static bool operator >(Money left, Money right) => left.Paisa > right.Paisa;

    public static bool operator <=(Money left, Money right) => left.Paisa <= right.Paisa;

    public static bool operator >=(Money left, Money right) => left.Paisa >= right.Paisa;

    private static long RoundToLong(decimal value) =>
        decimal.ToInt64(Math.Round(value, 0, MidpointRounding.AwayFromZero));

    // 1234567 -> "12,34,567": last three digits, then groups of two.
    private static string GroupDigits(ulong rupees)
    {
        var digits = rupees.ToString(CultureInfo.InvariantCulture);
        if (digits.Length <= 3)
        {
            return digits;
        }

        var head = digits[..^3];
        var builder = new StringBuilder();
        for (var i = 0; i < head.Length; i++)
        {
            if (i > 0 && (head.Length - i) % 2 == 0)
            {
                builder.Append(',');
            }

            builder.Append(head[i]);
        }

        return builder.Append(',').Append(digits[^3..]).ToString();
    }
}
