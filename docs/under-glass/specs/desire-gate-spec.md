> The implementation spec as written before building (kept for reference). Where the code differs, `sim/README.md` and `docs/under-glass/design.md` ("As built") say how and why.

# Under Glass phase 0d.1: the desire gate (rule 10): implementation spec

**Code base.** All hooks below were checked against `1d7d124`, the HEAD of `claude/affectionate-pasteur-qzdpvi`. `git diff 2ce627b 1d7d124 -- sim/UnderGlass.Sim sim/UnderGlass.Run` is empty, so the library is 0c's as merged. Line numbers refer to that commit.

**Evidence.**
- The base design's working prototype is `scratchpad/probe-minimal/sim/UnderGlass.Sim/Simulation.Desire.cs`. Its measurements are in `scratchpad/probe-minimal/runs/*.txt`: a5 (30 seed-years), c1 (100 seed-years), a2 (the 0a band at 400 x 14) and the `sw-*` sweep.
- The faithful prototype and its runs are in `scratchpad/probe-faithful-b/`.
- The brief's reach and act measurements are in `scratchpad/probe-desire/REPORT-50x112.txt`.

**Status of the numbers.** Every number marked *measured* comes from those files. Every other number is a first guess that the sweeps in section 12 will settle. Every Spinoza proposition number is the design's own, recalled from memory (VERIFY). No game fact is used.

---

## 0. What 0d.1 builds

0c's town has feelings but no wants, and that is why E1 fails:
- Acts aimed at someone come at town-wide rates and land on whoever is in reach. Mostly that is family, at home.
- The person hurt or helped almost never answers. An argument is answered in kind within 7 days 0.8% of the time, and a kindness is returned 8.8% of the time.
- Drift heals every grudge within about a month.

Meeting is not the problem. After an argument between households, the two are back in reach within a day in 90% of cases.

This phase builds rule 10's gate on **motives toward a named person**. A villager acts on a motive only when that person is in reach and the gate says yes. It also gives every villager a **character that can change**, and a **record of what happened to them**, which phase 0e's plasticity rule will feed on (Sid, 2026-10-07, section 11e).

**Base: the "minimal" design**, which two of the three judges chose. Its prototype is the only one measured against every gate:
- E1 73% (a5, 30 seed-years); 65% without avoidance (c1, 100 seed-years);
- war towns and dead towns 0%;
- pairs below -0.2: 1.0-1.2% at d55-d111;
- the 0a band at 57% in band and 24% over 70%;
- authority 97% decided right and 88% reported;
- grievance 0; cash conserved;
- 4 of 18 swept cells lively (22%).

**Kept from the base:**
- **Motives come from events, not from a clock.** Being argued with stirs **Answer**. Being given a gift or helped stirs **Return**. Being argued with by someone you still like stirs **MakeUp**. Being robbed, or named while innocent, stirs **Retaliate**.
- Families cover: the gate never acts between kin or housemates.
- Fear per hostile hit received: law 4's fear of greater harm, measured as the brake on war towns.
- A close-call draw keyed to the motive's state, so it cannot be redrawn every tick.
- One return per kindness, so there is no ping-pong.
- "Gave cause": someone argued with over their own scandal feels shame, not a grudge.
- Avoidance, and withdrawal home.
- The PerDay rates of the aimed acts stay, as the town's everyday occasions.

**Grafted from the other designs and the judges:**
1. **Character.** A per-person mutable character that **every** trait read in the library goes through, with `SetTrait`/`TraitOf`, a `--trait` runner flag, start and end snapshots, and a fringe metrics line (faithful).
2. **The record.** `LifeEvent`s with severity and an outcome **from both sides**: Answered, Returned, Rebuffed, Avoided, Ignored or None. They are resolved at night (faithful).
3. **Rule 10 in its literal form.** Hostile acts cost the form cost + 0.3, so Argued is 0.5 + 0.3 = 0.8, the same number the base measured. Law 1's power of acting enters effective boldness (faithful).
4. **Light hostile acts.** `Snubbed`, which needs daring, and `TurnedAway`, which needs none: the shy's cold shoulder. Both come under a cap of 2 light acts a day, and a third slight within 5 days leaves a mark (rule 4; faithful and mod-lessons).
5. **Avoidance opens to the shy who might.** Avoidance starts when no hostile act was taken and the best hostile margin is below 0, not only when every act is a clear no (judge 3).
6. **Only those who cannot answer leave.** Withdrawal happens only when the person could not clear an argument now, so the bold stay and the shy go (faithful G12).
7. **Stance.** A lasting per-person state, -1 to +1. Hurt pushes the bold toward combat and the shy toward withdrawal; kindness pulls both back toward 0. It has a 28-day half-life, enters effective boldness and the home weight, and has its own switch. This is Sid's deepening loop in 0d (mod-lessons).
8. **Fond.** Love wants to be near (law 4), counting only love above `LoveAt`, times absence. It lifts friendship, the binding half of E1, without faithful's cliff (judge 2).
9. **Pity**, behind a switch and off by default: a witnessed misfortune stirs help. Families cover, so it never fires at home (mod-lessons; the fallback lever for friendship).
10. **Fear of the subject's kin present**, behind a knob at 0 until swept (faithful).
11. **Stirs only from what the holder saw or took part in.** No motive comes from hearsay, except rule 9's keeper of a robbed shop (mod D34).
12. **A spark for the first season end**, behind a switch: the first-greeting tone, which logs its cause. The other option is `--tensions` (Q1).
13. **One reach test.** `InReach` is factored out of `Candidates`, so S1 and the gate share it.
14. **Motives without acts.** An observe-only slice computes motives and logs them to `_motiveLog`, never `_log`.

Appendix A lists every flaw the judges found in the base, how it is fixed, and each place where this spec does not follow a judge, with the reason.

---

## 1. Slices

Each slice is one commit, with its tests green and its numbers measured.

| Slice | Content | Must hold before the next |
|---|---|---|
| **0d.0 Character** | `_character` (`Temperament[]`), `CharacterOf`, `TraitOf`, `SetTrait`, the `Trait` enum. All 14 trait reads go through `CharacterOf` (section 6). `SimResult.CharactersAtStart` and `CharactersAtEnd`. Record pin **P1** *before* touching code. | T2 (`e7f6653087ff7e18`, `388d128d7fd4cdf2`), T12, P1, and every existing test unchanged. D1 and D2. |
| **0d.1 Motives, observed** | Options, records, `StirFrom`, `StirAccused`, the motive table, intensity, `LifeEvent` writing. `DesireActs = false`: motives are stirred, weighed and lapse, but no act starts, and every line goes to `_motiveLog`. | P1 holds with `Desire = true, DesireActs = false` (P2). Pure tests D3-D12. |
| **0d.2 The gate** | `Pursue` (R5-R7), the cooldown, slots, re-asking, fear, the rate exclusions (R11), avoidance (with the base trigger), withdrawal home, outcomes (R12). `DefaultTown.Feelings()` sets `Desire = true`. Every graft switch is off. | Scenes D13-D20. Invariants D35. **The base reproduces a5 within one SE at 30 seed-years** (section 12, step 1). Then the first 200 x 112 confirmation (step 2). |
| **0d.3 The shy and the brawler** | `LightActsOn` (Snubbed, TurnedAway, the mark, the new avoidance trigger, the withdrawal split), then `StanceOn`, then `PowerWeight`. Each is measured alone, then turned on in `DefaultTown.Feelings()` if the gate holds. | Scenes D21-D27. Steps 3-6. |
| **0d.4 Friendship** | `FondOn`, then `PityOn` only if needed. | Scenes D28-D30. Step 7. |
| **0d.5 Spark, sweep, ablation, docs** | `ToneOn`, `--tensions`, the parameter sweep, E2, the three-year run, pin **P3**, docs. | The phase gate (section 11). |

---

## 2. Data model

### 2.1 `Model.cs` (appended at the end; no positional record changes)

```csharp
/// <summary>The six temperament weights of law 12, for reading and setting one trait.</summary>
public enum Trait { Chattiness, Boldness, Understanding, SelfRegard, Sensitivity, Retention }

/// <summary>What a motive wants (design section 5). Saved as ints in later phases: append only.</summary>
public enum DesireKind { Answer, Return, MakeUp, Retaliate, Fond, Pity }

/// <summary>One weighing of one act for one motive by the gate (rule 10; principle 5).</summary>
public sealed record Pursuit(int Tick, string Holder, string Subject, DesireKind Motive, int Source, string ActKind,
    double Intensity, double Effective, double Cost, double Margin, string Call /* clear | no | close-yes | close-no | stands */,
    double P, bool Acted, int ActId);

/// <summary>A motive stirred, or stirred again.</summary>
public sealed record Stirring(int Tick, string Holder, string Subject, DesireKind Motive, int Source, double Felt);

/// <summary>How an act or a motive turned out (rule 18's input). Answered: hostility met with hostility.
/// Returned: kindness met with kindness. Rebuffed: kindness met with hostility. Avoided: met by avoiding.
/// Ignored: nothing within OutcomeDays. None: a light act, or a role with no outcome.</summary>
public enum Outcome { Open, Answered, Returned, Rebuffed, Avoided, Ignored, None }

/// <summary>What a person did, or had done to them, or chose not to do (Sid, 2026-10-07; rule 18).</summary>
public enum LifeRole { Did, Undergone, Declined, Avoided, Withdrew, Lapsed, GaveCause, Dropped }

/// <summary>One event in a person's life, for the plasticity rule of 0e. Written from the truth and
/// never read by any 0d rule. Severity: |row Joy| for Did; |felt mood| for Undergone; motive
/// intensity for Declined, Avoided, Lapsed and Dropped; felt for GaveCause; 0 for Withdrew.</summary>
public sealed record LifeEvent(int Tick, string Person, string Other, int ActId, string Kind, LifeRole Role,
    double Severity, bool Hostile, bool Light, Outcome Outcome = Outcome.Open, int ResolvedTick = -1);
```

### 2.2 `FeelingOptions` (in `Feelings.cs`), appended after `FriendAt`

The existing constants this spec reuses are `LoveAt` (0.2), `CoverAt` (0.4), `MoodDays` (3), `RepeatDays` (7), `ContactMinutes` (60), `PlasticScale`, and `PerceptionOptions.NearTiles` (5).

**Switches.** All have no effect unless `Steering` is on. Tuning never touches them; they exist for E2 ablation.

| Name | Default | In `DefaultTown.Feelings()` | Purpose |
|---|---|---|---|
| `Desire` | `false` | `true` from 0d.2 | The gate's own switch (requirement 8). Worlds built by tests keep 0c. |
| `DesireActs` | `true` | `true` | Slice switch. When false, motives are stirred and weighed, logged to `_motiveLog`, and no act starts. |
| `AnswerOn`, `ReturnOn`, `MakeUpOn`, `RetaliateOn` | `true` | `true` | Motive kinds, for E2. |
| `AvoidOn`, `WithdrawOn` | `true` | `true` | R9. |
| `LightActsOn` | `false` | `true` if step 3 passes | Snubbed, TurnedAway, the mark, the new avoidance trigger, the withdrawal split. |
| `StanceOn` | `false` | `true` if step 4 passes | R10. |
| `FondOn` | `false` | `true` if step 7 passes | R3. |
| `PityOn` | `false` | `false` (the fallback) | R4. |
| `ToneOn` | `false` | `false` (the d27 option) | R13. |
| `CloseCall` | `null` | | `Func<Pursuit, bool?>?`, the seam for Laya and for tests. A non-null answer replaces the draw. |

