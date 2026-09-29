using System.Text.Json;
using NpcSchedules;

namespace NpcMemory;

/// <summary>
/// One observer's belief about where a single subject NPC tends to be, learned from
/// co-presence. Region x time-block pseudo-counts, seeded from a rough prior for family and
/// friends (NpcSchedules.RoutinePrior). Hearts raise the learning strength for the player's
/// routine. A pair "unlocks" (the observer no longer needs a silent week to know the
/// subject's habits) once enough co-presence has accumulated.
/// </summary>
public sealed class RoutineBelief
{
    private readonly Dictionary<string, double[]> _counts = new(StringComparer.OrdinalIgnoreCase);
    private double _coPresenceTicks;

    public string Observer { get; }
    public string Subject { get; }
    public int BlockMinutes { get; }
    public int BlockCount => TimeUtils.BlockCount(BlockMinutes);

    /// <summary>Co-presence ticks needed before the pair is "unlocked".</summary>
    public int UnlockThreshold { get; set; } = 240; // 40 game hours (two full days) of being around each other

    /// <summary>Region -> block[] pseudo-counts. Each block value is a non-negative double.</summary>
    public IReadOnlyDictionary<string, double[]> Counts => _counts;

    public double CoPresenceTicks => _coPresenceTicks;
    public bool Unlocked => CoPresenceTicks >= UnlockThreshold;

    public RoutineBelief(string observer, string subject, int blockMinutes = 120)
    {
        Observer = observer;
        Subject = subject;
        BlockMinutes = blockMinutes;
        _coPresenceTicks = 0;
    }

    /// <summary>Seed the belief from a prior built off real schedules (family/friends).</summary>
    public void SeedPrior(NpcRoutine routine, string saveSeed, PriorOptions options)
    {
        if (routine is null) throw new ArgumentNullException(nameof(routine));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (routine.BlockMinutes != BlockMinutes)
            throw new ArgumentException(
                $"routine block size {routine.BlockMinutes} does not match belief block size {BlockMinutes}",
                nameof(routine));

        List<RoutinePrior.PriorCell> cells = RoutinePrior.Build(routine, Observer, saveSeed, options);
        foreach (RoutinePrior.PriorCell cell in cells)
        {
            // Cell.Block is a label like "0600": recover the block index by matching labels.
            int block = -1;
            for (int b = 0; b < BlockCount; b++)
            {
                if (string.Equals(TimeUtils.BlockLabel(b, routine.BlockMinutes), cell.Block, StringComparison.Ordinal))
                {
                    block = b;
                    break;
                }
            }
            if (block < 0)
                continue; // label not representable at this block size; nothing sensible to add

            Column(cell.Region)[block] += cell.Count; // accumulate, never overwrite
        }
    }

    /// <summary>Record co-presence at a region/block, adding `strength` to that cell.
    /// Higher strength means the subject's routine is learned faster (hearts for the player).</summary>
    public void Observe(string region, int block, int absoluteTick, double strength = 1.0)
    {
        if (block < 0 || block >= BlockCount)
            throw new ArgumentOutOfRangeException(nameof(block), block, $"block must be in [0, {BlockCount})");

        // `absoluteTick` is accepted for future age-weighting but is not used yet.
        Column(region)[block] += strength;
    }

    /// <summary>Accumulate co-presence time; the pair unlocks once it reaches UnlockThreshold.</summary>
    public void NoteCoPresence(int ticks) => _coPresenceTicks += ticks;

    /// <summary>Age all cells toward zero (multiply by `factor`, 0 &lt; factor &lt;= 1).</summary>
    public void Decay(double factor)
    {
        if (double.IsNaN(factor))
            factor = 0.0;
        factor = Math.Clamp(factor, 0.0, 1.0);

        foreach (double[] column in _counts.Values)
            for (int b = 0; b < column.Length; b++)
                column[b] *= factor;
    }

