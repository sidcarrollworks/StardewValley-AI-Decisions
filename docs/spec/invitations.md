# 15. Letters, invitations and (later) requests

**Status: not started.** Asked for by Sid on 2026-09-30: NPCs should be able to send the player
letters, including ones asking the player to come and see them. Quest-like requests using vanilla
mechanics are a later idea, sketched at the end. Builds on the ladder's `Mail` rung
([ladder.md](ladder.md)) and the mail channel in [text.md](text.md).

When motives land (D24, [motives.md](motives.md)), the urge changes below become motive changes:
`Accepted` uses the motive up and writes `AcceptedInvite` (a warm stress), `StoodUp` writes
`StoodUp` (a severe, plastic stress) and counts as an ignored attempt for frustration.

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
- **"I'd like to come by your farm."** A letter can also announce a visit: a character asks to
  come to the farm at a set time on a set day, and on that day the game walks them there (their
  schedule for the day is replaced, so the game does the pathing) and they wait by the farmhouse
  for an hour. Details below, "Farm visits by appointment".
- **Rare.** At most one letter a day across the whole town (the existing cap), and at most two
  invitations a week, farm visits included.
- **Shadow:** `[shadow] Shane would write: invitation to the Stardrop Saloon, 7 PM to 10 PM (p=0.61)`,
  then `[shadow] Shane: invitation accepted` or `... stood up`.

## Data model

- `LetterKind` enum: `MissedYou = 0`, `News = 1`, `Invitation = 2`, `FarmVisit = 3` (saved as an
  int in pending letters, so append only).
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
| "What would <npc> write to the player about?" | `choice` | the eligible kinds among "say they miss the player", "share some news" (only if it has a planned line), "invite the player to meet up" (only if an invitation is possible), "ask to come by the farm" (only if a farm visit is possible) | uniform |
| "When would <npc> come by the farm?" | `choice` | up to 3 free slots in the next `VisitLeadDays` days, e.g. "Thursday, 2 PM" | uniform |
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

## Farm visits by appointment (Sid, 2026-10-01)

Sid: *"Editing the schedule is a good idea, also plays well with sending mail. 'I want to visit you
at your farm at this time on this day' provides an opportunity for negative interaction: you could
stand up the character. Next message you see from that character: 'I came to your farm to see you
and waited for an hour but you weren't there.'"*

This is the preferred form of a visit: announced, planned ahead, and walked mostly by the game's
own pathing through a schedule for that one day. The unannounced visits in [find.md](find.md) stay
for later.

**The farm is outside the game's routing** (verify pass, 2026-10-01: `Farm` returns true from
`ShouldExcludeFromNpcPathfinding`, and the warp-route cache ignores `Farm`, `Backwoods` and
`Cellar`; married NPCs reach the farm only by direct warps). So a schedule can bring the NPC to the
farm's doorstep but not onto it. The visit is done in three legs:
1. **To the farm's edge, by schedule.** The day's schedule walks the NPC to the Bus Stop tile at the
   farm's east exit (the side every farm type has; **verify** per farm type), arriving
   `FarmWalkTicks` before the appointment. Pure vanilla pathing.
2. **Onto the farm, by the mod.** On arrival the mod warps the NPC to the farm's matching entrance
   tile with `Game1.warpCharacter` (the call married NPCs use, confirmed), which looks exactly like
   walking through a map exit, then walks it to the farmhouse door with the in-map pathfinder
   (`PathFindController`; **verify** it paths on the farm map, as spouses do outdoors). It waits,
   then walks back and is warped back to the Bus Stop.
3. **Back to its day, by schedule.** The rest of the one-day schedule resumes; a schedule replaced
   for the day survives warps (confirmed).
Leg 2 is a small spike of its own (one map, one known pattern), much smaller than the general
travel spike. **Fallback** if the in-farm walk misbehaves: the NPC waits at the farm's gate on the
Bus Stop side, and the letter says so ("I'll wait by your gate").

**Player-visible behavior**
- A letter: "I'd love to see how the farm is coming along. Could I come by on Thursday around 2?
  I'll wait by your door for a bit. - Leah". The day and time come from the NPC's real free time.
- On the day, the NPC leaves at the right time, walks to the farm, stands near the farmhouse door
  (`FarmVisitWaitTicks`, 6 = one hour), then goes home or back to its usual schedule.
