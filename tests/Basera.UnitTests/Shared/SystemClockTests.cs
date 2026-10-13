using Basera.Shared.Domain.Calendar;
using Basera.Shared.Time;

namespace Basera.UnitTests.Shared;

public sealed class SystemClockTests
{
    [Fact]
    public void Today_is_the_nepal_date_not_the_utc_date()
    {
        // 18:30 UTC on 12 Oct is 00:15 on 13 Oct in Nepal (UTC+05:45).
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero)));

        clock.UtcNow.Day.Should().Be(12);
        clock.NepalNow.Offset.Should().Be(new TimeSpan(5, 45, 0));
        clock.Today.Should().Be(new DateOnly(2026, 10, 13));
        clock.TodayBs.Should().Be(new NepaliDate(2083, 6, 27));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
