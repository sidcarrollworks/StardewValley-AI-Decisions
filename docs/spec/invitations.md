# 15. Letters, invitations and (later) requests

**Status: not started.** Asked for by Sid on 2026-09-30: NPCs should be able to send the player
letters, including ones asking the player to come and see them. Quest-like requests using vanilla
mechanics are a later idea, sketched at the end. Builds on the ladder's `Mail` rung
([ladder.md](ladder.md)) and the mail channel in [text.md](text.md).

## Player-visible behavior

- **Letters in the character's voice.** When the ladder's `Mail` rung fires, the player gets a letter
  the next morning. It is one of three kinds, chosen by the model from what fits the NPC:
  - **Missed you:** "I haven't seen you around lately. Stop by the shop sometime? - Pierre"
  - **News:** the NPC's planned line from its diary, as a letter ("You'll never guess who I saw at the
    beach on Tuesday...").
  - **Invitation:** "I'll be at the Stardrop Saloon tonight from 7. Come say hi if you're free.
    - Shane". It names a real place and a time window on the day the letter arrives, where that NPC
    will actually be.
- **Showing up counts.** If the player is at that place during the window and the NPC sees them
  there, the NPC remembers it ("Thanks for coming by last night"). If the player talks to the NPC
  there, it opens with a line about it. If the player doesn't come, the NPC remembers that too ("I
  waited for you at the saloon last night"), and its urge drops as if it had been ignored.
- **Rare.** At most one letter a day across the whole town (the existing cap), and at most two
  invitations a week.
- **Shadow:** `[shadow] Shane would write: invitation to the Stardrop Saloon, 7 PM to 10 PM (p=0.61)`,
  then `[shadow] Shane: invitation accepted` or `... stood up`.

## Data model

- `LetterKind` enum: `MissedYou = 0`, `News = 1`, `Invitation = 2` (saved as an int in pending
  letters, so append only).
- `PendingLetter(Npc, LetterKind, ArrivalDay, MailId, Line)` and
  `Invitation(Npc, Location, FromTick, ToTick, DayIndex, State = Open | Accepted | StoodUp)`, saved
  under a new save-data key `letters` ([persistence.md](persistence.md)).
- Mail ids: `squid.StardewNpcMod.<npc>.<dayIndex>` (one letter per NPC per day at most).
- New diary kinds ([diary.md](diary.md)): `AcceptedInvite` (subject Player; detail
  `place=...;talked=0|1`; news weight 4) and `StoodUp` (subject Player; detail `place=...`; news
  weight 4). Neither is shared in gossip: they stay between the two of them.

## Triggers and game hooks

| When | What | Game calls (checked in the 1.6.15 decompile) |
|---|---|---|
| a `Mail` attempt is drained (day D) | reserve tomorrow's letter; record a `PendingLetter` with no text yet | `Game1.addMailForTomorrow(id)` (skips an id the player already has or will get) |
| the 6:00 tick of day D+1 | decide the letter's kind and text (below), store it, and invalidate `Data/mail` so the text is served | `helper.GameContent.InvalidateCache("Data/mail")` |
| `AssetRequested` for `Data/mail` | add every pending letter's id and text | `e.Edit(asset => asset.AsDictionary<string, string>().Data[id] = text)`. The mailbox loads the text when the player opens the letter, so text written at 6:00 is what they read |
| each tick during an invitation's window | if the NPC's **own** ledger has a first-hand sighting of the player at the invitation's location this tick, mark `Accepted` | memory only (`Ledger.View`), no position read |
| `MenuChanged` with that NPC as speaker during the window, at the place | `talked=1`; (live) the NPC's queued "you came" line | existing hook |
| the end of the window | `Open` becomes `StoodUp` | - |

**Why the text is decided the morning it arrives.** An invitation must name a place where the NPC
will really be. By the 6:00 tick the game has already picked the NPC's schedule for the day
(confirmed: `NPC.dayUpdate` -> `resetForNewDay` -> `TryLoadSchedule` runs inside the new-day
processing, which SMAPI runs synchronously, before any 6:00 event), including rain variants. The day's
plan is `npc.Schedule`, a `Dictionary<int, SchedulePathDescription>` keyed by start time, each entry
with a `targetLocationName` and `targetTile`; a slot is the gap between one entry's time and the
next. So the letter is written from that day's actual schedule, and the invitation is for the
same day ("tonight"). A letter read on a later day says "tonight" about a day already gone; vanilla
letters share that quirk, and the window has simply closed.

**An NPC may read its own schedule.** Rule 2 in `AGENTS.md` forbids true positions of *others* in
decisions. An NPC knowing where it will be itself is its own plan, not surveillance. When this is
built, add that clarification to D2 in `docs/decisions.md`.

## Laya questions

| Question | Type | Options | Fallback |
|---|---|---|---|
| "What would <npc> write to the player about?" | `choice` | the eligible kinds among "say they miss the player", "share some news" (only if it has a planned line), "invite the player to meet up" (only if an invitation is possible) | uniform |
| "Where would <npc> ask the player to meet?" | `choice` | up to 3 candidate slots from its schedule, e.g. "the Stardrop Saloon, 7 PM to 10 PM" | uniform |

Both share the NPC card and relationship state ([laya.md](laya.md)) and can go in one batched
request. They run on a background task started at the 6:00 tick; the letter's text is stored when
the answers arrive. If the answers aren't in before the player opens the mailbox (unlikely: it's a
few hundred milliseconds), the fallback kind is `MissedYou`.

