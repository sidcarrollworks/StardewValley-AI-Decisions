namespace NpcDecision;

/// <summary>
/// A typed decision engine (Laya, run locally; or the deterministic fake). Three question types, all returning numbers, never
/// free text: choice (probabilities over the game's currently-valid options), score (on an
/// ordered scale), and yes/no (probability of the proposition). Options are always the actions
/// the game actually supports, so the client can never propose an invalid one.
/// </summary>
public interface IDecisionClient
{
    /// <summary>Probability per option, one entry per option in the same order.</summary>
    IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context);

    /// <summary>A score on an ordered scale from <paramref name="min"/> to <paramref name="max"/> (inclusive).</summary>
    double Score(string context, double min, double max);

    /// <summary>Probability in [0,1] that the proposition is true.</summary>
    double YesNo(string context, string proposition);
}
