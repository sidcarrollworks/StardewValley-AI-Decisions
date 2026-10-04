# 3. Initiation ladder

**Status: retired (2026-10-03, D31).** The motives ([motives.md](motives.md)) decide every attempt
now; the mod no longer runs the ladder. Kept for reference below. Before that: **done (shadow).** Urge, rungs, caps, response windows, escalation after being ignored,
conversations as responses, and Approach from a distance (Find) are built and logged. No rung does
anything in the game yet. Brief goal 3 and design decision 3; D16, D17; architecture, "Initiation
ladder".

## Player-visible behavior

Shadow (today): `[shadow] Abigail would try Emote (...)`, `[shadow] Abigail: Emote ignored (...)`,
`[shadow] Abigail would go looking for you at ...`.

Live, per rung (each behind its own switch, [rollout.md](rollout.md)):

| Rung | What the player sees | Game call (checked in the 1.6.15 decompile) | Only when |
|---|---|---|---|
| `Emote` | an emote over the NPC's head | `npc.doEmote(int whichEmote, bool playSound, bool nextEventCommand = true)` (`Character`). Id by mood, from the `Character` constants: `exclamationEmote` 16 by default, `heartEmote` 20 at 8+ hearts, `questionMarkEmote` 8 after being ignored (also available: happy 32, sad 28, blush 60, music note 56) | the player is in the NPC's location (it is "Near" by definition) |
| `Bubble` | a short line above the head | `npc.showTextAboveHead(string text, Color? spriteTextColor = null, int style = 2, int duration = 3000, int preTimer = 0)` with a bubble template ([text.md](text.md)) | same location |
| `Approach` near | the NPC turns and walks a few tiles toward the player, then resumes its day | face the player, then `npc.temporaryController = new PathFindController(npc, location, tile, facing) { NPCSchedule = true }` to a free tile next to the player's last seen spot, within the same location; when it finishes, the game itself calls `checkSchedule(Game1.timeOfDay)` and the NPC catches up on schedule steps queued meanwhile (see [find.md](find.md), "Travel") | same location, no event or menu, `Context.CanPlayerMove` |
| `Approach` from a lead | the NPC walks to where it believes the player is, in the map it is already in | as `Approach` near | the lead is in the NPC's current map |
| `QueuedLine` | the next time the player talks to the NPC, it opens with the line | `setNewDialogue(new Dialogue(npc, key, text), add: true, clearOnMovement: false)` ([intents.md](intents.md)); the planned intent if there is one, else a "missed you" template | any time; the game clears it at day end |
| `Mail` | a letter the next morning | register the letter in `Data/mail` via `helper.Events.Content.AssetRequested` (`e.Edit(asset => asset.AsDictionary<string, string>().Data[id] = text)`), then `Game1.addMailForTomorrow(id)`, which fills the per-farmer `mailForTomorrow` set; the game moves it to the mailbox during the new-day processing | hearts >= 2 |
| `ForcedDialogue` | a dialogue box opens unprompted | `setNewDialogue(dialogue, add: true)` then `Game1.drawDialogue(npc)` (it opens a `DialogueBox` on the top of the NPC's stack) | same location within 3 tiles (the NPC's own first-hand view), no menu, no event, player free to move; at most once a week across all NPCs |
| `Visit` (new, step 6) | the NPC leaves what it is doing (a shop may close) and goes to another map, maybe the farm, to find the player; 1 to 2 a week across the town | a one-off cross-map path, then restore the schedule ([find.md](find.md)) | urge >= 0.90, lead in another map, hearts >= 2, 9:00 to 20:00, caps in [find.md](find.md) |

A live attempt that the game refuses (menu open, event running, NPC busy) is not made and not
counted; the ladder is told with a new `NoteSkipped(npc, tick)` so the urge is unchanged.

## Data model

