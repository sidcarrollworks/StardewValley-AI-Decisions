using System.Globalization;
using System.Text.Json;
using NpcDecision;
using NpcMemory;
using NpcSchedules;

namespace NpcInitiation;

/// <summary>
/// The initiation ladder (brief, design decision 3), in shadow mode: each NPC carries an urge to
/// get the player's attention, and when it is high enough the ladder picks the mildest fitting
/// step and returns what it WOULD do. It never touches the game. Its only knowledge of the player
/// is each NPC's own ledger view. Deterministic: same inputs and seed give the same events.
/// </summary>
public sealed class InitiationLadder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IDecisionClient _decision;
    private readonly int _seed;
    private readonly InitiationOptions _options;

    private readonly Dictionary<string, NpcState> _npcs = new(StringComparer.OrdinalIgnoreCase);

    // Blocked events log at most once per (npc, step) per day: the last day each pair was
    // logged. Bounded by NPCs x steps, so it is never cleared. Keys are "{npc}|{step}".
    private readonly Dictionary<string, int> _blockedLastLoggedDay = new(StringComparer.OrdinalIgnoreCase);

    // Global counters (across all NPCs).
    private int _day = int.MinValue;     // day index the daily counters belong to
    private int _attemptsToday;
    private int _queuedLinesToday;
    private int _mailToday;
    private int _week = int.MinValue;    // week index the forced counter belongs to
    private int _forcedThisWeek;

    public InitiationLadder(IDecisionClient decision, int seed, InitiationOptions? options = null)
    {
        _decision = decision ?? throw new ArgumentNullException(nameof(decision));
        _seed = seed;
        _options = options ?? new InitiationOptions();
    }

    /// <summary>Current urge of an NPC in [0,1]; 0 for an unknown NPC.</summary>
    public double Urge(string npc) => _npcs.TryGetValue(npc, out var s) ? s.Urge : 0.0;

    /// <summary>Every known NPC's current urge, as a new dictionary (safe to hand to another thread).</summary>
    public IReadOnlyDictionary<string, double> Urges()
        => _npcs.ToDictionary(kv => kv.Key, kv => kv.Value.Urge, StringComparer.OrdinalIgnoreCase);

    /// <summary>Current rung of an NPC (the mildest step it may use next); 0 for an unknown NPC.</summary>
    public int Rung(string npc) => _npcs.TryGetValue(npc, out var s) ? s.Rung : 0;

    /// <summary>
    /// Run one ten-minute tick. Inputs may come in any order; NPCs are processed in
    /// OrdinalIgnoreCase name order. Returns the events of this tick in that order.
    /// </summary>
    public IReadOnlyList<InitiationEvent> Tick(
        int absoluteTick, IReadOnlyList<InitiationInput> inputs, Func<string, Diary> diaryFor)
    {
        int day = DayIndex(absoluteTick);
        RollGlobal(day);

        var events = new List<InitiationEvent>();
        foreach (var input in inputs.OrderBy(i => i.Npc, StringComparer.OrdinalIgnoreCase))
        {
            if (!_npcs.TryGetValue(input.Npc, out var state))
            {
                state = new NpcState { Day = day };
                _npcs[input.Npc] = state;
            }

            // (1) Resolve an open attempt whose response window has passed. This runs before the
            // day rollover, so an attempt left open overnight is settled on the day it was made and
            // the new day still starts from rung 0.
            if (state.OpenStep is { } openStep && state.OpenTick is { } openTick
                && absoluteTick >= ResolveAt(openStep, openTick))
            {
                int resolveAt = ResolveAt(openStep, openTick);
                // Stamp it when the window closed; a window that closed with the day belongs to that day.
                int stamp = Math.Min(absoluteTick, resolveAt);
                if (stamp == resolveAt && resolveAt % GameClock.TicksPerDay == 0)
                    stamp = resolveAt - 1;

                double before = state.Urge;
                state.OpenStep = null;
                state.OpenTick = null;
                if (openStep == InitiationStep.QueuedLine)
                {
                    // The player never came to talk, so never heard it: no penalty, no escalation.
                    events.Add(new InitiationEvent(stamp, input.Npc, "Expired", openStep, before, before,
                        "the player did not talk to them before the day ended"));
                }
                else
                {
                    state.Urge = Math.Max(0.0, state.Urge - _options.IgnorePenalty);
                    state.Rung = (int)openStep + 1; // escalate one rung past what was ignored
                    events.Add(new InitiationEvent(stamp, input.Npc, "Ignored", openStep, before, state.Urge,
                        openStep == InitiationStep.Mail
                            ? "no visit by the end of the day after the letter"
                            : $"no response within {_options.ResponseWindowTicks} ticks"));
                    // Shadow mode: the player never saw the attempt, so no IgnoredBy diary line
                    // (the urge penalty and escalation above still apply). RecordIgnoredBy turns
                    // on per rung in step 6, when the attempts really appear.
                    if (_options.RecordIgnoredBy)
                        diaryFor(input.Npc).Append(new DiaryEntry(stamp, "Player", "IgnoredBy", openStep.ToString()));
                }
            }

            // (2) Day rollover: urge fades overnight, rung and daily count reset.
            if (state.Day != day)
            {
                state.Urge = Clamp01(state.Urge * _options.OvernightFactor);
                state.Rung = 0;
                state.AttemptsToday = 0;
                state.Day = day;
            }

            // Never seen the player: no growth, no attempt.
            if (input.PlayerView is null)
                continue;

            // (3) Grow urge (+ intent boost once per day).
            int hearts = Math.Clamp(input.Hearts, 0, 14);
            double urge = state.Urge + _options.BaseGainPerTick + hearts * _options.HeartsGainPerTick;
            if (input.HasPendingIntent && state.IntentBoostDay != day)
            {
                urge += _options.IntentBoost;
                state.IntentBoostDay = day;
            }
            state.Urge = Clamp01(urge);

            // (4) Maybe attempt.
            var attempt = MaybeAttempt(absoluteTick, input, state, events);
            if (attempt is not null)
            {
                events.Add(attempt);
                diaryFor(input.Npc).Append(new DiaryEntry(absoluteTick, "Player", "TriedToReach", attempt.Step.ToString()));
            }
        }
        return events;
    }

    /// <summary>
    /// The player talked to this NPC. The NPC got the attention it wanted, so its urge is relieved,
    /// its rung resets, and it waits out the cooldown before trying anything. If an attempt was
    /// open, it is resolved as "Responded" and that event is returned; otherwise null.
    /// </summary>
    public InitiationEvent? NoteResponded(string npc, int absoluteTick)
    {
        if (!_npcs.TryGetValue(npc, out var state))
            return null;

        double before = state.Urge;
        state.Urge = Clamp01(state.Urge * _options.RespondRelief);
        state.Rung = 0;
        state.LastContactTick = absoluteTick;
        if (state.OpenStep is not { } step)
            return null;

        state.OpenStep = null;
        state.OpenTick = null;
        return new InitiationEvent(absoluteTick, npc, "Responded", step, before, state.Urge, "player responded");
    }

    /// <summary>The first tick at which an open attempt counts as unanswered. Active steps get the
    /// response window, cut off when the day ends; a queued line waits for the rest of the day; a
    /// letter arrives the next morning and waits until the end of that day.</summary>
    private int ResolveAt(InitiationStep step, int openTick)
    {
        int nextDay = GameClock.DayStartTick(DayIndex(openTick) + 1);
        return step switch
        {
            InitiationStep.QueuedLine => nextDay,
            InitiationStep.Mail => GameClock.DayStartTick(DayIndex(openTick) + 2),
            _ => Math.Min(openTick + _options.ResponseWindowTicks, nextDay),
        };
    }

    // ---- Attempt decision --------------------------------------------------------------------

    private InitiationEvent? MaybeAttempt(
        int absoluteTick, InitiationInput input, NpcState state, List<InitiationEvent> events)
    {
        // Caps and cooldown first, so the decision client is only asked when an attempt could happen.
        if (state.OpenStep is not null)
            return null;
        if (state.AttemptsToday >= _options.MaxAttemptsPerNpcPerDay)
            return null;
        if (_attemptsToday >= _options.MaxAttemptsPerDay)
            return null;
        if (state.LastAttemptTick is { } last && absoluteTick - last < _options.CooldownTicks)
            return null;
        if (state.LastContactTick is { } contact && absoluteTick - contact < _options.CooldownTicks)
            return null;

        var step = Candidate(absoluteTick, input, state, events);
        if (step is null)
            return null;

        var view = input.PlayerView!;
        // An Approach without the player in sight means going to where the NPC believes they are.
        Whereabouts? lead = step == InitiationStep.Approach && !IsNear(view) ? input.Lead : null;
        // The same question the draw just turned down waits a while before it is asked again.
        string askKey = step.Value + "|" + lead?.Place;
        if (state.LastAskedKey == askKey && state.LastAskedTick is { } asked && absoluteTick - asked < _options.AskAgainAfterTicks)
            return null;
        string context = string.Format(CultureInfo.InvariantCulture,
            "urge={0:0.00}; hearts={1}; step={2}; player last seen: {3}, {4} ticks ago, {5}{6}",
            state.Urge, input.Hearts, step.Value, view.Detail, view.AgeTicks,
            view.HopCount == 0 ? "first-hand" : $"hearsay ({view.HopCount} hops)",
            lead is null ? "" : $"; would look for them at {lead.Place} ({lead.Source})");
        double p = _decision.YesNo(context, AttentionProposition(input.Npc, step.Value.ToString()));
        if (double.IsNaN(p) || !(Uniform(input.Npc, absoluteTick) < p))
        {
            state.LastAskedKey = askKey;
            state.LastAskedTick = absoluteTick;
            return null;
        }

        state.OpenStep = step.Value;
        state.OpenTick = absoluteTick;
        state.LastAttemptTick = absoluteTick;
        state.AttemptsToday++;
        _attemptsToday++;
        if (step.Value == InitiationStep.ForcedDialogue)
            _forcedThisWeek++;
        else if (step.Value == InitiationStep.QueuedLine)
            _queuedLinesToday++;
        else if (step.Value == InitiationStep.Mail)
            _mailToday++;

        return new InitiationEvent(absoluteTick, input.Npc, "Attempt", step.Value, state.Urge, state.Urge,
            string.Format(CultureInfo.InvariantCulture, "urge {0:0.00} >= {1:0.00} at rung {2}; p={3:0.00}",
                state.Urge, _options.StepThresholds[(int)step.Value], state.Rung, p),
            lead,
            Threshold: _options.StepThresholds[(int)step.Value],
            ModelP: p);
    }

    /// <summary>The ladder's attention question for one step ("Emote", "Bubble", "Approach"...).
    /// Public so the spread panel's template-mapper test can pin the EXACT wording by calling
    /// this instead of repeating the string.</summary>
    public static string AttentionProposition(string npc, string step)
        => $"should {npc} try to get the player's attention with {step} now?";

    /// <summary>
    /// The mildest step at or above the NPC's rung that is available and whose threshold the urge
    /// meets. A step the urge cleared but that its gate or a cap passed over is a near-attempt and
    /// is recorded as a "Blocked" event (at most one per NPC per step per day), so the playtest
    /// log can see what the ladder weighed and set aside. Steps below the urge's bar stay silent.
    /// </summary>
    private InitiationStep? Candidate(
        int absoluteTick, InitiationInput input, NpcState state, List<InitiationEvent> events)
    {
        var thresholds = _options.StepThresholds;
        for (int rung = Math.Max(0, state.Rung); rung <= (int)InitiationStep.ForcedDialogue; rung++)
        {
            var step = (InitiationStep)rung;
            if (rung >= thresholds.Length || state.Urge < thresholds[rung])
                continue;
            double threshold = thresholds[rung];
            if (!Available(step, input))
            {
                NoteBlocked(absoluteTick, input, state, step, threshold, events,
                    "unavailable: " + UnavailableReason(step, input));
                continue;
            }
            if (step == InitiationStep.ForcedDialogue && _forcedThisWeek >= _options.MaxForcedPerWeek)
            {
                NoteBlocked(absoluteTick, input, state, step, threshold, events,
                    string.Format(CultureInfo.InvariantCulture, "cap: forced dialogue ({0} of {1} this week)",
                        _forcedThisWeek, _options.MaxForcedPerWeek));
                continue;
            }
            if (step == InitiationStep.QueuedLine && _queuedLinesToday >= _options.MaxQueuedLinesPerDay)
            {
                NoteBlocked(absoluteTick, input, state, step, threshold, events,
                    string.Format(CultureInfo.InvariantCulture, "cap: queued line ({0} of {1} today)",
                        _queuedLinesToday, _options.MaxQueuedLinesPerDay));
                continue;
            }
            if (step == InitiationStep.Mail && _mailToday >= _options.MaxMailPerDay)
            {
                NoteBlocked(absoluteTick, input, state, step, threshold, events,
                    string.Format(CultureInfo.InvariantCulture, "cap: mail ({0} of {1} today)",
                        _mailToday, _options.MaxMailPerDay));
                continue;
            }
            return step;
        }
        return null;
    }

    /// <summary>Record a near-attempt the ladder passed over, at most once per (npc, step) per
    /// day: a blocked step logs once and then stays quiet until tomorrow, not every tick.</summary>
    private void NoteBlocked(
        int absoluteTick, InitiationInput input, NpcState state, InitiationStep step, double threshold,
        List<InitiationEvent> events, string reason)
    {
        int day = DayIndex(absoluteTick);
        string key = $"{input.Npc}|{step}";
        if (_blockedLastLoggedDay.TryGetValue(key, out int lastDay) && lastDay == day)
            return;
        _blockedLastLoggedDay[key] = day;
        events.Add(new InitiationEvent(absoluteTick, input.Npc, "Blocked", step, state.Urge, state.Urge,
            reason, Threshold: threshold));
    }

    /// <summary>A short reason why <see cref="Available"/> said no, for a Blocked event's text.
    /// Mail is the one gate with two clauses: say which one failed.</summary>
    private static string UnavailableReason(InitiationStep step, InitiationInput input) => step switch
    {
        InitiationStep.Emote or InitiationStep.Bubble or InitiationStep.ForcedDialogue
            => "player out of sight",
        InitiationStep.Approach => "player out of sight and no lead",
        InitiationStep.QueuedLine => "player not seen today",
        InitiationStep.Mail => IsSeenToday(input.PlayerView) ? "player seen today" : "hearts below 2",
        _ => "not available",
    };

    private static bool Available(InitiationStep step, InitiationInput input) => step switch
    {
        InitiationStep.Emote or InitiationStep.Bubble or InitiationStep.ForcedDialogue
            => IsNear(input.PlayerView),
        // Walk up to the player, or go and look for them where the NPC believes they are.
        InitiationStep.Approach => IsNear(input.PlayerView) || HasLead(input.Lead),
        InitiationStep.QueuedLine => IsSeenToday(input.PlayerView),
        InitiationStep.Mail => !IsSeenToday(input.PlayerView) && input.Hearts >= 2,
        _ => false,
    };

    /// <summary>The NPC itself saw the player at a named spot this very tick.</summary>
    private static bool IsNear(LedgerView? view)
        => view is not null && view.HopCount == 0 && view.AgeTicks == 0 && view.Detail == LedgerDetail.NamedSpot;

    /// <summary>The NPC has somewhere to look: its own sighting from today, a neighbour's tip, or a
    /// learned habit for this hour. An "earlier today" with no place is not a lead.</summary>
    private static bool HasLead(Whereabouts? lead)
        => lead is { HasPlace: true, Source: WhereaboutsSource.SeenToday or WhereaboutsSource.Told or WhereaboutsSource.Habit };

    /// <summary>The NPC itself saw the player today (any detail short of Gone). Hearsay never counts.</summary>
    private static bool IsSeenToday(LedgerView? view)
        => view is not null && view.HopCount == 0
           && (view.Detail is LedgerDetail.NamedSpot or LedgerDetail.Location
               or LedgerDetail.Region or LedgerDetail.EarlierToday);

    /// <summary>Deterministic uniform in [0,1) from (seed, npc, tick).</summary>
    private double Uniform(string npc, int absoluteTick)
    {
        uint h = unchecked((uint)Fnv1a.Seed(
            _seed.ToString(CultureInfo.InvariantCulture), npc, absoluteTick.ToString(CultureInfo.InvariantCulture)));
        return h / 4294967296.0;
    }

    // ---- Days --------------------------------------------------------------------------------

    private static int DayIndex(int absoluteTick) => GameClock.DayIndex(absoluteTick);

    private void RollGlobal(int day)
    {
        if (_day != day)
        {
            _day = day;
            _attemptsToday = 0;
            _queuedLinesToday = 0;
            _mailToday = 0;
        }
        int week = day / 7;
        if (_week != week)
        {
            _week = week;
            _forcedThisWeek = 0;
        }
    }

    private static double Clamp01(double v) => double.IsNaN(v) ? 0.0 : Math.Clamp(v, 0.0, 1.0);

    // ---- Persistence -------------------------------------------------------------------------

    /// <summary>Per-NPC and global state as JSON. The options are deliberately not persisted.</summary>
    public string ToJson()
    {
        var dto = new LadderDto
        {
            Day = _day,
            AttemptsToday = _attemptsToday,
            QueuedLinesToday = _queuedLinesToday,
            MailToday = _mailToday,
            Week = _week,
            ForcedThisWeek = _forcedThisWeek,
            Npcs = _npcs
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new NpcDto
                {
                    Npc = kv.Key,
                    Urge = kv.Value.Urge,
                    Rung = kv.Value.Rung,
                    Day = kv.Value.Day,
                    AttemptsToday = kv.Value.AttemptsToday,
                    LastAttemptTick = kv.Value.LastAttemptTick,
                    LastContactTick = kv.Value.LastContactTick,
                    OpenStep = kv.Value.OpenStep,
                    OpenTick = kv.Value.OpenTick,
                    IntentBoostDay = kv.Value.IntentBoostDay,
                })
                .ToList(),
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static InitiationLadder FromJson(
        string json, IDecisionClient decision, int seed, InitiationOptions? options = null)
    {
        var ladder = new InitiationLadder(decision, seed, options);
        var dto = JsonSerializer.Deserialize<LadderDto>(json, JsonOptions) ?? new LadderDto();
        ladder._day = dto.Day;
        ladder._attemptsToday = dto.AttemptsToday;
        ladder._queuedLinesToday = dto.QueuedLinesToday;
        ladder._mailToday = dto.MailToday;
        ladder._week = dto.Week;
        ladder._forcedThisWeek = dto.ForcedThisWeek;
        foreach (var n in dto.Npcs ?? new List<NpcDto>())
        {
            // Skip nameless states AND the literal "null" the game uses for a quest with no
            // target (playtest review: the saved ladder carried a "null" NPC at urge 0).
            if (string.IsNullOrEmpty(n.Npc) || string.Equals(n.Npc, "null", StringComparison.OrdinalIgnoreCase))
                continue;
            ladder._npcs[n.Npc] = new NpcState
            {
                Urge = Clamp01(n.Urge),
                Rung = n.Rung,
                Day = n.Day,
                AttemptsToday = n.AttemptsToday,
                LastAttemptTick = n.LastAttemptTick,
                LastContactTick = n.LastContactTick,
                OpenStep = n.OpenStep,
                OpenTick = n.OpenTick,
                IntentBoostDay = n.IntentBoostDay,
            };
        }
        return ladder;
    }

    /// <summary>
    /// Each NPC's saved ladder state, read back from <see cref="ToJson"/> output (for example
    /// <see cref="BackgroundLadder.LatestJson"/>), in name order. Read-only: the NPC Minds viewer
    /// uses it to show rungs and open attempts without touching the live ladder, which belongs to
    /// the worker. Malformed or empty JSON gives an empty list.
    /// </summary>
    public static IReadOnlyList<LadderNpcState> ReadStates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<LadderNpcState>();
        LadderDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<LadderDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Array.Empty<LadderNpcState>();
        }
        return (dto?.Npcs ?? new List<NpcDto>())
            .Where(n => !string.IsNullOrEmpty(n.Npc))
            .OrderBy(n => n.Npc, StringComparer.OrdinalIgnoreCase)
            .Select(n => new LadderNpcState(n.Npc, Clamp01(n.Urge), n.Rung, n.Day, n.AttemptsToday,
                n.LastAttemptTick, n.LastContactTick, n.OpenStep, n.OpenTick))
            .ToList();
    }

    private sealed class NpcState
    {
        public double Urge;
        public int Rung;
        public int Day;                  // day index this state's daily fields belong to
        public int AttemptsToday;
        public int? LastAttemptTick;
        public int? LastContactTick;     // last time the player talked to them
        public InitiationStep? OpenStep; // unresolved attempt, if any
        public int? OpenTick;
        public int? IntentBoostDay;      // day the intent boost was last applied
        public string? LastAskedKey;     // step and lead of the last question the draw turned down; not saved
        public int? LastAskedTick;
    }

    private sealed class LadderDto
    {
        public int Day { get; set; } = int.MinValue;
        public int AttemptsToday { get; set; }
        public int QueuedLinesToday { get; set; }
        public int MailToday { get; set; }
        public int Week { get; set; } = int.MinValue;
        public int ForcedThisWeek { get; set; }
        public List<NpcDto>? Npcs { get; set; }
    }

    private sealed class NpcDto
    {
        public string Npc { get; set; } = "";
        public double Urge { get; set; }
        public int Rung { get; set; }
        public int Day { get; set; }
        public int AttemptsToday { get; set; }
        public int? LastAttemptTick { get; set; }
        public int? LastContactTick { get; set; }
        public InitiationStep? OpenStep { get; set; }
        public int? OpenTick { get; set; }
        public int? IntentBoostDay { get; set; }
    }
}