**Gate constants.** "R10" means rule 10 states the constant.

| Name | Default | Range | Rule |
|---|---|---|---|
| `FamiliarityWeight` | 0.5 | fixed | R10 |
| `IntensityWeight` | 0.5 | fixed | R10 |
| `PowerWeight` | 1.0 | 0-2 | Law 1: effective boldness + PowerWeight x (P - 0.5). It is 0 in the base slice and is measured at step 5. |
| `GiftCost` | 0.4 | 0.3-0.6 | R10, gift |
| `HelpCost` | 0.5 | 0.4-0.7 | R10, walk up (a help is in person) |
| `ArgueForm` | 0.5 | fixed | R10, walk up. With the surcharge, a confrontation costs 0.8. |
| `SnubForm` | 0.2 | fixed | R10, wave. With the surcharge, 0.5. |
| `HostileSurcharge` | 0.3 | fixed | R10, "hostile acts +0.3" |
| `GiftMin` / `HelpMin` / `ArgueMin` / `SnubMin` | 0.10 / 0.20 / 0.20 / 0.10 | 0-0.4 | R10's minimum intensity (the mod's bubble and walk-up values). TurnedAway has no gate, and its minimum is `SnubMin`. |
| `ClearBand` | 0.15 | fixed | R10. Clear when \|margin\| > 0.15 (strictly: "more than 0.15"). |
| `MoodTilt` | 0.1 | 0-0.2 | R10, "tilted by mood"; the mod's 0.10 |
| `SlotsPerDay` | 2 | fixed | R10, per (holder, subject, day). A slot is any attempt: an act begun, or a close call that ended with no act. |
| `AskAgainStep` | 0.1 | fixed | R10 |
| `HostileCooldownDays` | 3 | fixed | R10, per **ordered** pair, for heavy hostile acts only |
| `LightPerDay` | 2 | fixed | R10, per holder |
| `MotiveDays` | 7 | 3-14 | New (base). The event part of a motive fades linearly to 0 over this many days. |
| `FearPerHit` | 0.10 | 0-0.2 | New (base). Law 4, III P39 (VERIFY): + cost per heavy hostile act the subject did to the holder, as the holder witnessed it, within `FearDays`. |
| `FearDays` | 28 | 7-56 | New (base) |
| `FearPerKin` | 0.0 | 0-0.2 | Faithful. + cost per member of the subject's household who is in reach of the holder now and not of the holder's household. |
| `AvoidDays` | 7 | fixed | Design story 4 |
| `MarkCount` / `MarkDays` / `MarkSize` | 3 / 5 / 0.3 | fixed | Rule 4, "a third slight" |
| `StanceWeight` | 0.5 | 0-1 | Sid's direction 1 |
| `StanceKeepPerDay` | 0.975 | 0.95-0.99 | A 28-day half-life, like sentiments |
| `StanceHome` | 1.0 | 0-2 | Extra home weight per unit of withdrawn stance |
| `FondDays` | 7 | 4-14 | Law 4; the mod's MissingYou |
| `PityScale` | 2 | 0.5-3 | III P27 cor. 3 (VERIFY) |
| `PityMinutes` | 60 | fixed | Rule 7's "within an hour" |
| `Tone` | 0.03 | 0.01-0.06 | Section 6: the base chance that a day's first meeting is taken as not neutral |
| `ToneJoy` | 0.1 | 0.05-0.15 | The felt size of a warm or curt greeting |
| `OutcomeDays` | 7 | fixed | The window for outcomes; it matches the "answered within 7 days" metric |
| `AimedRateScale` | 1.0 | 0.5-1 | Multiplies the PerDay of GaveGift, HelpedSomeone and Argued while `Desiring`. A fallback for news volume only. `x * 1.0` is bit-identical, so the base is unchanged. |

**New tuning knobs, counted:**
- three from the base: `MotiveDays`, `FearPerHit`, `FearDays`;
- eight from the grafts: `PowerWeight`, `FearPerKin`, `StanceWeight`, `StanceKeepPerDay`, `StanceHome`, `FondDays`, `PityScale`, `Tone`;
- one fallback: `AimedRateScale`.

Every graft knob sits behind a switch, so E2 can ablate it.

`public static FeelingOptions WithDesire(params (string From, string To, double Regard)[] start)` is for tests: steering, `Desire = true`, `PowerWeight = 0`, and every graft switch off.

**Pure math, `public static class DesireMath`** (in `Feelings.cs`; not called `Desire`, which would clash with the option):

```csharp
public static double Effective(double boldness, double stance, bool hostile, double familiarity, double intensity,
    double power, FeelingOptions o)
    => boldness + o.StanceWeight * (hostile ? stance : Math.Min(0, stance))
       + o.FamiliarityWeight * familiarity + o.IntensityWeight * intensity + o.PowerWeight * (power - 0.5);
public static double Cost(double form, bool hostile, double fear, FeelingOptions o)
    => form + (hostile ? o.HostileSurcharge + fear : 0);
/// logistic(8m) = 0.5 + 0.5 tanh(4m); tanh(y) by its [3/2] Pade form y(15+y^2)/(15+6y^2).
/// Error at most 7e-6 for |m| <= 0.15. No Exp (0c's rule).
public static double CloseCallChance(double margin) { double y = 4 * margin; return 0.5 + 0.5 * y * (15 + y * y) / (15 + 6 * y * y); }
public static double Tilted(double p, double mood, bool hostile, FeelingOptions o)
    => Math.Clamp(p + o.MoodTilt * mood * (hostile ? -1 : 1), 0, 1);
public static string Call(double margin, FeelingOptions o) => margin > o.ClearBand ? "clear" : margin < -o.ClearBand ? "no" : "close";
public static double EventPart(double felt, int since, int m, FeelingOptions o)
    => felt * Math.Max(0, 1 - (m - since) / (double)(o.MotiveDays * Clock.MinutesPerDay));
public static double Fond(double regard, int daysApart, FeelingOptions o)
    => Math.Max(0, regard - o.LoveAt) * Math.Min(1, daysApart / (double)o.FondDays);
public static double Pity(double joy, double sens, double regard, double clarity, FeelingOptions o)
    => o.PityScale * Math.Abs(joy) * sens * (1 + Math.Max(0, regard)) * clarity;
public static double StanceAfterHurt(double stance, double felt, double boldness)
    => Math.Clamp(stance + felt * (2 * boldness - 1), -1, 1);
public static double StanceAfterKindness(double stance, double felt)
    => stance - Math.Min(1, felt) * stance;          // toward 0, never across it
public static (double Warm, double Curt) Tone(double power, double understanding, double regard, FeelingOptions o)
    => (o.Tone * 2 * power * (1 + regard), o.Tone * 4 * (1 - power) * (1 - understanding) * (1 - regard));
```

Nothing in these functions clamps a trait or assumes an archetype's range. Boldness enters linearly, so 0.02 and 0.98 give finite, ordered results (D2, D11).

### 2.3 Character (slice 0d.0; `Simulation.cs`)

```csharp
private readonly Temperament[] _character;                 // index = person; starts as _cast[i].Temperament
internal Temperament CharacterOf(int i) => _character[i];
public Temperament Character(string who) => _character[_index[who]];
public double TraitOf(string who, Trait t) => Get(_character[_index[who]], t);
/// For tests and 0e: set one trait, clamped to [0, 1]. Nothing in 0d calls it; call before Run.
public void SetTrait(string who, Trait t, double v) => _character[_index[who]] = With(_character[_index[who]], t, Math.Clamp(v, 0, 1));
```

`Get` and `With` are small switches over the six fields, using `with` expressions. `_character` is allocated in the constructor for **every** run, feelings on or off, because 0a's authority and 0b's temptation read boldness. It holds the cast's values, so the bytes are identical (P1, T2).

### 2.4 State: `Simulation.Desire.cs` (new partial)

```csharp
private sealed class Motive { public int Holder, Subject; public DesireKind Kind; public bool Hostile;
    public int Source; public int Since; public double Felt; public string Act = ""; public double AskedAt = double.NaN; public int Asks; }
private readonly SortedDictionary<(int Holder, int Subject, int Kind), Motive> _desires = new();
private readonly Dictionary<(int Holder, int Subject, int Day), int> _slotsUsed = new();
private readonly Dictionary<(int Actor, int Target), int> _lastHostile = new();   // heavy hostile acts, ordered pair
private readonly Dictionary<(int Holder, int By), List<int>> _hits = new();      // ticks, from the holder's witnessed beliefs
private readonly SortedDictionary<(int Holder, int Subject), int> _avoid = new(); // until minute
private readonly Dictionary<(int Holder, int Day), int> _lightToday = new();
private readonly HashSet<(int Holder, int Subject, int Day)> _turnedToday = new();
private readonly Dictionary<(int Holder, int Subject), int> _lastContactDay = new(); // a day with >= ContactMinutes together
private readonly Dictionary<(int Holder, int Subject), int> _lastKindDay = new();    // the holder's own kind act
private readonly HashSet<(int I, int J, int Day)> _toned = new();
private double[] _stance;                                                          // -1 withdrawn .. +1 combative
private readonly List<Pursuit> _pursuits = new(); private readonly List<Stirring> _stirred = new();
private readonly List<LifeEvent> _life = new(); private readonly List<string> _motiveLog = new();
private readonly HashSet<int> _pursuedActs = new(); private int _withdrawals;
private bool Desiring => Steering && _fo.Desire;
private bool Close(int a, int b) => _cast[a].Household == _cast[b].Household || AreKin(_names[a], _names[b]);
```

`_lastContactDay` starts at 0 for every pair, as if they met on the day before the run. That avoids a burst of Fond gifts on day 0.

### 2.5 `SimResult` (appended, `required`; empty when `Desiring` is false)

- `Pursuits`, `Stirred`, `LifeEvents`;
- `Pursued` (`IReadOnlySet<int>`, the act ids the gate started);
- `Avoids` (`(Tick, Holder, Subject, Source, Until)`), `Withdrawals` (int), `Marks` (int);
- `Stances`: name → `double[]`, one value per day at 23:59;
- `CharactersAtStart`, `CharactersAtEnd`: name → `Temperament`. These two are filled in every run, because they are cheap and 0e needs them;
- `MotiveLog`: the 0d.1 lines, never hashed.

### 2.6 `DefaultTown.cs`

Two light act kinds are appended **last** in `Acts()`, both with PerDay 0. `StartActs` tests `kind.PerDay <= 0` before any `Rng` call (`Simulation.cs:755`), so they draw nothing and shift no stream. Nobody's act list includes them.

