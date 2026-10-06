namespace UnderGlass.Sim;

/// <summary>Knobs for perception (design rule 2). Not saved; first guesses for phase 0.</summary>
public sealed class PerceptionOptions
{
    public double Close { get; set; } = 1.0;   // 0-2 tiles
    public double Near { get; set; } = 0.6;    // 3-5 tiles
    public double Far { get; set; } = 0.3;     // 6-8 tiles
    public int CloseTiles { get; set; } = 2;
    public int NearTiles { get; set; } = 5;
    public int FarTiles { get; set; } = 8;
    public double HalfBlock { get; set; } = 0.5; // each fence, bush or shelf in the way
    public double Night { get; set; } = 0.5;     // outdoors from NightFrom until DayFrom
    public int NightFrom { get; set; } = 20 * 60; // 20:00, as a minute of the day
    public int DayFrom { get; set; } = 6 * 60;    // 6:00
    public double KnowWhat { get; set; } = 0.3;  // clarity to know what happened
    /// <summary>Clarity a stranger needs to tell who it was (a good look, close).</summary>
    public double KnowWhoStranger { get; set; } = 0.9;
    /// <summary>Each point of familiarity lowers the clarity needed by this much, so family who
    /// know someone well (familiarity ~0.86+) can tell at 8 tiles (clarity 0.3).</summary>
    public double KnowWhoPerFamiliarity { get; set; } = 0.7;
}

/// <summary>
/// Layered perception (design rule 2): how clearly an observer sees an actor in one minute, and
/// what an observer takes away from a whole act. Pure.
/// </summary>
public static class Perception
{
    /// <summary>Clarity of one minute of sight: distance band x line of sight x darkness.</summary>
    public static double Instant(Location place, Tile observer, Tile actor, int minuteOfDay, PerceptionOptions o)
    {
        int d = observer.Chebyshev(actor);
        double band = d <= o.CloseTiles ? o.Close : d <= o.NearTiles ? o.Near : d <= o.FarTiles ? o.Far : 0;
        if (band == 0)
            return 0;
        double sight = LineOfSight(place, observer, actor, o);
        double dark = place.Outdoor && (minuteOfDay >= o.NightFrom || minuteOfDay < o.DayFrom) ? o.Night : 1;
        return band * sight * dark;
    }

    /// <summary>1 for a clear line, halved for each '+' tile crossed, 0 if a '#' is crossed.
    /// Bresenham between the two tiles, ends excluded.</summary>
    public static double LineOfSight(Location place, Tile from, Tile to, PerceptionOptions o)
    {
        int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        double sight = 1;
        while (true)
        {
            if (x0 == x1 && y0 == y1)
                break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
            if (x0 == x1 && y0 == y1)
                break;
            char c = place.At(new Tile(x0, y0));
            if (c == '#')
                return 0;
            if (c == '+')
                sight *= o.HalfBlock;
        }
        return sight;
    }

    /// <summary>Clarity of a whole act: the sum of the minutes watched, over the act's read time, at most 1.</summary>
    public static double OfAct(IEnumerable<double> instants, int readMinutes)
        => Math.Min(1, instants.Sum() / Math.Max(1, readMinutes));

    /// <summary>The clarity needed to tell who it was: less the better the observer knows them.</summary>
    public static double NeededToIdentify(double familiarity, PerceptionOptions o)
        => Math.Max(0.05, o.KnowWhoStranger - o.KnowWhoPerFamiliarity * familiarity);

    /// <summary>Whether the observer can tell who it was (Sid, 2026-10-05: "someone who is 8 blocks
    /// away might not know who is digging in the trash... if they know the person really well they
    /// could tell").</summary>
    public static bool Identifies(double clarity, double familiarity, PerceptionOptions o)
        => clarity >= NeededToIdentify(familiarity, o);

    /// <summary>A bold villager with low self-regard names a guess as fact (design 11a).</summary>
    public static bool GuessesConfidently(Temperament t) => t.Boldness >= 0.7 && t.SelfRegard <= 0.35;
}
