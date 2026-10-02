using System.Diagnostics;
using System.Text.RegularExpressions;
using NpcDecision;
using NpcMinds.Playtest;

namespace NpcMinds;

/// <summary>
/// A pass-through decision client that writes every question and answer to a
/// <see cref="RingLog{T}"/> for the NPC Minds viewer. It returns exactly what the wrapped
/// <see cref="ResilientDecisionClient"/> returns, so decisions are unchanged; it only watches.
/// It runs wherever the calls run (the ladder worker, the plan job), never on the game thread.
/// <para>
/// "Fell back" is read from the wrapped client's <see cref="ResilientDecisionClient.Fallbacks"/>
/// counter before and after each call. The mod builds one wrapper per caller and each caller is
/// a single thread, so the difference belongs to that call.
/// </para>
/// </summary>
public sealed class RecordingDecisionClient : IDecisionClient, IBatchDecisionClient
{
    /// <summary>How much of the model's state each call keeps, for the viewer's hover text.</summary>
    public const int ContextHeadChars = 400;

    private static readonly Regex PropositionNpc = new(@"^(?:should|does|is|will|would)\s+(\S+)\s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ResilientDecisionClient _inner;
    private readonly RingLog<DecisionCall> _log;
    private readonly string _caller;
    private readonly SpreadTable? _spread;
    private readonly PlaytestLog? _playtest; // the playtest log's model records (docs/spec/debug-tools.md)

    public RecordingDecisionClient(ResilientDecisionClient inner, RingLog<DecisionCall> log, string caller,
        SpreadTable? spread = null, PlaytestLog? playtest = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _caller = caller ?? "";
        _spread = spread;
        _playtest = playtest;
    }

    public IReadOnlyList<double> Choose(IReadOnlyList<string> options, string context)
        => Record("choice", context, null, () => _inner.Choose(options, context),
            probabilities => Pair(options, probabilities), "Which option fits best?");

    public double Score(string context, double min, double max)
        => Record("score", context, null, () => _inner.Score(context, min, max),
            value => new[] { new CallAnswer($"score {min:0.##}..{max:0.##}", value) }, $"score {min:0.##}..{max:0.##}");

    public double YesNo(string context, string proposition)
        => Record("yesno", context, proposition, () => _inner.YesNo(context, proposition),
            p => new[] { new CallAnswer("yes", p) }, proposition);

    public IReadOnlyList<Answer> Ask(string state, IReadOnlyList<Question> questions)
    {
        string? proposition = questions?.OfType<YesNoQuestion>().FirstOrDefault()?.Proposition;
        string question = string.Join(" + ", (questions ?? Array.Empty<Question>()).Select(Describe));
        IReadOnlyList<Answer> result = Record("batch", state, proposition, () => _inner.Ask(state, questions!),
            answers => Flatten(questions ?? Array.Empty<Question>(), answers), question);
        // Per-question spread recording: each yes/no question gets its own template row.
        if (_spread is not null && questions is not null)
        {
            foreach (Question q in questions)
            {
                if (q is not YesNoQuestion y)
                    continue;
                string? npc = NpcOf(state, y.Proposition);
                double value = result.FirstOrDefault(a => a?.Id == y.Id)?.YesNo ?? double.NaN;
                _spread.Record(SpreadTable.ReplaceNpc(y.Proposition, npc), npc, value);
            }
        }
        return result;
    }

    private T Record<T>(string type, string? context, string? proposition, Func<T> call,
        Func<T, IReadOnlyList<CallAnswer>> answers, string question)
    {
        int before = _inner.Fallbacks;
        var clock = Stopwatch.StartNew();
        T result = call(); // the resilient wrapper never throws on a model failure
        clock.Stop();
        bool fellBack = _inner.Fallbacks != before;

        try
        {
            IReadOnlyList<CallAnswer> shown = answers(result);
            string? npc = NpcOf(context, proposition);
            _log.Add(seq => new DecisionCall(seq, DateTime.UtcNow, _caller, type,
                npc, question, shown, clock.Elapsed.TotalMilliseconds,
                fellBack, Head(context)));
            // The spread panel's table: observe (never alter) the answer under the question
            // template. Choice calls have per-call options, so they carry no single number; the
            // per-question recording for batches is in Ask.
            string template = SpreadTable.ReplaceNpc(question, npc);
            if (_spread is not null && type is "yesno" or "score" && shown.Count > 0)
                _spread.Record(template, npc, shown[0].Value);

            // The playtest log's model record (docs/spec/debug-tools.md, "Playtest log"): the same
            // view the call log gets, queued for the game thread. Reads only; a model failure must
            // never change a decision, so recording stays best effort.
            double? answer = (type is "yesno" or "score") && shown.Count > 0 ? shown[0].Value : null;
            if (answer is { } value && double.IsNaN(value))
                answer = null; // JSON cannot carry NaN; the spread table drops it too
            _playtest?.QueueFromWorker(new ModelCallRecord(_caller, type, npc, template, question, answer,
                clock.Elapsed.TotalMilliseconds, fellBack)
            {
                Tick = 0, // Record has no game-clock context on the worker (the parent wires a tick later)
            });
        }
        catch
        {
            // Recording is best effort: a display problem must never change a decision.
        }
        return result;
    }

    /// <summary>
    /// Who a call is about: the state's "npc: Name" line (every planner state starts with the
    /// NPC card), else the name after "should"/"does" in the proposition (the ladder's
    /// "should Abigail try ..."). Null when neither is there.
    /// </summary>
    public static string? NpcOf(string? context, string? proposition)
    {
        if (!string.IsNullOrEmpty(context))
        {
            foreach (string line in context.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("npc:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = trimmed.Substring(4).Trim();
                    if (name.Length > 0)
                        return name;
                }
            }
        }
        if (!string.IsNullOrEmpty(proposition))
        {
            Match match = PropositionNpc.Match(proposition.Trim());
            if (match.Success)
                return match.Groups[1].Value;
        }
        return null;
    }

    private static string Head(string? context)
    {
        if (string.IsNullOrEmpty(context))
            return "";
        return context.Length <= ContextHeadChars ? context : context.Substring(0, ContextHeadChars) + "...";
    }

    private static string Describe(Question q) => q switch
    {
        YesNoQuestion y => y.Proposition,
        ChoiceQuestion c => $"pick one of {c.Options.Count}",
        ScoreQuestion s => $"score {s.Min:0.##}..{s.Max:0.##}",
        _ => q?.Id ?? "",
    };

    private static IReadOnlyList<CallAnswer> Pair(IReadOnlyList<string> options, IReadOnlyList<double>? probabilities)
    {
        var shown = new List<CallAnswer>();
        for (int i = 0; i < (options?.Count ?? 0); i++)
            shown.Add(new CallAnswer(options![i], probabilities is not null && i < probabilities.Count ? probabilities[i] : double.NaN));
        return shown;
    }

    private static IReadOnlyList<CallAnswer> Flatten(IReadOnlyList<Question> questions, IReadOnlyList<Answer>? answers)
    {
        var shown = new List<CallAnswer>();
        foreach (Question q in questions)
        {
            Answer? a = answers?.FirstOrDefault(x => x?.Id == q.Id);
            switch (q)
            {
                case YesNoQuestion:
                    shown.Add(new CallAnswer("yes", a?.YesNo ?? double.NaN));
                    break;
                case ChoiceQuestion c:
                    shown.AddRange(Pair(c.Options, a?.Probabilities));
                    break;
                case ScoreQuestion:
                    shown.Add(new CallAnswer("score", a?.Score ?? double.NaN));
                    break;
            }
        }
        return shown;
    }
}
