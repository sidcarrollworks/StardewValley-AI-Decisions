# 16. Motives: why a character acts, and whether it dares

**Status: designed, not built.** Redesigned twice with Sid on 2026-10-01; the second pass retires
the urge number as the thing that decides. Today a character has one number, the ladder's urge
([ladder.md](ladder.md)), which grows on a clock and fires a rung when it crosses a threshold. The
spring 16-18 playtest showed what that costs: by evening a dozen villagers want the player for no
reason, and only the daily cap holds them back. This spec replaces it with **a motive and the
boldness to act on it**: every act has a real reason, and personality decides who dares. Hurt and
annoyed are real (Sid's 2026-09-30 decision) and can cost friendship when the player keeps treating
a character badly. D24 records the decision.

Sid, 2026-10-01: *"You have a character's emotions and tendencies. For a character to act they
need a motive and a certain level of boldness. The boldness required to do the act also depends on
the relationship with the subject. If I see someone I already know, it's easier for me to say hi.
The required boldness can also be offset by the intensity of the emotion within context. Imagine
I've just offended you greatly or two characters fall in love and one kisses the other."* The
physics metaphor stays from the first pass: events are **stresses** on a character, from
**elastic** (springs back on its own) to **plastic** (leaves a lasting mark).

## The model in one picture

```
tendencies  (fixed)    temperament traits and emotion biases      temperament.md
emotions    (moving)   elastic + plastic stresses from the diary, per subject
regard      (saved)    the plastic part: one signed number per (observer, subject)
mood        (today)    earned from recent emotions, plus a small daily roll
motives     (why)      what the character wants to do about a subject
act         (whether)  boldness + familiarity + intensity  >=  cost of the act
```

A character acts only when both hold:
1. it has a **motive** with a subject (no motive, no act: Sid, 2026-10-01);
2. its **effective boldness** toward that subject reaches the act's **cost**.

## Player-visible behavior

- **No reason, no act.** A villager who hasn't seen, heard or felt anything stays quiet, however
  bold. Linus never tops anything by existing.
- **Shy characters speak to people they know, or when it matters.** Shane stays quiet with a
  stranger, says hi once he knows the player, and confronts them (or, more likely, writes) when
  badly hurt. Bold characters greet acquaintances readily.
- **Strong feelings override shyness.** Being badly offended, or falling in love, carries a
  shy character past what its temperament allows on an ordinary day.
- **Hostile acts take much more.** Snapping at someone needs far more than saying hi, even to
  someone the character knows well.
- **Mixed feelings net out.** A character who is grateful for a gift and hurt about a missed
  invitation feels the difference, and the day's mood tips which way it leans.
- **Characters remember differently.** Pam lets most slights go by the next week; Robin doesn't.
  Only a truly bad act stays with everyone.
- **Lines match the motive**: a grateful character and a hurt one talk differently about the same
  event ([text.md](text.md) buckets gain a motive variant).
- **Hurt and annoyed are real.** A character the player keeps ignoring, standing up or giving hated
  gifts gets short with them, stops reaching out for a while, and (only with the switch on, below)
  loses friendship points. A kind act (a liked gift, keeping an invitation, a talk) starts to mend it.
- Shadow:
  - `[shadow] Shane: motive Hurt 0.62 toward you (stood up yesterday; ignored twice); hostile letter: boldness 0.16 + familiarity 0.05 + intensity 0.31 = 0.52 vs cost 0.55, close call; Laya 0.48, outlook -0.18 tilts it to 0.50: writes`
  - `[shadow] Pam: motive Greeting toward you; Emote: 0.57 + 0.12 + 0.08 = 0.77 vs cost 0.20, clear yes`
  - `[shadow] Shane: outlook -0.18 today (rough morning; stood up yesterday)`
  - `[shadow] Shane would lose 20 friendship (grudge 0.81: stood up, hated gift, ignored)`

## Data model

### Motives

`Motive` enum, saved as an int, append only: `MissingYou = 0`, `News = 1`, `Grateful = 2`,
`Hurt = 3`, `Curious = 4`, `Worried = 5`, `WantsToTrade = 6` ([trades.md](trades.md)),
`Jealous = 7` ([romance.md](romance.md)), `Greeting = 8`, `NeedsHelp = 9`. Every motive has a **subject**: the
player or another NPC ([town-life.md](town-life.md)).

Motives come in two families:

| Family | Motives | Valence | How they combine |
|---|---|---|---|
| **Feelings** about the subject | `Grateful`, `MissingYou`, `Greeting`, `Curious` (+); `Hurt`, `Jealous` (-) | signed | **net** into one feeling per subject (below) |
| **Tasks** with the subject | `News`, `Worried`, `WantsToTrade`, `NeedsHelp` | none | each stands alone; the net feeling sets its tone |

### Stressor profiles

Each diary kind ([diary.md](diary.md)) has a profile in `MotiveOptions.Stressors` (code defaults,
not saved). One table holds both the emotional effect and the gossip value
([ledger-gossip.md](ledger-gossip.md)), so a new kind is added in one place.

| Field | Meaning |
|---|---|
| `Emotion` | the Ekman emotion it mostly stirs (picks the line tone, [temperament.md](temperament.md)) |
| `Valence` | +1 or -1 toward the subject |
| `Magnitude` | how hard it hits, 0..1, before sensitivity |
| `ElasticDecay` | per-day multiplier on the elastic part |
| `Plastic` | the share that becomes a lasting mark (into regard, below) |
| `Juiciness` | how gossip-worthy it is ([ledger-gossip.md](ledger-gossip.md)) |

First-guess defaults (tune from playtest logs):

| Kind | Emotion | Valence | Magnitude | ElasticDecay | Plastic | Juiciness |
|---|---|---|---|---|---|---|
| `GiftReceived` Love | happiness | + | 0.6 | 0.8 | 0.3 | 2 |
| `GiftReceived` Like | happiness | + | 0.3 | 0.7 | 0.1 | 1 |
| `GiftReceived` Dislike | anger | - | 0.3 | 0.7 | 0.1 | 2 |
| `GiftReceived` Hate | disgust | - | 0.6 | 0.8 | 0.3 | 3 |
| `QuestHelped` | happiness | + | 0.5 | 0.8 | 0.3 | 2 |
| `AcceptedInvite` | happiness | + | 0.4 | 0.8 | 0.2 | 1 |
| `Talked` | happiness | + | 0.1 | 0.5 | 0.02 | 0 |
| `StoodUp` | sadness | - | 0.7 | 0.85 | 0.5 | 3 |
| `BirthdayForgotten` | sadness | - | 0.6 | 0.85 | 0.4 | 2 |
| `MissedVisit` | sadness | - | 0.4 | 0.8 | 0.2 | 1 |
| `IgnoredBy` | anger | - | 0.2 | 0.5 | 0 (yields) | 1 |
| `PassedBy` | sadness | - | 0.15 | 0.5 | 0 (yields) | 0 |
| `SawGift` (giver is someone the observer is drawn to) | sadness | - | 0.3 | 0.7 | 0 (until confirmed) | see gossip |
| `SawRummaging` (planned kind, below) | disgust | - | 0.3 | 0.8 | 0.1 | 4 |
| `Praised` (the player's answer to a vanilla question, [vanilla-sources.md](vanilla-sources.md)) | happiness | + | 0.3 | 0.7 | 0.1 | 1 |
| `BrushedOff` | sadness | - | 0.15 | 0.5 | 0 (yields) | 0 |
| `Criticized` | anger | - | 0.4 | 0.8 | 0.3 | 2 |
| `HeartEvent` (the player saw the NPC's heart event) | happiness | + | 0.6 | 0.9 | 0.5 | 3 if another villager was in it, else 1 |
| `DanceAsked` accepted / declined (Flower Dance) | happiness / sadness | + / - | 0.5 / 0.4 | 0.85 | 0.3 / 0.2 | 3 |
| `MovieTogether` loved / disliked the film | happiness / disgust | + / - | 0.4 / 0.15 | 0.8 | 0.2 / 0 | 2 |
| `FarmVisited` ([invitations.md](invitations.md)) | happiness | + | 0.4 | 0.8 | 0.2 | 1 |
| `StoodUp` at the farm, `seen=1` (seen but ignored) | anger | - | 0.85 | 0.85 | 0.6 | 3 |
| `TownNews` (a vanilla conversation topic) | surprise | none | 0 | - | 0 | 2 |
| `Argued` (NPC to NPC, [town-life.md](town-life.md)) | anger | - | 0.4 | 0.8 | 0.3 | 3 |
| `ChattedWith` | happiness | + | 0.05 | 0.5 | 0.02 | 0 |
| `Heard` | the original's | the original's | original x `HearsayFactor` 0.5 | 0.6 | 0 until confirmed | the original's, faded |

Sensitivity scales every magnitude: `magnitude x (0.5 + sensitivity)`, so a typical villager
(0.5) feels the table value, Leo (0.79) about 1.3 times it, Robin (0.26) about three quarters.

**Elastic part.** Recomputed every tick from the recent diary: each entry contributes
`magnitude x ElasticDecay^d` (`d` in days, fractional). `Greeting` is not a diary stress: it
comes from the NPC's ledger view and is gone within the hour (below). The elastic part needs only the last `ElasticWindowDays` (3) of diary, which stays well inside the 500-entry
cap (to confirm with the playtest log, [debug-tools.md](debug-tools.md)).

**Yield point.** Kinds with `Plastic` 0 (yields) leave no mark once, but `YieldCount` (3) of the
same kind about the same subject within `YieldWindowDays` (5) leave a mark of `YieldPlastic` (0.3)
on the third and every later one. Three ignored attempts in a week dent a relationship; one
doesn't.

### Regard: the plastic part, saved per pair

Sid agreed on 2026-10-01 to split memory: the elastic part is a view over the recent diary, the
plastic part is saved, because the diary is trimmed to 500 entries per NPC and a grudge must not
vanish with the entries that caused it.

- `Regard(observer, subject)`: signed, -1..1, sparse (only pairs that have one), saved in
  `MemoryStore` with the subject `Player` or an NPC name. It replaces two planned numbers: the
  player grudge (the negative part of `Regard(npc, Player)` is the grudge) and town-life's
  `NpcBond` (seeded from `FriendsAndFamily`, [town-life.md](town-life.md)).
- When a stress lands, its plastic share moves regard:
  `regard += valence x magnitude x Plastic x RetentionFactor(observer)`, clamped.
- **Retention depends on the person** (Sid, 2026-10-01: *"Pam for example is a drunk so unless the
  grudge is terrible she will most likely not remember the same level of bad as say Robin"*).
  `RetentionFactor = 0.5 + retention`, from a per-character `retention` (0..1, 0.5 typical;
  [temperament.md](temperament.md)). Pam at 0.2 keeps 70% of what a typical villager keeps. A
  **severe** stress (magnitude at or above `SevereMagnitude` 0.7 after sensitivity) ignores
  retention: everyone remembers being stood up on their birthday.
- Daily drift at the 6:00 tick: negative regard moves toward 0 by `forgiveness`-scaled
  `RegardHealRate` (0.03 x (0.5 + forgiveness)); positive regard fades much slower
  (`RegardFadeRate` 0.005) with time apart. A kind act heals by its own plastic share, as above.
- Save format: a new additive `regard` key ([persistence.md](persistence.md)), so no version
  bump: a missing key loads empty, and NPC pairs are seeded once from `FriendsAndFamily`
  ([town-life.md](town-life.md)) with a `seeded` flag. `LastFriendshipPenaltyDay` stays in the
  ladder's state. The ladder's saved `Urge` and `IntentBoostDay` stay in its format, unused, so old
  saves load and older builds can still read new saves.

### Mood: earned, with a small daily tilt

Sid, 2026-10-01: *"The mood roll should be relatively small and mood should largely be based on
recent events. If a yes/no decision was given to Laya and the scores came out 50/50, your day's
mood roll would push it over either way. Is this character optimistic or pessimistic today."*

- **Earned mood**, -1..1: the valence-weighted sum of the character's elastic stresses about
  everyone, clamped. A loved gift yesterday and a nice chat this morning make a good day.
- **Daily roll**, -1..1, deterministic: `Fnv1a("mood", seed, npc, absolute day index)` (the
  ladder's per-save seed; an absolute day index, never season and day, so years don't repeat).
  A triangular draw (two uniforms averaged and centred), shifted by
  `RollSkew x (happiness - (anger + sadness) / 2)` from the emotion biases. With `TailChance`
  (1/40) the day is uncharacteristic: the roll is set against the character's lean, at 0.8 to 1.0.
- **Outlook** = `clamp(earned + RollWeight x (0.5 + sensitivity) x roll, -1, 1)`, `RollWeight`
  0.3. The roll alone moves a typical villager's outlook by at most 0.3; a strong recent event
  outweighs it.
- Outlook does three things, and only these:
  1. **Sways netting** (below): `MoodSway` (0.15) x outlook is added to each subject's net feeling.
  2. **Tips close calls**: a Laya answer is shifted by `MoodTilt` (0.10) x outlook toward
     friendly acts on a good day and toward hostile ones on a bad day (below). A 50/50 answer
     therefore always lands one way or the other.
  3. **Shows**: a line on the NPC card and in the shadow log. On a bad day hearsay reads worse,
     in tone only.

## How each motive is computed (deterministic, from memory only)

All values 0..1, pure, in `src/NpcInitiation/Motives.cs`. `d` = days since the source entry.

| Motive | From | Strength |
|---|---|---|
| `MissingYou` | days since the last `Talked` entry, hearts | `min(1, days / 7) x (0.3 + 0.07 x hearts) x (0.5 + warmth)`; 0 below 2 hearts |
| `Greeting` | the subject is near **in the NPC's own ledger view** (a fresh `NamedSpot`, the memory-only fact `IsNear` uses), and is familiar (2+ hearts, or regard >= 0.2) | `0.15 x (0.5 + warmth)` while near, gone within the hour after; once per subject per day. Sid: *"a character is within 8 squares, this might add temporary urge to walk up and say something"* |
| `News` | the best newsworthy diary entry from today or yesterday ([diary.md](diary.md)) | `news score / 5 x (0.5 + chattiness)` |
| `Grateful` | the elastic part of `GiftReceived` Love/Like, `QuestHelped`, `AcceptedInvite`, trades | summed, capped at 1 |
| `Hurt` | the elastic part of `IgnoredBy`, `StoodUp`, `BirthdayForgotten`, `PassedBy`, `MissedVisit`, `GiftReceived` Dislike/Hate, plus `max(0, -regard)` | summed, capped at 1 |
| `Curious` | no `Talked` entry ever, but a ledger entry about the player (seen or told) | `(0.6 in newcomer week, else 0.3) x (0.5 + curiosity)` ([newcomer-week.md](newcomer-week.md)) |
| `Worried` | hearts >= 4 and no own sighting and no tip for 3+ days | `min(1, (days - 2) / 5) x (0.5 + fear)` |
| `Jealous` | `Heard`/`SawGift` of the subject giving a Love gift to someone else, when the observer is drawn to the giver ([romance.md](romance.md)) | the elastic part; plastic only when confirmed ([ledger-gossip.md](ledger-gossip.md)) |
| `WantsToTrade` | an open trade offer ([trades.md](trades.md)) | the offer's want strength |
| `NeedsHelp` | something the NPC wants done or brought, from its own data: a loved or liked item it hasn't had in a while (`Data/NPCGiftTastes`), its work (Willy's fish, Robin's wood), a vanilla request it already posts ([vanilla-sources.md](vanilla-sources.md)) | `0.3 + 0.3 x (days since last asked / 14)`, capped at 0.6; at most one open request per NPC; expressed as a quest ([invitations.md](invitations.md), "Later") |

`MissingYou` (near-miss), from the first pass, is kept as a source of `MissingYou`: when the NPC's
routine belief ([routines.md](routines.md)) says the player is usually here at this hour and today
they aren't, `MissingYou` gets an elastic +0.15 that drains when they appear.

### Netting feelings, swayed by mood

Sid, 2026-10-01: several motives toward one subject **net**, swayed by the day's mood.

```
net(subject) = sum of positive feelings - sum of negative feelings + MoodSway x outlook
```

- The sign picks the **valence** of what the character does: friendly or hostile.
- `|net|` is the feeling's **intensity**.
- Grateful 0.4 and Hurt 0.3 net to a mild +0.1 (friendly, low intensity), and a bad day can turn
  that to a slightly cool greeting.
- Task motives don't net: each one keeps its own intensity, and the net feeling sets its tone and
  route. When `net <= -AvoidLevel` (-0.3), in-person friendly acts are off for that subject: a hurt
  character with news writes or queues a line instead of walking up.

### Using a motive up

A motive stops once it is satisfied, or the character repeats itself after every cooldown:

| Motive | Satisfied by |
|---|---|
| `MissingYou`, `Greeting`, `Curious` | a `Talked` entry, or a responded attempt; `Greeting` also by any answered emote or bubble that day |
| `News` | delivery of that news (`SharedNews` diary kind, written when the line is said or the letter read; [intents.md](intents.md)) |
| `Grateful` | a delivered thanks (`SharedNews` with `motive=Grateful`); the warm feeling stays in regard |
| `Hurt`, `Jealous` | never by acting; acting on them **vents**: the elastic part drops by `VentRelief` (0.5), regard is unchanged |
| `Worried` | a fresh sighting or tip |
| `WantsToTrade` | the offer taken or expired |

## The act rule

### Acts and their cost

The ladder's steps become **acts** with a cost. Its `StepThresholds` become `ActCost` (first
guesses):

| Act | Ladder step | Cost (friendly) | Allowed for |
|---|---|---|---|
| Emote | `Emote` | 0.20 | any feeling, `Greeting` |
| Bubble | `Bubble` | 0.35 | any feeling, `News`, `Worried` |
| Walk up | `Approach` | 0.50 | any but `Greeting` |
| Queued line | `QueuedLine` | 0.30 | any |
| Letter | `Mail` | 0.25 | any but `Greeting`, `Curious` |
| Interrupt | `ForcedDialogue` | 0.80 | `Hurt`, `Worried`, `News` with score >= 4 |
| Farm visit by appointment (a letter, then a scheduled walk) | `Mail` + a schedule for that day ([invitations.md](invitations.md)) | 0.45 | `MissingYou`, `Grateful`, `Curious` at 4+ hearts, `News` with score >= 3 |
| Ask for help (a quest) | `Mail` with a quest, or in person with a bubble ([invitations.md](invitations.md)) | 0.30 | `NeedsHelp` |
| Visit (unannounced) | `Visit` ([find.md](find.md)) | 0.70 | `MissingYou`, `Worried`, `Hurt`, `News` with score >= 4 |
| Kiss | romance ([romance.md](romance.md)) | 0.95 | romantic `Grateful`/`MissingYou` at partner status; shadow only until romance goes live |

The **emote** shows the feeling's emotion (ids confirmed in the 1.6.15 decompile): anger 12,
sadness 28, happiness 32, surprise 16, fear 8, and for a warm romantic feeling heart 20 or blush 60
(blush for shy characters). Disgust has no emote and uses anger.

**Hostile acts** (a sharp bubble, a cold letter, a confrontation) cost `HostileSurcharge` (0.30)
more. Avoiding someone is not an act: it costs nothing and shows only as an absence and a shadow
line.

### Effective boldness

```
effective = boldness + familiarity(subject, valence) + IntensityWeight x intensity
```

- `boldness` from the temperament seed. Boldness only appears here, never in a resting level
  (the first pass counted it twice).
- **Familiarity**, toward the player:
  - friendly: `0.03 x hearts` (0.30 at 10 hearts) plus `0.2 x max(0, regard)`;
  - hostile: `0.15 x max(0, -regard)` only. Sid: *"negative should count as familiarity for
    hostile acts but the threshold should be much higher than it is for friendly acts."* The
    surcharge plus the lower weight keeps hostile acts rare.
  - Toward an NPC the same, with `Regard` in place of hearts (`0.3 x max(0, regard)`).
- `IntensityWeight` 0.5; intensity is `|net|` for a feeling, the strength for a task, plus
  frustration (below).

The motive picks its act: **the strongest allowed act it can afford**, among acts available now
(the ladder's existing `Available`: near, seen today, 2+ hearts for letters). Acts are tried from
the most expensive down: the first one that is a clear yes is taken; a close call on a bigger act
goes to Laya first, and if that says no, the character falls back to the biggest clear act, if any.

### Clear calls and close calls

`margin = effective - cost`:
- `margin >= ClearBand` (0.15): act, no model call.
- `margin <= -ClearBand`: don't.
- In between: ask Laya, then apply the mood tilt, then decide (below).

This means most calls need no model call; the model is spent on the close ones.

Examples with the draft temperament table (made-up feelings):

| Who, what | Effective | Cost | Result |
|---|---|---|---|
| Pam (boldness 0.57), `Greeting`, 4 hearts | 0.57 + 0.12 + 0.08 = 0.77 | Bubble 0.35 | clear yes: says hi |
| Shane (0.16), `Curious` about a stranger, 0 hearts | 0.16 + 0 + 0.15 = 0.31 | Bubble 0.35 | close call: Laya and the mood decide |
| Shane, `Greeting`, 8 hearts | 0.16 + 0.24 + 0.05 = 0.45 | Bubble 0.35 | close call; if no, Emote (0.20) is a clear yes |
| Shane, `Hurt` 0.62 (grudge 0.3) | 0.16 + 0.045 + 0.31 = 0.52 | hostile letter 0.55 | close call |
| Pam, same hurt | 0.57 + 0.045 + 0.31 = 0.93 | hostile walk-up 0.80 | close call leaning yes: confronts |

### Frustration

Rung escalation is replaced by frustration. When an attempt goes unanswered:
- bold characters (boldness >= 0.5) push harder: `+FrustrationStep` (0.1) to that motive's intensity
  for the rest of the day, so they can afford a bigger act;
- shy ones back off: `-FrustrationStep`, so they drop to a letter or give up.

It resets at the 6:00 tick. Across days, ignored attempts leave `IgnoredBy` entries, which yield
into regard as above. In shadow `RecordIgnoredBy` is off, so the cross-day part only runs in unit
tests until a rung goes live; the shadow log still reports frustration within the day.

### Which motive goes first

When several motives (feelings toward different subjects, tasks) can act, the existing Laya
`choice` question picks one (fallback: the strongest). The pacing caps still hold: 2 attempts per
NPC a day, 6 in total, 2 queued lines, 1 letter, 1 interrupt a week, 6-tick cooldown. When fewer
than `StrongReserve` (2) of the day's 6 attempts remain, only motives with intensity >=
`StrongIntensity` (0.5) may use them, so cheap greetings can't crowd out a character with a real
reason.

## Grudge and friendship loss

The grudge is the negative part of `Regard(npc, Player)`. Kept from 2026-09-30, unchanged in
behavior:
- When `max(0, -regard) >= GrudgeThreshold` (0.75), the character loses `FriendshipPenalty`
  (20 points) with `Game1.player.changeFriendship(-20, npc)` (confirmed in the 1.6.15 decompile);
- at most once per `PenaltyCooldownDays` (7) per character, never below 0 points;
- then regard moves up by 0.3, so it takes more bad acts to repeat;
- a diary line `HeldAGrudge` (not shared in gossip, never cited directly);
- only when the Laya question "would <npc> hold this against the player?" draws yes.
This is the one place the mod changes friendship. It has its own live switch,
`Live.FriendshipEffects`, default off ([rollout.md](rollout.md)); in shadow it only logs.

## Planned diary kinds this needs

- `SharedNews` (`kind`, `subject`, `motive`): what got said, so a motive is used up. Never shared.
- `SawRummaging`: an NPC co-located with the player when the player searches a garbage can.
  **verify** in the 1.6.15 decompile: the vanilla garbage-can code, the radius in which villagers
  react, which villagers are exempt (Linus, from memory) and the friendship change it already
  applies. Only the reaction's existence and the actors matter here; the mod changes nothing.
- `Praised`, `BrushedOff`, `Criticized`: the player's answers to the questions vanilla dialogue
  already asks (`$q`/`$r`/`$y`), classed by the friendship effect vanilla attaches to each answer
  (positive, zero, negative). This works in shadow mode now, from vanilla dialogue alone
  ([vanilla-sources.md](vanilla-sources.md); the answer hook is being verified). When our own
  dialogue lands (step 6), its questions add more of the same.
- `HeartEvent`, `DanceAsked`, `MovieTogether`, `TownNews`, `FarmVisited`: from the vanilla signals in
  [vanilla-sources.md](vanilla-sources.md) and the farm visits in [invitations.md](invitations.md).

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
| Rain or snow | `Visit` and meet-up costs +0.15; outdoor town-life scenes x0.5 ([town-life.md](town-life.md)); bubbles and lines get weather variants ("Stay dry out there") |
| Storm | no `Visit` or meet-ups at all, except a worried partner's |
| The day before a festival | a `News` source "the festival is tomorrow" for outgoing characters (weight 2), so they bring it up |
| Winter | `MissingYou` grows 25% faster (people stay in; the player is missed more) |
| Green rain (year 1) | a `News` source for everyone that day (weight 3) |

Tests: each row as a unit test over a fake weather record; nothing reads `Game1` inside `src/`, the
mod passes a small `WeatherFacts` record into the tick.

## Triggers and game hooks

- Motives, netting, outlook and the act rule run in the ladder's tick (`BackgroundLadder`, off the
  game thread), from the same copied inputs it gets today plus the regard snapshot and the
  temperament record. Nothing new reads `Game1`.
- Stresses are applied to regard on the game thread when a diary entry is appended (the tick that
  writes it), so `MemoryStore` stays the one writer of saved memory.
- Regard drift and the mood roll's day change happen at the 6:00 tick, which is already the new
  date (AGENTS.md, "The night order").

## Laya questions

| Question | Type | State | Fallback |
|---|---|---|---|
| "Which of these would <npc> act on first?" | `choice` over motives with strength >= 0.2 (at most 5), phrased from their sources ("the player gave her a sunflower yesterday") | NPC card + motives + outlook | the strongest motive |
| "would <npc> <act> toward <subject> now?" (close calls only) | `noul` | NPC card + the motive, its sources, the act, effective boldness and cost | `0.5 + margin / (2 x ClearBand)` |
| "would <npc> hold this against the player?" | `noul` | NPC card + the grudge's source entries | 0.5 |

**Deciding a close call.** `p' = p + MoodTilt x outlook` for a friendly act, `p - MoodTilt x
outlook` for a hostile one; act when `p' >= 0.5`. No random draw: a close call is decided by what
the model thinks and how the character woke up, which is the 50/50 case Sid described. The Laya
question replaces the ladder's current "should <npc> try to get the player's attention with
<step> now?".

Temperament matters through the numbers and the card. Ekman emotion biases set how a feeling
shows (hurt as anger or as sadness, [temperament.md](temperament.md)), not whether it fires.

## Deterministic rules

- Pure functions over copied inputs; NPCs in name order; the mood roll and any tie-breaks from
  `Fnv1a` with the per-save seed.
- No motive, no act. The act rule above is the only way an attempt starts.
- Hostile acts and the kiss are shadow-only until Sid turns their switches on (`HostileActs`,
  `RomanceActs`, [rollout.md](rollout.md)); until then they log
  `[shadow] ... would ...`.
- Avoiding (net <= -0.3) blocks in-person friendly acts for that subject only.
- Spouse, children, events and festivals: as in [ladder.md](ladder.md).

## Tuning constants

In `MotiveOptions`, not saved:
- stressors: the table above, `HearsayFactor` 0.5, `ElasticWindowDays` 3, `YieldCount` 3,
  `YieldWindowDays` 5, `YieldPlastic` 0.3, `SevereMagnitude` 0.7;
- regard: `RegardHealRate` 0.03, `RegardFadeRate` 0.005, friendly familiarity 0.03 per heart and 0.2
  per positive regard, hostile familiarity 0.15;
- mood: `RollWeight` 0.3, `RollSkew` 0.3, `TailChance` 1/40, `MoodSway` 0.15, `MoodTilt` 0.10;
- acts: `ActCost` per act, `HostileSurcharge` 0.30, `IntensityWeight` 0.5, `ClearBand` 0.15,
  `AvoidLevel` 0.3, `FrustrationStep` 0.1, `VentRelief` 0.5, `StrongReserve` 2,
  `StrongIntensity` 0.5, `MinMotiveForChoice` 0.2;
- grudge: `GrudgeThreshold` 0.75, `FriendshipPenalty` 20, `PenaltyCooldownDays` 7.
`InitiationOptions.BaseGainPerTick`, `HeartsGainPerTick`, `IntentBoost`, `OvernightFactor`,
`IgnorePenalty` and `RespondRelief` are removed when this lands.

## Acceptance tests

- **No motive, no act**: a bold NPC (Pam) near the player with an empty diary and 0 hearts never
  attempts anything over a full day of ticks.
- **Familiarity**: Shane with a `Greeting` emotes at 8 hearts and not at 0 hearts; Pam emotes at 2.
- **Intensity overrides shyness**: Shane with a strong `Hurt` reaches a hostile letter; with a mild
  one he doesn't.
- **Hostile is harder**: for the same character, intensity and subject, a hostile act needs at
  least `HostileSurcharge` more effective boldness than the friendly act of the same step.
- **Netting**: Grateful 0.4 and Hurt 0.3 give a friendly act of intensity about 0.1; a strongly
  negative outlook turns it hostile only when the net is within `MoodSway`.
- **Mood**: the roll is deterministic per (seed, npc, day); a 50/50 close call goes friendly on a
  positive outlook and the other way on a negative one; a fresh loved gift outweighs the worst roll;
  tail days occur at about 1 in 40 over a long run.
- **Elastic vs plastic**: a `Greeting` is gone within the hour; a `StoodUp` moves regard; three
  `IgnoredBy` in five days move regard and two don't; one bad day never reaches the grudge
  threshold.
- **Retention**: the same stresses move Pam's regard less than Robin's; a severe one moves both
  equally.
- **Used up**: after a `SharedNews` for a `News` motive, no further attempt for that news.
- **Frustration**: after an ignored attempt a bold NPC can afford the next act up, a shy one only
  a letter or nothing; both reset at 6:00.
- **Caps**: with 2 attempts left in the day, a `Greeting` is refused and a `Hurt` of 0.6 is not.
- **Save**: an old save loads with empty regard and seeds NPC pairs once; regard round-trips.
- Grudge: four bad acts in a week reach the threshold; a penalty at most once a week, never below 0
  points, only when the draw says yes; shadow never calls `changeFriendship` (a fake sink in tests).
- In-game (switch on, test save): stand up an NPC and ignore them repeatedly for a week; one penalty
  of 20 points, logged, and the NPC's lines turn cool; a loved gift starts to mend it.

## Status

Designed (Sid, 2026-10-01, second pass); not built. Replaces the 2026-09-30 draft and the morning
2026-10-01 resting-urge draft: a resting urge from temperament put every villager between 0.35 and
0.64 on the draft table, past the first threshold, which recreated the clock. Depends on diary
enrichment ([diary.md](diary.md)) for most sources and on [temperament.md](temperament.md).

## Open questions

- Every number here is a first guess. The playtest log ([debug-tools.md](debug-tools.md),
  "Playtest log") records each decision's parts, so tune from data, not by feel.
- `retention` has no dialogue signal yet; start with hand values in the overrides (Pam low) and
  0.5 for everyone else? Recommendation: yes.
- Should a close call keep a random draw instead of the 0.5 cut? Recommendation: no; the cut makes
  the mood decisive on true coin-flips, which is the point.
- Should a grudge show in vanilla ways too, like refusing a gift? Recommendation: not in v1.
- Does any motive need to be visible to the player? Recommendation: no.
- The standalone-game note: the simulation core (memory, diary, stresses, regard, deterministic
  rolls) is game-agnostic by design and meant to port to a standalone town sim. Keep it
  self-contained.
