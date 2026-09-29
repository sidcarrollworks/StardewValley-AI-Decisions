namespace NpcSchedules;

/// <summary>
/// FNV-1a 32-bit hash. Used instead of <c>string.GetHashCode</c>, which is randomized per
/// process in .NET Core and would break the determinism guarantee ("same inputs and seed
/// always give the same prior").
/// </summary>
public static class Fnv1a
{
    public static int Hash32(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
                hash ^= (uint)(c >> 16) * 16777619u; // include the second UTF-16 unit too
                hash ^= (uint)(c >> 16);
            }
            return (int)hash;
        }
    }

    /// <summary>Combine several strings into one seed, order-sensitively.</summary>
    public static int Seed(params string[] parts)
        => Hash32(string.Join("\u001f", parts));
}
