> Draft spec, partly built (see Status below). It merges two design proposals of 2026-10-08 (design A, "everyday life first", and design B, "story first") with two read-only reports: the act model and the town at scale. Once it is built, `sim/README.md` and `docs/under-glass/design.md` ("As built") will say where the code differs and why.

> **Current planning (2026-10-09):** use the [roadmap](../roadmap.md). The optional batch 1
> mechanics are built; reflection currently offers only gift, help and confrontation. Connecting
> suitable catalog actions to reflection follows the contextual-encounters slice. Original
> E1/E2, news-volume and build-order requirements below are historical experiment criteria.
> The dated slice notes describe successive implementations; current code and `sim/README.md`
> settle the present API and enabled defaults.

# Under Glass: the act catalog, batch 1 (proposed phase 0d.7): implementation spec

**Status (2026-10-08).** Batch 1 is built (acts-0 to acts-6), every slice off: acts-0's seams on `claude/local-acts-0`, its measures and baseline on `claude/local-acts-0b`, acts-1 (Returns) on `claude/local-acts-1`, acts-2 (Company) on `claude/local-acts-2`, acts-3 (Welcome) on `claude/local-acts-3`, acts-4 (Repair) on `claude/local-acts-4`, acts-5 (Sides) on `claude/local-acts-5` and acts-6 (Late) on `claude/local-acts-6`. Every switch is off, and every pin holds.
- **Built:** `Acts.cs` (`ActGate`, `ActOptions` with its seven switches, all off, and the constants of 2.3); the appended fields and enum values of 2.1; `FeelingOptions.Acts` as its last property; `GateOf` with the legacy table for the shipped rows, and `Form`, `Min`, `ActsFor`, `IsLight` and `IsKindAimed` reading it (2.2), with return in kind or less and the pride term; `Fits` with the hours and the gate's limits (3.1), in `Simulation.Acts.cs`; `Begin`'s `with`; `ActCatalog.cs` with `Batch1`, `Kinds` and `Cards` (2.4), with no rows and no cards yet; the runner's `--catalog`; the replay records the switches; and `CatalogTests.cs` with tests 1 and 2, test 3 without `CatalogWatch`, and a determinism check (section 6).
- **Done differently:** `ReplayOptions` gets no `Kinds` or `Cast`: it already takes a whole `TownData` (`ReplayOptions.Town`), which carries both. For Return, a kindness whose row has no gate of its own is capped as the old code capped it (a help at a help's form, anything else at a gift's), so the shipped lists hold even for kinds outside the legacy table. The old code gave any motive it didn't name a gift; with the rows, Remorse, Defend and Curious have no act until their slices add rows. A row with no gate of its own (every shipped row) is ranked and capped by its shipped form, the value a new `FeelingOptions` has, not by a swept one, so a sweep of `GiftCost`, `HelpCost`, `SnubForm` or `ArgueForm` changes only what the gate charges, never which acts are offered or their order, as before the rows; a row with a gate of its own is placed by its own form. The replay adds the catalog's rows and cards as the runner does, and the viewer's Explanation tab defines all 21 `ActOptions` settings (each number names the slice that first reads it).
- **Built in acts-0's second part (`claude/local-acts-0b`):** `StoryMetrics.cs` (7.2 and 7.3) and the baseline on the shipped town and the 0d.6 review's set, in `sim/README.md`. Feuds and friendships come from the variety measures' story events (`Variety.Of`). The engine records each spell a pair spends in a feud (`SimResult.FeudSpells`, written at each night's close; nothing reads it and nothing reaches the log), for the median feud length.
- **Done differently in acts-0's second part:**
  - **A feud's length joins a pair's spells across pauses of 7 days or less.** Regard that sits at −0.3 crosses it back and forth: in 200 seed-years of the shipped town, 611 of the 1,395 spells that ended began again, 510 of them within three days. Spell by spell, the median is 20 days. Joined, there are 7.9 feuds a year (the engine notes 7.7 new feuds) and the median is 38 days. The median is Kaplan-Meier's: a feud still on at the run's end, or one whose last spell ended too close to the end for a 7-day pause to fit, counts as lasting longer than it was seen.
  - **Threads are read from `Act.About` alone.** The gate already sets `About` to its motive's source act, and a consequence's `About` is its scandal, so no second link is needed. A thread is counted in the season its third link lands. Trees rooted in the harness's placed scandal are left out.
  - **Warmth is counted from the acts.** It is the light kind acts aimed at someone, read from the rows' gates, so `SimResult` needs no `Warmth` field. Acts done and undergone are counted from the acts too, so they work with feelings off.
- **Built in acts-1, Returns (`claude/local-acts-1`):** Thanked, Complimented and Joked (4.1-4.3) behind `Returns`; the rules for light kind acts (section 1): they stir no motive in the target (`WarmOnly`, the one hook in `StirFrom`), they settle the kindness they answer, `WarmPerDay` counted apart from `LightPerDay`, the warm budget (`WarmBudgeted`, the one hook in `Feel`), and the cold reading (`ReadWarmAt`, read when the target first feels the act); watch mode, with `SimResult.CatalogWatch`; `ReturnsTests.cs`; a pin for the slice on the shipped town and on the 0d.6 review's set; the runner's `--ao <Name>=<value>` for the catalog's numbers.
- **Done differently in acts-1:**
  - **A catalog row whose slice is off is never offered** (`ActCatalog.SliceOf`, read by `Fits`). The rows are appended whatever the switches say (test 2), so without this the gate would offer a thank-you with `Returns` off.
  - **Complimented and Joked are started by the gate only.** Their per-head draws (0.08 and 0.15 a day) and their cards need the per-head machinery of acts-2, so they come with it.
  - **A warm act read cold has its own route, `Cold`,** in the feeling log (the spec's new `Felt` route). It still counts as undergone, and it stirs no motive, being light; its small hurt moves the target's stance.
  - **Watch mode records each motive once** (Fond, which comes back daily, once a day): a watched motive is never used up, so the gate would record it every tick.
  - **Two guards the spec didn't name:** a light kind act leaves no mark (rule 4's third slight, which read any light act), and an occasion's Fond motive answered with a joke or a compliment doesn't use up the day's occasion gift (0d.6's X12).
  - **Fond waits four weeks after a small act toward someone (`FondWarmDays`, 28).** A small act doesn't reset missing someone (question 2, answer c), so without a wait Fond complimented the same person every day they met. That was 536 compliments a year on the shipped town, most of the trivia budget, and it built ties: E1 82% on 40 seeds. With no wait, the gate's kind acts grew 2.24 times by year 3 of a three-year run (the spec's limit is 1.5; the baseline's 1.40). The sweep, 50 seeds x three years on the shipped town: 7 days gives 1.69, 14 days 1.58 (1.54 with half the warm budget), 28 days 1.46. A gift, which does reset missing, isn't held back.
  - **A light kind act holds no open outcome.** Its own life record and its target's settle as None at once, so a later kindness back settles the earlier gift or help, not the small act.
  - **The rules for light kind acts are Returns' rules.** With Returns off, a town's own light kind row (a wave) is treated as before the catalog.
  - **Watch mode walks the rows as the gate does.** A gift the gate weighed as a close call and declined is passed over, and the small act that would then clear is recorded.
- **Built in acts-2, Company (`claude/local-acts-2`):** PlayedGame and TreatedToDrink (4.4, 4.5) behind `Company`; the per-head draws (3.2, `StartPerHead`, after `StartActs`, which now skips per-head rows); `RateTargetAllowed`'s `Fits` hook; each kind's own rules in `Fits` (`KindFits`: a game's places, hours and ages; a treat's ages and purse); `AfterAct` (a game warms its asker as company does; a drink is paid as `Drinks` pays); Fond's gifts kept to occasions (question 1, answer b); the four per-head kinds' cards (`ActCatalog.Cards`, VERIFY); `CompanyTests.cs`; pins for Company alone and with Returns, on the shipped town and on the review's set.
- **Done differently in acts-2:**
  - **Complimented and Joked are drawn per head only when Company is on as well as Returns,** because Company builds the draws. So acts-1's pins don't move when acts-2 lands; with both on, the Returns rows are per head (0.08 and 0.15 a day) and their cards are added.
  - **PlayedGame is one row with three places.** At the saloon it opens at 17:00, and outdoors (the square, the beach) it closes at 19:00; the row's own hours are 9:00 to midnight, and `KindFits` checks the rest.
  - **"Fond's gifts only on 0d.6's occasions" doesn't need 0d.6's step m.** With Company on, Fond gives a gift only on the other's birthday or a festival (`Calendar`), at most one a day; with m on, 0d.6's own occasion rules apply too. In watch mode the rule holds only for the watch record's weighing.
  - **The per-head draws aren't recorded in watch mode**: watch records the gate's choices, and nothing draws.
- **Built in acts-3, Welcome (`claude/local-acts-3`):** Welcomed (4.6) behind `Welcome`; the Curious motive (`CuriousMotives`, computed each tick next to Fond); the welcome's familiarity both ways and its once-per-ordered-pair rule (`Welcome`, `KindFits`); `WelcomeTests.cs`; three pins.
- **Done differently in acts-3:**
  - **Fond and Curious are "computed" motives** (`Computed`): a close call on either is asked once a day, keyed by the day, as Fond's were.
  - **"Public places" are the places that aren't homes** (`Home:...`).
  - **A welcome is a story kind act, so it counts as kindness received in 0d.6's measures.** On the review's set this ends the newcomer's withdrawal: their stance at day 28 is 0.00 with Welcome on, against -0.56 without (200 seeds). On the shipped town the newcomer is known at 0.355 by day 28 against 0.190, and nothing else moves by more than 10% (feuds -6%).
