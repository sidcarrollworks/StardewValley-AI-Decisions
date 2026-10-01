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
    private readonly Newsworthiness? _news;
    private readonly IntentPlannerOptions _options;

    public IntentPlanner(IDecisionClient decision, ILineRenderer renderer, Newsworthiness? news = null, IntentPlannerOptions? options = null)
    {
        _decision = decision;
        _renderer = renderer;
        _news = news;
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

        // One Random for the whole plan. Pass 1 builds one Pick per speaking NPC; pass 2 samples
        // them in RANKED order, so the seeded stream is consumed deterministically.
        var random = new Random(seed);
        var picks = new List<Pick>();

        foreach (NpcMemorySnapshot snapshot in snapshots)
        {
            // 1a. Nothing to talk about -> never ask, never speaks.
            if (snapshot is null || snapshot.RecentDiary is null || snapshot.RecentDiary.Count == 0)
                continue;

            IReadOnlyList<DiaryEntry> diary = snapshot.RecentDiary
                .Where(e => e is not null && !Skipped(e.Kind))
                .Where(e => sourceDay is not { } day || GameClock.DayIndex(e.AbsoluteTick) == day)
                .ToList();
            if (diary.Count == 0)
                continue; // nothing from the day just ended: an old entry is not news

            // 2a. About what: the highest-news distinct entries when newsworthiness is available,
            // else the newest distinct entries (legacy). A snapshot with news but nothing above
            // MinNews is skipped WITHOUT a model call (docs/spec/diary.md). Each offered entry
            // carries its news score: it drives the offer order, the speaker ranking AND the
            // pick blend (intents.md, "Deterministic rules").
            List<(DiaryEntry Entry, double Score)> offered;
            if (_news is not null && snapshot.News is { } newsContext)
            {
                // Playtest review: a player Saw right after a conversation reads oddly ("I saw you
                // at the saloon yesterday" from the NPC you talked to there). Drop the Saw when
                // the same diary holds a Talked entry from the offered day (the day just ended
                // when sourceDay is set, which the mod always passes).
                bool talkedToday = diary.Any(e =>
                    e.Kind != null && e.Kind.Equals("Talked", StringComparison.OrdinalIgnoreCase)
                    && IsPlayerSubject(e.Subject));
                var candidates = new List<(DiaryEntry Entry, double Score)>();
                foreach (DiaryEntry e in Distinct(diary))
                {
                    if (talkedToday
                        && e.Kind != null && e.Kind.Equals("Saw", StringComparison.OrdinalIgnoreCase)
                        && IsPlayerSubject(e.Subject))
                        continue; // already talked today: the sighting is not the news
                    double score = _news.Score(e, newsContext);
                    if (score >= _news.Options.MinNews)
                        candidates.Add((e, score));
                }
                offered = candidates
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Entry.AbsoluteTick)
                    .Take(_options.MaxRecentDiaryEntries)
                    .ToList();
                if (offered.Count == 0)
                    continue; // nothing newsworthy: no model call
            }
            else
            {
                offered = TakeNewest(Distinct(diary), _options.MaxRecentDiaryEntries)
                    .Select(e => (e, 0.0))
                    .ToList();
                if (offered.Count == 0)
                    continue; // no valid options to hand Choose
            }

            double bestNews = offered.Max(x => x.Score);

            string context = BuildContext(snapshot.Npc, offered, snapshot.Voice, snapshot.Card, sourceDay);
            // The wording was picked by the rewording experiment (sidecar/eval/speak_experiments.md):
            // "does X have news for the player?" separates newsy from dull states best on BOTH
            // checkpoints (typed-decisions and english).
            string speakProposition = SpeakProposition(snapshot.Npc);
            List<string> options = offered
                .Select(x => NewsPhrasing.Sentence(snapshot.Npc, x.Entry, DaysAgo(x.Entry, sourceDay)))
                .ToList();

            // 1b + 2b. Who speaks and about what: one batched request when the backend supports
            // it (docs/spec/laya.md, "Data model": halves the overnight round trips); the
            // YesNo-then-Choose pair otherwise. Missing answers fall back per question. With a
            // single option there is nothing to pick: the pick question is skipped entirely.
            double speak;
            IReadOnlyList<double> probabilities;
            if (_decision is IBatchDecisionClient batch)
            {
                var questions = new List<Question> { new YesNoQuestion("speak", speakProposition) };
                if (options.Count > 1)
                    questions.Add(new ChoiceQuestion("pick", options));
                IReadOnlyList<Answer> answers = batch.Ask(context, questions);
                Answer? speakAnswer = answers.FirstOrDefault(a => a.Id == "speak");
                Answer? pickAnswer = answers.FirstOrDefault(a => a.Id == "pick");
                speak = speakAnswer?.YesNo ?? 0.5;
                probabilities = options.Count > 1
                    ? pickAnswer?.Probabilities ?? options.Select(_ => 1.0 / options.Count).ToArray()
                    : new[] { 1.0 };
            }
            else
            {
                speak = _decision.YesNo(context, speakProposition);
                probabilities = options.Count > 1
                    ? _decision.Choose(options, context) ?? Array.Empty<double>()
                    : new[] { 1.0 };
            }

            // NaN-safe comparison: a NaN probability can never pass.
            if (!(speak >= _options.SpeakThreshold))
                continue;

            picks.Add(new Pick(snapshot.Npc, snapshot.Voice, offered, options, speak, bestNews,
                BlendedWeights(probabilities, offered), snapshot.RecentLines, snapshot.News?.Hearts ?? 0));
        }

        // Ranking (week review, finding 4): best news first — the narrow yes/no band must not
        // decide the speakers — then the yes/no probability, then name ascending.
        picks.Sort(static (a, b) =>
        {
            int news = b.BestNews.CompareTo(a.BestNews);
            if (news != 0)
                return news;
            int prob = b.Speak.CompareTo(a.Speak);
            if (prob != 0)
                return prob;
            return string.Compare(a.Npc, b.Npc, StringComparison.OrdinalIgnoreCase);
        });

        // Pass 2: sample in ranked order. Novelty (no line this NPC already said) and one-topic-
        // per-subject (the higher-hearts speaker keeps a shared event) each get ONE fall to the
        // next-best option before the NPC is skipped (intents.md, "Deterministic rules").
        var ordered = new List<IntentCandidate>();
        var owners = new Dictionary<string, Owner>(StringComparer.OrdinalIgnoreCase);
        foreach (Pick pick in picks)
        {
            if (ordered.Count >= Math.Max(0, _options.MaxNpcsPerDay))
                break;

            var excluded = new HashSet<int>();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int? sampled = pick.Sample(random, excluded);
                if (sampled is not { } index)
                    break; // nothing left to fall back on

                (DiaryEntry entry, double _) = pick.Offered[index];
                string line = _renderer.Render(pick.Npc, pick.Voice, entry, DaysAgo(entry, sourceDay));

                // Novelty (intents.md, planned change 1): never repeat a line this NPC said.
                if (AlreadySaid(pick.RecentLines, line))
                {
                    excluded.Add(index);
                    continue; // the one fall
                }

                // One topic per subject (planned change 2): the higher-hearts speaker keeps it.
                string key = PairKey(entry);
                if (owners.TryGetValue(key, out Owner? owner))
                {
                    if (pick.Hearts <= owner.Hearts)
                    {
                        excluded.Add(index);
                        continue; // the one fall: someone closer already says it
                    }

                    // Takeover: the old owner falls to its next option once, in place, or drops.
                    IntentCandidate? fallback = owner.Repick(_renderer, sourceDay, random);
                    int position = ordered.IndexOf(owner.Candidate);
                    if (fallback is null)
                        ordered.RemoveAt(position);
                    else
                    {
                        ordered[position] = fallback;
                        owners[PairKey(fallback.Source)] = owner.With(fallback);
                    }
                    owners.Remove(key);
                }

                string reason = $"cited \"{pick.Options[index]}\" (sampled p={Format(pick.ProbabilityAt(index))})";
                var candidate = new IntentCandidate(pick.Npc, line, entry, reason, pick.BestNews);
                ordered.Add(candidate);
                owners[key] = new Owner(candidate, pick, index, pick.Hearts);
                break;
            }
        }

        return new IntentPlan(ordered);
    }

    private static string PairKey(DiaryEntry entry)
        => $"{entry.Kind}|{entry.Subject}|{entry.AbsoluteTick}"; // the EVENT, not the kind:
        // "Saw Player" is shared by every NPC, but only entries from the same moment are the
        // same event (both NPCs saw the player's gift to Haley together).

    /// <summary>The planner's speak question. Public so the spread panel's template-mapper test
    /// can pin the EXACT wording by calling this instead of repeating the string.</summary>
    public static string SpeakProposition(string npc) => $"does {npc} have news for the player?";

    /// <summary>State for the model: the NPC card (or the legacy voice anchor when the snapshot
    /// carries no card) as the highest-priority section, then the offered entries as plain-words
    /// news sentences (news first); cut to the token budget by <see cref="DecisionState"/>.</summary>
    private static string BuildContext(string npc, IReadOnlyList<(DiaryEntry Entry, double Score)> offered,
        string voice, string? card, int? sourceDay)
    {
        var state = new DecisionState();
        state.Add(100, card ?? $"npc: {npc}\nvoice: {voice}");
        state.Add(50, "news:\n" + string.Join("\n", offered.Select(x => "- " + NewsPhrasing.Sentence(npc, x.Entry, DaysAgo(x.Entry, sourceDay)))));
        return state.Build();
    }

    /// <summary>The pick weights: model probabilities modulated by the news scores. With usable
    /// probabilities each weight is p * news (a zero probability is a veto); with a missing or
    /// degenerate answer the news scores alone decide. The legacy no-news path has score 0, so
    /// its weights are exactly the probabilities (uniform on a missing answer).</summary>
    private static double[] BlendedWeights(IReadOnlyList<double> probabilities, IReadOnlyList<(DiaryEntry Entry, double Score)> offered)
    {
        var weights = new double[offered.Count];
        // "usable" means at least one probability is a real number: a non-empty list of all zeros
        // or NaNs is a degenerate answer and falls back to the pure news weights (a degenerate
        // entry inside a usable answer gets weight 0 = vetoed).
        bool usable = probabilities is not null && probabilities.Any(IsUsable);
        for (int i = 0; i < offered.Count; i++)
        {
            double p = usable && i < probabilities.Count && IsUsable(probabilities[i])
                ? probabilities[i]
                : 0.0;
            double news = offered[i].Score;
            weights[i] = usable ? p * (news > 0.0 ? news : 1.0) : (news > 0.0 ? news : 1.0);
        }
        return weights;
    }

    private static int DaysAgo(DiaryEntry entry, int? sourceDay)
        => sourceDay is { } source ? source + 1 - GameClock.DayIndex(entry.AbsoluteTick) : 1;

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

    private bool Skipped(string? kind)
        => kind is not null && _options.SkipKinds is { } skip
           && skip.Any(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase));

    private static bool IsPlayerSubject(string? subject)
        => string.Equals(subject, MemoryStore.PlayerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>A short option string for one diary entry, e.g. "Saw Player at Pierre's General Store"
    /// (a "Saw" detail is a place) or "IgnoredBy Player (Emote)" (any other detail is not). This is
    /// the dedupe key and the fallback label; the model-facing phrasing is NewsPhrasing.Sentence.</summary>
    private static string Summarize(DiaryEntry entry)
        => NewsPhrasing.Summarize(entry);

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

    /// <summary>One speaking NPC: its options, pick weights and novelty/collision bookkeeping.
    /// Sampling happens in ranked order during pass 2 (the one fall is a fresh draw).</summary>
    private sealed class Pick
    {
        public Pick(string npc, string voice, List<(DiaryEntry Entry, double Score)> offered,
            List<string> options, double speak, double bestNews, double[] weights,
            IReadOnlyList<string> recentLines, double hearts)
        {
            Npc = npc;
            Voice = voice;
            Offered = offered;
            Options = options;
            Speak = speak;
            BestNews = bestNews;
            Weights = weights;
            RecentLines = recentLines;
            Hearts = hearts;
        }

        public string Npc { get; }
        public string Voice { get; }
        public List<(DiaryEntry Entry, double Score)> Offered { get; }
        public List<string> Options { get; }
        public double Speak { get; }
        public double BestNews { get; }
        public double[] Weights { get; }
        public IReadOnlyList<string> RecentLines { get; }
        public double Hearts { get; }

        public double ProbabilityAt(int index)
            => index < Weights.Length ? Weights[index] : 0.0;

        /// <summary>Sample one option from the weights, minus <paramref name="excluded"/>; null
        /// when nothing usable is left.</summary>
        public int? Sample(Random random, IReadOnlySet<int> excluded)
        {
            var weights = new double[Weights.Length];
            bool any = false;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = excluded.Contains(i) ? 0.0 : Weights[i];
                any |= weights[i] > 0.0;
            }
            if (!any)
                return null;
            int index = SampleIndex(weights, weights.Length, random);
            return index >= 0 ? index : null;
        }
    }

    /// <summary>Who currently holds a (kind, subject) pair in the plan, so a higher-hearts NPC
    /// can take it over and the old owner falls once.</summary>
    private sealed class Owner
    {
        public Owner(IntentCandidate candidate, Pick pick, int citedIndex, double hearts)
        {
            Candidate = candidate;
            Pick = pick;
            CitedIndex = citedIndex;
            Hearts = hearts;
        }

        public IntentCandidate Candidate { get; }
        public Pick Pick { get; }
        public int CitedIndex { get; }
        public double Hearts { get; }

        /// <summary>The owner's one fall: re-sample once without the cited option, novelty-checked
        /// (no collision check: a takeover chain is rare and one hop is the spec). Null = drop.</summary>
        public IntentCandidate? Repick(ILineRenderer renderer, int? sourceDay, Random random)
        {
            int? index = Pick.Sample(random, new HashSet<int> { CitedIndex });
            if (index is not { } i)
                return null;

            (DiaryEntry entry, double _) = Pick.Offered[i];
            string line = renderer.Render(Pick.Npc, Pick.Voice, entry, DaysAgo(entry, sourceDay));
            if (AlreadySaid(Pick.RecentLines, line))
                return null;

            string reason = $"cited \"{Pick.Options[i]}\" (sampled p={Format(Pick.ProbabilityAt(i))})";
            return new IntentCandidate(Pick.Npc, line, entry, reason, Pick.BestNews);
        }

        public Owner With(IntentCandidate candidate)
            => new(candidate, Pick, CitedIndex, Hearts);
    }
}
