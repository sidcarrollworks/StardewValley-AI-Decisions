# 7. Newcomer week

**Status: not started.** Brief design decision 6. Depends on: mail delivery (the ladder's `Mail`
path, [ladder.md](ladder.md)), ambient gossip ([ledger-gossip.md](ledger-gossip.md)), and, for
visits, NPC travel ([find.md](find.md)).

## Player-visible behavior

During the first week of a new save:
- The town hears about the new farmer before meeting them. NPCs who haven't met the player still
  have a hearsay view ("the new farmer is out on the farm"), so they can mention it.
- A few NPCs (3 to 5, different per save) come to introduce themselves, each on its own day. The
  evening before, a letter says who is coming ("I'll stop by tomorrow morning to say hello. - Robin").
  On the day, the NPC waits near the farmhouse for up to two hours in the morning. Talking to them
  gives a small welcome gift and counts as meeting them.
- Shy NPCs don't visit. They send a short note with a small gift attached instead.
- A visitor the player doesn't talk to leaves at the end of the window; the next day they may say
  "I came by yesterday, but you were busy" (an `IgnoredBy`-style diary entry, kind `MissedVisit`).

In shadow mode, all of it is logged: `[shadow] newcomer week: Robin would visit on spring 3 at
9:00 (letter the evening before); Leah (shy) would send a note with a Salad on spring 4`.

## Data model

`NewcomerPlan` (saved under a new save-data key `newcomer`, [persistence.md](persistence.md)):

```
NewcomerPlan
  Eligible: bool                 decided once, on the first load with the mod
  Visits: [ NewcomerVisit ]
NewcomerVisit(Npc, DayIndex, Kind = Visit | Note, GiftItemId, State = Planned | Lettered | Waiting | Met | Missed | Sent)
```

Gift items come from a data table `data/newcomer.json` (D20: data in tables): per NPC, one or two
small qualified item ids and a quantity, for example Pierre -> parsnip seeds, Willy -> bait,
Caroline -> a cup of tea (item ids verify). NPCs without an entry are never picked. The table is
the **only** place items come from; no template or model output can name an item.

## Triggers and game hooks

| When | What |
|---|---|
| first `SaveLoaded` with no `newcomer` key | decide `Eligible` (see decision 3 in the roadmap): `Game1.stats.DaysPlayed <= 1` (verify) and year 1, spring 1-2. Otherwise `Eligible = false` forever |
| same | build the plan (below), save it with the next save |
| `DayEnding` before a visit day | register and send the letter (`Data/mail` via `AssetRequested`, `mailForTomorrow`: verify) |
| visit day, 9:00 tick | the visitor is placed near the farmhouse door: `Game1.warpCharacter(npc, "Farm", tile)` (verify) and stands, facing the door; ignore schedule for the window; restore it at the window's end (same open problem as [ladder.md](ladder.md)) |
| `MenuChanged` with the visitor as speaker, on the farm, during the window | give the gift with `Game1.player.addItemByMenuIfNecessary(item)` (verify), state `Met`, a `Talked` diary line and a `Visit` entry |
| window end (11:00) | if not met: state `Missed`, `MissedVisit` diary line, send the NPC back |
| note days | a letter with the item attached through the mail format's item command (`%item object <id> <count> %%`: verify), written by our template, never with other commands |
| day 1 | Lewis gets a first-hand ledger entry for the player at the Farm (he meets the player in the intro event, which the tick observer doesn't see: verify whether `TimeChanged` fires during it). Ambient gossip spreads it |

The introductions quest counts meeting NPCs by the friendship record, which is created on the first
click (brief, "Constraints"). A visitor the player talks to is therefore "met" by the game's rules.
Verify it counts toward the quest.

## Laya questions

| Question | Type | State | Fallback |
|---|---|---|---|
| "Would <npc> go out of their way to welcome a newcomer in person?" | `noul`, per candidate NPC | the NPC card ([laya.md](laya.md)) | 0.5 |

Asked once, on a background task at the first load; the plan is built when the answers are in
(the first tick after they are). With the fallback everything is 0.5, and the seeded draw below still
varies by save.

## Deterministic rules

- Candidates: NPCs with a gift entry, not children, not the player's spouse (none in week 1), able to
  walk to the farm (not Krobus, the Wizard, the Dwarf, Sandy: a table).
- Visitors: draw each candidate with probability = the model's p, using FNV-1a of (save seed, npc);
  keep 3 to 5 (if fewer than 3 pass, take the highest p; above 5, the highest p). Shy NPCs
  (`SocialAnxiety` = Shy, verify) become `Note` instead of `Visit`.
- Days: visits on days 2-7, at most one visitor a day, order seeded. Notes on any day 2-7.
- Rain: a visit day with rain moves to the next free dry day in the week, else becomes a note.
- Never on a festival day (spring 13 is outside the week anyway).

## Tuning constants

`MinVisitors` 3, `MaxVisitors` 5, `VisitStartTime` 900, `VisitWindowTicks` 12 (two hours), week
days 2-7. Not saved (the plan is).

## Acceptance tests

Unit (new `tests/NpcInitiation.Tests/NewcomerTests.cs`, pure planner in `src/NpcInitiation/Newcomer.cs`):
- Same save seed and answers give the same plan; different seeds differ.
- 3-5 visitors, one per day, only candidates with gifts, shy NPCs as notes, no excluded NPCs.
- Rain moves a visit; a full week turns it into a note.
- Not eligible: no plan, and it stays not eligible on later loads.
- State machine: Planned -> Lettered -> Waiting -> Met or Missed; a gift is granted once only.

In-game on a fresh save: letters arrive the evening before; the visitor waits by the door; talking
gives exactly one gift; the introductions quest progresses; a missed visit writes `MissedVisit`; the
visitor reaches its next schedule stop after leaving.

## Status

Not started. Nothing exists yet.

## Open questions

- Only new saves, or also existing ones via a console command? (roadmap decision 3)
- Is placing (warping) the visitor acceptable, or must they walk? Walking is more natural but needs
  the travel code; warping at 9:00 before the player is likely outside is a reasonable first cut.
- Gift list contents are Sid's call; the table makes that an edit, not code.
