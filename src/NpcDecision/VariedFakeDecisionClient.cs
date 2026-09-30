using System.Globalization;
using NpcSchedules;

namespace NpcDecision;

/// <summary>
/// A deterministic, non-uniform fake (docs/spec/laya.md, "Data model"): every answer derives from
/// FNV-1a over the state and the question, so shadow logs show varied but stable choices without
/// a model. Selected with <c>DecisionBackend = "Varied"</c>; the plain <see cref="FakeDecisionClient"/>
/// stays the default and the test baseline. No randomness, no network.
/// </summary>
public sealed class VariedFakeDecisionClient : IDecisionClient
{
    /// <summary>
    /// choice: a raw weight per option from the hash, normalized to probabilities summing to 1.
    /// The option's index is part of the hash, so two identical option texts get different
    /// weights. Raw weight for option i = (unchecked uint of
    /// Fnv1a.Seed(context, "|choice|", i.ToString(), "|", options[i])) % 100 + 1 (i.e. 1..100).
    /// Empty options give an empty result. Never uniform-by-construction across differing options.
    /// </summary>
    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
    {
        if (options.Count == 0)
            return Array.Empty<double>();

        var probabilities = new double[options.Count];
        double total = 0.0;
        for (int i = 0; i < options.Count; i++)
        {
            double weight = unchecked((uint)Fnv1a.Seed(context, "|choice|", i.ToString(CultureInfo.InvariantCulture), "|", options[i])) % 100 + 1;
            probabilities[i] = weight;
            total += weight;
        }

        for (int i = 0; i < probabilities.Length; i++)
            probabilities[i] /= total;

        return probabilities;
    }

    /// <summary>
    /// score: min + (max - min) * frac where frac = (unchecked uint of
    /// Fnv1a.Seed(context, "|score|", min.ToString(), "|", max.ToString())) % 1000 / 1000.0.
    /// </summary>
    public double Score(string context, double min, double max)
    {
        double frac = unchecked((uint)Fnv1a.Seed(context, "|score|", min.ToString(CultureInfo.InvariantCulture), "|", max.ToString(CultureInfo.InvariantCulture))) % 1000 / 1000.0;
        return min + (max - min) * frac;
    }

    /// <summary>
    /// noul: (unchecked uint of Fnv1a.Seed(context, "|noul|", proposition)) % 101 / 100.0,
    /// a probability in [0, 1].
    /// </summary>
    public double YesNo(string context, string proposition)
        => unchecked((uint)Fnv1a.Seed(context, "|noul|", proposition)) % 101 / 100.0;
}