```csharp
new ActKind("Snubbed", 0.5, -1, 1, 1, 0, Array.Empty<string>(),
    Affect: new Affect(Patient.Target, -0.15, 0.3, 1, TargetIs.Chosen)),
new ActKind("TurnedAway", 0.5, -1, 1, 1, 0, Array.Empty<string>(),
    Affect: new Affect(Patient.Target, -0.05, 0.3, 1, TargetIs.Chosen)),
```

- **Juiciness 0.5.** A story is told only when its score reaches `VolunteerLevel` (2). Juiciness 0.5 plus `KnowsBonus` 0.5 never reaches it, so light acts are seen and felt but never retold. Trivia volume rises only by the acts themselves, which are reported separately.
- **`Feelings()`:** `new() { Start = Tensions(), PlasticScale = 2, Desire = true, ... }`, with each graft switch set as its slice decides (section 1). Gifts stay free of money (question 9).

---

## 3. Notation

- `h` is the holder (actor) and `s` the subject, both person indices in ordinal name order.
- `m` is the minute, `d = Clock.Day(m)`, and `D = Clock.MinutesPerDay`.
- `St(h,s)` is 0c's steering regard, read only when steering.
- `fam = _fam[h,s]`.
- `b = CharacterOf(h).Boldness`, `U(h)` = understanding, `Sens(h) = 0.5 + sensitivity`.
- `P(h) = PowerOf(h)`, `mood(h) = MoodOf(h)`.
- `σ(h)` is `_stance[h]` when `StanceOn`, otherwise 0.
- `Heavy(k)`: a hostile Chosen kind that is not light (Argued).
- `Light(k)`: Snubbed or TurnedAway.
- `InReach(p, o, m)`: `o` is Free, in the same place, within `NearTiles` (Chebyshev), and in line of sight greater than 0. This is the reach part of `Candidates`, factored out.

---

## 4. Rules (each is a no-op unless `Desiring`)

### R0. Character (slice 0d.0)

Every trait read in the library goes through `CharacterOf(i)` (section 6 lists the 14 sites). No 0d rule writes `_character`. `SetTrait` exists for tests and the runner.

### R1. Where motives come from: `StirFrom(who, b, prior, m)`

This runs at the end of `Add` (`Simulation.cs:958`), after `Answered`. It reads only `who`'s own new belief `b`, their prior belief, their own regard, their own felt record, and `Did(who, ...)`.

1. **Preconditions.** Let `act = _acts[b.ActId]` and `row = KindOf(act).Affect`. Stop unless all of these hold:
   - `row` exists and `row.Patient == Patient.Target`;
   - `b.Target == who` (the holder believes it was done to them);
   - `b.Actor` names `s ≠ who`;
   - `prior?.Actor != s`, so each act stirs once per named actor.
2. **Seen, not heard (mod D34).** Stop if `b.Source != Source.Witnessed`. The one exception is step 7 (rule 9: the keeper's own shop).
3. **Families cover.** Stop if `Close(h, s)`.
4. **A changed cause.** If `prior` named another actor `o`, remove `o`'s motives whose `Source == act.Id` and write a `LifeEvent` Dropped.
5. **Felt.** `felt = |_felt[(h, act.Id)].Mood|` if that record exists, else `|row.Joy| x Sens(h)`. Stop if `felt <= 0`.
6. **Hostile Chosen row** (Argued, Snubbed, TurnedAway):
   - If the kind is heavy, call `Hit(h, s, act.Tick)`.
   - **Stance:** `σ(h) = StanceAfterHurt(σ(h), felt, b)` (R10).
   - **Gave cause.** If `act.About >= 0`, `Did(who, act.About)` and that act is a scandal: write a `LifeEvent` GaveCause, log `gave-cause`, and stop (III P40 schol., VERIFY).
   - **No light ping-pong.** If the kind is light, `act.About >= 0` and `Did(who, act.About)`, stop: a light answer to one's own hostility stirs nothing. It is still felt in mood and regard.
   - **MakeUp.** If `St(h, s) >= LoveAt` and `MakeUpOn`: stir **MakeUp** (friendly; act GaveGift).
   - **Answer.** Otherwise, if `AnswerOn`: stir **Answer** (hostile; acts Argued, Snubbed).
7. **Keeper scandal row** (Stole, RummagedInBin: `row.Target == Keeper`, `IsScandal`, Joy below 0), with `RetaliateOn`: stir **Retaliate** (hostile; acts Argued, Snubbed). Step 2's source test is skipped here, because rule 9 allows hearsay about one's own trade.
8. **Friendly Chosen row** (GaveGift, HelpedSomeone), with `ReturnOn`:
   - If `act.About >= 0` and `Did(who, act.About)`, stop: a return of one's own kindness is not returned.
   - Otherwise stir **Return**, with `Act = act.Kind`.
   - **Stance:** `σ(h) = StanceAfterKindness(σ(h), felt)`.
9. **`StirAccused(s, namer, actId, felt, m)`** is called in `Accused` (`Simulation.Feelings.cs:499`), in the loop that moves an innocent's regard for each namer, with `felt = |f| / namers`. Unless `Close`, it stirs **Retaliate** toward the namer and applies the stance hurt rule.

**`Stir(h, s, kind, hostile, act, source, felt, m)`:**
- A new key `(h, s, kind)` gets `Felt = min(1, felt)` and `Since = m`.
- An existing key gets `Felt = min(1, EventPart + felt)`, then `Since = m`, `Source = source` and `Act = act`. `AskedAt` and `Asks` are kept, so a rise of 0.1 or more reopens the question (R6).
- Log `"{m} stirred {h} {kind} {s} act {source} felt {felt:0.00}"` and add a `Stirring`.

### R2. Intensity

```
EventPart = DesireMath.EventPart(Felt, Since, m)          // linear to 0 over MotiveDays
I = 0                                    if EventPart <= 0
I = min(1, EventPart + max(0, -St(h,s))) for hostile motives (the grudge counts)
I = min(1, EventPart)                    for friendly motives (positive regard adds nothing: D24)
```

A motive whose `EventPart <= 0` lapses at the next `Pursue`. It is removed, logged `lapsed`, and written as a `LifeEvent` Lapsed with severity `Felt`.

### R3. Fond (`FondOn`; law 4, "love wants to be near"): computed when read, never stored

For a holder `h` who is Free, and each `s` in reach with `!Close(h, s)`:

```
daysApart = d - max(_lastContactDay[(h,s)], _lastKindDay[(h,s)])
I_fond    = DesireMath.Fond(St(h,s), daysApart)     // max(0, St - LoveAt) x min(1, daysApart / FondDays)
```

Fond is a friendly motive, act GaveGift, source -1 ("regard"), kind `DesireKind.Fond`. It is weighed with the stored motives (R5).

It is satisfied by a contact day or by the holder's own kindness. Each sets its day, so the motive falls to 0. Only love above `LoveAt` counts. A pair who meet most days hardly feels Fond at all, and that is the bound that faithful's raw regard x absence lacked.

### R4. Pity (`PityOn`; III P27 cor. 3, VERIFY)

In `FinishActs`, when an act ends whose row is `Patient.Actor` with Joy below 0 and Freedom 0 (Stumbled, Collapsed), for each witness `h` (in name order) whose belief names the actor `x ≠ h`, at clarity c > 0, with `!Close(h, x)`:
- stir **Pity** toward `x`, act HelpedSomeone;
- `felt = DesireMath.Pity(row.Joy, Sens(h), St(h,x), c)`;
- the motive's `EventPart` uses `PityMinutes` instead of `MotiveDays`.

Because families cover, pity never fires at home. That fixes mod-lessons' "kindness loops at home" risk.

### R5. The gate: `Pursue(m)`, every tick

**Order.**
- Holders in index order.
- For each holder, the candidate motives are their stored motives plus Fond (R3), ordered by `I` descending, then subject index, then `DesireKind`.
- At most one act per holder per tick.
- The holder is skipped unless `Free`.

**A motive is skipped** (try the next) unless all of these hold:
- **Reach:** `InReach(p, o, m)`.
- **Love covers** (Q9, rule 17): a hostile motive needs `St(h,s) < CoverAt`; a friendly one needs `St(h,s) > -LoveAt`.
- **Not avoiding:** a friendly motive is skipped while `h` avoids `s`.
- **Slots:** `_slotsUsed[(h,s,d)] < SlotsPerDay`.

**Effective boldness:**

```
eff = DesireMath.Effective(b, σ(h), hostile, fam, I, P(h))
```

**Cost** for act k, where fear applies only to hostile acts:

```
fear = FearPerHit x Hits(h, s within FearDays) + FearPerKin x |{q in household(s), q not in household(h), q ≠ s, InReach-able to h now}|
cost = DesireMath.Cost(form(k), hostile, fear)
```

The forms are Gift 0.4, Help 0.5, Argue 0.5 and Snub 0.2. So the costs are 0.4, 0.5, 0.8 + fear and 0.5 + fear.

**`ActsFor(motive)`**, most expensive first:

| Motive | Acts |
|---|---|
| Answer, Retaliate | Argued; then Snubbed (if `LightActsOn`) |
| MakeUp, Fond | GaveGift |
| Return of a help | HelpedSomeone; then GaveGift |
| Return of a gift | GaveGift |
| Pity | HelpedSomeone |

A kind is dropped from the list if any of these holds:
- `!FitsAge(age)`;
- the place is not in its `Allowed` list (when it has one);
- it is heavy and `m - _lastHostile[(h,s)] < HostileCooldownDays x D`;
- it is light and `_lightToday[(h,d)] >= LightPerDay`.

**Act lists are not consulted.** Anyone hurt can answer and anyone helped can return (requirements 2 and 3). Age still applies: help is for 10 and over, arguing for 13 and over.

**For each k, in order:**
1. If `I < Min(k)`, skip it.
2. `margin = eff - cost` and `call = DesireMath.Call(margin)`.
3. If the call is clear and nothing is chosen yet, choose k and stop.
4. If the call is no, or a close call already said no in this weighing, skip k. After a declined close call, only a clear yes on a cheaper act can be taken. This is **story 2**.
5. Otherwise it is a close call (R6). Yes: choose k and stop. No: mark `declined` and continue.

Each step adds a `Pursuit`. When `DesireActs` is false, the line goes to `_motiveLog`.

**If nothing is chosen:**
- If any close call was drawn this weighing, `_slotsUsed++`. A declined question uses an attempt.
- **Avoidance** (R9) is checked for hostile motives.
- Otherwise the motive waits.

**If something is chosen:**
- `_slotsUsed++`;
- remove the stored motive (Fond is not stored: its days are reset at R7);
- `Begin(m, k, p, injected: false, target: name(s), about: Source >= 0 ? Source : -1)`;
- add the act id to `_pursuedActs`;
- log `"{m} desire {h} {Motive} {s} act {Source|regard}: {k} intensity {I:0.00} eff {eff:0.00} cost {cost:0.00} {clear|close yes p}"`.

### R6. Close calls

```
p = DesireMath.Tilted(DesireMath.CloseCallChance(margin), mood(h), hostile)   // logistic(8m), tilted by mood
```

- **The answer stands.** If `!IsNaN(AskedAt)` and `|I - AskedAt| < AskAgainStep`, the earlier answer stands (call "stands"). It counts as a no for this weighing, and no slot is used.
- **Otherwise:**
  - `u = Rng.Unit(seed, "desire", h, s, Motive, Source, Asks, k.Name)`;
  - set `AskedAt = I` and `Asks++`;
  - yes if `u < p`, unless `CloseCall` returns a non-null answer.
- **Fond** stores no `AskedAt`. Its close calls are keyed `(h, s, "Fond", day, k.Name)` and asked once per day.
- The draw is keyed to the motive's state, not the minute. Waiting in reach cannot turn a 30% call into a certainty.

### R7. Acting: `Begin` additions (when `Desiring`, after `_did.Add`)

- **Heavy hostile Chosen act with a target:** `_lastHostile[(actor, target)] = m`. This covers rate-drawn and gate arguments, and rule 9's confrontations (R11).
- **Light act:** `_lightToday[(actor, d)]++`.
- **Kind Chosen act:** `_lastKindDay[(actor, target)] = d`.
- **`LifeEvent` Did** for the actor, written for **every** aimed act, rate-drawn or gated: Chosen, Kin, Keeper or Given, with a person target. Severity is `|row.Joy|`.
- **Outcome resolution** (R12).

S10's `why` line already prints `act {about}`, so a gate act cites its source.

### R8. The third slight (Q6; rule 4) (`LightActsOn`)

In `FinishActs`, after the target has felt a light act of kind k from `a`:
- Count the target's own witnessed beliefs of kind k with actor `a` and target self, begun within `MarkDays`, including this one.
- If the count is `>= MarkCount`, call `Move(t, a, -MarkSize, act.Id, "Mark", "Event", byThem: true, m)`. 0c's saturation, sentiment and log apply.
- `_marks++`.

The cooldown covers only heavy acts, and slots and the light cap pace light acts. So the third slight can happen for light acts, and cannot happen for arguments inside the cooldown. This settles Q6 as faithful's G11 did.

### R9. Avoidance, turning away, withdrawal (the shy's answers; Sid's direction 1)

**Avoidance starts** when a hostile motive gets no act. The trigger depends on `LightActsOn`:
- **off (base, measured):** every act in `ActsFor` is missing or has the call "no", and no close call was drawn;
- **on:** no act was taken, and the best margin among the hostile acts available is **below 0**. A declined close call on the negative side now counts. Judge 3's fix: the moderately shy withdraw instead of waiting.

**Then:**
- remove the motive;
- set `_avoid[(h,s)] = m + AvoidDays x D` (re-avoiding extends it);
- log `"{m} avoid {h} {s} act {source} margin {best:+0.00}"`;
- write a `LifeEvent` Avoided with severity I;
- if `LightActsOn` and the light cap allows, `h` turns away now (below).

**While avoiding:**
- no friendly gate act, Fond or Pity goes to `s`;
- no rate-drawn act goes to `s` (R11);
- `s`'s new hurts still stir motives. A cornered avoider whose margin later clears can answer.

**Turning away** (`LightActsOn`): this is `Begin(m, TurnedAway, p, target: s, about: source)`.
- It happens at most once per `(h, s, day)` (`_turnedToday`) and is subject to `LightPerDay`.
- It asks no gate question: turning away needs no daring.
- It is visible and felt (Joy -0.05), and never retold.

**`Withdraw(m)`** runs each tick after `Pursue`, with `AvoidOn && WithdrawOn`. For each holder with an active avoid, in key order, the holder withdraws if all of these hold:
- they are `Free`;
- `p.Why == "haunt"`;
- they have a home place;
- an avoided `s` is `InReach`;
- **(with `LightActsOn`) they could not answer now:**

  ```
  DesireMath.Effective(b, σ, true, fam, max(0, -St(h,s)), P) - (ArgueForm + HostileSurcharge + fear) < -ClearBand
  ```

  The bold who avoid stay. The shy, and the frightened, go.

**On withdrawing:**
- turn away first (if not done today and the cap allows);
- `Goal(p, home, DefaultTown.Sofa, p.GoalUntil, "home", null)`;
- `_withdrawals++`;
- log `"{m} withdraws {name} home from {s}"`;
- write a `LifeEvent` Withdrew.

**`Decide`** (`Simulation.cs:585-586`): the home option's weight becomes

```
1.0 + (Desiring && WithdrawOn ? AvoidCount(p) + StanceHome x max(0, -σ(p)) : 0)
```

`1.0 + 0` is bit-identical to `1.0`, so Off and observe keep their hashes.

### R10. Stance (`StanceOn`; a lasting state, Sid's deepening loop)

- **A hurt stir** (R1 steps 6, 7 and 9) of felt `f`: `σ = clamp(σ + f x (2b - 1), -1, 1)`.
  - The extremes move fastest: boldness 0.98 moves by +0.96f and 0.02 by -0.96f.
  - Boldness 0.5 does not move.
- **A kindness stir** (Return, step 8): `σ = σ - min(1, f) x σ`. It moves toward 0 and never across it.
- **At night:** `σ x StanceKeepPerDay`. Record `Stances`.
- **What it does:**
  - combative stance raises hostile daring only;
  - withdrawn stance lowers all daring and raises the home weight (R9).

**The loop it closes.** A shy person who is hurt grows more withdrawn. They stay home more, so they meet fewer people. They get fewer kindnesses, so less pull back. They are less willing to act.

A bold person who is hurt grows more combative and answers more. Fear per hit (R5) is the brake, and a pair of brawlers can still feud for a season.

Stance is not a trait. It is never written into `_character`, and **0e's plasticity rule reads `LifeEvents`, never stance** (section 13), so the two cannot count the same events twice.

Stance is computed only when `StanceOn`, so with it off `σ` stays 0 and nothing reads it.

### R11. Rate-drawn acts: kept as occasions, with exclusions (`Candidates`, `Simulation.Feelings.cs:691`, when `Desiring`)

- `o` is not a candidate target of actor `a` if `a` avoids `o`, or if the kind is heavy hostile and `(a, o)` is inside the cooldown.
- **Rates.** Every PerDay is unchanged, apart from `AimedRateScale` (1.0) on the three aimed kinds.
  - GaveGift, HelpedSomeone and Argued remain the town's everyday kindness and friction, and the sparks the gate answers.
  - Squabbled, DrunkScene and Stumbled stay as they are.
  - Gossip is unchanged.
  - S9's shop choice is unchanged: it is already held 28 days with a 0.3 margin.
- **Rule 9's confrontation** (`CheckScandals`, `Simulation.cs:1082`) keeps its own path. It now also stamps `_lastHostile[(confronter, culprit)] = m`, so invariant D35.3 covers confrontations.

### R12. Outcomes (the record for 0e; never read by a 0d rule)

**When an aimed act `x` by `j` toward `i` begins** (R7), resolve the latest **Open** `LifeEvent` in each of two places, if it began within `OutcomeDays`:

| `i`'s Did toward `j` (or `j`'s Undergone from `i`) | `x` | Outcome |
|---|---|---|
| hostile | hostile (heavy or light) | Answered |
| kind | kind | Returned |
| kind | hostile | Rebuffed |
| hostile | TurnedAway | Avoided |

