namespace UnderGlass.Sim.Generation;

/// <summary>
/// A neighbourhood as one outdoor place (town spec 2.1, 2.2): its rows, and the spots the generator
/// fills. Front steps are doors to the homes; the entry is where its connector road joins it.
/// </summary>
public sealed record Template(string Kind, IReadOnlyList<string> Rows, IReadOnlyList<Tile> Steps, Tile Entry,
    Tile Linger, int LingerRadius, IReadOnlyList<Tile> Benches, Tile Bin, IReadOnlyList<Tile> Zones)
{
    public int Width => Rows[0].Length;
    public int Height => Rows.Count;
}

/// <summary>
/// The neighbourhood templates, built from a few numbers rather than drawn tile by tile (a departure
/// from the spec's hand-drawn data file; the rows are the same kind of thing, and `--describe` prints
/// them). '#' is a house's footprint (blocks sight), '+' a hedge (halves sight; not walked on), '.'
/// open ground. Footprints are 5 wide and 3 deep, each with its front step in front of its middle.
/// </summary>
public static class Templates
{
    private const int Foot = 5, Deep = 3;

    private static char[][] Blank(int w, int h) => Enumerable.Range(0, h).Select(_ => Enumerable.Repeat('.', w).ToArray()).ToArray();
    private static IReadOnlyList<string> Rows(char[][] g) => g.Select(r => new string(r)).ToList();

    /// <summary>A footprint with its top-left corner at (x, y), and its step below it (facing down)
    /// or above it (facing up).</summary>
    private static Tile House(char[][] g, int x, int y, bool facesDown)
    {
        for (int dy = 0; dy < Deep; dy++)
            for (int dx = 0; dx < Foot; dx++)
                g[y + dy][x + dx] = '#';
        return new Tile(x + Foot / 2, facesDown ? y + Deep : y - 1);
    }

    private static void Hedge(char[][] g, int x0, int y0, int x1, int y1)
    {
        for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                g[y][x] = '+';
    }

    /// <summary>The Green: two rows of homes facing a green (about 12 x 8 in the middle), with
    /// benches at its corners; next-door steps 8 apart, the green between the rows.</summary>
    public static Template Green(int homes)
    {
        int perRow = (homes + 1) / 2, plot = 8;
        int w = 4 + perRow * plot + 4, h = 22;
        var g = Blank(w, h);
        Hedge(g, 0, 0, w - 1, 0);
        Hedge(g, 0, h - 1, w - 1, h - 1);
        var steps = new List<Tile>();
        for (int i = 0; i < perRow; i++)
            steps.Add(House(g, 4 + i * plot, 2, facesDown: true));      // the north row, steps on row 5
        for (int i = 0; i < homes - perRow; i++)
            steps.Add(House(g, 4 + i * plot, h - 2 - Deep, facesDown: false)); // the south row, steps on row 16
        // The green: open ground between the rows, ringed by a low hedge with gaps.
        int gx0 = w / 2 - 6, gx1 = w / 2 + 5, gy0 = 8, gy1 = 13;
        for (int x = gx0; x <= gx1; x++)
        {
            if (x % 4 != 0) g[gy0 - 1][x] = '+';
            if (x % 4 != 2) g[gy1 + 1][x] = '+';
        }
        var linger = new Tile(w / 2, (gy0 + gy1) / 2);
        var benches = new[] { new Tile(gx0, gy0), new Tile(gx1, gy0), new Tile(gx0, gy1), new Tile(gx1, gy1) };
        var zones = new[] { linger, new Tile(gx0 + 2, gy0 + 1), new Tile(gx1 - 2, gy1 - 1) };
        return new Template("Green", Rows(g), steps, new Tile(w - 1, h / 2), linger, 4, benches, new Tile(1, h / 2), zones);
    }

    /// <summary>The Lane: two facing rows of houses with hedged back gardens along a street 4 wide,
    /// and a small green at the far end; next-door steps 7 apart.</summary>
    public static Template Lane(int homes)
    {
        int perRow = (homes + 1) / 2, plot = 7;
        int w = 2 + perRow * plot + 12, h = 15;
        var g = Blank(w, h);
        Hedge(g, 0, 0, 2 + perRow * plot, 0);            // back gardens, north
        Hedge(g, 0, h - 1, 2 + perRow * plot, h - 1);    // and south
        var steps = new List<Tile>();
        for (int i = 0; i < perRow; i++)
        {
            steps.Add(House(g, 2 + i * plot, 2, facesDown: true));          // steps on row 5
            Hedge(g, 2 + i * plot + Foot + 1, 1, 2 + i * plot + Foot + 1, 3); // a hedge between gardens
        }
        for (int i = 0; i < homes - perRow; i++)
        {
            steps.Add(House(g, 2 + i * plot, h - 2 - Deep, facesDown: false)); // steps on row 9
            Hedge(g, 2 + i * plot + Foot + 1, h - 4, 2 + i * plot + Foot + 1, h - 2);
        }
        // The small green at the east end.
        int gx = 2 + perRow * plot + 2;
        var linger = new Tile(gx + 4, h / 2);
        var benches = new[] { new Tile(gx + 1, 3), new Tile(gx + 7, 3), new Tile(gx + 1, h - 4), new Tile(gx + 7, h - 4) };
        var zones = new[] { linger, new Tile(gx + 2, h / 2 - 2), new Tile(w / 3, h / 2) };
        return new Template("Lane", Rows(g), steps, new Tile(0, h / 2), linger, 3, benches, new Tile(gx + 8, h - 2), zones);
    }

    /// <summary>A home's rooms, sized by household (town spec 2.2): 8 x 6 for one, 9 x 7 for two,
    /// 10 x 8 for three or four, 12 x 9 for five or more. Bed (2,2) and Sofa (4,4) are always open,
    /// and the door is in the middle of the bottom row.</summary>
    public static (Location Home, Tile Door) Home(string name, int people)
    {
        (int w, int h) = people <= 1 ? (8, 6) : people == 2 ? (9, 7) : people <= 4 ? (10, 8) : (12, 9);
        var rows = Enumerable.Repeat(new string('.', w), h).ToList();
        return (new Location(name, false, rows), new Tile(w / 2, h - 1));
    }
}
