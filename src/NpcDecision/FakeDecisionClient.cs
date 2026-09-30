namespace NpcDecision;

/// <summary>
/// Deterministic fake for tests and shadow mode: uniform choice probabilities, the mid-point of
/// the scale for a score, and 0.5 for a yes/no. No model, no network, no randomness.
/// <para>
/// Batching (docs/spec/laya.md, "Data model") loops over the questions and answers each one with
/// the single-question method above, so ids and order come straight from the caller's list.
/// </para>
/// </summary>
public sealed class FakeDecisionClient : IDecisionClient, IBatchDecisionClient
{
    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => Uniform(options);

    public double Score(string context, double min, double max)
        => (min + max) / 2.0;

    public double YesNo(string context, string proposition)
        => 0.5;

    /// <summary>One answer per question, in the questions' order, with the caller's ids.</summary>
    public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
    {
        if (questions is null)
            throw new ArgumentNullException(nameof(questions));

        var answers = new List<Answer>(questions.Count);
        foreach (Question question in questions)
            answers.Add(AnswerOne(state, question));
        return answers;
    }

    /// <summary>Dispatches to the fake's own single-question methods (no logic of its own).</summary>
    private Answer AnswerOne(string state, Question question) => question switch
    {
        ChoiceQuestion choice => Answer.FromChoice(choice.Id, Choose(choice.Options, state)),
        ScoreQuestion score => Answer.FromScore(score.Id, Score(state, score.Min, score.Max)),
        YesNoQuestion yesNo => Answer.FromYesNo(yesNo.Id, YesNo(state, yesNo.Proposition)),
        _ => throw new ArgumentException($"Unknown question type {question.GetType().Name}.", nameof(question)),
    };

    internal static IReadOnlyList<double> Uniform(IReadOnlyList<string> options)
    {
        if (options.Count == 0)
            return Array.Empty<double>();
        double p = 1.0 / options.Count;
        return options.Select(_ => p).ToArray();
    }
}