- **Built in acts-4, Repair (`claude/local-acts-4`):** Apologised (4.7) behind `Repair`; the Remorse motive (`StirRemorse`, from `RemorseAfter` after a heavy hostile act ends and from a joke read cold), lasting `RemorseDays`; the answer on the spot (`AnswerApology`) with the excuse that wears out, the fear and grudge lifted, the stance eased, and the refused apologiser's sting; `ApologyCost` (new); StoryMetrics' jokes read cold, apologies and their accepted share, and remorse that lapsed; `RepairTests.cs`; three pins.
- **Done differently in acts-4:**
  - **The cost of accepting is `ApologyCost` (0.4) + dislike x (0.5 + retention), not 0.2 + ...** At 0.2, 94% of apologies were accepted on 40 seeds (the spec's band is 50-80%): those who argue know each other well, so what the target owes outweighs 0.2 easily. 0.3 gives 84%, 0.4 71%, 0.5 55%.
  - **Watched remorse is recorded, not stirred:** a stirred remorse writes its lines into the log.
  - **Remorse follows any heavy hostile act**, the town's own arguments as well as the gate's.
- **Built in acts-5, Sides (`claude/local-acts-5`):** Mocked, StoodUpFor and Comforted (4.8-4.10) behind `Sides`; witnesses taking sides after a heavy hostile act (`SidesAfter`): Defend (carrying the one defended as `Motive.With`, for `DefendMinutes`) or Pity at a hurt; the one defended's `WithJoy` and Return (`Defended`, route `Defended`); comfort cooling the grudge (`Comforted`); `MotiveAllows` (pity at a hurt only comforts; remorse comforts only within the hour); Mocked's cards (VERIFY); `SidesTests.cs` with the golden thread "the square"; three pins.
- **Done differently in acts-5:**
  - **Mocked needs expression 0.5 or more whatever 0d.6's `ShowOn` says:** the trait is on every card.
  - **Watched sides are recorded, not stirred**, as watched remorse is.
  - **The defended person's `WithJoy` is felt in `AfterAct`,** if they saw the act, rather than in `Feel`: `Feel` has one patient a row.
  - **Pity at a hurt is `HurtPity` (0.5) x understanding of a mishap's pity, and a comfort costs 0.35, not 0.4.** As specified, every argument seen made pity in most onlookers (828 a year on the shipped town), and a comfort, at a gift's cost, was returned with a gift ("in kind or less"): 665 gifts and 20 friendships a year.
  - **Witnesses take sides only at a first strike (`FirstStrikeSides`):** not at a stand-up, nor at an answer in an exchange already going. Without it, the spec's risk 3 happened: a stand-up is itself a heavy hostile act, so onlookers took sides again, and every answer drew defenders of its own. Feuds reached 35 a year (7.9 without). With it, feuds hold, and stand-ups are rare (2-3 a year). On the review's set, friendships are +36% and war towns are 8% over three years: two gates fail there.
- **Built in acts-6, Late (`claude/local-acts-6`):** LateForWork (4.11) behind `Late`; `Arrivals` after the late check; `LateTests.cs`; three pins. The design doc's rules 4, 5 and 10 have their "as built" notes and the tiers table the new kinds (acts-z).
- **The batch gate** (every slice on, one year and three, both bases): `sim/README.md`. Batch 1 without Sides passes every gate on both bases. With Sides, the review's set's friendships rise 39%.
- **Left:** batch 2 (section 9, and the batch 2 spec).

**Sid's words (2026-10-08).** "We also need to expand the amount of actions a person can do."

**Code base.** The hooks were read on `origin/main` at `03c5745`, and line numbers refer to that commit. Phase 0d.6 (`claude/under-glass-0d6`) edits `Simulation.Desire.cs`, `Simulation.Feelings.cs`, `Model.cs` and `DefaultTown.cs`, so these numbers will move when it merges. This work starts after that merge (section 12).

**Evidence.**
- **The act reader's report:** the 17 kinds, their rows, who carries them, and measured counts over 3 seed-years (seeds 1-3).
- **The scaling probe:** `sim/UnderGlass.Scale/` in a workflow worktree, not in the repo. Act rates are town-wide, so acts per person fall as 1/n.
- **Design A:** this spec takes its record classes, its guard rails and its measures.
- **Design B:** this spec takes its engine seams, its new motives, its thread measure and its order of building.

**Status of the numbers.**
- Every joy, plastic share, cost, rate and juiciness below is a first guess for the sweeps of section 7.
- Who does what ("who") comes from memory of Stardew Valley, not from the game's files, and is marked VERIFY.
- No Spinoza proposition is cited for a new rule here. Where a proposal cited one from memory, it is left out until checked.
- **A year in the simulator is 112 days**: 4 seasons of 28 days, which is 16 weeks. The figures "about 5 news a week" (design 12.7) and "trivia 1.4 a day" (`sim/README.md`) divided a year's count by 52 weeks and 365 days. By the simulator's own calendar, today's town makes about 265 news a year, which is 2.4 a day or about 16.6 a week. It makes about 700 trivia a year, which is about 6.2 a day. This spec states its budgets per simulator year (112 days) and per simulator day. Question 9 asks Sid about it.

---

## 0. What this builds, and why

Today's town has 17 act kinds. In a year, everyday life is mostly gifts (544), arguments (198) and stumbles (177). Four gaps follow.

1. **Most motives have one act.** Return, MakeUp and Fond are all answered with `GaveGift` (`Simulation.Desire.cs:290-313`).
   - The shy can't clear a gift's cost of 0.4, so their kindness goes unanswered and is recorded as Ignored.
   - Fond's only answer is a gift. That is how Sid's "close to love-bombing" came about (design 12.7).
2. **There is no repair.** Rule 5 names "an accepted apology" as one way regard heals, but it isn't built. MakeUp is answered with a gift.
3. **Nobody takes sides.** A witness feels an argument (laws 5 and 6), but only the person argued with can act on it. A feud never gains a second side.
4. **Rates fall as the town grows.** `ActKind.PerDay` is a town-wide rate, so in a town of 104 each person does a quarter of the rate-drawn acts they would do in a town of 26.

**Batch 1** adds 11 kinds and 3 motives:
- **Small returns and company:** Thanked, Complimented, Joked, PlayedGame, TreatedToDrink.
- **Meeting someone new:** Welcomed, with the new motive Curious.
- **Repair:** Apologised, with the new motive Remorse and an answer given on the spot.
- **Sides and comfort:** Mocked, StoodUpFor (new motive Defend), and Comforted (Pity, widened).
- **A break in the routine:** LateForWork, built from the late check that already exists.

**After batch 1:**
- Batch 2 (section 9) goes with the town-layout work: visits, invitations and dates, the shop counter, scenes and bans.
- The rest is a backlog (section 10).

**Everything is opt-in.**
- The new rows are appended to the act list only when the catalog is on.
- Each slice has its own switch.
- With every switch off, all 9 pinned hashes hold (section 6).

### How kinds were chosen for batch 1

A kind made batch 1 only if all of these held:
- **It answers something or invites something.** Then it can belong to a thread that a viewer can follow through `About` links (principle 5).
- **Someone can see it.** No kind in batch 1 happens behind anyone's back. That needs Sid's answer on hearsay (question 5).
- **Character shows in it.** The bold mock and stand up for people. The shy thank and compliment. The understanding apologise and comfort.
- **It plugs into machinery that exists.** A row brings witnessing, gossip, feelings, outcomes and the life record with it. Batch 1 needs no movement goals, no promises and no new kind of record.
- **It needs no written line.** The viewer's sentence is a template.
- **It stays within the tone limits.** Batch 1 has no new crime, no lies and no violence.

---

## 1. Three classes of act

This is design A's split. Batch 1 uses Story and Light; Ledger comes with batch 2's counter.

| Class | Is it an `Act`? | Felt | Retold | Stirs motives? | Outcome in the life record |
|---|---|---|---|---|---|
| **Story** | yes, as today | by clarity, as today | by juiciness | yes, as today | the full life record |
| **Light** | yes; `Gate.Light` is set | by clarity, with a daily cap on regard per pair | never (juiciness under 1.5) | no, but it can settle an open outcome | `None` after the window, never Ignored |
| **Ledger** (batch 2) | no: a tally per (person, doing, place, other) over 4 weeks | mood only | never | no | none |

### Light kind acts

Today's light acts, `Snubbed` and `TurnedAway`, are hostile. Batch 1 adds light acts that are kind. They follow these rules:
- **They stir no motive in the target.** `StirFrom` returns before its Return branch for them. A thank-you is never thanked back, and a compliment does not oblige a gift.
- **They still settle the target's open kindness.** `BeganAct` already marks a kind act that follows a kind act as `Returned` (`Simulation.Desire.cs:546-575`).
- **Each actor may do at most `WarmPerDay` (3) a day.** This cap is counted apart from `LightPerDay`, the cap on light hostile acts.
- **Each ordered pair has a regard budget of `WarmBudget` (0.01) a day** across all light kinds together.
  - The budget is needed because the repeat halving is kept per kind (`Simulation.Feelings.cs:220-229`). Without it, a mix of different small kindnesses would pass undiscounted.
  - Regard drifts back toward the seed by 0.005 a day. Against that, daily small kindness can keep a warm tie warm but cannot build one; ties are still built by Story acts.
- **0d.6's kindness counts leave them out for now.** `IsKindAimed` leaves out light kind acts. So they don't count in 0d.6's `TallyKindIn` and don't reset Fond's last-kindness day (`_lastKindDay`), until Sid decides otherwise (question 2). They are counted apart, as "warmth received".

---

## 2. Engine seams (slice acts-0: no change in behaviour)

Every field and enum value below is appended at the end, with a default that reproduces today.

### 2.1 Records (`Model.cs`, and a new file `Acts.cs`)

```csharp
// Acts.cs (new)
/// <summary>The gate's data for a kind (rule 10): its form cost and minimum intensity, the motives
/// it may answer, and its limits. Null on every shipped row: the legacy table applies.</summary>
public sealed record ActGate(double Form, double Min, IReadOnlyList<DesireKind> Serves,
    bool Light = false, bool NeedsCard = false, double PrideWeight = 0,
    double MinFamiliarity = 0, double MinRegard = -1, int MinAudience = 0);

// Model.cs: appended at the end of each record or enum
public sealed record Affect(..., double ReadWarmAt = double.NegativeInfinity, double WithJoy = 0);
public sealed record ActKind(..., ActGate? Gate = null, bool PerHead = false,
    int FromMinute = 0, int ToMinute = 1440);   // 1440: a day's minutes
public sealed record Act(..., string? With = null);
public enum DesireKind { Answer, Return, MakeUp, Retaliate, Fond, Pity, Remorse, Defend, Curious }
public enum Outcome { Open, Answered, Returned, Rebuffed, Avoided, Ignored, None, Accepted, Refused }
```

What the new fields mean:
- **`ReadWarmAt`:** a warm act is read cold when the patient's regard for the actor is below this value. Read cold, its joy becomes −`ColdShare` × |joy| (0.5): it is taken badly. Laya's role 2 (design 5a) may replace this rule later.
- **`WithJoy`:** what the act's `With` person feels, caused by the actor. This is the feeling of the person someone stood up for.
- **`PerHead`:** `PerDay` is a rate for each eligible card-carrier, not for the whole town (section 3.2).
- **`FromMinute` and `ToMinute`:** the hours in which the act can start. `ToMinute` may pass midnight, as a haunt's window can.
- **`Act.With`:** a third person. Only StoodUpFor uses it in batch 1.
- **`Motive`** (the private class at `Simulation.Desire.cs:19`) gains a `With` field, so a Defend motive can carry the person being defended into `Begin`.

### 2.2 The gate reads its data from the rows (`Simulation.Desire.cs`)

`GateOf(kind)` returns `kind.Gate`. For a shipped row, which has no `Gate`, it builds the gate from today's `FeelingOptions`:

| Shipped kind | Form / Min | Serves |
|---|---|---|
| Argued | `ArgueForm` / `ArgueMin`, plus `HostileSurcharge` | Answer, Retaliate |
| Snubbed | `SnubForm` / `SnubMin`, plus the surcharge; light | Answer, Retaliate, only while `LightActsOn` |
| TurnedAway | none; light | nothing (only `TurnAway` starts it) |
| HelpedSomeone | `HelpCost` / `HelpMin` | Return, Pity |
| GaveGift | `GiftCost` / `GiftMin` | Return, MakeUp, Fond |

Then:
- **`Form` and `Min`** (`:279-287`) read `GateOf`. Today an unknown name silently gets the argument's cost and minimum. After this change, a kind with no gate data serves no motive.
- **`ActsFor`** (`:290-313`) lists every kind whose gate serves the motive, most expensive first, with ties broken by name. It filters as today (age, place, the hostile cooldown, the light cap), and also by `Fits` (section 3.1).
- **Return in kind or less** (from design B). For Return, `ActsFor` offers only kinds whose form is at most the form of the kindness being returned.
  - On the shipped rows this gives today's lists exactly: returning help offers help, then a gift; returning a gift offers a gift.
  - With the catalog on, it stops a compliment from being returned with a gift.
- **`IsLight`** (`:63`) becomes `GateOf(k).Light`.
- **`IsKindAimed`** (`:68`) leaves out light kind acts.
- **Pride.** For a kind with a `PrideWeight`, the cost rises by `PrideWeight` × (self-regard − 0.5). This is one line in `Weigh`'s loop (`:402`).

**Test.** For every motive, source act and switch setting of the shipped town, the list built from the rows equals today's list.

### 2.3 Options

`FeelingOptions` gains one property at its end: `public ActOptions Acts { get; set; } = new();`. `ActOptions` lives in `Acts.cs`.

| Switch | Slice | What it turns on |
|---|---|---|
| `Watch` | all | every rule below works out what it would do and records it in `CatalogWatch`, but no act starts |
| `Returns` | acts-1 | Thanked, Complimented and Joked; the rules for light kind acts; return in kind or less for the new rows |
| `Company` | acts-2 | PlayedGame and TreatedToDrink; the per-head draws; Fond's list of company acts |
| `Welcome` | acts-3 | Welcomed and the Curious motive |
| `Repair` | acts-4 | Apologised, the Remorse motive and the apology's answer |
| `Sides` | acts-5 | Mocked, StoodUpFor and Comforted; the Defend motive; Pity from hurts seen |
| `Late` | acts-6 | LateForWork |

- Every switch is off by default, and `DefaultTown.Feelings()` turns none on.
- Every switch except `Late` acts only while the gate acts (`Acting`).
- LateForWork can happen with feelings off. It then changes the log, so `Late` is off in every pinned run.

**Constants** (first guesses):

| Constant | Default | Used by |
|---|---|---|
| `WarmPerDay` | 3 | light kind acts per actor per day |
| `WarmBudget` | 0.01 | regard per ordered pair per day from light kinds |
| `ColdShare` | 0.5 | how much of a warm act's joy turns negative when read cold |
| `NewAt` | 0.15 | familiarity below which someone is new (Curious) |
| `WelcomeFamiliarity` | 0.1 | familiarity gained both ways by Welcomed |
| `CuriousBase` | 0.25 | Curious intensity, times (0.5 + chattiness) |
| `RemorseAt` | 0 | the lowest regard for the target at which Remorse is felt |
| `RemorseShare` | 0.5 | share of the target's hurt the actor feels as Remorse |
| `RemorseDays` | 14 | how long Remorse lasts |
| `DefendAt` | 0.4 | the lowest regard for the victim at which a witness wants to defend them |
| `DefendMinutes` | 60 | how long Defend lasts |
| `ApologyExcuse` | 0.5, 0.25, 0 | regard given back by the 1st, 2nd and 3rd accepted apology for the same kind of act within `ApologyDays` |
| `ApologyDays` | 28 | the window for counting apologies |
| `RefusedSting` | 0.08 | mood lost by an apologiser who is refused |
| `ComfortCools` | 0.25 | share by which comfort cools the target's open Answer |

### 2.4 The catalog and the cards

- **`ActCatalog.Batch1(ActOptions o)`** returns the rows of the slices that are on.
- **`ActCatalog.Kinds(o)`** is `DefaultTown.Acts()` with those rows after it. Every shipped kind keeps its index, which matters for `StartActs`' order and the replay's kind indices (`Replay.cs:59`).
- **`ActCatalog.Cards(cast, o)`** returns the cast with card weights added for the per-head kinds and for the kinds that need a card.
  - It changes only each villager's `Acts` dictionary, using `with`.
  - It never touches the tail of `Cast()`, which 0d.6 edits.
- **The runner** gets `--catalog <slices>`, for example `--catalog returns,company`. The name `--acts` is already taken: 0d.6 uses it for cards.
- **`ReplayOptions`** gets `Kinds` and `Cast`; null means the default town. The population and layout work needs the same option for a generated town, so build it once for both.

### 2.5 New files

| File | Holds |
|---|---|
| `Acts.cs` | `ActGate`, `ActOptions` |
| `ActCatalog.cs` | the rows of section 4, and the cards |
| `Simulation.Acts.cs` | `Fits`; the per-head draws; `Arrivals`; `AfterAct`; the three new stirrings; the apology's answer; the warm budget; the watch record |
| `StoryMetrics.cs` | the measures of section 7 that don't exist yet |
| `UnderGlass.Sim.Tests/CatalogTests.cs`, `StoryMetricsTests.cs` | the tests of section 6 |

---

## 3. Hooks in existing files

Each hook is a line or two that calls into `Simulation.Acts.cs`, so it should merge cleanly once 0d.6 is in.

| File and place (main at 03c5745) | Hook |
|---|---|
| `Simulation.cs`, `Step` (`:448-480`) | `StartPerHead(m)` after `StartActs(m)`; `Arrivals(m)` after `CheckLate(m, t)` |
| `Simulation.cs`, `Begin` (`:873-901`) | an optional `with` parameter, stored on the `Act` |
| `Simulation.cs`, `FinishActs` (`:940-996`) | `AfterAct(act, kind, m)` next to `StirPity` (`:993`) |
| `Simulation.Desire.cs`, `StirFrom` (`:144-201`) | return early for a light kind act, after calling `Hurt` if it was read cold |
| `Simulation.Desire.cs`, `Pursue` (`:350`) | `motives.AddRange(CuriousMotives(h, p, m))` next to `FondMotives` |
| `Simulation.Desire.cs`, `ActsFor`, `Form`, `Min`, `IsLight`, `IsKindAimed` | as in section 2.2 |
| `Simulation.Desire.cs`, `Weigh`'s loop (`:402`) | `+ Pride(k, h)` added to the cost |
| `Simulation.Desire.cs`, `BeganAct` (`:546`) | the warm counter, next to the light one |
| `Simulation.Desire.cs`, `RateTargetAllowed` (`:131-138`) | `&& Fits(kind, a, o, m)`, which the per-head draws use through `Candidates` |
| `Simulation.Feelings.cs`, `Feel` (`:197-283`) | the cold reading (`ReadWarmAt`); the warm budget before `Move` |
| `Feelings.cs`, `FeelingOptions` | `Acts`, as its last property |

`Simulation.Money.cs` needs no edit. To pay for a drink bought for someone, `Simulation.Acts.cs` does what `Drinks` does (`:211-227`): it uses `_drank`, `Move` and `ToOutside`.

### 3.1 `Fits(kind, actor, target, m)`

This one filter serves both the gate and the per-head draws. It checks:
- the hours (`FromMinute` to `ToMinute`);
- the card, for `NeedsCard` kinds and for per-head draws;
- `MinFamiliarity` (the actor's familiarity with the target) and `MinRegard` (the actor's regard for the target);
- `MinAudience`: others awake in the same place, within 8 tiles of the actor and in sight, not counting the target;
- the rules each kind adds in section 4: Welcomed once per pair, Apologised once per act, TreatedToDrink's purse, PlayedGame's ages.

### 3.2 Per-head draws

`StartPerHead(m)` runs each tick, after `StartActs`.
- It takes the per-head kinds in catalog order.
- For each kind it takes people in name order. A person can draw if they are free, carry the kind (weight w > 0), fit its age, place and hours, and have a target (`HasTarget`).
- Each such person draws `Rng.Unit(seed, "perhead", kind, name, m) < w × PerDay / 288`. There are 288 five-minute ticks in a day.
- The target is chosen as today (`PickTarget`, weighted by regard).
- Someone who starts an act is busy, so later draws in the same tick find them not free.

These draws are keyed apart, so they move no other draw. They also stay the same per person as the town grows. The probe found that town-wide rates do the opposite: 0.197 rate-drawn acts per person a day at 26 people, and 0.052 at 104.

---

## 4. Batch 1: the acts

How to read each entry:
- **Row:** tier, base juiciness (J) and valence (V); read time and duration in minutes; where, when and at what age.
- **Affect:** the patient, joy, plastic share, freedom, how the target is set, and tilt.
- **Gate:** form cost / minimum intensity. "+h" means the hostile surcharge (0.3) plus fear.

Retelling follows from the volunteer level of 2 and the bonus of 0.5 for a listener who knows the person (`Simulation.cs:13-14`):
- J under 1.5: never retold.
- J from 1.5 to 1.9: told only on day 0, and only to listeners who know the person.
- J 2 or more: news.

### 4.0 At a glance

| # | Kind | Class | Tier, J, V | Read/dur | Started by | Serves | Slice |
|---|---|---|---|---|---|---|---|
| 1 | Thanked | Light | Trivia 0.5 +1 | 1/1 | gate | Return | returns |
| 2 | Complimented | Light | Trivia 1.0 +1 | 1/2 | gate; per head 0.08 | Return, MakeUp, Fond, Remorse | returns |
| 3 | Joked | Light | Trivia 0.5 +1 | 1/2 | per head 0.15; gate | Fond | returns |
| 4 | PlayedGame | Light | Trivia 1.0 +1 | 2/30 | per head 0.1; gate | Fond | company |
| 5 | TreatedToDrink | Story | Trivia 1.5 +1 | 1/2 | per head 0.08; gate | Return, MakeUp, Fond | company |
| 6 | Welcomed | Story | Trivia 1.5 +1 | 1/5 | gate | Curious | welcome |
| 7 | Apologised | Story | Trivia 1.5 +1 | 2/5 | gate | Remorse | repair |
| 8 | Comforted | Story | Trivia 1.5 +1 | 2/10 | gate | Pity, Remorse | sides |
| 9 | Mocked | Story | News 2.5 −1 | 1/3 | gate | Answer, Retaliate | sides |
| 10 | StoodUpFor | Story | News 2.5 −1 | 2/5 | gate | Defend | sides |
| 11 | LateForWork | Story | Trivia 1.5 −1 | 1/1 | derived | none | late |

Two kinds are news, and both are hostile and public. Every other kind is trivia. None is a scandal.

### 4.1 Thanked: "Haley thanked Evelyn for the shell."

- **Row.** Trivia 0.5, +1; read 1, lasts 1. Anywhere, any age.
- **Affect.** Target +0.08, plastic 0.1, freedom 1, Chosen.
- **Gate.** 0.2 / 0.02: rule 10's wave. Light. Serves Return.
- **Needs.** An open Return motive toward the target, which means a kindness from them in the last 7 days. The target must be in reach.
- **Who.** Anyone. In practice it is the shy who thank. The gate tries the most expensive act first, so anyone who can clear a gift still gives a gift.
- **Causes.**
  - The target's mood lifts a little.
  - `BeganAct` marks the kindness as Returned on both people's life records.
  - It is never retold, and as a light act it is never thanked back.
- **Plugs in.** The row and the gate table, and nothing else.
- **Story.** It closes loops that the life record calls Ignored today. Measure: the Ignored share of kindnesses falls.

### 4.2 Complimented: "Leah told Haley her photos were lovely."

- **Row.** Trivia 1.0, +1; 1/2. Anywhere; age 5 and up.
- **Affect.** Target +0.12, plastic 0.15, Chosen, tilt +1 (the glad do it more). It is read cold when the target's regard for the actor is below −0.2: from someone disliked, it reads as flattery, at −0.06.
- **Gate.** 0.3 / 0.05: rule 10's chat. Light. Serves Return, MakeUp, Fond and Remorse. Needs familiarity of 0.2 or more.
- **Per head.** 0.08 a day for carriers.
- **Who (cards, VERIFY).** Emily, Leah, Penny, Evelyn, Harvey, Robin, Caroline, Jodi, Gus and Lewis carry it at 1. Through the gate, anyone may use it.
- **Causes.** Mood and a small change in regard. It settles an open kindness. It is never retold.
- **Story.**
  - It is the shy's way to answer a kindness and to answer missing someone.
  - It is also the kind of act that the town's norms can later be read from (law 7, and rule 7's reactions).

