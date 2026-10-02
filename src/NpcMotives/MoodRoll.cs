using NpcMemory;
using NpcSchedules;
using NpcTemperament;

namespace NpcMotives;

/// <summary>
/// Today's mood (docs/spec/motives.md, "Mood: earned, with a small daily tilt"). Mostly earned
/// from recent events; a small deterministic daily roll tips coin-flips. Pure and reproducible:
/// the roll comes from FNV-1a of (seed, "mood", npc, absolute day), never a clock or a Random.
/// </summary>
public static class MoodRoll
{
    /// <summary>The daily roll, -1..1, and whether today is a rare out-of-character day.
    /// Triangular (two uniforms averaged and centred), skewed by the emotion biases; on a tail day
    /// (<see cref="MotiveOptions.TailChance"/>) it is set against the character's lean.</summary>
    public static (double Roll, bool Tail) Roll(string npc, int dayIndex, int seed, Temperament t, MotiveOptions o)
    {
        double u1 = Uniform(seed, npc, dayIndex, "a");
        double u2 = Uniform(seed, npc, dayIndex, "b");
        double tail = Uniform(seed, npc, dayIndex, "tail");
        double lean = t.Happiness - (t.Anger + t.Sadness) / 2;
        if (tail < o.TailChance)
        {
            // Against the character's lean, strongly: a bad day for Evelyn, a sunny one for Shane.
            double size = 0.8 + 0.2 * u1;
            return (lean >= 0 ? -size : size, true);
        }
        double triangular = (u1 + u2) - 1; // -1..1, peaked at 0
        return (Math.Clamp(triangular + o.RollSkew * lean, -1, 1), false);
    }

    /// <summary>Earned mood: the valence-weighted sum of the elastic stresses, clamped to -1..1.</summary>
    public static double Earned(IReadOnlyList<Stress> stresses)
        => Math.Clamp(stresses.Sum(s => s.Valence * s.Strength), -1, 1);

    public static Mood Today(string npc, int now, int seed, Temperament t, IReadOnlyList<Stress> stresses, MotiveOptions o)
    {
        (double roll, bool tailDay) = Roll(npc, GameClock.DayIndex(now), seed, t, o);
        double earned = Earned(stresses);
        double outlook = Math.Clamp(earned + o.RollWeight * Stresses.SensitivityFactor(t) * roll, -1, 1);
        return new Mood(earned, roll, tailDay, outlook);
    }

    private static double Uniform(int seed, string npc, int day, string salt)
    {
        int h = Fnv1a.Seed(seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "mood", npc, day.ToString(System.Globalization.CultureInfo.InvariantCulture), salt);
        return (uint)h / 4294967296.0;
    }
}
