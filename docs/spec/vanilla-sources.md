# 22. Vanilla sources: what the game already gives us

**Status: not started; verified** (DeepSeek's pass, PR #18, 2026-10-01; findings with file and
line in `stardew-source-notes.md`, "Motives verify pass"). Sid,
2026-10-01: *"I'd like to get as much info from the vanilla game as we can, as it is the best data
to help influence our characters and keep them in character."* D26 records the principle.

The game already knows a great deal about its characters and already has ways for them to act.
This spec lists what the mod can read from the game (to feed memory, motives and temperament) and
which vanilla mechanisms it can use to act (instead of building its own). Each entry links to the
feature spec that uses it. Most facts are now confirmed in the 1.6.15 decompile; what only the
running game can show is in Sid's in-game checklist (`stardew-source-notes.md`, "In-game checks
pending") and marked **in-game** here.

## The principle

1. **Read before inventing.** If the game records something about a character or the player, read
   it instead of guessing it. The game's own data is in character by construction.
2. **Use vanilla channels before custom ones.** A schedule the game paths, a quest the quest log
   tracks, an emote the game draws: each is less code, fewer bugs and more in keeping with the game
   than our own version.
3. **Reading stays read-only.** Every new signal is a read, a read-only Harmony postfix or a diary
   entry ([diary.md](diary.md), "Harmony"). Rule 2 still holds: hooks write memory; decisions read
   memory.
4. **Acting follows the rollout.** Every vanilla channel used to act has its own live switch,
   default off ([rollout.md](rollout.md)), and logs `[shadow]` first.
5. **New content, not edits.** The mod may add new keys to game data (a mail id, a quest, a
   schedule key for one day) through `AssetRequested`, but never changes vanilla entries
   (`docs/spec/README.md`, invariant 7). Adding dialogue keys for conversation topics is a separate
   decision for Sid (below).

## Player-visible behavior

Indirect: characters react to more of what the player does (answers to their questions, heart
events, festivals, movies, rummaging in the trash), behave more like themselves (seeds from their
own dialogue and data), and act through mechanics the player already knows (a letter, a scheduled
visit, a quest in the quest log, a phone call).

## Data model: signals to read

### Already used

`Data/Characters` traits (`Manner`, `SocialAnxiety`, `Optimism`, `Age`, `HomeRegion`,
`FriendsAndFamily`), dialogue files (temperament), `Data/Schedules` (family priors), gift tastes,
quests, festivals, `friendshipData` (hearts, status), weather. All confirmed.

### New player-action signals (each becomes a diary kind)