### 4.3 Joked: "Sam teased Penny about her lesson plans."

- **Row.** Trivia 0.5, +1; 1/2. Anywhere; age 7 and up.
- **Affect.** Target +0.08, plastic 0.1, Chosen, tilt +1. It is read cold when the target's regard for the joker is below 0.1: the tease is taken badly, at −0.04.
- **Gate.** 0.2 / 0.05. Light. Serves Fond. The joker needs regard of 0.3 or more for the target and familiarity of 0.5 or more.
- **Per head.** 0.15 a day.
- **Who (VERIFY).** At 1: Sam, Sebastian, Abigail, Alex, Robin, Gus, Marnie, Vincent and Jas. At 0.5: Haley, Shane and Pierre.
- **Causes.**
  - Warmth, or a small hurt when it is read cold.
  - The hurt moves the target's stance (`Hurt`) but stirs no motive, because the act is light.
  - With `Repair` on, a joke read cold stirs Remorse in the joker. They took part, so they saw it land.
- **Story.** The "only joking" thread: a tease read cold, then Remorse, then Apologised, then accepted or refused.

### 4.4 PlayedGame: "Sebastian and Sam played pool."

- **Row.** Trivia 1.0, +1; read 2, lasts 30. Age 5 and up. The target takes part, as in an argument, so both people are busy for 30 minutes. It can happen:
  - at the saloon, 17:00 to 24:00: pool, darts or the arcade (VERIFY that the saloon has a pool table and arcade machines);
  - in the Square or on the Beach, 9:00 to 19:00: children's games and catch.
