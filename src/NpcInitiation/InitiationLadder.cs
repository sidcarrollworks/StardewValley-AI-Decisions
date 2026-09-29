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

    // Global counters (across all NPCs).
    private int _day = int.MinValue;     // day index the daily counter belongs to
    private int _attemptsToday;
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

            // (1) Day rollover: urge fades overnight, rung and daily count reset.
            if (state.Day != day)
            {
                state.Urge = Clamp01(state.Urge * _options.OvernightFactor);
                state.Rung = 0;
                state.AttemptsToday = 0;
                state.Day = day;
            }

            // (2) Resolve an open attempt whose response window has passed.
            if (state.OpenStep is { } openStep && state.OpenTick is { } openTick
                && absoluteTick - openTick >= _options.ResponseWindowTicks)
            {
                double before = state.Urge;
                state.Urge = Math.Max(0.0, state.Urge - _options.IgnorePenalty);
                state.Rung = (int)openStep + 1; // escalate one rung past what was ignored
                state.OpenStep = null;
                state.OpenTick = null;
                events.Add(new InitiationEvent(absoluteTick, input.Npc, "Ignored", openStep, before, state.Urge,
                    $"no response within {_options.ResponseWindowTicks} ticks"));
                diaryFor(input.Npc).Append(new DiaryEntry(absoluteTick, "Player", "IgnoredBy", openStep.ToString()));
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
            var attempt = MaybeAttempt(absoluteTick, input, state);
            if (attempt is not null)
            {
                events.Add(attempt);
                diaryFor(input.Npc).Append(new DiaryEntry(absoluteTick, "Player", "TriedToReach", attempt.Step.ToString()));
            }
        }
        return events;
    }

    /// <summary>
    /// The player responded to this NPC (e.g. talked to them). Resolves the NPC's open attempt,
    /// if any, as "Responded" (urge relieved, rung reset) and returns that event; else null.
    /// </summary>
    public InitiationEvent? NoteResponded(string npc, int absoluteTick)
    {
        if (!_npcs.TryGetValue(npc, out var state) || state.OpenStep is not { } step)
            return null;

        double before = state.Urge;
        state.Urge = Clamp01(state.Urge * _options.RespondRelief);
        state.Rung = 0;
        state.OpenStep = null;
        state.OpenTick = null;
        return new InitiationEvent(absoluteTick, npc, "Responded", step, before, state.Urge, "player responded");
    }

    // ---- Attempt decision --------------------------------------------------------------------

    private InitiationEvent? MaybeAttempt(int absoluteTick, InitiationInput input, NpcState state)
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

        var step = Candidate(input, state);
        if (step is null)
            return null;

        var view = input.PlayerView!;
        string context = string.Format(CultureInfo.InvariantCulture,
            "urge={0:0.00}; hearts={1}; step={2}; player last seen: {3}, {4} ticks ago, {5}",
            state.Urge, input.Hearts, step.Value, view.Detail, view.AgeTicks,
            view.HopCount == 0 ? "first-hand" : $"hearsay ({view.HopCount} hops)");
        double p = _decision.YesNo(context, $"should {input.Npc} try to get the player's attention with {step.Value} now?");
        if (double.IsNaN(p) || !(Uniform(input.Npc, absoluteTick) < p))
            return null;

        state.OpenStep = step.Value;
        state.OpenTick = absoluteTick;
        state.LastAttemptTick = absoluteTick;
        state.AttemptsToday++;
        _attemptsToday++;
        if (step.Value == InitiationStep.ForcedDialogue)
            _forcedThisWeek++;

        return new InitiationEvent(absoluteTick, input.Npc, "Attempt", step.Value, state.Urge, state.Urge,
            string.Format(CultureInfo.InvariantCulture, "urge {0:0.00} >= {1:0.00} at rung {2}; p={3:0.00}",
                state.Urge, _options.StepThresholds[(int)step.Value], state.Rung, p));
    }

    /// <summary>The mildest step at or above the NPC's rung that is available and whose threshold the urge meets.</summary>
    private InitiationStep? Candidate(InitiationInput input, NpcState state)
    {
        var thresholds = _options.StepThresholds;
        for (int rung = Math.Max(0, state.Rung); rung <= (int)InitiationStep.ForcedDialogue; rung++)
        {
            var step = (InitiationStep)rung;
            if (rung >= thresholds.Length || state.Urge < thresholds[rung])
                continue;
            if (!Available(step, input))
                continue;
            if (step == InitiationStep.ForcedDialogue && _forcedThisWeek >= _options.MaxForcedPerWeek)
                continue;
            return step;
        }
        return null;
    }

    private static bool Available(InitiationStep step, InitiationInput input) => step switch
    {
        InitiationStep.Emote or InitiationStep.Bubble or InitiationStep.Approach or InitiationStep.ForcedDialogue
            => IsNear(input.PlayerView),
        InitiationStep.QueuedLine => IsSeenToday(input.PlayerView),
        InitiationStep.Mail => !IsSeenToday(input.PlayerView) && input.Hearts >= 2,
        _ => false,
    };

    /// <summary>The NPC itself saw the player at a named spot this very tick.</summary>
    private static bool IsNear(LedgerView? view)
        => view is not null && view.HopCount == 0 && view.AgeTicks == 0 && view.Detail == LedgerDetail.NamedSpot;

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

    private static int DayIndex(int absoluteTick) => absoluteTick / GameClock.TicksPerDay;

    private void RollGlobal(int day)
    {
        if (_day != day)
        {
            _day = day;
            _attemptsToday = 0;
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
        ladder._week = dto.Week;
        ladder._forcedThisWeek = dto.ForcedThisWeek;
        foreach (var n in dto.Npcs ?? new List<NpcDto>())
        {
            if (string.IsNullOrEmpty(n.Npc))
                continue;
            ladder._npcs[n.Npc] = new NpcState
            {
                Urge = Clamp01(n.Urge),
                Rung = n.Rung,
                Day = n.Day,
                AttemptsToday = n.AttemptsToday,
                LastAttemptTick = n.LastAttemptTick,
                OpenStep = n.OpenStep,
                OpenTick = n.OpenTick,
                IntentBoostDay = n.IntentBoostDay,
            };
        }
        return ladder;
    }

    private sealed class NpcState
    {
        public double Urge;
        public int Rung;
        public int Day;                  // day index this state's daily fields belong to
        public int AttemptsToday;
        public int? LastAttemptTick;
        public InitiationStep? OpenStep; // unresolved attempt, if any
        public int? OpenTick;
        public int? IntentBoostDay;      // day the intent boost was last applied
    }

    private sealed class LadderDto
    {
        public int Day { get; set; } = int.MinValue;
        public int AttemptsToday { get; set; }
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
        public InitiationStep? OpenStep { get; set; }
        public int? OpenTick { get; set; }
        public int? IntentBoostDay { get; set; }
    }
}
