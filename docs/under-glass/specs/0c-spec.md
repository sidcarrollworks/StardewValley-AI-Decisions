> The implementation spec as written before building (kept for reference). Where the code differs, `sim/README.md` and `docs/under-glass/design.md` ("As built") say how and why.

> **Current planning:** the [roadmap](../roadmap.md) supersedes the older work order and
> E1/E2 outcome gates. This document preserves the original mechanics and experiment criteria;
> it is not a request to restore those outcome quotas.

# Under Glass phase 0c: feelings — implementation spec

All hooks below were checked against commit `b53bce8` (HEAD of `claude/affectionate-pasteur-qzdpvi`; the sim code has not changed since 0b, PR #40). Line numbers refer to that commit. Every constant is a first guess for the sweeps to settle.

## 0. What 0c builds

Each villager gets three new things:
- **Regard** for every other person, from -1 to 1. This is love and hate (law 3).
- **Regard for each kind of person.** This is prejudice (law 9).
- **Mood.** It comes from the joy and sadness of the last three days, and together with the person's conditions it gives a **power of acting** P from 0 to 1 (laws 1 and 2).

Every belief a villager gets, and every act done to them, gives one signed felt amount. Joy is positive and sadness negative. That amount moves mood, and it is attributed to whoever the villager *believes* caused it, which moves regard. If they saw only "someone", it goes to the kind of person they saw instead.

Laws 5, 6 and 8 to 12 are factors or routing on that one flow:
- sympathy and antipathy (5);
- imitation and indignation (6);
- reciprocity, and love after conquered hate (8);
- kinds of people (9);
- blame by freedom and known hardship (10);
- presence, hearsay and time (11);
- temperament (12).

Sims-style sentiments are the named, cited parts of regard. No rule reads them.

Feelings then steer the ten decisions that today use familiarity in place of liking, or a fixed rule:
- who a gift, a favour or an argument goes to;
- who is in the mood to act;
- the gossip close tie;
- the mayor's trust and his sway;
- reporting;
- suspicion order and confident guesses;
- who confronts;
- grievance as a theft motive;
- where a household buys its groceries.

**Base design: A**, which two of the three judges favoured. Ideas taken from the other designs:
- **From C:**
  - a switch that turns feelings off and reproduces the pre-0c log hash, pinned;
  - a regard term at every hook that is exactly 0 at neutral regard;
  - worlds built by tests default to feelings off;
  - bands on the spread of regard in the gate;
  - the tuning order;
  - the III P25 and P26 readings of the hooks;
  - for a told act whose patients are the onlookers, the original witness is the patient;
  - contact healing, and "company keeps a friendship";
  - kin standing scaled by (1 - familiarity).
- **From B:**
  - slices, with a check that feelings which only observe change nothing;
  - rule 9's first-hand confirmation through consequence acts and restitution;
  - retention;
  - a 3-day linear mood window, with no `Exp`, `Tanh` or `Pow`;
  - the III P46 spill for identified strangers;
  - a cap per act;
  - recompute-and-diff with exact reversal;
  - a hold time and margin for shop choice;
  - log lines that cite a sentiment;
  - golden story tests;
  - Sid's read of ten seeds.

Appendix A lists every flaw the judges found, how it is fixed, and the four places where this spec does not follow a judge's advice, with the reason.

## 1. Slices

Build in this order. Each slice is one commit with its tests green and its numbers measured.

| Slice | Content | Must hold before the next |
|---|---|---|
| **0c.0** hygiene | `Simulation.Run` formats in the invariant culture. Add a pinned-hash test against b53bce8. | Hashes `e7f6653087ff7e18` and `388d128d7fd4cdf2` (section 9, T1-T2). |
| **0c.1** observe | Data model, `Feelings.cs`, `Simulation.Feelings.cs`, rules F1-F18, metrics, runner flags. `DefaultTown.Feelings()` returns `Steer = false`. | Watching changes no act and no belief (T12); every 0a and 0b number is the same as today. |
| **0c.2** steer | Rules S1-S10. `DefaultTown.Feelings()` returns `Steer = true`. | Gate checks 1, 2 and 3 (section 11). |
| **0c.3** measure | E1, the ablations, tuning (section 12), Sid's read, docs. | The whole gate. |

## 2. Data model

### 2.1 `sim/UnderGlass.Sim/Model.cs`

New parameters go at the end, with defaults, so every existing call and test compiles unchanged.

```csharp
// line 52: two traits appended (law 12; rule 4)
public sealed record Temperament(double Chattiness, double Boldness, double Understanding, double SelfRegard,
    double Sensitivity = 0.5, double Retention = 0.5);

/// <summary>Who is joyed or saddened by an act, and so who its cause is (law 3).
/// Target: the act's Target, caused by the actor. Actor: the actor, caused by the act's Target
/// (null: nobody, an accident). Onlookers: each witness, caused by the actor.</summary>
public enum Patient { Target, Actor, Onlookers }

/// <summary>How Act.Target is set when an act begins.</summary>
public enum TargetIs { None, Keeper, Chosen, Kin, Given }

/// <summary>An act kind's feeling row. Joy: what the patient feels, -1..1 (+ joy, - sadness);
/// separate from Valence, which still decides tiers. Plastic: the share that becomes regard,
/// 0..1. Freedom: how freely its cause is believed to act, 0..1 (law 10). Tilt: +1 active act,
/// -1 passive vice, 0 neither (law 1).</summary>
public sealed record Affect(Patient Patient, double Joy, double Plastic, double Freedom = 1,
    TargetIs Target = TargetIs.None, int Tilt = 0);

// ActKind (line 159): append after WithKin
    Kin? WithKin = null, Affect? Affect = null)

// Act (line 183)
public sealed record Act(int Id, int Tick, string Actor, string Kind, string Location, Tile At,
    bool Injected = false, string? Target = null, int About = -1);

// Belief (line 195): append after Suspects
    IReadOnlyList<string>? Suspects = null, string? Target = null, string? SeenKind = null);

/// <summary>A named lasting feeling with its cause (Sims 4; principle 5). Toward is a villager or
/// "kind:<Kind>". Strength 0..1.</summary>
public sealed record Sentiment(string Holder, string Toward, string Name, int ActId, double Strength, int Since, int Count = 1);

/// <summary>One feeling event: what moved whose mood and regard, and which act it cites.</summary>
public sealed record Felt(int Tick, string Holder, int ActId, string Route, string Basis, double Mood,
    string? Toward, double Raw, double Change);
```

What the new fields mean:
- **`Act.Target`** is the other party:
  - the keeper of the place, for theft and the bin;
  - the person chosen, for a gift, a favour or an argument;
  - the sibling, for a squabble;
  - the questioner, mayor or escort, for consequence acts;
  - the parent or keeper, for a family row.

  It is null when feelings are off.
- **`Act.About`** is the scandal or act that a consequence act answers. This is design rule 2's causeIds. It is public: the official says what it is for.
- **`Belief.Target`** is the target as the holder knows it, copied in gossip.
- **`Belief.SeenKind`** is the `Villager.Kind` of an actor the witness saw but could not name ("someone, a young man"), copied in gossip. This is perception at the moment of seeing (rule 2).
- **`Felt.Route`** is one of: Direct, Onlooker, Sympathy, Antipathy, Imitation, Kind, Spill, Association, Undergone, Accused, Confronted, Shame, Reconciled, Reattributed.
- **`Felt.Basis`** is one of: Witnessed, Found, Told, HeardName, Corroborated, Confirmed, Event.
- **`Felt.Raw`** is the change asked for; **`Felt.Change`** is what was applied after saturation and the cap.

### 2.2 `FeelingOptions` (new, in `sim/UnderGlass.Sim/Feelings.cs`)

The mutable class `FeelingOptions` holds the switches and constants below. Static helpers:
- `FeelingOptions.Off => new() { Enabled = false, Steer = false }`
- `FeelingOptions.Observe => new() { Steer = false }`

`Steer` has no effect while `Enabled` is false.

| Name | Default | Range | Rule |
|---|---|---|---|
| `Enabled` | true | | everything |
| `Steer` | true | | S1-S10 |
| `Sympathy`, `Imitation`, `Reconcile`, `Kinds`, `Freedom`, `Presence`, `Association`, `Shame` | true | | E2 ablation switches (F4, F8, F9, F6, F5, F10, F14) |
| `Household` | 0.6 | -1..1 | F1 |
| `Friend` | 0.4 | -1..1 | F1 |
| `Start` | empty `IReadOnlyDictionary<(string From, string To), double>` | -1..1 | F1: sets seed and baseline |
| `LoveAt` | 0.2 | 0..1 | F4 |
| `LoveShare` | 0.5 | | F4 (rule 6) |
| `HateCap` | 0.3 | | F4 (rule 6) |
| `ImitateShare` | 0.2 | | F4 (rule 6) |
| `PlasticScale` | 1 | 0..3 | F7 (sweeps) |
| `HearsayMood` | 0.5 | | F5 (rule 9) |
| `CredencePerRegard` | 0.25 | | F5, S4 (rule 9) |
| `HeardNameWeight` | 0.5 | | F5 (rule 9, confirmed first-hand) |
| `CorroboratedWeight` | 0.25 | | F5 (rule 9) |
| `SevereAt` | 0.7 | | keep (rule 4) |
| `RepeatDays` | 7 | | F7 (rule 4) |
| `MaxPerAct` | 0.5 | | F8 |
| `ReconcileFrom` | 0.2 | | F8 (a trough at or below -0.2) |
| `ReconcileShare` | 1.0 | | F8 (III P44) |
| `KindShare` | 0.5 | | F9 |
| `AssocShare` | 0.2 | | F10 |
| `AccusedJoy` | -0.3 | | F13 |
| `ConfrontJoy` | -0.3 | | F13 |
| `EventPlastic` | 0.5 | | F13, F14 |
| `ShameJoy` | -0.05 | | F14 |
| `ShameCap` | 6 | | F14 |
| `PublicHolders` | 3 | | F14 |
| `MoodDays` | 3 | | F16 (rule 4) |
| `PowerScale` | 0.4 | | F16 |
| `NeedPower` | 0.15 | | F16 |
| `WantPower` | 0.05 | | F16 |
| `HeldPower` | 0.10 | | F16 |
| `CompanyJoy` | 0.02 | | F16 |
| `DriftPerDay` | 0.005 | | F17 (rule 5) |
| `ContactMinutes` | 60 | | F17 |
| `ContactHeal` | 0.01 | | F17 |
| `SentimentMin` | 0.02 | | F18 |
| `SentimentKeepPerDay` | 0.975 | | F18 (28-day half-life) |
| `SentimentsPerPair` | 3 | | F18 |
| `LogAt` | 0.005 | | log lines |
| `TargetBase` | 0.2 | 0..1 | S1 |
| `TiltScale` | 1 | | S2 |
| `CloseTieAt` | 0.4 | | S3 (rule 8) |
| `SwayLoveAt` | 0.4 | | S4 |
| `ReportPerHate` | 0.3 | | S5 |
| `SuspectPerRegard` | 0.5 | | S6 |
| `GuessPerHate` | 1 | | S6 |
| `CoverAt` | 0.4 | | S7 |
| `ConfrontPerHate` | 2 | | S7 |
| `GrievanceWeight` | 0.5 | | S8 |
| `GrievanceAt` | 0.2 | | S8 |
| `ChainPull` | 2 | | S9 |
| `ShopMargin` | 0.3 | | S9 (rule 10) |
| `ShopHoldDays` | 28 | | S9 (rule 10) |
| `FeudAt` | -0.3 | | metrics, ties |
| `FriendAt` | 0.4 | | metrics, ties |

`Feelings.cs` also holds `public static class Feelings`: pure functions with no world, tested like `Authority.Weigh`. They are `Saturate`, `Squash`, `Base`, `Likeness`, `Keep`, `Excuse`, `Credence`, `ReconcileBonus`, `PowerOf`, `PowerFactor`, `TargetWeight`, `Close`, `ReportChance`, `AccusedSplit` and `SentimentName`.

### 2.3 State: `sim/UnderGlass.Sim/Simulation.Feelings.cs` (new partial)

Everything is indexed by `_index`, which follows name order. All of it is owned by the game thread; the sim is single-threaded.

```csharp
private readonly FeelingOptions _fo;
private double[,] _regard = null!, _baseline = null!, _trough = null!, _conquered = null!; // n x n
private double[,] _kind = null!;          // n x k
private string[] _kindNames = null!;      // distinct Villager.Kind, ordinal (11 in the town)
private int[] _kindOf = null!;            // villager index -> kind index
private List<(int Tick, double Amount)>[] _affects = null!;  // mood entries, last MoodDays
private int[,] _together = null!;         // minutes together today, [min(i,j), max(i,j)]
private bool[,] _slighted = null!;        // [h, j]: an act of j lowered h's regard today
private int _now;                         // current minute (set at the top of Step)
private readonly Dictionary<(int Holder, int ActId), FeltRecord> _felt = new();
private readonly Dictionary<(int Holder, int ActId, int Subject), double> _actTotals = new(); // Subject: person i, or -(k+1)
private readonly Dictionary<(int Cause, int Patient, string Kind), List<int>> _repeats = new();
private readonly HashSet<(string Who, int ActId)> _did = new();       // self-knowledge only
private readonly Dictionary<(int Kin, int ActId), HashSet<string>> _knownHolders = new();
private readonly HashSet<(int Subject, int ActId, string Event, int Namer)> _eventsFelt = new();
private readonly Dictionary<string, (string Shop, int Since)> _shopOf = new();
private readonly Dictionary<(string Holder, string Toward, string Name), Sentiment> _sentiments = new();
private readonly List<Felt> _feltLog = new();
private readonly List<(int Day, string A, string B, string What)> _ties = new();
private readonly HashSet<(int, int, string)> _tied = new();
private readonly List<(int Day, string Household, string From, string To)> _shopSwitches = new();
private readonly Dictionary<string, double[]> _powerByDay = new();
private readonly List<(int Day, double[] Regard)> _snapshots = new();

private sealed class FeltRecord
{
    public int Patient = -1; public string Route = ""; public double F0; public double Repeat = 1;
    public double S; public double Mood; public double W; public int Cause = -1;
    public bool NameHeard, Corroborated, Confirmed;
    public List<(int Subject, double Raw, double Applied)> Entries = new();
}
```

Public read API, for tests and the runner only. No rule reads another villager's mind through it.
- `Regard(a, b)` gives effective regard, the value decisions read.
- `PersonalRegard(a, b)`, `Baseline(a, b)`, `KindRegard(a, kind)`
- `Mood(a)`, `Power(a)`, `SentimentsOf(a)`

### 2.4 `SimResult` (Simulation.cs line 76): appended, all `required`, empty when feelings are off

```csharp
public required IReadOnlyList<string> Names { get; init; }      // name order, for the flat arrays
public required IReadOnlyDictionary<(string From, string To), double> Regard { get; init; }    // personal, at the end
public required IReadOnlyDictionary<(string From, string To), double> Baseline { get; init; }
public required IReadOnlyDictionary<(string From, string Kind), double> KindRegard { get; init; }
public required IReadOnlyList<(int Day, double[] Regard)> RegardSnapshots { get; init; } // each season end and the last day, flat n*n
public required IReadOnlyDictionary<string, double[]> PowerByDay { get; init; }
public required IReadOnlyList<Sentiment> Sentiments { get; init; }   // held at the end
public required IReadOnlyList<Felt> Feelings { get; init; }          // every regard change; mood-only events at |Mood| >= 0.05
public required IReadOnlyList<(int Day, string A, string B, string What)> Ties { get; init; } // feud, kin-feud, friendship, reconciled
public required IReadOnlyList<(int Day, string Household, string From, string To)> ShopSwitches { get; init; }
```

### 2.5 `sim/UnderGlass.Sim/DefaultTown.cs`

**Sensitivity** comes from `fixtures/game/temperament/temperament.json`. This is a private prototype; the values are replaced with the original cast later (design section 3).

| Villager | Sensitivity | Villager | Sensitivity | Villager | Sensitivity |
|---|---|---|---|---|---|
| Abigail | 0.38 | Harvey | 0.50 | Penny | 0.67 |
| Alex | 0.41 | Jas | 0.55 | Pierre | 0.36 |
| Caroline | 0.36 | Jodi | 0.51 | Robin | 0.26 |
| Demetrius | 0.50 | Kent | 0.54 | Sam | 0.40 |
| Emily | 0.37 | Leah | 0.33 | Sebastian | 0.64 |
| Evelyn | 0.30 | Lewis | 0.30 | Shane | 0.74 |
| George | 0.54 | Marnie | 0.37 | Vincent | 0.43 |
| Gus | 0.35 | Maru | 0.42 | Newcomer | 0.50 |
| Haley | 0.37 | Pam | 0.56 | | |

**Retention:**
- Pam 0.2, which is the mod's value in `src/NpcMotives/MotiveOptions.cs`.
- Robin 0.8. This is a VERIFY: design section 2 says "Robin keeps", but no number has been decided.
- Everyone else 0.5.

**Feeling rows in `Acts()`.** OutLate has no row (`Affect: null`).

| Kind | Patient | Joy | Plastic | Freedom | Target | Tilt |
|---|---|---|---|---|---|---|
| RummagedInBin | Target | -0.3 | 0.5 | 1 | Keeper | 0 |
| Stole | Target | -0.5 | 0.5 | 1 | Keeper | 0 |
| DrunkScene | Onlookers | -0.15 | 0.3 | 0.6 | None | -1 |
| Argued | Target | -0.3 | 0.3 | 1 | Chosen | 0 |
| HelpedSomeone | Target | +0.3 | 0.3 | 1 | Chosen | +1 |
| Squabbled | Target | -0.15 | 0.05 | 0.5 | Kin | 0 |
| GaveGift | Target | +0.2 | 0.3 | 1 | Chosen | +1 |
| Stumbled | Actor | -0.1 | 0 | 0 | None | 0 |
| Collapsed | Actor | -0.3 | 0 | 0 | None | 0 |
| WarnedByMayor | Actor | -0.3 | 0.4 | 0.4 | Given | 0 |
| TakenIn | Actor | -0.6 | 0.4 | 0.4 | Given | 0 |
| Questioned | Actor | -0.15 | 0.3 | 0.4 | Given | 0 |
| Service | Actor | -0.4 | 0.4 | 0.4 | Given | 0 |
| FamilyRow | Actor | -0.3 | 0.3 | 1 | Given | 0 |

Guide magnitudes, at sensitivity 0.5 and retention 0.5, with no saturation:

| Who | Change in regard toward the cause |
|---|---|
| A keeper who sees a theft close up | -0.25 |
| Someone who loves that keeper (0.6) and sees it close up | -0.075 (rule 6: 0.5 x 0.25 x 0.6) |
| A neutral witness of likeness 0.5 | -0.028 |
| A gift's recipient | +0.06 |
| A favour's recipient | +0.09 |
| An argument's target | -0.09 |
| A squabbled sibling | -0.0034 |

New functions:
- `public static FeelingOptions Feelings() => new() { Start = Tensions(), Steer = /* false in 0c.1, true from 0c.2 */ };`
- `public static IReadOnlyDictionary<(string From, string To), double> Tensions()`. It returns an empty dictionary until Sid picks the town's tensions (question 1). The comment lists the candidates.

`Cast()` keeps every other field.

## 3. Notation

| Symbol | Meaning |
|---|---|
| `sens(i)` | 0.5 + Sensitivity(i) |
| `U(i)` | Understanding(i) |
| `SR(i)` | SelfRegard(i) |
| `fam(i, j)` | the existing `_fam` |
| `R`, `B`, `K`, `T`, `Q` | regard, baseline, kind regard, trough, conquered (the love received since the trough) |
| `E(i, j)` | `clamp(R[i,j] + (1 - fam(i,j)) x K[i, kind(j)], -1, 1)`. With `Kinds` off, `E = R`. |
| `St(i, j)` | `Steer ? E(i, j) : 0`. This is the only regard a decision reads, so every hook is unchanged when Steer is off. |
| `Likeness(i, j)` | 0.25 each for: same Household, same Stage, same Kind, both have a Job at one Place. `Likeness(i, unknown) = 0`. |
| `keep(i, f)` | 1 if abs(f) >= SevereAt, else 0.5 + Retention(i) (rule 4) |
| `Squash(x)` | x / (1 + abs(x)) |
| `Saturate(r, d)` | r + d x (1 - abs(r)) when r != 0 and d has r's sign; else r + d. Then clamp to [-1, 1]. |
| `phi(h, C, row)` | Freedom ? row.Freedom x (1 - Excuse(h, C)) : 1 (F6) |
| `Did(who, actId)` | `_did.Contains((who, actId))`. Self-knowledge, called only with the subject's own name. |
| `Known(actId)` | `_cases[actId].Values` plus every `_carried` account with that ActId (both are what the authority holds) |

## 4. Feeling rules (slice 0c.1)

**F1. Seeds and baseline** (the store of law 3; rule 5; design section 5).
- `R = B`:
  - Household (0.6) for housemates, including Shane at the ranch;
  - Friend (0.4) if either lists the other;
  - 0 for everyone else, and always 0 with the newcomer.
- Then every `Start` entry overrides both R and B.
- `K = 0`, `T = min(0, R)`, `Q = 0`.
- Seeded state: 86 of 650 ordered pairs are at 0.4 or more (46 in households, 40 between friends).

**F2. Act roles** (law 3 needs a patient and a cause; rule 2's target and causeIds). Set in `Begin`, only when `Enabled`. When off they stay null and -1, so records and log lines are unchanged.
- **Keeper:** the keeper of the place (`_ao.Keepers[actor.Place]`), unless that is the actor.
- **Chosen:** drawn by S1.
- **Kin:** the nearest awake kin of the `WithKin` role in the same place within `FarTiles`, ties in name order. No draw.
- **Given:** passed in by the caller: the questioner, mayor or escort; the keeper in a row.
- `About` is passed by `Interview`, `Deliver` and `Rows`.
- `_did.Add((actor, act.Id))` in `Begin`, and in `SeenOutLate` for the person seen.

**F3. Patient X and cause C, read from the holder h's belief b.** Never from the truth.

| Patient | X | C |
|---|---|---|
| Target | `b.Target` (null: unknown) | `b.Actor` (null: "someone") |
| Actor | `b.Actor` | `b.Target` if row.Freedom > 0, else nobody |
| Onlookers | h, if `b.Source` is Witnessed; `b.Chain[^1]`, the witness the story came from, if Told | `b.Actor` |

If `C == h`, nothing is felt: h caused it. `Belief.Target` is set as follows:
- witnesses of Keeper kinds always know it;
- the target always knows it;
- other witnesses know it when `Perception.Identifies(clarity, fam(observer, target))`;
- finders of a Keeper kind's trace know it;
- gossip copies it.

**F4. The felt amount f0** (laws 2, 5, 6, 12; III P21-P24 and P27, VERIFY numbering). It is computed at h's first feeling about the act, and again if X changes. Here Joy is row.Joy and r = E(h, X):

| Case | f0 | Route |
|---|---|---|
| X is h | Joy x sens(h) | Direct, or Onlooker for Onlookers kinds |
| r >= LoveAt | LoveShare x r x Joy x sens(h) | Sympathy |
| r <= -LoveAt | -LoveShare x min(abs(r), HateCap) x Joy x sens(h) | Antipathy |
| otherwise (or X unknown) | ImitateShare x (0.5 + 0.5 x Likeness(h, X)) x Joy x (1 - 0.5 x U(h)) x sens(h) | Imitation |

- With `Sympathy` off, the first two rows use the Imitation row. With `Imitation` off, the last row gives 0.
- The magnitude comes from the row, never from X's own sensitivity, so nobody reads another mind.
- The sign of f0 is the sign of h's feeling. In the Antipathy row, a hated person's sadness is joy to h, and so love toward its cause (III P24). Their joy is sadness to h, and so hate toward its cause: envy.

**F5. Presence s (mood) and attribution weight w (regard)** (law 11; rule 9).

`Credence(a, t) = clamp(0.5 + 0.5 x fam(a, t) + CredencePerRegard x (1 - U(a)) x E(a, t), 0.25, 1)`. At regard 0 it equals today's 0.5 + 0.5 x familiarity.

| Basis | s | w |
|---|---|---|
| Witnessed, name seen | `b.Clarity` | `b.Clarity x b.Confidence` (1 x clarity when identified; 0.8 x clarity for a confident guess) |
| Witnessed "someone" | `b.Clarity` | 0 (the change goes to the seen kind, F9a) |
| Witnessed or Found, then a name heard (`NameHeard`) | unchanged | `HeardNameWeight x b.Clarity x b.Confidence` (rule 9: confirmed first-hand) |
| Found, no name | `b.Clarity` (the loss is present) | 0 |
| Told | `HearsayMood x Credence(h, b.Chain[0])` | 0 until corroborated or confirmed |
| Told and corroborated (F15) | as Told | `CorroboratedWeight x b.Confidence` |
| Any basis, confirmed (F15) | as above | `max(w, HeardNameWeight x b.Confidence)` |

- With `Presence` off, s = 1 and w = 1 whenever a name or a kind is known.
- Mood only ever rises: h feels `f0 x max(s seen so far)`, and the difference is added to mood.
- While the cause stays the same, w only ever rises. When the cause changes, w is recomputed (F11).

**F6. Blame and freedom** (law 10; III P49, V P3, V P6, VERIFY).
- `Excuse(h, C) = [h and C share a household] x NeedPressure(C) x (0.5 + 0.5 x U(h))`. Housemates live from one purse, so they know it was short; nobody else knows.
- `phi = row.Freedom x (1 - Excuse)`.
- Accidents (Freedom 0) are blamed on nobody, because F3 gives them no cause.
- Officials doing their job have Freedom 0.4.

**F7. Regard toward the believed cause** (laws 3 and 4; rule 4). For C a person other than h:
`dr = f0 x w x row.Plastic x PlasticScale x phi x keep(h, f0 x s) x Repeat`.
- `Repeat` applies only to the Direct route with Joy > 0. It is 0.5^n, by repeated multiplication, where n is the number of earlier acts of the same kind from C to h within RepeatDays (the `_repeats` list).
- There is no separate reciprocity term. "Who imagines themselves loved, loves back" is this Direct attribution of an act aimed at h. "Hate returned increases" is the same attribution plus S1's target choice, which sends arguments to the disliked.

**F8. Moving regard: `Move(h, subject, raw, actId, route, byThem)`** (laws 8 and 11).
1. **Cap.** Clip raw so that the sum of abs(raw) applied to (h, actId, subject) by every rule citing that act stays within MaxPerAct (`_actTotals`).
2. **Person subject:**
   - Set `R' = Saturate(R, raw)`.
   - If `byThem` and raw > 0, add the applied amount to `Q[h, j]`.
   - **III P44, love after conquered hate.** If `Reconcile` is on, `byThem`, raw > 0, R < 0 <= R', and T <= -ReconcileFrom, add `ReconcileShare x min(abs(T), Q)` and clamp. Then log a `Reconciled` sentiment and a "reconciled" tie.
   - **Trough upkeep.** If R' < T, set T = R' and Q = 0. If R' >= 0, set T = 0 and Q = 0.
   - If raw < 0 and `byThem`, set `_slighted[h, j]`.
3. **Kind subject:** `K' = clamp(K + raw, -1, 1)`.
4. Record `Felt`. Log `{m} regard {h} {subject} {change:+0.000} -> {value:0.000} act {id} {route}` when abs(change) >= LogAt. Apply F18.

Reversal (F11) subtracts the stored applied amount with no saturation, no bonus and no sentiment. It updates T if the new value is lower, and removes h's sentiments that cite that act toward that subject.

**F9. Kinds of people** (law 9; III P46, VERIFY).
- (a) **A witness who saw "someone".** For a Witnessed belief with no Actor and a SeenKind, the change goes to `K[h, SeenKind]` instead: raw = `f0 x b.Clarity x Plastic x PlasticScale x row.Freedom x keep x (1 - 0.5 x U(h))`.
- (b) **A known but unfamiliar person.** Every F7 change dr toward C also moves `K[h, kind(C)]` by `KindShare x (1 - fam(h, C)) x (1 - 0.5 x U(h)) x dr`.
- Kind changes and the spills of F10 never generalise further.
- Decisions read E, so prejudice weighs on strangers and fades as familiarity grows. Familiarity already grows 0.012 an hour together.

**F10. Standing spills to kin** (rule 17, shame by association; law 9). Only for scandal-tier acts, and only when F7's dr < 0. Each kin Q of C, in ordinal order, where Q is not h and not h's kin, moves by `AssocShare x (1 - fam(h, Q)) x (1 - 0.5 x U(h)) x dr`.

**F11. Re-attribution: recompute and diff** (the "attributes" of law 3; design story 1). `Feel(who, b, prior, teller, m)` is the one entry point for beliefs. `Add` calls it for every stored or changed belief, including fill-ins and suspects.
1. Resolve X and C (F3). Return if there is no row, Joy is 0, or C is h.
2. If the record is new, or X changed, set `F0` and `Route` (F4). On a new record, also set `Repeat`.
3. If `teller != null`, `b.Source != Told` and `b.Actor != prior?.Actor`, set `NameHeard`.
4. Mood: `S = max(S, s)`. Add `F0 x S - Mood` when its magnitude is larger, then store it.
5. Weight: `W = (C == Cause) ? max(W, w) : w`. Then `Cause = C`.
6. Desired entries per subject: C (F7), kind(C) (F9b), C's kin (F10); or SeenKind (F9a). Summed per subject.
7. Diff:
   - Old entries whose subject is gone, or whose sign changed, are reverted exactly.
   - New subjects are applied with `Move`.
   - A same-sign rise applies only `raw_new - raw_old`.
8. If the act has `About >= 0`, the belief is Witnessed and its Actor is known, run F15's Confirm and F14's KinPublic.

**F12. Undergoing an act** (Patient.Actor kinds; the actor keeps no belief about their own act). This runs in `FinishActs` after the witnesses.
- `f = Joy x sens(s)`, added to mood.
- If row.Freedom > 0 and act.Target is not s: `phi = row.Freedom x (Did(s, act.About) ? 1 - U(s) : 1)`. A guilty subject understands why, so they blame the official or parent less (law 10).
- `dr = f x Plastic x PlasticScale x phi x keep(s, f)`, moved toward act.Target with route Undergone.
- Collapsed and Stumbled move mood only.

**F13. Being named or confronted** (the note to rule 16: "an innocent suspect's feeling toward whoever named them"; law 10; III P40 schol., VERIFY).
- Function: `Accused(subject, actId, namers, joy, eventName)`.
- `guilty = Did(subject, actId)`.
- `f = joy x sens(subject) x (guilty ? 1.5 - SR(subject) : 1)`, added to mood once per (subject, act, event).
- **Innocent:** each namer, in ordinal order and once each, gets `raw = f / n x EventPlastic x keep(subject, f)`. Route Accused or Confronted; sentiment Wronged.
- **Guilty:** an `Ashamed` sentiment toward themselves (strength abs(f)), and no regard change. One who believes they gave just cause feels shame, not hate.

Namers:
- In `Interview`: the `From` of every account in `known` (cases plus carried) with From != subject and (Actor == subject, or Nearby contains subject).
- In `Deliver`: the same over `Known(actId)`.

Confrontation is the same event with namers = [by] and ConfrontJoy.

**F14. Kin shame** (rule 17: "a parent is angrier at a public scandal than a private one"; laws 5 and 9).
- Applies to kin K of the believed culprit C (K's own belief) for a scandal-tier act.
- `_knownHolders[(K, act)]` collects every name in the chain of each telling K receives, in both branches of `TryTell`. The listener, C and K's own kin are left out.
- Witnessing a consequence act about it (WarnedByMayor, TakenIn, Service or Questioned, whose actor K sees is C) adds PublicHolders synthetic names (`public:{Q.Id}`).
- Each new name, up to ShameCap per (K, act), is one step:
  - `f = ShameJoy x (1.5 - SR(K)) x sens(K)` to mood;
  - `raw = f x EventPlastic x phi(K, C, row) x keep(K, f)` toward C, route Shame, sentiment Ashamed.
- Families still cover: no reports, no retelling and no suspicion of kin. Those rules are unchanged.

**F15. Confirmation and corroboration** (rule 9).
- **Corroborate** runs in TryTell's already-held branch. If the held belief (before any fill-in) is Told, names someone, the new telling names the same person, and the told chain (teller included) shares no name with the held chain: set `Corroborated` once and run Feel again.
- **Confirm(h, a, subject)** runs when h holds a belief about a whose Actor is subject. It sets `Confirmed` and runs Feel again. It is called:
  - from F11 step 8, for consequence acts WarnedByMayor, TakenIn, Service and FamilyRow (not Questioned, since most people questioned are innocent);
  - from `Review` after `PayUp`, for the keeper of a place other than the Mart.

**F16. Mood and power of acting** (laws 1 and 2).
- `AddMood(i, a)` appends `(_now, a)`.
- `MoodSum(i) = sum of a x max(0, 1 - (_now - tick) / (MoodDays x 1440))`, a linear 3-day window (rule 4).
- `Mood(i) = Squash(MoodSum)`.
- `Cond(i) = -(NeedPower x NeedPressure(i) + WantPower x WantPressure(i, m) + HeldPower x [DetainedUntil > m])`. It is 0 without money.
- `Power(i) = clamp(0.5 + Cond + PowerScale x Mood, 0, 1)`.
- **Company:** after each chat (`_chatted.Add` in Socialise), each side gets `CompanyJoy x (0.5 + own Chattiness) x (E(self, other) > -LoveAt ? 1 : -1)`. This is mood only and is not logged, so co-workers do not climb toward love just from talking.
- Rest and health enter as events (Collapsed, Stumbled), not as conditions, so a reading of P at 23:59 is not dominated by the hour.

**F17. Night: `CloseFeelings(day, days)`** (rule 5; law 11; law 12).

For each ordered pair (i, j), in index order:
- `together` means `_together >= ContactMinutes`.
- If R < B: `R = min(B, R + DriftPerDay + (together && !_slighted[i,j] ? ContactHeal x (0.5 + U(i)) : 0))`.
- If R > B and the pair was not together: `R = max(B, R - DriftPerDay)`. Company keeps a friendship.
- If R >= 0: T = 0 and Q = 0. Healing that crosses 0 gives no bonus.

Then, in this order:
1. K moves toward 0 by DriftPerDay, never past it.
2. Sentiments are multiplied by SentimentKeepPerDay, and dropped under 0.01.
3. Mood entries older than MoodDays are pruned.
4. `PowerByDay[i][day] = Power(i)`.
5. Ties, for unordered pairs i < j, recorded the first time each happens:
   - "feud" or "kin-feud": both directions <= FeudAt, and the pair was not seeded so;
   - "friendship": both >= FriendAt, the pair is not a household or kin, and was not seeded so.
6. A regard snapshot is taken when `day % 28 == 27` or on the last day.
7. `_together` and `_slighted` are cleared.

**F18. Sentiments** (Sims 4; principle 5). Each `Move` with abs(change) >= SentimentMin creates or refreshes `(Holder, Toward, Name)`:
- Strength = min(1, Strength + abs(change));
- ActId and Since become the latest;
- Count goes up by one;
- each pair keeps at most SentimentsPerPair (3), dropping the weakest.

There are ten names:

| Route | Positive change | Negative change |
|---|---|---|
| Direct, Undergone | Grateful | Hurt |
| Onlooker, Sympathy, Imitation | Approving | Indignant |
| Antipathy | Pleased | Envious |
| Kind, Spill, Association | Approving | Wary |
| Accused, Confronted | | Wronged (innocent) |
| Shame, and the guilty subject | | Ashamed |
| P44 bonus | Reconciled | |

No rule reads sentiments. Regard decides; the only use is S10's log lines.

## 5. Steering rules (slice 0c.2; each one is a no-op when `Steer` is false)

**S1. Who an act is aimed at** (law 4; III P25, VERIFY).
- `Candidates(kind, actor, m)`: everyone other than the actor who is awake, `Free`, in the same place, within `_po.NearTiles` (5), with line of sight > 0, in name order.
- Weight: `TargetWeight = (TargetBase + max(0, pull)) x (1 - max(0, -pull))`, where `pull = Sign(Joy) x E(actor, o)`.
  - Love gives and helps.
  - Hate argues.
  - People argue less with those they love: a housemate at 0.6 weighs 0.08 against a stranger's 0.2.
- The draw: `Rng.Unit(_seed, "target", kind.Name, actor.V.Name, m.ToString())`.
- When Steer is off, the observe slice draws from the same stream with weight 1 and no `Free` filter. It changes no other draw.

Where this applies:
- In `StartActs` and `StartScheduled.Able`, a Chosen kind is offered only to actors with at least one candidate.
- In `Begin`, a Chosen or Kin target is made busy with the actor (`BusyUntil`).
- In `FinishActs`, the Chosen or Kin target, if awake in the place, perceives the act at clarity 1 and identifies the actor (they took part).

**S2. Power tilt** (law 1; rule 15). In `StartActs`, the actor's weight is `p.V.Acts[kind] x max(0, 1 + TiltScale x Tilt x (Power(p) - 0.5))`. The same function is used for the sum and for the walk. The joyful give and help more; the sad drink more. Town-wide rates are unchanged.

**S3. Gossip close tie** (rule 8). The `KnowsBonus` applies when `Familiarity(listener, named) >= KnowsAt` **or** `St(listener, named) >= CloseTieAt`.
- At the seed this adds no pair: every seeded pair at 0.4 or more already has familiarity 0.5 or more.
- It is the first fallback if the 0a band slips (section 12).

**S4. The mayor** (rules 9 and 16).
- `Trust(from) = from == mayor ? 1 : clamp(0.5 + 0.5 x fam + CredencePerRegard x (1 - U(mayor)) x St(mayor, from), 0.25, 1)`. For Lewis (understanding 0.6) that is at most plus or minus 0.1.
- `close = household || (fam >= SwayCloseAt && St > -LoveAt) || St >= SwayLoveAt`. At regard 0 this equals today's test.
- `SwayChance` stays 0.02.

**S5. Reporting** (III P25, VERIFY).
- `Willing(v, actId, culprit)`: chance = `(ReportBase + ReportPerBoldness x B) x (1 - max(0, e)) + ReportPerHate x max(0, -e)`.
- `e = culprit is null ? 0 : St(v, culprit)`.
- The `"willing"` stream is unchanged. Kin still never report.

**S6. Suspicion and guesses** (III P26, VERIFY; law 9).
- `PieceTogether` orders by `1.2 - fam - SuspectPerRegard x St(who, n)`.
- The `Guess` weight is `fam + 0.05 + GuessPerHate x max(0, -St(observer, n))`, used both for the total and in the loop.
- A witness who saw "someone, a young man" has K[young man] < 0, so young strangers rank first. This is law 9 working through E.

**S7. Who confronts** (rule 9, "preferring the person harmed"; III P25).
- From `members` (whose count is unchanged), drop anyone with `St(n, target) >= CoverAt`.
- Order by `Boldness x Current x (1 + ConfrontPerHate x max(0, -St(n, target)))`, then by name.
- If nobody is left, there is no confrontation this tick.
- The person harmed feels the most and so hates the most. They are preferred without a special case.

**S8. Grievance** (rule 17). In `Temptation`:
- `grievance = GrievanceWeight x max(0, -St(v, keeper of p.Place) - GrievanceAt)`, added to the motive.
- `why` is the largest of want, need, thrill and grievance. Ties keep that order, so grievance is named only when it is strictly the largest.
- Kin are not exempt: Abigail can take from her father's till after rows.

**S9. Groceries** (rule 10's life choices; rule 12).
- `_shopOf` starts from `Economy.GroceriesAt`, with Since = 0.
- `WeekCost` and the grocery loop in `Payday` read it.
- At the start of `Payday`, for each household in ordinal order, other than the store keeper's own:
  - Consider its members of stage Adult or Elder. Skip the household if it has none.
  - Store pull: `S = mean St(a, storeKeeper)` over those members.
  - Chain pull: `Cp = ChainPull x MartDiscount x (1 + NeedPressure(first member))`, which is 0.2 to 0.4.
  - If at least ShopHoldDays have passed since the last choice:
    - at the Store, switch to the Mart if `Cp - S >= ShopMargin`;
    - at the Mart, switch to the Store if `S - Cp >= ShopMargin`.
  - Log `{m} shop {h} {from} {to} (store {S:0.00}, chain {Cp:0.00})` and record it.
- In practice:
  - a comfortable household leaves the Store at S <= -0.1;
  - a Mart household returns at S >= 0.5.
- No household switches at the seed.
- Money still moves only through `Move` and `ToOutside`.

**S10. Why lines** (principle 5). When a Chosen target is drawn, or a theft's motive is grievance, and the actor holds a sentiment toward the target or keeper, log `{m} why {actor} {kind} {target} {Name} since d{Since/1440} act {ActId}`, citing the strongest such sentiment.

## 6. Hook sites (verified at b53bce8)

| File, method (lines) | Change |
|---|---|
| `Model.cs` Temperament (52), ActKind (159-179), Act (183), Belief (195-205) | 2.1 |
| `Feelings.cs` (new) | `FeelingOptions`, static `Feelings` (pure math) |
| `Simulation.Feelings.cs` (new) | 2.3 state; `StartFeelings`, `Feel`, `Undergo`, `Accused`, `KinHears`, `KinPublic`, `Corroborate`, `Confirm`, `Move`, `Revert`, `AddMood`, `Company`, `CloseFeelings`, `PickTarget`, `TargetFor`, `ShopChoice`, `St`, `E`, accessors |
| `Simulation.cs` ctor (196-236) | Last parameter `FeelingOptions? feelings = null`. `_fo = feelings ?? (places is null ? DefaultTown.Feelings() : FeelingOptions.Off)`. Worlds built by tests stay off unless the test passes options. `StartFeelings()` runs after `_fam`. |
| `Simulation.cs` Run (269-320) | 0c.0: save `CultureInfo.CurrentCulture`, set Invariant, and restore in `finally`. Fill the 2.4 fields. |
| `Simulation.cs` Step (325-354) | `_now = m;` first. No other change of order. |
| `Simulation.cs` StartActs (679-703) | S1 candidate filter; S2 weight (one weight function for sum and walk) |
| `Simulation.cs` StartScheduled (705-734) | `Able` needs a candidate for Chosen kinds (Steer) |
| `Simulation.cs` Begin (736-745) | `string? target = null, int about = -1`; F2; `_did`; S1 busy target; S10. Log suffix ` to {target}` only when Enabled and a target exists. |
| `Simulation.cs` FinishActs (783-817) | S1 participant at clarity 1; `Belief.Target` (F3); `SeenKind` for unnamed, unguessed witnesses; then `Undergo` for Patient.Actor kinds (F12) |
| `Simulation.cs` Guess (820-833) | S6 weight |
| `Simulation.cs` Add (835-841) | `string? teller = null`. Read the prior belief before overwriting. After `CurfewBroken`: `if (_fo.Enabled) Feel(who, b, prior, teller, m)`. |
| `Simulation.cs` Socialise (845-878) | When together: `_together[i,j] += TickMinutes`. After `_chatted.Add`: `Company(pa, pb)`. |
| `Simulation.cs` TryTell (889-938) | S3; told belief copies `Target` and `SeenKind`; both `Add` calls pass `teller`; held branch: `Corroborate` and `KinHears` before `return`; new branch: `KinHears` after `Add` |
| `Simulation.cs` CheckScandals (942-970) | S7; after recording: `Accused(target, act.Id, [by], ConfrontJoy, "confronted")` |
| `Simulation.cs` CloseDay (972-985) | `CloseFeelings(day, days)` after `CloseMoneyDay()` |
| `Simulation.Authority.cs` Report (120-156), Willing (158-159) | `Willing(p.V, act.Id, b.Actor)`; S5 |
| `Simulation.Authority.cs` Interview (226-261) | `Begin(m, kind, s, false, target: by.V.Name, about: actId)`; after `_interviews.Add`: `Accused(suspect, actId, namers from known, AccusedJoy, "interview")` |
| `Simulation.Authority.cs` Trust (263), Review (268-298) | S4; after `PayUp`: `Confirm(keeper, actId, accused)` when the place is not the Mart |
| `Simulation.Authority.cs` Deliver (302-330) | `Begin(..., target: by.V.Name, about: actId)`; `Accused(accused, actId, namers from Known(actId), AccusedJoy, "verdict")` |
| `Simulation.Suspicion.cs` PieceTogether (66-92) | S6 order |
| `Simulation.Family.cs` Rows (35-51) | `Begin(m, kind, c, false, target: keeper, about: actId)` |
| `Simulation.Traces.cs` CheckTraces (178-209) | The Found belief carries `Target: act.Target` for Keeper kinds |
| `Simulation.Habits.cs` SeenOutLate (106-124) | `_did.Add((s.V.Name, act.Id))` when Enabled. OutLate has no Affect row, so nothing else changes. |
| `Simulation.Money.cs` StartMoney (85-95), WeekCost (107-112), Payday (115-156) | `_shopOf` initialised and read; `ShopChoice(m)` first in Payday (S9) |
| `Simulation.Money.cs` Temptation (273-300) | S8; S10 |
| `Simulation.Money.cs` WantPressure, NeedPressure (253-264) | Read by `Cond` (F16) and `Excuse` (F6); unchanged |
| `DefaultTown.cs` Acts (103-137), Cast (235-348) | 2.5; new `Feelings()`, `Tensions()` |
| `Metrics.cs` | `FeelingStats`, `RegardSpread`, `Metrics.Feelings(runs, kinds)` (section 10) |
| `UnderGlass.Run/Program.cs` | Flags and the feelings block; `feelings:` passed to every `new Simulation` |

## 7. Run-loop order

No step is added to the minute loop. The new work happens inside existing steps:

```
Run: [culture -> Invariant]  per minute m:
  if 00:00: Payday (Mondays) [ShopChoice first]; Wants
  Step(m): [_now = m]
    Live (each person)                       [Begin(Collapsed) -> F2]
    tick: StartActs [S1 filter, S2 tilt, S1 draw]; Temptation [S8]; Drinks
    StartScheduled [S1 Able]
    Watch
    FinishActs [S1 participant, Target/SeenKind -> Add -> Feel (F3-F11, F15, F14); then Undergo (F12)]
    tick: Socialise [_together; Company; TryTell -> Add/Feel, Corroborate, KinHears]
          CheckScandals [S7; Accused(confronted)]
          CheckTraces [Found + Target -> Feel]
          See; PieceTogether [S6 -> Add -> Feel]
          Authorities: Report [S5]; Interviews [Begin+About; Accused]; Review [S4; Confirm]; Deliver [Begin+About; Accused]
          Rows [Begin+About]
    CheckLate
  if 23:59: CloseDay -> ForgetSightings; CloseMoneyDay; CloseFeelings [F17]; holder counts
[culture restored]
```

## 8. Determinism and RNG

- **One new stream:** `"target"` with parts (kind, actor, minute). It is drawn in both observe and steer modes. Named streams are keyed by strings, so no existing draw shifts. Every other stream is unchanged, including `"willing"`, `"guess"`, `"tempt"`, `"actor"` and `"act"`.
- **No randomness** in `Feel`, `Undergo`, `Accused`, shame, drift, mood, the shop choice or ties.
- **No `Math.Pow`, `Exp`, `Tanh` or `Log` in new code:**
  - the repeat discount and the sentiment fade use repeated multiplication;
  - mood uses a linear window;
  - the squash is rational.

  The existing `Math.Cos` (body clock) and `Math.Pow` (chat chance) remain as before.
- **Iteration order:**
  - arrays in index order, which is ordinal name order;
  - kin through `Family.Keys.OrderBy(ordinal)`;
  - namers ordinal;
  - dictionaries are only looked up by key, never iterated unsorted.
- **No `string.GetHashCode`.**
- **Culture.** 0c.0 makes `Run` format in the invariant culture. Measured at b53bce8: the same seed under `de_DE` today hashes `b0bd3c691b30da7c` instead of `e7f6653087ff7e18`, because juiciness prints as "1,5". The README's "hashes the same on every machine" becomes true.
- **Mind-reading audit for code review:**
  - only `Did` reads the truth, and only for the subject's own acts;
  - `Feel` resolves the patient and cause from the belief alone;
  - target choice and the participant rule read positions only as perception at the moment;
  - TryTell's tie reads the listener's regard, the same approximation it already makes with the listener's familiarity (noted as a risk).
- **The model.** Laya plays no part in 0c. Every feeling is a typed number; no text is written.

## 9. Tests

There are two new files: `FeelingMathTests.cs` (pure) and `FeelingTests.cs` (scenes). The existing 51 tests stay unchanged: their worlds default to `FeelingOptions.Off`, and the default-town tests only check determinism, ranges and conservation.

The scene convention, unless a test says otherwise:
- GossipTests' hall (40x6, "Room"), wander 0, `Body(100, -1)`;
- `Temperament(1, 0.5, 0.5, 0.6)`, so Sensitivity and Retention are 0.5: sens = 1, and keep = 1 below 0.7;
- Kind "villager", age 30;
- keepers through `new AuthorityOptions { Keepers = {["Room"] = "Kim"} }` with no mayor;
- `feelings: new FeelingOptions { Start = ... }`;
- the test's own ActKinds carry Affect rows;
- scheduled acts at 10:00 on day 0.

Exact values are read from `SimResult.Feelings` (`Raw` and `Change` as applied), because the night's drift moves the values at the end. Values at the end are only compared with inequalities.

**Slice 0c.0**

| # | Name | Setup | Assertion |
|---|---|---|---|
| T1 | TheLogReadsTheSameInEveryCulture | Seed 42, 1 day, run once with `CurrentCulture = de-DE`, once Invariant. Skip if de-DE can't be created. | Equal `LogHash`; culture restored after Run |
| T2 | FeelingsOffReproducesTheRunsFromBeforeFeelings | `new Simulation(42).Run(7)`; `new Simulation(7, scheduled: new[] { Harness.ScandalFor(7, DefaultTown.Acts()) }).Run(14)`. From 0c.1 both pass `feelings: FeelingOptions.Off`. | `e7f6653087ff7e18` and `388d128d7fd4cdf2`. Update only when a deliberate 0a or 0b change lands first. |

**Slice 0c.1, pure**

| # | Name | Assertion |
|---|---|---|
| T3 | Saturate_KeepsRegardInRange_AndDampsOnlyTheWayItLeans | Saturate(0.8, 0.2) = 0.84; (0.8, -0.2) = 0.6; (-0.5, -0.5) = -0.75; (0, d) = d; a 41x41 grid over [-1, 1] stays in range |
| T4 | TheLawFiveTable | Joy -0.5, sens 1: patient -0.5; lover 0.6: -0.15; hater -0.6: +0.075; neutral L 0, U 0.5: -0.0375; neutral L 1: -0.075. Joy +0.2 with a hater -0.6: -0.03. |
| T5 | ImitationGrowsWithLikeness_AndShrinksWithUnderstanding | `Likeness` gives 1 for same household, stage, kind and job place, 0 for none; abs(f0) at U 0.9 < abs(f0) at U 0.1 |
| T6 | BlameFollowsFreedom_AndAKnownHardshipExcuses | phi at Freedom 0 = 0; Excuse(housemate, need 1, U 1) = 1; Excuse(other household) = 0 |
| T7 | SevereFeelingsIgnoreRetention | Keep(-0.8, 0.2) = 1; Keep(-0.3, 0.2) = 0.7; Keep(-0.3, 0.8) = 1.3 |
| T8 | CredenceEqualsTodaysTrustAtNeutralRegard | Credence(fam 0.25, E 0, U 0.6) = 0.625; E +0.8 gives 0.705; E -0.8 gives 0.545; floor 0.25 |
| T9 | HateConqueredByLove_BonusIsBoundedByTheLoveGiven | ReconcileBonus(T -0.3, Q 0.315) = 0.3; (T -0.3, Q 0.1) = 0.1; (T -0.1, any Q) = 0 |
| T10 | MoodFadesLinearlyOverThreeDays_AndPowerStaysInRange | One entry of -0.6 weighs 1, 2/3, 1/3 and 0 at 0, 1, 2 and 3 days; PowerOf stays in [0, 1] over a grid |
| T11 | AccusedSplitsOverNamers_AndTheGuiltyFeelShameInstead | Innocent with 2 namers: each raw = -0.3 / 2 x 0.5 = -0.075. Guilty: no raw; mood x (1.5 - SR). |

**Slice 0c.1, scenes**

| # | Name | Setup | Assertion |
|---|---|---|---|
| T12 | ObservingFeelingsChangesNoActOrBelief | Default town, seeds 1-10 x 14 days, `FeelingOptions.Observe` against `Off` | Acts equal as (Id, Tick, Actor, Kind, Location, At, Injected); every belief equal as a canonical string of its 0a fields with Chain and Suspects joined (records compare lists by reference); Interviews, Verdicts, Confrontations, Motives and TownCash equal |
| T13 | RegardIsSeededFromTheCard | Default town and one test cast with `Start` | Pierre->Caroline 0.6; Shane->Marnie 0.6 (housemate); Lewis->Marnie = Marnie->Lewis = 0.4; Alex->Lewis 0; anyone <-> Newcomer 0; `Start` sets both regard and baseline |
| T14 | TheKeeperWhoSawTheThiefHatesThem_AFarStrangerBlamesAKind | Stole row (Target, -0.5, 0.5, 1, Keeper). Tom (3,2), Kim keeper (5,2), Far (10,2) at 7 tiles. | Kim: Change -0.25, Hurt citing act 0, PowerByDay[Kim][0] < 0.5. Far: no Tom entry; Kind entry raw = -0.05625 x 0.3 x 0.5 x 0.75; Regard(Far, Tom) < 0 at the end. |
| T15 | CloserWitnessesFeelMore | Ann (5,2) and Wes (7,2) both list Tom as a friend (familiarity 0.5, so both name him); Kim is the keeper | Change(Wes->Tom) / Change(Ann->Tom) = 0.6 (1e-9) |
| T16 | FeelingsFollowThoseWeLoveAndHate | Ann Start->Kim 0.6, Bea -0.6, Cal neutral, all within 2 tiles of Tom's theft | Changes toward Tom: Ann -0.075, Bea +0.0375, Cal -0.028125. Mood: Ann < 0 < Bea. Sentiments: Indignant (Ann, Cal), Pleased (Bea). |
| T17 | SomeoneBecomesANameAndTheFeelingMovesOntoIt (story 1) | T14 plus Ann at (5,2) naming Tom; `ChatChance 10` so Ann tells Far | Far's Reattributed entry Change = -(Kind entry Change) to 1e-12; then a Tom entry with Basis HeardName and Raw = f0 x 0.5 x 0.3 x conf x 0.5 (conf read from Far's final belief); K[Far, villager] equals only the new Spill entry |
| T18 | AFeelingFollowsTheBelievedCause_WhenAGuessIsCorrected | Gus (bold 0.8, self 0.3) at 7 tiles guesses Nat (pick a seed where he does); Ann, a housemate of Gus, named Tom and tells him (0.9 > 0.8 replaces) | PersonalRegard(Gus, Nat) back to its value before the act (1e-9); Gus->Tom < 0; Nat's sentiment from the act removed |
| T19 | HearsayMovesMoodOnly_UntilASecondIndependentTellerAgrees | Ann and Bea both see Tom close up; Cal stands 9 tiles from the act and within 8 of both | First Told entry for Cal: Mood < 0, Change 0. After Bea's telling: Corroborated entry, Raw = f0 x 0.25 x conf x 0.5. A teller whose chain contains Ann or Bea adds no regard entry. |
| T20 | APublicWarningSeenFirstHandConfirmsHearsay | Mayor May, `ReportBase 1`; Ann reports and is told to Cal; Cal stands by when May warns Tom | Before the warning: Cal->Tom unchanged. After: a Confirmed entry, Raw = f0 x 0.5 x conf x 0.5. |
| T21 | AMishapSaddensFriendsButBlamesNobody | Stumbled row (Actor, -0.1, 0, 0) by Sam; Ann Start->Sam 0.6 within 2 tiles | Ann Mood < 0; Sam Undergone mood -0.1; no `Felt` with Change != 0 |
| T22 | ADrunkSceneTold_IsFeltThroughTheWitnessWhoSawIt | DrunkScene (Onlookers, -0.15) seen by Wit; Lis (Start->Wit 0.6) and Str (likeness 0 to Wit) are told by Wit | Lis's route Sympathy, Str's route Imitation; abs(mood Lis) > abs(mood Str); neither changes regard |
| T23 | AnInnocentSuspectResentsWhoeverNamedThem_TheCulpritIsAshamed | SuspicionTests.Questioning (confess 0) with rows and feelings on | Wes, named by Tom: Accused entry toward Tom < 0, Wronged citing act 0. Tom: Ashamed (toward Tom), no Accused regard entry. Tom's Undergone change toward May = Wes's x (1 - U(Tom)). |
| T24 | TheConfrontedCulpritIsAshamed | GossipTests' confrontation scene (Bob rummages), rows on | c.Target is Bob and correct; Bob holds Ashamed; no Confronted regard entry from Bob |
| T25 | ShameFallsOnTheCulpritsKin_MoreWhenItIsPublic | Kid (15) steals at Kim's; Pat, his parent, is in "Away". (a) One neighbour tells Pat. (b) Three neighbours with distinct chains tell Pat. | Shame steps: (a) 1, (b) 3 or more and at most ShameCap; Change(Pat->Kid) in (b) <= 3 x (a); Pat holds Ashamed; Pat never reports or retells (no account from Pat naming Kid; no "told Pat" line for the act) |
| T26 | AScandalCostsTheCulpritsFamilySomeStanding | T25's theft seen by Wil (not kin) | Association entry Wil->Pat: Raw = 0.2 x (1 - fam(Wil, Pat)) x 0.75 x Raw(Wil->Kid) |
| T27 | AGrudgeFadesSlowlyApart_FasterTogether_NeverPastTheBaseline | Bob argues at Ann (test row Joy -1, Plastic 0.3, so raw -0.3), then (a) separate rooms, (b) together all day, (c) U 0.9 against U 0.1 | (a) Ann->Bob at the end of day k = -0.3 + 0.005(k+1). (b) Day 0 (slighted) -0.295, then +0.015 a day, never above 0. (c) Heals faster at 0.9. |
| T28 | AFriendshipHoldsOnDaysTogether_AndFadesApart | One gift raises Bob->Ann to 0.06 | Apart: -0.005 a day down to the baseline. Together: unchanged. |
| T29 | ThePowerOfActingFollowsJoySadnessAndConditions | (a) No Affect rows; (b) money scene, purse -200 against +1000; (c) one gift | (a) Power = 0.5 at every day end. (b) Short purse: Power = 0.5 - 0.15; full: 0.5. (c) The recipient's Power rises, then is 0.5 again from day 3. |
| T30 | EveryRegardChangeCitesAnActItsHolderKnows | Default town, seeds 1-3 x 28 days | Each `Felt` with Change != 0 cites an act that the holder holds a belief about, or is the actor of (Undergone), or was questioned, delivered or confronted for (in Interviews, Verdicts or Confrontations); every Sentiment.ActId < Acts.Count |
| T31 | RegardAndPowerStayInRange_AndTheSameSeedGivesTheSameRun | Default town, seeds 1-3 x 28 days | R and K in [-1, 1]; PowerByDay in [0, 1]; same seed gives the same hash; seed+1 differs |

**Slice 0c.2, scenes**

| # | Name | Setup | Assertion |
|---|---|---|---|
| T16b | (gift variant of T16) | Scheduled GaveGift by Tom; Start Tom->Kim 1.0; TargetBase 0.001 | `Acts[0].Target == "Kim"`; Ann warms to Tom (Approving); Bea cools (Envious) |
| T32 | AGiftGoesToSomeoneInReach_WarmsThem_AndRepeatsCountLess | Ann (GaveGift PerDay 20) and Bob alone in the room | Every gift's Target is Bob; Raw 0.06, 0.03, 0.015 within 7 days; first Change 0.06; Grateful citing a gift; a gift 8 days later counts in full |
| T33 | ArgumentsGoToTheDisliked_GiftsToTheLoved | Five within 5 tiles; Ann has Argued and GaveGift at PerDay 20; Start Ann->Bob -0.6, Ann->Cal +0.6; 2 days | Ann's arguments: Bob the most, Cal the fewest. Her gifts: Cal the most, Bob the fewest. Bob->Ann falls with Hurt. |
| T34 | AFeudGrowsWhenArgumentsGoBothWays | Ann and Bob, Argued PerDay 10, Start -0.25 both ways; Cal and Dee neutral in reach; 7 days | Ties has (d, Ann, Bob, "feud"); Ann's arguments go to Bob more than to Cal or Dee |
| T35 | HateConqueredByLoveEndsAbovePlainFriendship | Room 1: Dem argues at Seb (Joy -1, raw -0.3), then three helps from Dem on day 0 (Joy +0.6: raw 0.18, 0.09, 0.045). Room 2: the same helps from Dem2 to Kit with no argument. | Seb->Dem = 0.315 (crosses at +0.015; bonus min(0.3, 0.315)); Kit->Dem2 = 0.28758; Seb > Kit; Seb holds Reconciled citing the third help; a "reconciled" tie |
| T36 | TheSadDrinkMore_TheGladGiveMore | DrunkScene PerDay 20; Pam (purse -200, Power 0.35) and Gil (purse 1000) with equal weight; 50 seeds x 2 days | Pam is drawn more often than Gil |
| T37 | TheHarmedConfront_AndNobodyConfrontsSomeoneTheyLove | GossipTests' confrontation scene plus Kim as keeper among the holders, equally bold; variant with Zed (bold 0.9) at Start->Bob 0.6 | `c.By == "Kim"`; in the variant, `c.By != "Zed"` |
| T38 | TheMayorTrustsTellersHeLikes_AndIsSwayedOnlyByTheClose | Pure `Feelings.Close` and `Credence` | Household: close at any regard. fam 0.6 at regard -0.5: not close. fam 0.25 at regard 0.5: close. fam 0.6 at regard 0: close (as today). |
| T39 | LoveCoversAndHateReports | Pure `ReportChance` | Regard 0 gives base + perBold x B; 0.6 gives x0.4; -0.5 gives +0.15 |
| T40 | PrejudiceRanksStrangersOfAKindFirst_AndFadesWithAcquaintance | Far saw "someone" of kind "young man" (Tom); later a second "someone" scandal with Yan (young man) and Ola (old woman), both strangers, around | Far's suspects list Yan before Ola. After hours together, abs(Regard - PersonalRegard)(Far, Yan) <= (1 - fam) x abs(K) and shrinks. |
| T41 | AGrudgeAgainstTheKeeperIsAMotive | MoneyTests.Tempted(0) with WantChancePerDay 0 and Kim as keeper elsewhere; (a) Start Abi->Kim -0.8, (b) 0 | (a) Motives has (Abi, "grievance"), and a why line. (b) No theft in the same window. |
| T42 | AHouseholdThatTurnsOnTheKeeperShopsAtTheChain_AndMoneyStillAddsUp | MoneyTests' payday scene with Kim as keeper, run 36 days; (a) Start Ann->Kim -0.3, (b) -0.05, (c) a Mart household at +0.6 | (a) A switch at the day-28 payday and the Mart price paid after it. (b) No switch. (c) Returns to the Store. TownCash end - start = OutsideIn - OutsideOut (1e-6). |
| T43 | FamiliesStillCover | FamilyTests' scenes with `new FeelingOptions()` against `Off` | The same Accounts, Confrontations and "told" lines |
| T44 | FeelingSteeredActsSayWhy | Bob gives Ann a gift, so Ann holds Grateful; then Ann gives | Log has `why Ann GaveGift Bob Grateful` |

## 10. Runner

**Flags** (`Program.cs`):
- `--feel off|observe|on` (default on);
- `--off sympathy|imitation|reconcile|kinds|freedom|presence|association|shame`, which can be repeated;
- `--plastic <x>` (PlasticScale);
- `--target-base <x>`;
- `--log <seed>` also prints the regard, sentiment, why, shop and tie lines.

The existing 0a and 0b lines are printed unchanged in every mode. With `--feel off` they are byte-identical to today's.

**`Metrics.Feelings(runs, kinds) -> FeelingStats`**, printed as a `feelings:` block:

```csharp
public sealed record RegardSpread(int Season, double MeanChange, double P5, double P50, double P95,
    double UnderMinus02, double AtLeast04, double Moved01, double KinMean, double NonKinMean);
public sealed record FeelingStats(int Runs,
    double MeanPower, double PowerSpread, double LowPowerShare, double HighPowerShare,
    IReadOnlyList<(string Name, double Power)> LowestPower,
    IReadOnlyList<RegardSpread> BySeason,
    double FeudsPerYear, double KinFeudsPerYear, double FriendshipsPerYear, double ReconciliationsPerYear,
    IReadOnlyList<(string A, string B, int Seeds)> TopFeuds, IReadOnlyList<(string A, string B, int Seeds)> TopFriendships,
    double SeedsWithFeudAndFriendship, double WarTowns, double DeadTowns,
    IReadOnlyDictionary<string, double> SentimentsPerSeason, double SentimentShare,
    double DropWitnessed, double DropToldOnce, double DropCorroborated, double DropConfirmed,
    double ShameStepsPerScandal, double KinDropPerScandal,
    double InnocentsResentingNamer, double WrongConfrontDrop, double BystanderRegardForConstable,
    double GiftsToLoved, double ArgumentsToDisliked, double ArgumentsInHouseholds,
    double NewsPerYear, double TriviaPerYear,
    double GrievancePerYear, double ShopSwitchesPerYear, IReadOnlyList<double> HouseholdsAtChainBySeason,
    IReadOnlyDictionary<string, double> MeanKindRegard, double RegardTowardNewcomer, double RegardFromNewcomer);
```

What the fields measure:
- **Power** is read at day ends. "Low" is under 0.3 and "high" over 0.7. The five lowest are expected to include Pam.
- **The spread of regard** covers the 650 ordered pairs at each season end. "Change" is measured from the baseline. Kin and non-kin pairs are reported separately.
- **Ties** are counted a year, from `Ties`, with the top pairs across seeds.
- **War town:** at any season end, more than 10% of ordered pairs (kin included) are below -0.2.
- **Dead town:** at year end, fewer than 5% of ordered pairs have moved 0.1 or more from the baseline.
- **`SentimentShare`** is the share of the total abs(regard change) that came with a sentiment.
- **The drops toward a culprit** are the mean Change toward a scandal's actor among holders, by Basis.
- **`InnocentsResentingNamer`** is the share of innocents questioned who end at -0.1 or below toward a namer.
- **`BystanderRegardForConstable`** is the mean R - B toward the constable at year end, over non-kin.
- **Law 4 checks:**
  - the share of gifts and favours that go to someone the actor holds at 0.4 or more;
  - the share of arguments that go to someone held below 0;
  - the share of arguments inside a household.
- **News and trivia a year** are compared with 0a's 95 and 524, since targeted acts now need someone in reach.
- **Performance:** run time for 400 seeds x 14 days, on against off.
- **E2:** with each `--off`, report feuds, friendships, confrontations at the right person, the reported share, grievance and shop switches. A law stays if removing it moves one of these by 20% or more.

## 11. The phase gate

| # | Check | Run | Today | Pass |
|---|---|---|---|---|
| 1 | 0a band: witnessed placed scandals in 40-70% over 3+ days, by sight and gossip | `--seeds 400 --days 14 --inject` | 57% | **>= 52%** (about 1.5 standard errors at ~220 witnessed runs; aim 55-60%), and **<= 28%** over 70% (23% today) |
| 1b | Authority | same | decided right 99%, reported 87% | right >= 95%; reported 77-97% |
| 2 | 0b: largest unexplained change in town cash; tempted scandals | 100 x 112 | 0.000 g; 2.8 a year | **0.000 g**; **1.5-4 a year**, with grievance **<= 1** |
| 3 | E0 | tests | | T2 hashes hold with Off; T12 (observe) identical; T30 and T31 pass |
| 4 | E1 for feelings (no player) | 200 x 112 | | **>= 60%** of seeds with a new non-kin feud *and* a new friendship; war towns **< 5%**; dead towns **< 5%** |
| 5 | Spread of regard at each season end | 200 x 112 | | under -0.2: **1-5%** of ordered pairs; mean change from baseline **-0.03 to +0.05**; moved 0.1 or more: **5-25%** |
| 6 | Tests | | 51 | every existing test and T1-T44 pass, including the stories (T14-T17, T23, T25, T34, T35, T40-T42) |
| 7 | Sid's read | `--log` on 10 seeds | | Sid reads each seed's 10 strongest sentiments with their causes, its shop switches and its ties, and says whether they read true |

News and trivia a year, and performance under 1.5x, are reported but are not gates.

## 12. Tuning order

Each step runs 200 x 28 natural, then 400 x 14 placed. Record the sweeps in `sim/README.md`, as 0a's were.
1. **Steer on with `--plastic 0`.** Regard stays at the seed, so only S1's reach and participant rules, target choice at the seed, and S2 act on the town. Check checks 1 and 2, and news and trivia. If the band slips here, the cause is participants or the reach rule; measure each.
2. **`--plastic 1`.** Bring check 5 into its bands with `DriftPerDay` and `ContactHeal`.
3. **E1:** sweep `TargetBase` (0.05-0.3) and `--plastic` (0.5-2), then the Joy values of Argued, GaveGift and HelpedSomeone. If feuds or friendships stay under target at the top of the sweep, bring rule 10's desire gate forward from 0d. Do not add constants.
4. **If check 1 fails:**
   1. drop the regard half of S3 (`CloseTieAt = 2`);
   2. then set `SuspectPerRegard = 0`;
   3. then measure each S rule off in turn.

   Leave `RetellFactor` and `ScandalFadePerDay` alone: the band was tuned on them.
5. Set `GrievanceWeight` and `GrievanceAt` for check 2.
6. Set `ShopMargin` for 0-2 switches a year.

## 13. Deferred, with reasons

- **Law 7 (norms, praising and badmouthing, spite in which story is told) and rule 7's reactions.** Acts are still drawn at town-wide rates, so a learned norm has nothing to steer. Spite changes which story is told, and so the 0a band. Both come with the rule 10 desire gate.
- **Rule 10 as a general desire gate.** This covers cost and intensity, attempt slots, close calls tilted by mood, Laya, pity leading to help, envy at a visible purchase (B's NewThing), and Law 1 as each villager's own rate of acting. These replace PerDay rates, so they change news and trivia volume and the 0a band. 0c exposes Mood and Power for the gate.
- **Law 13 (wonder).** It adds juiciness and needs a gossip retune.
- **Rule 4's third-slight mark.** It cannot happen under rule 10's 3-day hostile cooldown (question 6).
- **Known hardship beyond housemates.** This needs a confession that states its motive, or promises and debts (rule 13).
- **Witnessed acts with juiciness 3:** confrontations and reconciliations as such acts, and LeftForChain. They compete with scandals in TryTell, and LeftForChain's actor choice would read minds.
- **Regard in the constable vote.** The vote is at noon on day 0, before any act has moved regard; a regard term would only repeat the seed, which familiarity already carries. It belongs with section 6a's power.
- **Familiarity falling 1% a day (rule 5), and forgetting weighted by regard (rule 3).** Both move the 0a band.
- **Avoidance and haunts chosen by regard.** They change who meets, and so the 0a band. This is the first follow-up after the gate.
- **Courting and jealousy, secrets and Betrayed, promises, prices by regard, loving or hating places or the chain as an institution.**
- **Lifestyles and aspirations; sentiments read by decisions.**
- **Saving regard.** The sim has no save format.
- **The act rows in `acts.json`.** They move with 0d.
- **The regard-pumping bots** (MaxRegard, GiftSpam, CrashThenRescue, Saint) come in 0d. F8's bonus is bounded by the love actually given (risk 4).

## 14. Risks

1. **0a drift.** Participants raise news reach; the reach rule changes news and trivia counts; S3 and S6 change listeners and suspects. All are measured in step 1 of section 12, with the fallback order given there.
2. **A dead town.** Gifts are about 3 a day town-wide and mostly go to the loved, while drift pulls toward the baseline, so new non-kin friendships may be rare. The levers are tuning step 3, then the rule 10 gate.
3. **A war town, or households going sour.** Measured today: 59% of arguments had someone within 5 tiles, and 39% had a housemate there. The `(1 - max(0, -pull))` factor cuts arguments toward a housemate at 0.6 to 0.4 of a stranger's weight. Squabbles have Plastic 0.05 and Freedom 0.5, about -0.003 each against +0.005 a day of drift. Kin are counted in the war check.
4. **Pumping P44.** Insulting someone and then giving to them yields at most the depth of the old hate, and never more than the love given since. Checked by the bots in 0d.
5. **Hearsay stays local.** Under rule 9, one telling moves only mood, so most of the town turns on a culprit only after corroboration or a public consequence. That is the intent; the `Drop*` metrics show how local it stays.
6. **The constable becomes disliked.** Indignation plus Questioned rows move bystanders' regard, but at about -0.002 a questioning. `BystanderRegardForConstable` watches it.
7. **Kinds are coarse.** "Young man" covers Alex, Sam, Sebastian and Shane, so prejudice falls on innocents of the same kind and may raise the innocent share of interviews (86% today). That is law 9 working; watch it.
8. **About 50 constants.** The ablation switches, `--plastic` and the tuning order keep this tractable.
9. **Spinoza references are from memory** (III P2 schol., P21-P27, P27 cor. 1, P40 schol., P43, P44, P46, P49; V P3, P6; Def. Aff. 25-26). Every code comment that cites one carries VERIFY until it is checked against Curley's translation.
10. **TryTell reads the listener's regard,** as it already reads the listener's familiarity. This should become the teller's own belief about the listener later.
11. **The pinned hashes tie T2 to b53bce8.** Any deliberate later change to 0a or 0b updates them on purpose.

## 15. Questions for Sid

1. **Starting tensions.** The town ships with none, so every dislike comes from the run. First guesses: Pierre and Shane both ways (the chain store), Sebastian toward Demetrius, Abigail toward Pierre. Which do you want?
2. **III P24.** Someone who hates the victim comes to *love* whoever harmed them, not only feel glad. Follow Spinoza? Built: yes.
3. **III P27 cor. 1 (indignation).** A neutral witness cools a little on whoever harmed someone like them. Rule 6 says "toward the target only". Built: yes.
4. **Should the constable tell an innocent suspect who named them?** Built: yes, so they resent the namer (Wronged).
5. **Retention.** Pam is at 0.2, the mod's value. Robin is at 0.8, a guess. Confirm Robin's number?
6. **Rule 10's 3-day hostile cooldown makes rule 4's "third slight within 5 days" impossible.** Should the cooldown become 1 day per pair, or should the window widen? The mark is deferred until you decide.
7. **The P44 bonus.** Love after conquered hate gets back up to the depth of the old hate, bounded by the love given since. In T35 that ends at 0.315 against 0.288 for a pair that never fought. Too strong or too weak?
8. **E1.** A feud is -0.3 or below both ways; a friendship is 0.4 or above both ways (the seeds' own levels); the target is 60% of seed-years; feuds inside a family are reported separately and not counted. Right?
9. **Should love cover the way kinship does?** Built: someone who loves the culprit at 0.4 or more never confronts them and is less likely to report them.
10. **Shop choice.** Built as one choice per household, since there is one purse. Should each adult choose instead?
11. **Sensitivity values.** Can the prototype use the values derived from Stardew's dialogue, on the understanding that they are replaced with the original cast?
12. **The mayor's trust.** Should it lean on regard? For Lewis that is at most 0.1 either way. Or should it stay on familiarity only?

## 16. Docs in the same PR

**`sim/README.md`:**
- a "Phase 0c: feelings" section with the gate numbers, the tuning sweeps and the new test count;
- the culture note;
- `--feel` added to the commands.

**`docs/under-glass/design.md`:**
- "As built in 0c" notes under rules 4, 5, 6, 8, 9, 10, 16 and 17;
- the law 5 table, with the III P24 and P27 cor. 1 readings marked as awaiting Sid;
- law 8 with the P44 formula;
- section 11's 0c line.

The mod's `NpcSchedules.sln` and docs are untouched.

---

## Appendix A. Flaws the judges found, and their fixes

| Flaw in A | Fix |
|---|---|
| No contact healing (rule 5) | F17: +0.01 x (0.5 + U) on a day together with no slight; company keeps a friendship |
| 24-hour half-life of mood, `Math.Pow` | F16: a linear 3-day window, no Pow |
| Law 1 conditions leave out company, rest and health | F16: company joy (mood only); rest and health enter as events |
| Kinds only for an unnamed actor | F9b: the III P46 spill for known but unfamiliar people |
| A told act whose patients are the onlookers treats the listener as the onlooker | F3: the witness the story came from is the patient |
| The resentment rule reads `_cases` only | F13: `known` and `Known(actId)` include `_carried` |
| The shop rule counts the keeper's own household; no hold time | S9: the keeper's household is excluded; 28-day hold; 0.3 margin |
| Unscaled reciprocity, Mutual and an escalation multiplier make siblings feud | All three removed; reciprocity is the direct attribution plus S1; Squabbled Plastic 0.05 and Freedom 0.5 |
| The war check counts non-kin only | War towns and spread bands include kin; feuds inside a family reported separately |
| Housemates as likely as anyone to be argued with | S1: `(1 - max(0, -pull))` |
| `Credence` rewrites `Belief.Confidence`, moving verdicts from day 0 | `Belief.Confidence` keeps 0a's formula; Credence weights feelings and the mayor's trust only |
| The mayor's sway drops the familiarity path | S4: a union that equals today's test at regard 0 |
| The "feelings off" test is weak; the reach filter is not tied to the switch | T2 pinned hashes; every new behaviour gated by Enabled or Steer; worlds built by tests default to Off |
| The dead-town line passes too easily; no bound on spread | Gate check 5's bands; dead town = under 5% of pairs moved |
| Re-attribution is approximate | F11: recompute and diff, exact reversal (T17, T18 to 1e-9 and 1e-12) |
| 13 sentiment names | 10 names (F18) |
| About 30 constants; feuds may be too rare | Ablation switches, sweep flags, tuning order, rule 10 gate as the fallback |
| No pity leading to help | Deferred with the rule 10 gate, as one judge advised |

**Where this spec does not follow a judge's advice:**
1. **S3 keeps rule 8's union with love,** instead of keeping eligibility on familiarity alone. The union adds no pair at the seed (verified above), and it is the first fallback.
2. **The P44 bonus is `min(abs(T), love given since)` at share 1.0.** One judge suggested 0.5 x abs(T) x (1 - R); another required love after conquered hate to end above a plain friendship. The formula meets the second requirement (T35) and stays bounded by the love actually given.
3. **The vote's Appeal gets no regard term,** for the reason given in section 13.
4. **No Mutual flag and no reciprocity term.** Both judges' runaway findings point to them.

## Appendix B. Measured on b53bce8 for this spec

**Pinned hashes, invariant culture:**
- seed 42 x 7 days: `e7f6653087ff7e18`;
- seed 7 x 14 days with `Harness.ScandalFor(7, DefaultTown.Acts())`: `388d128d7fd4cdf2`;
- seed 42 under `de_DE`: `b0bd3c691b30da7c`, so today's log depends on the culture.

**A probe of 24 seeds x 28 days, positions at the start of each act:**

| Act | A year | Nobody within 5 tiles | A housemate within 5 |
|---|---|---|---|
| GaveGift | 334 | 49% | 27% |
| Argued | 44 | 41% | 39% |
| HelpedSomeone | 41 | 42% | 32% |
| Squabbled | 25 | 1% | 93% |
| DrunkScene | 8 | 27% | 15% |

About 2,075 beliefs a run-year, of which 768 are told.

**Other numbers:**
- One seed-year runs in about 7.4 s on one thread.
- Seeded state: 86 of 650 ordered pairs at 0.4 or more.

The probe ran from the scratchpad (`/tmp/claude-0/-home-user-StardewValley-AI-Decisions/7396f398-ce38-53a3-ad27-2419289a1d17/scratchpad/probe/`). Nothing in the repo was changed.
