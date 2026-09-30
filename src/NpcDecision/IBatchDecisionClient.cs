namespace NpcDecision;

/// <summary>
/// A batch of typed questions about one state, answered in one round trip where the backend
/// supports it (docs/spec/laya.md, "Data model"): Laya answers several questions per request;
/// fakes and wrappers without real batching loop over the questions. Question ids are the
/// caller's own strings and come back unchanged on the answers.
/// </summary>
public interface IBatchDecisionClient
{
    /// <summary>
    /// Answers in the questions' order. An implementation that only partially answered (a backend
    /// that skipped an id) returns fewer entries; the resilient wrapper fills the gaps with
    /// per-question fallbacks.
    /// </summary>
    IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions);
}

/// <summary>One typed question, identified by the caller's <paramref name="Id"/>.</summary>
public abstract record Question(string Id);

/// <summary>choice: probabilities over the options (order preserved), like <see cref="IDecisionClient.Choose"/>.</summary>
public sealed record ChoiceQuestion(string Id, IReadOnlyList<string> Options) : Question(Id);

/// <summary>noul: P(proposition), like <see cref="IDecisionClient.YesNo"/>.</summary>
public sealed record YesNoQuestion(string Id, string Proposition) : Question(Id);

/// <summary>score: a value on the ordered scale min..max, like <see cref="IDecisionClient.Score"/>.</summary>
public sealed record ScoreQuestion(string Id, double Min, double Max) : Question(Id);

/// <summary>One answer; exactly one field is non-null depending on the question type.</summary>
public sealed record Answer(string Id, IReadOnlyList<double>? Probabilities, double? Score, double? YesNo)
{
    public static Answer FromChoice(string id, IReadOnlyList<double> probabilities) => new(id, probabilities, null, null);
    public static Answer FromScore(string id, double value) => new(id, null, value, null);
    public static Answer FromYesNo(string id, double value) => new(id, null, null, value);
}
