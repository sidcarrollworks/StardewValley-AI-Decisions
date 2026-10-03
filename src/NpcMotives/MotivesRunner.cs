using System.Globalization;
using System.Text.Json;
using NpcDecision;
using NpcMemory;
using NpcSchedules;

namespace NpcMotives;

/// <summary>
/// Runs the motives engine over time, in shadow (docs/spec/motives.md): each tick, every NPC's
/// motives, mood and best act are weighed from its copied <see cref="MotiveInputs"/>; when the
/// pacing allows, a motive the act rule can afford becomes an attempt the NPC WOULD make. It keeps
/// what the engine can't: open attempts and their response windows (ignored attempts frustrate),
/// the ladder's caps and cooldowns, and which motives were used up (a greeting, shared news, a
/// thanks; hurt vents). An NPC has at most one attempt of each sort open: one in person (an emote,
/// a bubble, a walk-up, an interrupt, a visit), which blocks new attempts until it is answered or
/// its short window passes, and one that waits (a letter, a queued line, a request), which only
/// blocks another of its sort. The model is asked only to pick among motives and on close calls, through
/// the given client (in the mod a <c>ResilientDecisionClient</c>, so on the worker thread only).
/// It never touches the game and never reads a live position. Deterministic: NPCs in name order,
/// no clock, and the one draw (the grudge) is FNV-1a over the per-save seed.
/// </summary>
public sealed class MotivesRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IDecisionClient _decision;
    private readonly MotivesEngine _engine;
    private readonly MotiveOptions _o;

    private readonly Dictionary<string, NpcState> _npcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MotiveDecision> _latest = new(StringComparer.OrdinalIgnoreCase);

    // Blocked events log at most once per (npc, reason) per day; never saved.
    private readonly Dictionary<string, int> _blockedLoggedDay = new(StringComparer.OrdinalIgnoreCase);

    private int _day = int.MinValue;
    private int _attemptsToday;
    private int _queuedLinesToday;
    private int _lettersToday;
    private int _week = int.MinValue;
    private int _interruptsThisWeek;
    private int _visitsThisWeek;

    public MotivesRunner(IDecisionClient decision, MotiveOptions? options = null)
    {
        _decision = decision ?? throw new ArgumentNullException(nameof(decision));
        _o = options ?? new MotiveOptions();
        _engine = new MotivesEngine(_o);
    }

    public MotiveOptions Options => _o;

    /// <summary>
    /// One ten-minute tick. Inputs may come in any order; NPCs are processed in name order and the
    /// events come back in that order.
    /// </summary>
    public IReadOnlyList<MotiveEvent> Tick(int absoluteTick, IReadOnlyList<MotiveInputs> inputs)
    {
        int day = GameClock.DayIndex(absoluteTick);
        RollGlobal(day);

        var events = new List<MotiveEvent>();
        foreach (MotiveInputs raw in inputs.OrderBy(i => i.Npc, StringComparer.OrdinalIgnoreCase))
        {
            NpcState s = StateOf(raw.Npc, day);

            // (1) Open attempts whose window has passed: settled before the day rollover, so an
            // attempt left open overnight belongs to the day it was made.
            if (s.Open is { } open && absoluteTick >= ResolveAt(open))
            {
                s.Open = null;
                events.Add(Resolve(raw.Npc, s, open, absoluteTick));
            }
            if (s.Waiting is { } waiting && absoluteTick >= ResolveAt(waiting))
            {
                s.Waiting = null;
                events.Add(Resolve(raw.Npc, s, waiting, absoluteTick));
            }

            // (2) The day rolls over: daily counts and frustration reset.
            RollNpc(s, day);

            // (3) The runner's own facts go into the inputs.
            MotiveInputs i = raw with
            {
                Now = absoluteTick,
                GreetedToday = s.GreetedDay == day,
                NewsShared = s.NewsSharedDay == day,
                IgnoredToday = s.IgnoredToday,
                AttemptsLeftToday = Math.Max(0, _o.MaxAttemptsPerDay - _attemptsToday),
                ThankedTick = s.ThankedTick,
                LastTalkTick = s.LastContactTick,
                VentTicks = s.VentTicks.ToArray(),
                Unavailable = CappedActs(s),
                AttentionCapped = AttentionCap(s) is not null,
                LightCapped = s.LightToday >= _o.MaxLightActsPerNpcPerDay,
            };

            // (4) A grudge past the threshold: would they hold it against the player? (once a day)
            if (MaybeGrudge(i, s, day) is { } grudge)
                events.Add(grudge);

            // (5) Weigh everything without the model: what the viewer shows, and the default pick.
            MotiveDecision preview = _engine.Decide(i);
            _latest[i.Npc] = preview;
            if (preview.Chosen is null)
                continue; // no motive, no act

            // (6) Pacing: an attempt still open and the cooldowns wait quietly; a cap is logged
            // once per NPC and reason a day.
            if (s.Open is not null || InCooldown(s, absoluteTick))
                continue;
            if (AttentionCap(s) is { } cap && i.LightCapped)
            {
                if (FirstToday(i.Npc, cap, day))
                    events.Add(new MotiveEvent(absoluteTick, i.Npc, "Blocked", null, preview.Chosen.Motive, false, cap, preview));
                continue;
            }

            // (7) Several motives: the model picks which goes first (fallback: the strongest).
            // A question the model answered "no" to a moment ago is not asked again until the
            // situation changes or a cooldown passes: the same inputs would give the same answer.
            MotiveDecision d = preview;
            string? choice = null;
            // Only motives that could act now go to the model: asking it to pick one the act rule
            // would then pass on wastes the call (playtest 2026-10-02: Emily was asked "news or
            // hurt?" every hour while both passed on the day's attempts reserve).
            List<MotiveStrength> pool = _engine.Candidates(i)
                .Where(m => m.Strength >= _o.MinMotiveForChoice)
                .OrderByDescending(m => m.Strength).ThenBy(m => (int)m.Motive)
                .Where(m => _engine.DecideFor(i, m).Result is not null)
                .Take(Math.Max(1, _o.MaxChoiceOptions))
                .ToList();
            if (pool.Count == 1 && pool[0].Motive != preview.Chosen.Motive)
            {
                // The strongest can't act but another can: that one goes, no question needed.
                d = _engine.DecideFor(i, pool[0]);
                _latest[i.Npc] = d;
            }
            string askKey = $"{string.Join(",", pool.Select(m => m.Motive))}|{preview.Chosen.Motive}|{preview.Pending?.Act}";
            if (s.LastAskedKey == askKey && s.LastAskedTick is { } askedAt && absoluteTick - askedAt < _o.CooldownTicks)
                continue;
            bool asked = false;
            if (pool.Count >= 2)
            {
                asked = true;
                IReadOnlyList<double> p = _decision.Choose(pool.Select(m => MotiveText.Option(m.Motive)).ToList(),
                    MotiveText.ChoiceState(i, preview));
                MotiveStrength picked = pool[ArgMax(p)];
                choice = string.Join(" | ", pool.Select((m, k) => string.Format(CultureInfo.InvariantCulture,
                    "{0} {1:0.00}: {2:0.00}", m.Motive, m.Strength, k < p.Count ? p[k] : double.NaN)));
                if (picked.Motive != preview.Chosen.Motive)
                {
                    d = _engine.DecideFor(i, picked);
                    _latest[i.Npc] = d;
                }
            }

            // (8) A close call goes to the model; the mood tilts the answer.
            double? modelP = null, tilted = null;
            Act? act = d.Result;
            if (d.Pending is { } pending)
            {
                asked = true;
                double p = _decision.YesNo(MotiveText.CloseCallState(i, d),
                    MotivesEngine.CloseCallProposition(i.Npc, pending.Act, pending.Hostile));
                modelP = p;
                tilted = _engine.Tilt(d, p);
                act = _engine.ResolveClose(d, p);
            }

            if (act is not { } taken || d.Chosen is null)
            {
                if (asked)
                {
                    s.LastAskedKey = askKey;
                    s.LastAskedTick = absoluteTick;
                }
                // Log a pass only when it differs from this NPC's last pass today.
                string key = d.Pending is { } close ? $"{d.Chosen?.Motive}|close|{close.Act}" : $"{d.Chosen?.Motive}|{d.Reason}";
                if (s.LastPassKey != key || s.LastPassDay != day)
                {
                    s.LastPassKey = key;
                    s.LastPassDay = day;
                    events.Add(new MotiveEvent(absoluteTick, i.Npc, "Pass", d.Pending?.Act, d.Chosen?.Motive,
                        d.Pending?.Hostile ?? false, d.Reason, d, modelP, tilted, choice));
                }
                continue;
            }

            // (9) The attempt.
            bool hostile = d.Checks.FirstOrDefault(c => c.Act == taken)?.Hostile ?? false;
            Open(s, taken, d.Chosen.Motive, hostile, absoluteTick, day);
            events.Add(new MotiveEvent(absoluteTick, i.Npc, "Act", taken, d.Chosen.Motive, hostile,
                d.Pending is not null && d.Pending.Act == taken ? "close call" : "clear yes",
                d, modelP, tilted, choice));
        }
        return events;
    }

    /// <summary>
    /// The player talked to this NPC: its open attempts are answered (a queued line is heard, so
    /// its news or thanks is delivered), frustration clears, the greeting is done for the day, and
    /// the NPC waits out the cooldown before trying anything.
    /// </summary>
    public IReadOnlyList<MotiveEvent> NoteTalked(string npc, int absoluteTick)
    {
        int day = GameClock.DayIndex(absoluteTick);
        RollGlobal(day);
        NpcState s = StateOf(npc, day);
        RollNpc(s, day);
        s.LastContactTick = absoluteTick;
        s.GreetedDay = day;

        var answered = new List<MotiveEvent>();
        foreach (Attempt a in new[] { s.Open, s.Waiting }.OfType<Attempt>())
        {
            if (a.Act == Act.QueuedLine)
                Deliver(s, a.Motive, absoluteTick, day);
            answered.Add(new MotiveEvent(absoluteTick, npc, "Responded", a.Act, a.Motive, a.Hostile,
                "the player talked to them"));
        }
        if (answered.Count > 0)
            s.IgnoredToday = 0;
        s.Open = null;
        s.Waiting = null;
        return answered;
    }

    /// <summary>Each NPC's latest weighed decision (a new dictionary, safe to hand to another
    /// thread): its motives, mood and best act, whether or not it was allowed to act.</summary>
    public IReadOnlyDictionary<string, MotiveDecision> LatestDecisions()
        => new Dictionary<string, MotiveDecision>(_latest, StringComparer.OrdinalIgnoreCase);

    /// <summary>Each NPC's pacing state, in name order (a read-only copy for display).</summary>
    public IReadOnlyList<MotiveNpcState> States()
        => _npcs.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new MotiveNpcState(kv.Key, kv.Value.AttemptsToday, kv.Value.IgnoredToday,
                kv.Value.Open?.Act, kv.Value.Open?.Tick, kv.Value.Open?.Motive,
                kv.Value.Waiting?.Act, kv.Value.Waiting?.Tick, kv.Value.LastPenaltyDay))
            .ToList();

    // ---- pacing ------------------------------------------------------------------------------

    /// <summary>The daily cap on acts that ask for the player's attention, or null. Light acts
    /// (<see cref="MotiveOptions.IsLight"/>) may still go while it holds; the NPC is blocked only
    /// when its light acts are used up too.</summary>
    private string? AttentionCap(NpcState s)
    {
        if (s.AttemptsToday >= _o.MaxAttemptsPerNpcPerDay)
            return string.Format(CultureInfo.InvariantCulture, "cap: {0} attempts today", s.AttemptsToday);
        if (_attemptsToday >= _o.MaxAttemptsPerDay)
            return string.Format(CultureInfo.InvariantCulture, "cap: the town's {0} attempts today", _attemptsToday);
        return null;
    }

    /// <summary>True the first time a (npc, reason) block is seen on a day.</summary>
    private bool FirstToday(string npc, string reason, int day)
    {
        string key = npc + "|" + reason;
        if (_blockedLoggedDay.TryGetValue(key, out int logged) && logged == day)
            return false;
        _blockedLoggedDay[key] = day;
        return true;
    }

    private bool InCooldown(NpcState s, int tick)
        => (s.LastAttemptTick is { } a && tick - a < _o.CooldownTicks)
           || (s.LastContactTick is { } c && tick - c < _o.CooldownTicks);

    /// <summary>Acts a cap rules out now (the town's daily and weekly caps, and a second letter or
    /// queued line while one already waits); the act rule then picks a cheaper one.</summary>
    private IReadOnlyCollection<Act> CappedActs(NpcState s)
    {
        var capped = new List<Act>();
        if (s.Waiting is not null)
            capped.AddRange(new[] { Act.Letter, Act.QueuedLine, Act.AskForHelp });
        if (_queuedLinesToday >= _o.MaxQueuedLinesPerDay)
            capped.Add(Act.QueuedLine);
        if (_lettersToday >= _o.MaxLettersPerDay)
        {
            capped.Add(Act.Letter);
            capped.Add(Act.AskForHelp);
        }
        if (_interruptsThisWeek >= _o.MaxInterruptsPerWeek)
            capped.Add(Act.Interrupt);
        if (_visitsThisWeek >= _o.MaxVisitsPerWeek)
            capped.Add(Act.Visit);
        return capped;
    }

    private static bool Waits(Act act) => act is Act.Letter or Act.QueuedLine or Act.AskForHelp;

    private void Open(NpcState s, Act act, Motive motive, bool hostile, int tick, int day)
    {
        var attempt = new Attempt { Act = act, Tick = tick, Motive = motive, Hostile = hostile };
        if (Waits(act))
            s.Waiting = attempt;
        else
            s.Open = attempt;
        s.LastAttemptTick = tick;
        if (MotiveOptions.IsLight(act, motive))
            s.LightToday++; // a wave asks nothing of the player: none of the day's attempts
        else
        {
            s.AttemptsToday++;
            _attemptsToday++;
        }
        switch (act)
        {
            case Act.QueuedLine: _queuedLinesToday++; break;
            case Act.Letter or Act.AskForHelp: _lettersToday++; break;
            case Act.Interrupt: _interruptsThisWeek++; break;
            case Act.Visit: _visitsThisWeek++; break;
        }

        // Using the motive up (motives.md, "Using a motive up"). Any face-to-face act counts as
        // the day's greeting. A queued line delivers only when the player comes to talk; an emote
        // shows a feeling but says nothing.
        if (motive == Motive.Greeting || act is Act.Emote or Act.Bubble or Act.WalkUp or Act.Interrupt)
            s.GreetedDay = day;
        if (hostile)
            s.VentTicks.Add(tick); // acting on hurt vents it; regard is unchanged
        else if (act is not (Act.QueuedLine or Act.Emote))
            Deliver(s, motive, tick, day);
    }

    private static void Deliver(NpcState s, Motive motive, int tick, int day)
    {
        if (motive == Motive.News)
            s.NewsSharedDay = day;
        else if (motive == Motive.Grateful)
            s.ThankedTick = tick;
    }

    private MotiveEvent Resolve(string npc, NpcState s, Attempt a, int tick)
    {
        int resolveAt = ResolveAt(a);
        int stamp = Math.Min(tick, resolveAt);
        if (stamp == resolveAt && resolveAt % GameClock.TicksPerDay == 0)
            stamp = resolveAt - 1; // a window that closed with the day belongs to that day
        if (a.Act == Act.QueuedLine)
            return new MotiveEvent(stamp, npc, "Expired", a.Act, a.Motive, a.Hostile,
                "the player did not talk to them before the day ended");
        if (GameClock.DayIndex(stamp) == s.Day)
            s.IgnoredToday++; // frustration, for the rest of the day
        return new MotiveEvent(stamp, npc, "Ignored", a.Act, a.Motive, a.Hostile,
            Waits(a.Act)
                ? "no visit by the end of the day after the letter"
                : $"no response within {_o.ResponseWindowTicks} ticks");
    }

    /// <summary>The first tick at which an open attempt counts as unanswered (the ladder's
    /// windows): in-person acts get the response window, cut off when the day ends; a queued line
    /// waits for the rest of the day; a letter until the end of the next day.</summary>
    private int ResolveAt(Attempt a)
    {
        int day = GameClock.DayIndex(a.Tick);
        int openTick = a.Tick;
        return a.Act switch
        {
            Act.QueuedLine => GameClock.DayStartTick(day + 1),
            Act.Letter or Act.AskForHelp => GameClock.DayStartTick(day + 2),
            _ => Math.Min(openTick + _o.ResponseWindowTicks, GameClock.DayStartTick(day + 1)),
        };
    }

    // ---- the grudge (motives.md, "Grudge and friendship loss") ---------------------------------

    private MotiveEvent? MaybeGrudge(MotiveInputs i, NpcState s, int day)
    {
        double grudge = Math.Max(0, -i.RegardForPlayer);
        if (grudge < _o.GrudgeThreshold || s.GrudgeAskedDay == day)
            return null;
        if (s.LastPenaltyDay is { } last && day - last < _o.PenaltyCooldownDays)
            return null;
        s.GrudgeAskedDay = day;
        double p = _decision.YesNo(MotiveText.GrudgeState(i, _o.PenaltyCooldownDays), MotiveText.GrudgeProposition(i.Npc));
        if (double.IsNaN(p))
            p = 0.5; // the spec's fallback
        uint h = unchecked((uint)Fnv1a.Seed(i.Seed.ToString(CultureInfo.InvariantCulture), "grudge", i.Npc,
            day.ToString(CultureInfo.InvariantCulture)));
        if (!(h / 4294967296.0 < p))
            return null; // the draw said no: asked again tomorrow
        s.LastPenaltyDay = day;
        string causes = MotiveText.GrudgeCauses(i.Diary, i.Now, _o.PenaltyCooldownDays);
        return new MotiveEvent(i.Now, i.Npc, "Grudge", null, Motive.Hurt, true,
            causes.Length > 0 ? causes : "an old grudge", ModelP: p, Grudge: grudge, RegardRelief: _o.GrudgeRelief);
    }

    // ---- days --------------------------------------------------------------------------------

    private NpcState StateOf(string npc, int day)
    {
        if (!_npcs.TryGetValue(npc, out NpcState? s))
            _npcs[npc] = s = new NpcState { Day = day };
        return s;
    }

    private void RollNpc(NpcState s, int day)
    {
        if (s.Day == day)
            return;
        s.Day = day;
        s.AttemptsToday = 0;
        s.LightToday = 0;
        s.IgnoredToday = 0;
        // Vents only matter while the hurt they relieved is still in the elastic window.
        int keepFrom = GameClock.DayStartTick(day - _o.ElasticWindowDays - 1);
        s.VentTicks.RemoveAll(t => t < keepFrom);
    }

    private void RollGlobal(int day)
    {
        if (_day != day)
        {
            _day = day;
            _attemptsToday = 0;
            _queuedLinesToday = 0;
            _lettersToday = 0;
        }
        int week = day / 7;
        if (_week != week)
        {
            _week = week;
            _interruptsThisWeek = 0;
            _visitsThisWeek = 0;
        }
    }

    /// <summary>The highest probability's index; ties and NaN go to the first (the strongest).</summary>
    private static int ArgMax(IReadOnlyList<double> p)
    {
        int best = 0;
        for (int k = 1; k < p.Count; k++)
            if (p[k] > (double.IsNaN(p[best]) ? double.NegativeInfinity : p[best]))
                best = k;
        return best;
    }

    // ---- persistence -------------------------------------------------------------------------

    /// <summary>Pacing state as JSON (saved under the additive key <c>motives</c>). The options
    /// and the latest decisions are not saved.</summary>
    public string ToJson()
    {
        var dto = new RunnerDto
        {
            Day = _day,
            AttemptsToday = _attemptsToday,
            QueuedLinesToday = _queuedLinesToday,
            LettersToday = _lettersToday,
            Week = _week,
            InterruptsThisWeek = _interruptsThisWeek,
            VisitsThisWeek = _visitsThisWeek,
            Npcs = _npcs.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => NpcDto.From(kv.Key, kv.Value))
                .ToList(),
        };
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>A runner from <see cref="ToJson"/> output; null, empty or damaged JSON gives a
    /// fresh runner (the save still loads).</summary>
    public static MotivesRunner FromJson(string? json, IDecisionClient decision, MotiveOptions? options = null)
    {
        var runner = new MotivesRunner(decision, options);
        if (string.IsNullOrWhiteSpace(json))
            return runner;
        RunnerDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<RunnerDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return runner;
        }
        if (dto is null)
            return runner;
        runner._day = dto.Day;
        runner._attemptsToday = dto.AttemptsToday;
        runner._queuedLinesToday = dto.QueuedLinesToday;
        runner._lettersToday = dto.LettersToday;
        runner._week = dto.Week;
        runner._interruptsThisWeek = dto.InterruptsThisWeek;
        runner._visitsThisWeek = dto.VisitsThisWeek;
        foreach (NpcDto n in dto.Npcs ?? new List<NpcDto>())
        {
            if (string.IsNullOrEmpty(n.Npc) || string.Equals(n.Npc, "null", StringComparison.OrdinalIgnoreCase))
                continue;
            runner._npcs[n.Npc] = n.ToState();
        }
        return runner;
    }

    private sealed class Attempt
    {
        public Act Act { get; set; }
        public int Tick { get; set; }
        public Motive Motive { get; set; }
        public bool Hostile { get; set; }
    }

    private sealed class NpcState
    {
        public int Day;
        public int AttemptsToday;        // acts that ask for attention; light acts are counted apart
        public int LightToday;
        public int IgnoredToday;
        public int? LastAttemptTick;
        public int? LastContactTick;
        public string? LastAskedKey;     // the model question last answered with a pass
        public int? LastAskedTick;
        public string? LastPassKey;      // not saved: after a load the first pass logs again
        public int? LastPassDay;
        public Attempt? Open;            // in person: blocks new attempts until settled
        public Attempt? Waiting;         // a letter, queued line or request: blocks only its own sort
        public int? GreetedDay;
        public int? NewsSharedDay;
        public int? ThankedTick;
        public List<int> VentTicks = new();
        public int? GrudgeAskedDay;
        public int? LastPenaltyDay;
    }

    private sealed class RunnerDto
    {
        public int Day { get; set; } = int.MinValue;
        public int AttemptsToday { get; set; }
        public int QueuedLinesToday { get; set; }
        public int LettersToday { get; set; }
        public int Week { get; set; } = int.MinValue;
        public int InterruptsThisWeek { get; set; }
        public int VisitsThisWeek { get; set; }
        public List<NpcDto>? Npcs { get; set; }
    }

    private sealed class NpcDto
    {
        public string Npc { get; set; } = "";
        public int Day { get; set; }
        public int AttemptsToday { get; set; }
        public int LightToday { get; set; }
        public int IgnoredToday { get; set; }
        public int? LastAttemptTick { get; set; }
        public int? LastContactTick { get; set; }
        public string? LastAskedKey { get; set; }
        public int? LastAskedTick { get; set; }
        public Attempt? Open { get; set; }
        public Attempt? Waiting { get; set; }
        public int? GreetedDay { get; set; }
        public int? NewsSharedDay { get; set; }
        public int? ThankedTick { get; set; }
        public List<int>? VentTicks { get; set; }
        public int? GrudgeAskedDay { get; set; }
        public int? LastPenaltyDay { get; set; }

        public static NpcDto From(string npc, NpcState s) => new()
        {
            Npc = npc,
            Day = s.Day,
            AttemptsToday = s.AttemptsToday,
            LightToday = s.LightToday,
            IgnoredToday = s.IgnoredToday,
            LastAttemptTick = s.LastAttemptTick,
            LastContactTick = s.LastContactTick,
            LastAskedKey = s.LastAskedKey,
            LastAskedTick = s.LastAskedTick,
            Open = s.Open,
            Waiting = s.Waiting,
            GreetedDay = s.GreetedDay,
            NewsSharedDay = s.NewsSharedDay,
            ThankedTick = s.ThankedTick,
            VentTicks = s.VentTicks.ToList(),
            GrudgeAskedDay = s.GrudgeAskedDay,
            LastPenaltyDay = s.LastPenaltyDay,
        };

        public NpcState ToState() => new()
        {
            Day = Day,
            AttemptsToday = AttemptsToday,
            LightToday = LightToday,
            IgnoredToday = IgnoredToday,
            LastAttemptTick = LastAttemptTick,
            LastContactTick = LastContactTick,
            LastAskedKey = LastAskedKey,
            LastAskedTick = LastAskedTick,
            Open = Open,
            Waiting = Waiting,
            GreetedDay = GreetedDay,
            NewsSharedDay = NewsSharedDay,
            ThankedTick = ThankedTick,
            VentTicks = VentTicks?.ToList() ?? new List<int>(),
            GrudgeAskedDay = GrudgeAskedDay,
            LastPenaltyDay = LastPenaltyDay,
        };
    }
}

/// <summary>One NPC's pacing state as the runner keeps it (a read-only copy for display).</summary>
public sealed record MotiveNpcState(
    string Npc,
    int AttemptsToday,
    int IgnoredToday,
    Act? OpenAct,          // an in-person attempt waiting for the player
    int? OpenTick,
    Motive? OpenMotive,
    Act? WaitingAct,       // a letter, queued line or request waiting for the player
    int? WaitingTick,
    int? LastPenaltyDay);
