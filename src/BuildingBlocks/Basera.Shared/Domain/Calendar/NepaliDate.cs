using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Basera.Shared.Domain.Calendar;

/// <summary>
/// A calendar date in Bikram Sambat. Fields are stored as offsets so that <c>default(NepaliDate)</c>
/// is the valid date 2000-01-01 rather than an impossible 0000-00-00.
/// </summary>
public readonly record struct NepaliDate : IComparable<NepaliDate>
{
    private readonly int _yearOffset;
    private readonly int _monthOffset;
    private readonly int _dayOffset;

    public NepaliDate(int year, int month, int day)
    {
        if (!NepaliCalendar.IsValid(year, month, day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), $"{year:D4}-{month:D2}-{day:D2} is not a valid BS date.");
        }

        _yearOffset = year - NepaliCalendar.MinYear;
        _monthOffset = month - 1;
        _dayOffset = day - 1;
    }

    public int Year => _yearOffset + NepaliCalendar.MinYear;

    public int Month => _monthOffset + 1;

    public int Day => _dayOffset + 1;

    public int DaysInMonth => NepaliCalendar.DaysInMonth(Year, Month);

    public DayOfWeek DayOfWeek => ToDateOnly().DayOfWeek;

    public static NepaliDate FromDateOnly(DateOnly date) => NepaliCalendar.ToNepali(date);

    public DateOnly ToDateOnly() => NepaliCalendar.ToGregorian(Year, Month, Day);

    public NepaliDate AddDays(int days) => FromDateOnly(ToDateOnly().AddDays(days));

    /// <summary>Adds whole BS months; the day is clamped to the target month's length (32 Asar + 1 month = 31 Shrawan).</summary>
    public NepaliDate AddMonths(int months)
    {
        var index = (Year * 12) + (Month - 1) + months;
        var year = index / 12;
        var month = (index % 12) + 1;
        if (year is < NepaliCalendar.MinYear or > NepaliCalendar.MaxYear)
        {
            throw new ArgumentOutOfRangeException(nameof(months), months, "Result is outside the supported BS range.");
        }

        return new NepaliDate(year, month, Math.Min(Day, NepaliCalendar.DaysInMonth(year, month)));
    }

    public NepaliDate FirstDayOfMonth() => new(Year, Month, 1);

    public NepaliDate LastDayOfMonth() => new(Year, Month, DaysInMonth);

    public string MonthName(bool nepali = false) => NepaliCalendar.MonthName(Month, nepali);

    /// <summary>"2081-01-15".</summary>
    public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";

    /// <summary>"15 Baisakh 2081", or "१५ बैशाख २०८१" with <paramref name="nepali"/>.</summary>
    public string ToLongDateString(bool nepali = false) =>
        nepali
            ? NepaliDigits.Convert($"{Day} {MonthName(nepali: true)} {Year}")
            : $"{Day} {MonthName()} {Year}";

    /// <summary>Parses "yyyy-MM-dd" written with ASCII or Nepali digits.</summary>
    public static NepaliDate Parse(string text) =>
        TryParse(text, out var date) ? date : throw new FormatException($"\"{text}\" is not a BS date in yyyy-MM-dd format.");

    public static bool TryParse([NotNullWhen(true)] string? text, out NepaliDate date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = NepaliDigits.ToAscii(text.Trim()).Split('-');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || !NepaliCalendar.IsValid(year, month, day))
        {
            return false;
        }

        date = new NepaliDate(year, month, day);
        return true;
    }

    public int CompareTo(NepaliDate other) =>
        (Year, Month, Day).CompareTo((other.Year, other.Month, other.Day));

    public static bool operator <(NepaliDate left, NepaliDate right) => left.CompareTo(right) < 0;

    public static bool operator >(NepaliDate left, NepaliDate right) => left.CompareTo(right) > 0;

    public static bool operator <=(NepaliDate left, NepaliDate right) => left.CompareTo(right) <= 0;

    public static bool operator >=(NepaliDate left, NepaliDate right) => left.CompareTo(right) >= 0;
}
