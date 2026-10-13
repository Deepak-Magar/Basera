using Basera.Shared.Domain.Calendar;

namespace Basera.UnitTests.Shared;

public sealed class BillingPeriodTests
{
    [Fact]
    public void Covers_one_bs_month()
    {
        var period = new BillingPeriod(2081, 1);

        period.Start.Should().Be(new NepaliDate(2081, 1, 1));
        period.End.Should().Be(new NepaliDate(2081, 1, 31));
        period.Days.Should().Be(31);
        period.StartAd.Should().Be(new DateOnly(2024, 4, 13));
        period.EndAd.Should().Be(new DateOnly(2024, 5, 13));
        period.ToString().Should().Be("2081-01");
    }

    [Fact]
    public void Next_and_previous_cross_years()
    {
        new BillingPeriod(2080, 12).Next().Should().Be(new BillingPeriod(2081, 1));
        new BillingPeriod(2081, 1).Previous().Should().Be(new BillingPeriod(2080, 12));
    }

    [Fact]
    public void Containing_an_ad_date()
    {
        BillingPeriod.Containing(new DateOnly(2026, 10, 13)).Should().Be(new BillingPeriod(2083, 6));
    }

    [Fact]
    public void Contains_checks_the_boundaries()
    {
        var period = new BillingPeriod(2081, 1);

        period.Contains(new DateOnly(2024, 4, 13)).Should().BeTrue();
        period.Contains(new DateOnly(2024, 5, 13)).Should().BeTrue();
        period.Contains(new DateOnly(2024, 5, 14)).Should().BeFalse();
        period.Contains(new NepaliDate(2080, 12, 30)).Should().BeFalse();
    }

    [Fact]
    public void Default_is_a_valid_period()
    {
        default(BillingPeriod).Should().Be(new BillingPeriod(2000, 1));
    }

    [Theory]
    [InlineData("2081-01", true)]
    [InlineData("२०८१-०६", true)]
    [InlineData("2081-13", false)]
    [InlineData("2081", false)]
    [InlineData(null, false)]
    public void Try_parse(string? text, bool ok)
    {
        BillingPeriod.TryParse(text, out _).Should().Be(ok);
    }
}
