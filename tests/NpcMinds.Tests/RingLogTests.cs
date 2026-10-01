using Xunit;

namespace NpcMinds.Tests;

public sealed class RingLogTests
{
    [Fact]
    public void KeepsTheNewestItemsNewestFirst()
    {
        var log = new RingLog<long>(3);
        for (int i = 0; i < 5; i++)
            log.Add(seq => seq);

        Assert.Equal(new long[] { 5, 4, 3 }, log.Newest());
    }

    [Fact]
    public void ClearEmptiesButSequenceKeepsRising()
    {
        var log = new RingLog<long>(3);
        log.Add(seq => seq);
        log.Clear();
        Assert.Empty(log.Newest());
        Assert.Equal(2, log.Add(seq => seq)); // the page can still tell new from old
    }

    [Fact]
    public void ManyThreadsCanAddAtOnce()
    {
        var log = new RingLog<long>(10_000);
        Parallel.For(0, 4_000, _ => log.Add(seq => seq));

        IReadOnlyList<long> items = log.Newest();
        Assert.Equal(4_000, items.Count);
        Assert.Equal(4_000, items.Distinct().Count());
    }
}