## Deterministic rules

- **Invitation eligible when:** hearts >= 3; no other open invitation (one at a time, town-wide);
  fewer than `MaxInvitesPerWeek` (2) this week and none from this NPC this week; not a festival day;
  the NPC has at least one candidate slot.
- **Candidate slots** come from the NPC's schedule for the day: a stay of at least
  `MinSlotTicks` (6, one hour) at one location, starting between 9:00 and 21:00, in a location not in
  `noSearch` and not in `noInvite` (a new `data/regions.json` list: farm buildings, mines, and any
  map the player can't enter at the time; check the list against the maps NPC schedules actually
  use). Up to 3 slots, earliest first.
- **Kind eligibility:** `News` needs a planned line for this NPC today ([intents.md](intents.md)); if
  the letter uses it, the line is marked delivered so it isn't said again in person.
- **Accepted** needs the NPC's own first-hand sighting at the place during the window; hearsay
  doesn't count. **Talked** is a bonus detail, not required.
- **Ladder effect:** the letter's `Mail` attempt is resolved by the invitation: `Accepted` counts as
  `Responded` (urge x 0.5, rung 0), `StoodUp` as `Ignored` (urge -0.2, escalation). For the other
  kinds, the existing rule stands (the player talks to the NPC before the end of the next day).
- **Text:** all from `i18n/default.json` (`mail.MissedYou`, `mail.News`, `mail.Invitation` per NPC),
  through the mail sanitizer. Letters never attach items.

## Tuning constants

`MaxInvitesPerWeek` 2, `MaxInvitesPerNpcPerWeek` 1, `InviteMinHearts` 3, `MinSlotTicks` 6, slot start
9:00 to 21:00, `MaxSlots` 3. Not saved.

## Acceptance tests

Unit (pure, `src/NpcInitiation/Letters.cs`):
- Slot finder: from a fixture schedule, stays of an hour or more in allowed maps become slots; short
  stops, bedrooms-only days and `noInvite` maps give none.
- Kind eligibility: no planned line means no `News`; no slot means no `Invitation`; caps and the
  one-open-invitation rule hold.
- Accepted only with a first-hand sighting at the place inside the window; a sighting elsewhere, or
  a tip, leaves it open; the window's end makes it `StoodUp`.
- Ladder: `Accepted` and `StoodUp` resolve the `Mail` attempt with the right urge changes.
- Pending letters and invitations round-trip through JSON; `LetterKind` is saved as an int.

In-game: a letter arrives the morning after a shadow `Mail` attempt (once live); its text names a
place the NPC really is at that time; going there marks it accepted; skipping it writes `StoodUp`;
the next overnight lines can cite either.

## Status

Not started. Today the `Mail` rung only logs `would try Mail`.

## Later: requests and quests from vanilla mechanics

Sid's idea: NPCs asking for things, maybe as quests. Vanilla has several mechanics the mod could use
rather than inventing its own. The mail commands and data formats below were checked in the 1.6.15
decompile (`LetterViewerMenu`, `Quest`, `NPC.checkForNewCurrentDialogue`).

| Mechanic | What it would give | Notes |
|---|---|---|
| A quest attached to a letter: `%item quest <id> %%` shows an "accept quest" button; `%item quest <id> true %%` adds it at once | "Could you bring me a trout? - Willy", tracked in the quest log with its own completion | our quests go into `Data/Quests` (key -> `type/title/description/objective/conditions/nextQuests/money/rewardDescription/canBeCancelled`) through `AssetRequested`. Note the game itself grants friendship on some completions (an item delivery gives the target 150 points for a daily quest, 255 otherwise); the mod would be choosing to use that, which is Sid's call |
| Help-wanted board (the daily quest) | nudging which NPC posts today's request toward one who wants the player's attention | invasive: it changes a vanilla system; probably not |
| Special orders (the town board) | multi-day requests with objectives | heavy content; a candidate for pairing with a content mod |
| Conversation topics (`Game1.player.activeDialogueEvents`, topic id -> days left; `autoGenerateActiveDialogueEvent(id, 4)`; mail can start one with `%item conversationtopic <id> <days> %%`) | vanilla-style dialogue reacting to what happened ("I heard about the fire at the mines"), expiring after some days | an NPC says the entry whose key equals the topic id in its `Characters/Dialogue/<Name>`, once (tracked as mail flag `<Name>_<topic>`). Our topic dialogue would be templated text added by asset edit. Caution: showing a topic line clears the NPC's other queued dialogue, including ours ([intents.md](intents.md)) |
| NPC gifts by mail | a small item enclosed with a thank-you letter | item grants only from a table, like newcomer week |
| The movie theater | an NPC inviting the player to a movie | 1.6 has the theater and invitations in the other direction; later |

Recommended first request type when the time comes: the letter with an item-delivery quest, because
it reuses mail (built here), the quest log tracks completion for us, and the NPC's diary gets a
natural `QuestHelped` entry ([diary.md](diary.md)).

## Open questions

- Should an invitation ever be for the NPC's home? Bedroom doors open at 2 hearts (confirmed: the
  `Door` action checks `getFriendshipHeartLevelForNPC >= 2`, then remembers `doorUnlock<Name>`), so
  `InviteMinHearts` 3 is above that.
- Should accepting an invitation do anything beyond memory and a line (a friendship bonus)? Today the
  mod never changes friendship; the vanilla conversation there already gives the usual points.