- **Affect.** Target +0.08, plastic 0.1, Chosen. The actor gets the same mood as company (`AddMood`), with no change in regard. A row has only one patient; a `Mutual` field is in the backlog.
- **Gate.** 0.3 / 0.1. Light. Serves Fond. Needs familiarity of 0.2 or more, and the two must be within 6 years of age of each other or both under 13.
- **Per head.** 0.1 a day.
- **Who (VERIFY).** At 1: Sam, Sebastian, Abigail, Alex, Jas and Vincent. At 0.3: Emily, Shane, Haley and Maru.
- **Causes.** Half an hour spent standing together. That time counts toward chats, familiarity and 0d.6's days together. A standing game every week would be a promise, which is batch 2.
- **Story.** Friendships among the young across three households. Later come bets, and CheatedAtGame (backlog).

### 4.5 TreatedToDrink: "Shane bought Emily a drink."

- **Row.** Trivia 1.5, +1; 1/2. At the saloon, 18:00 to 01:00. Both people 18 or older.
- **Affect.** Target +0.15, plastic 0.25, Chosen, tilt +1.
- **Gate.** 0.3 / 0.1. Serves Return, MakeUp and Fond.
- **Per head.** 0.08 an evening, for carriers at the saloon.
- **Money.** The actor's household pays one `SaloonDrink` (12g) to the bar's household, which restocks as in `Drinks`.
  - If the target hasn't had their drink today, this is it, and `Drinks` won't charge their household again.
  - If they have, it is a second drink, paid the same way.
  - If Gus is the one treating, the drink is on the house and no money moves.
  - All of this stays inside the town, so the money test still holds to the gram.