- **Met:** the player is on the farm and the NPC sees them, or the player is inside the farmhouse
  (the NPC knocks; see below). If they talk, the NPC has its visit line ("So this is your farm!
  It's lovely."). Diary: `FarmVisited` (`talked` 0/1), a warm stress.
- **Stood up:** the player is elsewhere for the whole hour. Diary: `StoodUp` with `place=Farm` and
  `waited=<ticks>`, a severe plastic stress ([motives.md](motives.md)). The next time the player
  talks to the NPC, or in its next letter, it says so: "I came to your farm to see you and waited
  for an hour, but you weren't there." Gossip: a stand-up is juicy (3), so others may hear of it.
- **Seen but ignored:** the player is on the farm and the NPC sees them, but they never talk during
  the hour. Diary: `StoodUp` with `seen=1`; it hurts more than missing it.
- Shadow: `[shadow] Leah would write: farm visit, Thursday 14:00 (MissingYou 0.48)`, then on the day
  `[shadow] Leah would walk to the farm at 13:10 and wait until 15:00`, then `... met you` or
  `... was stood up`.

**Data model**
- `FarmVisit(Npc, DayIndex, ArriveTick, WaitTicks, State = Planned | OnTheWay | Waiting | Met |
  StoodUp | Cancelled)`, saved under `letters` with the invitations.
- New diary kind `FarmVisited` (subject Player; `talked`; news weight 3; juiciness 1).
  `StoodUp` gains the keys `place=Farm`, `waited`, `seen`.
- Mail id as for other letters; the visit's day schedule key: `squid.StardewNpcMod.visit.<dayIndex>`.

**Triggers and game hooks**

| When | What |
|---|---|
| a motive's best act is `FarmVisit` ([motives.md](motives.md), "The act rule") | choose the day and slot (below), reserve tomorrow's letter |
| `DayStarted` on the visit day (after the game built the day's schedules) | build the day's raw schedule: its normal stops up to the visit, then the Bus Stop farm exit by `ArriveTick - FarmWalkTicks`, then its normal stops after the visit; apply it with `npc.TryLoadSchedule("squid.StardewNpcMod.visit", raw)` (confirmed: a one-day replacement; tomorrow rebuilds from data). Never edit `Characters/schedules/<Name>` itself: an asset edit persists for every matching day |
| the NPC reaches the farm exit | warp onto the farm, walk to the door (leg 2) |
| each tick while `Waiting` | Met if the NPC's **own** ledger has a first-hand sighting of the player on the farm or at the door (below); talking during the window sets `talked=1` |
| the end of the wait | `Met` or `StoodUp`; the NPC continues its schedule |

**The player inside the farmhouse.** The NPC can't see into the house, but a real visitor would
knock. Perception handles it, so rule 2 holds: `MemoryStore.Observe` (the one place that reads
positions) treats an NPC within 2 tiles of the farmhouse door and the player inside `FarmHouse` as
co-located, as if the door was answered, and writes the usual first-hand ledger entry. The visit
code only reads that memory. Live, the knock is shown as a HUD message "Leah is at your door" and a
knock sound (**verify** the HUD message and sound calls); in shadow, a log line.

**Deterministic rules**
- Eligible when: hearts >= `FarmVisitMinHearts` (4); no other open invitation or farm visit,
  town-wide; within the invitation caps; not the NPC's spouse or a child; not in `data/visits.json`
  (characters who never leave, e.g. remote ones).
- **Slots** are the NPC's own free time in the next `VisitLeadDays` (1 to 3) days: a gap of at least
  `FarmVisitTravelTicks + FarmVisitWaitTicks` between 9:00 and 18:00 where its schedule doesn't keep
  it at work (its shop or workplace), on a non-festival day, starting at least
  `FarmVisitTravelTicks` after the previous stop so it can get there in time. Shopkeepers therefore
  come on their days off or after hours; nobody's shop closes for a planned visit.
- A visit day's schedule is the only schedule change. If the visit day turns stormy or becomes a
  festival, the visit is `Cancelled` and the NPC writes a short note (no stress either way).
- Stood up counts once: one `StoodUp` per visit, however long the player stays away.
- The NPC never enters the farmhouse or any farm building.

**Live switch**: `FarmVisits`, default off ([rollout.md](rollout.md)). It replaces a schedule for
one day and moves the NPC onto the farm, so it comes after `Mail` and after the leg-2 spike (the
general travel spike is not needed).

**Tuning constants**: `FarmVisitMinHearts` 4, `VisitLeadDays` 1-3, `FarmVisitWaitTicks` 6,
`FarmVisitTravelTicks` per home (measured from the NPC's home to the Bus Stop exit in the first
in-game runs), `FarmWalkTicks` 2 (the walk from the farm entrance to the door),
slots 9:00 to 18:00. Not saved.

**Acceptance tests**
- Slot finder: fixture schedules give slots only in free time; a shopkeeper's working hours never
  give one; festival days give none.
- The day schedule: built from the normal one, with the farm stop inserted, and the normal entries
  after it unchanged.
- States: a first-hand sighting during the wait is `Met`; `Observe` turns the NPC at the door and
  the player in the farmhouse into a first-hand sighting (a knock), so that is `Met` too; no sighting is `StoodUp` with `waited=6`; seen-but-not-talked is `StoodUp` with `seen=1`;
  a storm cancels with no stress.
- In-game (switch on): the NPC arrives at the Bus Stop exit on time, appears at the farm entrance,
  walks to the door, waits, walks back out and is back on its usual schedule afterward; its shop
  opened on schedule; the stand-up line appears the next time the player talks to it. Run it on at
  least two farm types.

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
natural `QuestHelped` entry ([diary.md](diary.md)). Under motives this is how `NeedsHelp` is
expressed ([motives.md](motives.md)); what to ask for comes from the NPC's own gift tastes and
needs ([vanilla-sources.md](vanilla-sources.md)). Phone calls (1.6) are a further channel between a
letter and a visit, once verified ([vanilla-sources.md](vanilla-sources.md)).

## Open questions

- Should an invitation ever be for the NPC's home? Bedroom doors open at 2 hearts (confirmed: the
  `Door` action checks `getFriendshipHeartLevelForNPC >= 2`, then remembers `doorUnlock<Name>`), so
  `InviteMinHearts` 3 is above that.
- Should accepting an invitation do anything beyond memory and a line (a friendship bonus)? Today the
  mod never changes friendship; the vanilla conversation there already gives the usual points.