- **From the actor's side:** `i`'s Did toward `j`.
- **From the receiver's side:** `j`'s Undergone from `i`. This records how `j` responded.

**When `i` starts avoiding `j`:** `i`'s Open Undergone events from `j` become Avoided, and so does `j`'s Open Did toward `i`.

**At night (`CloseDesires`):**
- Open events older than `OutcomeDays` become **Ignored**, or **None** when the act was light. Light acts are never counted as ignored.
- Roles other than Did and Undergone are written with Outcome None.

**Undergone events** are written in `Feel` (Direct route, patient = self), `Undergo`, `Accused` and `Shamed`. Each carries the act id, the other person (the believed cause) and `|felt mood|`. If no cause is named, Other is `"someone"`.

### R13. The first greeting of the day (`ToneOn`; section 6; the d27 option)

In `Socialise`, right after `Company(pa, pb)`, run for each direction (i, j) once per day (`_toned`):

```
(pw, pc) = DesireMath.Tone(P(i), U(i), St(i,j))
u = Rng.Unit(seed, "tone", name_i, name_j, d)
warm if u < pw;   curt if u >= 1 - pc;   else neutral
```

**On warm or curt:**
- `f = ±ToneJoy x Sens(i)`, and `AddMood(i, f)`;
- `Move(i, j, f x 0.3 x PlasticScale x Keep(f, Ret(i)), key -2 - m, "Tone", "Event", byThem: true, m)`;
- a curt greeting stirs **Answer** (felt `|f|`, source -1) and the stance hurt rule;
- a warm one stirs nothing;
- log `"{m} tone {curt|warm} {i} {j} because power {P:0.00} understanding {U:0.00} regard {St:+0.00}"`.

**What it reads.** It reads only i's own power, understanding and regard. It is how i takes the meeting, section 5's MoodRoll, and the log line names that cause. It does not read anything j did.

**Fixing `Metrics.cs:265`.** Before shipping `ToneOn`, guard `f.ActId >= 0`, because Tone moves carry no act. This is only needed when the switch ships.

### R14. Behaviour at trait extremes (Sid's direction)

**Toward a stranger** (fam 0.25), first argument, I = 0.45 (felt 0.3 + grudge 0.15), P 0.5, mood 0, no fear. `eff = b + 0.35 + StanceWeight x σ`. The stance column shows σ after the stir (0.3 x (2b - 1)) at StanceWeight 0.5.

| Boldness | eff (no stance / stance) | Argued (cost 0.8) | Snub (cost 0.5) | Outcome |
|---|---|---|---|---|
| 0.02 | 0.37 / 0.226 | -0.43 / -0.57 no | -0.13 close / -0.27 no | Stance on: avoid, turn away, withdraw. Off: a 26% snub, else avoid. |
| 0.30 | 0.65 / 0.59 | -0.15 close (p 0.23) / -0.21 no | +0.15 close / +0.09 close | Snub likely; if declined below 0, avoid |
| 0.50 | 0.85 / 0.85 | +0.05 close (p 0.60) | +0.35 clear | Argue 60%, else snub (story 2) |
| 0.70 | 1.05 / 1.11 | +0.25 / +0.31 clear | | Argues |
| 0.98 | 1.33 / 1.474 | +0.53 / +0.67 clear | | Argues every 3 days until fear: 4 hits in 28 days (5-6 with stance) bring it into the band |

**A gift returned** (I = 0.2 x Sens):
- clear for b + 0.5 fam + 0.1 > 0.55;
- at boldness 0.02 it is a clear no toward a stranger (-0.155) and a close call toward someone known (fam 0.6: +0.02).

The very shy return kindness only to people they know.

No row needs a clamp. Every outcome changes monotonically with boldness.

---

## 5. Run-loop order (each minute)

1. `Live` for everyone.
2. On ticks: `StartActs` (rates; S1 with the R11 exclusions), then `Temptation` and `Drinks` (inside `if (HasMoney)`), then **`Pursue`**, then **`Withdraw`**. `Pursue` and `Withdraw` sit inside `if (tick)`, after the money block, where the prototype measured them.
3. `StartScheduled`, `Watch`, `FinishActs`:
   - beliefs are added here, which reaches **`StirFrom`**;
   - **Pity** stirs and the **mark** happen at the end of each act.