- **Needs.** The actor's household purse holds at least one drink. It does not check whether the household can afford it: Pam's rounds draining a trailer that is already short is a story.
- **Who (VERIFY).** Gus, Pam, Shane, Pierre, Lewis, Marnie, Demetrius and Leah, at 1.
- **Causes.** It works like a gift, plus the money. It stirs Return in the target. With return in kind or less, the answer is a round back, a compliment or a thank-you.
- **Story.** It ties the bar to money and to company. It is told on day 0 to people who know them, so who buys whom a drink can seed courting stories (11a) without a courting rule.

### 4.6 Welcomed: "Robin showed the newcomer round the square."

- **Row.** Trivia 1.5, +1; 1/5. Public places; any age.
- **Affect.** Target +0.15, plastic 0.25, Chosen.
- **Gate.** 0.3 / 0.1. Serves Curious. At most once per ordered pair, ever.
- **The Curious motive (new).**
  - It is computed each tick, as Fond is, and not stored.
  - A free holder feels it toward everyone in reach whom they know less than `NewAt` (familiarity 0.15) and haven't yet welcomed.
  - Its intensity is `CuriousBase` × (0.5 + chattiness).
  - It is never felt between kin or housemates.
- **Causes.** Familiarity rises by 0.1 both ways. The act stirs Return in the target: a thank-you or a compliment back.
- **Who.** Anyone; the chatty and the bold first.
  - In the shipped town only the newcomer starts below 0.15. Strangers seed at 0.25 (`Simulation.cs:318`) and the newcomer at 0 (`:312`).
  - So in this town, Welcomed is the newcomer's arc: law 13's curiosity at what is new.
  - VERIFY: Lewis greets the farmer on day 1 in the game.
- **Why it matters for a bigger town.** It is keyed on familiarity, not on the name "Newcomer". Suppose a generated town seeds familiarity by neighbourhood (the layout work). Then Welcomed is how people from different neighbourhoods meet, and how anyone who moves in later (0e) is received.

### 4.7 Apologised: "Sam said sorry to Penny for the joke."

- **Row.** Trivia 1.5, +1; read 2, lasts 5. Anywhere; age 5 and up. `About` is the hurt being apologised for.
- **Affect.** Target +0.1, plastic 0.2, Chosen.
- **Gate.** 0.5 / 0.15: a walk-up. Pride raises the cost by 0.5 × (self-regard − 0.5). Serves Remorse. At most one apology per `About` act.
- **The Remorse motive (new).** It is stirred in the actor of a heavy hostile act (Argued, Mocked, StoodUpFor), or of a joke read cold, when the act ends. All of these must hold:
  - the actor saw the target take it (they took part, so they always did);
  - the actor holds the target at `RemorseAt` (0) or above;
  - the act was not over the target's own scandal (the "gave cause" case, where the actor was in the right);
  - the two are not kin or housemates, because families cover.

  How much is felt: the target's felt amount × `RemorseShare` (0.5) × (0.5 + understanding). It fades over `RemorseDays` (14). Remorse is a kind motive, so the existing guard applies: nobody apologises to someone they now dislike (`Weigh`, `:383`).
- **The answer.** When the act ends, the target answers on the spot. This is design B's "Ask" in its smallest form.
  - Obliged = 0.5 × max(0, regard for the apologiser) + 0.5 × familiarity + 0.3 × understanding.
  - Cost = 0.2 + dislike × (0.5 + retention), where dislike is max(0, −regard).
  - If the margin (Obliged − Cost) is more than 0.15 either way, the margin decides.
  - Otherwise a seeded draw decides, keyed ("apology", act id), with P = logistic(8 × margin), tilted by mood.
- **If accepted.** Law 10: a cause is supplied ("he didn't mean it").
  - The target gets back part of the regard the `About` act cost them, read from `_felt`. The share is 0.5 for the first accepted apology from this person for this kind of act within 28 days, 0.25 for the second, and nothing after that ("sorry means less").
  - The `About` act's fear hit is removed from `_hits`.
  - The target's open Answer toward the apologiser, stirred by that act, is settled.
  - The target's stance eases, as after a kindness.
  - The apology's life event is marked Accepted.
- **If refused.**
  - The apologiser's mood falls by `RefusedSting` × (0.5 + sensitivity).
  - The hurt stays.
  - The life event is marked Refused.
  - The Remorse for that act is settled: one apology per act.
- **Who.** Anyone; more likely with high understanding and modest self-regard. Likely: Penny, Harvey, Leah, Maru, Robin, Sam and Emily. Rarely: George, Shane and Haley (VERIFY). It comes from traits, not cards.
- **Story.** Rule 5's "accepted apology": the first way out of a hurt other than waiting.
- **Guard for E1.** An apology needs a fresh act, from the last 14 days, and an apologiser who doesn't dislike the target. A pair already in a feud (−0.3 both ways) is past that point. So apologies should mend fresh hurts between people who get on, and leave feuds alone. Section 7 measures whether they do.

### 4.8 Comforted: "Emily sat with Haley after the square."

- **Row.** Trivia 1.5, +1; read 2, lasts 10. Anywhere; age 7 and up.
- **Affect.** Target +0.2, plastic 0.25, Chosen, tilt +1.
- **Gate.** 0.4 / 0.15: rule 10's gift. Serves Pity and Remorse. For Remorse, the target must have been hurt within the last hour.
- **Pity, widened.**
  - Today Pity comes only from a mishap with nobody to blame, and only while `PityOn` is set, which it isn't in the town.
  - With `Sides` on, a witness who saw someone be the target of a heavy hostile act (an argument, or mocking) also feels Pity, if:
    - they hold that person at 0 or above;
    - they are not that person's kin or housemate. Families cover, so this never happens at home.
  - How much is felt: `DesireMath.Pity`, as today (`Simulation.Desire.cs:258-275`). It lasts `PityMinutes`.
  - Pity from a hurt is answered only with Comforted. Pity from a mishap keeps HelpedSomeone, under `PityOn` as now.
- **Causes.**
  - The target's mood rises.
  - Their stance is pulled toward 0 (`StanceAfterKindness`).
  - Their open Answer toward whoever hurt them cools by `ComfortCools`, a quarter.
  - It stirs Return.
  - With 0d.6's `ShowOn`, a witness feels Pity only for the part of the hurt that shows (Expression), so maskers like Penny are comforted less.
- **Who.** People high in sensitivity and understanding: likely Emily, Penny, Evelyn, Harvey, Marnie, Leah, Jodi, Caroline, Maru and Jas. It comes from traits (VERIFY).
- **Story.** A bad day becomes the start of a friendship. It is the friendship lever that 0d's Pity was meant to be, aimed at hurts instead of stumbles.

### 4.9 Mocked: "Haley laughed at Penny in front of the market."

- **Row.** News 2.5, −1; read 1, lasts 3. Public places, not homes; age 10 and up.
- **Affect.** Target −0.3, plastic 0.35, freedom 1, Chosen. Onlookers feel it through the bystander rules (laws 5 and 6), as for an argument.
- **Gate.** 0.55 / 0.25 +h. Serves Answer and Retaliate. It needs:
  - a card (`NeedsCard`);
  - an audience of 2 or more (`MinAudience`);
  - with 0d.6's expression trait, Expression of 0.5 or more.
- **Why a cost of 0.55.**
  - It costs a little more than an argument (0.5). The gate tries the dearest act first, so it picks mocking only when someone bold enough has a crowd in front of them. Otherwise the same motive becomes an argument, or nothing.
  - So **Mocked replaces arguments; it does not add hostility.**
  - It shares the 3-day hostile cooldown per pair and the fear hit. `IsHeavyHostile` already covers it.
- **Who (cards, VERIFY).** At 1: Haley, Alex, Abigail, Shane, George and Pam. At 0.5: Sam, Sebastian and Pierre. There is no rate: mocking is only ever an answer.
- **Causes.**
  - In the target, through the existing `StirFrom`, all as for an argument:
    - Answer, or MakeUp if they still love the mocker;
    - a hurt to their stance, which pushes the shy toward withdrawal, a road to the hermit;
    - a fear hit.
  - In onlookers:
    - Defend in those who love the target;
    - Pity in the kind;
    - those who like the target cool on the mocker.
  - It is news, and most people tell it.
- **Story.** One act opens three threads: the target's answer, someone who defends them, and someone who comforts them. This is design B's thread 2, "the square".

### 4.10 StoodUpFor: "Marnie told Pierre to leave Shane alone."

- **Row.** News 2.5, −1; read 2, lasts 5. Anywhere; age 7 and up (children defend their siblings). `Target` is the aggressor, and `With` is the person defended.
- **Affect.** Aggressor −0.2, plastic 0.3, Chosen. If the defended person saw it, they feel `WithJoy` +0.2 toward the defender, at plastic 0.3.
- **Gate.** 0.5 / 0.2 +h. Serves Defend. It is heavy hostile, so the cooldown and the fear hit apply.
- **The Defend motive (new).**
  - It is stirred in a witness when a heavy hostile act ends against someone they love (regard of `DefendAt`, 0.4, or more) or against their kin.
  - The witness must have seen it at clarity 0.3 or more, and named both people.
  - How much is felt: |joy| × clarity × (regard, or 0.5 for kin) × (0.5 + sensitivity).
  - It lasts `DefendMinutes` (60): you stand up for someone then or not at all.
  - The person defended may be kin; that is the point, since families cover by defending. The aggressor is never the defender's kin or housemate.
