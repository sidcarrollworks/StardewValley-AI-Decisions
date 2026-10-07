> The implementation spec as written before building (kept for reference). Where the code differs, `sim/README.md` and `docs/under-glass/design.md` ("As built") say how and why.

# Under Glass phase 0d.6: hermits, brawlers, moods that spread, and missing people: implementation spec

**Code base.** The hooks below were checked against `c469801` (main after PR #42). Line numbers refer to that commit.

**Evidence.**
- `docs/under-glass/withdrawal-research.md`: the rules W1 (shy plus left out), W2 (shy plus repeated hurts), C1 (contagion), B1 to B3 (brawlers), the build plan (steps a to h) and its pass criteria. Its section 7 holds Sid's answers of 2026-10-07.
- `docs/under-glass/masking-research.md`: the expression trait, M1 (the split), M2 (what each part drives) and M8 (the tone).
- `docs/under-glass/design.md`, section 12, question 7: households argue; expression is a seventh trait; missing people and gifts.
- The baseline of section 11, measured on 2026-10-07 at `c469801`.

**Status of the numbers.** Every per-event and per-day constant is a game choice, not a value from a paper (the research note says so for its own constants). Each is a first guess for the sweeps in section 12. Every Stardew date in X12 was read from the game's own content files (1.6.15, `Data/Characters` and `Data/Festivals/FestivalDates`) on Sid's PC on 2026-10-07, with a throwaway reader outside the repo. The expression values for the cast are a guess from the game's `Manner` and `SocialAnxiety` fields (VERIFY with Sid).

---

## 0. What 0d.6 builds

The town makes about 3.5 brawlers a seed-year and no hermits. At boldness 0.02 Penny's stance ends the year near 0 and her free hours out do not change. The research note gives three causes:
1. Rows at home and hurts a person undergoes never move stance (`StirFrom` returns before `Hurt` for kin and housemates).
2. Any kindness undoes a withdrawn stance at once (`StanceAfterKindness` removes the share felt).
3. Nothing reads another person's mood, so a low person never brings anyone down.

0d.6 builds the paths Sid asked for, each behind its own switch, off by default:
- **a.** Measure being left out, contact days, and sustained hermits and brawlers. No switch: it only records.
- **b.** Hurts at home count (W2). Households argue: the gate may act inside a household when regard there is bad.
- **c.** Moods spread (C1): bad moods weigh more at home only, softened by love.
- **d.** Shy plus left out pushes toward the hermit (W1), with the friend buffer, the included side, content loners, and inclusion discounted for a withdrawn stance.
- **e.** The dials: a withdrawn stance pulls home harder, talks less and seeks less.
- **f.** Recovery: fading by retention, a fresh start at each season, and being answered kindly.
- **g.** Patience with combative people (B1 as Sid restated it), and the coercion ratchet (B2).
- **h.** Expression, a seventh trait: what shows and what is held.
- Then **the first greeting, read through expression** (the tone, which exists and is off).
- Then **missing people**: proneness per person, steady friendships missing less, a calendar of birthdays and festivals, and repeated gifts counting for less. It replaces `FondDays` 14.
- And **watching**: one switch under which every 0d.6 rule computes what it would do, records it and changes nothing.

With every switch off, every pinned hash holds, P3 (`e9fd83b284f5c1b6`) included. The town ships with every switch off. Which steps to turn on is Sid's decision (section 15); turning any on re-pins P3.

---

## 1. Slices

Each slice is one commit with its tests green and its numbers measured. Each step is measured with its switch on, on top of the earlier steps that passed; a step that fails stays off and is reported.

| Slice | Content | Must hold before the next |
|---|---|---|
| **0d.6a Measure** | X0: the daily records, left out (E), sustained hermit and brawler spells, `WithdrawalMetrics`, the runner's "withdrawal" block, the `--log` lines for the shyest five. X13's watch switch and its notes. | Every pin unchanged. The baseline of section 11 reproduces. |
| **0d.6b Home** | X1 (`HomeHurtOn`), X2 (`HouseholdGateOn`). | Section 10, step b. |
| **0d.6c Contagion** | X3 (`ContagionOn`): source-tagged mood entries, the shared-act exclusion, the asymmetry at home, the cap. | Step c. |
| **0d.6d Left out** | X4 (`LeftOutOn`), X5 (`InclusionDiscountOn`). | Step d. |
| **0d.6e Dials** | X6 (`DialsOn`). | Step e. |
| **0d.6f Recovery** | X7 (`RecoveryOn`). | Step f. |
| **0d.6g Patience** | X8 (`PatienceOn`), X9 (`CoercionOn`). | Step g. |
| **0d.6h Expression** | X10 (`ShowOn`), the seventh trait. | Step h. |
| **0d.6t Tone** | X11: the tone read through expression. | Measured and reported; on or off is Sid's call. |
| **0d.6m Missing** | X12 (`MissingOn`), the calendar. | Section 10, step m, with the three-year check. |
| **0d.6z Docs** | README, design "As built", this spec's departures, the report to Sid. | The phase gate (section 10). |

---

## 2. Data model

### 2.1 `Model.cs` (appended; no positional record changes)

```csharp
public sealed record Temperament(double Chattiness, double Boldness, double Understanding, double SelfRegard,
    double Sensitivity = 0.5, double Retention = 0.5, double Expression = 0.75);   // Expression appended (X10)

public enum Trait { Chattiness, Boldness, Understanding, SelfRegard, Sensitivity, Retention, Expression } // appended

/// A day of the year: season 0 spring to 3 winter, day 1 to 28.
public readonly record struct Birthday(int Season, int Day);

public sealed record Villager(..., IReadOnlyDictionary<string, Kin>? Family = null, Birthday? Birthday = null); // appended

/// One person's days, for the measures of 0d.6 (X0). Index: day of the run.
public sealed record PersonDays(int[] KindIn, bool[] Met, int[] KindOut, int[] Unanswered, int[] OutMinutes, double[] LeftOut);
```

`Simulation.Get` and `With` (`Simulation.Character.cs:27`, `:37`) gain the `Expression` case before their default branch, which today falls through to `Retention`.

### 2.2 `FeelingOptions` (`Feelings.cs`), appended after `AimedRateScale`

Every switch has an effect only while the gate acts (`Acting`: feelings steer, `Desire` and `DesireActs` on), and the stance rules only while `StanceOn`.

**Switches** (all `false`; none is set in `DefaultTown.Feelings()`):

| Switch | Step | Rule |
|---|---|---|
| `WithdrawalWatch` | all | X13: each rule below computes and records what it would do, and changes nothing |
| `HomeHurtOn` | b | X1 |
| `HouseholdGateOn` | b | X2 |
| `ContagionOn` | c | X3 |
| `LeftOutOn` | d | X4 |
| `InclusionDiscountOn` | d | X5 |
| `DialsOn` | e | X6 |
| `RecoveryOn` | f | X7 |
| `PatienceOn` | g | X8 |
| `CoercionOn` | g | X9 |
| `ShowOn` | h | X10, and X11 with `ToneOn` |
| `MissingOn` | m | X12 |

**Constants** (first guesses; section 12 sweeps the marked ones):

| Name | Default | Rule | Meaning |
|---|---|---|---|
| `LeftOutDays` | 28 | X0 | the window E is read over |
| `LeftOutKindWeight`, `LeftOutContactWeight`, `LeftOutUnansweredWeight` | 0.4, 0.3, 0.3 | X0 | E's three parts |
| `LonerChattiness`, `LonerBoldness`, `LonerSpan` | 0.5, 0.3, 0.3 | X0 | content loners (Sid's answer 3) |
| `HomeHurtWeight` | 0.5 | X1 | a hurt from kin or a housemate counts half (kin forgive and are forgiven) |
| `HurtRepeatStep`, `HurtRepeatCap`, `HurtRepeatDays` | 0.25, 2, 28 | X1 | repetition sensitizes, up to double |
| `HomeCoverAt` | 0.2 | X2 | at home, love covers a hostile answer only at or above this |
| `ContagionK` * | 0.01 | X3 | |
| `ContagionCap` * | 0.05 | X3 | a day's contagion entries for one person, at most this either way |
| `TieFriend`, `TieOther` | 0.6, 0.3 | X3 | the tie: 1 for kin and housemates |
| `HomeNeg` | 2 | X3 | bad moods weigh double at home, softened by love |
| `ContagionConditions` * | 0 | X3 | share of the conditions (need, want, held) in the state passed on; 0 is the note's "mood only" |
| `LeftOutRate` * | 0.01 | X4 | W1's Kx, a day |
| `IncludedBelow`, `IncludedShyAbove`, `IncludedPull` | 0.2, 0.5, 0.01 | X4 | the included shy approach |
| `FriendBuffer`, `FriendBufferWithdrawn`, `FriendWithdrawnAt`, `FriendSeenDays` | 0.3, 0.6, -0.3, 7 | X4 | |
| `FriendLossHurt`, `FriendLossMargin` | 0.3, 0.1 | X4 | losing a friend is a W2 hurt |
| `WithdrawnAt`, `InclusionShare` | -0.3, 0.3 | X5 | |
| `StanceHomeDial`, `ChatStance` | 3, 0.5 | X6 | |
| `StanceKeepSpan`, `FreshDays` | 0.013, 7 | X7 | |
| `PatienceRounds`, `PatienceRefillDays`, `PatienceDrop` | 2.5, 10, 0.25 | X8 | |
| `CoerceStep` * | 0.05 | X9 | |
| `HeldPull`, `ShowLowMood`, `ShowMin` | 0.3, 0.2, 0.05 | X10 | |
| `MissDays`, `ProneSpread`, `ProneMin`, `ProneMax` | 10, 2, 0.5, 1.5 | X12 | |
| `SteadyDays`, `MissSecure`, `OccasionWish` | 8, 0.8, 0.5 | X12 | |
| `AdaptDays`, `AdaptFactor` | 28, 0.85 | X12 | |

### 2.3 State: `Simulation.Withdrawal.cs` (new partial)

- `_days` (per person `PersonDays`), allocated in `StartDesire` for the run's length while `Desiring`.
- `_pairMet`: `uint[n, n]`, one bit a day, the last 32 days together (an hour or more) per pair; X12's steadiness.
- `_lastingTicks`: per person, the ticks of W2 hurts in the last `HurtRepeatDays` (X1's repetition).
- `_friendLast`: `bool[n, n]`, who counted as a friend last night (X4's loss).
- `_windowFrom`: the day E's window may start from (0; with `RecoveryOn`, the season's first day).
- `_caughtToday`: per person, the contagion entries taken today (X3's cap); `_gave`, `_caught`: per person, the contagion passed on and taken over the run.
- `_patience`: per ordered pair, rounds used and the tick of the last change (X8).
- `_watched`: per rule, a count and a sum (X13).
- `_affects` entries gain a source: `(int Tick, double Amount, int Src)`, where `Src` is the act a feeling came from, or -1 for company, the tone and contagion (X3).

### 2.4 `SimResult` (appended, `required`; empty when the gate is off)

```csharp
/// Each person's days (0d.6, X0).
public required IReadOnlyDictionary<string, PersonDays> Daily { get; init; }
/// Contagion each person passed on and took over the run (X3).
public required IReadOnlyDictionary<string, (double Gave, double Caught)> Contagion { get; init; }
/// With WithdrawalWatch: what each rule would have done (count, sum).
public required IReadOnlyDictionary<string, (int Count, double Sum)> Watched { get; init; }
```

### 2.5 `DefaultTown.cs`

- **Expression** (X10), from the game's `Data/Characters` fields in `fixtures/game/temperament/characters.json`: 0.75, plus 0.15 for `Rude` or less 0.1 for `Polite`, plus 0.05 for `Outgoing` or less 0.15 for `Shy`. Sid's reading overrides two: Penny 0.25 (she masks) and Pam 0.85 (she lets it out) (design 12.7; `masking-research.md` M1). The newcomer keeps 0.75. A guess: VERIFY with Sid.
- **Birthdays** (X12), on each card, from `Data/Characters`: Kent spring 4, Lewis spring 7, Vincent spring 10, Haley spring 14, Pam spring 18, Shane spring 20, Pierre spring 26, Emily spring 27; Jas summer 4, Gus summer 8, Maru summer 10, Alex summer 13, Sam summer 17, Demetrius summer 19; Penny fall 2, Jodi fall 11, Abigail fall 13, Marnie fall 18, Robin fall 21, George fall 24; Caroline winter 7, Sebastian winter 10, Harvey winter 14, Evelyn winter 20, Leah winter 23. The newcomer has none.
- **Festivals** (X12), from `Data/Festivals/FestivalDates`: the Egg Festival spring 13, the Flower Dance spring 24, the Luau summer 11, the Dance of the Moonlight Jellies summer 28, the Stardew Valley Fair fall 16, Spirit's Eve fall 27, the Festival of Ice winter 8, the Feast of the Winter Star winter 25. In `Calendar.cs`, with day 0 as spring 1 (a Monday, as in Stardew and as `Clock.Weekday` already counts).

---

## 3. Notation

- *Close(a, b)*: kin or housemates (`Simulation.Desire.cs:61`).
- *St(a, b)*: the effective regard decisions read (`Simulation.Feelings.cs:131`).
- *Sens(i)*: 0.5 + sensitivity. Applied only to new terms that have no felt amount (W1, contagion, the included side), never twice (research 5).
- *shy(i)*: 1 - boldness.
- *Lasting(h, felt, weight)*: X1's W2 hurt. *Ease(h, share)*: `StanceAfterKindness` with that share.
- Every new nightly loop runs over people in name order, and draws nothing. No 0d.6 rule draws from `Rng`; the rules are arithmetic on state, so the order of evaluation is the only source of difference, and it is fixed.

---

## 4. Rules

### X0. Measuring (step a; always while the gate runs)

Writes nothing to the log and draws nothing. Per person per day:
- **KindIn**: kindnesses received: an aimed kind act (gift or help) undergone, from someone not Close. Counted in `Underwent` (`Simulation.Desire.cs:639`).
- **Met**: a day with `ContactMinutes` (60) or more together with someone not Close. Counted with `_lastContactDay` in `CloseDesires`.
- **KindOut, Unanswered**: the person's own aimed kind acts to someone not Close whose outcome resolved that day (Returned, Rebuffed, Avoided or Ignored); of them, Ignored. Counted in `SetOutcome` (`:670`) for `Did` events only.
- **OutMinutes**: free minutes out of home, as `CountOut` (`:95`) counts them, by day.
- **LeftOut**, E(i), at night, over the window W: the last `LeftOutDays` days, never before `_windowFrom`:

```
rel(x, med) = med > 0 ? clamp(1 - x / med, -1, 1) : 0
E(i) = clamp( 0.4 x rel(kindIn_W(i), median kindIn_W)
            + 0.3 x (1 - solitary(i)) x rel(met_W(i), median met_W)
            + 0.3 x (kindOut_W(i) > 0 ? unanswered_W(i) / kindOut_W(i) : 0), 0, 1 )
solitary(i) = clamp((LonerChattiness - chattiness) / LonerSpan, 0, 1) x clamp((boldness - LonerBoldness) / LonerSpan, 0, 1)
```

- Medians over everyone (the mean of the two middle values for an even count). Relative to the town, so a quiet town does not make everyone left out (research 3a).
- `rel` is clamped at -1, a departure from the note's unclamped form, so a person with three times the median of kindness does not cancel any amount of being unanswered.
- **Content loners** (Sid's answer 3): someone who prefers solitude, low in chattiness and not shy, is not harmed by time alone, so their contact part counts less. Shane 0.67, Kent and Sebastian 0.44, George 0.33, the newcomer 0.22, Penny and Harvey 0.
- With `RecoveryOn`, E is 0 while W holds fewer than `FreshDays` days (X7).

**Sustained measures** (metrics only, `WithdrawalMetrics`):
- *Hermit spell*: 28 or more days in a row with stance at -0.5 or below, **and** mean free hours out over the spell's last 28 days below 60% of the person's own first-season mean (research 3c: both parts are needed; the hikikomori definition is behaviour at home).
- *Brawler spell* (B3): 28 or more days in a row at +0.5 or above.
- *The shyest third*: the 9 of 26 lowest in boldness at the start (ties by name).

### X1. Hurts at home count (step b, `HomeHurtOn`; W2)

Hurts that stir no motive today move stance, with no motive:

```
Lasting(h, felt, weight):
    n   = h's W2 hurts in the last HurtRepeatDays
    f   = felt x weight x min(HurtRepeatCap, 1 + HurtRepeatStep x n) x Buffer(h)      // Buffer: X4, else 1
    stance(h) = StanceAfterHurt(stance(h), f, boldness(h))                             // with ShowOn: X10's split
```

Called from:
1. `StirFrom`, before its return for Close pairs (`:157`): a hurt aimed at the holder (row target Chosen or Kin, joy below 0), seen, by kin or a housemate. Weight `HomeHurtWeight`. Rows from the rates and from X2's gate both count (Sid: "rows at home come through the gate as well as through the rates"). A child's squabble counts at its own small size.
2. `Undergo` (`Simulation.Feelings.cs:481`): an act the person undergoes with a cause (actor row, joy below 0, freedom above 0, a target): warned, taken in, questioned, service, a family row. Weight 1, or `HomeHurtWeight` when the cause is Close (a family row).
3. `Accused` (`:502`), the guilty: named or confronted over their own scandal. Weight 1. (The innocent already move stance through `StirAccused`.)
4. `Shamed` (`:565`): kin ashamed of a scandal. Weight `HomeHurtWeight`.
5. X4's friend loss.

Mishaps (a stumble, a collapse) blame nobody and are not W2 hurts (a decision for Sid). The direction is the existing one: the bold who are hurt at home grow combative, the shy withdrawn (Patterson's coercive family process is about families).

### X2. Households argue (step b, `HouseholdGateOn`)

Sid (2026-10-07): the gate may act inside a household when regard there is bad.
- `StirFrom`: a heavy hostile act (an argument) aimed at the holder by kin or a housemate, seen, while St(holder, them) < `HomeCoverAt`: record the hit (fear), and stir **Answer**, unless they gave cause. No `Hurt`: at home, stance moves by X1 only. Nothing else stirs at home: no Return, MakeUp, Retaliate, Pity, Fond or tone.
- `Weigh` (`:383`): love covers a hostile motive toward someone Close only at `HomeCoverAt` or above (0.4 elsewhere).
- No avoiding at home: a hostile motive toward someone Close that cannot clear the gate lapses instead of starting `StartAvoid`.
- Families still cover everywhere else: no report, retelling, suspicion or confrontation of kin.

Who it reaches as the town is: Sebastian toward Demetrius and Abigail toward Pierre start at -0.3, so their stepfather's or father's argument can now be answered. Pam and Penny have no argument in their acts (`DefaultTown.cs:375-381`), so the trailer gets no rows from the rates; Sid's "real arguments in a household like theirs" needs a cast change, which is his (section 15). The runner's `--acts Pam=Argued:0.5` measures it.

### X3. Moods spread (step c, `ContagionOn`; C1)

In `Company` (`Simulation.Feelings.cs:166`), per chat, after company's own joy, each side takes one entry, both computed from the moods before either is added:

```
gap(A <- B) = Shown(B without the acts A felt) - State(A without the acts B felt)
e(A <- B)   = ContagionK x Sens(A) x T(A,B) x gap x Neg(A,B)
State(x)    = Squash(x's mood entries, weighted as MoodOf) + ContagionConditions x Conditions(x) / PowerScale
Shown(x)    = State(x) x Show(x)                          // Show = 1 until ShowOn (X10)
T(A,B)      = 1 if Close; TieFriend if St(A,B) >= FriendAt; else TieOther
Neg(A,B)    = HomeNeg x (1 - 0.5 x max(0, St(A,B)))  if gap < 0 and Close;  else 1
```

- **Shared acts are not felt twice.** Mood entries carry their source act. What B passes to A leaves out B's entries from acts A has a felt record for (`_felt`, holder and act), since sympathy and imitation already gave A its share. A's side leaves out A's entries from acts B felt, so two people who felt the same thing pass nothing between them.
- **The cap**: a person's contagion entries in a day sum to at most `ContagionCap` either way; an entry past it is cut to fit.
- The difference form pulls toward the other and never past them; equal states pass nothing; a gloomy pair cannot sink below its lower member by contagion alone.
- Contagion entries pass on like any mood, so second-degree spread emerges at about K x T a step (Sid's answer: no rule for further degrees).
- It moves mood only, never regard: regard comes from acts, and contagion has none.
- `Conditions` (need, an unmet want, being held) at share 0 is the research note's rule. Mood holds only the last three days of events and company, so a poor household is not low by mood. At 1, the state passed on is the power of acting's own reading, so Pam's short purse can pull Penny down at home (research 2d's scenario). Measured both ways; Sid's decision.

### X4. Left out (step d, `LeftOutOn`; W1)

At night, after X0, for each person:

```
stance -= LeftOutRate x E x shy^2 x Sens x (1.5 - selfRegard) x Buffer
if E < IncludedBelow and shy > IncludedShyAbove and stance < 0:
    stance = min(0, stance + IncludedPull x Sens)                // the included shy approach (Gazelle and Rudolph)
```

- shy squared is the interaction: boldness 0.02 gets 0.96, 0.5 gets 0.25, 0.8 gets 0.04.
- (1.5 - selfRegard): the lonely put exclusion down to themselves (Vanhalst).
- **The friend buffer**, on W1 and W2 (`Buffer`): `FriendBuffer` if the person has a friend, someone not Close they hold at `FriendAt` or above and spent a day with in the last `FriendSeenDays`, whose stance is above `FriendWithdrawnAt`; `FriendBufferWithdrawn` if every such friend is withdrawn (Rubin 2006); 1 if there is none. The note's "mutual" (regard both ways) becomes the holder's own regard: no rule reads another mind's regard. The friend's stance is read as behaviour that shows (staying home, talking less).
- **Losing a friend**: a friend held at `FriendAt` or above who falls below `FriendAt - FriendLossMargin` is a W2 hurt of `FriendLossHurt`. The margin keeps a seeded friendship at exactly 0.4 from counting a loss at every small slight.
- At the steady state, stance sits near -rate x E x ... / (1 - keep). With keep 0.975 and Kx 0.01, Penny at E 0.5 settles near -0.1: no hermit. Kx is swept (section 12).

### X5. Inclusion discounted (step d, `InclusionDiscountOn`)

In `StirFrom`'s kindness branch (`:201`): while stance is below `WithdrawnAt`, a kindness eases it by `InclusionShare` of what it would, unless the giver is a friend (St at `FriendAt` or above) or the kindness comes inside a quarrel (a heavy hostile act between the two, either way, in the last `FearDays`). One gift does not undo weeks of being left out (Vanhalst; Masi). Combative stances keep the full share: that holds the brawler count, and it is a choice, not a finding.

### X6. The dials (step e, `DialsOn`)

- **Home**: `HomeWeight` (`:116`) uses `StanceHomeDial` (3) in place of `StanceHome` (1).
- **Chattiness in use**: each person's chattiness in the chat chance (`Simulation.cs:1063`) is chattiness x (1 + `ChatStance` x min(0, stance)).
- **Seeking**: Fond's (and X12's) wish is multiplied by (1 + min(0, stance)). The withdrawn stop seeking even those they love (Williams; Riva).

Each lowers what E counts, which closes the loop (Boivin; Cacioppo 2009) with a gain below 1, while stance still fades.

### X7. Recovery (step f, `RecoveryOn`)

- **Fading by retention**: stance keeps `StanceKeepPerDay` + `StanceKeepSpan` x (retention - 0.5) a night: 0.9685 to 0.9815, half-lives of about 22 and 37 days (Zadro: the anxious stay hurt longer).
- **A fresh start** (Gazelle and Faldowski 2019, VERIFY; Sid's answer 5): at each season's start, E's window starts again, and for its first `FreshDays` days E is 0.
- **Being answered kindly**: one's own kindness returned eases stance by the share felt, at full strength (Masi: being understood helps most). Today the return of one's own kindness returns before easing (`:198`).
- Kindness from a friend and inside a quarrel already eases at full strength (X5).

### X8. Patience with combative people (step g, `PatienceOn`; B1 as Sid restated it)

Sid: "it is the people dealing with a combative person whose kindness toward them runs out, after a couple of rounds, not at the first one: patience drops off quickly, by how patient the person is."

```
capacity(h)       = PatienceRounds x (0.5 + understanding(h)) x (1.5 - sensitivity(h))      // about 2 to 3 rounds
used(h, s)       <- max(0, used - days since / PatienceRefillDays) + 1    at each heavy hostile act s aims at h, as h sees it
left(h, s)        = capacity(h) - used(h, s), refilled to now
kind daring of h toward s  -= PatienceDrop x clamp(1 - left, 0, 1)
```

Rounds are counted where the hit is (`Hit`, `:221`). The drop is read in `Weigh` for kind motives (Return, MakeUp, Fond, Pity). Penny (understanding 0.7, sensitivity 0.67) has 2.5 rounds; Pam 1.9; most people 2.5. This replaces the note's stance-based B1 (`RejectedLessKindOn`).

### X9. The coercion ratchet (step g, `CoercionOn`; B2)

When someone keeps away from an argument (`StartAvoid`, `:477`, which already resolves the arguer's act as Avoided), the arguer's stance rises by `CoerceStep` x the intensity of the avoider's motive. Escalation won (Patterson). Answered or reconciled, nothing changes. This joins the two extremes: a brawler grows by driving the shy away, and the shy one withdraws because of it.

### X10. Expression, the seventh trait (step h, `ShowOn`)

Sid (2026-10-07): a seventh trait, expression, how much of a feeling shows, inherited and plastic like the other six under rule 18.

```
Show(i) = clamp(expression(i) x (1 - ShowLowMood x max(0, -mood(i))), ShowMin, 1)      // M1, with its low-mood term
```

- **What others perceive is what shows.** Contagion passes Shown(B) = State(B) x Show(B) (X3).
- **A hurt splits.** Every stance hurt (`Hurt` and X1's `Lasting`) of size f: the shown part f x Show moves stance by boldness, as now; the held part f x (1 - Show) moves stance toward withdrawn by `HeldPull` x held, whatever the boldness (design 12.7; research 5's table). A bold masker is pulled a little toward withdrawal.
- **The answering motive takes the shown part.** Answer and Retaliate are stirred with f x Show (M2: the shown part drives answering acts).
- **Mood takes the whole feeling.** Masking hides the face, not the feeling (Webb et al. 2012; Gross and John's suppressors feel more negative emotion, not less).
- Regard moves as now: the held hurt still lowers regard, silently (M8).
- Not built, with reasons in section 13: the held reservoir R (stance already is a fading held state), the threat and kin terms of M1, the closeness cost, displacement and rupture, caring masking.

### X11. The first greeting, read through expression (`ToneOn` with `ShowOn`)

No new rule: the tone (`Tone`, `:599`) already moves mood, regard and stance (`Hurt`) and stirs Answer on a curt reading. With `ShowOn`, its hurt splits as X10 says and its Answer is stirred with the shown part. So Penny feels a curt greeting in full, answers little, and is pulled toward withdrawal; Pam answers it. The tone and contagion form the misreading loop of research 2d; they are measured together.

### X12. Missing people (`MissingOn`; replaces `FondDays` 14)

Sid (2026-10-07): people differ in how prone they are to missing others; in a secure friendship with regular contact, missing goes down; gifts in a steady tie are kept for birthdays and holidays; repeated gifts count for less.

In `FondMotives` (`:368`), for someone not Close in reach:

```
base     = max(0, St(h, s) - LoveAt)
apart    = min(1, daysApart x Prone(h) / MissDays)                 // as today, with MissDays 10 at proneness 1
steady   = min(1, days together in the last 28 / SteadyDays)        // from _pairMet
occasion = 1 on s's birthday or a festival day, else 0
wish     = base x apart x (1 - MissSecure x steady) + OccasionWish x base x occasion
Prone(h) = clamp(1 + ProneSpread x (mean(chattiness, sensitivity, retention, 1 - selfRegard) - 0.5), ProneMin, ProneMax)
```

- **Hedonic adaptation**: in `Feel` (`:220`), a kindness of the same kind from the same giver feels `AdaptFactor` less for each earlier one in the last `AdaptDays`, in mood and in regard, on top of rule 4's halving within 7 days. Kin included: Evelyn's fifty gifts a year to George count for less too.
- The occasion wish lasts the day; the close call is asked once a day, as Fond's is today.
- Not built: a gift that "fits the person very well" (needs gift tastes), and constant gifts counting against the giver (Sid: "possibly"). Section 13.

### X13. Watching (`WithdrawalWatch`)

Every rule above, with its switch on and watching on, computes what it would do and records it in `_watched` (rule, count, sum), and leaves everything as it was: no stance, mood, motive, weight or chance changes. A test runs a year with every switch on and watching on, and gets P3's hash.

---

## 5. Night order

`CloseDay` (`Simulation.cs:1207`) at 23:59:
1. `ForgetSightings`, `CloseMoneyDay` (unchanged).
2. `CloseDesires` (unchanged order): contact days (and now X0's Met and X12's pair bits); stance fades (X7's keep by retention) and is recorded; outcomes past their window resolve (X0's tallies); pruning.
3. **`CloseWithdrawal`** (new): X0's E; X4's push, the included pull and friend losses; X7's fresh start at a season's end; X3's daily cap reset; the day's stance recorded again.
4. `CloseFeelings` (unchanged; it clears `_together`, which 2 and 3 read first).

---

## 6. Hook sites (at `c469801`)

| Where | Line | What changes |
|---|---|---|
| `Simulation.Desire.cs` `StirFrom` | 144 | X1 and X2 before the Close return (157); X10's shown part in the stirs; X5 in the kindness branch (201); X7 at the early return (198) |
| `Hurt` | 215 | X10's split |
| `Hit` | 221 | X8's rounds |
| `HomeWeight` | 116 | X6 |
| `CountOut` | 95 | X0's daily minutes |
| `FondMotives` | 360 | X12; X6's seeking |
| `Weigh` | 375 | X2's cover (383); X8's drop (391) |
| `StartAvoid` | 477 | X2's no-avoid at home; X9 |
| `Tone` | 599 | X11 via `Hurt` and the stir |
| `Underwent`, `SetOutcome` | 639, 670 | X0's tallies |
| `CloseDesires` | 676 | X0's Met and pair bits; X7's keep |
| `Simulation.Feelings.cs` `_affects`, `AddMood`, `MoodOf` | 20, 138, 144 | the source tag |
| `Company` | 166 | X3 |
| `Feel` | 197 | X12's adaptation (220-229) |
| `Undergo`, `Accused`, `Shamed` | 481, 502, 565 | X1 |
| `Simulation.cs` `Socialise` | 1063 | X6's chattiness |
| `CloseDay` | 1207 | `CloseWithdrawal` |
| `SimResult` | 76 | section 2.4 |
| `Feelings.cs` `FeelingOptions`, `DesireMath` | 121, 340 | section 2.2; new pure functions in `WithdrawalMath` |
| `Model.cs` | 53, 85, 282 | section 2.1 |
| `Simulation.Character.cs` | 27, 37 | `Expression` |
| `DefaultTown.cs` `Cast` | 432 | expression and birthdays |

---

## 7. Determinism, switches and pins

- No 0d.6 rule draws a random number. Nightly loops run in name order. Dictionaries iterated for state are sorted or keyed by index.
- With every switch off, no rule's output changes: X0 only records, the source tag only travels with mood entries (summed in the same order), and every new branch is behind its switch. Pins that must hold, unchanged: `e7f6653087ff7e18`, `388d128d7fd4cdf2` (feelings off), P1 and P2 `c0488cc6bf81e64f`, the gate's `a123526358094b43`, `07505a08bf1a9f78`, `f2af5b9f5b1fb4c4`, `b6a87fe3b8fc0916`, and P3 `e9fd83b284f5c1b6`.
- Watching with every switch on gives P3 (X13).
- Each step's own configuration is pinned once it is measured (a year of seed 1), so later work notices if it moves.
- New log lines only under a switch. The runner's `--log` prints the shyest five after the hash, outside the log.

---

## 8. Tests

**Pure** (`WithdrawalMathTests`): E is relative to the town, zero medians count nothing, content loners count contact less, each part clamps; contagion pulls toward the other and never past, equal states pass nothing, love softens the bad at home, the tie strengths; W1's push is shy squared, so the bold barely move; W2's repetition doubles at most; patience lasts a couple of rounds and refills; proneness and the wish (steady ties miss less, occasions give); the expression split (shown plus held is the whole, held pulls toward withdrawal at any boldness); the calendar (day 0 is spring 1, each birthday and festival on its day).

**Scenes** (`WithdrawalSceneTests`, in the style of `DesireSceneTests`: one room, set traits, injected acts, an assertion on a number):
1. `ASadHousemateBringsYouDown`: a housemate's low mood lowers Penny's with contagion on, and not with it off.
2. `AGladFriendLifts`: the same, upward, at friend strength.
3. `EqualMoodsChangeNothing`, `NoChatNoContagion`.
4. `LoveSoftensTheBadAtHome`: regard 0.8 halves the pull down compared with regard 0.
5. `ASharedActIsNotFeltTwice`: two people who both saw an argument pass nothing of it between them.
6. `TheCapHolds`: five low people chatting with one person in a day give no more than the cap.
7. `TheShyLeftOutWithdraw`: boldness 0.02, no kindness and no company for 84 days while the others meet: a hermit spell, and hours out fall.
8. `TheShyIncludedDoNot` (invariant): over 20 seeds, a shy person given a kindness and a day together each week is never a hermit.
9. `TheBoldLeftOutBarelyMove`: boldness 0.8 in scene 7 ends above -0.1.
10. `RowsAtHomeWithdrawTheShy`: rows from a housemate move a shy person's stance down with `HomeHurtOn`, and stir no motive.
11. `AFriendBuffers`: scene 7 plus one friend seen weekly stays above -0.3.
12. `AStrangersGiftIsNotEnough`: a stranger's gift to someone withdrawn eases a third of what a friend's eases.
13. `SensitivityCutsBothWays`: high sensitivity withdraws faster when left out and recovers faster when included.
14. `WinningMakesBolder`: an argument met by avoidance raises the arguer's stance (X9).
15. `HouseholdsArgueWhenRegardIsBad`: a housemate held below 0.2 answers an argument at home; held at 0.6, never.
16. `PatienceRunsOut`: after two arguments a return of kindness still happens; after the third, it does not.
17. `PennyHoldsWhatPamLetsOut`: the same hurts withdraw a masker more and spread less of her mood than someone who shows it.
18. `AFreshStartEachSeason`: E is 0 in a season's first week with recovery on.
19. `SteadyFriendsGiveOnBirthdays`: a steady pair gives on the birthday, not from days apart; a friend not seen for weeks misses them.
20. `RepeatedGiftsCountForLess`: the fifth gift in a month from the same person moves mood and regard less than the first.
21. `AContentLonerIsNotLeftOut`: a bold, quiet person alone all day gets a lower E than a shy one alone all day.

**Pins and switches**: every pin above holds with every switch off; watching with every switch on gives P3; each step's own configuration is pinned.

**Metrics** (`WithdrawalMetricsTests`): a hermit spell needs both 28 days and the fall in hours out; a brawler spell needs 28 days; a spell cut off by the run's end counts, but not toward recovery; the shyest third.

---

## 9. Metrics and runner

`WithdrawalMetrics.Summarise(runs, cast, o)` gives `WithdrawalStats`; the runner prints a "withdrawal" block whenever the gate runs:
- by season: mean and 90th percentile of E; kindness received, contact days and unanswered kindness, a person-season;
- sustained hermits and brawlers a seed-year (distinct people) and their spells, who they are, the share of hermits from the shyest third;
- recovery: of the hermit spells that end inside the run, the share back above -0.3 within 28 days; seed-years with a hermit through the whole year, and that hermit's E;
- hermits' free hours out, first season against the spell;
- the power of acting: mean, spread, share of person-days below 0.35, seed-years with a sink (anyone's 28-day mean below 0.3);
- contagion passed on and taken, the top five each way, and Penny against Pam;
- arguments inside households a year (by the gate and at the rates) and family feuds;
- watched rules, when watching.

Runner flags: `--0d6 <steps>` (for example `--0d6 bcd`) turns those steps' switches on; `--acts <Name>=<Kind>:<weight>` (repeatable) sets an act weight on someone's card for experiments; `--fo` reaches every new constant. `--log` prints, after the hash, each of the shyest five with their boldness, mean E by season, stance at each season's end, and free hours out by season.

---

## 10. The gate for every step

Each step runs the three town runs and the tests, with its switch on and every earlier step that passed:

| Run | Command | Pass |
|---|---|---|
| 0a band | `--seeds 400 --days 14 --inject` | 52% or more in band (by sight and gossip), 28% or less over 70% |
| one year | `--seeds 200 --days 112` | E1 60% or more; no war or dead towns; below -0.2 at 1-5% from the second season; money 0.000 g |
| three years | `--seeds 50 --days 336` | year 3 below -0.2 at 1-5% and moved 0.1+ at 5-25%; moved at d335 at most 1.5 x d111 |
| tests | `dotnet test sim/UnderGlass.sln` | all pass |

And each step's own:

| Step | Pass |
|---|---|
| a | every hash unchanged; the baseline reproduces (hermits 0; Penny's stance about 0) |
| b | Penny's mean stance over the year lower than the baseline; sustained brawlers at most 1.2 x the baseline; arguments inside households and family feuds reported |
| c | mean power within ±0.03 of the baseline; its spread up by at most 50%; at most 10% of person-days below 0.35; a sink in at most 5% of seed-years; E2: switching it off moves a story metric by 20% or more |
| d | **0.2-1.0 sustained hermits a seed-year** (Sid's answer 1); 80% or more of them from the shyest third; scene 7's hours out fall 25% or more; a shy person with no exclusion never a hermit (scene 8) |
| e | in the town runs, hermits' free hours out fall 25% or more from their first season; trivia and news within ±15% of the baseline |
| f | 40-60% of hermit spells back above -0.3 within 28 days of their end; a hermit through the whole year in at most 20% of seed-years unless that person's E is still above 0.6 |
| g | sustained brawlers within ±50% of the baseline; reconciliations 0.5 a year or more; feuds 7.72 a year ±25% (5.8-9.7) |
| h | Penny more withdrawn and passing on less than Pam; the gate holds |
| t | reported (tone off and on, with contagion and expression on) |
| m | the three-year check above; gifts by the gate in year 3 at most 1.25 x year 1; birthday and festival gifts reported |

---

## 11. Baseline (measured 2026-10-07 at `c469801`, this PC)

| | Result |
|---|---|
| 0a band (400 x 14) | 55% in band, 25% over 70% (by sight and gossip) |
| one year (200 x 112) | E1 60%; war 0%, dead 0%; below -0.2: d27 1.1%, d55 1.6%, d83 1.9%, d111 2.1%; money 0.000 g; run time 58 s |
| power of acting | mean 0.55, spread 0.06; lowest Pam 0.43, Penny 0.44 |
| ties a year | feuds 7.72, friendships 0.89, reconciliations 1.42 |
| news, trivia a year | 265, 696 |
| stance | ever a hermit (season end, -0.5) 0.0 a run, ever a brawler 3.5 |
| Penny | stance at the end -0.03; free hours out 5.9 a day in season 1, 5.8 in season 4 |
| three years (50 x 336) | moved 0.1+: d111 13.9%, d335 20.3% (1.46 x); below -0.2 in year 3 3.1-3.9%; feuds 7.21, friendships 2.35 a year |

---

## 12. Tuning order, with fallbacks

1. **c, contagion.** The note's K 0.01 moves a typical mood by about 0.01 a day against company's 0.05-0.15. Sweep `ContagionK` {0.01, 0.03, 0.1} x `ContagionConditions` {0, 1}, raising `ContagionCap` with K (0.05, 0.1, 0.2). Take the smallest K that passes E2 within the power bounds. If none passes E2, keep it off and report.
2. **d, left out.** Sweep `LeftOutRate` {0.01, 0.02, 0.03, 0.05} for 0.2-1.0 hermits a seed-year. If hermits come from outside the shyest third, raise the shy exponent before the rate. If none appear, measure E's spread first: the medians may leave the shy unexcluded.
3. **b, home.** If brawlers pass 1.2 x, let home hurts move only toward withdrawal (fallback).
4. **g.** Sweep `CoerceStep` {0.05, 0.2, 0.5}; `PatienceDrop` {0.25, 0.5}.
5. **m, missing.** Sweep `MissDays` {7, 10, 14} x `MissSecure` {0.5, 0.8} against the three-year drift.

---

## 13. Deferred, with reasons

- **0e's set point** (research 3c): a season as a hermit lowering boldness for good. Sid's answer 6 (more likely boldness than chattiness) is recorded for 0e; 0d.6 keeps stance as the fast state (design 12.7).
- **The held reservoir R, closeness cost, displacement, rupture, caring masking** (`masking-research.md` M2-M7): stance already is a fading held state; the rest needs the act tables of 0d's JSON and is better measured after X10 alone.
- **M1's threat and kin terms**: need power over others (design 6a), which does not exist yet.
- **A gift that fits the person very well**: needs gift tastes.
- **Constant gifts counting against the giver** (Sid: "possibly"): built only if adaptation alone does not hold year 3.
- **Contagion past direct ties**: emerges on its own (Sid's answer 3; the fact-check advised against modelling Framingham's three degrees).
- **Diathesis-stress as a switch** (research 5): one more switch with no criterion to choose by yet.

---

## 14. Risks

- **Contagion drags everyone toward neutral with `ShowOn`.** Shown is at most the mood, so maskers pass a fainter state. The cast's mean expression (about 0.75) keeps it small; measured at step h.
- **W1's loop runs away.** Its gain is below 1 by construction (fading, the buffer, the included side), and step d's upper bound (1.0 a seed-year) catches it.
- **Households at war.** X2 lets families feud; family feuds are counted apart from E1, and reported.
- **Too many switches.** Each has an E2 row in the report; the ones that move nothing are Sid's to cut.

---

## 15. Questions for Sid

1. Which steps to turn on in the town (each re-pins P3).
2. Pam and the trailer: give Pam arguments in her acts? Without them, X2 cannot reach the trailer.
3. Contagion: pass on mood only (the note), or mood plus hardship (so a short purse brings a household down)?
4. Mishaps (a stumble, a collapse): count them as hurts for the shy?
5. Expression for the cast: the game-data guess, or his own values?
6. The tone: on, now that it reads through expression?
7. Missing people: on, replacing Fond's 14 days?

---

## 16. Docs in the same PR

- `sim/README.md`: a 0d.6 section with each step, its gate and its measurements, like 0d.5's.
- `docs/under-glass/design.md`: "As built" under rules 10 and 18 and section 12; Sid's answers, when he gives them, in section 12.
- `docs/under-glass/withdrawal-research.md`: a note at the top that `b6a87fe3b8fc0916` is not P3 (P3 is `e9fd83b284f5c1b6`; the note quoted the pin of Fond at 10 days).
- This spec, with its departures recorded where the code differs.