    /// <summary>The region/block with the highest count, or null if everything is zero.</summary>
    public (string Region, int Block)? BestGuess()
    {
        double best = double.NegativeInfinity;
        foreach (double[] column in _counts.Values)
            foreach (double value in column)
                if (value > best)
                    best = value;

        if (double.IsNegativeInfinity(best) || best <= 0)
            return null; // nothing learned / every cell is zero

        // Deterministic tie-break: region name (OrdinalIgnoreCase) ascending, then block index.
        foreach (string region in _counts.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            double[] column = _counts[region];
            for (int b = 0; b < column.Length; b++)
                if (column[b] == best)
                    return (region, b);
        }

        return null;
    }

    /// <summary>
    /// Where the subject tends to be during one time block: the region with the most weight in that
    /// block, its share of the block's total, and the total itself (how much evidence there is).
    /// Null when nothing was learned for the block. Ties go to the region name, ascending.
    /// Note the observer only learns from time spent together, so this is "where I usually see
    /// them at this hour", not the subject's true routine.
    /// </summary>
    public BlockGuess? BestGuessAt(int block)
    {
        if (block < 0 || block >= BlockCount)
            throw new ArgumentOutOfRangeException(nameof(block), block, $"block must be in [0, {BlockCount})");

        double total = 0;
        string? bestRegion = null;
        double best = 0;
        foreach (string region in _counts.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            double value = _counts[region][block];
            if (!(value > 0))
                continue;
            total += value;
            if (value > best)
            {
                best = value;
                bestRegion = region;
            }
        }
        return bestRegion is null ? null : new BlockGuess(bestRegion, block, best / total, total);
    }

    public string ToJson()
    {
        var dto = new BeliefDto
        {
            Observer = Observer,
            Subject = Subject,
            BlockMinutes = BlockMinutes,
            UnlockThreshold = UnlockThreshold,
            CoPresenceTicks = _coPresenceTicks,
            Counts = _counts.ToDictionary(
                kv => kv.Key,
                kv => (double[])kv.Value.Clone(),
                StringComparer.OrdinalIgnoreCase),
        };
        return JsonSerializer.Serialize(dto);
    }

    public static RoutineBelief FromJson(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        BeliefDto dto = JsonSerializer.Deserialize<BeliefDto>(json)
            ?? throw new ArgumentException("JSON did not contain a belief", nameof(json));
        const int minutesPerDay = TimeUtils.TicksPerDay * 10; // 120 x 10-minute ticks = 1200 minutes
        if (dto.BlockMinutes <= 0 || dto.BlockMinutes > minutesPerDay || minutesPerDay % dto.BlockMinutes != 0)
            throw new ArgumentException($"invalid block size {dto.BlockMinutes} in JSON", nameof(json));

        var belief = new RoutineBelief(dto.Observer, dto.Subject, dto.BlockMinutes)
        {
            UnlockThreshold = dto.UnlockThreshold,
        };
        belief._coPresenceTicks = dto.CoPresenceTicks;

        if (dto.Counts is not null)
        {
            foreach ((string region, double[] column) in dto.Counts)
            {
                double[] target = belief.Column(region);
                int n = Math.Min(column?.Length ?? 0, target.Length);
                for (int b = 0; b < n; b++)
                    target[b] = column![b];
            }
        }

        return belief;
    }

    /// <summary>The block column for a region, created (and sized) on first use.</summary>
    private double[] Column(string region)
    {
        if (!_counts.TryGetValue(region, out double[]? column))
        {
            column = new double[BlockCount];
            _counts[region] = column;
        }
        return column;
    }

    /// <summary>Serialization shape; kept private so the public surface stays a read-only matrix.</summary>
    private sealed class BeliefDto
    {
        public string Observer { get; set; } = "";
        public string Subject { get; set; } = "";
        public int BlockMinutes { get; set; }
        public int UnlockThreshold { get; set; }
        public double CoPresenceTicks { get; set; }
        public Dictionary<string, double[]>? Counts { get; set; }
    }
}
