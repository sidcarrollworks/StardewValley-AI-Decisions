using System.Globalization;
using System.Linq;
using NpcDecision;
using NpcMemory;

namespace NpcIntents;

/// <summary>
/// Picks what each NPC will say tomorrow, decided at sleep. The model makes the typed choices
/// (who speaks = yes/no; about what = choice over recent diary entries); the line is templated by
/// an <see cref="ILineRenderer"/>. Deterministic given the same snapshots and seed.
/// </summary>
public sealed class IntentPlanner
{
    private readonly IDecisionClient _decision;
    private readonly ILineRenderer _renderer;
    private readonly IntentPlannerOptions _options;

    public IntentPlanner(IDecisionClient decision, ILineRenderer renderer, IntentPlannerOptions? options = null)
    {
        _decision = decision;
        _renderer = renderer;
        _options = options ?? new IntentPlannerOptions();
    }

    /// <summary>
    /// Build tomorrow's intents from the NPC snapshots.
    /// </summary>
    /// <param name="snapshots">One snapshot per NPC (the mod builds these from each NPC's diary).</param>
    /// <param name="seed">Determinism seed for sampling from the model's probabilities.</param>
    /// <param name="sourceDay">The calendar day being slept on (<see cref="GameClock.DayIndex"/>). When
    /// given, only that day's diary entries are candidates, so tomorrow's "yesterday" is true; older
    /// entries are never offered. Null keeps every entry (tests, tools).</param>
    public IntentPlan Plan(IEnumerable<NpcMemorySnapshot> snapshots, int seed, int? sourceDay = null)
    {
        if (snapshots is null)
            return new IntentPlan(Array.Empty<IntentCandidate>());

        // One Random for the whole plan: sampling follows the input order, so the same snapshots
        // + seed always consume the stream identically.
        var random = new Random(seed);
        var scored = new List<(IntentCandidate Candidate, double Probability)>();

        foreach (NpcMemorySnapshot snapshot in snapshots)
        {
            // 1a. Nothing to talk about -> never ask, never speaks.
            if (snapshot is null || snapshot.RecentDiary is null || snapshot.RecentDiary.Count == 0)
                continue;

            IReadOnlyList<DiaryEntry> diary = sourceDay is { } day
                ? snapshot.RecentDiary.Where(e => e is not null && GameClock.DayIndex(e.AbsoluteTick) == day).ToList()
                : snapshot.RecentDiary;
            if (diary.Count == 0)
                continue; // nothing from the day just ended: an old entry is not news

            string context = BuildContext(diary, snapshot.Voice);

            // 1b. Who speaks: yes/no, probability must clear the threshold.
            // NaN-safe comparison: a NaN probability can never pass.
            double speak = _decision.YesNo(context, "does this NPC have something to say today?");
            if (!(speak >= _options.SpeakThreshold))
                continue;

            // 2a. About what: newest distinct entries only (diary is oldest first).
            IReadOnlyList<DiaryEntry> newest = TakeNewest(Distinct(diary), _options.MaxRecentDiaryEntries);
            if (newest.Count == 0)
                continue; // no valid options to hand Choose

            List<string> options = newest.Select(Summarize).ToList();
            IReadOnlyList<double> probabilities = _decision.Choose(options, context) ?? Array.Empty<double>();

            // 2b. Sample one entry from the returned distribution (never argmax).
            int index = SampleIndex(probabilities, options.Count, random);
            DiaryEntry entry = newest[index];

            // 3. Render the cited entry.
            // Delivered the morning after the source day, so its entries are one day old.
            int daysAgo = sourceDay is { } source ? source + 1 - GameClock.DayIndex(entry.AbsoluteTick) : 1;
            string line = _renderer.Render(snapshot.Npc, snapshot.Voice, entry, daysAgo);

            // 4. Novelty: never repeat a line this NPC already said.
            if (AlreadySaid(snapshot.RecentLines, line))
                continue;

            double cited = index < probabilities.Count ? probabilities[index] : 0.0;
            string reason = $"cited \"{options[index]}\" (sampled p={Format(cited)})";

            scored.Add((new IntentCandidate(snapshot.Npc, line, entry, reason), speak));
        }

        // 5. Highest yes/no first, name ascending as the deterministic tie-break, then cap.
        IEnumerable<IntentCandidate> ordered = scored
            .OrderByDescending(x => x.Probability)
            .ThenBy(x => x.Candidate.Npc, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Candidate)
            .Take(Math.Max(0, _options.MaxNpcsPerDay));

        return new IntentPlan(ordered);
    }