4. On ticks:
   - `Socialise`: `Company`, then **`Tone`**, then `TryTell` (tellings reach `Add`, but R1 step 2 stops hearsay stirs);
   - `CheckScandals`: `Accused`, then **`StirAccused`**; confrontations stamp the cooldown;
   - then the rest as before.
5. `CheckLate`.
6. At 23:59, `CloseDay` runs **`CloseDesires`** before `CloseFeelings`. `CloseFeelings` clears `_together`. `CloseDesires`:
   - sets `_lastContactDay` from `_together >= ContactMinutes`;
   - decays stance and records it;
   - resolves outcomes past their window;
   - prunes `_slotsUsed`, `_lightToday`, `_turnedToday` and `_toned` for past days, and `_hits` older than `FearDays`.

A motive stirred at tick t is first weighed at tick t + 5. The rate draws of a tick come first, so `Pursue` sees the actors they made busy.

---

## 6. Hook sites (verified at 1d7d124)

| File, method (line) | Change | Slice |
|---|---|---|
| `Model.cs` (end) | `Trait`, `DesireKind`, `Pursuit`, `Stirring`, `Outcome`, `LifeRole`, `LifeEvent` | 0d.0-0d.1 |
| `Feelings.cs` `FeelingOptions` (after `FriendAt`, l.119) | The options of 2.2; `WithDesire` | 0d.1 |
| `Feelings.cs` | `DesireMath` | 0d.1 |
| `Simulation.cs` ctor (l.220) | `_character` from `_cast[i].Temperament`; `_stance` | 0d.0 |
| `Simulation.Feelings.cs:120-123` (`Sens`, `U`, `SR`, `Ret`), `:171-172` (chattiness in `Company`) | `CharacterOf` | 0d.0 |
| `Simulation.cs:912` (`Perception.GuessesConfidently(o.Temperament)`), `:998` (chattiness), `:1104`, `:1108` (boldness in `CheckScandals`) | `CharacterOf` | 0d.0 |
| `Simulation.Money.cs:295`, `:305` | `CharacterOf` | 0d.0 |
| `Simulation.Authority.cs:65`, `:91`, `:163-164`, `:245` | `CharacterOf` | 0d.0 |
| `Simulation.cs` `Step` (l.396-410) | `Pursue(m); Withdraw(m);` after the `HasMoney` block, inside `if (tick)` | 0d.2 |
| `Simulation.cs` `Decide` (l.585-586) | Home weight (R9) | 0d.2 |
| `Simulation.cs` `StartActs` (l.750-755) | `PerDay x AimedRateScale` for the three aimed kinds when `Desiring` | 0d.2 |
| `Simulation.cs` `Begin` (l.817) | R7: stamps, Did, outcomes | 0d.1-0d.2 |
| `Simulation.cs` `FinishActs` (l.882) | Pity (R4), mark (R8) | 0d.3-0d.4 |
| `Simulation.cs` `Add` (l.958, after `Answered` at l.968) | `StirFrom(who, b, prior, m)` | 0d.1 |
| `Simulation.cs` `Socialise` (l.973) | `Tone` after `Company` | 0d.5 |
| `Simulation.cs` `CheckScandals` (l.1082) | Confrontation cooldown stamp | 0d.2 |
| `Simulation.cs` `CloseDay` (l.1122) | `CloseDesires(day)` before `CloseFeelings` | 0d.1 |
| `Simulation.Feelings.cs` `Feel` (l.197) | `LifeEvent` Undergone for a Direct, self-patient entry | 0d.1 |
| `Simulation.Feelings.cs` `Undergo` (l.479) | `LifeEvent` Undergone | 0d.1 |
| `Simulation.Feelings.cs` `Accused` (l.499) | `StirAccused`; `LifeEvent` Undergone | 0d.1 |
| `Simulation.Feelings.cs` `Candidates` (l.691) | Extract `InReach`; R11 exclusions | 0d.2 |
| `Simulation.cs` `Run` / `SimResult` (l.296) | Appended fields | 0d.0-0d.1 |
| `DefaultTown.cs` `Acts()`, `Feelings()` (l.166) | Snubbed, TurnedAway; switches | 0d.2-0d.5 |
| `Metrics.cs` | `DesireMetrics`, fringe line; l.265 guard with `ToneOn` | 0d.2-0d.5 |
| `UnderGlass.Run/Program.cs` | Flags (section 10) | 0d.2 |

**`About` reuse is safe.** It was checked at 1d7d124: `About` is read only in `Answered` (`Simulation.Feelings.cs:598-603`), which acts only on Warned, TakenIn, Service, FamilyRow and Questioned, and in `Undergo`'s blame (l.489), which is reached only for `Patient.Actor` rows. Gate acts are `Patient.Target`. Neither the runner nor `Metrics.cs` reads `About`.

---

## 7. Determinism, RNG and switches

**New streams:**
- `"desire"`: (holder, subject, motive kind, source, asks, act kind); for Fond, (holder, subject, "Fond", day, act kind);
- `"tone"`: (i, j, day).

Neither is in the brief's list of names in use. Skipping a draw shifts no other stream, because `Rng.Unit` is a stateless hash.

**Order:**
- holders by index;
- motives by (-I, subject, kind);
- `_desires` and `_avoid` are `SortedDictionary`;
- `Withdraw` groups by holder in key order;
- witnesses for Pity in name order.

Dictionaries are looked up and never iterated for a decision.

**Arithmetic:** no `Exp`, `Pow`, `Tanh` or `Log`. The logistic is the Padé form. The 0.975 stance decay is one multiply a night.

**Off** (`Enabled = false`): `Steering` and `Desiring` are false, and no gate code runs. `_character` holds the cast's values. Snubbed and TurnedAway have PerDay 0 and are tested before any draw. The home weight is `1.0`. **T2 holds:** `e7f6653087ff7e18`, `388d128d7fd4cdf2`.

**Observe** (`Steer = false`): `Desiring` is false, so **T12 holds**. A new T12b checks the same with `Desire = true`.

**`Desire = false`, steering on:** exactly 0c. **P1:** seed 1 x 112 days, `new Simulation(1, feelings: DefaultTown.Feelings() with Desire = false)`, recorded at 0d.0 before any code. The brief measured `c0488cc6bf81e64f` for the same run at 05dd781, so expect that value.

**`DesireActs = false`:** motives only. Lines go to `_motiveLog`, `LifeEvents` and `Pursuits` are filled, and nothing else changes, so **P2 = P1**. `LifeEvent` writing in `Begin` and `Feel` only appends to lists. Stance is mutated but read only by `Pursue` and `Decide`, and both are inactive when `DesireActs` is false.

**Log lines kept intact** for tools: `" sentiment "` (field [^4]), `" paid "`, `" kept-in-family "`, `"; vouched for "`.

**New log lines** while acting: `stirred`, `desire`, `lapsed`, `gave-cause`, `avoid`, `withdraws`, `mark`, `tone`.

**Run time.** The prototype measured 94 s per 30 seed-years, against 0c's about 60. Fond is evaluated only for holders who are Free, with someone in reach, and a regard above `LoveAt` toward them: a short list.

---

## 8. Tests

**New files:**
- `DesireMathTests.cs` (pure);
- `DesireSceneTests.cs`;
- `CharacterTests.cs`;
- `DesireInvariantTests.cs`.

**The scene convention is SteeringTests':**
- `V(...)` with sensitivity and retention 0.5, so `Sens` = 1;
- one `Room()` (40 x 6), `wander: 0`, `body: Awake()`;
- the scenes' own rows (`GiftRow`, `ArguedRow`, and a `HelpRow = new(Patient.Target, 0.3, 0.3, 1, TargetIs.Chosen, Tilt: 1)`);
- the light kinds copied from 2.6;
- scheduled acts at `Ten` on day 0;
- `FeelingOptions.WithDesire(...)` (PowerWeight 0, grafts off) unless stated;
- Ann at (3,2), Bob at (5,2), Cal at (5,4), each in their own household;
- familiarity 0.25 unless `Friends` is given.

### Pure (`DesireMathTests`, `CharacterTests`)

| # | Assertion |
|---|---|
| D1 | `CharacterOf` holds the cast's temperament for every person at the start and at the end of a default run (`CharactersAtStart == CharactersAtEnd`). `SetTrait("Ann", Boldness, 1.7)` stores 1.0. `TraitOf` round-trips all six traits. |
| D2 | Extremes. `Effective(0.02, 0, true, 0, 0, 0.5)` with Argued's cost gives margin -0.78; `Effective(0.98, ...)` gives +0.18. Stance -1 and +1 at both extremes give finite results, and eff is ordered by boldness at every fixed stance. Nothing throws. |
| D3 | `CloseCallChance` matches `1/(1+e^(-8m))` within 1e-5 for m from -0.15 to 0.15 in steps of 0.01 (the test may use `Math.Exp`). Exact values: 0.5 at 0; 0.598688 (±1e-6) at 0.05; 0.689975 at 0.1; 0.768531 at 0.15; 0.310025 at -0.1. It is odd about 0.5. |
| D4 | `Tilted(0.5, 1, false) = 0.6` and `Tilted(0.5, 1, true) = 0.4`. It is clamped to [0, 1]. |
| D5 | `Call(0.151) = "clear"`, `Call(0.15) = "close"`, `Call(-0.15) = "close"`, `Call(-0.151) = "no"`. |
| D6 | `Effective(0.5, 0, true, 0.25, 0.45, 0.5) = 0.85`. With power 0.6 and PowerWeight 1 it is 0.95. Friendly, with stance -0.4, fam 0.4 and I 0.2: 0.6. Hostile, with stance +0.4, fam 0.4 and I 0.2: 1.0. Friendly with stance +0.4 is unchanged by the stance (0.8). |
| D7 | `Cost(0.5, true, 0) = 0.8`; `Cost(0.2, true, 0.2) = 0.7`; `Cost(0.4, false, 0.2) = 0.4`. |
| D8 | `EventPart(0.3, m0, m0 + 3.5D) = 0.15` and at `m0 + 7D` it is 0. Intensity: a hostile motive at regard -0.2 is 0.5 at m0 and 0.35 at m0 + 3.5D. A friendly motive at regard -0.2 is 0.3 at m0. A re-stir of 0.3 at m0 + 3.5D gives Felt 0.45. |
| D9 | `Fond(0.5, 3) = 0.3 x 3/7 = 0.128571`; `Fond(0.5, 14) = 0.3`; `Fond(0.15, 30) = 0`. |
| D10 | `Pity(-0.1, 1.0, 0, 1) = 0.2`; at regard 0.6, 0.32; at clarity 0.5, half. |
| D11 | `StanceAfterHurt(0, 0.3, 0.2) = -0.18`; `(0, 0.3, 0.5) = 0`; `(0, 0.3, 0.98) = 0.288`; `(-0.9, 1, 0.02) = -1`. `StanceAfterKindness(-0.18, 0.2) = -0.144` and `(0.5, 2) = 0`. It never crosses 0. |
| D12 | `Tone(0.5, 0.5, 0)` gives warm 0.03 and curt 0.03. `Tone(0.2, 0.2, -0.5)` gives warm 0.006 and curt 0.1152. Every value lies in [0, 1] at the extremes of P, U and regard. |

