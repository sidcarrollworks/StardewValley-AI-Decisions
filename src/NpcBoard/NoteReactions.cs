using System.Globalization;
using System.Text;
using NpcDecision;
using NpcIntents;

namespace NpcBoard;

/// <summary>
/// How a villager takes a note on the town board (docs/spec/notice-board.md). Saved as an int
/// once notes are remembered, so values are append-only: never insert or reorder.
/// </summary>
public enum NoteReaction
{
    Indifferent = 0,
    Amused = 1,
    Touched = 2,
    Curious = 3,
    Annoyed = 4,
    Offended = 5,
}

/// <summary>One villager who reads the note: its NPC card (rendered on the game thread) and its
/// regard for the note's author (-1..1; 0 when none).</summary>
public sealed record NoteReader(string Npc, string Card, double RegardForAuthor = 0);

/// <summary>A villager's reaction to a note: the model's probability for each reaction (in
/// <see cref="NoteReactions.Order"/>), the reaction picked, whether that was the fallback
/// (the answers were flat: a model failure or the fake), the emote it would show (-1: none),
/// and the line it would say the next time the player talks to it (null: says nothing).</summary>
public sealed record NoteReactionResult(
    string Npc,
    NoteReaction Reaction,
    IReadOnlyList<double> Probabilities,
    bool FellBack,
    int Emote,
    string? Line);

/// <summary>
/// The notice-board experiment (roadmap step 21, first stage): a note in the player's own words,
/// and how each villager would react to it. The model never writes text (AGENTS.md rule 6): the
/// note goes into the state as quoted data, the model answers a typed choice over reactions, and
/// the reaction maps to a confirmed emote and a templated line. Calls the model, so run it off the
/// game thread, through a <c>ResilientDecisionClient</c> (rule 5). Deterministic for the same
/// answers.
/// </summary>
public static class NoteReactions
{
    /// <summary>About two sentences (Sid, 2026-10-02, for journal pages; the same cap suits notes).</summary>
    public const int MaxNoteChars = 200;

    /// <summary>The reactions offered to the model, in this order.</summary>
    public static readonly IReadOnlyList<NoteReaction> Order = new[]
    {
        NoteReaction.Amused, NoteReaction.Touched, NoteReaction.Curious,
        NoteReaction.Annoyed, NoteReaction.Offended, NoteReaction.Indifferent,
    };

    /// <summary>Each reaction as an option: what the reader does, in plain words.</summary>
    public static string Option(NoteReaction r) => r switch
    {
        NoteReaction.Amused => "finds it funny",
        NoteReaction.Touched => "is touched by it",
        NoteReaction.Curious => "is curious about it",
        NoteReaction.Annoyed => "is annoyed by it",
        NoteReaction.Offended => "is offended by it",
        _ => "doesn't care about it",
    };

    /// <summary>The emote shown at the board (ids confirmed in the 1.6.15 decompile,
    /// stardew-source-notes.md): happy 32, heart 20, question 8, angry 12, x 36; none for
    /// indifference.</summary>
    public static int EmoteOf(NoteReaction r) => r switch
    {
        NoteReaction.Amused => 32,
        NoteReaction.Touched => 20,
        NoteReaction.Curious => 8,
        NoteReaction.Annoyed => 12,
        NoteReaction.Offended => 36,
        _ => -1,
    };

    /// <summary>What the reader says the next time the player talks to it; null for indifference.
    /// Plain templates for the experiment; voices come with the other lines (docs/spec/text.md).</summary>
    public static string? LineOf(NoteReaction r) => r switch
    {
        NoteReaction.Amused => LineSanitizer.Sanitize("I saw your note on the board. That made me laugh."),
        NoteReaction.Touched => LineSanitizer.Sanitize("I read your note on the board. That was sweet of you."),
        NoteReaction.Curious => LineSanitizer.Sanitize("I saw your note on the board. What was that about?"),
        NoteReaction.Annoyed => LineSanitizer.Sanitize("That note on the board... really?"),
        NoteReaction.Offended => LineSanitizer.Sanitize("I didn't appreciate that note on the board."),
        _ => null,
    };

