# 19. Dating, marriage and spouses

**Status: not started.** Sid, 2026-09-30: "I would really like this to play with the spouses and
dating mechanics well." Until now the spec left spouses out of most live behavior. This file says
how every system treats a partner, working with the game's own romance mechanics rather than
around them.

## What the game already does (1.6.15 decompile)

- `Friendship.Status` is `Friendly`, `Dating`, `Engaged`, `Married` or `Divorced`; `Friendship` also
  has `WeddingDate`, `DaysMarried`, `RoommateMarriage` (Krobus) and `IsDating()`/`IsMarried()`/...
- **Spouse gift jealousy is vanilla:** giving a gift to someone while married can cost the spouse's
  friendship (`Data/Characters` `SpouseGiftJealousy`, default -30 points, with a
  `SpouseGiftJealous` line). The mod must not add a second penalty for the same act.
- Spouses talk through **marriage dialogue**: on talk, queued `currentMarriageDialogue` entries are
  pushed onto the dialogue stack ahead of anything else. Married NPCs use `marriage_*` schedules.
- Dating several people at once has vanilla consequences at high hearts (a group event).

## Player-visible behavior

- **Partners miss you more.** Dating, engaged and married characters feel `MissingYou` and `Worried`
  more strongly (below), so they write, wave or come looking more readily than friends.
- **Jealousy, in memory and words.** A dating or engaged partner who *learns* (sees it, or hears it
  through gossip, [ledger-gossip.md](ledger-gossip.md)) that the player gave a gift to, or spent a
  long time with, someone else they are dating gets a `Jealous` motive: cooler lines, a pointed
  letter. It feeds the grudge at half weight ([motives.md](motives.md)). For a **spouse**, a gift
  vanilla already punished adds nothing more.
- **Milestones remembered.** Starting to date, the engagement, the wedding and each anniversary become
  diary entries, so lines can refer to them ("A year ago today..."). An anniversary gets a planned line
  by default.
- **Date invitations.** Partners' invitations ([invitations.md](invitations.md)) can be dates: the
  beach at sunset, the Saloon on a Friday. Standing a partner up hurts more than standing up a
  friend.
- **Spouse at home.** A spouse doesn't cross the map looking for the player (their day is the farm
  and `marriage_*` schedules). Instead: a late-night worried line when the player comes home after
  midnight, a note by letter next morning, or a queued "where were you?" line.
- **Divorce** makes the ex a strongly `Hurt`, non-approaching character; their lines are cold.
- **Roommate (Krobus)** is treated like a spouse for missing and worrying, never for jealousy.

## Data model

- New `Motive` value `Jealous = 7` (appended; [motives.md](motives.md)).
- New diary kinds ([diary.md](diary.md)): `StartedDating`, `Engaged`, `Married`, `Divorced`,
  `Anniversary` (subject Player; news weight 5), written when a daily poll of
  `friendshipData[npc].Status` sees a change, or on the wedding date's anniversary.
- Relationship status is a new field on the NPC card sent to Laya ([laya.md](laya.md)): "dating the
  player", "married to the player".

## Triggers and game hooks

| When | What |
|---|---|
| 6:00 tick | poll each NPC's `Friendship.Status`; write milestone entries on change; anniversary check against `WeddingDate` |
| `SawGift`, `Heard` about a gift, or 12+ co-located ticks between the player and another partner | `Jealous` source entries for each *other* partner who knows (their own memory only) |
| spouse, player not home by 00:00 (the spouse's own ledger: no sighting since evening) | `Worried` rises; a queued line or a letter next morning |
| live lines for a spouse | pushed with `setNewDialogue(dialogue, add: true)`; marriage dialogue is pushed on talk ahead of ours, so ours shows after it (check in-game that it isn't lost) |

## Laya questions

No new question types. The relationship status in the card changes the answers of the existing
ones (which motive to act on, whether to hold a grudge, whether to visit).

## Deterministic rules

- Motive multipliers: `MissingYou` and `Worried` x1.5 dating/engaged, x2 married; `Curious` 0 for
  partners.
- `Jealous` sources count only other partners (dating/engaged status with the player), never mere
  friends; strength per source 0.5 x 0.85^days; skipped for a spouse when vanilla gift jealousy
  already applied to the same gift (same day, same recipient).
- `StoodUp` weight x1.5 for a partner's invitation.
- Spouse: never `Visit` or cross-map travel; ladder rungs allowed are `Emote`, `Bubble`,
  `QueuedLine` and `Mail` (a note).
- Divorced: no approach rungs at all; `Hurt` has a floor of 0.5 for 28 days.

## Tuning constants

The multipliers above, `JealousCoTicks` 12, `JealousDecay` 0.85, `DivorceHurtFloor` 0.5 for 28 days.
Not saved.

## Acceptance tests

- Status polling writes each milestone once; anniversaries on the right day in later years.
- Jealousy: a gift to another partner seen by partner A gives A a `Jealous` source; the same gift for
  a spouse with vanilla jealousy applied gives none; mere friends never get jealous.
- Spouse never gets `Visit`; divorced NPC never approaches.
- In-game (a save with a spouse, and one dating two NPCs): the lines and logs match the above; vanilla
  marriage dialogue still shows.

## Status

Not started. Depends on motives, gossip of events, and invitations.

## Open questions

- Should the player's children (the `Child` class) ever take part (e.g. missing a parent)? Not
  planned; they aren't villagers.
- Does a polyamorous save (dating many, as vanilla allows until the group event) need softer
  jealousy? Recommendation: follow vanilla; after the group event, jealousy halves.
