using Basera.Shared.Domain.Calendar;

namespace Basera.Shared.Time;

/// <summary><see cref="IClock"/> over .NET's <see cref="TimeProvider"/>; tests pass a fake provider.</summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    /// <summary>Nepal Standard Time. Fixed offset, no daylight saving, so no time zone database is needed.</summary>
    public static readonly TimeSpan NepalOffset = new(5, 45, 0);

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateTimeOffset NepalNow => UtcNow.ToOffset(NepalOffset);

    public DateOnly Today => DateOnly.FromDateTime(NepalNow.DateTime);

    public NepaliDate TodayBs => NepaliDate.FromDateOnly(Today);
}