### Scenes (`DesireSceneTests`)

| # | Name | Setup | Assertion |
|---|---|---|---|
| D13 | TheHurtAnswerBack | Ann (bold 0.5, `Argued` in her list) has one scheduled Argued. Bob (bold 0.9, **empty act list**). No rates. 4 days. | Exactly one Argued by Bob, target Ann, `About` = Ann's act, Tick ≤ Ten + 20. A `Pursuit` for Bob with call "clear" and I ≥ 0.3. No second Argued by Ann (she has no motive; her own act stirs nothing). |
| D14 | ThePairCoolsDown | As D13, but Ann is bold 0.9 and both have Argued at perDay 0. 10 days. | Heavy hostile acts per ordered pair are at least 3 D apart. Bob's answer to Ann comes within the first hour after her act (the cooldown is per ordered pair). |
| D15 | KindnessReturnedOnce | Ann has a scheduled GaveGift. Bob is bold 0.5 with no acts. | Exactly 2 GaveGift acts, the second by Bob to Ann with `About` = the first. No third gift in 7 days. `Stirred` has Bob Return and no Ann Return. The `LifeEvent` Did of Ann's gift resolves Returned; Bob's Undergone resolves Returned. |
| D16 | Story2_DeclinedHelpFallsBackToGift | Ann has a scheduled HelpedSomeone. Bob is bold 0.35 (eff 0.625 at I 0.3: help +0.125 close, gift +0.225 clear). `CloseCall = _ => false`. | Bob's act is a GaveGift to Ann with `About` = the help. Pursuits: Help close-no, then Gift clear. No HelpedSomeone by Bob. With `CloseCall = _ => true`, Bob helps. |
| D17 | FamiliesCover | As D13, with Bob as Ann's housemate. Then again with Bob as her kin through `family`. | No `Stirring` and no answer, in both. |
| D18 | LoveDoesNotConfront | As D13, with `Start[(Bob,Ann)]` at 0.6, then 0.3, then 0.1. | At 0.6 and 0.3: MakeUp, and Bob gives Ann a gift; no Argued. At 0.1: Answer. |
| D19 | TheQuestionWaits | As D13, with Bob bold 0.475, fam 0.25 and PowerWeight 0 (margin about +0.05, close). `CloseCall` counts its calls and answers false. Ann stays in reach 6 hours. Then Ann argues again on day 4. | `CloseCall` is called exactly once on day 0. On day 4 the Felt rises by ≥ 0.1, and it is called again. Slots per (Bob, Ann, day) ≤ 2. |
| D20 | TheShyAvoid_Story4 | Base trigger. Bob is bold 0.05, with Gift in his list at perDay 20. Cal is in reach. Ann argues once at Ten on day 0. 10 days. | No hostile act by Bob. `Avoids` has (Bob, Ann) until Ten + 7D. No gift Bob → Ann before then, while Bob → Cal gifts exist. At least one Bob → Ann gift after it. |
| D21 | TheShyTurnAway | `LightActsOn`. As D20, with `CloseCall = _ => false`. | A TurnedAway Bob → Ann at most 5 minutes after Ann's act ends. An `avoid` line. A `LifeEvent` Avoided for Bob. Ann's Did resolves Avoided. |
| D22 | OnlyTheShyLeave | `LightActsOn`. Bob has two haunts: Room (weight 1, all day) and a home place. Ann stays in Room. Run once with Bob at bold 0.05 and once at bold 0.45 (an avoid forced by `FearPerHit` 0.5 and two earlier scheduled arguments by Ann). | At 0.05: `Withdrawals` ≥ 1 on day 0, and fewer minutes in Room on days 1-6 than with `WithdrawOn = false`. At 0.45 the fear brings the Argued margin under -0.15, so Bob also leaves. With FearPerHit 0, Bob's margin is in the band: an avoid can still start, but no withdrawal. |
| D23 | TheThirdSlightMarks | `LightActsOn`, `AnswerOn = false`. Bob has scheduled Snubbed at Ten on days 0, 1 and 2, and Ann is bold 0. | Exactly one Felt with route "Mark" from Ann toward Bob, on day 2. `Marks = 1`. |
| D24 | NoLightPingPong | `LightActsOn`. Ann argues; Bob snubs back (forced by `CloseCall = _ => false` and bold 0.4). | Ann has no Answer stirred by Bob's snub. The run has exactly 2 hostile acts. |
| D25 | StanceDeepens | `StanceOn`. Bob is bold 0.2. Ann argues on days 0, 4 and 8. | `Stances["Bob"]` after day 8 < after day 0 < 0. Bob's home weight rises (checked through more home minutes than with `StanceOn = false`). A Return stir from a scheduled Cal gift on day 9 moves Bob's stance toward 0 without crossing it. |
| D26 | BrawlersFeud_FearCaps | Ann and Bob are both bold 0.98. One scheduled Argued by Ann. 21 days. FearPerHit 0.1. | A "feud" tie forms. Argued per direction ≤ 7 (the cooldown). With FearPerHit 0.3, at most 3 per direction. |
| D27 | CharacterIsReadNow | D13 run twice: as is, then with `SetTrait("Bob", Boldness, 0.02)` before `Run`. | The first argues back; the second avoids (no hostile act; an `avoid` line). |
| D28 | FondMissesThem | `FondOn`, `Start[(Ann,Bob)] = 0.5`. Bob is in reach of Ann 30 minutes a day and never has a contact day. Setup detail to settle at build: any arrangement of haunts that gives that. | Ann's first act toward Bob is a GaveGift, motive Fond, src regard, on day 3 (0.1286 ≥ GiftMin; day 2 gives 0.0857). Her next Fond gift is no sooner than day 6. Bob's return of it is not returned. |
| D29 | PityHelps | `PityOn`. Cal has a scheduled Stumbled in sight of Dee (bold 0.6, E 0). | Dee helps Cal within 60 minutes, motive Pity. With `PityOn = false`, she does not. With Dee as Cal's housemate, she does not (families cover). |
| D30 | ReattributionDrops | An act first believed to be Bob's is later reattributed to Cal (reuse F11's FeelingEventTests setup). | Ann's Answer toward Bob is removed with a `LifeEvent` Dropped; a new Answer toward Cal follows. |
| D31 | HearsayStirsNothing | Ann argues at Bob out of Dee's sight. Bob later tells Dee. | Dee has no motive. Then Ann robs Dee's shop out of sight, and Dee hears of it: Dee has a Retaliate (rule 9). |
| D32 | GaveCause | Bob steals at Ann's shop (scheduled). Ann argues at Bob with `About` = the theft (scheduled through `Begin`'s `about`). | `LifeEvent` GaveCause for Bob; no Answer stirred. |

### Pins and switches

| # | Test |
|---|---|
| D33 | T2 and T12 unchanged. T12b: `Desire = true, Steer = false` matches Off as T12 does. P1: `DefaultTown.Feelings()` with `Desire = false`, seed 1 x 112, recorded at 0d.0. P2: as P1 with `Desire = true, DesireActs = false`, equal to P1. P3: the shipped default, seed 1 x 112, pinned at 0d.5. |
| D34 | Each graft switch off reproduces the previous slice's pin. Pins at the end of 0d.2, 0d.3 and 0d.4 are recorded and kept. |

### Run-wide invariants (D35, `DefaultTown`, seeds 1-3, 56 days, shipped default)

1. Every act in `Pursued` has a target, a `Pursuit` with Acted true, and intensity ≥ its kind's minimum.
2. No gate act between `Close` pairs. No gate Argued or Snubbed by a holder whose `St` toward the target is ≥ 0.4 at that minute.
3. Heavy hostile acts per ordered pair are ≥ 3 days apart. This includes rate-drawn acts and confrontations.
4. ≤ 2 slots per (holder, subject, day). ≤ 2 light acts per holder per day. ≤ 1 TurnedAway per (holder, subject, day).
5. No friendly act, gated or rate-drawn, goes to someone the actor is avoiding.
6. Every gate act's target was `InReach` at its start.
7. Every `LifeEvent` of role Did or Undergone older than `OutcomeDays` at the end has an outcome other than Open. Light ones have None or a real outcome, never Ignored.
8. Stance is in [-1, 1].
9. Cash is conserved to 0.000 g.
10. Two runs give the same `LogHash`. The motive log is not in it.

---

## 9. Golden stories (pinned after 0d.4, default town)

- **G1, a feud.** Take the first seed in 1-50 whose ties hold a new non-kin feud. Assert the log chain: the first hostile act between the pair, then `stirred ... Answer`, then `desire ... Argued ... act N` (citing it), and at least one answer each way, then the tie.
- **G2, a friendship.** Take the first seed with a new non-seeded friendship across households. Assert that it starts with a rate-drawn or Fond kindness, and that `Return` acts follow each way.
- **G3, a hermit in the making** (`StanceOn`). Run the shipped default with `--trait Penny=Boldness:0.02`. Within a year, Penny has more Avoided and Withdrew events and fewer hub minutes in season 4 than in season 1, against the same seed at her authored boldness.
- **G4, a brawler** (`StanceOn`). Run with `--trait Alex=Boldness:0.98`. Alex's hostile Did count leads the town, and his stance at year end is > 0.3.

---

## 10. Metrics and runner

**`DesireMetrics`** (in `Metrics.cs`), per year:
- **Motives:** stirred, by kind; weighed, by kind and call; gate acts, by kind; rate-drawn Argued, gifts and help.
- **Answering:** answered in kind within 1, 3 and 7 days, and kindness returned within 7 days, across households (from `Acts` and `About`).
- **Withdrawal:** gave-cause; avoids and the top avoiders; withdrawals; turned-away acts; snubs; marks.
- **Arguments:** the top gate arguers; cross-household arguments; unordered pairs with 2+ and 3+ each way.
- **Spread:** per season end, ordered pairs below -0.2, split into across households and kin-or-home.
- **Outcomes:** the share of each, by role, across households.
- **Stance:** at each season end, p10, p50 and p90; hermits (σ ≤ -0.5) and brawlers (σ ≥ +0.5) per seed-year.
- **Concentration:** the share of feuds involving the town's most frequent feuder.
- **Fringe line:** the boldest and the shyest villager by `CharactersAtStart`, with their acts, Did and Undergone counts, avoids, withdrawals, season-4 hub minutes and end stance.

The existing E1 lines (`SeedsWithFeudAndFriendship`, `WarTowns`, `DeadTowns`, `BySnapshot`), news and trivia (with light acts counted separately), grievance and tempted scandals all stay.

**Runner flags:**
- `--desire off|observe|on` (`Desire` and `DesireActs`);
- `--fo Name=value` already reaches every option, and its `Set` helper already parses bool, int and double separately (`Program.cs:55`). Faithful's crash was in its probe console, not the repo;
- `--trait Name=Trait:value` calls `SetTrait` before the run;
- `--tensions X` seeds Q1's candidate pairs at -X both ways for Sid's experiment. Seeded pairs never count as new feuds.

The report prints a "desire" block like the prototype's.

