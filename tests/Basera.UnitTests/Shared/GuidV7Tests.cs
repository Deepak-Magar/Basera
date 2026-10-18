using Basera.Shared.Domain;

namespace Basera.UnitTests.Shared;

public sealed class GuidV7Tests
{
    [Fact]
    public void Has_version_7_and_the_rfc_variant()
    {
        var text = GuidV7.New().ToString();

        text[14].Should().Be('7');
        text[19].Should().BeOneOf('8', '9', 'a', 'b');
    }

    [Fact]
    public void Starts_with_the_unix_millisecond_timestamp()
    {
        var timestamp = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        var text = GuidV7.New(timestamp).ToString("N");

        Convert.ToInt64(text[..12], 16).Should().Be(timestamp.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Later_ids_sort_after_earlier_ones()
    {
        var start = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 100).Select(i => GuidV7.New(start.AddMilliseconds(i)).ToString()).ToList();

        ids.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void Ids_in_the_same_millisecond_differ()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Enumerable.Range(0, 1000).Select(_ => GuidV7.New(timestamp)).Should().OnlyHaveUniqueItems();
    }
}
