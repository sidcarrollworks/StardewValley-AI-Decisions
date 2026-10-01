using NpcInitiation;
using NpcIntents;
using NpcMemory;
using NpcSchedules;
using NpcTemperament;

namespace NpcMinds;

/// <summary>
/// What the mod hands the builder each tick. Everything is memory or the ladder's last finished
/// state, never a live position: the ladder inputs already carry each NPC's own ledger view and
/// Find lead (built by the mod for the ladder), so the viewer shows exactly what the ladder saw.
/// </summary>
public sealed record MindsInputs(
    long Seq,
    int Now,                                        // absolute tick
    string Backend,
    bool ModelUp,
    MindsStats Stats,
    string PlanState,
    string? LadderJson,                             // BackgroundLadder.LatestJson
    IReadOnlyList<InitiationInput> LadderInputs,    // this tick's ladder inputs (views, leads, hearts)
    IReadOnlyCollection<string> IntentsToday,
    IReadOnlyList<IntentCandidate> PlanToday,
    IReadOnlyList<FeedItem> Feed,
    Func<string, NewsContext?>? NewsFor = null,     // null: no tonight's-news preview
    Func<string, TemperamentView?>? TemperamentFor = null); // null: no temperament shown

/// <summary>
/// Builds a <see cref="MindsSnapshot"/> on the game thread. Pure and read-only: it only reads
/// <see cref="MemoryStore"/> (diaries) and the given inputs, and changes nothing, so building a
/// snapshot can never alter what an NPC knows or decides (tests pin this).
/// </summary>
public sealed class MindsSnapshotBuilder
{
    public const int DiaryLines = 8;
    public const int NewsPicks = 3;

    private readonly Newsworthiness _news;
    private readonly InitiationOptions _ladderOptions;
    private readonly IntentPlannerOptions _planOptions;

    public MindsSnapshotBuilder(Newsworthiness? news = null, InitiationOptions? ladderOptions = null,
        IntentPlannerOptions? planOptions = null)
    {
        _news = news ?? new Newsworthiness();
        _ladderOptions = ladderOptions ?? new InitiationOptions();
        _planOptions = planOptions ?? new IntentPlannerOptions();
    }

    public MindsSnapshot Build(MemoryStore memory, MindsInputs inputs)
    {
        GameTime time = GameClock.FromAbsoluteTick(inputs.Now);
        int today = GameClock.DayIndex(inputs.Now);

        var ladder = InitiationLadder.ReadStates(inputs.LadderJson)
            .ToDictionary(s => s.Npc, StringComparer.OrdinalIgnoreCase);
        var ladderInputs = new Dictionary<string, InitiationInput>(StringComparer.OrdinalIgnoreCase);
        foreach (InitiationInput input in inputs.LadderInputs ?? Array.Empty<InitiationInput>())
            if (input is not null)
                ladderInputs[input.Npc] = input;
        var planned = new Dictionary<string, IntentCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (IntentCandidate c in inputs.PlanToday ?? Array.Empty<IntentCandidate>())
            planned.TryAdd(c.Npc, c);
        var intents = new HashSet<string>(inputs.IntentsToday ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        var npcs = new List<NpcMind>();
        foreach (string npc in memory.Diaries.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            Diary diary = memory.Diaries[npc];
            ladder.TryGetValue(npc, out LadderNpcState? state);
            ladderInputs.TryGetValue(npc, out InitiationInput? input);
            planned.TryGetValue(npc, out IntentCandidate? line);

            int rung = state?.Rung ?? 0;
            double[] thresholds = _ladderOptions.StepThresholds;
            npcs.Add(new NpcMind(
                npc,
                input?.Hearts ?? 0,
                state?.Urge ?? 0.0,
                rung,
                StepName(rung),
                rung >= 0 && rung < thresholds.Length ? thresholds[rung] : null,
                state is not null && state.Day == today ? state.AttemptsToday : 0,
                state?.OpenStep?.ToString(),
                state?.OpenTick is { } open ? Math.Max(0, inputs.Now - open) : null,
                intents.Contains(npc),
                line?.Line,
                LastSeen(input?.PlayerView),
                LeadOf(input?.Lead),
                TonightsNews(npc, diary, today, inputs.NewsFor),
                diary.Recent(DiaryLines).Select(e => Line(npc, e, inputs.Now)).ToList(),
                diary.Entries.Count,
                inputs.TemperamentFor?.Invoke(npc)));
        }

        return new MindsSnapshot(
            inputs.Seq,
            $"{GameClock.SeasonName(time.SeasonIndex)} {time.DayOfMonth}, year {time.Year}",
            Clock(time.Tick),
            inputs.Now,
            inputs.Backend,
            inputs.ModelUp,
            inputs.Stats,
            inputs.PlanState,
            _ladderOptions.StepThresholds.ToArray(),
            Enum.GetNames(typeof(InitiationStep)),
            npcs,
            (inputs.PlanToday ?? Array.Empty<IntentCandidate>())
                .Select(c => new PlannedLine(c.Npc, c.Line, c.Reason, c.News)).ToList(),
            inputs.Feed ?? Array.Empty<FeedItem>());
    }

    private static string StepName(int rung)
        => Enum.IsDefined(typeof(InitiationStep), rung) ? ((InitiationStep)rung).ToString() : $"rung {rung}";

    /// <summary>Today's entries the planner would consider tonight, scored the way it scores them
    /// (skipped kinds out, one per summary, at least MinNews), best first. A preview only: the
    /// real plan also asks the model and samples.</summary>
    private IReadOnlyList<NewsPick> TonightsNews(string npc, Diary diary, int today, Func<string, NewsContext?>? newsFor)
    {
        if (newsFor?.Invoke(npc) is not { } context)
            return Array.Empty<NewsPick>();

        var newest = new Dictionary<string, DiaryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (DiaryEntry e in diary.Entries)
        {
            if (e is null || GameClock.DayIndex(e.AbsoluteTick) != today)
                continue;
            if (_planOptions.SkipKinds.Contains(e.Kind, StringComparer.OrdinalIgnoreCase))
                continue;
            newest[NewsPhrasing.Summarize(e)] = e; // later entries win, like the planner's dedupe
        }

        return newest.Values
            .Select(e => (Entry: e, Score: _news.Score(e, context)))
            .Where(x => x.Score >= _news.Options.MinNews)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.AbsoluteTick)
            .Take(NewsPicks)
            .Select(x => new NewsPick(Describe(x.Entry), x.Score))
            .ToList();
    }

