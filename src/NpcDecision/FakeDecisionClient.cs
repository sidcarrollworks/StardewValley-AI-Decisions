namespace NpcDecision;

/// <summary>
/// Deterministic fake for tests and shadow mode: uniform choice probabilities, the mid-point of
/// the scale for a score, and 0.5 for a yes/no. No model, no network, no randomness.
/// </summary>
public sealed class FakeDecisionClient : IDecisionClient
{
    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => Uniform(options);

    public double Score(string context, double min, double max)
        => (min + max) / 2.0;

    public double YesNo(string context, string proposition)
        => 0.5;

    internal static IReadOnlyList<double> Uniform(IReadOnlyList<string> options)
    {
        if (options.Count == 0)
            return Array.Empty<double>();
        double p = 1.0 / options.Count;
        return options.Select(_ => p).ToArray();
    }
}
