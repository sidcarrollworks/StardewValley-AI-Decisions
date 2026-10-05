namespace UnderGlass.Sim;

/// <summary>
/// Deterministic randomness (SplitMix64). Every draw comes from a stream named by strings, so the
/// same seed and the same names give the same numbers on every machine and in every order of
/// evaluation. Never use System.Random or string.GetHashCode here.
/// </summary>
public static class Rng
{
    /// <summary>FNV-1a 64 over the parts, separated, so ("ab","c") and ("a","bc") differ.</summary>
    public static ulong Hash(params string[] parts)
    {
        ulong h = 14695981039346656037UL;
        foreach (string part in parts)
        {
            foreach (char c in part)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            h ^= 0x1F;
            h *= 1099511628211UL;
        }
        return h;
    }

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A uniform number in [0, 1) for this seed and these names.</summary>
    public static double Unit(long seed, params string[] parts)
        => (Mix(Hash(parts) ^ Mix((ulong)seed)) >> 11) * (1.0 / (1UL << 53));

    /// <summary>A uniform integer in [min, max] (inclusive).</summary>
    public static int Range(long seed, int min, int max, params string[] parts)
        => min + (int)Math.Floor(Unit(seed, parts) * (max - min + 1));
}