- **Causes.**
  - In the aggressor: Answer toward the defender. The feud gains a side.
  - In the person defended: the `WithJoy` feeling, and Return.
  - Onlookers take sides by laws 5 and 6.
  - If the defender likes the aggressor, Remorse.
- **Who.** From traits: the bold who love someone. Likely Marnie, Jodi, Kent, Sebastian, Alex, Emily, Gus and Lewis (VERIFY).
- **Story.** Sides and factions. This is new hostile volume, so it has a budget (section 7.2).

### 4.11 LateForWork: "Shane came in late again."

- **Row.** Trivia 1.5, −1; read 1, lasts 1. At the person's job place. Anyone with a job; the children's lessons count.
- **Affect.** Actor −0.05, plastic 0, freedom 0.3, no target. A freedom above 0 keeps it out of Pity: a mishap has freedom 0.
- **Started.** It is derived, like `OutLate`.
  - `CheckLate` (`Simulation.cs:581-592`) already marks the person late and writes a `late` log line.
  - `Arrivals(m)` starts the act when that person reaches their job place that day.
  - Whoever is there sees them come in.
- **Causes.**
  - Embarrassment.
  - It is told on day 0 to people who know the person.
  - It feeds habits (`Simulation.Habits.cs`), so the town can come to know someone as "often late".
- **Who.** Night owls who sleep through their alarm. Design A counts about two late arrivals a season today.
- **Story.** Rule 1's "acting normal": a break in someone's routine, seen by others. It is the cheapest such act, and the pattern for batch 2's StoppedComing and FoundItClosed.

### 4.12 What each motive can do in batch 1

| Motive | Acts, dearest first (form) | New acts |
|---|---|---|
| Answer, Retaliate | Mocked (0.55+h; needs an audience and a card), Argued (0.5+h), Snubbed (0.2+h; only with light acts on) | Mocked |
| Return | in kind or less: HelpedSomeone (0.5), GaveGift (0.4), TreatedToDrink (0.3), Complimented (0.3), Thanked (0.2) | 3 |
| MakeUp | GaveGift (0.4), TreatedToDrink (0.3), Complimented (0.3) | 2 |
| Fond | GaveGift (0.4; with `Company` on, only on 0d.6's occasions, question 1), PlayedGame (0.3), TreatedToDrink (0.3), Complimented (0.3), Joked (0.2) | 4 |
| Pity | HelpedSomeone (0.5; mishaps, under `PityOn`), Comforted (0.4; hurts, under `Sides`) | 1 |
| Remorse (new) | Apologised (0.5 plus pride), Comforted (0.4; only if the target was hurt in the last hour), Complimented (0.3) | all |
| Defend (new) | StoodUpFor (0.5+h) | all |
| Curious (new) | Welcomed (0.3) | all |

These rules don't change:
- one act a tick per person;
- two attempt slots per pair a day;
- love covers;
- families cover;
- the close-call band;
- a question is asked again only when its motive moves by 0.1.

---

## 5. The life record, the replay and the viewer

- **The life record.**
  - `BeganAct` writes the new kinds into it.
  - Apologised resolves as Accepted or Refused, two new `Outcome` values appended to the enum.
  - Light kinds resolve as None.
- **New `Felt` routes:**
  - Apology: the regard given back;
  - Defended: the `WithJoy` feeling;
  - Cold: a warm act read cold.
- **`SimResult`** gets two new fields, appended and `required`. Both are empty when the catalog is off:
  - `CatalogWatch`: the acts that watch mode would have started;
  - `Warmth`: light kindness received, per person per day.
- **The replay.** It records the new kinds through `ReplayOptions.Kinds`. The format doesn't change.
- **The viewer** (`sim/viewer/index.html`) gets:
  - a `VERB` sentence for each new kind (the quoted lines in section 4);
  - `MOTIVE` sentences for Remorse, Defend and Curious;
  - the two new outcome names;
  - a "thread" view that follows `About` links and motive sources from any act.

  The Population tab can show each person's acts done and undergone, by kind, for the current season.

---

## 6. Opt-in, pins and tests

**The shipped town** has no catalog rows and no switch on. All 9 pinned hashes in `PinnedTests.cs` hold, and so do 0d.6's own pins once it merges.

**Tests** (`CatalogTests.cs`):
1. The gate built from the rows equals today's mapping, for every motive, source act and shipped switch setting.
2. With every batch-1 row appended and every switch off, the run gives the shipped hash (P3, `e9fd83b284f5c1b6`) and the feelings-off hashes. This works for two reasons:
   - no row is a scandal, so `Harness.ScandalFor` places the same scandal as before;
   - per-head draws are made only when their switch is on.
3. With watch on and every slice on, the run still gives the shipped hash, and `CatalogWatch` is not empty.
4. One scene test per kind: a small town with people placed by hand, checking the kind's preconditions and what it causes.
5. Determinism: two runs give one hash, in any culture.
6. With `Company` on, money is conserved to the gram.
7. Golden threads (design section 8), written before their slice's rules:
   - **"Only joking":** Joked, read cold, then Remorse, then Apologised; accepted on one seed and refused on another.
   - **"The square":** Mocked at the market, then StoodUpFor, then Comforted.

**Pinning and promotion.**
- Each slice pins its own catalog hash once it passes.
- A slice moves into the shipped town only after Sid has read seasons with it on. That re-pins P3.

---

## 7. Measures and gates

**The baseline comes first** (slice acts-0). Every measure below is taken on two bases, because Sid has not yet decided which 0d.6 steps ship:
- the shipped town, as `main` ships it after 0d.6 merges;
- the 0d.6 candidate, with its recommended switches on.

Each base gets 100 seed-years (112 days each) and 50 three-year runs. Every slice is then compared against numbers from the same code.

### 7.1 Gates every slice must pass

| Gate | Today | Must hold |
|---|---|---|
| E1: seed-years with a new feud and a new friendship between households | 60-68% shipped; 69-78% for the 0d.6 candidate | 60% or more; dead towns and war towns each under 5% |
| The 0a band: witnessed placed scandals reaching 40-70% of the town over 3 or more days | 55-57% | 50% or more; the share under 40% no more than 5 points above today |
| Three-year drift (check 7) | flat, with `FondDays` 14 | mean regard within ±0.02 a season; gate kindnesses in year 3 at most 1.5× year 1 |
| Feuds and friendships per person | 7.7 feuds and 0.9 friendships a year (26 people): about 30 and 3.4 per 100 people | each within ±30%; the share between households no lower |
| Money | conserved exactly | conserved exactly |
| Cost | 0.08-0.11 s per simulated day; the replay is 1.16 MB per 28 days | at most 1.5× each |

### 7.2 Budgets for the new volume

For 26 people, per simulator year (112 days):

| Measure | Today | Band |
|---|---|---|
| Trivia | about 700 (6.2 a day) | 900-1,350 (8-12 a day) |
| News | about 265 (2.4 a day) | at most 290 (+10%), unless Sid says otherwise (question 9) |
| Heavy hostile acts from the gate (Argued, Mocked, StoodUpFor) | about 155 | at most +15%; Mocked alone must not raise the total |
| Gifts from the gate | 235 (of 544 in all) | down 40% or more with `Company` on |
| Kindnesses returned within a week, between households | 62% | rises, and stays under 90% |
| Jokes read cold | none today | 10-20% of jokes |
| Apologies accepted | none today | 50-80% |
| Remorse that lapses with no apology | none today | report |
| Hermits (0d.6 candidate) | 0.29-0.30 a seed-year, 98% from the shyest third | above 0, with 90% or more from the shyest third |
| Brawlers | 1.1-1.4 a seed-year | within ±30% |

### 7.3 The life record and threads (`StoryMetrics.cs`)

- **Outcome shares by kind.** With `Returns` on, the Ignored share of kindnesses must fall. The Answered and Avoided shares of hostile acts are reported.
- **Per person:**
  - acts done and undergone, by kind and severity;
  - feuds and friendships;
  - warmth received.

  The probe asked for this view because shares of all pairs fall as 1/n.
- **Threads.** A thread is a chain of 3 or more acts, linked by `About` or by a motive's source act, that touches 2 or more households.
  - Today's count is measured first.
  - The target for batch 1 is 3 or more threads a season at 26 people. This is design B's story-first gate.
- **Median feud length.** The days from when a pair crosses −0.3 both ways to when it leaves. Once `Repair` is on, it must stay at 28 days or more.

### 7.4 E2, for each slice

Turning a slice off must move a story metric by 20% or more. The story metrics are E1, threads, feuds or friendships per person, the Ignored share and gifts. A slice that moves none of them is cut, or kept as texture if Sid wants it, as 0d.6 did with contagion.

### 7.5 Scale

After each slice, rerun at 52 and 104 people. Use the scaling probe, or `GeneratedTown` once the layout work adds it. Per-head rates, and motives that need someone in reach, should keep acts per person within ±20% of the 26-person town. Gossip saturation at scale is a problem for the layout work, not this one.

---

## 8. Slices

| Slice | Content | Must hold before the next |
|---|---|---|
| acts-0 Seams | the records; `ActGate`; the gate built from the rows; `ActOptions` and watch; the catalog and cards; the runner's `--catalog`; `ReplayOptions`; `StoryMetrics`; the baseline | the 9 pins; tests 1-3; the baseline written into `sim/README.md` |
| acts-1 Returns | Thanked, Complimented, Joked; the rules for light kind acts; the warm budget | section 7.1; the Ignored share falls; drift |
| acts-2 Company | PlayedGame, TreatedToDrink; per-head draws; Fond's list of company acts | section 7.1; gifts; the trivia band; money |
| acts-3 Welcome | Welcomed; Curious | section 7.1; the newcomer's mean familiarity at day 28 rises; nothing else moves by more than 10% |
| acts-4 Repair | Apologised; Remorse; the answer | section 7.1; the accepted share; median feud length |
| acts-5 Sides | Mocked, StoodUpFor, Comforted; Defend; Pity from hurts seen | section 7.1; the hostile budget; war towns; brawlers |
| acts-6 Late | LateForWork | the news budget |
| acts-z Docs | design rules 4, 5 and 10 "as built"; the examples in the tiers table; `sim/README.md`; this spec's Status; the report to Sid | the batch gate: section 7.1 with every slice on, over one year and over three |

Why this order:
- **Returns first.** They are the cheapest slice, and they test the question every later slice depends on: do cheap answers turn ignored kindness into ties without drift?
- **Company second.** It replaces Fond's gifts, which is Sid's love-bombing concern.
- **Repair before Sides.** Hurts need a way out before mocking and taking sides add new ones.
- **Sides last.** It is the only slice that adds hostility (StoodUpFor), and it should be measured with Repair's Remorse already on.

---

## 9. Batch 2: places and promises

Batch 2 is built together with the town-layout work. It needs three things from that work:
- movement goals to a person or a door;
- shops named by role, in place of the hard-coded "Store", "Mart" and "Saloon";
- more than one keeper.

It also needs two engine pieces of its own:
- the `Promise` record (rule 13);
- the Ask in general: the apology's answer, widened to any request.

| Kind | Class | Driven by | What it adds |
|---|---|---|---|
| Visited | Light | Fond, Pity, Curious, and Seek (new) | a movement goal to a home's door (rule 10's visit, 0.7). Finding nobody home updates what the visitor believes about the host's hours. |
| JoinedThem | Light | Pity, Seek | walking over to someone who is alone at a haunt: the town's answer to someone left out, from what was seen |
| Invited, MetUp, StoodUp, Refused | Story | Fond, MakeUp, Return | the first `Promise` (a date); an appointment rule in `Decide`; StoodUp at base 3 (rule 13) |
| OfferedPeace, MadePeace | Story | MakeUp (a quiet feud), Remorse | repair for long feuds; `Outcome.Reconciled` |
| MadeScene | Story, loud | Answer (intensity 0.6 or more) | rule 2's sound through walls, heard without knowing who |
| Banned | Story | the keeper's Retaliate | `Consequence.Ban` used at last; a place left out of `Decide` for 14 days |
| Bought, BoughtFromRival, FoundItClosed | Ledger; Light | errands | rule 12's counter: money changes hands at the counter, the keeper's regulars, Pierre against the chain in public |

