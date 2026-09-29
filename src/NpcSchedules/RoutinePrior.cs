namespace NpcSchedules;

/// <summary>
/// Turns an NPC's routine counts into the rough belief another NPC starts with:
/// each block's mass is smeared into its neighbours by a random amount, each region gets
/// log-normal noise, and the result is scaled to a pseudo-count strength (family high,
/// friends low). Same inputs and seed always give the same prior.
/// </summary>
public static class RoutinePrior
{
    public sealed record PriorCell(string Region, string Block, double Count);

    /// <summary>
    /// Build a prior for <paramref name="observer"/> over <paramref name="routine"/>.
    /// </summary>
    /// <param name="saveSeed">Per-save value so different saves have different-looking priors.</param>
    public static List<PriorCell> Build(NpcRoutine routine, string observer, string saveSeed, PriorOptions options)
    {
        int blockCount = TimeUtils.BlockCount(routine.BlockMinutes);
        var matrix = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

        // seed everything from FNV-1a, never string.GetHashCode
        int seed = Fnv1a.Seed("prior", routine.Name, observer, saveSeed);
        var rng = new Random(seed);

        foreach ((string region, int[] column) in routine.RegionTicks)
        {
            double[] counts = column.Select(c => (double)c).ToArray();

            // smear each block into its neighbours (day wraps around)
            var smeared = new double[blockCount];
            for (int b = 0; b < blockCount; b++)
            {
                double keep = counts[b] * (1 - rng.NextDouble() * options.MaxSmear);
                double spill = counts[b] - keep;
                double leftShare = rng.NextDouble();
                int left = (b - 1 + blockCount) % blockCount;
                int right = (b + 1) % blockCount;
                smeared[b] += keep;
                smeared[left] += spill * leftShare;
                smeared[right] += spill * (1 - leftShare);
            }

            // log-normal noise per region
            double multiplier = Math.Exp(Gauss(rng) * options.RegionNoiseSigma);
            for (int b = 0; b < blockCount; b++)
                smeared[b] *= multiplier;

            matrix[region] = smeared;
        }

        // scale to the pseudo-count strength for this relationship
        double strength = options.Kind switch
        {
            RelationshipKind.Family => options.FamilyStrength,
            RelationshipKind.Friend => options.FriendStrength,
            _ => options.OtherStrength,
        };
        double total = matrix.Values.Sum(column => column.Sum());
        double scale = total > 0 ? strength / total : 0;

        var cells = new List<PriorCell>();
        foreach ((string region, double[] column) in matrix.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            for (int b = 0; b < blockCount; b++)
            {
                double rounded = Math.Round(column[b] * scale, 2);
                if (rounded > 0)
                    cells.Add(new PriorCell(region, TimeUtils.BlockLabel(b, routine.BlockMinutes), rounded));
            }
        return cells;
    }

    /// <summary>Standard normal via Box-Muller, consuming the seeded RNG only.</summary>
    private static double Gauss(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
