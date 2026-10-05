using NpcMemory;
using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// The elastic part of a character's feelings (docs/spec/motives.md, "Elastic part"): recomputed
/// from the recent diary every time, never saved. Each entry's stress is its magnitude, scaled by
/// sensitivity, decayed by its per-day factor over the (fractional) days since. Pure.
/// </summary>
public static class Stresses
{
    /// <summary>Sensitivity scales every magnitude: a typical villager (0.5) feels the table value.</summary>
    public static double SensitivityFactor(Temperament t) => 0.5 + t.Sensitivity;

    /// <summary>The elastic stresses from the last <see cref="MotiveOptions.ElasticWindowDays"/> of
    /// diary, one per contributing entry. <paramref name="heartsWithPlayer"/> decides whether a
    /// <c>SawGift</c> of the player's stirs jealousy (the observer must be drawn to the giver).</summary>
    public static IReadOnlyList<Stress> Elastic(
        IReadOnlyList<DiaryEntry> diary, int now, Temperament t, int heartsWithPlayer, MotiveOptions o)
    {
        var result = new List<Stress>();
        int windowStart = now - o.ElasticWindowDays * GameClock.TicksPerDay;
        double sens = SensitivityFactor(t);
        foreach (DiaryEntry e in diary)
        {
            if (e.Kind == "Heard")
            {
                if (Hearsay(e, now, windowStart, sens, heartsWithPlayer, o) is { } hearsay)
                    result.Add(hearsay);
                continue;
            }

            if (e.AbsoluteTick < windowStart || e.AbsoluteTick > now)
                continue;
            double days = (now - e.AbsoluteTick) / (double)GameClock.TicksPerDay;

            if (e.Kind == "SawGift")
            {
                // Jealousy needs someone the observer is drawn to: for the player as giver, 8+ hearts
                // (romance.md); a loved gift to someone else stings most.
                IReadOnlyDictionary<string, string> d = DiaryDetail.Parse(e.Detail);
                string giver = StressorTable.TargetOf(e);
                bool drawn = string.Equals(giver, "Player", StringComparison.OrdinalIgnoreCase) && heartsWithPlayer >= 8;
                if (!drawn)
                    continue;
                double magnitude = d.TryGetValue("taste", out string? taste) && taste == "Love" ? 0.3 : 0.15;
                result.Add(new Stress(giver, Motive.Jealous, e.Kind, -1, magnitude * sens * Math.Pow(0.7, days), e.AbsoluteTick));
                continue;
            }

            StressorProfile? p = StressorTable.Of(e);
            if (p is null)
                continue;
            result.Add(new Stress(StressorTable.TargetOf(e), p.Motive, e.Kind, p.Valence,
                p.Magnitude * sens * Math.Pow(p.ElasticDecay, days), e.AbsoluteTick));
        }
        return result;
    }

    /// <summary>
    /// What a <c>Heard</c> does to the listener (ledger-gossip.md, "How the listener takes it").
    /// It is <see cref="Stress.MoodOnly"/> unless it is a scandal
    /// (<see cref="MotiveOptions.HearsayActsFromJuiciness"/>) or stirs jealousy:
    /// only the elastic part, at <see cref="MotiveOptions.HearsayFactor"/> of the original, decayed
    /// from when it was heard (D33). Relevance: a listener drawn to the player (8+ hearts) takes a
    /// story about the player <see cref="MotiveOptions.MaxRelevance"/> times as hard, and a gift
    /// the player gave someone else that pleased them stirs jealousy instead of thanks (romance.md).
    /// </summary>
    public static Stress? Hearsay(DiaryEntry e, int now, int windowStart, double sens, int heartsWithPlayer, MotiveOptions o)
    {
        int at = Gossip.HeardAt(e);
        if (at < windowStart || at > now)
            return null;
        if (Gossip.Original(e) is not { } original || Gossip.IsHeard(original))
            return null;
        StressorProfile? p0 = StressorTable.Of(original);
        if (p0 is null)
            return null;
        double days = (now - at) / (double)GameClock.TicksPerDay;
        bool aboutPlayer = string.Equals(e.Subject, MemoryStore.PlayerName, StringComparison.OrdinalIgnoreCase);
        bool drawn = aboutPlayer && heartsWithPlayer >= o.DrawnHearts;
        double relevance = drawn ? o.MaxRelevance : 1;
        double strength = p0.Magnitude * o.HearsayFactor * relevance * sens * Math.Pow(p0.ElasticDecay, days);
        if (drawn && original.Kind == "GiftReceived" && p0.Valence > 0)
            return new Stress(e.Subject, Motive.Jealous, "Heard:" + original.Kind, -1, strength, at);
        // Only a scandal (bad, and juicy enough) is a reason to act toward the player; any other
        // story about what they did to someone else is casual news that colours the mood.
        bool scandal = p0.Valence < 0 && (Gossip.BaseOf(e, StressorTable.JuicinessOf) ?? 0) >= o.HearsayActsFromJuiciness;
        return new Stress(e.Subject, p0.Motive, "Heard:" + original.Kind, p0.Valence, strength, at, MoodOnly: !scandal);
    }

    /// <summary>
    /// Venting (motives.md, "Using a motive up"): acting on hurt or jealousy never satisfies it,
    /// but each such act halves (<see cref="MotiveOptions.VentRelief"/>) the elastic part of the
    /// negative stresses from entries at or before it. Regard is unchanged. Pure.
    /// </summary>
    public static IReadOnlyList<Stress> Vent(IReadOnlyList<Stress> stresses, IReadOnlyList<int>? ventTicks, MotiveOptions o)
    {
        if (ventTicks is null || ventTicks.Count == 0)
            return stresses;
        return stresses.Select(s =>
        {
            if (s.Valence >= 0)
                return s;
            int vents = ventTicks.Count(v => v >= s.Tick);
            return vents == 0 ? s : s with { Strength = s.Strength * Math.Pow(o.VentRelief, vents) };
        }).ToList();
    }

    /// <summary>
    /// The plastic share an entry leaves in regard, before retention and sensitivity: the
    /// profile's share, or for a yielding kind <see cref="MotiveOptions.YieldPlastic"/> once
    /// <see cref="MotiveOptions.YieldCount"/> of the same kind about the same subject fall within
    /// <see cref="MotiveOptions.YieldWindowDays"/> (counting this one). Earlier entries come from
    /// <paramref name="diary"/>; the entry itself need not be in it yet.
    /// </summary>
    public static double PlasticShare(DiaryEntry entry, StressorProfile p, IReadOnlyList<DiaryEntry> diary, MotiveOptions o)
    {
        if (!p.Yields)
            return p.Plastic;
        int since = entry.AbsoluteTick - o.YieldWindowDays * GameClock.TicksPerDay;
        int count = 1 + diary.Count(e => !ReferenceEquals(e, entry) && e != entry
            && e.Kind == entry.Kind
            && string.Equals(e.Subject, entry.Subject, StringComparison.OrdinalIgnoreCase)
            && e.AbsoluteTick >= since && e.AbsoluteTick <= entry.AbsoluteTick);
        return count >= o.YieldCount ? o.YieldPlastic : 0;
    }
}
