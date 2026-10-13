using Basera.Shared.Domain;

namespace Basera.UnitTests.Shared;

public sealed class MoneyTests
{
    [Fact]
    public void Rupees_and_paisa_round_trip()
    {
        var money = Money.FromRupees(12_500.75m);

        money.Paisa.Should().Be(1_250_075);
        money.Rupees.Should().Be(12_500.75m);
    }

    [Fact]
    public void From_rupees_rejects_fractions_of_a_paisa()
    {
        var act = () => Money.FromRupees(10.005m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_and_subtract()
    {
        var a = Money.FromRupees(100);
        var b = Money.FromRupees(30.50m);

        (a + b).Should().Be(Money.FromRupees(130.50m));
        (a - b).Should().Be(Money.FromRupees(69.50m));
        (b - a).IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Overflow_throws_instead_of_wrapping()
    {
        var act = () => Money.FromPaisa(long.MaxValue) + Money.FromPaisa(1);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Multiply_by_quantity_and_by_factor()
    {
        Money.FromRupees(250).Multiply(3).Should().Be(Money.FromRupees(750));

        // 1.005 rounds away from zero to 1.01.
        Money.FromPaisa(100).Multiply(1.005m).Should().Be(Money.FromPaisa(101));
    }

    [Theory]
    [InlineData(12_500_00, 10, 1_250_00)]
    [InlineData(333, 10, 33)] // 33.3 paisa -> 33
    [InlineData(335, 10, 34)] // 33.5 paisa -> 34 (half away from zero)
    [InlineData(-335, 10, -34)]
    public void Percent_rounds_to_nearest_paisa(long paisa, int percent, long expected)
    {
        Money.FromPaisa(paisa).Percent(percent).Paisa.Should().Be(expected);
    }

    [Theory]
    [InlineData(1_234_549, 1_234_500)]
    [InlineData(1_234_550, 1_234_600)]
    [InlineData(-1_234_550, -1_234_600)]
    public void Round_to_rupee(long paisa, long expected)
    {
        Money.FromPaisa(paisa).RoundToRupee().Paisa.Should().Be(expected);
    }

    [Fact]
    public void Allocate_loses_no_paisa()
    {
        var parts = Money.FromRupees(100).Allocate(3);

        parts.Select(p => p.Paisa).Should().Equal(3_334, 3_333, 3_333);
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(1_000_001, 6)]
    [InlineData(-1_000_001, 6)]
    [InlineData(0, 4)]
    [InlineData(99, 100)]
    public void Allocate_sums_to_the_total_and_parts_differ_by_at_most_one_paisa(long paisa, int count)
    {
        var parts = Money.FromPaisa(paisa).Allocate(count);

        parts.Should().HaveCount(count);
        parts.Sum(p => p.Paisa).Should().Be(paisa);
        (parts.Max(p => p.Paisa) - parts.Min(p => p.Paisa)).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Allocate_requires_at_least_one_part()
    {
        var act = () => Money.FromRupees(10).Allocate(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1_250_000, "रु 12,500.00")]
    [InlineData(0, "रु 0.00")]
    [InlineData(5, "रु 0.05")]
    [InlineData(99_999, "रु 999.99")]
    [InlineData(12_500_000, "रु 1,25,000.00")]
    [InlineData(1_234_567_890, "रु 1,23,45,678.90")]
    [InlineData(-50_000, "-रु 500.00")]
    public void Formats_with_rupee_symbol_and_nepali_grouping(long paisa, string expected)
    {
        Money.FromPaisa(paisa).ToString().Should().Be(expected);
    }

    [Fact]
    public void Formats_with_nepali_digits()
    {
        Money.FromRupees(12_500).Format(nepaliDigits: true).Should().Be("रु १२,५००.००");
    }

    [Fact]
    public void Formats_the_most_negative_amount_without_overflow()
    {
        Money.FromPaisa(long.MinValue).Format().Should().StartWith("-रु ");
    }

    [Fact]
    public void Compares_by_amount()
    {
        (Money.FromRupees(5) < Money.FromRupees(6)).Should().BeTrue();
        Money.Zero.Should().Be(Money.FromPaisa(0));
    }
}
