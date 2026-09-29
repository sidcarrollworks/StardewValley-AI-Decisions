namespace NpcMemory;

/// <summary>
/// Tile-distance co-location. "Seen" means within <paramref name="radius"/> tiles on BOTH axes
/// (Chebyshev distance: a square, not a circle) — being in the same location or region is not
/// enough on its own. The default radius of 8 is a placeholder to tune later.
/// </summary>
public static class Proximity
{
    public const int DefaultRadius = 8;

    public static bool WithinRadius(int x1, int y1, int x2, int y2, int radius = DefaultRadius)
        => Math.Abs(x1 - x2) <= radius && Math.Abs(y1 - y2) <= radius;
}
