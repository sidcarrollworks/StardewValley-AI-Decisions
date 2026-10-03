# Roadmap

What to build next, in order, and what Sid has decided. Each step is one or more PRs off `main`,
with tests and doc updates (`AGENTS.md`, "Workflow"). Sizes: **S** is one small PR, **M** a few days
of agent work, **L** several PRs.

## Where things stand

Steps 1-9 of the brief are done in shadow mode, plus a console heartbeat (PR #3), diary enrichment
parts 1 and 2 (PRs #5 and #6, gifts/quests/festivals via read-only Harmony postfixes) and the Laya
decision layer (PR #7: DecisionState + the NPC card, batching, the varied fake, health
re-check/warm-up/short-circuit, budget pass-through, the morning wait, heartbeat counters, and the
eval set — typed-decisions 5/6 vs english 3/6); 624 tests pass at the PR #7 head. Nothing is live.
Laya runs on Sid's GPU (~45 ms a question); what remains before a live decision is the in-game
week with Laya on, plus the speak-question rewording experiments the eval points at.

## Order

| # | Step | Size | Spec | Depends on |
|---|---|---|---|---|
| 1 | **Diary enrichment, part 1:** `DiaryDetail`, `MemoryStore.Note`, `Talked`, `PassedBy`, `BirthdayForgotten`, newsworthiness in the planner (drops housemates at home, ranks by news) | M | [diary.md](diary.md), [intents.md](intents.md) | - |
| 2 | **Diary enrichment, part 2:** Harmony set-up (read-only postfixes), `GiftReceived` and `SawGift`, `QuestHelped`, `Festival`/`MissedFestival` | M | [diary.md](diary.md) | 1 |
| 3 | **Laya in practice (shadow):** `DecisionState` and the NPC card within the 512-token budget, the varied fake, health re-check and short-circuit, budget pass-through, batching, the morning wait, the eval set and the checkpoint A/B. (Laya already runs on Sid's GPU.) | M | [laya.md](laya.md) | 1 (for real news in the eval) |
| 4 | **Voices and lines:** voice notes for every vanilla villager (`data/voices.json`), then the lines in `i18n/default.json` (SMAPI's translation file) with a bucket per NPC and kind, tone fallbacks, per-channel sanitizers and lengths. Claude writes, Sid edits | L | [text.md](text.md) | 1, 2 (the kinds to write for) |
| 5 | **Intents ready to ship:** plan persistence, recent lines, novelty and cite cooldowns, one topic per event | S | [intents.md](intents.md), [persistence.md](persistence.md) | 1 |
| 6 | **First live release:** (changed 2026-10-02, D30: emotes and bubbles go first, built in `src/NpcLive`; then) `Live` config, `[live]` logs, circuit breaker, console kill switch, the single-player guard, then `IntentLines` on, and nothing else | M | [rollout.md](rollout.md), [multiplayer-compat.md](multiplayer-compat.md) | 4, 5, and Sid's word |
| 7 | **Routine and gossip fixes:** `UnlockThreshold` not saved, decay, family priors, ambient gossip and `Heard` | M | [routines.md](routines.md), [ledger-gossip.md](ledger-gossip.md) | - (parallel with 3-6) |
| 8 | **Letters and low rungs:** mail plumbing, then letters in three kinds including invitations (shadow, then live), then `Emote`, `Bubble`, `QueuedLine` live one at a time; temperament floor; `LiveGate` | L | [invitations.md](invitations.md), [ladder.md](ladder.md) | 6 |
| 9 | **Newcomer week:** new saves only, plus the `npcmod_newcomer` test command | L | [newcomer-week.md](newcomer-week.md) | 7 (spreading), 8 (mail) |
| 10 | **Day length:** off by default; Sid tries 20 minutes | S | [day-length.md](day-length.md) | none; any time |
| 11 | **Travel spike, then movement:** an in-game experiment that walks an NPC to another map and restores its schedule; then `ApproachNear` and `ForcedDialogue` | M | [ladder.md](ladder.md), [find.md](find.md) | 8 |
| 12 | **Visits:** the `Visit` rung, 1 to 2 a week, shops close while the keeper is out; shadow first, with the pacing test | L | [find.md](find.md) | 11 (live part); the shadow part can start after 7 |
| 13 | **Multiplayer research:** answer the list in [multiplayer-compat.md](multiplayer-compat.md) from SMAPI docs and decompiled 1.6, and estimate | S | [multiplayer-compat.md](multiplayer-compat.md) | none; any time |
| 14 | **Motives:** a motive plus enough boldness for the act replaces the urge (D24); elastic stresses from the diary and saved regard; mood tips close calls; gossip juiciness (D25); the playtest log first, so tuning has data; hurt and grudges in shadow, then the friendship penalty behind its own switch. Part 1, the playtest log, is **done** (PR #24); part 2, the engine with its runner, regard, the viewer's view and the motives records, is **built** in `src/NpcMotives` (2026-10-02); part 3, wiring it into the mod beside the ladder, is **done** (2026-10-02) and has run in two playtests; history at install's seeding rule is **built** (`RegardHistory`, the game reader is local); retiring the urge is **done** (2026-10-03, D31); then gossip juiciness and the penalty's switch | L | [motives.md](motives.md), [temperament.md](temperament.md) | 1, 2 (diary kinds); can run in shadow before 6 |
| 15 | **Trades:** the offer table, barter shops through `Data/Shops`, the question box, `WantsToTrade` | M | [trades.md](trades.md) | 8 (mail and the live switches); table text drafted with step 4 |
| 16 | **Debug tools:** console commands first, then the NPC Minds tab (spike the tab; fall back to a hotkey menu) | M | [debug-tools.md](debug-tools.md) | none; commands can start right after 1 |
| 17 | **Romance:** status milestones, partner multipliers, jealousy, date invitations, spouse behavior | M | [romance.md](romance.md) | 14 (motives), 8 (invitations) |
| 18 | **Town life:** NPC chats as bubbles (shadow, then live), regard between NPCs, then meet-ups (one-day schedules) and looking for each other | M | [town-life.md](town-life.md) | 7 (ambient gossip); 19 for meet-ups; 11 for same-day looking-for |
| 19 | **Vanilla sources:** settle the verification list (running), then the read-only signals first (dialogue answers, heart events, garbage cans, festivals, movies, conversation topics), the regard-seed tool, then the one-day schedule channel (for in-town stops: meet-ups and the notice board), then requests as quests (`NeedsHelp`, `Requests`). Farm visits by appointment are **deferred** (Sid, 2026-10-02). Facts verified in PR #18; in-game checks pending | L | [vanilla-sources.md](vanilla-sources.md), [invitations.md](invitations.md) | signals: 2 (Harmony set-up); can run in shadow alongside 14 |
| 20 | **Character spread:** the spread eval over all 34 cards (`sidecar/eval/run_spread.py`, results and `data/laya-calibration.json`) and the viewer's spread panel are **done** (PR #20, #21); corrections are off, since only the news question is flat and it doesn't follow its trait either (its personality goes into code). Left: re-run when the card or a question changes, and pick the checkpoint per question before 14's close calls go live (D27) | S | [laya.md](laya.md), [debug-tools.md](debug-tools.md) | none for the eval; the panel any time; corrections before 14's close calls go live. Low urgency in the first season (Sid, 2026-10-02): few close calls happen early |
| 21 | **The town notice board** (Sid, 2026-10-02: preferred over farm visits; possibly the mod's real draw): first the console experiment (type a note, see how Laya says each villager would react), then the board in shadow (villagers "would post" and "would react"), then the board itself behind `NoticeBoard`. The experiment's core is **built** (2026-10-02: `src/NpcBoard`, `sidecar/eval/run_notes.py`); the console command is next | L | [notice-board.md](notice-board.md) | the console experiment: none; the board: 14 (stresses), 19 (one-day schedules) |

**Later, not scheduled:** the player's bedtime journal (Sid, 2026-10-02: an idea to talk over later; [journal.md](journal.md)); farm visits by appointment (deferred by Sid, 2026-10-02; [invitations.md](invitations.md)); phone calls and special orders ([vanilla-sources.md](vanilla-sources.md)); pairing with content mods ([multiplayer-compat.md](multiplayer-compat.md), "Later"); release
packaging, including starting the Laya server with the game ([laya.md](laya.md), "Sidecar
lifecycle"), filling `UpdateKeys` in `mod/StardewNpcMod/manifest.json` (empty today) for the site it's
published on, and a release zip (`EnableModZip`); multiplayer itself.

**Drafted 2026-09-30:** seed temperaments and Ekman emotion biases for the 34 villagers ([temperament.md](temperament.md)),
computed by `tools/TemperamentExtractor` from their dialogue and `Data/Characters` traits; a table
for Sid to review, not read by the mod yet.

**Done 2026-09-30:** a local decompile of the game settled the spec's "verify" items
([README.md](README.md), "Verifying game facts"; findings in `stardew-source-notes.md`). The biggest
results: the game already has a "walk somewhere, then resume the schedule" pattern, which makes
visits much less risky than feared (step 11's spike now confirms behavior rather than discovering
it); shops really do close while their keeper is away; unread lines are cleared by the game every
night; and planned lines can be wiped by conversation topics, which step 6 must handle.

### Why this order

- **Diary first**, because every later step feeds on it. Laya can only be judged on interesting
  choices (with today's diaries the right answer is nearly always "nothing worth saying"), the lines
  need to know which kinds exist, and the first live feature is only worth shipping if the lines say
  something.
- **Laya third, in shadow.** It comes after diary part 1 so its eval set uses real news. Sid can
  install the sidecar any time (`sidecar/README.md`); nothing in the mod waits for it.
- **Voices and lines are an L** because Sid wants every character to sound like themselves: that is
  a bucket per vanilla villager per kind, not a handful of generic templates. It can be split into
  PRs by group of characters, so Sid reviews a few voices at a time.
- **A first live release (steps 4-6) comes before newcomer week and visits.** Newcomer week needs
  mail, gifts and placing an NPC on the farm at once. Shipping the simplest live feature first
  (planned lines, shown only when the player talks to the NPC) proves the live switches, the sanitizers and the
  kill switch on something low-risk.
- **Letters come before newcomer week**, since newcomer week's letters and notes reuse the same mail
  plumbing, and invitations are Sid's ask.
- **Visits last**, because they need the travel spike. Their shadow logging (who would visit, how
  often) can start as soon as the ladder change is in, which is the time to tune the 1-to-2-a-week
  target.
- **Motives (14) right after diary enrichment** in practice: they turn the richer diary into
  behavior, and they run in shadow, so they can land before the first live release even though the
  table lists them later. The friendship penalty goes live last among the motive parts.
- **Trades (15) after letters (8)**, because they reuse the live-switch plumbing and a letter is one
  way a trader reaches out.
- **Debug tools (16) early in practice:** the console commands are small and make every later step
  easier to judge; build them right after diary part 1. The menu tab follows.
- **Romance (17) and town life (18)** build on motives, invitations and gossip, so they come after
  those; their shadow parts can start as soon as the pieces they read exist.
- **Day length and multiplayer research are independent** and small: good tasks for whoever has a
  free slot (DeepSeek included).

## Decided by Sid (2026-09-30)

| # | Question | Decision | Reflected in |
|---|---|---|---|
| 1 | Harmony for gifts? | Yes. Read-only postfixes, following the wiki's guidance | [diary.md](diary.md), "Harmony" |
| 2 | What goes live first? | Overnight lines only | [rollout.md](rollout.md) |
| 3 | Newcomer week on existing saves? | New saves only, plus a test command | [newcomer-week.md](newcomer-week.md) |
| 4 | Day length default? | Vanilla; Sid tries 20 minutes, then decides on 24 | [day-length.md](day-length.md) |
| 5 | NPCs walking to another map to find the player? | Yes, with a very strong urge; 1-2 visits a week on average; some characters more prone than others, decided by the model from character and relationship; a shop closing meanwhile is fine if rare | [find.md](find.md), [ladder.md](ladder.md) |
| 6 | Start the Laya server with the game? | Not while developing; yes for a release | [laya.md](laya.md), [config.md](config.md) |
| 7 | Multiplayer? | Single-player for now; research what it would take | [multiplayer-compat.md](multiplayer-compat.md) |
| 8 | Who writes the lines? | Claude writes, Sid edits; language must match each character | [text.md](text.md) |
| 9 | NPC mail | NPCs send letters, including invitations to visit; quest-like requests later, using vanilla mechanics where possible | [invitations.md](invitations.md) |
| 10 | Other mods | Consider pairing with a content mod once the system works | [multiplayer-compat.md](multiplayer-compat.md), "Later" |
| 11 | Can characters be hurt or annoyed? | Yes; and repeatedly treating a character badly costs friendship points | [motives.md](motives.md) |
| 12 | Trading | Villager-style trades that give characters another reason to seek the player out; the vanilla barter shops cover it | [trades.md](trades.md) |
| 13 | Debug view | An in-game menu tab showing NPC data, plus console commands | [debug-tools.md](debug-tools.md) |
| 14 | Romance | The mod should work well with dating and spouses | [romance.md](romance.md) |
| 15 | NPCs with each other, weather and seasons | Yes to both | [town-life.md](town-life.md), [motives.md](motives.md) |
| 16 | What makes a character act? (2026-10-01) | A motive and enough boldness for the act, eased by familiarity and strong feeling; no idle urge (D24) | [motives.md](motives.md) |
| 17 | How does gossip spread? (2026-10-01) | By juiciness, fading fast; hearsay sticks only when confirmed (D25) | [ledger-gossip.md](ledger-gossip.md) |
| 18 | Vanilla data and mechanics (2026-10-01) | Get as much as possible from the vanilla game, and act through vanilla channels; farm visits by appointment through one-day schedules, with stand-ups remembered (D26) | [vanilla-sources.md](vanilla-sources.md), [invitations.md](invitations.md) |
| 19 | Do villagers' model answers trend alike? (2026-10-02) | Measure it: a spread eval over all villagers' cards and a spread panel in the viewer; correct per question only where the data shows it's flat | [laya.md](laya.md), [debug-tools.md](debug-tools.md) |
| 20 | Farm visits or the notice board first? (2026-10-02) | The notice board; farm visits by appointment are deferred | [notice-board.md](notice-board.md), [invitations.md](invitations.md) |

## Still open (none block the next steps)

- Should accepting an invitation, or a future request, ever change friendship beyond what the
  vanilla conversation gives? Today the mod never changes friendship. Recommended: no.
- Which content mods Sid plays with, so those are supported first.
