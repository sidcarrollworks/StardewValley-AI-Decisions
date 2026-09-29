namespace NpcDecision;

/// <summary>
/// VERIFY — no real Jev client yet. Jev is TypeSafe's hosted typed-decision API; its exact
/// endpoint, request shape and auth are not confirmed, and the brief forbids inventing them.
/// Wire this up only once the real API docs are in hand.
/// </summary>
public sealed class JevDecisionClient : IDecisionClient
{
    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => throw new NotImplementedException("Jev endpoint not implemented (verify API first).");

    public double Score(string context, double min, double max)
        => throw new NotImplementedException("Jev endpoint not implemented (verify API first).");

    public double YesNo(string context, string proposition)
        => throw new NotImplementedException("Jev endpoint not implemented (verify API first).");
}
