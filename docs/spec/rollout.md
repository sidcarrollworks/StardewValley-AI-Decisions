# 11. Shadow-to-live rollout

**Status: first switches live (2026-10-02, D30).** `Emote` and `Bubble`, with the gate, the
circuit breaker and the `[live]` lines, are built and tested in `src/NpcLive` and wired into the mod
(PR #30). Everything else is shadow (D1, `AGENTS.md` rule 1). This file says how a
behavior goes from a `[shadow]` line to something that happens in the game, and in which order.

## Player-visible behavior

Nothing changes until Sid turns a switch on in `config.json`. Each live behavior has its own switch,
default off, and logs `[live]` instead of `[shadow]` when it acts. Turning a switch off returns
that behavior to shadow on the next launch; nothing a live feature did needs undoing (lines expire
the same day, letters are ordinary letters, gifts are ordinary items).

## Data model

`ModConfig.Live`, an object of booleans:

```json
"Live": {
  "IntentLines": false, "Emote": false, "Bubble": false, "QueuedLine": false,
  "Mail": false, "Letters": false, "ApproachNear": false, "ForcedDialogue": false,
  "Visit": false, "NewcomerWeek": false, "Trades": false, "FriendshipEffects": false, "TownLife": false
}
```

Plus `DayLengthMinutes` ([day-length.md](day-length.md)), which is a gameplay setting rather than an
NPC behavior. Switches are read once at `Entry` (restart to change), like all config.

## Order and gates

A behavior goes live only when all of these hold:
1. It has run in shadow for at least **7 in-game days** on Sid's test save with its log lines
   reviewed, and the rates look right (how often, which NPCs, never in a cutscene).
2. Its unit tests and `LiveGate` checks exist ([ladder.md](ladder.md)).
3. Its "open questions" that affect game state are answered in-game (schedule restore, dialogue
   removal, mail registration).
4. Sid has asked for it explicitly (`AGENTS.md` rule 1).

Recommended order, least invasive first:

| # | Switch | Why this position | Risk |
|---|---|---|---|
| 1 | `IntentLines` | passive: shown only when the player talks to the NPC; every line cites a true event | a line shows at a bad moment; mitigated by expiring daily |
| 2 | `Emote` | purely visual | none known |
| 3 | `Bubble` | visual, short text | text quality (templates) |
| 4 | `QueuedLine` | same mechanism as 1 | same as 1 |
| 5 | `Mail` and `Letters` | needs asset editing; letters persist in the mailbox; `Letters` adds news and invitation kinds ([invitations.md](invitations.md)) | a malformed letter; covered by the mail sanitizer |
| 6 | `ApproachNear` | first movement; must restore the schedule | NPC stuck or late for its schedule |
| 7 | `ForcedDialogue` | interrupts the player; rare by design | annoyance |
| 8 | `NewcomerWeek` | combines mail, gifts, and placing an NPC | item grant; only new saves |
| 9 | `Visit` (was `ApproachFar`) | cross-location travel; 1 to 2 a week, a shop may close while its keeper is out | the riskiest code: needs the travel spike first |
| - | `Trades` | any time after `Mail`: uses the vanilla shop menu, gives items from `data/trades.json` | balance; covered by the table test |
| - | `TownLife` | bubbles between NPCs can go live early (after `Bubble`); meet-ups need the travel spike | chatter volume; capped at 6 rendered scenes a day |
| - | `FriendshipEffects` | only after motives have run in shadow long enough to trust the grudge numbers | the one change to friendship points; capped at 20 points per character per week |
| - | `FarmVisits` | after `Mail` and the farm-entry spike: a letter, then a schedule for one day to the farm's edge, and the mod walks the NPC onto the farm and back ([invitations.md](invitations.md)) | an NPC stuck on the farm; it must be back on its schedule the same day |
| - | `Requests` | after `Mail`: `NeedsHelp` posts a quest through a letter ([invitations.md](invitations.md), [motives.md](motives.md)) | the quest log fills; at most one open request per NPC and few a week |
| - | `HostileActs` | after the rung it uses is live and motives have run in shadow: sharp bubbles, cold letters, confrontations ([motives.md](motives.md)) | characters feel mean; hostile acts cost far more than friendly ones, so they stay rare |
| - | `RomanceActs` | after romance milestones run in shadow: a kiss and other big romantic acts ([romance.md](romance.md)) | a scene at the wrong moment; partner status required, never during events |

## Safety invariants (live)

- The mod changes friendship points only through the grudge penalty (`FriendshipEffects`), grants
  items only from the newcomer and trade tables, never edits vanilla dialogue assets, and never
  removes anything from the player except what the player hands over in a trade.
- Every NPC the mod moves gets its schedule back the same day, or the move is not made.
- No live action during events, festivals, cutscenes, menus, or while the player can't move.
- If any live handler throws, it logs an error and turns that one switch off for the session (a
  circuit breaker), so a bug can't repeat every tick.
- A console command `npcmod_live off` (SMAPI console, `helper.ConsoleCommands.Add`) turns every live
  switch off for the session.

## Triggers and game hooks

Each live path is described in its feature file. Common: the game thread executes; nothing live runs
on a background thread.

## Laya questions

None of its own.

## Acceptance tests

- Unit: config defaults are all off; a thrown live handler flips only its own switch (circuit
  breaker as a pure class).
- In-game, per switch: the item in its feature's "in-game" list, plus a full day with the switch on
  and one with it off showing identical shadow logs apart from `[live]` tags.

## Status

Built (2026-10-02, `src/NpcLive`): `LiveSwitches` (`Emote`, `Bubble`), `LiveGate`,
`LiveBreaker` (the circuit breaker, and `TripAll` for `npcmod_live off`), `LivePlanner` (emote ids
and bubble lines) and `LiveLedger` (an ignored live act writes `IgnoredBy`). Wired into the mod by
PR #30. Bubbles speak in each villager's voice (`BubbleVoices`, PR #31). Since 2026-10-04 the gate
also holds back a second act from the same villager within 3 ticks
(`LiveOptions.MinTicksBetweenActs`). The emote ids and the `doEmote` and `showTextAboveHead`
signatures are confirmed in the 1.6.15 decompile (`stardew-source-notes.md`, "Audit pass").
Not started: the other switches. Found in the audit (2026-10-05), not fixed yet: the live ledger
is not reset at the title screen, so a shown act from before a reload can still be written as
`IgnoredBy`.

## Decided

- The first live release is `IntentLines` only (Sid, 2026-09-30). `Emote` and the rest follow one at
  a time, each with Sid's go-ahead.
- **Changed (Sid, 2026-10-02, D30):** `Emote` and `Bubble` go first, friendly and hostile alike, from
  the motives runner only. `IntentLines` follows.
- `ApproachFar` is wanted (Sid, 2026-09-30), as the rare `Visit` rung ([find.md](find.md)); it keeps
  its place at the end of the order because it depends on the travel code.
