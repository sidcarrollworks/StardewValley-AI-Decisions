# 22. Vanilla sources: what the game already gives us

**Status: not started; a verification pass is running** (DeepSeek, from 2026-10-01). Sid,
2026-10-01: *"I'd like to get as much info from the vanilla game as we can, as it is the best data
to help influence our characters and keep them in character."* D26 records the principle.

The game already knows a great deal about its characters and already has ways for them to act.
This spec lists what the mod can read from the game (to feed memory, motives and temperament) and
which vanilla mechanisms it can use to act (instead of building its own). Each entry links to the
feature spec that uses it. Entries marked **verify** are recalled, not confirmed; the number after
them is the item in the verification list Sid gave the local DeepSeek agent on 2026-10-01
(answers go into `stardew-source-notes.md`).

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

| Signal | Game source | Diary kind | Feeds | Status |
|---|---|---|---|---|
| The player's answer to a character's question (`$q`/`$r`/`$y` dialogue) | a read-only postfix on the answer handler | `Praised`, `BrushedOff`, `Criticized`, by the answer's friendship effect | `Grateful`, `Hurt`, regard ([motives.md](motives.md)). Vanilla's own questions give real player choices now, before our dialogue exists | **verify** (A1) |
| Heart events the player has seen | `Farmer.eventsSeen` (confirmed), mapped to NPCs through `Data/Events` | `HeartEvent` (subject: the NPC; `event`, `hearts`) | strong warm plastic stress; gossip juiciness 3 when another villager was in it | mapping **verify** (B6) |
| Searching a garbage can near villagers | the garbage-can handler | `SawRummaging` | disgust, juiciness 4 ([ledger-gossip.md](ledger-gossip.md)) | **verify** (B5) |
| Flower Dance: who the player asked, who accepted, who they danced with | the festival's dance code | `DanceAsked` (`accepted` 0/1), `SawDance` (`with`) | `Grateful`, `Hurt`, `Jealous` ([romance.md](romance.md)) | **verify** (B7) |
| Luau soup, grange display, egg hunt | festival results at `DayEnding` | `Festival` gains `result=...` | shared town news; pride or embarrassment | **verify** (B8) |
| A movie together and how the NPC liked it | the theater invitation and reaction code; `lastSeenMovieWeek` (confirmed field) | `MovieTogether` (`liked` love/like/dislike) | `Grateful` or mild `Hurt` | **verify** (B9) |
| The player passing out, and who found them | collapse handling, rescue mail | `PassedOut` for the rescuer; `Heard` for others | `Worried`, `News` | **verify** (B10) |
| Vanilla friendship decay | the nightly decay | none: read so our grudge doesn't punish the same neglect twice | `MissingYou` | rules **verify** (B12) |
| Conversation topics the game starts (Community Center rooms, Joja, weddings, births...) | `Farmer.activeDialogueEvents` (confirmed) | `TownNews` (`topic`, `days`) for every villager | `News`, gossip, opinions of the player | topic list **verify** (A4) |
| NPCs away at the Ginger Island resort | the resort visitor list | none: a known absence | keeps `Worried` and "looked for you" from firing | **verify** (B11) |

### Character data to mine (offline, in the tools)

| Source | What it gives | Used by |
|---|---|---|
| Each villager's dialogue: mentions of other villagers near warm or cold words | a proposed seed for `Regard(A, B)` | [town-life.md](town-life.md) |
| Heart events' actors (`Data/Events`) | which villagers the game shows together, and in what mood | `Regard` seeds; who chats with whom |
| Each villager's gift tastes (`Data/NPCGiftTastes`) | what they want, for `NeedsHelp` requests and trade offers | [motives.md](motives.md), [trades.md](trades.md) |
| Their own conversation-topic and event dialogue | how they talk about town events | line voices ([text.md](text.md)) |

Seeds are proposals: like the temperament table, they go into a reviewed fixture
(`fixtures/game/regard/`) with an overrides file, and Sid edits the misfits.

## Vanilla channels for acting

| Channel | Use | Spec | Status |
|---|---|---|---|
| **Emotes** (`Character.doEmote`; ids confirmed: angry 12, sad 28, happy 32, heart 20, blush 60, exclamation 16, question 8) | the `Emote` act shows the feeling's emotion: anger 12, sadness 28, happiness 32, surprise 16, fear 8, a romantic warm feeling 20 or 60 | [motives.md](motives.md) | confirmed |
| **One day's schedule** | an NPC goes somewhere at a set time and the game paths it there and back: **farm visits by appointment** ([invitations.md](invitations.md)), meet-ups ([town-life.md](town-life.md)) | find, invitations, town-life | how to swap one day's schedule **verify** (A2) |
| **Arrival messages** (`endOfRouteMessage`, `endOfRouteBehavior`, confirmed fields) | a line or animation when a scheduled stop is reached ("There you are!") | [invitations.md](invitations.md) | behavior **verify** (A2) |
| **Quests** (letters with `%item quest <id>`, confirmed; runtime quests with a target NPC) | a `NeedsHelp` motive asks for something real, and the quest log tracks it; completion already writes `QuestHelped` | [motives.md](motives.md), [invitations.md](invitations.md) | runtime creation **verify** (A3) |
| **Special orders** (`requester`, confirmed) | bigger, multi-day requests | [invitations.md](invitations.md), "Later" | data format **verify** (A3) |
| **Phone calls** (1.6) | a call to the farmhouse phone: between a letter and a visit, for `Worried` or `MissingYou` | [invitations.md](invitations.md) | asset and conditions **verify** (C13) |
| **Trigger actions** (1.6) | data-driven mail, conversation topics and friendship changes, also from letters (`%action`, confirmed) | [rollout.md](rollout.md) | action names **verify** (C14) |
| **Conversation topics** (`autoGenerateActiveDialogueEvent`, confirmed) | make a piece of news a few-day town topic | [invitations.md](invitations.md) | needs new dialogue keys: Sid's call |
| **Portraits** (`$a`, `$s`, `$h`) | a hurt line shows the sad face | [text.md](text.md) | parsing **verify** (C15); needs a sanitizer rule change: Sid's call |

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

Not started. The verification pass (DeepSeek, from 2026-10-01) settles the **verify** marks;
update this file and the feature specs with its findings.

## Open questions

- May the mod add dialogue keys for conversation topics and its own scheduled-stop lines? They are
  new keys, not edits to vanilla lines, but they live in vanilla dialogue assets. Recommendation:
  yes for new keys only, behind their switches.
- May the template system append a portrait code (`$s`) after sanitizing? The model would never
  choose it. Recommendation: yes, from a fixed list per emotion.
- Which signals come first? Recommendation: dialogue answers (A1), heart events (B6) and garbage
  cans (B5), because they are the player's own choices and work in shadow mode today.
