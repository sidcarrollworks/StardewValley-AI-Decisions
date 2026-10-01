# 16. Motives: why a character wants to see the player

**Status: designed, not built.** Redesigned with Sid on 2026-10-01. Today a character has one
number, the ladder's urge ([ladder.md](ladder.md)): how much it wants the player's attention,
never why. The spring 16-18 playtest showed what that costs: urge climbs in a straight line for
everyone, so by evening a dozen villagers want the player for no reason, and the only thing
limiting attempts is the daily cap. This spec replaces the urge clock with **diary-driven
motives** — every action and line has a real reason — and adds the social physics around them:
a temperament resting level, a daily mood roll, elastic and plastic stresses, and the same engine
pointed at other NPCs. Hurt and annoyed are real (Sid's 2026-09-30 decision) and can cost
friendship when the player keeps treating them badly.

The design language comes from Sid (2026-10-01): *"it shouldn't accrue over the day for no reason
and should be driven by their diary and experience... naturally some people would seek you out at
a different rate... NPCs should approach you when they need something, need something done, miss
you."* And the physics metaphor he chose: events are **stresses** on a character's mood that sit
on a spectrum from **elastic** (springs back to resting on its own) to **plastic** (leaves a
lasting deformation).

## The model in one picture

```
urge = resting(temperament) + mood roll(today) + sum of motive stresses
       ^ springs back toward resting as motives decay
       ^ mood roll is bounded, deterministic, and dampened by what the player earned

every diary kind is a stressor profile:  magnitude  x  decay  +  plasticity
elastic:  proximity, small talk, a brush-off            (fast decay, no dent)
plastic:  a grudge, being stood up, repeated neglect    (slow decay, accumulates)
yield:    repeated elastic strains past a threshold become plastic
```

Nothing in this picture is a new saved number. Motives are a **view over the diary** (like the
ledger view is over the ledger): each tick recomputes today's motive sum from recent entries and
their stressor profiles. Only the grudge, which is history, is stored (in the ladder's state).

## Player-visible behavior

- Characters reach out for different reasons, and it shows. The same forgotten birthday makes a
  shy character write a sad note and a rude one go cold; a loved gift makes an outgoing character
  come and say thanks.
- Lines match the motive: a grateful character and a hurt one talk differently about the same event
  ([text.md](text.md) buckets gain a motive variant).
- **Idle characters are quiet.** No reason, no growth: an NPC who hasn't seen or heard anything
  sits at its resting level. Characters with reasons get there fast; Linus never tops the urge
  table just by existing (the playtest's complaint).
- **Personality shows in the resting state.** At rest, some villagers are more likely than others
  to say hi when the player is near (high resting), and some (Linus) take real events to move.
- **Hurt and annoyed are real.** A character the player keeps ignoring, standing up or giving hated
  gifts gets short with them, stops reaching out for a while, and (only with the switch on, below)
  loses friendship points. A kind act (a liked gift, keeping an invitation, a talk) starts to mend it.
- Shadow: `[shadow] Shane: motives hurt 0.62 (stood up yesterday; ignored twice), missing you 0.20`
  and `[shadow] Shane woke up on the wrong side of the bed (-0.12)` and
  `[shadow] Shane would lose 20 friendship (grudge 0.81: stood up, hated gift, ignored)`.

## Data model

`Motive` enum, saved as an int, append only: `MissingYou = 0`, `News = 1`, `Grateful = 2`, `Hurt = 3`,
`Curious = 4`, `Worried = 5`, `WantsToTrade = 6` ([trades.md](trades.md)), `Jealous = 7`
([romance.md](romance.md)). Motives have **subjects**: the player, or another NPC (opinions, below).

Per NPC (in the ladder's saved state, defaults for old saves):
- `Grudge` (0..1): the accumulated hurt that friendship loss is based on; saved, decays daily.
- `LastFriendshipPenaltyDay`: for the cap.

Motive strengths are **not** saved: they are recomputed from memory (diary, ledger, hearts) each
tick, like ledger decay. Only the grudge, which is history, is stored.

### Stressor profiles

Each diary kind ([diary.md](diary.md)) gets a profile in `MotiveOptions.Stressors` (code defaults,
not saved):

| Profile field | Meaning |
|---|---|
| `Magnitude` | how hard the event hits, 0..1 |
| `Decay` | per-day multiplier on the stress it leaves (`0.8^d` style) |
| `Plastic` | the share of the stress that deforms permanently instead of springing back |

**Elastic vs plastic.** An elastic stress (seeing the player 8 tiles away, small talk, a casual
brush-off) raises the urge now and decays fast — by the next hour it is gone. A plastic stress
(a grudge event) decays slowly and its remainder accumulates into `Grudge`. The **yield point** is
what turns repeated elastic strains plastic: `n` ignored attempts in `m` days (the numbers the
ladder's escalation already uses) cross `YieldThreshold` and leave a dent. The same physics Sid
described: *"seeing you 8 blocks away is an elastic interaction because it doesn't last long."*

### Resting urge, from temperament

Each villager's **resting urge** is derived from its temperament seed
([temperament.md](temperament.md)): `resting = 0.5 + Spread x (composite - 0.5)`, where the
composite is `0.4 x boldness + 0.3 x warmth + 0.3 x chattiness` and `Spread` (0.6) puts the town
between roughly 0.15 and 0.55. The spread is the point: most villagers rest below the ladder's
first threshold (0.30), so at rest some characters naturally emote and say hi while others
(Linus's low warmth and boldness) stay quiet until something real happens. A **flat** 0.5 for
everyone would recreate the urge clock in disguise — everyone already past the emote gate.

Two traits get their final roles here (Sid, 2026-10-01):
- **sensitivity is the amplitude** — it scales how much events shake a character (stressor
  magnitudes up or down, and the mood roll's bounds, below);
- **boldness is the expression** — it scales how much of that internal state reaches behavior:
  the ladder's step thresholds shift down for bold characters and up for shy ones. A
  shy-but-sensitive character swings hard internally and rarely acts on it; a bold one acts on
  small swings.

### The daily mood roll

Each day a character wakes with a mood offset (Sid: *"the chance to wake up on the wrong side of
the bed"*), deterministic from the save seed, the NPC and the date — `Fnv1a("mood", save, npc,
date)` — so it needs no saved state and is reproducible. It is:

- **bounded by sensitivity** (the volatility): `offset in [-V, +V]`, `V = 0.05 + 0.20 x
  sensitivity`. Evelyn barely moves; high-sensitivity characters swing;
- **skewed by emotion biases** ([temperament.md](temperament.md)): a sadness/anger-leaning
  character's roll drifts negative (Shane), a happiness-leaning one positive (Sam);
- **dampened by earned mood**: strong recent plastic state (a grudge above 0.4, or a fresh loved
  gift) pulls the roll toward the mood the player earned, so they cannot wake up furious the day
  after a loved gift;
- **rarely uncharacteristic**: most days land in a narrow middle band, and with `TailChance`
  (1/40) the day is a ±2σ outlier — a bad day for Evelyn, a sunny one for Shane. *"Consistent but
  not boring, characters are allowed to have uncharacteristically bad days, but how often that
  happens is determined by who they are and how you affect them."*

The roll moves the *resting* level, not the motives: events still decide what the character does;
the roll decides how much energy they wake up with. It shows in the NPC card for Laya and in the
shadow log line above.

## How each motive is computed (deterministic, from memory only)

All values 0..1, in `src/NpcInitiation/Motives.cs`, pure. `d` = days since the source entry.
`xTemperament` below means the strength is scaled by the named trait.

| Motive | From | Strength |
|---|---|---|
| `MissingYou` | days since the last `Talked` entry, hearts | `min(1, days / 7) x (0.3 + 0.07 x hearts)` x warmth; 0 below 2 hearts |
| `MissingYou` (near-miss) | the routine belief ([routines.md](routines.md)) says the player is usually here at this hour, and today they are not | an elastic +0.15 for pairs whose belief is unlocked; grows a little each hour they stay absent, drains when they appear |
| `News` | the best newsworthy diary entry from today or yesterday ([diary.md](diary.md)) | `news score / 5`, halved if already planned as an intent line |
| `Grateful` | `GiftReceived` Love/Like, `QuestHelped`, `AcceptedInvite`, trades ([trades.md](trades.md)) | per entry `weight x 0.8^d`, summed, capped at 1 |
| `Hurt` | `IgnoredBy`, `StoodUp`, `BirthdayForgotten`, `PassedBy`, `MissedVisit`, `GiftReceived` Dislike/Hate | per entry `weight x 0.85^d`, summed, capped at 1; each kind act since (a talk, a liked gift) subtracts 0.2 |
| `Curious` | no `Talked` entry ever, but a ledger entry about the player (seen or told) | 0.6 in newcomer week ([newcomer-week.md](newcomer-week.md)), else 0.3 |
| `Worried` | hearts >= 4 and no own sighting and no tip for 3+ days | `min(1, (days - 2) / 5)` x fear bias |
| `Proximity` | the player is within 8 tiles of the NPC **in the NPC's own ledger view** (a fresh `NamedSpot` — the same memory-only fact `IsNear` uses) | +0.15 while co-located, `ProximityDecay` (0.5 per 10 ticks) after; elastic, comes and goes quickly. Sid: *"a character is within 8 squares, this might add temporary urge to walk up and say something"* |
| `WantsToTrade` | an open trade offer ([trades.md](trades.md)) | the offer's want strength |

**Urge.** The ladder's urge keeps its role, caps and thresholds, but there is **no idle gain**:
`gain = sum of motive strengths` (each scaled by sensitivity for magnitude), applied toward
crossing thresholds only while motives are above `MinMotiveForAction` (0.05); otherwise urge
**decays toward resting** (`UrgeReturnRate` 0.1 per tick of the gap). A character with nothing to
feel sits at its resting level. The intent boost is replaced by the `News` motive; the overnight
halving goes away. `Hurt` is special: it **lowers** the urge to approach (the character avoids
you) but raises the chance of a letter, via the Laya question below. Elastic stresses (proximity,
near-miss) move the urge in hours; plastic ones (hurt) in days.

**Grudge.** Each tick `Grudge` moves toward the current `Hurt` strength (at most +0.05 a day), and
decays by 10% each day with no new hurt. Repeated bad acts push it up; one bad day doesn't.
Forgiveness scales the decay.

**Opinions: the same physics about other NPCs.** The motive engine takes a subject; pointing it
at NPCs costs nothing extra and is what makes the town live without the player
([town-life.md](town-life.md), "Opinions and the off-screen town"). An **opinion** is a view over
the diary: what A has witnessed B do (the `Saw` entries about B, the shared `Festival`, a
`ChattedWith`), run through the same stressor profiles, with temperament compatibility
(anger-biases clash, warmth likes warmth) folding in as a small elastic term. No new saved state.
A negative opinion is the same `Hurt` machinery: repeated small strains (snubs, an argument)
accumulate and cross the yield point; bonds decay or recover with time apart. "Sam and Shane
disagree about everyone" falls out for free.

**The player's answers, later.** When live dialogue lands (step 6), the game's response options
become the richest producer of stresses: a positive, neutral or negative answer to "what do you
think of my new shoes?" writes `Praised` / `BrushedOff` / `Criticized` diary events, each with a
stressor profile (elastic for a brush-off, plastic for a cut-down). The physics here is built
first; the dialogue system only adds sources.

## Weather and seasons

Agreed with Sid on 2026-09-30. Weather and season change motives and what characters do about them,
never the facts in memory. Game state read (all confirmed in the 1.6.15 decompile): `Game1.isRaining`,
`isSnowing`, `isLightning`, `Game1.IsRainingHere(location)` / `IsLightningHere(location)`,
`Game1.weatherForTomorrow`, `Game1.currentSeason`. Weather is public knowledge, so reading it is not
a D2 problem; where the *player* is still comes only from the NPC's memory.

| Situation | Effect |
|---|---|
| Storm (lightning) and the NPC's last knowledge puts the player outdoors | `Worried` +0.3 for NPCs with 4+ hearts |
| The NPC's last knowledge puts the player in the mines or Skull Cavern, and no sighting since 18:00 | `Worried` +0.2 per two hours, for 4+ hearts and partners |
| Rain or snow | `Visit` and meet-up chances x0.5; outdoor town-life scenes x0.5 ([town-life.md](town-life.md)); bubbles and lines get weather variants ("Stay dry out there") |
| Storm | no `Visit` or meet-ups at all, except a worried partner's |
| The day before a festival | a `News` source "the festival is tomorrow" for outgoing characters (weight 2), so they bring it up |
| Winter | `MissingYou` grows 25% faster (people stay in; the player is missed more) |
| Green rain (year 1) | a `News` source for everyone that day (weight 3) |

Tests: each row as a unit test over a fake weather record; nothing reads `Game1` inside `src/`, the
mod passes a small `WeatherFacts` record into the tick.

## Laya questions

| Question | Type | State | Fallback |
|---|---|---|---|
| "Which of these would <npc> act on first?" | `choice` over the NPC's motives with strength >= 0.2 (at most 5), phrased from their sources ("the player gave her a sunflower yesterday") | NPC card + motives + today's mood | the strongest (argmax) motive instead of uniform: a safe default |
| "would <npc> hold this against the player?" | `noul` | NPC card + the grudge's source entries | 0.5 |

The chosen motive is passed to the ladder (step choice, via its existing yes/no) and the planner (the
line's motive variant). Temperament matters through the card and the resting level: a shy or negative
character's hurt tends toward letters or silence, an outgoing one's gratitude toward visits.
Per-character seed values (warmth, sensitivity, forgiveness, chattiness, curiosity, boldness)
derived from each villager's dialogue and game traits are specified in
[temperament.md](temperament.md); when wired they scale the factors above per character. The same
spec adds Ekman emotion biases, which set how a motive shows (hurt as anger or as sadness), not
whether it fires. Today's mood roll is a line on the card so the model can weigh a bad day.

## Tuning constants

In `MotiveOptions`, not saved: the formulas' factors above, `GrudgeThreshold` 0.75,
`FriendshipPenalty` 20, `PenaltyCooldownDays` 7, `GrudgeDailyDecay` 0.9, `GrudgeMaxGainPerDay` 0.05,
`MinMotiveForChoice` 0.2, `MinMotiveForAction` 0.05, `UrgeReturnRate` 0.1, `RestingSpread` 0.6,
resting weights (boldness 0.4, warmth 0.3, chattiness 0.3), `ProximityStrength` 0.15,
`ProximityDecay` 0.5, `MoodVolatilityBase` 0.05, `MoodVolatilityPerSensitivity` 0.20, `MoodTailChance`
1/40, `YieldThreshold` and the stressor profile table (per diary kind).

## Acceptance tests

- Each motive from fixture diaries: the right strength, decaying by day; `Hurt` reduced by a later
  kind act; `Curious` only before the first talk.
- **No idle gain**: a diary with nothing recent and a resting level of 0.3 leaves the urge at 0.3
  after a full day of ticks (the old `BaseGainPerTick` behavior must not reappear).
- **Resting level discriminates**: Sam's resting urge > the emote threshold > Linus's; with no
  events, Sam may emote and Linus never does.
- **Elastic vs plastic**: a proximity spike is gone within the hour; a `StoodUp` leaves a dent that
  survives the day; `n` ignored attempts cross the yield point and become grudge; one bad day
  alone never reaches the grudge threshold.
- **Mood roll**: deterministic (same save, npc, date), bounded by sensitivity, skewed by the
  emotion biases in the right direction, dampened so a grudge >= 0.4 or a fresh loved gift
  overrides a bad roll; the tail day fires with the tuned frequency.
- **Opinions**: two fixture diaries about a third NPC give A and B different opinions; a snub
  repeated three times moves B's opinion plastically; a week apart recovers it.
- Urge growth is faster with strong motives and the same caps still hold.
- Grudge: one bad day never reaches the threshold; four bad acts in a week do; decay without new hurt;
  a penalty at most once a week, never below 0 points, only when the draw says yes; shadow never calls
  `changeFriendship` (a fake friendship sink in tests).
- The fallback choice is the strongest motive.
- In-game (switch on, test save): stand up an NPC and ignore them repeatedly for a week; one penalty
  of 20 points, logged, and the NPC's lines turn cool; a loved gift brings the grudge down.

## Status

Designed (Sid, 2026-10-01); not built. Replaces the 2026-09-30 draft, whose `0.5 + sum of motives`
formula kept half the urge clock. Depends on diary enrichment ([diary.md](diary.md)) for most
sources and on [temperament.md](temperament.md) for the resting level, roll and scaling.

## Open questions

- Should a grudge ever show in vanilla ways too, like the NPC refusing a gift or a shorter greeting?
  Recommendation: not in v1; lines and behavior are enough.
- Does any motive need to be visible to the player (a UI)? Recommendation: no; the point is that
  characters feel motivated, not that the player reads numbers.
- The resting composite (boldness 0.4, warmth 0.3, chattiness 0.3) is a first guess; review the
  temperament table's resting values and hand-tune obvious misfits (Linus, Sam, Shane) in
  `temperament-overrides.json`.
- The standalone-game note: the simulation core (memory, diary, stressor physics, deterministic
  rolls) is game-agnostic by design, and this social physics is meant to port to a standalone town
  sim without Stardew's schedule lock. Decisions here should keep that core self-contained.
