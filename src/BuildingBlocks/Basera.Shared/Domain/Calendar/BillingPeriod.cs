using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Basera.Shared.Domain.Calendar;

/// <summary>
/// One Bikram Sambat month: the unit Basera bills in (PRODUCT_SPEC §4.4).
/// Stored as its first day, so <c>default(BillingPeriod)</c> is the valid period 2000-01.
/// </summary>
public readonly record struct BillingPeriod : IComparable<BillingPeriod>
{
    private readonly NepaliDate _start;

    public BillingPeriod(int year, int month)
    {
        _start = new NepaliDate(year, month, 1);
    }

    public int Year => _start.Year;

    public int Month => _start.Month;

    public NepaliDate Start => _start;

    public NepaliDate End => _start.LastDayOfMonth();

    public int Days => _start.DaysInMonth;

    public DateOnly StartAd => Start.ToDateOnly();

    public DateOnly EndAd => End.ToDateOnly();

    public static BillingPeriod Containing(NepaliDate date) => new(date.Year, date.Month);

    public static BillingPeriod Containing(DateOnly date) => Containing(NepaliDate.FromDateOnly(date));

    public BillingPeriod Next() => Containing(_start.AddMonths(1));

    public BillingPeriod Previous() => Containing(_start.AddMonths(-1));

    public bool Contains(NepaliDate date) => date.Year == Year && date.Month == Month;

    public bool Contains(DateOnly date) => date >= StartAd && date <= EndAd;

    public string MonthName(bool nepali = false) => _start.MonthName(nepali);

    /// <summary>"2081-01".</summary>
    public override string ToString() => $"{Year:D4}-{Month:D2}";

    /// <summary>Parses "yyyy-MM" written with ASCII or Nepali digits.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out BillingPeriod period)
    {
        period = default;
        var parts = NepaliDigits.ToAscii(text?.Trim() ?? string.Empty).Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !NepaliCalendar.IsValid(year, month, 1))
        {
            return false;
        }

        period = new BillingPeriod(year, month);
        return true;
    }

    public int CompareTo(BillingPeriod other) => _start.CompareTo(other._start);

    public static bool operator <(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) < 0;

    public static bool operator >(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) > 0;

    public static bool operator <=(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BillingPeriod left, BillingPeriod right) => left.CompareTo(right) >= 0;
}