    /// <summary>How far from the town's middle (0.5) a trait must be to make the summary.</summary>
    public const double LeaningThreshold = 0.1;

    // Plain words for a high and a low value of each trait (docs/spec/temperament.md).
    private static readonly IReadOnlyDictionary<string, (string High, string Low)> TraitWords =
        new Dictionary<string, (string, string)>
        {
            ["warmth"] = ("warm", "cool"),
            ["sensitivity"] = ("sensitive", "thick-skinned"),
            ["forgiveness"] = ("forgiving", "holds grudges"),
            ["chattiness"] = ("chatty", "quiet"),
            ["curiosity"] = ("curious", "incurious"),
            ["boldness"] = ("bold", "timid"),
            ["anger"] = ("quick to anger", "slow to anger"),
            ["disgust"] = ("easily disgusted", "hard to disgust"),
            ["fear"] = ("fearful", "fearless"),
            ["happiness"] = ("cheerful", "glum"),
            ["sadness"] = ("prone to sadness", "rarely sad"),
            ["surprise"] = ("easily surprised", "unflappable"),
        };

    /// <summary>The display form of a seed temperament: every trait in the table's order, and a
    /// summary of the (at most three) strongest leanings, strongest first, ties by trait order.</summary>
    public static TemperamentView TemperamentOf(Temperament temperament, bool seeded, string? gameTraits)
    {
        Temperament t = temperament ?? Temperament.Neutral;
        var traits = Temperament.BehaviourTraits.Select(n => new TraitValue(n, t.Get(n))).ToList();
        var emotions = Temperament.EmotionTraits.Select(n => new TraitValue(n, t.Get(n))).ToList();
        string summary = string.Join(", ", traits.Concat(emotions)
            .Select((v, i) => (v, i))
            .Where(x => Math.Abs(x.v.Value - 0.5) >= LeaningThreshold - 1e-9)
            .OrderByDescending(x => Math.Abs(x.v.Value - 0.5))
            .ThenBy(x => x.i)
            .Take(3)
            .Select(x => x.v.Value > 0.5 ? TraitWords[x.v.Name].High : TraitWords[x.v.Name].Low));
        if (summary.Length == 0)
            summary = seeded ? "even-tempered" : "no seed (town average)";
        return new TemperamentView(traits, emotions, summary, gameTraits, seeded);
    }

