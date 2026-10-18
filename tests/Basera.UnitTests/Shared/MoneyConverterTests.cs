using Basera.Shared.Domain;
using Basera.Shared.Persistence;

namespace Basera.UnitTests.Shared;

public sealed class MoneyConverterTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1_250_000L)]
    [InlineData(-50_000L)]
    [InlineData(long.MaxValue)]
    public void Round_trips_through_paisa(long paisa)
    {
        var converter = new MoneyConverter();

        converter.ConvertToProvider(Money.FromPaisa(paisa)).Should().Be(paisa);
        converter.ConvertFromProvider(paisa).Should().Be(Money.FromPaisa(paisa));
    }
}