Done: `InitiationStep` (Emote 0 .. ForcedDialogue 5, saved as ints: append only), `InitiationInput`,
`InitiationEvent`, `InitiationOptions`, per-NPC state and global counters in `InitiationLadder`
(`ToJson`/`FromJson`, saved under `ladder`).

Planned:
- `InitiationStep.Visit = 6`, appended after `ForcedDialogue`, and visit counters in the ladder JSON
  (defaults for old saves); details in [find.md](find.md). Adding the step means: a seventh
  `StepThresholds` entry (0.90), `Available`, `ResolveAt` (the trip decides), the loop bound in
  `Candidate` (today `rung <= (int)InitiationStep.ForcedDialogue`), the caps, and `LadderDto`.
- Live execution needs one record handed from the ladder worker to the game thread: the existing
  `InitiationEvent` with `Kind == "Attempt"` is enough (step, NPC, lead). The game thread executes
  it; the worker never touches `Game1`.
- `Mail` attempts may become invitations ([invitations.md](invitations.md)); the step stays `Mail`,
  and the letter's kind is decided when it is written.

## Triggers and game hooks

Done: `TimeChanged` builds inputs from memory and enqueues; `Drain` returns events; `MenuChanged`
enqueues a response.

Planned, live:
- The game thread executes drained `Attempt` events for switched-on rungs, after checking the
  "only when" column against the current game state. Checking live state to decide whether the game
  *can* show something is allowed; it does not decide *whether the NPC wants to* (D2).
- `Mail`: the letter text is built when the attempt is drained, registered for tomorrow, and the
  response is the player opening it or talking to the NPC before the end of the next day. Opening
  is easy to see: the mailbox adds the letter's id to `Game1.player.mailReceived` when it opens it,
  and the open `LetterViewerMenu` carries it in `mailTitle`.

## Laya questions

| Question | Type | Fallback |
|---|---|---|
| "should <npc> try to get the player's attention with <step> now?" (done) | `noul` | 0.5 |
| `Visit` only: "would <npc> drop what they are doing right now and go looking for the player?" ([find.md](find.md)) | `noul` | 0.5 |

Planned change: the state becomes the shared NPC card plus the ladder facts ([laya.md](laya.md)),
so the model sees the NPC's temperament (manners, shyness) and not only numbers. A shy NPC should
answer lower for `Bubble` and `ForcedDialogue`, higher for `Mail`.

## Deterministic rules

All done; see D16 and architecture, "Initiation ladder": the mildest step at or above the rung that
the urge allows and that is available; caps (2 per NPC a day, 6 in total, 2 queued lines, 1 letter,
1 forced dialogue a week); 6-tick cooldown; settle before the rollover; the draw is FNV-1a uniform
below `p`.

