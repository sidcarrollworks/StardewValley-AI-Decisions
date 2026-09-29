namespace NpcDecision;

/// <summary>
/// VERIFY — no real Laya client yet. Laya (Convai Innovations) runs locally with three question
/// types (choice / score / yes-no); its exact local-server protocol is not confirmed and the
/// brief forbids inventing it. Wire this up only once the real docs are in hand.
/// </summary>
public sealed class LayaDecisionClient : IDecisionClient
{
    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => throw new NotImplementedException("Laya local server not implemented (verify protocol first).");

    public double Score(string context, double min, double max)
        => throw new NotImplementedException("Laya local server not implemented (verify protocol first).");

    public double YesNo(string context, string proposition)
        => throw new NotImplementedException("Laya local server not implemented (verify protocol first).");
}