    /// <summary>
    /// The player's text, made safe for the state: the line sanitizer's characters stripped, line
    /// breaks and runs of spaces folded, double quotes turned to single (the note is quoted in the
    /// state, so it can't close its own quote), and cut to <see cref="MaxNoteChars"/> at a word
    /// boundary with an ellipsis.
    /// </summary>
    public static string Clean(string? text, int maxChars = MaxNoteChars)
    {
        string s = LineSanitizer.Sanitize(text ?? string.Empty).Replace('"', '\'');
        var folded = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            char ch = char.IsWhiteSpace(c) || char.IsControl(c) ? ' ' : c;
            if (ch == ' ' && (folded.Length == 0 || folded[^1] == ' '))
                continue;
            folded.Append(ch);
        }
        string clean = folded.ToString().Trim();
        if (clean.Length <= maxChars)
            return clean;
        int room = Math.Max(1, maxChars - 3); // the ellipsis counts toward the cap
        int cut = clean.LastIndexOf(' ', room);
        return (cut > room / 2 ? clean[..cut] : clean[..room]).TrimEnd() + "...";
    }

    /// <summary>The model's state: the reader's card, then the note as quoted data, then how the
    /// reader feels about its author. Cut to the state budget by whole lines.</summary>
    public static string State(NoteReader reader, string cleanNote, string author)
    {
        var state = new DecisionState();
        if (!string.IsNullOrWhiteSpace(reader.Card))
            state.Add(3, reader.Card);
        state.Add(2, $"a note pinned on the town notice board, written by {author}:\n\"{cleanNote}\"");
        state.Add(1, $"{reader.Npc} reads it. How {reader.Npc} feels about {author}: {RegardWords(reader.RegardForAuthor)}");
        return state.Build();
    }

    /// <summary>
    /// Each reader's reaction to one note, in name order. Flat answers (all options within 1e-9 of
    /// each other: the fallback, or the plain fake) read as indifference and are marked
    /// <see cref="NoteReactionResult.FellBack"/>; otherwise the most likely reaction, ties going to
    /// the earlier option.
    /// </summary>
    public static IReadOnlyList<NoteReactionResult> React(
        IDecisionClient decision, IEnumerable<NoteReader> readers, string note, string author = "the player")
    {
        string clean = Clean(note);
        IReadOnlyList<string> options = Order.Select(Option).ToList();
        var results = new List<NoteReactionResult>();
        foreach (NoteReader reader in readers.OrderBy(r => r.Npc, StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyList<double> p = decision.Choose(options, State(reader, clean, author)) ?? Array.Empty<double>();
            bool flat = p.Count != options.Count || p.Any(double.IsNaN) || p.Max() - p.Min() < 1e-9;
            NoteReaction reaction = NoteReaction.Indifferent;
            if (!flat)
            {
                int best = 0;
                for (int k = 1; k < p.Count; k++)
                    if (p[k] > p[best])
                        best = k;
                reaction = Order[best];
            }
            results.Add(new NoteReactionResult(reader.Npc, reaction, p.ToList(), flat, EmoteOf(reaction), LineOf(reaction)));
        }
        return results;
    }

    /// <summary>
    /// The experiment's report, for the console and the log: one line per villager with its
    /// reaction and the model's top answers, then how the town split, and whether the answers
    /// barely differed between villagers (the character-spread worry, docs/spec/laya.md): the
    /// biggest spread of any one option's probability across readers.
    /// </summary>
    public static IReadOnlyList<string> Report(string note, IReadOnlyList<NoteReactionResult> results)
    {
        var lines = new List<string> { $"note: \"{Clean(note)}\"" };
        foreach (NoteReactionResult r in results)
        {
            string top = r.FellBack ? "no answer (fallback)" : string.Join(", ", r.Probabilities
                .Select((p, k) => (p, k)).OrderByDescending(x => x.p).Take(2)
                .Select(x => string.Format(CultureInfo.InvariantCulture, "{0} {1:0.00}", Order[x.k], x.p)));
            lines.Add($"  {r.Npc}: {r.Reaction} ({top})" + (r.Line is null ? "" : $" - \"{r.Line}\""));
        }
        lines.Add("town: " + string.Join(", ", results.GroupBy(r => r.Reaction)
            .OrderByDescending(g => g.Count()).ThenBy(g => (int)g.Key)
            .Select(g => $"{g.Key} {g.Count()}")));
        double spread = SpreadAcrossReaders(results);
        if (!double.IsNaN(spread))
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "spread: the most any reaction's probability varied between villagers was {0:0.00}{1}",
                spread, spread < 0.05 ? " (flat: the villagers barely differ)" : ""));
        return lines;
    }

    /// <summary>The largest max-minus-min of one option's probability across the readers that got
    /// a real answer; NaN with fewer than two.</summary>
    public static double SpreadAcrossReaders(IReadOnlyList<NoteReactionResult> results)
    {
        List<NoteReactionResult> answered = results.Where(r => !r.FellBack).ToList();
        if (answered.Count < 2)
            return double.NaN;
        return Enumerable.Range(0, Order.Count)
            .Select(k => answered.Max(r => r.Probabilities[k]) - answered.Min(r => r.Probabilities[k]))
            .Max();
    }

    private static string RegardWords(double regard) => regard switch
    {
        >= 0.5 => "fond of them",
        >= 0.2 => "likes them",
        <= -0.5 => "holds a grudge against them",
        <= -0.2 => "a bit sore with them",
        _ => "no strong feelings",
    };
}