| Signal | How to read it (confirmed unless marked) | Diary kind | Feeds |
|---|---|---|---|
| The player's answer to a character's question | a read-only postfix on `Dialogue.chooseResponse(Response)`: `__instance.speaker`, the dialogue key, and (as `NPCDialogueResponse`) the answer's id and vanilla friendship change; the index from `DialogueBox.selectedResponse`. Vanilla has 17 such questions in 9 villagers' dialogue (Abigail 4, Alex 3, Haley 2, Maru 2, Sebastian 2, Clint, Leah, Penny, Sam 1), 5 in `Data/ExtraDialogue` and 46 inside event scripts; festival questions carry no friendship and are skipped | `Praised` / `BrushedOff` / `Criticized` by the sign of vanilla's friendship change | `Grateful`, `Hurt`, regard ([motives.md](motives.md)) |
| Heart events the player has seen | `Farmer.eventsSeen` gains the id when an event ends; the NPC and heart level come from the event's `f <Npc> <points>` precondition in `Data/Events` keys (250 points a heart). There is no flag telling a heart event from a cutscene: an event with an `f` precondition counts as a heart event for that NPC | `HeartEvent` (`event`, `hearts`) | warm plastic stress |
| Searching a garbage can near villagers | `GameLocation.CheckGarbage`: the **first** villager within 7 tiles (straight-line) reacts, with `DumpsterDiveFriendshipEffect` (default -25) and an emote by age; once per can per day. The witness is a local, so the producer finds it as the NPC whose friendship changed in that call (a prefix snapshot, a postfix diff), which needs no position read | `SawRummaging` | disgust, juiciness 4 |
| Flower Dance | the answer to the player's invitation is decided at the festival (accept: spouse, or 4+ hearts with no partner and not married, +250); `Farmer.dancePartner` resets overnight, so capture it **during** the festival on `OneSecondUpdateTicked`, like the existing festival capture | `DanceAsked` (`accepted`), `SawDance` for the other dancers' partners | `Grateful`, `Hurt`, `Jealous` ([romance.md](romance.md)) |
| Luau soup | quality sets an immediate friendship change for every villager whose home region is Town (+120 / +60 / 0 / -50 / -100); nothing is stored, so diff the town's friendship points across the festival | `Festival` with `soup=<quality>` | one shared town reaction (happiness or disgust) |
| Grange display, egg hunt | `player.festivalScore` is readable at `DayEnding` (reset overnight); the durable markers are the `wonGrange` and `wonEggHunt` conversation topics | `Festival` with `result=won` | pride; `News` |
| A movie together | the invitation is accepted in `NPC.tryToReceiveActiveObject`; the reaction (love / like / dislike) is never stored but is deterministic: recompute `MovieTheater.GetResponseForMovie` for that NPC and date when `NPC.lastSeenMovieWeek` changes | `MovieTogether` (`liked`) | `Grateful`, mild `Hurt` |
| The player passing out | `Farmer.performPassoutWarp` (stamina or 2 AM); the rescue letter id is `<PassOutMail>_{Billed or NotBilled}_{Male or Female}` and the rescuer's name exists only in the letter's text (**in-game**: read it to map ids to names). Dying in the mines is separate: the `PlayerKilled` event, whose rescuer is chosen in code (Robin, Clint, Maru or Linus; 10% the spouse; Willy or Leo on the island) and can be read from the event's actors while it plays | `PassedOut` (`found` 1 for the rescuer) | `Worried`, `News` |
| Vanilla friendship decay | `Farmer.resetFriendshipsForNewDay`, after `DayEnding`, on days the player didn't talk to them: spouse -20 a day; dating -10 a day below 10 hearts; datable but not dating -2 below 8 hearts; everyone else -2 below 10 hearts. There is **no** "no decay below 2 hearts" rule (the wiki is wrong) | none | `MissingYou`; absence alone never feeds `Hurt`, so the grudge never punishes what vanilla already decays |
| Conversation topics | `Farmer.activeDialogueEvents`; the full vanilla list with triggers and days is in the source notes (`Introduction`, `cc_*`, `joja_Begin`, `married_*`, `dating_*`, `divorced_*`, `wonGrange`, `pamHouseUpgrade`, `movieTheater`, `GreenRainFinished`, ...). The game also spawns `<topic>_memory_oneday` ... `_oneyear` at 1, 7, 14, 28, 56 and 104 days, its own anniversaries | `TownNews` (`topic`, `days`); `_memory_*` ids as anniversaries ("a week since the bridge was fixed") | `News`, gossip, opinions of the player |
| NPCs away at the resort | `Game1.netWorldState.Value.IslandVisitors` (or `Game1.IsVisitingIslandToday`), set overnight, readable in the morning | none: a known absence | keeps `Worried` and "looked for you" quiet |

### Character data to mine (offline, in the tools)

| Source | What it gives | Used by |
|---|---|---|
| Each villager's dialogue: mentions of other villagers near warm or cold words | a proposed seed for `Regard(A, B)` | [town-life.md](town-life.md) |
| Heart events' actors (`Data/Events`) | which villagers the game shows together, and in what mood. There is no structured participant list; the event script's opening actor placements are plain text, so the tool parses them (and Sid reviews the result) | `Regard` seeds; who chats with whom |
| Each villager's gift tastes (`Data/NPCGiftTastes`) | what they want, for `NeedsHelp` requests and trade offers | [motives.md](motives.md), [trades.md](trades.md) |
| Their own conversation-topic and event dialogue | how they talk about town events | line voices ([text.md](text.md)) |

Seeds are proposals: like the temperament table, they go into a reviewed fixture
(`fixtures/game/regard/`) with an overrides file, and Sid edits the misfits.

## Vanilla channels for acting

