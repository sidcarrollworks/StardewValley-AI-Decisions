# 16. Motives: why a character wants to see the player

**Status: not started.** Discussed with Sid on 2026-09-30. Today a character has one number, the
ladder's urge ([ladder.md](ladder.md)): how much it wants the player's attention, never why. This
spec splits it into named motives built from the diary, so every action and line has a real reason,
and lets characters be hurt or annoyed, which (Sid's decision) can cost friendship when the player
keeps treating them badly.

## Player-visible behavior

- Characters reach out for different reasons, and it shows. The same forgotten birthday makes a
  shy character write a sad note and a rude one go cold; a loved gift makes an outgoing character
  come and say thanks.
- Lines match the motive: a grateful character and a hurt one talk differently about the same event
  ([text.md](text.md) buckets gain a motive variant).
- **Hurt and annoyed are real.** A character the player keeps ignoring, standing up or giving hated
  gifts gets short with them, stops reaching out for a while, and (only with the switch on, below)
  loses friendship points. A kind act (a liked gift, keeping an invitation, a talk) starts to mend it.
- Shadow: `[shadow] Shane: motives hurt 0.62 (stood up yesterday; ignored twice), missing you 0.20`
  and `[shadow] Shane would lose 20 friendship (grudge 0.81: stood up, hated gift, ignored)`.

## Data model

`Motive` enum, saved as an int, append only: `MissingYou = 0`, `News = 1`, `Grateful = 2`, `Hurt = 3`,
`Curious = 4`, `Worried = 5`, `WantsToTrade = 6` ([trades.md](trades.md)).

Per NPC (in the ladder's saved state, defaults for old saves):
- `Grudge` (0..1): the accumulated hurt that friendship loss is based on; saved, decays daily.
- `LastFriendshipPenaltyDay`: for the cap.

Motive strengths are **not** saved: they are recomputed from memory (diary, ledger, hearts) each
tick, like ledger decay. Only the grudge, which is history, is stored.

## How each motive is computed (deterministic, from memory only)

All values 0..1, in `src/NpcInitiation/Motives.cs`, pure. `d` = days since the source entry.

| Motive | From | Strength |
|---|---|---|
| `MissingYou` | days since the last `Talked` entry, hearts | `min(1, days / 7) x (0.3 + 0.07 x hearts)`; 0 below 2 hearts |
| `News` | the best newsworthy diary entry from today or yesterday ([diary.md](diary.md)) | `news score / 5`, halved if already planned as an intent line |
| `Grateful` | `GiftReceived` Love/Like, `QuestHelped`, `AcceptedInvite`, trades ([trades.md](trades.md)) | per entry `weight x 0.8^d`, summed, capped at 1 |
| `Hurt` | `IgnoredBy`, `StoodUp`, `BirthdayForgotten`, `PassedBy`, `MissedVisit`, `GiftReceived` Dislike/Hate | per entry `weight x 0.85^d`, summed, capped at 1; each kind act since (a talk, a liked gift) subtracts 0.2 |
| `Curious` | no `Talked` entry ever, but a ledger entry about the player (seen or told) | 0.6 in newcomer week ([newcomer-week.md](newcomer-week.md)), else 0.3 |
| `Worried` | hearts >= 4 and no own sighting and no tip for 3+ days | `min(1, (days - 2) / 5)` |
| `WantsToTrade` | an open trade offer ([trades.md](trades.md)) | the offer's want strength |

**Urge.** The ladder's urge keeps its role, caps and thresholds, but its growth per tick becomes the
weighted sum of motives instead of a flat rate: `gain = BaseGainPerTick x (0.5 + sum of motive
strengths)`. A character with nothing to feel grows slowly; one with strong reasons gets there
fast. The intent boost is replaced by the `News` motive. `Hurt` is special: it **lowers** the urge
to approach (the character avoids you) but raises the chance of a letter, via the Laya question
below.

**Grudge.** Each tick `Grudge` moves toward the current `Hurt` strength (at most +0.05 a day), and
decays by 10% each day with no new hurt. Repeated bad acts push it up; one bad day doesn't.

**Friendship loss (Sid, 2026-09-30).** When `Grudge >= GrudgeThreshold` (0.75):
- the character loses `FriendshipPenalty` (20 points, the size of one vanilla conversation's gain)
  with `Game1.player.changeFriendship(-20, npc)` (confirmed in the 1.6.15 decompile);
- at most once per `PenaltyCooldownDays` (7) per character, and never below 0 points;
- then `Grudge` drops by 0.3, so it takes more bad acts to repeat;
- a diary line `HeldAGrudge` (not shared in gossip, never cited in a line directly);
- only when the Laya question "would <npc> hold this against the player?" draws yes (forgiving
  characters mostly don't).
This is the one place the mod changes friendship. It has its own live switch, `Live.FriendshipEffects`,
default off ([rollout.md](rollout.md)); in shadow it only logs what would happen.

## Laya questions

| Question | Type | State | Fallback |
|---|---|---|---|
| "Which of these would <npc> act on first?" | `choice` over the NPC's motives with strength >= 0.2 (at most 5), phrased from their sources ("the player gave her a sunflower yesterday") | NPC card + motives | the strongest (argmax) motive instead of uniform: a safe default |
| "would <npc> hold this against the player?" | `noul` | NPC card + the grudge's source entries | 0.5 |

The chosen motive is passed to the ladder (step choice, via its existing yes/no) and the planner (the
line's motive variant). Temperament matters through the card: a shy or negative character's hurt
tends toward letters or silence, an outgoing one's gratitude toward visits.

## Tuning constants

In `MotiveOptions`, not saved: the formulas' factors above, `GrudgeThreshold` 0.75,
`FriendshipPenalty` 20, `PenaltyCooldownDays` 7, `GrudgeDailyDecay` 0.9, `GrudgeMaxGainPerDay` 0.05,
`MinMotiveForChoice` 0.2.

## Acceptance tests

- Each motive from fixture diaries: the right strength, decaying by day; `Hurt` reduced by a later
  kind act; `Curious` only before the first talk.
- Urge growth is faster with strong motives and the same caps still hold.
- Grudge: one bad day never reaches the threshold; four bad acts in a week do; decay without new hurt;
  a penalty at most once a week, never below 0 points, only when the draw says yes; shadow never calls
  `changeFriendship` (a fake friendship sink in tests).
- The fallback choice is the strongest motive.
- In-game (switch on, test save): stand up an NPC and ignore them repeatedly for a week; one penalty
  of 20 points, logged, and the NPC's lines turn cool; a loved gift brings the grudge down.

## Status

Not started. Depends on diary enrichment ([diary.md](diary.md)) for most sources.

## Open questions

- Should a grudge ever show in vanilla ways too, like the NPC refusing a gift or a shorter greeting?
  Recommendation: not in v1; lines and behavior are enough.
- Does any motive need to be visible to the player (a UI)? Recommendation: no; the point is that
  characters feel motivated, not that the player reads numbers.
