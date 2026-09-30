# 2. Overnight intents and line selection

**Status: partial (shadow).** Planning, selection and templated lines are done and logged. Newsworthiness,
novelty, persistence of the plan and in-game delivery are not started. Brief goal 2 and design
decision 2; D15; architecture, "Overnight intents".

## Player-visible behavior

- **Shadow (today):** at the 6:00 tick the log shows up to three
  `[shadow] <npc> would say: "<line>" (cited "<entry>" (sampled p=...))`.
- **Live:** the first time the player talks to a planned NPC that day, the NPC says the planned line
  before (or instead of) its normal dialogue. It is said once. At most three NPCs a day have one.
  Each line cites a real diary entry from the day before, so it is always true ("You gave me a
  sunflower yesterday", never a made-up event).
- If the player never talks to that NPC, the line is simply never shown. The ladder may still push for
  it ([ladder.md](ladder.md), `QueuedLine`), and an undelivered line never carries over to the next
  day.

## Data model

Existing (`src/NpcIntents/Models.cs`): `NpcMemorySnapshot(Npc, Voice, RecentDiary, RecentLines)`,
`IntentCandidate(Npc, Line, Source, Reason)`, `IntentPlan(Candidates)`.

Planned:
- `NpcMemorySnapshot` gains, at the end with defaults, `NewsContext? News = null` (see
  [diary.md](diary.md)) and `string Tone = "Neutral"` ([text.md](text.md)).
- `IntentCandidate` gains, at the end, `double News = 0`, the newsworthiness of the cited entry.
- **Saved plan:** `PlannedLine(Npc, DayIndex, Line, SourceKind, SourceSubject, Delivered)`, saved
  under a new save-data key `intents` ([persistence.md](persistence.md)), so a line planned last
  night survives save-and-quit. Today the plan is memory-only.
- **Recent lines:** per NPC, the last 20 delivered lines with their day and cited (kind, subject),
  saved under `recentLines`. Fills `RecentLines` (novelty) and the "already cited" penalty in
  newsworthiness. In shadow mode, "delivered" means "logged", so novelty can be tested before
  anything is live.

## Triggers and game hooks

| When | What | Thread |
|---|---|---|
| `DayEnding` | snapshot diaries (plus news context and recent lines), start `IntentPlanJob` | game, then background |
| every `TimeChanged`, and `DayStarted` | `CollectPlan`: non-blocking take; log; fill `_intentsToday`; (live) queue lines | game |
| live, when collected | for each planned NPC: `npc.setNewDialogue(new Dialogue(npc, "squid.StardewNpcMod:intent", line), add: true, clearOnMovement: false)` | game |
| live, `MenuChanged` | if the opened `DialogueBox.characterDialogue` is our `Dialogue` (compare the reference we pushed, or its translation key `squid.StardewNpcMod:intent`), mark it delivered, append to recent lines | game |
| the next day | nothing: the game drops it (below) | - |

What the 1.6.15 code does with it (decompile, `NPC.cs`, `Dialogue.cs`, `Game1.cs`):
- The `string` overload of `setNewDialogue` takes a **translation key**, not text. Raw text needs the
  `Dialogue(NPC speaker, string translationKey, string dialogueText)` constructor, whose first string
  is only a label. The brief's `addExtraDialogues` is really `addExtraDialogue(Dialogue)`.
- `clearOnMovement` must be **false**: it sets `removeOnNextMove`, and the line is popped whenever the
  NPC starts a new path, is warped, or finishes a route animation, which on a schedule day is almost
  immediately.
- **Unread lines expire on their own:** every NPC's dialogue stack is reset during the new-day
  processing (`ResetCharacterDialogues`), so nothing is left for the mod to remove.
- **Risk:** talking to an NPC runs `checkForNewCurrentDialogue`, which clears the stack when a
  conversation topic or location line applies, and events clear it too. A planned line can therefore
  be wiped before the player reads it. On each `MenuChanged` for that NPC, if our line was not shown and is
  no longer in `npc.CurrentDialogue`, push it again (at most once more that day). Test this in-game
  before `IntentLines` goes live.
- Text is split into pages by pixel height (a 1200 x 384 box, about 460 px of it portrait), so a long
  line becomes an extra page rather than being cut. `#` separates lines in dialogue text and the
  sanitizer strips it.

## Laya questions

Asked in `IntentPlanner`, per NPC with news, on the plan job's thread:

| Question | Type | State ([laya.md](laya.md)) | Fallback |
|---|---|---|---|
| "Does <npc> have something worth telling the player today?" | `noul` | NPC card + up to 5 news items | 0.5 |
| "Which of these would <npc> most want to bring up?" | `choice`, options = the news items (at most 5) | same state | uniform |

Both questions share one state, so they should go in one request once the client supports batching
([laya.md](laya.md)). The speaker ranking stays: highest yes/no first.

## Deterministic rules

Today (done): skip `TriedToReach`; only entries from the day just ended; newest 5 distinct entries;
sample from the model's probabilities with one `Random(seed)`; seed = DayIndex; render with
`daysAgo`; drop a line equal to a recent one; keep the top 3 by yes/no, ties by name.

Planned changes, in order:
1. **Newsworthiness filter** before asking ([diary.md](diary.md)): entries under `MinNews` are never
   options. Options are the top 5 by news score (ties: newest first), not the newest 5.
2. **Tie-break by news:** ranking is yes/no probability, then the best news score among the NPC's
   options, then name. With the Fake backend (all 0.5) the speakers become the NPCs with the best
   news, not the alphabetically first.
3. **Novelty:** a line is rejected if it equals one of the NPC's last 20 lines, or if the same
   (kind, subject) was cited by this NPC in the last `CiteCooldownDays` (3). A rejected NPC falls to
   its next-best option once, then is skipped.
4. **One topic per subject across NPCs:** if two speakers would cite the same event (both saw the
   player's gift to Haley), keep the one with more hearts; the other falls to its next option.
5. **Vanilla overlap:** the brief asks to reject lines "too similar to vanilla dialogue". Templated
   lines are ours, so overlap is unlikely; defer until LLM text exists, which is not planned
   (D12).

## Tuning constants

| Name | Default | Where | Saved |
|---|---|---|---|
| `SpeakThreshold` | 0.5 | `IntentPlannerOptions` | no |
| `MaxNpcsPerDay` | 3 | `IntentPlannerOptions` | no |
| `MaxRecentDiaryEntries` (options) | 5 | `IntentPlannerOptions` | no |
| `SkipKinds` | `TriedToReach` | `IntentPlannerOptions` | no |
| `RecentLinesKept` | 20 | new | no (the lines are saved, the count is not) |
| `CiteCooldownDays` | 3 | new | no |
| `PlanningBudgetMs` | 20000 | `ModConfig` | config |

## Acceptance tests

Unit (`tests/NpcIntents.Tests`):
- Options are chosen by news score, not recency; a zero-news entry is never an option.
- Fake backend: the speakers are the three NPCs with the best news; names only break exact ties.
- A line already in `RecentLines` is rejected and the NPC falls to its next option once.
- The same (kind, subject) cited two days ago is rejected; four days ago it is allowed.
- Two NPCs citing the same event: only the higher-hearts one keeps it.
- `PlannedLine` and recent lines round-trip through JSON; a plan from an earlier day is discarded
  on load.

In-game:
- Shadow: sleep, then check that lines cite gifts, quests and festivals when they happened, and that
  no line repeats within three days.
- Save and quit after 6:00, reload: the same planned lines are still pending (logged at load).
- Live (after Sid switches it on): talk to a planned NPC; the line appears once, then normal
  dialogue; talk again: no repeat. Sleep: an unread line is gone next day.

## Status

- Done: `src/NpcIntents/IntentPlanner.cs`, `IntentPlanJob.cs`, `LineRenderer.cs`, `PlaceNames.cs`,
  `LineSanitizer.cs`, `VoiceSheets.cs`; the mod's `StartPlanning` and `CollectPlan`; tests in
  `tests/NpcIntents.Tests` (60 at `c97a829`).
- Not started: newsworthiness ranking, novelty wiring (`RecentLines` is always empty today), plan
  persistence, delivery.

## Open questions

- How often a conversation topic or location line wipes a planned line before it is shown (see the
  risk above). Measure in shadow by logging when it would have happened.
- Should the planned line come before the NPC's normal daily line or replace it? Recommendation:
  before, as an added line, so vanilla dialogue (and its friendship gain) is untouched.
- Should married spouses get planned lines? Marriage dialogue has its own paths
  (`addMarriageDialogue`); leave spouses out of live delivery until tested.