---

## 11. Phase gate

| # | Check | Run | Pass | Base prototype (a5 unless noted; measured) |
|---|---|---|---|---|
| 1 | 0a band (sight and gossip) | 400 x 14 `--inject` | ≥ 52% in band, ≤ 28% over 70% | 57 / 24% (a2) |
| 1b | Authority | same | decided right ≥ 95%; reported 77-97% | 97%; 88% |
| 2 | 0b | 100 x 112 | cash 0.000 g; tempted scandals 1.5-4 a year; grievance ≤ 1 | 0.000; 2.4; 0.00 |
| 3 | E0 | tests | T2, T12, T12b, P1-P3, D35 | prototype passed T2 and T12 |
| 4 | E1 | 200 x 112 | ≥ 60% of seed-years with a new non-kin feud and a new friendship | 73% (30 sy); 65% without avoid (c1, 100 sy) |
| 4b | War and dead towns | 200 x 112 | each < 5% | 0 / 0% |
| 5 | Spread at season ends | 200 x 112 | below -0.2 in 1-5% of pairs; mean change -0.03 to +0.05; moved 0.1+ in 5-25% | d55-d111: 1.0-1.2%, +0.014 to +0.022, 9.4-12.5%. **d27: 0.3%** (section 12, step 9) |
| 6 | Lively parameter space | section 12, step 10 | ≥ 15% of cells pass 4, 4b and 5 (d55-d111) | 4 of 18 (22%) |
| 7 | Three years | 50 x 336 | 4b and 5 hold at each season end of year 3; moved 0.1+ at d335 within 1.5 x its d111 value | not run |
| 8 | Fringe (reported) | G3, G4, fringe line | at boldness 0.02: no Argued, Avoided and Withdrew above the town median; at 0.98: the most Argued | |
| 9 | Volumes (reported) | | news, trivia, light acts, gifts and help a year | news 205, trivia 621 (0c: 95, 478) |
| 10 | Time (reported) | | < 2 x 0c per seed-year | about 1.6 x |

---

## 12. Tuning order, with fallbacks

Each step runs 50 x 112, and confirms at 200 x 112 and 400 x 14 when it changes a default. Rerun the 0a band after every step that moves news.

1. **Base reproduction** (0d.2, grafts off, `PowerWeight 0`, `FearPerHit 0.1`, avoid on). It must match a5 within one SE: E1 73 ± 8%, feuds 6.7, friendships 0.93, d111 below -0.2 1.2%, news 205.
   - It differs from the prototype in two ways: the witness-only rule (D34), and slots counting declined attempts. Both are expected to move numbers by less than one SE.
   - If it does not match, bisect those two before anything else.
2. **Confirm at 200 x 112 and 400 x 14** (judges 2 and 3). If E1 is under 60% here, go straight to step 7 (Fond) before steps 3-6.
3. **`LightActsOn`.** Watch snubs and turned-aways a year, marks, war towns, d111 below -0.2, the band and E1.
   - Expected: below -0.2 rises, which helps the d27 floor, and avoids rise.
   - Fallbacks, in order: `SnubMin` to 0.2; then the base avoidance trigger with the light acts kept; then `LightActsOn` off, to Sid.
4. **`StanceOn`**, with `StanceWeight` 0.25, 0.5 and 1.
   - Watch war towns, the brawler and hermit counts, concentration and E1.
   - Keep the largest weight with war towns < 5% and 4b and 5 held.
   - Fallback: off, to Sid (question 8).
5. **`PowerWeight`** 0 against 1. Keep 1 unless a gate check moves by more than one SE.
6. **`FearPerHit`** 0.05-0.15, then **`FearPerKin`** 0-0.2. Use them for war towns and the brawler-pair concentration (Sam–Shane in the base).
7. **`FondOn`** with `FondDays` 4, 5, 7, 10 and 14.
   - Pass: E1 ≥ 60% with moved 0.1+ ≤ 25% and mean change ≤ +0.05.
   - **Cliff check:** friendships a year must not change more than threefold between adjacent settings. Faithful's cliff was 0.06 → 6.4 between 10 and 7 days.
   - Fallbacks: `PityOn` with `PityScale` 1-3 (watch help news and the band); then `GiftMin` 0.05; then a second return per kindness, to Sid (question 6).
8. **News.** If check 1 fails: Argued's juiciness to 2.5, then `AimedRateScale` 0.5. The base measured that 0.5 gives news 151 and the band 60/23%, but E1 67% and below -0.2 at 0.6-0.8%, so it is a last resort.
9. **d27.** Compare `ToneOn` (`Tone` 0.01-0.05) with `--tensions` 0.3. Keep a spark only if d27 reaches 1% with checks 1 and 4b held. Otherwise ask Sid to accept d27 as a warm-up (question 3).
10. **Parameter space** for check 6: `PlasticScale` {1.5, 2, 2.5} x `FearPerHit` {0.05, 0.1, 0.15} x `FondDays` {5, 7, 10} x `StanceWeight` {0, 0.5}. That is 54 cells x 20 seed-years. A cell is lively if it passes 4, 4b and 5 at d55-d111.
11. **E2.**
    - `Desire = false` against on.
    - Each of `AnswerOn`, `ReturnOn`, `MakeUpOn`, `RetaliateOn`, `AvoidOn`, `WithdrawOn`, `LightActsOn`, `StanceOn` and `FondOn` off.
    - Keep a rule only if removing it moves a story metric by ≥ 20%. Story metrics: feuds, friendships, reconciliations, answered and returned shares, hermits and brawlers, and avoids.
    - Stance and withdrawal serve Sid's direction 1. If E2 says drop them, that becomes a question for Sid, not an automatic cut.
12. **Three years** (check 7). If moved 0.1+ keeps climbing, lower `FondDays` before anything else.

---

## 13. Toward character over time (sketch, not built)

**What 0d leaves for 0e:**
- **The state.** `_character[i]`, read by every rule, plus `SetTrait`, and snapshots at the start and end.
- **The record.** `LifeEvents`, each with person, other, act, kind, role, severity, hostility, light/heavy and outcome, from both sides.
- **The fast state.** Stance, which 0e must not read (below).
- **Behaviour that already deepens a trait's expression without changing the trait.** Stance, avoidance and the home weight in 0d. Plasticity in 0e makes the change stick.

### 13.1 Plasticity (rule 18; `inheritance-research.md`, section 4)

Keep, per person and trait:
- a slow **settled** value `z` on a hidden scale;
- a fast **pressure** `q`, which decays.

The displayed trait, the one `_character` holds, is `t = S(z + q)`, where `S` is a deterministic sigmoid: the rational form `0.5 + 0.5x/(1 + |x|)`, or a table. There is no `Exp`. Extremes need no clamp.

**Each night**, for each person `i` and each of i's `LifeEvents` that resolved that day:

```
Δq[trait] += w(role, kind, outcome, trait) x severity x A(age) x V(i)
q        *= QKeep                                   // "changes stick while they keep being triggered"
z        += Settle x q                              // a little of the pressure becomes character
```

- **`w`** is a table, for Sid to tune. First guesses from rule 18:
  - Rebuffed or Ignored on one's own kindness lowers boldness and self-regard;
  - Returned raises self-regard;
  - Answered after one's own hostile act lowers boldness for the shy and raises it for the bold;
  - Avoided or Withdrew lowers chattiness and boldness;
  - an Undergone hostile act with outcome Ignored (swallowed) raises sensitivity and retention;
  - a reconciliation raises understanding.
- **Severity.** Rule 4's 0.7 marks a severe event. Its weight can be superlinear, for example `severity x (1 + severity)`, so a single severe event moves a trait as much as several small ones.
- **`A(age)`** is the hardening curve (Roberts & DelVecchio 2000, VERIFY): 1.0 for a child, 0.55 at 20, 0.40 at 30, 0.28 from 50, with a **floor of 0.25** ("never fully").
- **`V(i) = 0.7 + 0.6 x sensitivity`** is differential susceptibility (Belsky & Pluess, VERIFY).
- **Drift toward company** (Sid): `z += Converge x A(age) x Σ_j minutes_together(i,j) x (z_j - z_i)`, with weights from time and perhaps regard. Keep it subtle.

**Stance and plasticity count different things.** Stance is the fast expression of the same events: it decays in four weeks and never writes `_character`. Plasticity reads only `LifeEvents`. A person who stays withdrawn keeps producing Avoided and Withdrew events, and those, not the stance, move their boldness.

### 13.2 Inheritance with mutation (Model 1 of the research)

- **A hidden genetic value per trait.** Each person has `g`. Founders are back-filled from their authored values: `g = VA x z + noise`, seeded by name.
- **A child at birth:**
  - `g_child = (g_mother + g_father)/2 + N(0, sqrt(VA/2))` (segregation);
  - `z_child = g_child + d + e`, with the non-additive part `d` and the birth environment `e` drawn fresh;
  - `q = 0`.
- **Mutation:** with probability `p ≈ 0.015 x n/60`, where `n = 25 + 2 x (father's age - 20)`, add `N(0, 1)` to `g`. That is about 1 child in 11.
- **Normal draws** come from a named `Rng.Unit` stream through Irwin-Hall (the sum of 12 uniforms minus 6) or an inverse-CDF table. Box-Muller is not used, because it needs `Log` and `Cos`.
- **Plasticity never touches `g`.** A parent who became a hermit passes on their nature, not their withdrawal. What they pass on is a lonely household.
- **Kin ties as a head start.** A child's seed regard for the people its parents love or hate is shifted toward theirs (Sid: "more easily, not certainly"). This is a seed value, not a rule.

### 13.3 What has to exist first

