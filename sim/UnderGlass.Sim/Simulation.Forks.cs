using System.Globalization;

namespace UnderGlass.Sim;

// Forked runs (batch 2 spec 6.3, slice m-2): a copy of a run that is the run to a day, then draws
// every die anew. V7 (Variety.Open) asks how much of a year is still open at that day.
public sealed partial class Simulation
{
    private int _forkAt = int.MaxValue;
    private long _forkSeed;

    /// <summary>
    /// V7: from the start of <paramref name="day"/> on, every keyed draw uses another seed, made from
    /// this run's seed and <paramref name="salt"/>. Before that minute the run is its base's to the
    /// line: the setup and every draw read the base's seed. Called before Run, as SetTrait is. A fork
    /// at the run's length in days, or later, changes nothing.
    /// </summary>
    public void Fork(int day, long salt)
    {
        if (day < 0)
            throw new ArgumentOutOfRangeException(nameof(day), day, "a fork's day is 0 or later");
        _forkAt = day * Clock.MinutesPerDay;
        _forkSeed = unchecked((long)Rng.Hash("fork", _seed.ToString(CultureInfo.InvariantCulture),
            salt.ToString(CultureInfo.InvariantCulture)));
    }
}