Batch 2's own questions are left to its spec, apart from those in section 13 that decide its scope.

---

## 10. Backlog

These come from the two proposals and are not designed further here.
- **Money between people.**
  - Kinds: AskedForHelp, DidFavour, LentMoney, Repaid, ForgaveDebt, CalledInDebt.
  - BrokePromise, which needs a `Reportable` flag so that a broken promise isn't treated as a crime.
  - Motives: Need, Owed, Owes.
- **Talk behind backs** (needs question 5).
  - Kinds: SpokeWellOf, BadMouthed, TalkedAbout (overheard), Complained, HadAWord, Confided, RevealedSecret, ShowedOff.
  - With them: Envy, and trust (rule 14).
- **Work made visible:** Taught, Treated, Mended, Delivered, Tended, and Pastime (a haunt that names what is done there).
- **Home:** AteTogether, with supper as a time slot; SharedMeal, BroughtFood, RanErrand, SentNote, ShirkedChores, StoppedAllowance, PlayedTruant, KidsPlayed.
- **Breaks in the routine:** StoppedComing, from a keeper's ledger.
- **Turning points:** Confessed.
- **Mischief and lies** (needs question 7):
  - Pranked;
  - CheatedAtGame, with bets on PlayedGame;
  - Vandalised, property only;
  - ShiftedBlame, Slandered.
- **Upheavals** (11a): courting, splitting up, moving out. The Upheaval tier has no kinds today.
- **Engine:**
  - `Mutual`: both parties are patients;
  - `Act.Amount` and `Act.Topic`;
  - `Reportable` and `IsCrime`;
  - the impulse path, which generalises `Temptation`;
  - card weights that scale thefts (today a theft ignores its weight, `Simulation.Money.cs:290`).

---

## 11. Risks

1. **Kindness inflation.** More kind acts could push regard up over the years, as Fond did.
   - Guards: light kinds never stir motives; plastic of 0.15 or less on light rows; the warm budget; return in kind or less; Fond's gifts kept to occasions.
   - Check: the three-year run after each slice.
2. **Repair empties E1 of feuds.**
   - Guards: Remorse is felt only toward someone not disliked, and only for a hostile act in the last 14 days. There is one apology per act, the excuse shrinks with each repeat, and accepting costs the target their dislike.
   - Check: E1 and the median feud length.
3. **Sides spiral into war towns.** StoodUpFor adds hostile acts and gives every feud a second side.
   - Guards: the hostile cooldown and fear hits; Defend lasts only an hour; a cost of 0.8.
   - Check: war towns, brawlers and the hostile budget.
4. **The shy are rescued from becoming hermits.** Thanks and compliments are the shy's acts, and comfort finds people who are hurt. If these count as being included, 0d.6's path to the hermit weakens, and Sid wants fringe people.
   - Guard: they are left out of `TallyKindIn` until question 2 is answered.
5. **News inflation.** Mocked and StoodUpFor are news.
   - Guards: Mocked replaces arguments rather than adding to them; news may rise by at most 10%.
6. **The 0d.6 merge.** This work touches the same four files.
   - Guard: build after 0d.6 merges; only hooks go into those files, everything else into new files; append only.
7. **The cast readings are guesses** (VERIFY).
   - Guard: gate acts come from traits wherever they can.
8. **Cost.** More acts, more beliefs, and more motive scans each tick. Curious, like Fond, scans every other person for each free person each tick.
   - Guard: the cost gate. The spatial index belongs to the layout work.

---

## 12. Coordination

- **Start after `claude/under-glass-0d6` merges.**
  - It edits `StirFrom`, `Weigh`, `FondMotives`, `Feel`, `Model.cs` and `DefaultTown.cs`.
  - It branched off before the viewer was merged. Its diff against main removes `Replay.cs` and the viewer, so check at its merge that main's replay and viewer survive.
- **Don't edit `DefaultTown.Acts()` or the tail of `DefaultTown.Cast()`.**
- **After 0d.6 merges, recheck these against its code:**
  - `Missing()` and its occasions: Fond's gifts;
  - `TallyKindIn`: it must skip light kinds;
  - `Shown()`: Pity and Mocked read Expression.
- **Share with the population and layout work:** `ReplayOptions` (kinds and cast) and the per-person metrics. Build them once.
- **Determinism.**
  - New draws are keyed ("perhead", kind, name, minute) and ("apology", act id).
  - Iterate in name order.
  - Never use `string.GetHashCode`.

---

## 13. Questions for Sid

1. **Fond and gifts.** With `Company` on, what does missing someone lead to?
   - (a) A gift first, as now, then the small acts.
   - (b) The small acts (a game, a drink, a compliment, a joke), with gifts only on birthdays and festivals through 0d.6's occasions.
   - (c) No gifts from Fond at all.

   *Recommendation: (b).* It is Sid's own model of gifts in a steady tie (12.7), and the one most likely to hold regard flat over three years.
2. **Do small kindnesses count as being included?** 0d.6 counts kindness received against being left out, and kindness resets missing someone.
   - (a) Thanks, compliments, jokes and games count in full.
   - (b) They count at a quarter.
   - (c) They don't count, and are reported apart as warmth.

   *Recommendation: (c) for batch 1,* with a report of how many hermit spells (a) or (b) would have ended. Then decide.
3. **How much does an accepted apology give back?**
   - (a) A fixed third of the regard the act cost.
   - (b) Law 10's excuse, which wears out: half the first time for that kind of harm from that person in 28 days, a quarter the second time, then nothing.
   - (c) No regard back; only the fear and the grudge are lifted.

   *Recommendation: (b).* Sorry means something, and less each time.
4. **Who mocks, jokes, plays and buys drinks?** The cards are guesses (VERIFY).
   - (a) Our first readings, which you correct.
   - (b) Traits only, with no per-name cards. For example: the bold and low in understanding mock; the chatty and fond joke.
   - (c) Your own list.

   *Recommendation: (b) for every gate act.* Cards only for the four per-head kinds and for Mocked, starting from (a) for you to correct.
5. **Hearing about yourself (this decides batch 2's scope).** Today a motive stirs only from what the holder saw, apart from a robbed keeper (the mod's D34). Acts behind someone's back (bad-mouthing, speaking well of someone, complaining to the mayor) can only matter if hearing about them can stir a motive.
   - (a) No: such acts move only the listener's regard.
   - (b) Yes, at corroboration strength, and only for stories about the hearer.
   - (c) Yes, in full.

   *Recommendation: (b).*