| Channel | Use | Spec | Notes (confirmed unless marked) |
|---|---|---|---|
| **Emotes** (`Character.doEmote`; angry 12, sad 28, happy 32, heart 20, blush 60, exclamation 16, question 8) | the `Emote` act shows the feeling's emotion | [motives.md](motives.md) | vanilla itself uses 56 (music note) for a liked movie and 24 for a disliked one |
| **One day's schedule** | meet-ups in town ([town-life.md](town-life.md)); the first leg of a farm visit ([invitations.md](invitations.md)) | town-life, invitations | `npc.TryLoadSchedule(key, raw)` from `DayStarted` replaces today's schedule only; tomorrow rebuilds from data; survives warps. Schedules load from `Characters/schedules/<Name>` (not `Data/Schedules`); never edit that asset (an edit applies to every matching day). **The farm is excluded from NPC routing**: a schedule reaches the farm's edge, not the farm |
| **Arrival messages and animations** (`endOfRouteMessage`, `endOfRouteBehavior`) | a line or animation at a scheduled stop | invitations, town-life | behaviors: `change_beach`/`change_normal`, `square_*`, or any of the 105 `Data/AnimationDescriptions` keys; the message `silent` suppresses talk |
| **Quests** | `NeedsHelp` asks for something real; completion writes `QuestHelped` | [motives.md](motives.md), [invitations.md](invitations.md) | `new ItemDeliveryQuest(npc, itemId)` + `questLog.Add` needs no data entry; a quest in a letter (`%item quest <id>`) needs a `Data/Quests` entry (a new key) |
| **Special orders** | bigger, multi-day requests | invitations, "Later" | `team.AddSpecialOrder(id)` with a `Data/SpecialOrders` entry (`Requester`); completion pays 250 friendship to the requester by default, and money |
| **Phone calls** (1.6) | a call to the farmhouse phone, between a letter and a visit | [invitations.md](invitations.md) | `Data/IncomingPhoneCalls` entries (new keys) with a date condition and `IgnoreBaseChance` for a given day, or `Phone.Ring(callId)` for an exact time; the text is tokenizable |
| **Trigger actions** (1.6) | data-driven plumbing, also from letters (`%action ...%%`) | [rollout.md](rollout.md) | `AddMail`, `RemoveMail`, `AddConversationTopic <id> [days]`, `RemoveConversationTopic`, `AddFriendshipPoints <npc> <points>`; dialogue uses `$action` |
| **Conversation topics** | make a piece of news a few-day town topic | invitations | NPCs without a dialogue key for a topic skip it and the topic is not used up, so a topic with no keys is harmless. Needs new dialogue keys to say anything: Sid's call |
| **Portraits** | a hurt line shows the sad face | [text.md](text.md) | `$h $s $u $l $a` and `$0..$n` are read anywhere in their `#` segment, so appending one at the end works; block commands must start a segment. Needs a sanitizer rule change: Sid's call |

## Triggers and game hooks

- New read-only postfixes go in `mod/StardewNpcMod/Patches/`, one class per patched method, as
  [diary.md](diary.md) requires; each queues an observation that the next tick writes to memory.
- `eventsSeen`, `activeDialogueEvents`, festival results and the resort list are read once a day
  (at `DayEnding` or the 6:00 tick) and diffed against yesterday's copy, kept in memory.
- Offline mining runs in `tools/` against the decompiled content, like `TemperamentExtractor`, and
  writes fixtures; the mod reads fixtures, never the raw files.

## Laya questions

None of its own. The new diary kinds reach the model through the existing questions
([motives.md](motives.md), [intents.md](intents.md)).

## Deterministic rules

- A signal that can't be read (a patch target missing after a game update) logs a warning once and
  is skipped; nothing else changes ([diary.md](diary.md), "Harmony").
- Diffed daily signals are idempotent: reloading a save never writes the same `HeartEvent` or
  `TownNews` twice.
- The resort list only explains absences; it never reveals where anyone is to another NPC's
  decisions (rule 2).

## Tuning constants

None of its own; each diary kind gets a row in the stressor table ([motives.md](motives.md)).

## Acceptance tests

- Each postfix: a fake call queues the right observation; a missing target logs once and the mod
  loads.
- Daily diffs: a new id in `eventsSeen` writes one `HeartEvent`; a reload writes none; a topic that
  expires writes nothing.
- The regard-seed tool: fixed dialogue fixtures give the expected seeds; the overrides file wins.
- In-game, per signal: Sid's checklist from the verification pass (E19), with what the playtest log
  should show ([debug-tools.md](debug-tools.md), "Playtest log").

## Status

Not started. Facts verified in the decompile (PR #18); in-game checks pending: a garbage can next
to a villager, one vanilla question answered both ways, a movie, passing out once, plus the
heart-event key dump and the rescue letters' names (`stardew-source-notes.md`).

## Open questions

- May the mod add dialogue keys for conversation topics and its own scheduled-stop lines? They are
  new keys, not edits to vanilla lines, but they live in vanilla dialogue assets. Recommendation:
  yes for new keys only, behind their switches.
- May the template system append a portrait code (`$s`) after sanitizing? The model would never
  choose it. Recommendation: yes, from a fixed list per emotion.
- Which signals come first? Recommendation: dialogue answers, heart events and garbage cans,
  because they are the player's own choices, work in shadow mode today, and their hooks are now
  confirmed.
- Vanilla's own friendship effects (a garbage dive -25, a liked movie +100, the soup) already change
  points. Our stresses add memory and behavior on top, never more points; only the grudge penalty
  changes friendship ([motives.md](motives.md)).
