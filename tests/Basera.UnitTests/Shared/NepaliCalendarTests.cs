using Basera.Shared.Domain.Calendar;

namespace Basera.UnitTests.Shared;

public sealed class NepaliCalendarTests
{
    [Theory]
    [InlineData(2000, 1, 1, "1943-04-14")] // epoch
    [InlineData(2081, 1, 1, "2024-04-13")]
    [InlineData(2083, 6, 27, "2026-10-13")]
    [InlineData(2080, 12, 30, "2024-04-12")]
    public void Converts_known_dates_both_ways(int year, int month, int day, string ad)
    {
        var expected = DateOnly.Parse(ad, System.Globalization.CultureInfo.InvariantCulture);

        NepaliCalendar.ToGregorian(year, month, day).Should().Be(expected);
        NepaliCalendar.ToNepali(expected).Should().Be(new NepaliDate(year, month, day));
    }

    [Fact]
    public void Every_day_in_the_supported_range_round_trips()
    {
        var previous = (NepaliDate?)null;
        for (var date = NepaliCalendar.MinDate; date <= NepaliCalendar.MaxDate; date = date.AddDays(1))
        {
            var bs = NepaliCalendar.ToNepali(date);

            bs.ToDateOnly().Should().Be(date);
            if (previous is { } p)
            {
                bs.Should().BeGreaterThan(p);
            }

            previous = bs;
        }

        previous.Should().Be(new NepaliDate(2099, 12, NepaliCalendar.DaysInMonth(2099, 12)));
    }

    [Fact]
    public void Month_lengths_for_2081()
    {
        Enumerable.Range(1, 12).Select(m => NepaliCalendar.DaysInMonth(2081, m))
            .Should().Equal(31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31);
        NepaliCalendar.DaysInYear(2081).Should().Be(366);
    }

    [Fact]
    public void Chaitra_2080_has_30_days()
    {
        NepaliCalendar.DaysInMonth(2080, 12).Should().Be(30);
    }

    [Fact]
    public void Every_year_has_365_or_366_days_and_months_of_29_to_32()
    {
        for (var year = NepaliCalendar.MinYear; year <= NepaliCalendar.MaxYear; year++)
        {
            NepaliCalendar.DaysInYear(year).Should().BeInRange(365, 366, "year {0}", year);
            for (var month = 1; month <= 12; month++)
            {
                NepaliCalendar.DaysInMonth(year, month).Should().BeInRange(29, 32);
            }
        }
    }

    [Theory]
    [InlineData(1999, 12, 30)]
    [InlineData(2100, 1, 1)]
    [InlineData(2081, 13, 1)]
    [InlineData(2081, 2, 33)]
    [InlineData(2081, 9, 30)] // Poush 2081 has 29 days
    public void Rejects_invalid_dates(int year, int month, int day)
    {
        NepaliCalendar.IsValid(year, month, day).Should().BeFalse();

        var act = () => NepaliCalendar.ToGregorian(year, month, day);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_ad_dates_outside_the_table()
    {
        var act = () => NepaliCalendar.ToNepali(NepaliCalendar.MinDate.AddDays(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Month_names_in_english_and_nepali()
    {
        NepaliCalendar.MonthName(1).Should().Be("Baisakh");
        NepaliCalendar.MonthName(6).Should().Be("Asoj");
        NepaliCalendar.MonthName(12, nepali: true).Should().Be("चैत");
        NepaliCalendar.NepaliMonthNames.Should().HaveCount(12);
    }
}
