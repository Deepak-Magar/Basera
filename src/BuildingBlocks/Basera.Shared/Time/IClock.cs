using Basera.Shared.Domain.Calendar;

namespace Basera.Shared.Time;

/// <summary>
/// The current time, injectable so tests can fix "now". Business dates (due dates, billing periods,
/// "today" on the warden's screen) are Nepal dates, not server or UTC dates.
/// </summary>
public interface IClock
{
    /// <summary>Now in UTC: what timestamps are stored as.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Now as wall-clock time in Nepal (UTC+05:45).</summary>
    DateTimeOffset NepalNow { get; }

    /// <summary>Today's date in Nepal, AD.</summary>
    DateOnly Today { get; }

    /// <summary>Today's date in Nepal, BS.</summary>
    NepaliDate TodayBs { get; }
}