Planned additions:
- **Motives replace the urge** ([motives.md](motives.md), D24): no urge number decides any more.
  An attempt needs a motive with a subject, and effective boldness (temperament boldness +
  familiarity with the subject + the motive's intensity) at or above the act's cost. The steps
  become acts with costs (`StepThresholds` becomes `ActCost`); hostile acts cost more. Clear calls
  skip the model; close calls ask a reworded Laya question and the day's mood tips them. Rung
  escalation becomes frustration (bold characters push harder after being ignored, shy ones back
  off), `IgnorePenalty` and `RespondRelief` become a motive being used up, and the intent boost
  becomes the `News` motive. Caps and cooldowns stay, with 2 of the day's 6 attempts reserved for
  strong motives. The saved `Urge` stays in the format, unused.
- **Temperament floor** (deterministic, before the model): NPCs whose `Data/Characters`
  `SocialAnxiety` is `Shy` (`NpcSocialAnxiety.Shy`; read with `npc.GetData()`) never use `Bubble` or `ForcedDialogue`; they skip to the
  next available step. Their news shows up as letters instead, matching brief decision 6. With
  motives this becomes mostly a consequence of low boldness; keep the hard floor until playtest
  logs show the boldness costs alone do the same, except that a very strong feeling may then break
  it (Sid, 2026-10-01: intensity can override shyness).
- **Spouse and children:** excluded from the ladder when live (they have their own game logic).
  Children are `Data/Characters` `Age == NpcAge.Child` (Jas, Vincent); the player's own children
  are the separate `StardewValley.Characters.Child` class and aren't villagers.
  In shadow they stay, for tuning data.
- **Not during events or festivals:** no attempt while `Game1.eventUp` is true (any event or
  cutscene, festivals included) or `Game1.isFestival()`. SMAPI's `Context.CanPlayerMove` is
  `IsPlayerFree && Game1.player.CanMove`, where `IsPlayerFree` needs no menu and no dialogue, and
  during an event is true only at a festival, so check `eventUp` separately. Shadow logs these too, marked `(event running: would wait)`.

## Tuning constants

All in `InitiationOptions` (`src/NpcInitiation/Models.cs`), none saved: `BaseGainPerTick` 0.004,
`HeartsGainPerTick` 0.0005, `IntentBoost` 0.25, `OvernightFactor` 0.5, `IgnorePenalty` 0.2 (these
five and `RespondRelief` go when motives land; `StepThresholds` becomes `ActCost`,
[motives.md](motives.md)),
`RespondRelief` 0.5, `ResponseWindowTicks` 6, `CooldownTicks` 6, `AskAgainAfterTicks` 6 (a
turned-down step and lead waits that long before the model is asked again), `MaxAttemptsPerNpcPerDay` 2,
`MaxAttemptsPerDay` 6, `MaxQueuedLinesPerDay` 2, `MaxMailPerDay` 1, `MaxForcedPerWeek` 1,
`StepThresholds` {0.30, 0.45, 0.60, 0.70, 0.80, 0.95}. New: `ForcedMaxTiles` 3; a seventh threshold 0.90 for `Visit` and the visit caps ([find.md](find.md)).

## Acceptance tests

Existing: `tests/NpcInitiation.Tests` (`InitiationLadderTests`, `LadderTimingTests`,
`BackgroundLadderTests`, `FindTests`).

To add:
- `Visit` appended as 6: a ladder saved before it loads unchanged; the escalation loop reaches it.
- Shy NPC: never offered `Bubble` or `ForcedDialogue`; with urge 0.5 and a first-hand view it picks
  `Approach` if available, else nothing.
- `NoteSkipped` leaves urge, rung and counters unchanged.
- An executor test in the mod is not possible (no game in tests); instead keep a pure
  `LiveGate.CanShow(step, gameFacts)` in `src/NpcInitiation` that takes a small record of game facts
  (same location, menu open, event running, distance from the NPC's own view) and test it.

In-game, per rung as it goes live: the action appears; an ignored attempt shows `IgnoredBy` in the
diary and escalates next time; talking relieves urge (log); caps hold over a full day; nothing
happens during a cutscene or festival; the NPC resumes its schedule after an approach and reaches its
next destination on time.

While a rung is shadow, `InitiationOptions.RecordIgnoredBy` stays false: the attempt never
appeared, so no `IgnoredBy` diary line is written (the urge penalty and escalation still apply).
The week review caught this: shadow `IgnoredBy` lines were the highest-weighted everyday news and
would have made live NPCs complain about attempts the player never saw.

## Status

Done: `src/NpcInitiation/InitiationLadder.cs`, `BackgroundLadder.cs`, `Models.cs`, `PlayerSearch.cs`;
`ModEntry.RunLadder`, `OnMenuChanged`. Not started: every live execution path, temperament floor,
`NoteSkipped`, `LiveGate`.

## Open questions

- Handing an NPC back to its schedule after a custom walk: the decompile shows how
  ([find.md](find.md), "Travel"), and that `PathToOnFarm`, named in the brief, doesn't exist in 1.6.
  An in-game experiment still has to confirm it before `Approach` goes live.
- Should a response to a letter require reading it, or is talking to the NPC enough? Recommendation:
  either counts.
