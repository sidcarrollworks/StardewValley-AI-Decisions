> Draft spec, 2026-10-08. **Built:** m-0, the scenario harness and the reach measures (`claude/reach-checks`; as built in 5.6); m-1, story events part 2 (`claude/story-events-2`; as built in 6.7); m-2, forked runs and V7 (`claude/forks`; as built in 6.8). Nothing else in batch 2 is built.
>
> It merges three designers' drafts of that day: **acts** (batch 2's kinds and their engine pieces), **reach** (festivals, the reach levers, the first new scandals, the scenario harness) and **twists** (story events, the variety measures, the deal). It builds step 2 of section 4 of `actions-and-twists.md`, with the measures of its step 0, and section 9 of `specs/acts-spec.md`, and keeps the acts spec's conventions: `ActGate`; the Light, Story and Ledger classes; a switch per slice; watch mode; E2.
>
> **Code base: `claude/checkpoint-5` (`c7b3c6e`).** Every file:line below was checked against it; the designers read checkpoint-3 and -4, and references that moved were corrected. Paths are under `sim/UnderGlass.Sim/` unless named; `Program.cs` is `sim/UnderGlass.Run/Program.cs`.
>
> **Built, and used here, not specified again:**
> - **acts-0** (#59; `sim/README.md`, "The act catalog: acts-0"): `ActGate` and `ActOptions` with seven switches (`Acts.cs`); `ActCatalog.Batch1`, `Kinds`, `Cards`; `GateOf`, `Served` and `ActsFor`, which try a motive's acts dearest first, ties by name (Simulation.Desire.cs:309, 330, 374); `Fits`, `Audience` and the pride term (`Simulation.Acts.cs`); `Act.With`; Remorse, Defend, Curious, Accepted, Refused (Model.cs:318, 332); `--catalog`; the replay's `ActOptions.*` scalars; `CatalogTests` (11).
> - **Story events and V1-V6, V8** (#56; `Variety.cs`; `sim/README.md`, "Variety"). Not built: V7, family pooling in V1, the fair-twist rule.
> - **The corrected `TownMetrics`** (#57) and **the 60-person town**, generator version 2 (#58). Open PRs: **the canonical town hash** (#60: appended fields with defaults no longer move a town's hash) and **scaled gates for grown towns**, with `--town-seeds` (#61).
>
> **Not built when this was written:** batch 1's acts-1 to acts-6 (Thanked, Complimented, Joked, PlayedGame, TreatedToDrink, Welcomed, Apologised, Comforted, Mocked, StoodUpFor, LateForWork), and with them `AfterAct`, `CatalogWatch`, the warm budget, the cold reading and the apology's answer; `StoryMetrics` comes with acts-0's follow-up. Batch 2 comes after them (1.3). *Since then* (checkpoint-7): `StoryMetrics` (#62) and acts-1 to acts-4 (#64-#67, all off) are built; acts-5, Sides, is a draft that fails its gates (#69); acts-6 is not started.
>
> **Numbers.** Costs, joys, plastic shares, thresholds and juiciness are first guesses for the sweeps. *Measured*: the reach designer's read-only probe on checkpoint-3. *Probe*: the twists designer's throwaway program (checkpoint-3 plus the variety code). The variety baseline (6.3) is checkpoint-5's. "Who" comes from the cast's traits in `DefaultTown.cs`. Recalled Stardew facts are marked VERIFY; the festival dates were read from the game's data (Calendar.cs:5-7).
>
> **Where the drafts disagreed,** this spec chose one festival design, one home for switches (`ActOptions`) and rows (`ActCatalog`), one writer for unseen acts, the creditor's motives stirred by the debt rule (so hearsay about a Given act still stirs nothing, D34), one news ceiling, and C13 reported, never gated. Each choice is stated where it applies.

# Under Glass: the act catalog, batch 2 (places and promises): implementation spec

## Sid's words (2026-10-08)

- "We also need to expand the amount of actions a person can do." (acts spec)
- A scandal "could reach 100% of the town if it was really public or very limited if it was resolved or covered up quickly." (town spec, answer 2)
- Take inspiration from The Sims, its mods and other life sims, with "lots of twists and turns", so new games don't feel too similar. (acts spec, Sid's answers)

**His answers, as used here:** grow Pelican Town from 60 (every kind runs in the shipped 26; 0.1 says what grown towns add); reach depends on context, so scenario checks (section 5) replace the fixed 40-70% band, now only reported (C13); forgetting follows the tie's strength (built in T1); gate acts come from traits (4b), so no kind needs a card; hearing about yourself counts at corroboration strength (5b, 2.7); lies and vandalism come last, off by default (7b), so Lewis never lies; places and promises come next (8a): this batch; news may rise at most 10% (9a; 8.1).

---

## 0. What this builds, and why

**Four gaps:**
1. **Nobody goes to a person.** People go to work, haunts, hubs and home (`Decide`, Simulation.cs:616-678); a motive toward someone out of reach waits (`Weigh`, Simulation.Desire.cs:463). No visits, errands or meetings.
2. **Nothing has a future part.** Rule 13's promises (design.md:224-227) are not built: no dates, tabs or loans.
3. **Reach depends only on who saw the act.** Both scandals come from temptation (DefaultTown.cs:117-122); a confrontation starts no act (Simulation.cs:1188-1194); a verdict is seen only by whoever stands near (Simulation.Authority.cs:322-351); nothing makes a story smaller.
4. **Runs repeat.** Over 200 seed-years of the shipped town (6.3): Pam's scandal in 92% (V1), 2.7 headlines in effect against a target of 20 (V2), no twist in the median season (V6).

**Batch 2 builds** places and promises (8a: Visited, JoinedThem, Invited and its promise, OfferedPeace, MadePeace, MadeScene, Banned, the counter); kinds on the same machinery (Hosted, RanUpTab, LentMoney, KeptFoundItem, ReturnedLostItem, EnteredContest, and what follows); festivals as town-wide gatherings (dates in Calendar.cs:23-29); two reach levers, Announced (a confrontation in public, a verdict read out, a debt called in, a post on the board) and Settled (paid back and forgiven before the second retelling); three scandals, S4 (a debt that falls due unpaid), S9 (a humiliation at a festival) and S6 (the mayor's let-off found out); the first twist, the deal (each seed draws its own start at day 0); and step 0's missing measures: the scenario harness, V7, family pooling and the fair-twist rule. The measures change no behaviour.

**In all:** 29 act kinds and a ledger tally; five motives (Seek, Peace, Need, Owed, Envy); the records `Promise`, `LostItem`, `Announcement`, `FestivalDay`, `Scenario`; movement goals to a door or a person (2.5), the Ask made general, an appointment rule, sound through walls, bans and festivals.

**Dice make situations, minds make choices.** A lost purse, a contest skill, a forgotten date and a dealt start are dice; going, inviting, keeping, forgiving and offering peace are choices.

**Guarantees:** with every switch off, every pin holds (7.1); `Harness.ScandalFor` places the same scandal whatever is on (2.2).

### 0.1 Slices, and where each kind works

The slices, with their switches, needs and criteria, are in section 9: b2-0 to b2-18 and b2-z (the act slices), m-0 to m-3, d-1 to d-6 and m-z (the harness, the deal and the measures).

Every kind runs in the shipped 26: homes are rooms behind doors on roads (DefaultTown.cs:63, 76-88), and a 10×8 home holds a party of 8; the Store, Mart and Saloon have keepers (DefaultTown.cs:275-278). What grown towns add:
- **At 31:** five more homes; the Blacksmith and FishShop keepers can ban (Towns.cs:91), but sell no groceries.
- **At 60:** front steps within 8 tiles, so neighbours see who calls on whom and hear each other's rows; more strangers; the Square's 13×13 crowd holds about 3.5 people per 10 tiles, so `Venues` can move a festival to a district green; C12 (a neighbourhood's scandal stays local, a festival's crosses districts). The Cafe and Workshop keepers, and rival pairs such as the Cafe against the Saloon, need `BanPlaces` and `Rivals` entries or E8's shop roles (town spec T6).

---

## 1. Who builds what

### 1.1 Two builders (issue #46)

- **The local agent builds the act slices**, b2-0 to b2-z, as L9, unless the cloud session claims one on #46 first: every rule inside the minute loop (kinds, motives, festivals, the reach levers, the scandals), each behind its switch.
- **The cloud session builds the harness, the town pieces and the measures:** m-0 to m-3, d-1 to d-6, m-z (the scenario harness, story events, the variety measures, forks, the deal). It owns the harness, `TownData` and the runner's town flags (`--town`, `Make`) from T0-T4. It has built acts-0 (taken over from the local track) and the variety measures.
- **New files.** Local: `ActCatalog.Batch2.cs`, `Promises.cs`, `Reach.cs`, and `Simulation.Ask`, `.Promises`, `.Visits`, `.Peace`, `.Loud`, `.Bans`, `.Counter`, `.Festivals` (festivals, blunders, contests), `.Parties`, `.Debts`, `.LostItems`, `.Reach` (announced, settled, amends, let-offs, the board). Cloud: `Scenarios.cs`, `Simulation.Scenarios.cs`, `ReachMetrics.cs`, `Variety.Events.cs`, `Deal.cs`. Each side writes its tests (7.3, 7.4). Shared files and each hook: 2.12.

### 1.2 Rules between them

- In a shared file, a change is a field appended with a default that keeps today, or a one-line hook into the editor's own file. Nobody moves, reformats or renames there; whoever merges second rebases.
- Nobody edits `DefaultTown.cs` (acts spec 12). `PinnedTests.cs` takes new tests only.
- **Local gives:** `BeginUnseen`, `Broke`, `Settle`, `ToRead`, `Post` and a way to start CalledInDebt (the bodies of the follow-ups); the kinds' names; `Promise` and `ActOptions.StartPromises`; the new `SimResult` fields.
- **Cloud gives:** `RecordCircle` (whoever lands second adds its call in `BeginUnseen`); `Scenario`, and each local slice's check before that slice merges; `_forceSway`; each new kind's story event (m-3).

### 1.3 Order, and what batch 2 needs from batch 1

- **The cloud track starts now.** m-0, m-1, m-2 and d-1 need nothing from batch 1 or 2.
- **The act slices come after acts-1**, which brings `AfterAct` (the dispatch after an act ends), `CatalogWatch` (the watch record) and the rules for light kind acts (the warm budget, the cold reading). If b2-0 must start first, it builds `AfterAct` and `CatalogWatch` as the acts spec defines them (acts spec 3, 6).
- **`Ask`** (2.3) makes acts-4's apology answer general. Whichever of b2-0 and acts-4 lands second wires the apology in.

| Batch 2 | Needs from batch 1 | Uses when on, works without |
|---|---|---|
| b2-0 | acts-0 (built), acts-1 | acts-4 |
| b2-1 Visits | acts-1 (light kind acts, the cold reading) | acts-3 (Curious: welcome visits), acts-4 (Remorse: a visit to apologise) |
| b2-2 Dates | | acts-4 (the no-show's Remorse) |
| b2-7 Amends | acts-4 (Remorse, Apologised, the answer) | |
| b2-8 Scenes, b2-16 Contests | | acts-5 (Mocked, between MadeScene and Argued) |
| b2-10 Counter | | acts-6 (FoundItClosed follows LateForWork's pattern) |
| b2-15 Blunders | | acts-4 (an apology settles a scene), acts-5 (Comforted) |

The 60 town (built) is needed only by C12 and the rerun at 60 (8.4).

---

## 2. Records and seams

b2-0 (local) and m-0 (cloud) build these with no change in behaviour. Every value and field is appended, with a default that reproduces today.

### 2.1 Records

```csharp
// Acts.cs: ActGate, after acts-0's Form, Min, Serves, Light, NeedsCard, PrideWeight,
// MinFamiliarity, MinRegard, MinAudience
    bool Loud = false,          // heard through walls, up to HearTiles, without identity (rule 2)
    bool HeardStirs = false,    // 5b: hearsay about the holder stirs at corroboration strength
    Going Going = Going.None,   // reached by walking: to a door (a visit) or a person seen
    bool Asked = false,         // the holder asks; on a yes the person asked acts (a loan)
    bool KeeperOnly = false,    // only the place's keeper, toward a customer (a ban)
    bool ToEnemy = false,       // exempt from "nobody is kind to someone they dislike"
    bool Sized = false,         // joy scales with Act.Amount over the debtor's weekly costs
    double MinExpression = 0    // the actor's expression must reach this (a scene)
public enum Going { None, Door, Walk }

// Model.cs
public sealed record Gathering(..., double Visitors = 1,          // :147-149
    YearDay? Date = null,       // on only on this day of each year
    bool Holiday = false);      // while on: nobody works or patrols, no other gathering is on
// On (:151-153) gains && (Date is null || Calendar.DateOf(Clock.Day(minute)) == Date)
public sealed record ActKind(..., int ToMinute = Clock.MinutesPerDay,  // :202-219
    bool? Reportable = null,    // reported and judged (rule 16); null: IsScandal
    bool Placeable = true)      // Harness.ScandalFor may place it
{ public bool IsCrime => Reportable ?? IsScandal; }
public sealed record Act(..., string? With = null, double Amount = 0);                      // :239-240
public enum DesireKind { ..., Curious, Seek, Peace, Need, Owed, Envy }                       // :318
public enum Outcome { ..., Refused, Kept, Broken, Reconciled, Forgiven, Won, Lost }          // :332

// Promises.cs
public enum PromiseKind { Date, Party, Tab, Loan }                           // append only
public enum PromiseState { Open, Kept, Broken, Forgiven, Lapsed, Refused }   // append only
/// Date, Party: By comes to Place in [DueFrom, DueTo), and To is there. Tab, Loan: By owes To
/// Amount by DueTo. Late: settled after DueTo. Made, MadeAct -1: made before the run (old debts).
public sealed record Promise(int Id, PromiseKind Kind, string By, string To, int Made, int MadeAct,
    string Place, Tile Spot, int DueFrom, int DueTo, double Amount = 0,
    PromiseState State = PromiseState.Open, int Settled = -1, int SettledAct = -1, bool Late = false);
public sealed record LostItem(int Id, string Owner, string Place, Tile At, double Value, int Dropped,
    string? Holder = null, bool Kept = false, int Exposed = -1);

// Reach.cs
public sealed record Venue(string Place, Tile Center, int Radius, int From, int To);
/// How: confronted, read-out, board, called-in.
public sealed record Announcement(int ActId, int Tick, string By, string How, string Place, IReadOnlyList<string> Heard);
public sealed record FestivalDay(int Day, string Name, string Place, int Came, int Awake, int Peak);
```

**`SimResult`** (Simulation.cs:169) gains, after `Familiarity`, fields that are not `required` and stay empty unless their slice is on: `Promises` (b2-2); `Bans` (Who, Place, From, Until; b2-9); `Visits` (Tick, Visitor, Host, Result: in, out, no-answer, shut; b2-1); `Items` (b2-14); `Contests` (Day, Contest, Entrants, Winner; b2-16); `Announcements` (b2-5); `Settled` (act → minute; b2-6); `FestivalDays` (b2-4); and the cloud's `Circles` (act → who knew its actor at familiarity `KnowsActorAt` 0.2+ when it began, in name order, for every scandal-tier and placed act; always written, read by no rule, so no hash moves) and `Scenarios` (act → scenario name). `Consequence.Ban` exists (Model.cs:282); batch 2 first uses it.

### 2.2 Five edits that keep the shipped town as it is

1. **Placed scandals:** `Harness.cs:17` filters `k.IsScandal && k.Placeable`. Every new scandal-tier row sets `Placeable: false`, so the pool stays {RummagedInBin, Stole} and `388d128d7fd4cdf2` holds; town spec 9's exception (a new scandal re-pins at rate 0) goes. A kind Sid wants placed re-pins.
2. **The authority's filters:** `IsCrime` replaces `IsScandal` in `Report` and `OwnAccount` only. Confrontation, the scandal index, kin shame, association, the keeper's motive and `Variety.Of` keep `IsScandal`.
3. **A Given deed resolves None**, not Open, in `BeganAct`. Shipped Given rows are `Patient.Actor` and return earlier (Simulation.Desire.cs:649-650). Without it, a refusal or repayment turns Ignored a week later.
4. **Computed motives:** the private `Motive` gains `bool Computed`, set by `FondMotives`; `Weigh` tests it where it tests `DesireKind.Fond` today. Fond's keys and draws don't change. Seek, Need and Pity for someone alone are computed each tick and share Fond's one close call a day per pair (`_fondAsked`).
5. **`JoyOf(act, row)`** = `row.Joy × (Gate.Sized ? clamp(Amount / debtor's WeekCost, 0.25, 1) : 1)`, in place of `row.Joy` in `Feel`, `Undergo` and `StirFrom`'s fallback. Shipped rows are × 1.

### 2.3 The Ask, made general

acts-4's apology answer becomes `bool Ask(int asked, int asker, AskKind what, int actId, int m)` (`Simulation.Ask.cs`), `enum AskKind { Apology, Door, Invitation, Party, Peace, Loan }` (append only). Regard is the asked person's for the asker; dislike = max(0, −regard); withdrawn = max(0, −stance).

| What | Obliged | Cost |
|---|---|---|
| Apology (acts-4) | 0.5 × max(0, regard) + 0.5 × familiarity + 0.3 × understanding | 0.2 + dislike × (0.5 + retention) |
| Door | 0.3 + 0.5 × max(0, regard) + 0.3 × familiarity | 0.2 + dislike × (0.5 + retention) + 0.3 × withdrawn |
| Invitation, Party | 0.5 × max(0, regard) + 0.3 × familiarity + 0.2 × chattiness, + 0.2 if Fond or Seek toward the asker | 0.15 + dislike × (0.5 + retention) + 0.3 × withdrawn |
| Peace | 0.5 × understanding + 0.3 × familiarity + 0.3 × own Peace intensity, + 0.2 within 3 days of the Winter Star | 0.2 + dislike × (0.5 + retention) + 0.3 × max(0, self-regard − 0.5) |
| Loan | 0.5 × max(0, regard) + 0.3 × familiarity + 0.3 × understanding | 0.2 + 0.3 × amount / own weekly costs + 0.5 × own household's need + 0.3 per broken promise of the asker's they know |

A margin more than `ClearBand` (0.15) from zero decides. Inside it, `FeelingOptions.CloseCall` (the Laya seam, Feelings.cs:146) decides if set, else the draw ("ask", what, act, asked) against `DesireMath.Tilted(CloseCallChance(margin), mood, hostile: false)` (Feelings.cs:491-499). An invitation that clashes with the asked person's job is a no that stings nobody and starts no act. After any other no the ask waits `AskAgainDays` (7 for invitations, 14 else). Log `ask {asked} {what} {asker} act {id} yes|no margin {m}`.

### 2.4 Unseen acts

`BeginUnseen(m, kind, actor, place, at, target, about, knowers)`, after `SeenOutLate` (Simulation.Habits.cs:54-74): writes the act and `Begin`'s log line; `_scenes[id]` around `place`; `_witnesses[id] = 0`; `RecordCircle`; with feelings on, `_did` and `BeganAct`; and each knower's belief (named, clarity 1, the caller's source and confidence). No `_watching` entry, no `BusyUntil`, no `Gains`. Used by StoodUp, LeftOffTheList, BoughtFromRival, BrokePromise, KeptFoundItem and LetOff; every other kind starts through `Begin` with its actor present.

### 2.5 Movement goals to a door or a person

- `Person` gains `VisitTo`, `JoinTo`, `Keeping` (a promise; all −1) and `(string Shop, int Until)? Bag`. Six `Why` values, kept by `Decide` like "haunt" and "home": "call", "visit", "hosting", "join", "appointment", "errand". Detention, bed, work and patrol come first.
- `Going(p, h, d, I, m, day)` (`Simulation.Visits.cs`), called where `Weigh` gives up on a subject out of reach (false while `Visits` is off), weighs JoinedThem if the subject is visible in the same place, else Visited, through the normal gate. A yes sets a goal (`Goal`, Simulation.cs:680-690), not an act; the motive stays until the two are in reach. Recorded as not acted on; log `go {holder} {kind} {subject}`.
- A door is the outside tile of the link into the host's home (`Link.DoorA` where `B` is the home); standing on it crosses nothing (`Walk`). A person is the tile beside them, re-aimed each tick while seen; the goal ends when sight is lost.
- `Arrived(p, m)` after `Arrive` (Simulation.Growth.cs:170) handles the knock, the counter, a meeting and a join.

### 2.6 Promises, and the appointment rule

- At most one open promise of each kind per pair. `ActOptions.StartPromises` (the deal's old debts) is empty by default and read once.
- **`Appointment(p, m)`**, in `Decide` after the job rule: anyone holding an open Date or Party promise goes to `Place`, `Spot` plus a keyed offset, from `AppointmentLead` (30) before `DueFrom` until `DueTo`. Each decides once, at leaving time; they don't go if their regard for the other fell below 0 since agreeing, or if ("forgot", promise, name) < `ForgetBase` (0.1) × (1 − retention). Bed, detention and work still win.
- **`Promises(m)`** each tick: meetings (MetUp), window ends (StoodUp, Lapsed), what falls due (BrokePromise, then Owed or debt Pity), and hand-overs that wait for two people in reach and free (repayments, returned items, party invitations), as `Rows` waits (Simulation.Family.cs:35-51).
- **`ClosePlaces(day)`** each night: peace stirrings, hosting choices, lifting bans, errand days, lost items.
- Trust from kept promises (rule 14) is batch 3's; the list keeps what it needs.

### 2.7 Sound through walls, and hearing about yourself

- **`Overhear(act, m, t)`**, `Loud` kinds only: each minute, an awake non-actor without sight of the act gets a heard instant of `HeardClarity` (0.15) if in the same place within `HearTiles` (12), or in a place linked by a door whose tile on the act's side is within 12 of the act while they are within 12 of the door's tile on their side (`_doors`, Simulation.cs:253). `AfterSeen` gives a hearer a belief with clarity from `Perception.OfAct`, no actor, target or kind of person, and juiciness × `HeardFactor` (0.5): a scene heard (1.75) is never volunteered. A telling that names the actor fills in "someone" (`TryTell`, Simulation.cs:1139-1140).
- **5b:** for `HeardStirs` kinds and stories in which the hearer is the target, `StirFrom` stirs a told belief as if seen at |`JoyOf`| × (0.5 + sensitivity) × `CorroboratedWeight` (0.25) × confidence, and `Presence` counts it corroborated (weight 0.25 × confidence). Every other told belief stirs nothing. Batch 3 reuses this.

### 2.8 Bans and festivals (the seams)

- **Bans:** `_bans[(person, place)] = until`. `Decide` leaves out haunts and hubs in a banned place without changing the draw's order; an errand goes to the rival; a banned drinker never reaches `Drinks` there; log `ban-lifted`.
- **Festivals:** `OnlyDay` (Model.cs:148) is a run day, `Date` a day of the year. `Holiday(m)` is true while a `Holiday` gathering is on; `Decide`'s work, patrol and gathering rules read it. No shipped gathering sets either field.

### 2.9 The cloud session's seams

- **m-0:** the constructor parameter `scenarios`, appended; `StartScenarios(m)`; `RecordCircle(act, kind, injected)` in `Begin` and `BeginUnseen`; `LeaveTrace` skips `NoTrace` scenario acts; `|| _forceSway.Contains(actId)` in `Review`, filled only by `sway`.
- **m-2:** `_seed` loses `readonly`; `Fork(day, salt)`; the swap in `RunDays` (6.3).
- **d-1, d-4:** `FeelingOptions.StartSentiments`, applied in `StartFeelings`; `TownData.Chemistry`.

### 2.10 Switches and constants

`ActOptions` gains these after acts-0's seven, and `ActOptions.Switches` (Acts.cs:73) lists them for `--catalog` and `With`. Off by default; `DefaultTown.Feelings()` turns none on. A switch whose dependency is off, or a rule whose row is missing, makes the constructor throw. Switches marked "no" act without the gate and with feelings off, like `Late`. **Watch** covers all: each rule records what it would do in `CatalogWatch` and starts no act, goal, promise, payment, gathering or announcement. Constants are first guesses.

| Switch (slice) | Gate only? | Needs | Constants |
|---|---|---|---|
| `Visits` (b2-1) | yes | | `VisitFrom` 10:00, `VisitUntil` 20:00, `VisitMinutes` 60, `VisitKnowAt` 0.4, `OutSeenAt` 6 ticks, `FailedDays` 28, one visit a day, `VisitAgainDays` 3, `VisitSpot` (5,5); `JoinMinutes` 30, `AloneTiles` 3, `SeekBase` 0.3, `SeekAfter` 90, `SeekFull` 180, `PityAlone` 0.3, `LowShown` −0.1 |
| `Dates` (b2-2) | yes | | 1-3 days ahead; 18:00-20:00 (15:00-17:00 under 18); `AppointmentLead` 30, `WaitMinutes` 30, `ForgetBase` 0.1, `AskAgainDays` 7 / 14 |
| `Peace` (b2-3) | yes | | `PeaceQuietDays` 14, `PeaceBase` 0.3, `PeaceShare` 0.5, `PeaceCooldownDays` 28, `PeacePride` 0.5 |
| `Festivals` (b2-4) | no | | `FestivalWeight` 20, `Venues`, `FestivalJuice` 0 |
| `PublicConfront`, `ReadVerdicts` (b2-5) | no | | `AnnounceFactor` 0.6, `AnnounceTiles` 8, `ReadAt` Market, `ReadAfterMinutes` 180, `Announcing` (Confronted, CalledInDebt) |
| `Settled` (b2-6) | no | | `SettledShare` 0.5, `SettleBefore` 2 |
| `Amends` (b2-7) | yes | `Repair`, `Settled` | `AmendsWeight` 0.3 |
| `MadeScenes` (b2-8) | yes | | `SceneMin` 0.6, `SceneExpression` 0.7, `HearTiles` 12, `HeardClarity` 0.15, `HeardFactor` 0.5 |
| `Bans` (b2-9) | yes | | `BanDays` 14, doubling; `KeeperOffence` 1.0; `BanPlaces` Store, Mart, Saloon, Blacksmith, FishShop |
| `Counter` (b2-10) | yes | money | `TripsPerWeek` 2, `CounterMinutes` 10, `BagMinutes` 30, `RegularTrips` 2 in 28 days, `Favour` 0.1, `SaleJoy` 0.005, `Rivals` (Store, Mart) |
| `Hosting` (b2-11) | yes | `Dates` | `HostBase` 0.4, `HostForm` 0.8, high spirits power 0.65+ for 3 nights, `PartySize` 6, 18:00-21:00, `HostEveryDays` 28; left off at regard 0.4, familiarity 0.5 |
| `Debts` (b2-12) | yes | money | `TabLimit` 60g, `TabGraceDays` 7 after the next payday, `TabFloor` −0.2, `ForgiveAt` 0.4, `OwedDays` 14 |
| `Loans` (b2-13) | yes | money, `Debts` | `NeedAt` 0.5, `LoanDays` 28, `LoanCap` and `LenderReserve` 2 weeks of costs |
| `Items` (b2-14) | yes | money | `LostPerDay` 0.004, 10-60g, `FindTiles` 2, `FindPerHour` 1.0, `RecogniseAt` 0.5, `Honesty` 0.6, `KeepScale` 2, `NoticePerHour` 0.1 at home, 0.2 for the owner in public, `ItemDays` 56 |
| `Blunders` (b2-15) | no | `Festivals` | `BlunderChance` 0.03, `HumiliatedAt` 5, `HumiliatedJoy` −0.3, `Shaming` (Blundered, MadeScene, SoreLoser) |
| `Contests` (b2-16) | yes | `Festivals` | `CompeteBase` 0.3, `EnterForm` 0.4, skill 0.7, effort 0.3, noise 0.2, `EnvyBase` 0.3, `EnvyDays` 2 |
| `Partiality` (b2-17) | no | | `LetOffAskDays` 5 |
| `Board` (b2-18) | no | | `BoardPlace` Square, `BoardSpot` (2,4) (VERIFY: the game's board stands by Pierre's door; the Store's door is the Square's (0,5), DefaultTown.cs:67), `ReadTiles` 3, `ReadChance` 0.5, `PostDays` 7 |

"Money" is `HasMoney` (Simulation.Money.cs:85). The replay writes every bool, int and double of `ActOptions` (Replay.cs:350-370), so the new ones reach the settings and defaults with no change; list constants stay in code.

### 2.11 Catalog, runner, replay, determinism

- **Catalog.** `ActCatalog.Batch2(o)` gives the batch-2 rows that are on, in slice order; `Kinds(o, town)` returns the town's kinds, then `Batch1(o)`, then `Batch2(o)`, so no index moves (the replay's kind indices, Replay.cs:69). Every batch-2 row has `PerDay` 0, so `StartActs` skips it before drawing (Simulation.cs:802-803). New: `ActCatalog.Gatherings(gatherings, o)` appends the festivals when `Festivals` is on and `Watch` off; `ActCatalog.Town(TownData)` applies kinds, cards and gatherings, and the runner and replay call it in place of their inline calls.
- **Runner.** `--catalog` takes the new names; `--catalog-opt <Name>=<value>` sets any `ActOptions` scalar through `SetOption`, like `--forget`.
- **Replay.** Acts are written field by field (Replay.cs:210-215), so `Amount` changes nothing; the enum lists (:239, 245-246) carry the new motives and outcomes. Promises, bans, visits, items, contests, announcements and festival days are new top-level tables, written only when present. The viewer gets a VERB sentence per kind (section 3's lines), MOTIVE sentences, the outcome names, and announcements and settlements in the stories.
- **Determinism.** New draws are keyed: ("ask", what, act, asked), ("forgot", promise, name), ("errand-day", household, week, k), ("host", name, day), ("lost", name, minute), ("find", item, who, minute), ("keep", item), ("notice", item, who, minute), ("skill", contest, name), ("judge", contest, year, name), ("blunder", festival, name, day), ("blunder-at", …), ("read-board", post, name, minute), ("scene", key, minute), ("scene-actor", key, minute), ("deal", …), ("fork", seed, salt). Festival crowds reuse ("crowd-x"/"crowd-y", gathering, name, day) (Simulation.cs:652-653). No draw while a switch is off; no keyed draw shifts another; loops in name order; no `string.GetHashCode`.

### 2.12 Hooks in existing files

The cloud session owns the m- and d- hooks, the local agent the rest.

| File:line | Method | Hook | Slice |
|---|---|---|---|
| Acts.cs:12-14, 24-89 | `ActGate`, `ActOptions` | gate fields; switches, `Switches`, constants | b2-0 |
| ActCatalog.cs:24-29 | `Kinds` | `Batch2(o)` after `Batch1(o)`; new `Gatherings`, `Town` | b2-0 |
| Model.cs:147-153 | `Gathering`, `On` | `Date`, `Holiday`; the date test | b2-0 |
| Model.cs:202-219, 239-240, 318, 332 | `ActKind`, `Act`, `DesireKind`, `Outcome` | appended (2.1) | b2-0 |
| Harness.cs:17 | `ScandalFor` | `&& k.Placeable` | b2-0 |
| Simulation.cs:169 | `SimResult` | local fields; `Circles`, `Scenarios` | b2-0; m-0 |
| Simulation.cs:188-211 | `Person` | `VisitTo`, `JoinTo`, `Keeping`, `Bag` | b2-0 |
| Simulation.cs:213 | fields | `_seed` not `readonly`; `_forkAt`, `_forkSeed`; `Fork` | m-2 |
| Simulation.cs:265-270; Simulation.Town.cs:7 | constructors | `scenarios`, appended | m-0 |
| Simulation.cs:383 | `RunDays` | `if (m == _forkAt) _seed = _forkSeed;` after `_now = m;` | m-2 |
| Simulation.cs:395-450 | `RunDays` | fill the new `SimResult` fields | b2-0; m-0 |
| Simulation.cs:474 | `Step` | `Blunders(m);` after `StartActs(m)` | b2-15 |
| Simulation.cs:484 | `Step` | `StartScenarios(m);` after `StartScheduled(m)` | m-0 |
| Simulation.cs:486 | `Step` | `Promises(m);` after `FinishActs(m)` | b2-2 |
| Simulation.cs:490 | `Step` | `CountFestival(m); Confronts(m); ReadBoard(m);` after `CheckScandals(m)` | b2-4, b2-5, b2-18 |
| Simulation.cs:491 | `Step` | `CheckLostItems(m);` after `CheckTraces(m)` | b2-14 |
| Simulation.cs:555 | `Live` | `Arrived(p, m);` after `Arrive(p, m)` | b2-1 |
| Simulation.cs:637 | `Decide` | work rule `&& !Holiday(m)` | b2-4 |
| Simulation.cs:641-642 | `Decide` | `Appointment(p, m)`, then `Errand(p, m)`, before `Patrol` | b2-2, b2-10 |
| Simulation.cs:642 | `Decide` | `if (!Holiday(m) && Patrol(p, m))` | b2-4 |
| Simulation.cs:644 | `Decide` | keep the six new goals | b2-0 |
| Simulation.cs:647-648 | `Decide` | leave out banned places; gatherings `&& (!Holiday(m) \|\| g.Holiday)` | b2-9, b2-4 |
| Simulation.cs:873 | `Begin` | `RecordCircle(act, kind, injected);` after `_acts.Add(act)` | m-0 |
| Simulation.cs:929 | `Watch` | `Overhear(act, m, t)` after the sight loop | b2-8 |
| Simulation.cs:982 | `FinishActs` | `AfterSeen(act, kind, watchers, m);` after `_witnesses[act.Id] = witnesses`: hearers, announcing kinds, the humiliation | b2-5, b2-8, b2-15 |
| Simulation.cs:986 | `FinishActs` | batch 2's dispatch in acts-1's `AfterAct`, beside `StirPity` | all |
| Simulation.cs:1026 | `Add` | `Learned(who, b, m);` after `StirFrom` | b2-9, b2-11, b2-14 |
| Simulation.cs:1061 | `Socialise` | `_lastChat[i] = _lastChat[j] = m;` after `_chatted.Add` | b2-1 |
| Simulation.cs:1092, 1107, 1127 | `TryTell` | `TellingJuice(b, m)` for `Current(b, m)` | b2-6 |
| Simulation.cs:1105 | `TryTell` | `if (!MayTell(teller, listener, b)) continue;` | b2-6, b2-17 |
| Simulation.cs:1123 | `TryTell` | `Retold(best.ActId);` after `_told.Add` | b2-6 |
| Simulation.cs:1186-1188 | `CheckScandals` | `if (QueueConfront(act.Id, by, target, m)) break;` before the record | b2-5 |
| Simulation.cs:1207 | `CloseDay` | `ClosePlaces(day);` after `Forget(day)` | all |
| Simulation.Desire.cs:19-34, 453, 507-552 | `Motive`, `FondMotives`, `Weigh` | `Computed` (507, 513, 520-522, 552) | b2-0 |
| Simulation.Desire.cs:169-170, 183 | `StirFrom` | 5b; `JoyOf` | b2-0 |
| Simulation.Desire.cs:268 | `Window` | Seek, Need computed; Peace 7 days; Owed, debt Pity 14; Envy 2 | b2-0 |
| Simulation.Desire.cs:430-431 | `Pursue` | `SeekMotives`, `AloneMotives`, `VisitMotives`, `NeedMotives` beside `FondMotives` | b2-1, b2-13 |
| Simulation.Desire.cs:463 | `Weigh` | out of reach: `Going` | b2-0 (b2-1) |
| Simulation.Desire.cs:468-469 | `Weigh` | `ToEnemy` skips the dislike test | b2-3 |
| Simulation.Desire.cs:558 | `Weigh` | `Asked`: ask, then the person asked acts | b2-13 |
| Simulation.Desire.cs:671 | `BeganAct` | Given deeds resolve None | b2-0 |
| Simulation.Feelings.cs:99-101 | `StartFeelings` | apply `StartSentiments` | d-1 |
| Simulation.Feelings.cs:217, 491 | `Feel`, `Undergo` | `JoyOf` | b2-0 |
| Simulation.Feelings.cs:302-312 | `Presence` | 5b | b2-0 |
| Feelings.cs:298 | `FeelingOptions` | `StartSentiments`, after `Acts` | d-1 |
| Feelings.cs:455 | `SentimentName` | route "Peace" gives Reconciled | b2-3 |
| Simulation.Money.cs:103 | `TownCash` | plus lost items lying anywhere | b2-14 |
| Simulation.Money.cs:149-163 | `Payday` | groceries less the week's trips | b2-10 |
| Simulation.Money.cs:224 | `Drinks` | the tab; the bar's tally | b2-12, b2-10 |
| Simulation.Authority.cs:52 | `Authorities` | `ReadOut(m);` after `Deliver(m)` | b2-5 |
| Simulation.Authority.cs:120-122 | `Report` | `AskAboutCase(p, to, m);` at the top | b2-17 |
| Simulation.Authority.cs:136, 181 | `Report`, `OwnAccount` | `IsCrime` for `IsScandal` | b2-0 |
| Simulation.Authority.cs:138 | `Report` | a victim who settled doesn't report | b2-6 |
| Simulation.Authority.cs:296 | `Review` | `\|\| _forceSway.Contains(actId)` | m-0 |
| Simulation.Authority.cs:301-302 | `Review` | `if (letOff) { LetOff(actId, accused, m); continue; }` | b2-17 |
| Simulation.Authority.cs:303 | `Review` | `ToRead(actId, accused);` before the record | b2-5 |
| Simulation.Suspicion.cs:34 | `See` | `SawBag(o, s, m)` beside `Habit` | b2-10 |
| Simulation.Traces.cs:20 | `LeaveTrace` | return early for a `NoTrace` scenario act | m-0 |
| Town.cs:33 | `TownData` | `Chemistry` | d-4 |
| Replay.cs:20; 46-62 | `ReplayOptions`, `Record` | `Deal`; the `deal` block | d-1 |
| Replay.cs:52; 161-257 | `Record` | `ActCatalog.Town`; the new tables | b2-0; b2-2 on |
| Program.cs:101, 151, 180 | flags | `--catalog` names, `--catalog-opt`, `ActCatalog.Town` | b2-0 |
| Program.cs:194-202, 216-218, 448-455 | `Make`; sentiments; variety block | `--check`, `--deal`, `--forks`; act −1 prints "before the run" | m-0 to d-1 |
| Variety.cs:12, 38-45, 50-54, 80 | `StoryEvent`, `VarietyStats`, `Kinds`, `Of` | appended fields and parameters; new kinds | m-1, m-3 |
| sim/viewer/index.html | sentences | VERB, MOTIVE, outcomes; Stories tab; "How this town began" | each slice; d-1 |

---

## 3. The acts

**Reading the rows.** Class; tier, juiciness J and valence; read time / duration in minutes; where, when and age. Affect: patient, joy, plastic share, freedom (1 unless given), target, tilt. Gate: form cost / minimum intensity, "+h" the hostile surcharge (0.3) plus fear. Rates: none; every kind comes from a motive, a choice or a rule, with `PerDay` 0. A story travels by J (volunteer level 2, +0.5 for a listener who knows the person, Simulation.cs:13-14): under 1.5 never retold; 1.5-1.9 told on day 0 to listeners who know the person; 2+ is news.

### 3.0 The rows

| Kind (slice) | Class; tier J V; read/dur | Where, when, age | Affect | Gate; started by |
|---|---|---|---|---|
| Visited (b2-1) | Light; trivia 1.0 +1; 1/2 | the host's home, 10:00-20:00; 7+ | target +0.1, 0.1, Chosen, tilt +1; cold (host's regard < −0.2) −0.05 | 0.7 / 0.1, `Going.Door`, `MinFamiliarity` 0.4 (not for Curious); Fond, Pity, Curious, Seek, MakeUp, Remorse, Peace |
| JoinedThem (b2-1) | Light; trivia 0.5 +1; 1/2 | public places, not homes or the subject's workplace; 5+ | target +0.08, 0.1, Chosen, tilt +1; cold (< 0) −0.04 | 0.5 / 0.1, `Going.Walk`; Seek, Pity, Fond |
| Invited (b2-2) | Story; trivia 1.5 +1; 1/2 | public places; 10+ | target +0.1, 0.15, Chosen | 0.5 / 0.15, `MinFamiliarity` 0.3; Fond, MakeUp, Return (of form 0.5+) |
| MetUp (b2-2) | Story; trivia 1.5 +1; 1/2 | the promise's place and window | target +0.15, 0.25, Chosen | both arrive |
| StoodUp (b2-2) | Story; news 3.0 −1; 1/1 | the promise's place, at the window's end; `About` the Invited | target −0.35, 0.35, Chosen | unseen: the window ends |
| Refused (b2-2) | Light; trivia 1.0 −1; 1/1 | where the asking happened | target −0.1, 0.15, Given | the Ask's no |
| OfferedPeace (b2-3) | Story; trivia 1.5 +1; 2/5 | anywhere, on a visit too; 13+ | target +0.15, 0.2, Chosen; cold (< −0.5) −0.075 | 0.7 / 0.15, `PrideWeight` 0.5, `ToEnemy`; Peace |
| MadePeace (b2-3) | Story; news 2.5 +1; 1/5 | where the offer was | target +0.25, 0.4, Chosen | the Ask's yes; actor accepted, target offered |
| Confronted (b2-5) | Story; news 3.0 −1; 1/5 | where the two meet; no age limit | none (`Accused` carries it) | rule 9, in public (4.2) |
| MadeScene (b2-8) | Story, `Loud`; news 3.5 −1; 1/5 | anywhere; 13+ | target −0.4, 0.35, Chosen | 0.6 / 0.6 +h, `MinExpression` 0.7; Answer, Retaliate |
| Banned (b2-9) | Story; news 2.5 −1; 1/3 | a `BanPlaces` place; 18+ | target −0.35, 0.35, Chosen | 0.6 / 0.3 +h, `KeeperOnly`; Retaliate, Answer, Owed |
| Bought (b2-10) | Ledger | the counter | mood +0.005 both | an errand |
| FoundItClosed (b2-10) | Story; trivia 1.5 −1; 1/1 | a shop in posted hours with no staff | actor −0.05, 0.1, freedom 0.5, Given (the keeper) | arriving |
| BoughtFromRival (b2-10) | Light; trivia 0.5 −1; 1/1 | where the bag is seen | target −0.08, 0.3, Given | unseen: a bag seen |
| Hosted (b2-11) | Story; trivia 1.5 +1; 2/1 | the host's home, 18:00-21:00; host 18+ | onlookers +0.15, 0.2, no target, tilt +1 | a choice, then the guests |
| LeftOffTheList (b2-11) | Story; trivia 1.5 −1; 1/1 | where they learn of it | target −0.2, 0.3, Chosen, `HeardStirs` | unseen: learning of the party |
| RanUpTab (b2-12) | Story; trivia 1.5 0; 1/1 | the bar from 18:00; 18+ | none | `Drinks` |
| PaidOffTab (b2-12) | Story; trivia 1.5 +1; 1/1 | the bar, or where the debtor next meets the keeper | target +0.1, 0.2, Given | `Drinks`, `Promises` |
| BrokePromise (b2-12) | Story; scandal 4.0 −1; 1/1 | 4.5 | 4.5 | unseen: the due time |
| CalledInDebt (b2-12) | Story; news 2.5 −1; 1/3 | public, `MinAudience` 1; `About` the BrokePromise | target −0.3, 0.3, Chosen | 0.55 / 0.2 +h; Owed |
| ForgaveDebt (b2-12) | Story; news 2.0 +1; 2/3 | where they meet | target +0.4, 0.4, Chosen, `Sized` | 0.5 + 0.3 × amount / lender's weekly costs, min 0.15; Pity from an overdue debt |
| LentMoney (b2-13) | Story; trivia 1.5 +1; 1/2 | public; both adults | target +0.25, 0.3, Chosen, tilt +1, `Sized` | 0.5 / 0.2, `PrideWeight` 1.0, `Asked`; Need |
| Repaid (b2-13) | Story; trivia 1.5 +1; 1/2 | where they meet | target +0.15, 0.3, Given, `Sized` | a hand-over in `Promises` |
| KeptFoundItem (b2-14) | Story; scandal 4.0 −1; 1/1; `Reportable`, `Placeable` false | where it is noticed | target −0.3, 0.4, Chosen, `HeardStirs` | unseen: exposure |
| ReturnedLostItem (b2-14) | Story; trivia 1.5 +1; 1/2 | where finder (or keeper) meets owner | target +0.25, 0.3, Chosen, tilt +1 | a hand-over in `Promises` |
| Blundered (b2-15) | Story; news 2.5 0; 1/2 | 4.5 | 4.5 | a mishap at a festival |
| EnteredContest (b2-16) | Story; trivia 1.5 0; 1/1 | the festival's centre at its start | none | a choice |
| WonContest (b2-16) | Story; news 2.5 +1; 1/2 | the festival | actor +0.3, 0, freedom 0 | judging |
| SoreLoser (b2-16) | Story; news 2.5 −1; 1/3 | the festival, or later | target −0.2, 0.3, Chosen | 0.5 / 0.15 +h; Envy |
| LetOff (b2-17) | Story; scandal 4.5 −1; 1/1 | 4.5 | 4.5 | unseen: a let-off |

29 act kinds and a ledger tally; ten news, three scandals. Batch 2's Given rows stir no motive: they skip every branch of `StirFrom` (Simulation.Desire.cs:186-223).

### 3.1 Visits (b2-1)

- **Visited: "Emily called round at Haley's."** Any served motive toward someone out of reach; `VisitMotives` adds Fond toward someone loved and away, Seek at home, and acts-3's Curious toward someone not yet welcomed. Never kin or housemates; one visit a visitor a day; a pair once in `VisitAgainDays`. The visitor walks to the door and knocks; if the host is in and awake the door Ask decides; on a yes they stand at `VisitSpot` for `VisitMinutes`, the host's goal is "hosting", Visited begins, and the motive that brought them (a gift, an apology, a peace offer, comfort) is weighed as usual. The hour counts as a day together (`ContactMinutes`, Simulation.Desire.cs:778-781) and in 0d.6's company measure. The visitor believes the host is home at an hour unless they saw the host out then on `OutSeenAt` ticks (`_habits`, Simulation.Habits.cs:31, 43-49; sightings at home don't count) or called then within `FailedDays` and found them out ("out" from a housemate, or no answer). Who: Alex, Abigail, Haley, Pam and Sam clear the cost; Penny goes on a close call, about 4 in 10. A shut door is no act: the visitor's Did resolves Refused with `RefusedSting`. `SimResult.Visits`; log `call {visitor} {host} in|out|no-answer|shut`. Lines: "{actor} called round at {target}'s." / "{actor} knocked at {target}'s, and nobody was in."
- **JoinedThem: "Leah went over and sat with Penny on the beach."** The subject is visible (same place, 8 tiles, in sight); the joiner walks beside them, the act begins within 2 tiles, and they stay `JoinMinutes` ("join"); the person joined is not held. Driven by Seek, and by **Pity for someone alone and low**, computed each tick: a free holder at a haunt sees someone with nobody within `AloneTiles` whose shown mood (mood × `Show`, Simulation.Withdrawal.cs:349-350) is `LowShown` or lower; intensity `PityAlone` × (0.5 + sensitivity) × (1 + max(0, regard)) × min(1, −shown / 0.3). Line: "{actor} went over and sat with {target}."
- **Seek (new).** Computed each tick like Fond: `SeekBase` × (0.5 + chattiness) × min(1, alone / `SeekFull`) × 0d.6's dial (`Seeking`, Simulation.Withdrawal.cs:235-241), "alone" being free minutes since the last chat (`_lastChat`), from `SeekAfter`. At a haunt it aims at someone visible at regard 0.2+ and familiarity 0.4+ (JoinedThem); at home 10:00-20:00, at the best-liked such person whose home they know and who they believe is in (Visited). Hermits stay possible: chattiness scales Seek, the dial damps the withdrawn, the costs keep out the timid, and light kind acts stay out of 0d.6's kindness tally.

### 3.2 Dates (b2-2)

- **Invited: "Sam asked Penny to meet at the beach tomorrow."** The slot: the inviter's own free evening in 1-3 days, 18:00-20:00 (15:00-17:00 if either is under 18), at their haunt open then, else the Saloon (both 18+) or the Beach. When it ends the invitee answers the Ask: yes makes `Promise(Date)`, no starts Refused, a job clash is a quiet no. One open promise per pair. Who: the chatty and bold, and children after lessons. Did and Undergone stay Open until the promise ends (Kept, Broken, Refused). Line: "{actor} asked {target} to meet at {place} on {day}."
- **MetUp: "Sam and Penny met at the beach, as planned."** Both there, awake, within 8 tiles; the second to come is the actor; both stay to the window's end. Invited → Kept; MetUp pairs like a kindness. Line: "{actor} and {target} met at {place}, as planned."
- **StoodUp: "Alex never came, and Haley waited at the saloon."** One waited `WaitMinutes`+ and the other never came (neither: Lapsed, no act); the reasons are the appointment rule's. The waiter feels Answer, or MakeUp if they still love the other, and tells it; the no-show, holding the waiter at 0+, feels acts-4's Remorse ("missed it, then said sorry"). Invited → Broken; the two pair as a hostile act. Line: "{actor} never came, and {target} waited at {place}."
- **Refused: "Haley turned Alex down."** The Ask's no to an invitation, a peace offer or a loan, face to face; the asker takes `RefusedSting`; the asking act resolves Refused. Line: "{actor} turned down {target}'s {invitation|offer to make peace|request for a loan}."

### 3.3 Peace (b2-3)

- **Peace (new): MakeUp for a long feud.** Stirred nightly when the holder's regard is −0.3 or lower, familiarity 0.4+, not kin or housemates, no heavy hostile act either way for `PeaceQuietDays` (the holder knows, having taken part both ways, `_lastHostile`), no offer within `PeaceCooldownDays`. Felt `PeaceBase` × understanding × min(1, quiet days / 28) × (1 − 0.5 × retention), doubled within 3 days of the Winter Star (Calendar.cs:28); 7 days. Its own kind so the gate can exempt it from the dislike test and the life record names it.
- **OfferedPeace: "Sam tried to make it up with Shane."** `ToEnemy` skips the dislike test, not the avoidance test. Who: high understanding, low retention, modest pride: Leah, Evelyn, Demetrius, Maru, Harvey; rarely Pam, Haley, George. Resolves Reconciled or Refused. Line: "{actor} tried to make it up with {target}."
- **MadePeace: "Sam and Shane made it up."** Each side's dislike is halved through `Move` (Simulation.Feelings.cs:378-429) on the route "Peace", with law 8's bonus if regard crosses 0; fear hits (`_hits`), avoidance (`_avoid`) and open Answer and Retaliate between them clear; a "made-peace" tie is written; `SentimentName` maps "Peace" to Reconciled. Line: "{actor} and {target} made it up." Guard for E1: quiet feuds 14 days old only, one offer per pair in 28 days, median feud 28 days+.

### 3.4 MadeScene: "Shane threw his drink at Pierre." (b2-8)

Heavy hostile (cooldown, fear hit). `Served` tries the dearest act first, so a motive at 0.6+ in someone who shows feelings makes a scene, otherwise an argument. Who (expression, DefaultTown.cs:243-250): often Alex, Abigail, Haley, Pam, Sam; when angry enough George, Pierre, Robin, Lewis, Shane; never Penny, Harvey, Kent, Caroline, Demetrius, Jodi or Leah (under 0.7). Heard through walls as "someone was shouting at {place}". Lines: "{actor} made a scene at {target}." / at a bar in drinking hours "{actor} threw a drink at {target}." / heard only "Someone was shouting at {place}."

### 3.5 Banned: "Gus barred Shane from the saloon." (b2-9)

The actor keeps the place; the target is a customer, not staff, kin or a housemate. Driven by a theft at their place (the robbed keeper's Retaliate, Simulation.Desire.cs:205-212, which today can only argue, the legacy gate at :313); **the keeper's offence**, new in `Learned`: a keeper who witnesses a MadeScene or DrunkScene in their place feels Retaliate at |joy| × clarity × (0.5 + sensitivity) × `KeeperOffence`; and an unpaid tab (Owed). Love covers at 0.4, so Gus bars Shane, not Pam. `Consequence.Ban` for `BanDays` (doubled for a second); the target leaves at once (`Why = ""`) and `Decide` skips the place; news; the banned feel Answer toward the keeper; not on the mayor's ladder. `SimResult.Bans`; log `ban {who} {place} until {minute}`. Line: "{actor} barred {target} from {place} for a fortnight."

### 3.6 The counter (b2-10)

- **Errands.** Each household that buys groceries makes `TripsPerWeek` trips to its shop (`ShopOf`, Simulation.Money.cs:119-120) on keyed days, by its first adult or elder by name with no job that day (else whoever finishes earliest), in the shop's posted hours (the keeper's job hours and days off), never in festival hours. The shop's own family eats at cost (:154-155).
- **Bought (ledger).** A 28-day tally of (customer, shop, keeper), never retold: the shopper within 3 tiles of the counter while staff work. A trip pays `WeekCost / TripsPerWeek` × (1 − `Favour` × the keeper's regard) (rule 12): at the Store, `Move` to the keeper's household plus restocking through `ToOutside`, as `Payday` does (:158-159); at the Mart, `ToOutside`. `Payday` charges only trips not made (:149-163); money conserved; nothing on day 0. The bag goes home with them, or lasts `BagMinutes`. Regulars: `RegularTrips`+ in 28 days, known to the keeper's household. The bar's drinks (:211-227) are tallied alike.
- **FoundItClosed: "Jodi found Pierre's shut."** The keeper slept through an alarm, collapsed, or is detained or doing service. `Undergo` blames them at freedom 0.5 (Simulation.Feelings.cs:495-501). The shopper goes on to the rival if open. Line: "{actor} found {place} shut."
- **BoughtFromRival: "Jodi came back from the chain, and Caroline saw it."** The keeper of a `Rivals` shop, or their household, sees a regular with the rival's bag; once per customer a day; sympathy (law 5) spreads it in the household. At 26 Store regulars reach the Mart only after a household switch (`ShopChoice`, Simulation.Feelings.cs:775-800) or FoundItClosed; Caroline (Square 9-12), Abigail (14-18) and Pierre (17-20) (DefaultTown.cs:427-433) sit on the walk home from the Mart's door. Line: "{actor} was seen with shopping from {shop} instead of {target}'s." VERIFY: Pierre's grudge against Joja, and the Joja coupons scene.

### 3.7 Hosting (b2-11)

- **Hosted: "Robin had people round."** Nightly, someone with an occasion, a birthday in 1-3 days (`Calendar.IsBirthday`, Calendar.cs:41) or high spirits (`_powerByDay`), weighs `HostBase` × (0.5 + chattiness) (× 0.6 for spirits) through `DesireMath.Effective(boldness, stance, false, mean familiarity with the guests, wish, power)` against `HostForm`, close call keyed ("host", name, day); a pursuit of "party"; once in `HostEveryDays`. Guests: the top `PartySize` by regard (0.2+), outside the household, familiarity 0.4+, nobody disliked (two feuding guests may both come). The host invites each when next in reach (Invited, promise kind Party); one not met before the day is not asked. The appointment rule gathers them; Hosted starts at 18:15 with 2+ guests. `SimResult.Promises` holds who came. Line: "{actor} had people round."
- **LeftOffTheList: "Caroline heard about Robin's party and hadn't been asked."** On any belief about a Hosted act held by someone not invited, not kin or a housemate, at regard 0.4+ and familiarity 0.5+. Hosted (1.5) is told on the day to those who know the host well (1.5 + 0.5, Simulation.cs:1107-1109), the likeliest to be left off. Told, it stirs Answer or MakeUp at corroboration strength (2.7); seen, at full. Pairs as a hostile act. Line: "{target} heard about {actor}'s party and hadn't been asked."

### 3.8 Debts (b2-12)

- **RanUpTab: "Pam put it on her tab at the saloon."** A drinker whose household holds less than `SaloonDrink` drinks on credit if the tab is under `TabLimit` and the keeper holds them at `TabFloor`+; no money moves (the bar still restocks, Simulation.Money.cs:225); it opens `Promise(Tab)`, due at 18:00 `TabGraceDays` after the next payday; over the limit, no drink. At 26 that is Pam (no income; Penny's wage doesn't cover the trailer, DefaultTown.cs:296, 300-306): a way out besides the bin, and less need behind `Temptation` (:294). Line: "{actor} put it on the tab at {place}."
- **PaidOffTab: "Pam paid off her tab."** When the household covers the tab plus a drink: `Move(home, bar, tab)`; Kept, Late if overdue; late with an accepted apology before the second retelling, it settles. Line: "{actor} paid off the tab at {place}."
- **BrokePromise** (S4, 4.5): `Broke` stirs the creditor: **Pity** (ForgaveDebt) if they hold the debtor at `ForgiveAt`+ and their household is not short, felt 0.3 × (0.5 + understanding) × size; else **Owed (new)** (Banned, CalledInDebt, Argued), 0.3 + 0.4 × size; size = clamp(amount / the debtor's weekly costs, 0.25, 1); both last `OwedDays`. No money promise between kin or housemates.
- **CalledInDebt: "Lewis asked Shane for his money back, in front of everyone."** If the debtor can pay, Repaid follows (PaidOffTab for a tab with loans off). `Announcing`: every witness learns the broken promise at 0.6 × 4.0 = 2.4 ("called-in"), which is how a debt becomes public. Line: "{actor} asked {target}, in front of everyone, for the money back."
- **ForgaveDebt: "Gus told Pam to forget the money."** Only on an overdue debt (rule 13). Forgiven; with `Settled` on, the broken promise is settled if retold fewer than twice. Line: "{actor} told {target} to forget the money."

### 3.9 Loans (b2-13)

- **Need (new).** Computed each tick: a free adult whose household need is `NeedAt`+ (`NeedPressure`, Simulation.Money.cs:269-274), toward someone in reach, not kin or a housemate, at regard 0.3+ and familiarity 0.4+, with a visible income (job, pension, sales), no open loan to them and no refusal in 14 days. Intensity: the need.
- **LentMoney: "Gus lent Pam 120g."** The borrower decides to ask (pride falls on them); the lender answers the Ask; yes starts LentMoney (actor the lender), no Refused. Amount: the shortfall below a week of costs, up to 10g, capped by `LoanCap` and by what the lender's household holds beyond `LenderReserve`. `Move` (Simulation.Money.cs:107) opens `Promise(Loan)`, due in `LoanDays`. Who: Pam and Penny ask (self-regard 0.3); Gus lends to Pam, Leah and Maru to Penny. Line: "{actor} lent {target} {amount}g."
- **Repaid: "Pam paid Gus back."** Once the household holds the amount plus 2 weeks of costs, at the next meeting in reach; Kept, Late if overdue. Line: "{actor} paid {target} back."

### 3.10 Lost items (b2-14)

- **The occasion** (rule 15: world facts only): each tick an awake person in a public place drops a purse (10-60g from their pocket) when ("lost", name, minute) < `LostPerDay` / 192; it counts in `TownCash` while it lies; mood −0.05; log `lost-item {owner} item {id} {value} at {place}`.
- **Finding:** within `FindTiles` with a clear look, by a keyed draw each tick (`CheckTraces`' pattern). Recognising it (`RecogniseAt`), the finder keeps it once if motive − honesty − risk > 0 (motive as in `Temptation`, Simulation.Money.cs:293-301; honesty `Honesty` × (understanding + max(0, regard for the owner)); risk, people in sight) and ("keep", item) < `KeepScale` × the excess; else returns it. A stranger's purse goes to the keeper or the mayor. Log `picked-up {finder} item {id} kept|returning|handed-in`.
- **ReturnedLostItem: "Leah gave Penny back the purse she lost on the beach."** Stirs Return in the owner; the money goes back to their pocket. Line: "{actor} gave {target} back the purse lost at {place}."
- **KeptFoundItem: "Shane had Penny's purse, and kept it."** Keeping is a world fact: the value goes to the finder's pocket and the item lies in their home for `ItemDays`, the object trace that lasts (research 2.2). Someone in that home who knows the owner well notices it (0.1 an hour × clarity; visitors too, so this needs b2-1; kin notice but never retell), or the owner sees it within 2 tiles in public (0.2 an hour). Then it starts unseen with the noticer as knower, is told, the finder is confronted, the owner feels Answer (at corroboration strength if told), and the next meeting hands it back. Each exposure cites a find a day+ earlier. Line: "{actor} had {target}'s purse, and kept it."

### 3.11 Contests (b2-16)

On festival days, VERIFY against the game: the egg hunt (Egg Festival, 5-12), the grange display (the Fair, adults with a trade; VERIFY that Lewis judges), ice fishing (Festival of Ice, 13+).
- **EnteredContest: "Marnie entered the grange display."** Wish `CompeteBase` × (0.5 + self-regard) × (0.5 + skill), skill a known draw ("skill", contest, name), through the gate's formula against `EnterForm`; resolved Won or Lost. Line: "{actor} entered the {contest}."
- **WonContest: "Marnie won the grange display."** 30 minutes before the end: 0.7 × skill + 0.3 × power + 0.2 × ("judge", contest, year, name), ties to the first name; `SimResult.Contests`. Line: "{actor} won the {contest}."
- **Envy (new):** in each loser who saw it and is not the winner's kin, `EnvyBase` × (0.5 + self-regard) × (1 − understanding) × (1 + max(0, −regard for the winner)); `EnvyDays`.
- **SoreLoser: "Pierre said Marnie's win wasn't fair."** Onlookers who like the winner cool on the loser (laws 5, 6); 5+ witnesses at a festival humiliate the loser (S9). Later, RiggedContest (S7) can hook the judge and the mayor's let-off (Authority.cs:123-124). Line: "{actor} said {target}'s win at the {contest} wasn't fair."

### 3.12 Unchanged

A motive's acts are every kind whose gate serves it (3.0), dearest first, ties by name (`Served`): so Banned comes before MadeScene, then acts-5's Mocked (0.55+h), then Argued (0.5+h). Hosting and entering a contest are choices through the gate's formula. Unchanged: one act a tick, two slots per pair a day, love covers, families cover, the hostile cooldown, the close-call band.

---

## 4. Festivals, reach levers and scandals

### 4.1 Festivals (b2-4)

Day 0 is spring 1, a Monday (Calendar.cs:4-5); a year is 112 days (16 weeks), so weekdays repeat each year.

| Festival | Date (Calendar.cs:25-28) | Run day | Weekday | Venue at 26 | Hours (VERIFY) |
|---|---|---|---|---|---|
| Egg Festival | spring 13 | 12 | Saturday | Square | 9:00-14:00 |
| Flower Dance | spring 24 | 23 | Wednesday | Square (the game: Cindersap Forest, VERIFY) | 9:00-14:00 |
| Luau | summer 11 | 38 | Thursday | Beach | 9:00-14:00 |
| Dance of the Moonlight Jellies | summer 28 | 55 | Sunday | Beach | 22:00-24:00 |
| Stardew Valley Fair | fall 16 | 71 | Tuesday | Square | 9:00-15:00 |
| Spirit's Eve | fall 27 | 82 | Saturday | Square | 22:00-24:00 (the game ends at 23:50, VERIFY) |
| Festival of Ice | winter 8 | 91 | Monday | Square (the game: Cindersap Forest, VERIFY) | 9:00-14:00 |
| Feast of the Winter Star | winter 25 | 108 | Thursday | Square | 9:00-14:00 |

- **Rows.** One gathering per festival with `Date`, `Holiday` and `FestivalWeight` (20, the opening meeting's, DefaultTown.cs:97). The Square's crowd stands around (15,13), radius 6; the Beach's around (15,6), radius 5; `Venues` lets a grown town move them. The Egg Festival replaces Saturday's market.
- **Who comes.** A festival is a hub of weight 20 (Simulation.cs:647-677). While it is on nobody works, the constable doesn't patrol and no other gathering is on; haunts and home stay options. Penny's lessons are a job (DefaultTown.cs:366) and stop. Keepers come, so shops shut (VERIFY that the game's do; question 6). The tired stay away (tiredness comes first in `Decide`, so night festivals get the night people), as do the detained, anyone doing service, and anyone whose home option wins (`HomeWeight`, Simulation.Desire.cs:130-136). *Measured:* 99-100% of the town by day, 48-68% of those awake at night; at weight 6, still 96-99% by day.
- **Witnessing** is rule 2's; the festival changes who is near. A bin placed at a daytime festival had a median of 13-15 witnesses, against 1 for a placed scandal today. Outdoors from 20:00 clarity halves (Perception.cs:13-14, 38). At 60 a stranger is named only on a close look (0.9 − 0.7 × 0.08 = 0.84, Perception.cs:74-75), so festival stories about strangers travel as "someone, a young man". Hours side by side grow familiarity (0.012 an hour × (1 − f), Simulation.cs:28, 1047), so stories cross circles (C12).
- **No venue bonus:** the crowd alone takes a bin to 90% of the circle in two days in 91-93% of runs, so `FestivalJuice` is 0; the dial stays in case S9 fails.
- **Also:** 0d.6's festival gifts (Simulation.Withdrawal.cs:316) find their targets in reach; at 60, a festival floods people with new faces and weak ties fade faster that week (`flood`, Simulation.Growth.cs:258, 270-271); the 14-day band runs include the Egg Festival; no errand in festival hours.
- **Measure.** `CountFestival(m)` reads true positions for measurement only, as `SceneOf` does (Simulation.cs:898-910), into `FestivalDays`. Target: 90%+ of the awake by day; nights reported.

### 4.2 Announced (b2-5; the board, b2-18)

**`Announce(listener, actId, by, actor, confidence, how, m)`** delivers the act as a story told by the announcer, like the second half of `TryTell` (Simulation.cs:1125-1149), except: it skips the announcer, the person named and the true actor, and an announcer never announces their own kin; juiciness is `AnnounceFactor` (0.6) × base, unfaded (a theft at 2.7: told to anyone on day 0, on day 1 to those who know the culprit well); confidence is the announcer's × (0.5 + 0.5 × the listener's familiarity with them), chain [by] + theirs; a listener who holds it already follows :1133-1146 (a name fills in "someone", juiciness never rises). Heard in front of others it counts corroborated (`Corroborate`, Simulation.Feelings.cs:593-600); a verdict read out, confirmed (`Confirm`, :603-610). It adds `_told` and `Retold`; the named person's kin feel `KinPublic`'s shame (:566-572); the named person, in earshot, feels `Accused(named, actId, [by], ...)` with "confronted", "read-out" or "called-in" (:509-539). One `Announcement`; log `{m} announced {act} by {by} {how} to {n}`. **Earshot:** same place, within `AnnounceTiles` (8), in sight.

1. **Public confrontations (`PublicConfront`).** When `CheckScandals` picks the confronter, `QueueConfront` queues the pair and marks the act confronted. `Confronts(m)` waits until both are free within 8 tiles in one place, then does today's record (Simulation.cs:1188-1194) and begins **Confronted** (`PerDay` 0, target the accused). `_announces[confronted] = scandal` keeps the link with feelings off, when `Begin` drops `About` (:868-869). When it ends, `AfterSeen` announces the scandal to its witnesses; met alone, nobody else hears. **Line:** "{actor} confronted {target} about {about}."
2. **Verdicts read out (`ReadVerdicts`).** Every verdict but a let-off joins `_toRead`. `ReadOut(m)` runs when the mayor is awake and free within the radius of a `ReadAt` gathering (the Market) or a festival, `ReadAfterMinutes` (180) after it began, once a day there; he reads every waiting verdict in act order, naming the accused at confidence 1, who may be the wrong person (a twist with a visible cause). *Measured:* 14 people on average in earshot of Lewis at noon on market day.
3. **Announcing kinds** (Confronted, CalledInDebt): when one ends, every witness who knew what happened learns its `About` act from its actor. They need no `PublicConfront`.
4. **The board (`Board`).** A post is (act, poster, named, posted at, until). The mayor posts each verdict; the harness can post an act. `ReadBoard(m)`: anyone awake within `ReadTiles` (3) of `BoardSpot`, in sight, reads each unread post with `ReadChance` (0.5) a tick, keyed ("read-board", post, name, minute), as an `Announce` with how "board"; posts last `PostDays` (7).

### 4.3 Settled, and amends (b2-6, b2-7)

- **Settled** when retold fewer than `SettleBefore` (2) times (`Retold` counts tellings and announcements) and the culprit paid the victim back (if it cost anything) and the victim accepted an apology (acts-4; the harness's `settle` does both), or the victim forgave it (ForgaveDebt). The victim is the act's target (keeper, creditor, the scene's target); with feelings off, the place's keeper or the creditor `Broke` recorded. Too late: log `{m} settle-late {act} retold {n}`, no change.
- **Effect.** `TellingJuice(b, m)` = `Current × SettledShare` (0.5) for a settled act, in `TryTell`'s three readings of `Current`. `MayTell` passes it only to listeners at familiarity `KnowsActorAt` (0.2)+ with the person named. A victim who settled does not report it; filed accounts stand. `SimResult.Settled`; log `{m} settled {act}`.
- **Restitution:** a theft's value (`_theftValue`, Simulation.Money.cs:77), a broken promise's amount, nothing for a bin, scene, blunder or returned purse; from pocket then purse to the victim's household as `PayUp` pays (:348-372), no fine; Mart losses leave town (:362-368).
- **Amends (b2-7).** A culprit found out feels acts-4's Remorse toward the victim, if they hold the victim at `RemorseAt` (0)+ and are not kin or housemates (families have it out at home). Found out: named to their face (any `Accused` event) or caught in the act (named at clarity 0.9+). Intensity |joy| × `RemorseShare` × (0.5 + understanding), for `RemorseDays`. An apology about a scandal pays restitution first; acts-4's answer adds `AmendsWeight` (0.3) to Obliged when it pays in full; accepted before the second retelling, the act is settled.

### 4.4 Levers that wait

Hushed and Denied (C7, C8) need trust and a credence test by closeness (batch 3). The +0.5 for public figures and wonder (law 13) come later and re-tune C11. An argument about a scandal announces nothing.

### 4.5 The first new scandals

BrokePromise, KeptFoundItem and LetOff are scandals with `Reportable: false` and `Placeable: false`: they fade slowly, are confronted and shame kin, but nobody reports them and the harness never places them. `Variety.Of` counts them as Scandal events (Variety.cs:117) until m-3 routes them to DebtRevealed, KeptExposed and LetOffExposed.

**S4, a secret debt revealed (BrokePromise; b2-12).** Row: scandal 4.0 (rule 13), −1, 1/1, 18+. Affect: target (the creditor) −0.4, plastic 0.4, freedom 0.7, Given, tilt 0. The debtor's motive is Need (`SimResult.Motives` "need"). `Broke(debtor, creditor, amount, promiseAct, place, at, m)`, from `Promises(m)` at the due minute or the harness's S4 scene, starts it unseen where the promise was made; `About` is the opening act (−1 for a dealt debt), `Amount` the debt; log `{m} broke-promise {debtor} {creditor} {amount} act {id}`. Only the creditor (Witnessed, named, clarity 1, J 4.0) and the debtor know. **Public through** (1) the creditor's tellings, two a day for about three days, second-hand holders at 1.4 never retelling: about a third of the circle; (2) CalledInDebt before others, at 2.4 (Pam haunts the Saloon 15:00-24:00, Gus works there 11:00-24:00, DefaultTown.cs:390, 418; lore VERIFY); (3) rule 9's confrontation once a quarter of those who know the debtor hold it. **Kept small** by paying before the due minute, or settling before the second retelling; a creditor who loves the debtor tells it but never answers it. No trace (a found trace never names, Model.cs:242). Kin feel shame (`KinHears`, Simulation.Feelings.cs:554-562); holders cool on them (association, :274). **Lines:** "{actor} didn't pay {target} back when it was due." / "{target} called in {actor}'s debt in front of everyone at the {place}."

**S9, a public humiliation at a festival (Blundered; b2-15).** Sources: Blundered, and MadeScene and SoreLoser at a festival. Row: news 2.5, valence 0 (a mishap, like `Stumbled` and `Collapsed`), 1/2, only in a festival crowd, 5+ (news because it exists only at festivals; the research gives 1.5 elsewhere). Affect: actor −0.3, plastic 0, freedom 0; with `PityOn`, witnesses may want to help (`StirPity`, Simulation.Desire.cs:279-297). No motive: `Blunders(m)` draws once per attendee and festival, `Rng.Unit(seed, "blunder", festival, name, day) < BlunderChance`, at `From + Rng.Range(seed, 30, To − From − 30, "blunder-at", festival, name, day)` rounded to a tick, if awake, free and in the crowd; about 5 a year at 26. *Measured* for a bin: 90% of the circle in two days in 91-93% of runs, against 9% on a Wednesday morning. **The humiliation:** a `Shaming` act ending at a festival with `HumiliatedAt` (5)+ witnesses makes its actor feel named in public, `Accused(actor, act, [], HumiliatedJoy, "humiliated", m)`: mood −0.3 × sensitivity × (1.5 − self-regard), "Ashamed", and with `HomeHurtOn` a lasting hurt that pushes the shy toward withdrawal; log `{m} humiliated {actor} act {id} witnesses {n}`. A scene's apology (acts-4) settles it; a blunder can't be settled; Comforted (acts-5) eases the hurt, not the reach. **Line:** "{actor} made a fool of {themselves} in front of everyone at the {festival}."

**S6, a let-off comes out (LetOff; b2-17).** Partiality is the only sway built (rule 16); a softer step needs listeners who know records, and a bribe (`TookBribe`) waits for the lies (7b). Row: scandal 4.5, −1, 1/1, unseen. Affect: target (the victim of the scandal let off) −0.3, plastic 0.4, freedom 1, Given; actor the mayor; `About` the scandal. Motive: 2% for someone close (Authority.cs:46, 123-124; closeness, Feelings.cs:431-432); *measured* 5 in 249 natural verdicts, about one seed-year in 40. `LetOff(scandal, accused, m)` from `Review`, at the mayor's place; the victim is the scandal place's keeper unless the mayor, else the first (by name) whose account named the accused, else none; the askers are all whose accounts named the accused (`_cases`), not the mayor. **The trace** is the missing consequence: after `LetOffAskDays` (5), an asker who meets the mayor asks (`AskAboutCase`); Lewis doesn't lie and says so; the asker holds it Told, chain [mayor], confidence 1, confirmed (F15); log `{m} asked {asker} {mayor} about {scandal}: let off`. **Standing:** held at 4.5 × (1 − max(0, regard for the mayor)): Pierre, his friend (DefaultTown.cs:403; seed 0.4, Feelings.cs:35), at 2.7; Gus, Harvey or Shane at 4.5; 4.5 with feelings off. A victim who asks gets Retaliate toward the mayor, |joy| × (0.5 + sensitivity) × (1 − max(0, regard)). Public through the askers' tellings and rule 9's confrontation of the mayor (6 of 24 at 26), public with `PublicConfront`. The accused, their kin and the mayor's kin never tell it (`Shielded` in `MayTell`; families cover). **Line:** "{actor} let {about.actor} off for {about}."

**The kept purse** (3.10) is an exposure that comes late, weeks after the deed, from a visit or a sighting; handed back before anyone notices, it leaves no story.

---

## 5. The scenario checks

### 5.1 Today's reach, measured

*Measured* on checkpoint-3 with `DefaultTown.Feelings()`: placed scandals 400 seeds × 14 days (reach at day 13), natural ones 200 × 112 (reach 14 days on); circle = familiarity 0.2+ toward the actor at the start of the act's day. Values are in 5.5's "Today" column. In short: witnesses decide reach (45% of placed scandals have none); the 26 town has no quiet edge, since one witness, even a loner, tells about eight people in three days at the hubs; natural scandals are many small ones (Pam's bins, Abigail's thefts), none town-wide; and a festival makes a town-wide story (a bin in the Square reaches 90% of the circle in two days in 93% of runs at the Flower Dance, against 9% on an ordinary Wednesday, and 91% at the Egg Festival against 81% at the Saturday market, where Lewis has 14 people in earshot at noon).

### 5.2 Records (`Scenarios.cs`)

```csharp
/// A chosen kind in a chosen scene (research 2.3), placed like ScandalFor's acts and marked Injected.
public sealed record Scenario(
    string Name,                          // "C3", "S4-called-in", ...
    string Kind,                          // the kind placed
    string Actor = Harness.Anyone,        // a name, or whoever is first able
    string? Target = null,                // S4: the creditor
    IReadOnlyList<string>? Places = null, // null: the kind's Allowed list
    int Day = 1, int From = 480, int To = 1260,   // the window, each day from Day
    int GiveUpDays = 3,                   // then dropped, with a log line
    int MinInRange = 0, int MaxInRange = int.MaxValue,
    Onlookers Onlookers = Onlookers.Any,
    bool AtHub = false,                   // the actor stands within a gathering that is on
    bool AtFestival = false,              // ... and it is a holiday
    bool NoTrace = false,
    double Amount = 0,                    // S4: the debt
    IReadOnlyList<FollowUp>? Then = null,
    string? Key = null);                  // the draws' key (default Name): one key, one act
public enum Onlookers { Any, Loners, KinOnly }
public sealed record FollowUp(int AfterMinutes, string What);
```

### 5.3 Placement and follow-ups

`StartScenarios(m)` handles each pending scenario in list order inside its window:
1. **Able:** free, of the kind's age, in one of its places, with a target if needed, not the keeper at work (`StartScheduled`'s tests, Simulation.cs:838-844); in range (awake others in the place, within 8 tiles, in sight) within [MinInRange, MaxInRange]; `Loners`: all of them at chattiness 0.4 or less; `KinOnly`: all kin or housemates; `AtHub`/`AtFestival`: within a gathering's radius (a holiday for `AtFestival`).
2. **When:** if anyone is able, this minute with chance 1/`PlacedMeanWait` (Simulation.cs:186), keyed ("scene", key, minute); the actor by ("scene-actor", key, minute); a named actor at their first able minute.
3. **The act:** `Begin(m, kind, actor, injected: true)`, `Scenarios[id] = Name`; `NoTrace` adds it to `_noTrace`; follow-ups are queued at the act's minute + `AfterMinutes`.
4. **S4:** a `BrokePromise` scene calls `Broke` at the window's start, at the creditor's job place or home; nobody needs to be able.

Scenes sharing a `Key` place the same act at the same minute, which is how C5 compares one act settled and left alone. Each follow-up throws at construction if its switch is off; the cloud owns the dispatcher and each body is one call into the local slice:

| What | Needs | Does |
|---|---|---|
| `settle` | `Settled` | amends to the victim, who accepts; restitution moves (`Settle`) |
| `read-out` | `ReadVerdicts` | joins the mayor's reading list, naming its actor (`ToRead`) |
| `post` | `Board` | the mayor posts it, naming its actor (`Post`) |
| `sway` | none | when the mayor decides this case he lets the accused off (`_forceSway`); `Partiality` makes it an act |
| `call-in` | `Debts` | the first time creditor and debtor are in earshot with 2+ others, the creditor starts CalledInDebt, whatever the gate says |
| `hush`, `deny` | batch 3 | reserved |

### 5.4 Reach measures (`ReachMetrics.cs`)

```
For a run r and an act a:
  d0 = Clock.Day(a.Tick); last = r.Days - 1; C = r.Circles[a.Id]   // under 3: counted apart
  holds(p, d) = r.Beliefs[p] has a.Id with GotTick < (d + 1) * 1440
                // GotTick is first hearing; later updates keep it (Simulation.cs:1140, 1142; Simulation.Suspicion.cs:89)
  heard(p, d) = holds(p, d) and not (Source == Found and Actor == null)
  CircleReach(d) = |{p in C : holds(p, d)}| / |C|;  CircleHeard(d) with heard
  TownReach(d) = r.HoldersByDay[a.Id][d] / (r.CastSize - 1);  TownHeard(d) with r.HeardByDay
  W = r.Witnesses[a.Id];  InRange = r.Scenes[a.Id].InRange
  Died = r.HeardByDay[a.Id][last] <= W
  Grew = the last d > d0 with HeardByDay[d] > HeardByDay[d-1], minus d0 (as Program.cs:285-291)
  Rank correlation: Spearman, average ranks for ties.
  Exposure: Town (a holiday or the Meeting on at a.Location), Crowd (W >= 5, or within an on
            gathering's radius), Seen (W 2-4), Private (otherwise)
```

`ReachMetrics.Evaluate(check, runs, kinds, town)` returns `CheckResult(Check, Scene, Runs, Placed, Counted, Values, Pass, Criterion)`; every check reports circle and town shares. C12 reads districts from `TownMetrics.Districts` (TownMetrics.cs:26). The runner: `--check C3` (repeatable), `--check today` (C3, C4, C10, C11, C13), `--check all` (every check whose switches are on); each check runs its own seeds and days and prints its scene, runs placed, values, criterion and pass or fail. The replay keeps `ScandalFor` (Replay.cs:61); recording scenario runs waits for replay version 2.

### 5.5 C1-C13, and the scandal checks

| # | Scene | Measure | Pass (first guess) | Runs | When | Today |
|---|---|---|---|---|---|---|
| C1 | `RummagedInBin` in the Square during the Flower Dance (day 23), `AtFestival`; a second scene at the Egg Festival (day 12) | CircleReach(d0 + 2) | 90%+ in 90%+ of placed runs | 400 × 27 | b2-4 | prototype: 93%, 91% |
| C2 | `RummagedInBin` in the Square while Noon or the Market is on, `AtHub`, `MinInRange` 5 | CircleReach(last), runs with W ≥ 5 | median 50-90%; 25%+ of runs over 70% | 400 × 14 | m-0 | median 88%, 97% over 70% |
| C3 | `RummagedInBin` at the ClinicYard, exactly 1 in range, `Loners`, `NoTrace` | CircleHeard(last); Died | 20% or less in 70%+ of runs; dies in about half | 400 × 14 | m-0 | fails: 8% (a loner 7%); never retold 1%; median 42% |
| C4 | the placed scandal, `MaxInRange` 0 | CircleReach(last) where found; share never found reported | median 30% or less | 400 × 14 | m-0 | passes: 12%; found in 78% |
| C5 | `RummagedInBin`, 1-2 in range, trace kept; two scenes, one `Key`, one with `settle` at +60 | CircleReach(last) | settled median at most half the unsettled; share settled too late reported | 400 × 14 × 2 | b2-6 | |
| C6 | `Stole`, `KinOnly`, `MinInRange` 1 | holders outside the household at d0 + 7 | none in 95%+; leaks reported with the first outside chain and overheard `FamilyRow` acts | 400 × 14 | m-0 (rare at 26: Abigail and her parents at the Store) | |
| C7 | hushed, every holder agreeing | | | | batch 3 | |
| C8 | denied: a liked culprit against a disliked one | | | | batch 3 | |
| C9 | C4's scene with `read-out`; a second with `post` | TownReach 24 hours after the announcement | median 90%+ | 400 × 14 | b2-5 (b2-18) | |
| C10 | the placed scandal | Spearman of W with CircleReach(last) | 0.5+ | 400 × 14 | m-0 | 0.91 (natural 0.84) |
| C11 | natural scandals | CircleReach(d0 + 14) | median 40-70%; 10%+ at 90%+; 15%+ under 20% | 200 × 112 | m-0 | fails two: median 36%; 1% at 90%+; 38% under 20% (bins 52%, thefts 12%) |
| C12 | at 60: (a) `RummagedInBin` in a neighbourhood's public place; (b) C1's scene | (a) median CircleReach(last) ÷ median TownReach(last); (b) share of runs where half of every other district holds it by d0 + 2 | (a) 2+; (b) 80%+ | 400 × 14 / 27 | m-3 (b2-4) | |
| C13 | the placed scandal, witnessed runs | TownHeard(last) in 40-70% and Grew 3+ days (Program.cs:285-292) | reported; 52%+ at 26 | 400 × 14 | m-0 | 55% (natural 58%) |
| S4 | `S4-kept`: Pam owes Gus 150g, due unpaid on day 2 at 20:00; `S4-called-in`: with `call-in` at 0; `S4-settled`: with `settle` at +60; one `Key` | CircleReach(d0 + 7) of the BrokePromise | kept median 40% or less; called in 70%+; settled at most half of kept | 400 × 14 | b2-12 | |
| S6 | the placed scandal with `sway`, runs where the mayor decided | the LetOff's CircleReach 14 days on, split by the first asker's regard for the mayor | median for askers at 0.4+ at most half the rest's | 400 × 14 | b2-17 | |
| S9 | C1's scene with `Blundered` | as C1 | as C1 | 400 × 27 | b2-15 | |

### 5.6 As built: m-0 (2026-10-08)

Built on `claude/reach-checks`, from `claude/checkpoint-6`: `Scenarios.cs`, `Simulation.Scenarios.cs`, `ReachMetrics.cs`, the runner's `--check` and `ScenarioTests` (7) and `ReachMetricsTests` (4). With no scenario every pin holds. Where it differs from 5.2-5.5:
- **`Simulation.Place(scenarios)`**, called before `Run` as `SetTrait` is, instead of a constructor parameter, so the constructors in a shared file stay as they are.
- **The circle** is read from a copy of familiarity taken at the start of each day (`KeepDayStart`, one line in `RunDays`), for every scandal and every scenario act, in name order. An actor nobody knows yet (the newcomer) has an empty circle, and no circle reach.
- **Follow-ups:** only `sway` is built (`_forceSway`, one `||` in `Review`), for b2-17. The others, and `Amount`, are refused until their slices land.
- **C6 counts outsiders who know who did it.** Rule 17's cover works on the believed culprit: a keeper who finds stock missing doesn't know it was his daughter, and tells it. So an outsider who knows only that something was taken is reported apart, not counted as a leak.
- **C13 is reported only,** with 52%+ as its guide.
- **`--check`** takes `today` (C2, C3, C4, C6, C10, C11, C13) or a name, repeatable. Each check runs its own seeds and days unless `--seeds` or `--days` is given. It refuses `--town-seeds`.

**Today** is in `sim/README.md` ("Reach in context").

---

## 6. Twists: story events, the variety measures and the deal

### 6.1 Why

The town repeats itself (6.3: V1 92%, V2 2.7, median V6 0 at 26). Story events and the variety gate read results and never write; the deal (6.4) writes each seed's inputs before the run. The *probe* (6.6) found that dealt ties vary the smaller stories but not the biggest repeats: Sam and Shane's feud moves only with a work part, and Pam's and Abigail's scandals only with money between people (b2-12, b2-13).

**Twists the kinds make:** reversal (MadePeace, ForgaveDebt, PaidOffTab after a BrokePromise, a settlement); revelation (a kept purse found out, BrokePromise told, a public call-in, a let-off found out, a verdict read out with the wrong name); snub (StoodUp, LeftOffTheList, Refused, Banned); rise and fall (WonContest, SoreLoser, Blundered); a chain across households (FoundItClosed → the rival → BoughtFromRival → Pierre cools on a customer).

### 6.2 Story events (m-1, m-3)

**Built** (`Variety.cs`): `StoryEvent` (:12-15); 14 kinds, biggest first (:50-54); `Named`, `Twists` (:57-67); `Of` (:80-150); `Headline`, `Split`, `Measure`, `From` (:154-292); `SeedYear` (:300); the runner's variety line (Program.cs:448-455); `VarietyTests` (6). **m-1 adds** `Variety.Events.cs` (patterns, `RunLog`, cause chains, the fair-twist rule; `StoryMetrics.cs` reuses `RunLog`), and appends:

```csharp
// Variety.cs:12
public sealed record StoryEvent(string Kind, int Day, IReadOnlyList<string> People, string Detail = "",
    int Tick = -1, IReadOnlyList<CauseStep>? Chain = null, bool Fair = false);
// Variety.Events.cs. A step of a cause chain, earliest first: an act, belief, telling, report, tie, or a
// dealt start (Tick -1, ActId -1). Visible: seen, found or told by someone outside the act before the
// story's tick; a dealt start always is. (Not "Link": Model.cs:45.)
public sealed record CauseStep(int Tick, string What, int ActId, bool Visible);
public sealed class RunLog   // a run's log read once into rows; read only
{
    public static RunLog Of(SimResult r);
    public IReadOnlyList<(int Tick, string Holder, int ActId, string? Actor, Source Source)> Beliefs { get; }
    public IReadOnlyList<(int Tick, string Teller, string Listener, int ActId)> Told { get; }
    public IReadOnlyList<(int Tick, string Who, int ActId)> Found { get; }
    public IReadOnlyList<(int Tick, string Who, int ActId)> Suspected { get; }
    public IReadOnlyList<(int Tick, string Who, int ActId, string? Named)> Reports { get; }
}
```

`VarietyStats` (:38-45) gains `V1Feud`, `V1FeudPair`, `V1Culprit`, `V1CulpritName`, `V6All`, `FairShare`, `Unfair` (by kind), `V7`, `V7Headline` (NaN without forks); `Variety.Of` gains optional `record` (verdicts on record at the start, Authority.cs:56) and `dealt`. The rows read are the log's `act` (Simulation.cs:878), `belief` (:1019), `told` (:1129), `confront` (:1193), `regard`/`reconciled`/`tie`/`sentiment`/`shop` (Simulation.Feelings.cs:419, 424, 703, 476, 798), `report`/`questioned`/`verdict`/`warned` (Simulation.Authority.cs:151, 263, 300, 348), `found` (Simulation.Traces.cs:55), `suspects` (Simulation.Suspicion.cs:88) and batch 2's new lines; fields of rows naming a kind of person are read from the end (Metrics.cs:255). Each kind is a plain scan in the style of Felt's sifting queries (stages over rows bound to people, with "unless"; the staged form follows Kreminski's Winnow, VERIFY), with no query engine; a pattern whose kinds are absent finds nothing.

**The new rank order** (headline order): WrongVerdict, BlameMoved, SecretOut, LetOffExposed, KeptExposed, DebtRevealed, LetOff, FirstScandal, Scandal, DebtCalledIn, KinFeud, Upheaval, Hermit, Humiliated, Withdrawn, Confessed, FellOut, StoodUp, Feud, Reconciled, Brawler, Friendship, Won, Debt, Elected. New or changed kinds:

| Kind | Pattern | People | Twist | Slice |
|---|---|---|---|---|
| BlameMoved | X's leading name moves from p to q | p, q | reversal | m-1 |
| LetOffExposed | a `LetOff` held by anyone but the mayor | mayor, a | revelation | b2-17 |
| KeptExposed | a `KeptFoundItem` act | finder, owner | revelation | b2-14 |
| DebtRevealed | a `BrokePromise` held by 3+ besides the creditor | debtor, creditor | revelation | b2-12 |
| LetOff | `verdict X a … let-off` (`Verdict.LetOff`, Model.cs:294); nobody knows, so no twist | mayor, a | | m-1 |
| FirstScandal | a's first unplaced scandal (`IsScandal`), with no verdict on record at the start | a | | m-1 |
| Scandal | narrowed to a's second or later | a | | m-1 |
| DebtCalledIn | `CalledInDebt` by c to d; detail the amount | c, d | | b2-12 |
| Upheaval | a grocer switch (`ShopSwitches`, Simulation.cs:133) or `Banned`; the Upheaval tier's own kinds join later | h, or a | | m-1, b2-9 |
| Humiliated | shamed before 3+ (audience `Scenes[X].InRange`, Model.cs:278, less 1 with a target; one a person a day): warned, taken in or set to service; mocked (acts-5); a scene (b2-8); `humiliated` (b2-15) | a | | m-1 |
| StoodUp | an `Invited` promise from h to g, then `StoodUp` | h, g | | b2-2 |
| Reconciled | adds `MadePeace` | a, b | reversal | b2-3 |
| Won | `WonContest` by a | a | | b2-16 |

- **BlameMoved:** lead(X, d) is the name held by most holders of X at the end of day d (holders other than the actor whose belief names someone), defined with 2+ holders and a strict winner, from `belief` rows (each replaces the holder's belief). It fires on the first day the lead is q after a last lead p ≠ q, once per (X, p, q), for retold bad acts (valence < 0, tier News+, Model.cs:229-231), not placed.
- **Twists** are `Twists` plus BlameMoved, LetOffExposed, KeptExposed and DebtRevealed.
- **The fair-twist rule** (research section 3): a twist counts toward V6 only if its chain has 2+ steps before it and one was visible before its tick. X is visible before t if a `belief` row (Witnessed or Found, for someone other than X's actor and target) or a `told` row for X came before t. Ties, reports and verdicts are steps, not traces. Unfair twists are counted by kind and printed.

| Twist | Chain, earliest first | Fair when |
|---|---|---|
| WrongVerdict on X | X; each `report` naming the accused | 2+ steps, X visible |
| BlameMoved | X; the first belief naming p; the first naming q | X visible |
| SecretOut | X; each `found` and `suspects` row; the first naming | a `found` or `suspects` row before the naming |
| Confessed | X; its reports; the `questioned` row | X visible |
| FellOut | the friendship; each later act between them that lowered either's regard (`Felt` change < 0, act id ≥ 0) | 2+ steps, one act visible |
| Reconciled | each act that lowered a's regard for b, or the dealt tie; the kindness `reconciled` cites | 2+ steps; the dealt tie or an act visible |
| LetOffExposed | X; its reports; the verdict; the `asked` row | X visible |
| KeptExposed | `lost-item`; `picked-up`; the act that brought the noticer | that act visible |
| DebtRevealed | the opening act (or dealt debt); the BrokePromise; its tellings | the opening act visible, or dealt |

### 6.3 The measures, and today

V1-V6 and V8 are built as `sim/README.md` defines them (seed-years from `Split`; Ids without the day; targets V1 30% or less, V2 20+, V3 under 5%, V4 60%+ at equal N, V5 50% or less, V6 median 2+, V8 3+). m-1 changes two: **V1 pools families** before counting, feud (Feud, FellOut, KinFeud for one pair) and culprit (FirstScandal, Scandal for one person), printed overall and per family; **V6 counts fair twists**, with all twists and the fair share beside it. m-2 adds **V7** (6.4).

**Today** (checkpoint-5, each seed one year):

| Town (seeds) | V1 | V2 | V3 | V4 | V5 | V6 median (mean) | V8 |
|---|---|---|---|---|---|---|---|
| shipped 26 (200) | 92% (Scandal Pam) | 2.7 | 0.0% | 56% | 59% (Lewis: Feud) | 0 (0.39) | 3 |
| 31 town (100) | 95% | 3.5 | | 53% | 57% | 0 (0.45) | 3 |
| `pelican:60@1` (100) | 74% (Brawler Alex) | 5.3 | | 83% | 88% (Megan: Friendship, a work pair) | 0 (0.50) | 3 |
| 100 towns of 60 (`--town-seeds`, #61) | 70% | 6.8 | | 100% | 71% (Harvey: Friendship) | 0 (0.53) | |

E1: 60% shipped, 42% at 31. The feud family is not measured yet (*probe*: Sam and Shane 53%). m-1 re-measures with families and fairness on the shipped town and the 0d.6 candidate, 200 seed-years each.

**V7, forked runs (m-2).** For 50 seeds, a base and 3 forks (salts 1-3) at day 28, 112 days each; a fork matches the base to minute 28 × 1440, then every die is drawn anew. V7 is the share of forks whose major Ids (ranks 1-13) on days 28-111 differ from the base's; target 25-60% (under 25%, a year is settled by day 28; over 60%, day 28 decides nothing). The share whose headline differs is printed too.

```csharp
// Simulation.cs:213: private long _seed; private int _forkAt = int.MaxValue; private long _forkSeed;
/// V7: from the start of this day on, every keyed draw uses another seed. Called before Run, as SetTrait is.
public void Fork(int day, long salt)
{
    _forkAt = day * Clock.MinutesPerDay;
    _forkSeed = unchecked((long)Rng.Hash("fork", _seed.ToString(CultureInfo.InvariantCulture),
        salt.ToString(CultureInfo.InvariantCulture)));
}
// Simulation.cs:383, after _now = m: if (m == _forkAt) _seed = _forkSeed;
```

Every die reads `_seed` when drawn, the sway included, so all change after the fork and none before; no fork, every pin holds.

**Runner.** The variety line adds the families, fair twists (all, fair share, unfair kinds) and, with `--forks <K>` (needs `--days 112`+), `V7 open future {v7} [25-60%] ({S} seeds x {K} forks at day 28; headline changed {h})`. `--log <seed>` adds `stories:`, one a line: `d{day} {Kind} {people}[ {detail}][ (unfair)]: {chain}`, steps as `act {id} {Kind} by {actor}[ to {target}] d{day}`, `told d{day}`, `found d{day}`, `report d{day}`, `friends since d{day}` or `dealt: {cause}`. The recorder may write a `stories` key for a Stories tab, each kind's sentence a template.

### 6.7 As built: m-1 (2026-10-08)

Built on `claude/story-events-2`: `Variety.Events.cs` (`RunLog`, `CauseStep`, the leads, chains and the fair-twist rule), the new kinds BlameMoved, LetOff, FirstScandal, Upheaval and Humiliated, V1's families and V6's fair twists, the runner's variety lines, and `VarietyTests` (11). Where it differs from 6.2-6.3:
- **Dates.** A secret out is dated by its first naming (the log's first belief row naming the actor), and a confession by its interview. Before, they were dated by the first hearing (a belief's GotTick, often a trace with no name, days earlier) and by the act.
- **`Variety.Of` has no `record` or `dealt` yet.** No town starts with verdicts on record, and the deal is d-1's, so every person's first scandal is a FirstScandal and no chain starts from a dealt tie.
- **Humiliated** leaves out the consequences of the harness's placed scandal, as the other kinds leave the scandal out.
- **`StoryMetrics` doesn't read `RunLog` yet:** it is the act catalog's file (local's).

`VarietyStats` gains `V1Feud`, `V1FeudPair`, `V1Culprit`, `V1CulpritName`, `V6All`, `FairShare`, `Unfair`, and `V7` and `V7Headline` (NaN until m-2's forks). The numbers are in `sim/README.md` ("Story events, part 2").

### 6.8 As built: m-2 (2026-10-08)

Built on `claude/forks`, as 6.3 writes it: `_seed` is no longer `readonly`, and `RunDays` swaps it at the fork's minute. `Fork` and its two fields are in a new file, `Simulation.Forks.cs`, so `Simulation.cs` changes by those two edits only. V7 is `Variety.Open` (`Variety.Forks.cs`); the runner's `--forks <K>` prints it. The tests are `ForkTests` (3). Where it differs from 6.3:
- **The window.** V7 reads days 28-111 of each run (`MajorStories(events, ForkDay, Year)`), so a run longer than a year is compared over its first year only. A fork runs as long as its base, so a story that needs days to show (a hermit's spell) is read the same way in both.
- **What's major.** The first 13 of `Variety.Kinds` (`MajorRanks`), WrongVerdict to Hermit. Feuds and friendships aren't among them, so a fork whose only change is another feud reads as the same year.
- **The headline** is the biggest story over the same days; two runs with no story there have the same headline.
- **`Fork(day, salt)`** refuses a day before 0. A fork at the run's length or later changes nothing (the test pins P3).
- **The runner keeps each fork's story events, not its result**, so `--forks 3` over 50 seeds holds 50 results, not 200.

The numbers are in `sim/README.md` ("V7, the open future").

### 6.4 The deal (d-1 to d-6)

| Part | Drawn at day 0 | Lands in | Read today by |
|---|---|---|---|
| t, tensions | 2-5 of 14, causes both remember, one way or both, depth 0.15-0.3 | `FeelingOptions.Start` (Feelings.cs:37), day-0 sentiments | every rule reading regard (Simulation.Feelings.cs:127-131) |
| b, warm ties | 0-2 of 8 | the same | the same |
| w, allies at work | co-workers from different households start warm, half the seeds | the same | the same |
| r, the rota | those co-workers split their hours, half the seeds (question 13) | `Job.Start`, `Job.End` | alarms, commute, lateness (Simulation.cs:593, 604, 637) |
| p, purses | each household in debt, short, flush or as shipped | `Economy.StartPurse` (Simulation.Money.cs:11, 93) | need, temptation, spending |
| d, old debts | a debt carried by a drawn tension | `ActOptions.StartPromises` | b2-12 |
| h, hooks | one of 8 per person | `Temperament`, haunt weights, `Economy.Wants` | the table below |
| c, chemistry | a number per pair of adults | `TownData.Chemistry` | nothing until batch 4 |

**Canon, never dealt:** Lewis is mayor (DefaultTown.cs:14), draws no hook, and no `SwayChance` is dealt (Authority.cs:44-46), though he can be a tension's other side; kin, households, homes, ages (DefaultTown.cs:336-347), and no tie joins kin or housemates; the shipped tensions (DefaultTown.cs:199-213); the newcomer, known to nobody (Simulation.cs:326-327) and in no tie, though they draw a hook and chemistry; job places, incomes, allowances, starting shops, haunts, and job hours except under r (DefaultTown.cs:298-322, 362-473); friends and vices (a hook never adds a vice: temptation ignores its weight, Simulation.Money.cs:290); the act kinds.

```csharp
// Deal.cs. All parts off: no draw, and Of returns the town it was given.
public sealed class DealOptions
{
    public bool Tensions { get; set; }  public bool Warm { get; set; }  public bool Work { get; set; }     // t b w
    public bool Rota { get; set; }  public bool Purses { get; set; }  public bool OldDebts { get; set; }  // r p d (needs b2-12)
    public bool Hooks { get; set; }  public bool Chemistry { get; set; }                                  // h c
    public int MinTensions { get; set; } = 2;  public int MaxTensions { get; set; } = 5;
    public double DepthMin { get; set; } = 0.15;  public double DepthMax { get; set; } = 0.3;  // no dealt feud
    public double BothWays { get; set; } = 0.5;  public int MaxWarm { get; set; } = 2;
    public double WarmMin { get; set; } = 0.2;  public double WarmMax { get; set; } = 0.35;    // under FriendAt
    public double WorkShare { get; set; } = 0.5;  public double RotaShare { get; set; } = 0.5;
    public double DebtShare { get; set; } = 0.10;  public double ShortShare { get; set; } = 0.15;
    public double FlushShare { get; set; } = 0.20;
    public double HookScale { get; set; } = 1;   // 0: hooks drawn, none applied (E2)
    public bool Any => Tensions || Warm || Work || Rota || Purses || OldDebts || Hooks || Chemistry;
    public static DealOptions Parse(string parts);   // "tbwrpdhc" or "all"
}
public sealed record DealEntry(string Id, string Holder, string Other, string Cause, (double Min, double Max)? Owes = null);
public sealed record DealtTie(string Id, string Holder, string Other, double Regard, bool BothWays, double OtherRegard, string Part);
public sealed record DealtShift(string Name, string Place, int Start, int End);
public sealed record DealtPurse(string Household, double Shipped, double Start, string How);   // debt, short, flush
public sealed record DealtDebt(string Id, string Debtor, string Creditor, double Amount, int DueDay);
public sealed record DealtHook(string Name, string Hook);
public sealed record Dealt(long Seed, IReadOnlyList<DealtTie> Ties, IReadOnlyList<DealtShift> Shifts,
    IReadOnlyList<DealtPurse> Purses, IReadOnlyList<DealtDebt> Debts, IReadOnlyList<DealtHook> Hooks, int ChemistryPairs);
public static class Deal
{
    public static (TownData Town, Dealt Dealt) Of(long seed, TownData town, DealOptions o);   // pure
    public static TownData Apply(TownData town, Dealt dealt);
    public static readonly IReadOnlyList<DealEntry> PelicanTensions, PelicanWarm;
    public static readonly IReadOnlyList<string> WorkCauses, HookNames;
}
```

- **Appended:** `FeelingOptions.StartSentiments` (`IReadOnlyList<Sentiment>`, empty), after `Acts` (Feelings.cs:298); `TownData.Chemistry` (`(string A, string B, double Value)`, −1..1, empty), after `Familiarity` (Town.cs:33); `ReplayOptions.Deal`, after `Label` (Replay.cs:20). `Deal.Of` copies `FeelingOptions` and `ActOptions` before setting `Start`, `StartSentiments` and `StartPromises` (one `TownData` may serve many runs, as `Copy` assumes, Program.cs:249-256), after `ActCatalog.Town`.
- **Keys** start "deal" (Rng.cs:36-41). Entries are ordered by `Rng.Unit(seed, "deal", list, entry.Id)`, ties by Id, first k taken, so a new entry moves no other's key.
- **Tensions:** k = `Rng.Range(seed, MinTensions, MaxTensions, "deal", "tensions")`; depth from ("deal", "depth", id); both ways if ("deal", "both", id) < BothWays, the other's depth ("deal", "depth", id, other); `Start[(holder, other)] = −d`, never over an existing key. It is the pair's baseline (they heal back to it, Simulation.Feelings.cs:645-650), above −0.3, so a later feud is new (:678-680).
- **Warm ties:** k = `Rng.Range(seed, 0, MaxWarm, "deal", "warm")`, strength from ("deal", "warmth", id), both ways; from `LoveAt` 0.2 a hurt stirs MakeUp (Simulation.Desire.cs:200-203) and arguments aim less (Feelings.cs:423-427).
- **Allies at work:** co-workers share a job place (spots within 8 tiles, hours overlapping on a shared workday), different households, not kin or card friends, 13+: at 26, Shane and Sam, Lewis and Penny. If ("deal", "work", a, b) < WorkShare, both start at 0.2-0.35 with a cause keyed ("deal", "work-cause", a, b); pairs in `Start` are skipped.
- **The rota:** those pairs without the mayor (Shane and Sam): if ("deal", "rota", place) < RotaShare, the union of their shifts is cut at its midpoint, the first half keyed ("deal", "rota-first", place): 9:00-13:00 and 13:00-17:00 against 9:00-17:00 and 9:00-14:00. Incomes are weekly; missing stock is found only while the keeper is present (Simulation.Traces.cs:47-49) and the trace lasts 3 days.
- **Remembered causes:** day-0 sentiments (ActId −1, Since 0): the holder `Grudge` at d; the other `Grudge` at d₂ if both ways, else `Wary` at d / 2; warm ties and allies `Thankful` both ways. No rule reads them (Model.cs:299-302); they give why-lines a cause (`why Shane Argued Kent Grudge since d0 act -1`) and fade in 28 days (Feelings.cs:96).
- **Purses:** by household name, u = ("deal", "purse", h), never the town's: u < DebtShare in debt, −100 × members × (1 + 2 × ("deal", "debt", h)); u < DebtShare + ShortShare short, 25-50% of the shipped start; u ≥ 1 − FlushShare flush, 2-3 times; else shipped (DefaultTown.cs:317-318). Whole g; money conserved.
- **Old debts (d-6):** `barred` and `the-shelves` carry debts; drawn with `OldDebts` on, each adds a `Promise(Loan)` to `StartPromises` (`Made`, `MadeAct` −1; amount keyed in the band; due day `Rng.Range(seed, 14, 56, "deal", "due", id)`) and `Indebted`/`Owed` sentiments at 0.1; no purse moves at day 0; a missed day is S4 with a visible cause.
- **Hooks:** everyone but the mayor, by name, `HookNames[Rng.Range(seed, 0, HookNames.Count − 1, "deal", "hook", name)]`; traits clipped to [0.02, 0.98]. **Chemistry:** each pair a < b of unrelated adults, not housemates, ("deal", "chemistry", a, b) + ("deal", "chemistry2", a, b) − 1 (a triangle on (−1, 1)), × 1.5 for a romantic, clipped.

**The lists.** Entries meet (a shared job or haunt place at overlapping hours, hubs excluded; a test checks it), are free to quarrel (not kin, housemates, card friends, the newcomer or a child: arguing needs 13, DefaultTown.cs:125), are new (no shipped tension and none of the commonest feuds: *probe* Sam–Shane 53%, Abigail–Alex 26%, Alex–Emily 22%, Alex–Lewis 21%, Emily–Pierre 21%, Haley–Pierre 20%, Alex–Pam 19%), and share the load (two entries a person a list at most). The holder is the one wronged; causes are invented templates.

Tensions (holder → other; cause):
- `sculpture` Leah → Haley; {other} laughed at {holder}'s driftwood sculpture at the Flower Dance. (VERIFY: Leah carves driftwood)
- `pool-table` Sebastian → Alex; {other} shoved {holder} over a game of pool last winter. (VERIFY: the pool table; question 1)
- `sober-up` Shane → Kent; {other} told {holder} to sober up, in front of the whole bar.
- `doctors-orders` Pam → Harvey; {other} told {holder} to drink less, in front of Gus.
- `the-chain` George → Lewis; {holder} blames {other} for letting the chain store open.
- `feed-price` Marnie → Pierre; {other} put up the price of feed, and {holder} said so in the shop. (VERIFY: who sells feed)
- `egg-hunt` Haley → Abigail; {other} won the egg hunt at the Egg Festival, and {holder} still calls it cheating. (VERIFY: Abigail and the egg hunt)
- `barred` Shane → Gus; {other} barred {holder} from the saloon for a week over a broken chair. Debt 40-120g.
- `cut-off` Pam → Emily; {other} refused to serve {holder} one night.
- `the-lessons` Jodi → Penny; {holder} thinks {other} is too soft on Vincent at lessons.
- `the-noise` Demetrius → Sam; {other}'s band practice kept {holder} up before an experiment. (VERIFY: Sam's band)
- `the-motorbike` Kent → Sebastian; {other}'s motorbike woke {holder} at dawn, again. (VERIFY: Sebastian's motorbike)
- `the-lens` Maru → Abigail; {other} borrowed {holder}'s telescope lens and cracked it. (VERIFY: Maru's telescope)
- `the-shelves` Robin → Pierre; {other} still has not paid {holder} for the shop's new shelves. Debt 150-300g.

Warm ties (both ways): `covered-shifts` Shane, Sam: Shane covered Sam's shifts the week Vincent was ill. `the-loan` Pierre, Emily: Pierre let Emily pay for her fabric later, the winter money was tight (VERIFY: Emily sews). `the-cart` Kent, Leah: Kent pulled Leah's cart out of the mud in a spring storm. `the-dress` Caroline, Penny: Caroline lent Penny a dress for the Flower Dance. `the-burn` Harvey, Gus: Harvey sat up with Gus the night he burned his hand. `the-roof` Robin, Marnie: Robin mended Marnie's barn roof for nothing after a storm. `the-mornings` Caroline, George: Caroline sat with George every morning the winter Evelyn was ill. `the-cake` Evelyn, Jodi: Evelyn baked the cake for Jodi's wedding.

Work causes: "{a} covered {b}'s work the week {b} was ill." "{a} and {b} worked through the night together when the storm hit." "{a} took the blame for a mistake {b} made at work." Both the work part and the warm list reach the pairs that feud where they work: Sam and Shane on the chain's floor (DefaultTown.cs:412-415, 442-445), Emily serving Pierre (:381-384, 425-428).

**Hooks:** the research's five plus grudging and easygoing (rule 4's "Pam lets go, Robin keeps", DefaultTown.cs:229-234) and homebody, so not every dealt seed is louder. Each works through card knobs; nothing reads its name. No trait moves more than 0.25, no weight beyond × 0.6 to × 1.5; `HookScale` scales each change.

| Hook | Card edits | Read by |
|---|---|---|
| nosy | Chattiness +0.15 | chats and tellings (Simulation.cs:1056-1058), company, missing people, Curious, Seek |
| proud | SelfRegard +0.2 | shame (Feelings.cs:445-451), confident guesses (Perception.cs:84), being left out (Feelings.cs:561-567), the pride term (apologies, peace offers, loan requests), contests, Envy |
| competitive | Boldness +0.1; `Argued` × 1.5 if carried | daring at every gate (Feelings.cs:481-483), standing for constable (Simulation.Authority.cs:65), confronting, temptation, reporting, `PlayedGame` × 2 |
| romantic | Sensitivity +0.1; `GaveGift` × 1.5 if carried | every felt amount (Simulation.Feelings.cs:120), gifts, Pity for the lonely, StoodUp's hurt, chemistry × 1.5 |
| spendthrift | `Economy.Wants` × 1.5 | want prices and pressure (Simulation.Money.cs:253-258, 293), `TreatedToDrink` × 2, tabs and Need sooner |
| grudging | Retention +0.25, Understanding −0.1 | keeping, excuses, credence (Feelings.cs:381-397), healing (Simulation.Feelings.cs:647), the Ask's and apology's costs, Peace |
| easygoing | Retention −0.2, Understanding +0.1 | the same, the other way |
| homebody | haunt weights × 0.6 | free time against home (Simulation.Desire.cs:130-136); hubs keep their weight (Simulation.Growth.cs:163-164); visits find them home |

### 6.5 Day 0 only, the mind test, and the runner

`Deal.Of` is pure; the run reads dealt values where it reads any town's: cards (Simulation.Character.cs:14), regard and sentiments once in `StartFeelings` (Simulation.Feelings.cs:80-105), purses once in `StartMoney` (Simulation.Money.cs:87-100), old debts once (2.6). The one hook, after `Start` and before the trough loop (Simulation.Feelings.cs:99-104):

```csharp
foreach (Sentiment s in _fo.StartSentiments.OrderBy(x => x.Holder, StringComparer.Ordinal)
             .ThenBy(x => x.Toward, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal))
    if (s.Holder != s.Toward && _index.ContainsKey(s.Holder) && _index.ContainsKey(s.Toward))
        AddSentiment(s.Holder, s.Toward, s.Name, -1, s.Strength, 0); // Simulation.Feelings.cs:465; logs "0 sentiment … act -1"
```

**`MindTests.cs` (d-1)** is design rule 15's promised test that an occasion cannot write a mind (design.md:232, 488; none exists), with the deal first and lost items next: (1) an IL scan of every `Simulation` body (`call`, `callvirt`, `newobj` via `Module.ResolveMethod`) finds no `Deal`, `DealOptions` or `Dealt` method; (2) no `Simulation` field has a deal type; (3) `Deal`'s statics are `initonly` immutable records; (4) for seeds 1-20, `Deal.Apply(TownData.Default(), dealt)` equals what `Of` returned and runs to the same hash; (5) after construction, `PersonalRegard`, `Baseline` (Simulation.Feelings.cs:68-69) and `Character` (Simulation.Character.cs:19) match the record, and only the minute-0 sentiments and why-lines cite act −1.

**Runner, replay, viewer.** `--deal <parts>` applies per seed in `Make` (Program.cs:194-202), after `--tensions`, `--fo`, `--acts`, `--catalog`, before `--trait`. Sentiment printing (Program.cs:216-218) prints act −1 as "before the run" with its cause. After the report: tensions a run, one-way share, most dealt, became a feud or reconciled; warm ties and allies, became friends or feuds; rotas; purses dealt and households in debt at the end; hooks; chemistry pairs. `--log` prints the deal first (`dealt: Leah holds a grudge against Haley (-0.22): Haley laughed at …`). The replay writes a top-level `deal` block (ties with cause keys and sentences, shifts, purses, debts, hooks, a chemistry count), outside `settings` (GlossaryTests.cs:86); dealt ties also show in `settings.Tensions` (Replay.cs:339-340). The Population tab gets "How this town began", and stories link to the dealt ties they start from.

### 6.6 The deal's target and gates

**Target** (research section 3): Sam and Shane's share in V1's feud family falls from about 55% to 30% or less, and no other pair passes 30%, over 200 seed-years with and without `--deal`, read from the feud family and the top-feuds line (Program.cs:379; Metrics.cs:249-251), on the shipped town and the 0d.6 candidate (`--0d6 bdefghm`). **Gates with `tbwrph`:** the feud family 30% or less; E1 60%+; war and dead towns each under 5%; news within the ceiling (8.1); money exact; C13 reported; three-year regard within ±0.02 a season; hermits and brawlers ±30%; E2: each part, turned off, moves a story metric 20%+ (chemistry exempt).

**The probe** (throwaway: no day-0 sentiments, two earlier entries, allies without the 8-tile test, a chain-only rota; 60 seed-years, about ±6 points near 30%):

| Seeds 1-60 | shipped | t | tb | tbph | +w | +r | +w+r |
|---|---|---|---|---|---|---|---|
| Sam–Shane feud | 53% | 53% | 50% | 53% | 40% | 35% | **28%** |
| E1 | 63% | 68% | 75% | 70% | 82% | 55% | 70% |
| news a year | 254 | 252 | 250 | 280 | 274 | 256 | 251 |

Tensions are texture (9-11% became a feud; a cool start stirs no motive, since motives come from events), kept as the causes the viewer and the fair-twist rule need (question 14); warm ties work (14-19% became friendships); hooks and purses raise feuds 18% and reconciliations 31%, and news to the ceiling; Sam and Shane's feud comes from five hours a weekday on one floor, both carrying `Argued` (DefaultTown.cs:415, 445): allies alone make them a repeated friendship (32%), the rota alone drops E1 to 55%, both meet the target. **Recommendation:** t, b, w in d-1; p in d-2; h in d-3; r in d-5 if Sid agrees; the feud-family gate applies once w and r are on.

---

## 7. Opt-in, pins and tests

### 7.1 Off means off

**These pins hold** with every batch 1 and batch 2 row appended, every switch off, no deal and no fork: `PinnedTests.cs:36-38` (`e7f6653087ff7e18`, `388d128d7fd4cdf2`); `:101-106`, the five gate pins, P3 `e9fd83b284f5c1b6` among them; `:138` and `:180` (`c0488cc6bf81e64f`); `:148-154`, the six 0d.6 pins; `:167`, watching every 0d.6 rule gives P3; `CatalogTests.cs:170-193` (P3 and the two feelings-off hashes with the catalog appended); `Town31Tests.cs:125` (`db268a57b95bada2`); `TownGrowthTests.cs:354` (forgetting on, `0fb826867e0573b5`); `TownDataTests.cs:16-18`; `TownGenTests.cs:22-24`, the 60 towns' census hashes (`6a3457c801ecf24d`, `59912ac5fdd8ac4c`, `a953219dce6d71a7`), and `:272`, the 7-day log hash of `pelican:60@1` (`c8990fcb4c6537c7`, generator version 2). The census hash covers the whole town file today, so appended `ActKind`, `Gathering`, `FeelingOptions`, `ActOptions` and `TownData` fields move it until #60 (the canonical hash) lands; b2-0 and d-1 either land after #60 or re-pin those three with a note. `TownJson` writes every field, so appended fields must round-trip (`TownJsonTests`).

**Why they hold:** every rule checks its switch first and returns before any draw, line or change; a switch that is off draws nothing; every batch-2 row has `PerDay` 0 (Simulation.cs:802-803) and `Temptation` names its kinds (Simulation.Money.cs:288); `ScandalFor`'s pool is unchanged; `IsCrime` equals `IsScandal` on shipped kinds, no shipped Given row reaches the new branch, `JoyOf` is × 1, a gathering with no `Date` is on as today; `Circles` and `FestivalDays` are measurements the engine never reads and the log never shows; with `Watch` and every slice on, the run gives P3.

### 7.2 What every slice shows

(1) Off: every pin in 7.1 with its rows appended. (2) Watch: P3, and `CatalogWatch` holds the slice's rules. (3) Scenes: one per kind or rule, in `SteeringTests`' style (DesireSceneTests.cs:6-15): one long room, nobody wanders or tires, acts at 10:00 on day 0, `FeelingOptions.WithDesire` with the slice on. (4) Determinism: two runs, one hash, any culture (PinnedTests.cs:11-30). (5) A pin with only the slice on: seed 1, 112 days, recorded when it passes. (6) Section 8's gates, its own criterion, E2.

### 7.3 Tests, act slices

| Slice | File | Tests |
|---|---|---|
| b2-0 | `CatalogTests.cs` (extended) | 7.1 with every row appended; `ScandalFor(s, ActCatalog.Kinds(every switch on))` equals `ScandalFor(s, DefaultTown.Acts())` for seeds 1-400; `IsCrime == IsScandal` on shipped kinds; `Ask` reproduces acts-4's apology answers; `Computed` leaves Fond's draws unchanged; Given deeds resolve None and no shipped row reaches it; `JoyOf` × 1; an undated gathering is on as today; watch with every slice on gives P3; `BeginUnseen` writes an act nobody watches whose knowers hold it as given |
| b2-1 | `VisitSceneTests.cs` | a call let in, both stay an hour; nobody in, and the next call comes at another hour; a shut door; a visit that carries an apology (acts-4 on); the shy stay home; joining someone alone and low; Seek after 90 minutes |
| b2-2 | `PromiseSceneTests.cs` | met; stood up by bed, detention, a change of mind, the forgetting draw; the waiter's Answer and the no-show's Remorse; a job clash refuses with no sting; one promise per pair |
| b2-3 | `PeaceSceneTests.cs` | a feud quiet 14 days, an offer, dislike halved and hits cleared; a refusal and its cooldown; no offer after a fresh argument; never kin; the Winter Star doubles the wish |
| b2-4 | `FestivalTests.cs` | a dated gathering is on only on its date, every year; a holiday stops work, patrol and other hubs, and work resumes; the shipped town on day 23 at 11:00: everyone awake and free is in the crowd, nobody at work, shops shut; the tired go home from a night festival; `FestivalDays` counts |
| b2-5 | `ReachTests.cs` | a queued confrontation waits for the two to meet; earshot holds the scandal at 0.6 × base with chain [confronter], a wall blocks; the accused is felt confronted once; a verdict is read at the market 180 minutes in, listeners confirmed, a let-off never read; switches off, `CheckScandals` as today |
| b2-6 | `ReachTests.cs` | a settled act is told at half juiciness and never to a listener under 0.2 with the person named; a late settlement logs `settle-late` and changes nothing; restitution to the gram; a settled victim doesn't report |
| b2-7 | `ReachTests.cs` | a culprit named in an interview who doesn't dislike the victim feels Remorse and pays back first; kin have a row instead |
| b2-8 | `LoudSceneTests.cs` | heard through a `#` wall and a door, never named, at half juiciness; a witness's telling fills in the name; a scene only at intensity 0.6+ and expression 0.7+ |
| b2-9 | `BanSceneTests.cs` | a scene, a ban, the ban lifts; a theft, a ban; love covers |
| b2-10 | `CounterSceneTests.cs` | a trip pays its share at the keeper's price; a shut shop, then the rival; a bag seen by the keeper's household, once a day; money |
| b2-11 | `PartySceneTests.cs` | a birthday, a plan, invitations in reach, the night; left off when told (corroboration strength) and when seen (full) |
| b2-12 | `DebtSceneTests.cs` | a tab opens when short and is paid when full; unpaid at the due time, an unseen BrokePromise only the creditor holds, at 4.0, Witnessed, told, never reported, confrontable; a paid debt makes no act; Owed then Banned; Pity then ForgaveDebt, which settles it; a call-in before an audience announces it |
| b2-13 | `DebtSceneTests.cs` | asked and lent; refused, then the wait; repaid on meeting; forgiven only overdue, only by someone fond; called in before an audience |
| b2-14 | `LostItemSceneTests.cs` | found and returned; a stranger's handed in; kept out of need; found out by a visitor three weeks later; the owner sees it in public |
| b2-15 | `ReachTests.cs` | Blundered only in a festival crowd; the same person blunders at the same festival in two runs; five witnesses humiliate, four do not |
| b2-16 | `ContestTests.cs` | entrants by trait; the winner by score; a proud loser's Envy, then SoreLoser; never toward kin |
| b2-17 | `ReachTests.cs` | a forced sway makes an unseen LetOff with the keeper as target; after 5 days the keeper meeting the mayor holds it at 4.5 × (1 − standing), confirmed; the accused and kin never tell it; `Partiality` off, a forced sway is a plain let-off with no act |
| b2-18 | `ReachTests.cs` | a post is read once per person within 3 tiles and expires after 7 days |

### 7.4 Tests, cloud

- **`ScenarioTests`, `ReachMetricsTests` (m-0):** `Circles` holds every placed act's circle with the hash unchanged; a scene places the same act twice, and two scenes with one key differ only after their follow-up; the predicates in hand-built worlds (one loner, nobody, kin only, at a hub); `NoTrace`; a scene that can't happen is dropped after `GiveUpDays` with a log line; `ReachMetrics` on hand-built results (reach by `GotTick`, the circle floor of 3, ties in the rank correlation, C13 equal to the runner's band); `--check today` on 3 seeds prints five results.
- **`VarietyTests` (m-1, m-2)**, beside the six built: on made-up results (a helper fills `SimResult`'s required fields), FirstScandal then Scandal with no verdict on record, BlameMoved needs a 2-holder lead that changes name, Humiliated needs an audience of 3, Upheaval from a shop switch, LetOff from a verdict; fairness (a Reconciled after a dealt tension is fair; one whose hate came from an act nobody saw is not; a FellOut after an argument a third person saw is fair; V6 counts fair twists, `V6All` all); families pool (Feud and FellOut for one pair count once, and FirstScandal and Scandal for one person); forks (seed 1 forked at day 28 matches the base before minute 40320 and differs after; no fork gives P3; one seed, day and salt give one hash); V7 on made-up runs; purity (two calls agree; the input is unchanged).
- **`DealTests` (d-1 to d-6):** off by default (`Deal.Of` returns the same `TownData` and an empty record; P3 and `388d128d7fd4cdf2` hold through `TownData.Default()`); the same seed deals the same town in any culture; keyed per entry (a 15th tension changes the set only where it is drawn); canon over seeds 1-200 (mayor, kin, households, ages, job places and hours unless r, homes, friends, act-weight keys, incomes, allowances, the four shipped tensions at −0.3, the newcomer in no tie at familiarity and regard 0, no trait moved over 0.25, all in [0.02, 0.98]); every listed pair meets and none is kin, housemates, friends, the newcomer, a child or a shipped tension; money conserved with every part on (seeds 1-20 × 112 days); `ScandalFor` unchanged (seeds 1-100); chemistry changes no hash (seeds 1-3); minute-0 lines are exactly the dealt sentiments, and a holder's argument cites `Grudge … act -1`; no dealt tie starts a feud; the rota changes only the co-workers' hours, covers their union, never the mayor; a dealt debt opens a day-0 promise and a missed day makes a BrokePromise with `About` −1.
- **`MindTests` (d-1):** the five of 6.5.
- **`PinnedTests` gains** `TheDealIsPinned(parts, hash)`: `Metrics.LogHash(new Simulation(1, Deal.Of(1, TownData.Default(), DealOptions.Parse(parts)).Town).Run(112))` for "t" and "tbw" (d-1), "tbwp" (d-2), "tbwph" (d-3), "tbwrph" (d-5, if Sid agrees), each recorded when its slice lands, as 0d.6's were (PinnedTests.cs:148-156).

### 7.5 Golden threads (design section 8)

Each is written before its slice's rules:
- **A tab at the saloon** (design story 4; b2-12, b2-5, b2-9, b2-8): Pam's tab goes unpaid; Gus calls it in at the bar before four people, and most of the town holds it in two days. On one seed she pays; on another he forgives; on a third he bars her after she makes a scene.
- **Stood up** (b2-2, acts-4): an accepted invitation, a forgetting draw, StoodUp; the no-show feels Remorse, apologises and is forgiven.
- **The purse** (b2-14, b2-1): lost on the beach and kept; a visit three weeks later finds it; a confrontation; it is handed back.
- **Shut on the day** (b2-10): Pierre misses his alarm; Jodi finds the shop shut and goes to the chain; Caroline sees the bag; Pierre cools on Jodi.
- **The Winter Star** (b2-3, b2-4): a quiet feud ends in an offer of peace in festival week.
- **The laugh at the festival** (design story 5; b2-15, b2-4): someone blunders at the Luau before ten or more; their circle holds it by the next day.

---

## 8. Measures and gates

**Baselines first.** acts-0's follow-up writes the acts spec's baseline (`StoryMetrics`); m-0 writes today's C checks; the variety baseline is in 6.3; b2-0 writes 8.2's base (batch 1's measures with batch 1 on). Each on the shipped town and the 0d.6 candidate, from the code being judged.

### 8.1 Gates every slice must pass

| Gate | Today | Must hold |
|---|---|---|
| E1 (seed-years with a new feud and a new friendship between households) | 60% shipped, 42% at 31; 69-78% for the 0d.6 candidate | 60%+ at 26; dead and war towns each under 5%; grown towns by #61's scaled gates |
| Three-year drift | flat, `FondDays` 14 | mean regard ±0.02 a season; gate kindnesses in year 3 at most 1.5× year 1 |
| Feuds, friendships per person | 7.7 and 0.9 a year at 26 | each ±30%; the share between households no lower |
| Money; cost | exact; 0.08-0.11 s a day, replay 1.16 MB per 28 days | exact to the gram; at most 1.5× |
| News | about 265 a year (acts spec 7.2) | at most today's + 10% (9a), batch 1, batch 2 and the deal together |
| C13; C3, C4, C10, C11 | 55%; 5.5 | reported; C13 52%+ at 26 (the pins hold it); C3 and C11 fail today and gate nothing until Sid sets them (question 5) |
| The slice's own check | | b2-4 C1; b2-5 C9; b2-6 C5; b2-12 S4; b2-15 S9; b2-17 S6; b2-18 C9 by the board; m-3 C12 |
| V1-V8; E2 | 6.3 | reported for every slice (the deal's gates, 6.6); E2 per 8.3 |

### 8.2 Batch 2's budgets (per 112-day year at 26; first guesses)

- **News:** about 265 today; batch 2 adds about +18 (StoodUp 4, MadePeace 1.5, WonContest 3, SoreLoser 1.5, CalledInDebt 1, ForgaveDebt 0.5, Confronted about 2, Blundered about 5; Banned and MadeScene replace arguments). It must stay under the shared ceiling (about 290); a slice that would cross it waits until it trades volume or Sid raises it.
- **Scandals:** about 2.8 tempted today (sim/README.md:156); BrokePromise 0.5-3, KeptFoundItem exposed 1-3, LetOff about 0.03; reported (Sid noted the town has too few).
- **Story trivia:** about +160; at most +25% over batch 1's base. **Light acts:** Visited about 200, JoinedThem about 300, BoughtFromRival about 10; reported apart, as company.
- **Heavy hostile from the gate:** about 155; SoreLoser and CalledInDebt about +2.5; at most +15% for batches 1 and 2 together. **Confrontations:** ±20% a season. **Median feud:** 28 days+.
- **Hermits (0d.6 candidate):** 0.29-0.30 a seed-year; above 0, 90%+ from the shyest third. **Money:** exact. **Cost:** at most 1.5× (`VisitMotives` scans everyone; overhearing; the checks' runs).

### 8.3 E2, for each slice

Turning a slice off must move its metric by 20%+; a lever is judged by its check; a slice that moves nothing is cut or kept as texture if Sid wants it. b2-1 friendships between households per person; b2-2 threads a season; b2-3 reconciliations a year; b2-4 tellings between households, C1; b2-5 C9; b2-6 C5; b2-7 the share of scandals settled; b2-8 holders per hostile news act; b2-9 feuds involving a keeper; b2-10 shop switches; b2-11 friendships among guests; b2-12 the trailer's bin scandals, S4; b2-13 need-driven scandals (`SimResult.Motives`); b2-14 natural scandals; b2-15 S9; b2-16 feuds between households; b2-17 S6; b2-18 C9 by the board; d-1 to d-5 a story metric or V measure each, chemistry exempt.

### 8.4 Scale

After the batch gate, rerun the gates at 31 (`Towns.Pelican31`) and at 60 (`pelican:60@1` and 100 town seeds, #61), per person and per district (town spec 6.2), with C12. Batch 2 has no rates, so nothing thins as the town grows; what changes is who is near.

---

## 9. Slices, in order

Each slice shows 7.2 and passes 8.1 before the next; one may go earlier only where "Needs" allows. Contents: 2.10 and 3.0 for the act slices. Batch 1's needs: 1.3.

| Slice | Needs | Its own criterion (first guesses) |
|---|---|---|
| b2-0 Seams | acts-1 | 7.1; the b2-0 tests; 8.2's base in `sim/README.md` |
| b2-1 Visits | b2-0 | 0.3-2 visits per adult a week; failed calls fall from season 1 to 2; the shyest third at most a third of the boldest's; 0d.6 hermits above 0, 90%+ from the shyest third |
| b2-2 Dates | b2-0 | accepted 50-80%; StoodUp 5-20% of accepted; a thread a season of Invited → StoodUp → Apologised (acts-4 on); news +2% or less |
| b2-3 Peace | b2-0 | accepted 30-60%; median feud 28 days+; E1 |
| b2-4 Festivals | b2-0; m-0 for C1 | 90%+ of the awake by day; C1; festival-day tellings 1.5× an ordinary day's |
| b2-5 Announced | b2-0 (b2-4 for festival read-outs) | C9; confrontations ±20% a season; E1 |
| b2-6 Settled | b2-0 | C5; money; reports per scandal measured |
| b2-7 Amends | b2-6; acts-4 | share settled without the harness reported; E1; median feud 28 days+ |
| b2-8 Scenes | b2-0 | Argued + MadeScene within +5% of Argued alone; a hearer per scene in the median |
| b2-9 Bans | b2-0 (b2-8 adds triggers) | 1-6 bans a year; no banned person in the place; never staff, kin or housemates |
| b2-10 Counter | b2-0; money | money; grocery spend a season within rounding of today's; FoundItClosed 1-6 a season |
| b2-11 Hosting | b2-2 | 2-8 parties a year; at most 3 left off a party |
| b2-12 Debts | b2-0, b2-5's delivery; money; b2-6 to settle | 0.5-3 broken promises a year; never reported; S4; money |
| b2-13 Loans | b2-12 | 1-6 loans a year; 50%+ repaid; money |
| b2-14 Items | b2-1; money | 5-20 lost a year; 50%+ returned; 20-60% of kept found out in 56 days; each exposure cites a find a day+ earlier |
| b2-15 Blunders | b2-4 | S9; the news ceiling; hermits 90%+ from the shyest third |
| b2-16 Contests | b2-4 | 3+ entrants per adult contest; at most 1 sore loser each; the hostile budget |
| b2-17 Let-offs | b2-0; m-0's `sway` (b2-5 to confront the mayor in public) | S6 |
| b2-18 Board | b2-5 | C9 by the board |
| b2-z Docs | all | 8.1 with every slice on, over one year and three; the rerun at 31 and 60; design rules 2, 9, 11, 12, 13, 15, 16 "as built"; design section 10 (the band replaced); the tiers' examples; `sim/README.md`; the viewer's sentences; this spec's status |
| m-0 Harness: scenarios, circles, `ReachMetrics`, `--check` | nothing | every pin; `ScenarioTests`, `ReachMetricsTests`; today's C2, C3, C4, C6, C10, C11, C13 in `sim/README.md` (shipped and 0d.6) |
| m-1 Story events, part 2: `Variety.Events.cs`, chains, fairness, new kinds, families | nothing (`Variety.cs` is built) | every pin; `VarietyTests`; V1-V6, V8 re-measured with families and fairness (200 seed-years, shipped and 0d.6) |
| m-2 Forks: `Fork`, V7 | m-1 | every pin without a fork; the fork tests; V7's baseline (50 × 3) |
| m-3 Checks as levers land: C1, C5, C9, C12, S4, S6, S9; each kind's story event | m-0; each slice; the 60 town for C12 | each check's scene before its slice merges; each kind's pattern and test with it or right after; C12 at 60 |
| d-1 Ties (t, b, w); `StartSentiments`; `MindTests` | m-1 for its gates | 6.6's gates for t, b, w, the feud family reported; E2 per part; `DealTests`; pins t, tbw |
| d-2 Purses (p) | d-1 | money; households in debt at the end at most twice shipped; tempted scandals ±50% of 2.8; the culprit family reported |
| d-3 Hooks (h) | d-2 | hermits and brawlers ±30% from the expected people; the constable's top share no higher; news within the ceiling; V5, V8 reported |
| d-4 Chemistry (c) | d-1 | one hash with and without it |
| d-5 Rota (r) | d-1; Sid's yes (question 13) | 6.6's full gates, the feud-family target included |
| d-6 Old debts (d) | b2-12 | DebtCalledIn and DebtRevealed appear and are fair; the debt's reach reported in S4 |
| m-z Docs | all | `sim/README.md`; design.md "as built"; this spec's status; Sid reads a season of three dealt seeds beside the shipped town |

**Across the tracks:** a slice that makes a check testable (b2-4, b2-5, b2-6, b2-12, b2-15, b2-17, b2-18) merges after m-0 and that check's scene; whoever of b2-0 and m-0 lands second adds `RecordCircle` to `BeginUnseen`; d-6 waits for b2-12, C12 for b2-4; the 60 town's keepers and shop roles widen bans, the counter and tabs, and nothing waits for them.

**Why this order:** measures first, so slices are judged on numbers from the same code; visits, then dates, because going to a person and the first promise are what peace visits, guests, items, hosting, debts and loans reuse; peace before scenes, bans and contests, so hurts have a way out before new hostility; festivals, announced and settled early, being cheap and the makers of C1, C9 and C5; blunders and contests need festivals; let-offs are rare and need only the harness; the deal's ties need nothing.

---

## 10. What waits for later batches, and why

- **Batch 3:** seeded secrets (mechanism 3; they need rule 14's trust tiers, Confided and RevealedSecret, or they are unfair twists); Hushed and Denied (C7, C8); trust from kept promises; surprise in gossip (mechanism 12, law 13) and +0.5 for public figures, which re-pin every gate and C11 (habits know only hours, Simulation.Habits.cs:40-52); rare spikes (mechanism 7: they need the acts they spike into, and the news budget is full).
- **Research step 5:** life goals (mechanism 4; no Strive motive); upheavals with warning (mechanism 5; Banned and the grocer switch are first seeds); a storyteller over occasions (mechanism 6; rule 15's catalog starts with lost items; question 2).
- **Batch 4:** named rivals and loves (mechanism 8; courting); starting courtships; chemistry's first reader. **Phase 0e:** arrivals and departures (mechanism 9). **After batch 3, Sid's call:** town mores per run (mechanism 11).
- **Later:** a past record for the deal (`AuthorityOptions.Record`, a past nobody knows); a deal for generated people (the generator draws their tensions, town spec 4.3 step 10); RiggedContest (S7), a softer let-off, TookBribe (with the lies, 7b); a lying or less fair official; an argument that announces a scandal; recording scenario runs (replay version 2); shop roles for generated towns (town T6, E8).
- **Built here:** fixed dates with open outcomes (mechanism 10): festivals and contests.

---

## 11. Risks

1. **Festivals flatten variety.** Report C11 by "a festival within 3 days" and by exposure; festivals should make the town-wide tail (C11 wants 10% at 90%+; today 1%), not move the median.
2. **Festivals at 60 with forgetting on** speed the fade of weak ties that week (Simulation.Growth.cs:258, 270-271): measure circle sizes over a year at 60, with and without.
3. **Public confrontations move E1:** confrontations ±20% a season; E1 and feuds in their gates.
4. **One news ceiling, three claims:** batch 2 (+18), batch 1's StoodUpFor, the deal's hooks and purses (280 in the probe; work and the rota brought it to 251). Measure each slice with everything merged before it.
5. **C3 and C11 fail today** (question 5). **6. Settling blinds the authority:** measure reports per scandal with b2-6 on.
7. **A new scandal kind moves the placed scandal:** `Placeable: false` on every new scandal row; b2-0's test catches a miss.
8. **Two builders in the same files:** `TryTell` (Simulation.cs:1080-1150) takes four hooks from b2-6 and b2-17; acts-1 to acts-6, b2-0 and d-1 all touch `Acts.cs`, `Feelings.cs` or `Simulation.cs`. 1.2's rule; kind names fixed in section 3.
9. **Lewis is always honest,** so every let-off can come out; a lying official waits for 7b.
10. **Repair and peace empty E1 of feuds:** quiet feuds only, one offer per pair in 28 days, Remorse only toward someone not disliked, the median feud gate.
11. **The shy are rescued from becoming hermits:** chattiness scales Seek, the dial, the costs, light acts out of the kindness tally; hermits 90%+ from the shyest third.
12. **The core stops being itself** (research question 3): the canon list (6.4), hooks bounded at 0.25, `HookScale`, Sid's read in m-z.
13. **The deal's lists make their own repeats:** allies at work made Lewis–Penny a repeated friendship (25-27%); V1 counts friendships; lists grow with the town.
14. **The biggest repeats stay** (V1 92%, V2 2.7) until tabs and loans land.
15. **Unfair twists:** purses, hooks and rotas are unseen; only visible causes count (a dealt tie is visible, a hook is not).
16. **Hearsay stirring too much (5b):** only `HeardStirs` kinds, only about the hearer, at corroboration strength.
17. **Cost:** `VisitMotives` scans everyone; overhearing runs each minute of a loud act; V7 adds 150 seed-years; each check runs 400 seeds; the runner keeps every `SimResult` (Program.cs:244-245), so compute seed-years and checks as each run ends.
18. **Census hashes move with appended fields** until #60 lands (7.1).

---

## 12. Questions for Sid

The first four are the research's; the rest come from the drafts and the merge. **Sid's answers (2026-10-08)** follow each question; where they differ from the recommendation, the sections they touch are to be revised before their slices are built.

1. **Is a non-lethal scuffle allowed?** (a) No, violence stays out; (b) later, a shove with no injury behind its own switch, with mechanism 7's breaks; (c) yes, in batch 2. *Recommendation: (a) for batch 2, and ask again with the breaks.* Under (a) the deal's pool-table cause becomes "{other} beat {holder} at pool and gloated."
   **Sid: (c),** a scuffle in batch 2. Its row, its breaks (mechanism 7) and its place in the tone limits are to be specified in section 3.
2. **May the storyteller react to measured tension, or only follow a schedule drawn at setup?** (a) A schedule only; (b) it reacts; (c) (a) by default, (b) behind a switch. *Recommendation: (a) first:* it keeps occasions apart from minds (rule 15; the mind test) and V7's forks meaningful. Revisit (c) once V7 is measured.
   **Sid: (c):** a schedule drawn at setup by default, with reacting to tension behind its own switch.
3. **How far may the deal move a core villager?** (a) Ties and purses only, no hooks on the core; (b) bounded hooks, as here (a trait by up to 0.25, a weight × 0.6 to × 1.5); (c) larger. *Recommendation: (b),* with `HookScale` as the dial.
   **Sid: (b).**
4. **Should a new game avoid your recent runs?** (a) No, each seed stands alone; (b) yes, now: the runner skips seeds whose deal repeats a recent headline; (c) later, from a record kept outside the simulator. *Recommendation: (c):* the deal stays a pure function of the seed.
   **Sid: (c).**
5. **The lone witness (C3) and natural reach (C11).** One witness, even a loner, carries a scandal to a median of 42% of the circle; under 20% in only 8% of runs. (a) Keep C3 as a target for the 60 town's scattered edge and for hermits only; (b) let a teller's daily listeners depend on chattiness, `TellsPerDay` × (0.5 + chattiness), which re-pins the shipped town; (c) drop C3. *Recommendation: (a);* C11's 40-70% median waits until festivals and announcements are measured.
   **Sid:** "I think we are missing the social pressure to not spread a scandal." So none of (a)-(c) yet: a rule for that pressure comes first (to be specified with the reach levers, section 4), measured against C3 and C11; then C3's target is read again. Cloud proposed its forms (holding back about someone liked, a teller losing regard for spreading about the well liked, friends confronting the teller), and Sid agreed (second round).
6. **Festivals in the shipped town** (VERIFY against the game). Work and shops: (a) stop for the festival's hours, as written; (b) keepers stay at their counters. *Recommendation: (a),* as the game is recalled to do. The forest festivals: (a) the Square stands in for the Flower Dance and the Festival of Ice; (b) a clearing off ForestPath, for festivals only; (c) skip them. *Recommendation: (a) at 26, (b) as a green in grown towns.* Night festivals: (a) the tired go home (48-68% of the awake come); (b) a festival keeps everyone up. *Recommendation: (a):* a night festival of night people is its own story.
   **Sid: the recommendations:** work and shops stop (a); the Square at 26 and a green in grown towns; the tired go home (a).
7. **What does Lewis say when asked about a let-off?** (a) The truth, as written; (b) he deflects, which is a lie and waits for 7b. *Recommendation: (a) now, (b) once lies exist.*
   **Sid: (a),** until lies exist.
8. **When does a settlement count?** (a) Only before the second retelling, as the research says; (b) within a day, however often told. *Recommendation: (a),* measured by C5's share settled too late; switch to (b) if most come too late.
   **Sid: (a).**
9. **Should a kept purse be reported to the mayor?** (a) Not yet: the owner and the town deal with it; (b) yes, with `PayUp` repaying the owner instead of the place's keeper (Simulation.Money.cs:362). *Recommendation: (a).*
   **Sid: (a).**
10. **Visits to kin in another household** (grown towns only; at 26 all kin live together). (a) Never; (b) behind its own switch once the 60 town has kin across households. *Recommendation: (b).*
   **Sid: (b).**
11. **Should Pierre answer when a regular buys from the rival?** (a) Regard only, as written (a Given target); (b) a motive to answer, which can become an argument. *Recommendation: (a),* to protect the hostile budget.
   **Sid: (a),** the recommendation, after first leaning to (b) and asking why (a). The answer: to protect the hostile budget, since each new source of arguments can feed a spiral, as acts-5's Sides showed (#69).
12. **May the deal start some pairs warm?** The research lists tensions only; in the probe, warm ties and allies at work raised E1 to 70-82% and friendships by half without raising news. (a) Yes; (b) tensions only. *Recommendation: (a).*
   **Sid: (a),** yes.
13. **May the deal set the hours of co-workers from different households (the rota)?** Sam and Shane's feud falls to 30% or less only with allies at work and the rota together; at 26 the rota touches only them, and changes their hours, not their jobs or pay. (a) Yes, behind its own switch; (b) no: drop the V1 feud target for the deal and remeasure after acts-4's Repair and batch 2's MadePeace. *Recommendation: (a).*
   **Sid: (a),** yes.
14. **If tensions fail E2 in d-1, do they stay as texture or go?** They are the causes behind the why-lines and the viewer's opening state. (a) Keep them, as 0d.6 kept contagion; (b) drop them. *Recommendation: (a);* remeasure once acts-5's Sides and batch 2's MadePeace give a cool start more to act on.
   **Sid: (a),** yes.
