using Basera.Shared.Domain.Calendar;

namespace Basera.UnitTests.Shared;

public sealed class NepaliDateTests
{
    [Fact]
    public void Default_is_the_first_supported_date()
    {
        default(NepaliDate).Should().Be(new NepaliDate(2000, 1, 1));
    }

    [Fact]
    public void Add_months_clamps_to_the_shorter_month()
    {
        // Jestha 2081 has 32 days; Asar 2081 has 31.
        new NepaliDate(2081, 2, 32).AddMonths(1).Should().Be(new NepaliDate(2081, 3, 31));
    }

    [Fact]
    public void Add_months_crosses_years_in_both_directions()
    {
        new NepaliDate(2081, 11, 15).AddMonths(3).Should().Be(new NepaliDate(2082, 2, 15));
        new NepaliDate(2081, 2, 15).AddMonths(-3).Should().Be(new NepaliDate(2080, 11, 15));
    }

    [Fact]
    public void Add_days_crosses_month_and_year_boundaries()
    {
        new NepaliDate(2080, 12, 30).AddDays(1).Should().Be(new NepaliDate(2081, 1, 1));
        new NepaliDate(2081, 1, 1).AddDays(-1).Should().Be(new NepaliDate(2080, 12, 30));
    }

    [Fact]
    public void First_and_last_day_of_month()
    {
        var date = new NepaliDate(2081, 9, 10); // Poush 2081, 29 days

        date.FirstDayOfMonth().Should().Be(new NepaliDate(2081, 9, 1));
        date.LastDayOfMonth().Should().Be(new NepaliDate(2081, 9, 29));
    }

    [Fact]
    public void Day_of_week_comes_from_the_ad_date()
    {
        new NepaliDate(2081, 1, 1).DayOfWeek.Should().Be(DayOfWeek.Saturday); // 2024-04-13
    }

    [Fact]
    public void Formats()
    {
        var date = new NepaliDate(2081, 1, 5);

        date.ToString().Should().Be("2081-01-05");
        date.ToLongDateString().Should().Be("5 Baisakh 2081");
        date.ToLongDateString(nepali: true).Should().Be("५ बैशाख २०८१");
    }

    [Theory]
    [InlineData("2081-01-05")]
    [InlineData("२०८१-०१-०५")]
    [InlineData(" 2081-1-5 ")]
    public void Parses_ascii_and_nepali_digits(string text)
    {
        NepaliDate.Parse(text).Should().Be(new NepaliDate(2081, 1, 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2081/01/05")]
    [InlineData("2081-01-40")]
    [InlineData("2081-01")]
    [InlineData("+2081-01-05")]
    public void Try_parse_rejects_bad_input(string? text)
    {
        NepaliDate.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Compares_chronologically()
    {
        (new NepaliDate(2080, 12, 30) < new NepaliDate(2081, 1, 1)).Should().BeTrue();
        (new NepaliDate(2081, 2, 1) > new NepaliDate(2081, 1, 31)).Should().BeTrue();
    }
}