    /// <summary>Short context for the model: the voice anchor plus a compact diary summary.</summary>
    private string BuildContext(IReadOnlyList<DiaryEntry> diary, string voice)
    {
        IReadOnlyList<DiaryEntry> entries =
            TakeNewest(Distinct(diary), Math.Max(1, _options.MaxRecentDiaryEntries));
        string summary = string.Join("; ", entries.Select(Summarize));
        return $"voice: {voice}; recent diary: {summary}";
    }

    /// <summary>Drop repeats of the same summary, keeping the newest occurrence (oldest-first order kept).</summary>
    private static IReadOnlyList<DiaryEntry> Distinct(IReadOnlyList<DiaryEntry> diary)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<DiaryEntry>();
        for (int i = diary.Count - 1; i >= 0; i--)
        {
            if (diary[i] is { } entry && seen.Add(Summarize(entry)))
                kept.Add(entry);
        }
        kept.Reverse();
        return kept;
    }

    /// <summary>The newest <paramref name="max"/> entries, preserving their relative order.</summary>
    private static IReadOnlyList<DiaryEntry> TakeNewest(IReadOnlyList<DiaryEntry> diary, int max)
    {
        if (diary is null || diary.Count == 0 || max <= 0)
            return Array.Empty<DiaryEntry>();

        int take = Math.Min(diary.Count, max);
        var newest = new List<DiaryEntry>(take);
        for (int i = diary.Count - take; i < diary.Count; i++)
            newest.Add(diary[i]);
        return newest;
    }

    /// <summary>A short option string for one diary entry, e.g. "Saw Player at Pierre's General Store".</summary>
    private static string Summarize(DiaryEntry entry)
    {
        string summary = $"{entry.Kind} {entry.Subject}".Trim();
        if (!string.IsNullOrWhiteSpace(entry.Detail))
            summary += $" at {PlaceNames.Display(entry.Detail)}";
        return summary;
    }

    private static bool AlreadySaid(IReadOnlyList<string> recentLines, string line)
    {
        if (recentLines is null || recentLines.Count == 0)
            return false;

        foreach (string said in recentLines)
        {
            if (said is not null && string.Equals(said, line, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Pick one option index by sampling the returned probabilities. Never argmax, so behaviour
    /// varies, yet deterministic for a fixed <see cref="Random"/> seed. Falls back to a uniform
    /// pick when the probabilities are missing, empty, or degenerate (all zero / non-finite).
    /// </summary>
    private static int SampleIndex(IReadOnlyList<double> probabilities, int optionCount, Random random)
    {
        if (optionCount <= 0)
            return -1;

        if (probabilities is null || probabilities.Count == 0)
            return random.Next(optionCount);

        int count = Math.Min(probabilities.Count, optionCount);

        double total = 0.0;
        for (int i = 0; i < count; i++)
        {
            double p = probabilities[i];
            if (IsUsable(p))
                total += p;
        }

        if (total <= 0.0)
            return random.Next(optionCount);

        double target = random.NextDouble() * total;
        double cumulative = 0.0;
        for (int i = 0; i < count; i++)
        {
            double p = probabilities[i];
            if (IsUsable(p))
                cumulative += p;
            if (target < cumulative)
                return i;
        }

        return count - 1;
    }

    private static bool IsUsable(double p)
        => p > 0.0 && !double.IsNaN(p) && !double.IsInfinity(p);

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