    public static LastSeenView? LastSeen(LedgerView? view)
    {
        if (view is null)
            return null;
        string place = view.Place is null ? "" : $" at {PlaceNames.Display(view.Place)}";
        string how = view.HopCount switch
        {
            0 => "saw you",
            1 => $"{view.ToldBy ?? "someone"} told them you were",
            _ => $"heard from {view.ToldBy ?? "someone"} (second-hand) you were",
        };
        string summary = view.Detail switch
        {
            LedgerDetail.Gone => "hasn't seen you today",
            LedgerDetail.EarlierToday => view.HopCount == 0 ? $"saw you earlier today, {Ago(view.AgeTicks)}" : $"heard you were around earlier today, {Ago(view.AgeTicks)}",
            _ when view.HopCount == 0 && view.AgeTicks == 0 => $"can see you{place} right now",
            _ => $"{how}{place}, {Ago(view.AgeTicks)}",
        };
        return new LastSeenView(view.Detail.ToString(), view.Place, view.AgeTicks, view.HopCount, view.ToldBy, summary);
    }

    public static LeadView? LeadOf(Whereabouts? lead)
    {
        if (lead is null)
            return null;
        string place = lead.Place is null ? "" : PlaceNames.Display(lead.Place);
        string summary = lead.Source switch
        {
            WhereaboutsSource.SeenNow => $"you're right here ({place})",
            WhereaboutsSource.SeenToday => $"{place}: saw you there {Ago(lead.AgeTicks)}",
            WhereaboutsSource.Told when lead.HopCount > 1 => $"{place}: {lead.ToldBy} heard you were there {Ago(lead.AgeTicks)}",
            WhereaboutsSource.Told => $"{place}: {lead.ToldBy} saw you there {Ago(lead.AgeTicks)}",
            WhereaboutsSource.Habit => $"{place}: you're usually there at this hour ({lead.HabitShare * 100:0}%, evidence {lead.Evidence:0})",
            _ => "no idea where you are",
        };
        return new LeadView(lead.Source.ToString(), lead.Place, summary);
    }

    /// <summary>A diary entry in plain words from the NPC's side, with when it happened.</summary>
    public static DiaryLine Line(string npc, DiaryEntry entry, int now)
    {
        GameTime at = GameClock.FromAbsoluteTick(entry.AbsoluteTick);
        int days = GameClock.DayIndex(now) - GameClock.DayIndex(entry.AbsoluteTick);
        string day = days switch
        {
            <= 0 => "today",
            1 => "yesterday",
            _ => $"{days} days ago",
        };
        return new DiaryLine(entry.AbsoluteTick, $"{day} {Clock(at.Tick)}", entry.Kind, Describe(entry));
    }

    public static string Describe(DiaryEntry entry)
    {
        string who = string.Equals(entry.Subject, MemoryStore.PlayerName, StringComparison.OrdinalIgnoreCase) ? "you" : entry.Subject;
        IReadOnlyDictionary<string, string> detail = DiaryDetail.Parse(entry.Detail);
        return (entry.Kind ?? "") switch
        {
            "Saw" => $"saw {who} at {PlaceNames.Display(entry.Detail)}",
            "Talked" => $"talked with {who}",
            "TriedToReach" => $"tried to get {Possessive(who)} attention ({entry.Detail})",
            "IgnoredBy" => $"was ignored by {who} ({entry.Detail})",
            "PassedBy" => $"{who} walked right past",
            "BirthdayForgotten" => $"birthday, and {who} forgot",
            "GiftReceived" => $"got a {(detail.TryGetValue("name", out string? n) ? n : "gift")} from {who}" +
                              (detail.TryGetValue("taste", out string? t) ? $" ({t.ToLowerInvariant()})" : ""),
            "SawGift" => $"saw you give {entry.Subject} a gift",
            "QuestHelped" => $"{who} helped with a request",
            "Festival" => detail.TryGetValue("with", out string? w) && w == "1" ? $"spent the festival with {who}" : "was at the festival",
            "MissedFestival" => $"{who} skipped the festival",
            _ => NewsPhrasing.Summarize(entry),
        };
    }

    private static string Possessive(string who) => who == "you" ? "your" : who + "'s";

    /// <summary>Tick of day as a 12-hour clock, like the game's: 0 -> "6:00 am", 119 -> "1:50 am".</summary>
    public static string Clock(int tickOfDay)
    {
        int hhmm = TimeUtils.TimeOfDay(Math.Clamp(tickOfDay, 0, GameClock.TicksPerDay - 1));
        int hour = hhmm / 100 % 24;
        int minute = hhmm % 100;
        string half = hour < 12 ? "am" : "pm";
        int h12 = hour % 12 == 0 ? 12 : hour % 12;
        return $"{h12}:{minute:00} {half}";
    }

    /// <summary>A tick age in words, the same wording as the SMAPI log.</summary>
    public static string Ago(int ticks) => ticks switch
    {
        <= 0 => "just now",
        < 6 => $"{ticks * 10} minutes ago",
        < 12 => "an hour ago",
        _ => $"{ticks / 6} hours ago",
    };
}