6. **Gifts and money.** A drink bought for someone costs the household 12g, but a gift still costs nothing (open since 0d).
   - (a) Gifts stay free.
   - (b) Gifts cost 5-20g from the household purse now.
   - (c) Gifts are bought at a shop, once errands exist (batch 2).

   *Recommendation: (c), with (a) until then.*
7. **Lies and vandalism (backlog).** Design B proposed lying in an interview (ShiftedBlame), made-up stories (Slandered) and damage to property (Vandalised).
   - (a) Never.
   - (b) Built last, each off by default; lies only with ways for them to be found out.
   - (c) In the next batch.

   *Recommendation: (b).* All three are within the tone limits (no violence), but lies can feel unfair to the player.
8. **What comes after batch 1?**
   - (a) Places and promises: visits, invitations and dates, the shop counter, scenes and bans, built with the town-layout work.
   - (b) Money between people: asking for help, loans, debts.
   - (c) Talk behind backs.

   *Recommendation: (a).* The layout work needs reasons for people to go to particular places, and visits and errands are what would make neighbourhoods.
9. **How much news?** "About 5 news a week" divided a year's 265 by 52 weeks. The simulator's year is 16 weeks, so the town makes about 16.6 news a week (2.4 a day), and about 6 trivia a day rather than 1.4. Is today's volume what you meant by "a few a week"?
   - (a) Yes: keep today's volume as the ceiling.
   - (b) No, aim lower: turn down `AimedRateScale` before adding acts.

   *Recommendation: (a) for batch 1,* with news allowed to rise by at most 10%. Fix the per-week figures in `sim/README.md` and design 12.7 either way.

## 12. Questions for Sid

**Sid's answers (2026-10-08): the recommendation on every question below.** He added two directions for what comes
after batch 1: take inspiration from The Sims, its popular mods and other life sims for actions; and make runs differ,
with room for "lots of twists and turns", so that new games don't feel too similar. A research pass on both is in
progress; its catalog and twist mechanisms will shape batch 2 and later.

1. Fond and gifts: with the catalog's company slice on, what does missing someone lead to? (a) a gift first, as now, then the small acts; (b) the small acts (a game, a drink, a compliment, a joke), with gifts kept for birthdays and festivals through 0d.6's occasions; (c) no gifts from Fond at all. Recommendation: (b). It is Sid's own model of gifts in a steady tie (12.7), and the one most likely to hold regard flat over three years.

2. Do small kindnesses count as being included? 0d.6 counts kindness received against being left out, and kindness resets missing someone. (a) thanks, compliments, jokes and games count in full; (b) they count at a quarter; (c) they don't count and are reported apart as 'warmth'. Recommendation: (c) for batch 1, plus a report of how many hermit spells (a) or (b) would have ended; decide after that.

3. How much does an accepted apology give back? (a) a fixed third of the regard the act cost; (b) law 10's excuse, which wears out: half the first time for that kind of harm from that person within 28 days, a quarter the second time, then nothing; (c) no regard back, only the fear and the grudge lifted. Recommendation: (b): sorry means something, and less each time.

4. Who mocks, jokes, plays and buys drinks? The cards are guesses (VERIFY). (a) our first readings of the cast, which Sid corrects; (b) traits only, no per-name cards (for example, the bold and low in understanding mock, the chatty and fond joke); (c) Sid's own list. Recommendation: (b) for every gate act, with cards only for the four per-head kinds and for Mocked, starting from (a) for Sid to correct.

5. Hearing about yourself (this decides batch 2's scope): today a motive stirs only from what the holder saw, except for a robbed keeper (D34). Acts behind someone's back (bad-mouthing, speaking well of someone, complaining to the mayor) only matter if hearing about them can stir a motive. (a) no: such acts move only the listener's regard; (b) yes, at corroboration strength, and only for stories about the hearer; (c) yes, in full. Recommendation: (b).

6. Gifts and money: a drink bought for someone costs the household 12g, but a gift still costs nothing (open since 0d). (a) gifts stay free; (b) gifts cost 5-20g from the household purse now; (c) gifts are bought at a shop once errands exist (batch 2). Recommendation: (c), with (a) until then.

7. Lies and vandalism (backlog): (a) never; (b) built last, each off by default, with lies only if there are ways for them to be found out; (c) in the next batch. Recommendation: (b). Lying in an interview, made-up stories and damage to property are all within the tone limits (no violence), but lies can feel unfair to the player.

8. What comes after batch 1? (a) places and promises (visits, invitations and dates, the shop counter, scenes and bans), built together with the town-layout work; (b) money between people (asking for help, loans, debts); (c) talk behind backs. Recommendation: (a). The layout work needs reasons for people to go to particular places, and visits and errands are what would make neighbourhoods.

9. How much news? 'About 5 news a week' (design 12.7) divided a year's 265 by 52 weeks. The simulator's year is 112 days, which is 16 weeks, so today's town makes about 16.6 news a week (2.4 a day) and about 6 trivia a day, not 1.4. Is today's volume what Sid meant by 'a few a week'? (a) yes: keep today's volume as the ceiling; (b) no: aim lower, which means turning down AimedRateScale before adding acts. Recommendation: (a) for batch 1, allowing news to rise by at most 10%. Fix the per-week figures in sim/README.md and design 12.7 either way.

## The slices' questions, and Sid's answers (2026-10-08)

The built slices (acts-1 to acts-4, #64-#67) raised these, listed on #46 as C1-C12. Sid answered in two rounds; where he asked for more first, the explanation given is noted.

1. **Fond's wait (#64).** Fond waits 28 days after a small act (`FondWarmDays`); without it, 536 compliments a year. Keep the wait, or should a small act reset missing someone? Cloud made the case for each (lean: with missing people on, 0d.6 step m, a small act eases missing in part, more for the steady, and the timer goes). ***Sid: test both.***
2. **Thanks barely happen (#64, #65).** ***Sid: cloud's option:*** compliments stop answering gifts, so a thank-you is the light reply.
3. **The trivia budget (#64).** The review's set is near its top (about 1,290 a year with acts-4). Raise it? After asking what it is (7.2: 900-1,350 trivia a year at 26, against about 700 before the catalog), ***Sid: cloud's recommendation:*** the budget is relative to the town's own trivia, so the catalog may add up to half again (1.5×): about 1,050 a year on today's town, about 1,430 with every 0d.6 step on.
4. **Gifts returned (#65).** Gifts fell a third, not 40%. Return a gift with a treat or a compliment, or is a gift for a gift right? ***Sid: any of those, depending on the person and their traits.***
5. **Per-head cards (#65).** ***Sid: yes,*** from traits instead of guesses (question 4's (b) now for the per-head kinds too).
6. **A welcome as kindness received (#66).** ***Sid: it delays withdrawal but doesn't prevent it,*** as built.
7. **Who welcomes the newcomer (#66).** All 25 in the first month. ***Sid: a few;*** "everyone else is preoccupied with their own lives".
8. **Jokes read cold (#67).** 1-2%, against 10-20%. After the explanation (jokers joke only with people they like, who mostly like them back, so a joke is rarely taken badly and the "only joking" thread barely starts), ***Sid: both:*** jokers joke with anyone they know well, liked or not, and a joke is taken badly more easily (by the other's mood or sensitivity).
9. **Friendships up 30% with the four slices on (#67).** After asking what the slices are, ***Sid: yes.***
10. **Remorse and the compliment (#67).** ***Sid: yes,*** remorse prefers an apology.
11. **`ApologyCost` (#67).** ***Sid: 0.4.***
12. **Remorse between kin (cloud's review of #67).** ***Sid:*** kin apologise "if it's in their nature", and parents should try to enforce it.

## 13. The first step

Build slice acts-0 (the seams and the baseline), then add Thanked on its own behind the Returns switch. acts-0 moves the gate's costs and act lists out of the name switches in Simulation.Desire.cs (Form, Min and IsLight at lines 63 and 279-287, ActsFor at 290-313) and into data on the rows, through ActGate. The shipped rows rebuild today's lists exactly, so behaviour doesn't change. Three tests prove it: the lists built from the rows equal today's mapping; with every row appended but every switch off, the run still gives the shipped hash; and watch mode gives the same hash. acts-0 also writes the baseline measures (E1, the 0a band, three-year drift, feuds and friendships per person, outcome shares, threads) on the shipped town and on the 0d.6 candidate. Every later slice is compared against them, so they must come from the same code. Thanked comes next because it is the smallest real act. It needs no new motive, no new record and no hook beyond the gate's data: BeganAct already marks a kindness answered with a kindness as Returned, and StirFrom already refuses to return a return. So it tests, in isolation, the question every later slice depends on: do cheap answers turn ignored kindness into ties without breaking E1, the band or the three-year drift? Timing: acts-0 edits Simulation.Desire.cs and Model.cs, which claude/under-glass-0d6 also edits, so it waits for that merge. StoryMetrics.cs and the baseline runner code are new files and can start now. One finding to pass on: the docs turn a year's counts into per-week and per-day figures using 52 weeks and 365 days, but the simulator's year is 112 days (16 weeks). Today's 265 news a year is therefore about 16.6 a simulator week, not 5. The spec's budgets use simulator days, and question 9 asks Sid about it. Spec to save as docs/under-glass/specs/acts-spec.md; source design read from /home/user/StardewValley-AI-Decisions/docs/under-glass/design.md (main and the 0d6 branch, via git show) and /home/user/StardewValley-AI-Decisions/sim/UnderGlass.Sim/ on origin/main 03c5745.
