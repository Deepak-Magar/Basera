using System.Globalization;

namespace Basera.Shared.Domain.Calendar;

/// <summary>
/// Bikram Sambat (BS) ↔ Gregorian (AD) conversion for 2000–2099 BS, driven by the embedded
/// month-length table <c>calendar-bs.csv</c>. Dates are stored in AD; BS is for display and billing periods.
/// </summary>
public static class NepaliCalendar
{
    public const int MinYear = 2000;
    public const int MaxYear = 2099;

    /// <summary>2000-01-01 BS.</summary>
    public static readonly DateOnly Epoch = new(1943, 4, 14);

    private const string ResourceName = "Basera.Shared.calendar-bs.csv";

    // MonthDays[y][m] = days in month m (0-based) of year MinYear + y.
    private static readonly int[][] MonthDays = LoadTable();

    // YearStart[y] = days from Epoch to the first day of year MinYear + y; one extra entry marks the end.
    private static readonly int[] YearStart = BuildYearStarts(MonthDays);

    public static DateOnly MinDate => Epoch;

    public static DateOnly MaxDate => Epoch.AddDays(YearStart[^1] - 1);

    public static IReadOnlyList<string> EnglishMonthNames { get; } =
    [
        "Baisakh", "Jestha", "Asar", "Shrawan", "Bhadra", "Asoj",
        "Kartik", "Mangsir", "Poush", "Magh", "Falgun", "Chaitra",
    ];

    public static IReadOnlyList<string> NepaliMonthNames { get; } =
    [
        "बैशाख", "जेठ", "असार", "साउन", "भदौ", "असोज",
        "कात्तिक", "मंसिर", "पुस", "माघ", "फागुन", "चैत",
    ];

    public static string MonthName(int month, bool nepali = false)
    {
        EnsureMonth(month);
        return (nepali ? NepaliMonthNames : EnglishMonthNames)[month - 1];
    }

    public static int DaysInMonth(int year, int month)
    {
        EnsureYear(year);
        EnsureMonth(month);
        return MonthDays[year - MinYear][month - 1];
    }

    public static int DaysInYear(int year)
    {
        EnsureYear(year);
        return YearStart[year - MinYear + 1] - YearStart[year - MinYear];
    }

    public static bool IsValid(int year, int month, int day) =>
        year is >= MinYear and <= MaxYear
        && month is >= 1 and <= 12
        && day >= 1 && day <= MonthDays[year - MinYear][month - 1];

    public static DateOnly ToGregorian(int year, int month, int day)
    {
        if (!IsValid(year, month, day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), $"{year:D4}-{month:D2}-{day:D2} is not a valid BS date in {MinYear}–{MaxYear}.");
        }

        var days = YearStart[year - MinYear] + day - 1;
        for (var m = 0; m < month - 1; m++)
        {
            days += MonthDays[year - MinYear][m];
        }

        return Epoch.AddDays(days);
    }

    public static NepaliDate ToNepali(DateOnly date)
    {
        if (date < MinDate || date > MaxDate)
        {
            throw new ArgumentOutOfRangeException(nameof(date), $"{date:yyyy-MM-dd} is outside the supported range {MinDate:yyyy-MM-dd}–{MaxDate:yyyy-MM-dd}.");
        }

        var offset = date.DayNumber - Epoch.DayNumber;

        // Index of the last year whose first day is on or before the date.
        var index = Array.BinarySearch(YearStart, offset);
        var y = index >= 0 ? index : ~index - 1;

        var remaining = offset - YearStart[y];
        var m = 0;
        while (remaining >= MonthDays[y][m])
        {
            remaining -= MonthDays[y][m];
            m++;
        }

        return new NepaliDate(MinYear + y, m + 1, remaining + 1);
    }

    private static void EnsureYear(int year)
    {
        if (year is < MinYear or > MaxYear)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, $"BS year must be in {MinYear}–{MaxYear}.");
        }
    }

    private static void EnsureMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1–12.");
        }
    }

    private static int[][] LoadTable()
    {
        using var stream = typeof(NepaliCalendar).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} not found.");
        using var reader = new StreamReader(stream);

        var rows = new List<int[]>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("Year", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split(',').Select(c => int.Parse(c, CultureInfo.InvariantCulture)).ToArray();
            var expectedYear = MinYear + rows.Count;
            if (cells.Length != 13 || cells[0] != expectedYear || cells.Skip(1).Any(d => d is < 29 or > 32))
            {
                throw new InvalidOperationException($"calendar-bs.csv: bad row for {expectedYear}: {line}");
            }

            rows.Add(cells[1..]);
        }

        if (rows.Count != MaxYear - MinYear + 1)
        {
            throw new InvalidOperationException($"calendar-bs.csv: expected {MaxYear - MinYear + 1} years, found {rows.Count}.");
        }

        return rows.ToArray();
    }

    private static int[] BuildYearStarts(int[][] monthDays)
    {
        var starts = new int[monthDays.Length + 1];
        for (var y = 0; y < monthDays.Length; y++)
        {
            starts[y + 1] = starts[y] + monthDays[y].Sum();
        }

        return starts;
    }
}