- The life course: couples, births, children growing up and leaving home, deaths.
- Time skips of 5-10 years.
- Familiarity decay (rule 5's 1% a day), or a town of strangers grown familiar will never quarrel.

### 13.4 Questions for Sid that 0e raises

1. The `w` table: which outcome moves which trait, and by how much? Should Sid hand-write it, or should the experiments find it?
2. Should "answered after one's own hostile act" move the bold up and the shy down (winning and losing)? The sim does not judge who won.
3. Should `q` decay with a single `QKeep` for every trait? Research suggests retention is the most event-driven and the least heritable.
4. The hidden scale and its sigmoid: are traits allowed to reach 0.99?
5. Should drift toward company weigh regard (friends shape us more than housemates we dislike)?
6. Should siblings share a part of `d` (a quarter, as in the research)?
7. Should 0e also give the shy a "seek people out less" rule in `Decide` beyond stance's home weight, or is the home weight enough?

---

## 14. Deferred, with reasons

- **Law 7 and rule 7** (reactions, siding, norms) and **Indignant motives** for onlookers: they multiply hostility and change which stories are told, and so the band. They belong with rule 7.
- **Rule 10 replacing the aimed rates entirely:** kept as occasions until the gate is measured. `AimedRateScale` is the dial (question 1).
- **Rule 9's confrontation and rule 10's life choices through the formula:** confrontation keeps its path, plus the cooldown stamp. S9 keeps its hold time and margin.
- **Walking up, visits, and planning days around motives** (rule 11): these change who meets, and the reach measurement says it is not needed.
- **Familiarity decay** (rule 5): it moves the band. 0e needs it.
- **The mod's "an ignored attempt adds or takes 0.1":** the 0e plasticity rule reads `Ignored` outcomes instead.
- **Venting by halves:** see Appendix A.
- **The player and the first greeting toward them:** rule 13's tone is the town-only form.
- **Laya:** only the `CloseCall` seam and the motive order (`OrderMotives`, private) exist.
- **Gifts costing money** (question 9).

---

## 15. Risks

1. **Friendship is the binding half of E1.** The base gives 65-73% with friendships about 0.9 a year. Fond is unmeasured; its cliff check is step 7, and Pity is the measured-next fallback.
2. **The first season end** misses 1% below -0.2 in every base cell (best 0.6%). Light acts and a spark may fix it. Otherwise it is a waiver for Sid.
3. **News roughly doubles** (95 → 205). The band holds at 57% and 24%, with 4 points under the 28% ceiling. Light acts are never retold, so they add no news.
4. **Brawler concentration.** Sam–Shane gave 18 of 201 base feuds, and they oscillate between feud and friendship. Stance can deepen it. Fear per hit, fear of kin and the concentration metric watch it.
5. **Stance tips the town toward war** if the bold cluster (Alex, Abigail, Haley, Pam, Sam) hardens together. Its own switch, step 4's limit, and E2.
6. **Withdrawal moves who meets**, and so the band. Withdrawals were 37 a year in the base. Stance and the split change that number, so recheck the band at each step.
7. **Snub volume.** Faithful measured about 387 snubs a year in a town with no rate-drawn arguments. Here snubs answer hurts, so expect fewer. They are trivia acts, reported, never retold.
8. **Fear from one's own beliefs can make an innocent feared** after a misattribution. Participants see clearly, so this needs reattribution. It is consistent with no mind-reading.
9. **No equilibrium past one year has been measured.** Check 7.

---

## 16. Questions for Sid

1. **The rates stay** as the town's occasions, so rate-drawn gifts, help and arguments carry no motive. Rule 10's letter says every act toward someone needs a motive. Accept this for 0d, with `AimedRateScale` as the dial once the gate is measured?
2. **News at about 4 a week for 26 people** (205 a year): is that "a few a week"? If not, should Argued's juiciness drop to 2.5 before the rate does?
3. **The first season end:** accept d27 as a warm-up, or seed starting tensions (Q1), or the first-greeting tone, which logs its cause?
4. **Fear of greater harm** as +0.1 cost per hostile act received from that person in 28 days, optionally plus the subject's kin present. Is that law 4's reading?
5. **Slots** count attempts: acts begun, and close calls that ended with no act. Right?
6. **One return per kindness:** no ping-pong, with friendship growing through Fond. Allow a second return if Fond is not enough?
7. **Snubbed and TurnedAway** as the light hostile acts: seen and felt, never retold. Acceptable?
8. **Stance** gives hermits and brawlers a fast state in 0d, and 0e's plasticity reads only events. Accept that boundary? Should stance ship on if E2 shows it moves less than 20%?
9. **Gifts stay free of money** for now?
10. **The robbed keeper's own retaliation** is separate from the town's one confrontation per scandal. Keep both?
11. **Households:** the gate never acts between kin or housemates, so family friction comes from rate draws, rows and squabbles. Accept?
12. The 0e questions in 13.4.

---

## 17. Docs in the same PR

- `docs/under-glass/design.md`:
  - rule 10 as built: costs with the surcharge, fear, Fond, the avoidance trigger, rates kept as occasions;
  - Q6 settled (R8); Q13 measured;
  - 11e: the character seam and `LifeEvents`;
  - section 11's 0d status.
- `docs/under-glass/0c` deferred list: "Law 1 as each villager's own rate of acting" is now `PowerWeight` in the gate.
- `sim/README.md`: the gate, flags, metrics, the probe and sweep table, and test counts.
- The decision record: no motive from hearsay but rule 9's; families cover in the gate; stance and plasticity count different things.

---

## Appendix A. Flaws the judges found, and their fixes

**Flaws found in the base (minimal):**

| Flaw | Fix |
|---|---|
| Rule 10 is half built: the three aimed rates stay outside the gate | Kept on purpose, as occasions, because they were measured and are the sparks the gate answers. Named a departure; `AimedRateScale` is the dial (question 1); E2 ablates the gate with `Desire = false` |
| The +0.3 hostile surcharge dropped | Literal form: Argued = walk up 0.5 + 0.3 = 0.8, the same number measured; Snub = wave 0.2 + 0.3 |
| Law 1 (power of acting) left out | `PowerWeight x (P - 0.5)` in effective boldness, default 1, measured against 0 at step 5 |
| No motive from love alone; friendship thin (0.93 a year) | Fond (R3): love above `LoveAt` x absence, satisfied by contact or one's own kindness; the cliff check in step 7; Pity as the fallback |
| No light hostile act, no third slight | Snubbed (gated, 0.5) and TurnedAway (no gate), the light cap, and the mark (R8) |
| Avoidance only on a clear no; moderately shy people wait | With `LightActsOn`, avoid when the best hostile margin is below 0 and nothing was taken |
| Withdrawal lapses after 7 days; no lasting state | Stance (R10), with a 28-day half-life, in effective boldness and the home weight; withdrawal only for those who could not answer (faithful's split) |
| d27 misses the 1% floor in every cell | Light acts (expected to help), then `ToneOn` or `--tensions` behind switches, then a waiver for Sid (step 9, question 3) |
| News doubles; 4 points of room under the band ceiling | Light acts are never retold; contingencies in step 8 (Argued juiciness 2.5, then `AimedRateScale`) |
| Brawler concentration (Sam–Shane 18 of 201) | Fear per hit kept; `FearPerKin` sweep; a concentration metric; stance limited by war towns at step 4 |
| `About` reused for the source: may be misread | Checked at 1d7d124 (section 6): only `Answered` (consequence kinds) and `Undergo` (`Patient.Actor` rows) read it |
| Episodes record only the holder's side; no rebuffed or ignored | `LifeEvent` with outcomes from both sides, resolved at night (R12) |
| Tellings reach `StirFrom`, so hearsay stirs a motive (against D34) | R1 step 2: witnessed only, except rule 9's keeper (D31) |
| Misattributed fear | Hits only from witnessed beliefs, so the participant's clarity is 1; the remaining case is a risk (15.8) |
| The E1 margin is thin; no 200-seed confirmation | Step 2 runs it before any graft |
| 16% of arguments still inside households | Kept: rates and rows give families their friction (faithful's "households never argue" was a flaw) |

**Where this spec does not follow a judge, and why:**

1. **Venting by halves** (judge 1, from mod-lessons) is not taken. Each hostile act removes the motive, and the next hurt re-stirs it, as the base does. The base already makes 6.7 feuds a year and needed fear to stop war towns. Venting by halves would keep hostile motives alive between hurts and push toward war. Judge 1 recommended it to fix faithful's stalled feuds, a problem the base does not have.
2. **Indignant motives for onlookers** (judge 1) are deferred to rule 7. Five onlookers per argument would multiply hostile motives, and siding is rule 7's business.
3. **Fear scaled by (1 - boldness)** (mod-lessons) is not taken. Judge 2 found that it weakens the brake for exactly the bold cluster. Fear stays flat per hit.
4. **The hostile familiarity term as the grudge** (mod-lessons) is not taken. Rule 10's literal 0.5 x familiarity is kept, because families cover, so home is never the cheap place to quarrel. The grudge enters through intensity instead.
5. **Stance:** judge 1 wanted it in 0d; judge 2 wanted it only after E1 passes. It is built in 0d.3 behind `StanceOn` and turned on in the default town only after the base has passed steps 1-2 and checks 4b and 5 still hold with it. That meets both.
6. **Pity:** judges 1 and 3 suggested it, judge 3 said measure first, and judge 2 preferred Fond. Fond is the default lever and Pity the fallback. Families cover, which removes pity at home, the volume risk the judges found.
7. **Faithful's love motive** (regard x absence) is not taken because of its measured cliff. Fond counts only love above `LoveAt`, as faithful's own risk-1 fallback proposed.
8. **Faithful's per-minute close-call key** is not taken. The base's key on the motive's state is kept (judges 2 and 3).
9. **Faithful's elastic entries with `_feltCause`** are not taken. The base's one motive per (holder, subject, kind), re-stirred, with motives dropped on reattribution, is simpler and was measured.
10. **The `--fo` parser fix** (judge 3) needs no change. The repo's `Set` already parses bool, int and double separately (`Program.cs:55`); faithful's crash was in its probe console.
11. **`FearPerKin`** is in the spec at 0. Judge 3 wanted it added; judge 1 said not without a sweep. It is swept at step 6.
12. **Mod-lessons' trait reads left on the immutable record**, and its constructor default that shifts the default hash: neither is copied. All 14 reads switch in 0d.0, and the gate lives in `FeelingOptions`.

## Appendix B. Base measurements quoted (probe-minimal, `--fo Desire=true FearPerHit=0.1 AvoidOn=true`, plastic 2)

**a5, 30 seed-years:**
- **Gate traffic a year:**
  - stirred: Answer 108, Return 158, Retaliate 10, MakeUp 2.4;
  - gate acts: Argued 95, GaveGift 142, HelpedSomeone 16;
  - rate-drawn: Argued 37, gifts 287, help 37.
- **Withdrawal:** avoids 5.7 a year; withdrawals 37.
- **Arguments:** cross-household arguments 111 a year; 8.4 unordered pairs with 2+ each way.
- **Ties:** feuds by season 0.53, 2.30, 2.20, 1.67; friendships 0.03, 0.10, 0.40, 0.40. Seeds with a feud 97%, with a friendship 73%.
- **Spread:**

  | Season end | Below -0.2 | Mean change | Moved 0.1+ |
  |---|---|---|---|
  | d27 | 0.3% | +0.011 | 5.4% |
  | d55 | 1.0% | +0.014 | 9.4% |
  | d83 | 1.2% | +0.019 | 11.4% |
  | d111 | 1.2% | +0.022 | 12.5% |

- **Placement:** arguments inside households 16%; with someone disliked 71%.
- **Volume:** news 205, trivia 621 a year.
- **Time:** run 559 s for 30 seed-years on 4 cores.

**Other runs:**
- **c1, 100 seed-years, no avoid:** E1 65%; feuds 8.1, friendships 0.99; d111 below -0.2 1.5%.
- **a2 band, 400 x 14 `--inject`:** 57% in band, 24% over 70%.
- **v1, no fear:** feuds 12 a year; war towns 6%.
- **r1, Argued rate halved:** news 151; band 60/23%; E1 67%; below -0.2 0.6-0.8%.
- **Sweep (`sw-*`, 18 cells, 20 seed-years each):**
  - 10 reach E1 ≥ 60%;
  - 4 also hold d111 below -0.2 within 1-5% with no war or dead towns: p2/f0.05, p2/f0.1, p2.5/f0.1 and p2.5/f0.15, all at Argued 0.8;
  - at Argued 1.1 the feuds collapse;
  - at plastic 1.5, friendships are 0.2-0.5 a year.
