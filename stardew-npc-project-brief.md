# Stardew NPC Mod: Project Brief

## Paste as Project instructions

This project is a SMAPI (C#) mod for Stardew Valley 1.6 that makes NPCs feel less scripted. NPCs remember what happened, start conversations, and look for each other and the player using limited, aging knowledge. Typed decisions come from Laya (Convai, open-weight, run locally as a separate process). The model never generates text.

Working rules:
- Separate what is verified in source or docs from what is recalled. Mark recalled items "verify".
- The game's true positions for the player or any NPC must never be inputs to an NPC decision. Only ledger entries, aged and coarsened, may be.
- In live-game code, "co-located" means the same location AND within 8 tiles (Chebyshev square), not the same region. The 8-tile radius is a placeholder to tune.
- Decay and coarsening are done by deterministic code, not by a model.
- Any generated text must be sanitized before it reaches a dialogue string (see Constraints).
- Prefer shadow mode (log what the mod would do, change nothing) before any behavior goes live.
- Read `stardew-source-notes.md` before answering questions about game internals.

## Goal

NPCs that:
1. Carry a private diary of what they witnessed and did.
2. Say something new the day after something happens (an "intent" stored at sleep, delivered next morning).
3. Start contact with the player instead of waiting to be clicked.
4. Track each other and the player through a "last seen" ledger, asking neighbors when someone is missing.
5. Learn each other's routines from co-presence, with family and friends starting with a rough prior.

## Decision layer

- **Laya** (Convai Innovations, Apache-2.0): open weights, local. Three question types: `choice`, `score` (ordered), `noul` (yes/no probability). Context 512 tokens (english checkpoint) or 1024 (`typed-decisions`). Needs Python 3.10+, so it runs as a separate local process (`laya-serve`, `POST /v1/systemone`) called over localhost; see `sidecar/README.md`. Verified from the repo at v0.3.22.
- **Settled (Sep 2026): Laya only; Jev is dropped.** Open-source and local, so the model itself can be modified, and players carry no API cost.
- Text: a short sentence from an LLM overnight, or templates. Laya only decides what, who, and how.
- Caution: the Hugging Face Laya article is a community post that links mostly to thejevai.com, which is not TypeSafe's domain. Rely on the Laya GitHub repo and model card.

## Design decisions so far

1. **Diary.** The game keeps only counters and flags. The mod keeps a per-NPC event log and saves it through SMAPI's save-data API.
2. **Overnight intents.** At sleep, decide which NPCs have something to say and about what. On day start, push the line with `addExtraDialogues` or `setNewDialogue(text, add: true, clearOnMovement: true)`. Each line must cite a diary entry. Cap at 1 to 3 NPCs a day. Add a novelty bonus and cooldowns. Reject lines too similar to vanilla dialogue or the NPC's last ~20 lines.
3. **Initiation ladder** (mildest fitting step): emote, bubble (`showTextAboveHead`), approach (`moveTowardPlayer` or `PathToOnFarm`), queued line, mail, forced dialogue box (rare). Per-NPC urge value. Daily caps. Ignored attempts lower urge and enter the diary.
4. **Last-seen ledger.** One entry per subject per observer, including the player. Recorded on the ten-minute tick when co-located. Detail degrades with age: named spot (the tile), then location, then region, then "earlier today", then gone from the next 6:00. Gossip never replaces fresher knowledge (a later sighting wins; for the same sighting, fewer hops win) and never passes on a forgotten view. Gossip passes on the already-coarsened view and never adds detail. Two-hop cap on asking. Simulate silently off-screen. Render text only when the player is in the location.
5. **Routines.** Counts by time block and region, learned from co-presence. Family and friends start with a coarsened, time-jittered prior built from real schedules. Pairs unlock after enough co-presence, replacing a global silent week. Hearts set how fast an NPC learns the player's routine.
6. **Newcomer week.** News of the player spreads through the ledger at hearsay level. Some NPCs visit to introduce themselves with a small gift. Visitors vary per save (seeded). A letter announces the visit. Shy NPCs send a note. Gift items are granted in code, never by generated text.
7. **Day length.** Target 24 real minutes: `Game1.realMilliSecondsPerGameMinute = 1200` and `realMilliSecondsPerGameTenMinutes = 12000`. Test 18 to 20 minutes first.

## Constraints and risks

- Dialogue strings can run trigger actions and grant items. Strip `#`, `$`, `%`, `{`, `[` from generated text.
- Cross-location travel uses a precomputed route table plus queued schedule paths. `ignoreScheduleToday` makes `checkSchedule` return immediately, and `PathToOnFarm` sets it. Any custom travel must restore the schedule afterward.
- The friendship record is created the first time the player clicks an NPC. A visit with no click may not register for the introductions quest. Test early.
- Homogenization: one model writing every NPC tends to blur voices. Give each NPC a short voice sheet.

## Open questions

- How shop opening hours are enforced under a longer day. Answered (1.6.15 decompile): opening hours are checked against the game clock (`OpenShop` action), so a longer day doesn't change them.
- The code that moves an NPC once `isWalkingTowardPlayer` is set (not found in NPC.cs).
- ~~Where the per-item gift log lives in the Farmer class.~~ `Farmer.giftedItems` (1.6.15 decompile).
- ~~Whether off-screen NPCs' `currentLocation` updates in real time.~~ Yes, on the host: every location's NPCs update each tick (1.6.15 decompile).

Answers and many more facts: `stardew-source-notes.md`, "Checked in the 1.6.15 decompile".

## Next steps

1. ~~Schedule extractor: raw schedules to region-by-time-block counts.~~ **Done** (see README): simulates a full year per NPC mirroring the verified 1.6 key order and command semantics, outputs region x block counts + routine priors; 61 tests; real 1.6 fixtures unpacked from the game.
2. ~~Ledger record and the age-based view function (C# stub).~~ **Done** (see `src/NpcMemory`): `Diary` (event log), `Ledger` (last-seen with age decay + two-hop gossip), `RoutineBelief` (co-presence routine learning + prior seeding + pair unlock); 77 tests. Built by three parallel subagents on deepseek-flash against a parent-authored contract.
3. ~~Shadow-mode logging harness.~~ **Done** (see `src/NpcShadow`): deterministic simulator drives the memory layer through co-located ticks and emits a shadow log (`Saw` / `Decayed` / `Unlocked` lines) over a span of days; 29 tests.
4. ~~SMAPI skeleton with the sidecar client.~~ **Done, compile-verified** (`mod/StardewNpcMod`): hooks `SaveLoaded` / `DayStarted` / `TimeChanged` / `Saving` / `ReturnedToTitle`; persists Diary/Ledger/RoutineBeliefs per save via `Helper.Data` (memory survives reload, not reset each day); live co-location each ten-minute tick (same location AND within 8 tiles, never same region); and a typed `IDecisionClient` (choice / score / yes-no) with a deterministic fake and a timeout-with-fallback wrapper, real Jev/Laya left as stubs marked "verify". 180 tests green at the time. Run in-game: persistence across save/reload verified by the user. Sidecar (Laya) not started. **Out of scope, not started:** the initiation ladder, newcomer week, day length.
5. ~~Overnight intents.~~ **Done, shadow mode** (`src/NpcIntents` + mod): at sleep the intent planner decides which NPCs will speak and about what (typed yes/no + choice via the decision client), renders templated first-person lines (sanitized), and shadow-logs them next morning. 36 tests (22 renderer + 14 planner), built by two parallel flash subagents against a parent-authored contract. The plan is held in memory only (survives sleep→morning, not save-and-quit); persist it before real delivery, and wire `RecentLines` for novelty once real delivery exists.

6. ~~Audit fixes.~~ **Done** (branch `claude/audit-fixes-initiation`): year-aware absolute ticks with a migration for older saves; calendar-day decay (anything from before today is Gone at 6:00); gossip never overwrites fresher knowledge, refuses self-gossip and Gone views; the named-spot stage stores the tile; the shadow harness uses same location, not region; memory is recorded from the NPC side (NPCs track the player and each other, hearts speed up learning the player's routine; one diary line per co-located span, capped); planned lines only cite the day just ended and use display names; model calls run on background tasks with a per-call timeout and a planning budget, never on the game thread or during the save. Ledger thresholds are no longer saved with the ledger.
7. ~~Laya client.~~ **Done**: `LayaDecisionClient` over `laya-serve` (choice / score / noul), per-call timeout with real cancellation, bearer key optional; `sidecar/` has run scripts and a smoke test. Not yet run against a live server. Open: whether `typed-decisions` or `english` answers NPC questions better (A/B once running).
8. ~~Initiation ladder.~~ **Done, shadow mode** (`src/NpcInitiation`): per-NPC urge, mildest fitting step with escalation after being ignored, per-NPC / daily / weekly-forced caps (letters and queued lines have their own small daily caps), cooldown, ignored attempts lower urge and go in the diary. Each step has a fitting response window: an hour for an emote, bubble, approach or forced dialogue; the rest of the day for a queued line (which quietly expires if never heard); the day after for a letter. Its only knowledge of the player is the NPC's own ledger view. Runs on a background worker in the mod and logs what it would do. A response is any conversation with the NPC, read from a dialogue box whose speaker is that NPC (verify in-game); talking also relieves urge and starts the cooldown.

9. ~~Finding the player.~~ **Done, shadow mode** (`MemoryStore.AskAround` / `LookFor`, `PlayerSearch`, the ladder's Approach): an NPC that misses the player (urge 0.45+, no first-hand sighting in the last hour) asks the NPCs it is with, under the gossip rules; its answer to "where is the player?" is its own sighting today, else a tip, else its habit for this hour (routine belief), else unknown. With a lead, Approach works from a distance, so a keen NPC goes looking before it writes. Logged as "asked X about you" and "would go looking for you at ...". The fish shop now maps to the Beach region (audit).

386 tests green after step 9. Docs for other models: `AGENTS.md`, `docs/architecture.md`, `docs/decisions.md` (open work is listed at the end of decisions.md).

Roadmap step 1 (diary enrichment part 1) landed in PR #5: `DiaryDetail`, `MemoryStore.Note`, the day-end `Talked`/`PassedBy`/`BirthdayForgotten` notes, `Newsworthiness` and the planner's news filter and ranking. 482 tests green.

**From here on**, the plan lives in `docs/spec/`: a spec for every feature and a prioritized roadmap (`docs/spec/roadmap.md`) with the decisions still open for Sid.

## Side ideas

- **Sims 4:** script mods are Python and hook autonomy. Wrap the autonomy pick: take the top 8 to 12 legal candidates, ask Laya to choose, and fall back on timeout. Run the model in a sidecar process, off the sim thread.
- **Civ 6:** weak fit. Its AI problem is competence, not character. Diplomacy judgments are the one place it might help.
