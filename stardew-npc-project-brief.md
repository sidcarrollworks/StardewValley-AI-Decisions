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

## Under Glass: current direction (2026-10-09)

The independent game in [`sim/`](sim/README.md) is now aimed first at an in-depth social
simulation viewed from above. Player gameplay comes afterward. The next milestone is a short run
whose people and choices are worth following: observers can see what someone remembers, imagines,
considers, intends, attempts and experiences. Feud/friendship counts and stability checks are
diagnostics, not proof of an interesting town; coherent extreme outcomes are allowed.

Sid reviewed the authored, Laya and local-generator recordings and finds the new direction
promising. The [current roadmap](docs/under-glass/roadmap.md) is the priority guide: concrete
encounter context and separate personal/listener appraisal next, then broader actions and tagged
speech, follow-through and controlled model comparisons. The [contextual-event spec](docs/under-glass/specs/contextual-events-spec.md)
is planned, not implemented. Earlier batch orders, news ceilings and E1/E2 thresholds remain
historical evidence. They do not require quiet, hostile or socially collapsed runs to be tuned away.

The optional [quiet-reflection prototype](docs/under-glass/specs/reflection-spec.md) is built:
nine editable authored thoughts cover kindness, suspicion, connection, boundaries, pride, repair,
regret and doubling down, with a memory-grounded neutral fallback. Five response profiles fit the
remembered encounter. Each thought also has a distinct waking variant: opt-in dreams use a real
sleep/wake transition and knowledge from before sleep. Dreams default off, share the daily/source
limits with daytime reflection, and never enter the belief ledger as events. An authored thought,
or a proposal from a configured local generator, is
evaluated against actual response lines by Laya. An explicit authored baseline runs without any service. A seeded
draw accepts, reshapes, defers or rejects the proposal; an accepted act waits for an ordinary
encounter, can expire, and retains its source memory as its cause. Deferred ideas can return after
distinct later personal knowledge and a full-day cooldown, at most twice per root. Rejection
stays terminal; drift in mood/regard or expiration alone does not trigger another try. Once an
intention acts, new thoughts can occur while the original outcome remains tracked. The replay
viewer shows current moments, earlier-idea links, event navigation and plain-language outcomes
as the clock reaches them. Model calls pause simulation time, and recorded answers can be reused
only with matching requests. All of this remains separate from Stardew and SMAPI.

The observer controls now stay at the top while scrolling (2026-10-09). Selecting a person
highlights their encounters, private thoughts/outcomes and relationship changes on a separate
timeline row. Hover describes nearby events already reached; click pauses and jumps exactly.
Future markers show only their time. The prepared local pages retain their original recordings.

Scene and fake-HTTP tests verify the mechanics, context boundaries, deadline/fallback behavior and
replay. The first live seven-day seed-7 recording made 27 successful Laya calls, without fallback,
in about 1.5 seconds total. A separate 15-call paired trial had complete context and no truncation,
but renaming choices moved probabilities about as much as changing a memory. Working integration
is established; better social judgment is not. The follow-up adapter adds Canonical and Balanced
evaluation: temporary labels, identical-choice grouping, and in Balanced mode a rotation through
each position before averaging and sampling once. Replay defaults to Balanced; the API and trial
retain Raw for compatibility/comparison. Five-choice smoke evaluations had medians of 19.14 ms
Raw, 23.41 ms Canonical and 127.28 ms Balanced. Presentation controls now hold by construction;
this does not prove model understanding. See the [first trial](docs/under-glass/experiments/reflection-trial-2026-10-08.md)
and [reliability/social probes](docs/under-glass/experiments/reflection-reliability-2026-10-08.md).
The optional loopback generator now runs pinned `Qwen/Qwen3-0.6B`
(`c1899de289a04d12100db370d81485cdf75e47ca`), with roughly one-second local GPU smoke inference.
Requests carry a stable seed; full generation responses and model metadata are recorded, and
invalid or failed generation uses an explicit authored fallback. **Still to verify:** whether
generated ideas and these choices make the town more compelling. A controlled probe found
generator option-order sensitivity and copied speech. Generation now sees supported act types
and known source roles while Laya receives full dialogue; thought/act mismatches still need
review. The wider tagged dialogue
catalog and player interaction remain unbuilt. The detailed plan is in
[`docs/under-glass/design.md`](docs/under-glass/design.md); the mod's goals and history below remain
their own track.

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

Roadmap step 2 (diary enrichment part 2) landed in PR #6: read-only Harmony postfixes on `NPC.receiveGift` and `Quest.questComplete` (queued at the tick, never applied inside a patch), the `GiftReceived`/`SawGift`/`QuestHelped`/`Festival`/`MissedFestival` kinds through `src/NpcDiaryEvents` producers, and `LineRenderer` templates for all of them. 561 tests green.

Roadmap step 3 (the Laya decision layer) landed in PR #7: `DecisionState` and the NPC card, batched questions (the planner's speak/pick pair is one request), the varied fake (`DecisionBackend: Varied`), the health re-check with warm-up and short-circuit, budget pass-through, the morning wait menu, heartbeat counters, and the eval set (`sidecar/eval/`: typed-decisions 5/6 vs english 3/6). 624 tests green.

The step-3 in-game week (spring 10-16, shadow mode, Laya on the GPU) passed its acceptance test as plumbing (0/171 fallbacks, 0 drops, plan collected every morning) but its review found the decisions not ready for steps 5/6. PR #9 fixed the bugs (festival notes now saved; no more `null` quest diary; shadow `IgnoredBy` no longer written; habit leads need 12 evidence and never point where the seeker already is). PR #10 is the news-ranking redesign: speakers ranked by news score (not the compressed yes/no band), the pick blends model probabilities with news (veto kept, degenerate answers fall back to news weights), `MinNews` 2.0, `SpeakThreshold` 0.25 as a veto floor, and plain-sentence model states matching the eval phrasing; eval re-run live (typed-decisions 5/6, english 4/6). 647 tests green.

The first playtest after the redesign (spring 16-18) confirmed it: the quest news outranked every saw and produced "Thanks for helping me out yesterday" with the model only 0.4 for the pick. Its review (Claude) then drove the playtest-fixes PR: a player `Saw` is dropped when the NPC talked to the player that day; a one-option pick skips the pick question; sighting and tip leads are capped in age and never point into the seeker's own region; the saved ladder no longer keeps a `null` NPC. Motives (step 14, the urge-clock fix) is the next design item.

The NPC Minds viewer (a debug tool, `docs/spec/debug-tools.md`, "Live viewer") came next: a read-only page at `http://127.0.0.1:8765/` that the mod serves while the game runs, showing each NPC's urge, rung, open attempt, what it knows of the player, where it would look, today's line, tonight's likely news and recent diary, plus feeds of ladder events and every model call with its probabilities and latency. It changes nothing in the game. 703 tests green. It then gained each NPC's seed temperament (traits, emotions and the game's own personality words), the first thing in the mod to read the temperament table; 731 tests green.
Seed temperaments (2026-09-30, draft PR): six traits per villager (warmth, sensitivity, forgiveness, chattiness, curiosity, boldness) plus six Ekman emotion biases (anger, disgust, fear, happiness, sadness, surprise), computed by `tools/TemperamentExtractor` from each villager's dialogue files, gift reactions and `Data/Characters` traits (`docs/spec/temperament.md`). The table is in `fixtures/game/temperament/`; the mod doesn't read it yet. 678 tests green.

Motives redesign (2026-10-01, design only, PR #17; D24, D25): the urge clock goes. An NPC acts only with a motive and enough boldness for the act (boldness + familiarity + the feeling's intensity against the act's cost); feelings are elastic stresses from the diary plus saved per-pair regard; mood is earned from recent events with a small daily roll that tips close calls; gossip spreads by juiciness and hearsay sticks only when confirmed; a per-day playtest log records each decision's parts. Specified in `docs/spec/motives.md`; nothing built yet.

Step 7 follow-up (2026-10-02): the family-prior seeding from PR #16 loaded `Data/Schedules/<Name>`, which does not exist in 1.6, so the outer catch swallowed the first load error and no priors were ever seeded in-game. Fixed: the asset is `Characters/schedules/<Name>` (loaded through `Helper.GameContent.Load`, other mods' edits included), a villager without a schedule asset skips only that villager, and a failed run never sets `Seeded`, so misses seed on the next load.

Playtest log (2026-10-02, step 14 part 1): one JSON-lines file per save and in-game day (`Mods/StardewNpcMod/playtest/<save>/<year>-<season>-<day>.jsonl`) records everything with data today — presence deltas, ladder attempts and blocks with urge/threshold/model p, gossip lines and AskAround answers, game facts (weather, festival, gifts, quests, talks), the overnight plan with news scores, the per-NPC memory census against the 500-entry diary cap, every model call and per-section timing. `tools/playtest_summary.py` turns a save's folder into per-day tables. Built before the motives so the first motives playtest has tuning data; the motive-only record types wait for the motives PRs.

Motives engine (2026-10-02, step 14 part 2, Claude in the cloud): the whole of `docs/spec/motives.md` that needs no game, as `src/NpcMotives`. The stressor table, elastic stresses, regard (retention, severe hurts, the yield point, drift, confirmed hearsay), the mood roll, motives and netting, the act rule with clear and close calls, and a runner that paces it over time on its own worker (one in-person and one waiting attempt per NPC, the ladder's caps, frustration, motives used up, the model asked only to pick among motives and on close calls, the shadow grudge). A `MemoryStore.Noting` hook applies regard as diary entries are written. The viewer's cards show the runner's real numbers, and the playtest log gains `decision`, `stress` and `regard` records. Not wired into the mod yet: that needs a build against the game (step 14 part 3, local). 840 tests green.

Motives wired in shadow (2026-10-02, step 14 part 3): the engine now runs beside the urge ladder every tick — inputs from the ladder's own views and leads (never live positions), model calls only on its worker through `Guarded("motives")`, acts and grudges as `[shadow]` lines, regard applied through the `MemoryStore.Noting` hook, regard drift and the snapshot at 6:00, `regard` and `motives` saved and loaded, and the viewer showing the runner's real decisions. First motives playtest data now accumulates in the playtest log.

Live emotes and bubbles (2026-10-02, rollout D30): the first behavior to leave shadow — the motives runner's Emote and Bubble acts, each behind its own switch in config.json, both off by default. Drained about once a second so a wave shows while the player is still there, gated by live facts (no event/festival/menu, in the player's location within range, visible, not busy), shown through the breaker (`npcmod_live off` trips it), and a shown-then-ignored act writes a real `IgnoredBy` diary entry. History at install seeds regard once per save from the game's own gift log, heart events and relationship status (`historySeeded`), so a mid-playthrough install feels normal. The Laya failure streak now warns once and shows the red header in the viewer.

First motives playtest (2026-10-02, spring, BUNKO save): the runner ran beside the ladder with Laya up (about 35 ms a call, no fallbacks). Fixes from reading the log: the overnight lines thanked the player for hated gifts ("Thanks again for the Daffodil" to Jodi, while her motives were Hurt), now by taste; a seen gift says who gave it and how it went down (Sid: the witness saw the reaction); hearsay lines and the model's hearsay options say who told what instead of raw detail; and a plain talk no longer counts as a reason to thank someone (Haley's daily thank-you letters). Each act now also needs a big enough reason (`ActMinStrength`; a letter 0.30), so a bold villager no longer writes over a passing thought. 850 tests green.

Notice-board experiment (2026-10-02, roadmap step 21, first stage): `src/NpcBoard` asks how each villager would react to a note in the player's own words, as a typed choice over six reactions (amused, touched, curious, annoyed, offended, indifferent) with the note as quoted data, and maps the reaction to an emote and a templated line; `sidecar/eval/run_notes.py` runs it against a local Laya for every villager's card and reports how the town split and whether the villagers differ. 859 tests green.

Long playtest (2026-10-02, spring 18 to summer 1): from spring 20 every Laya question failed in about 4 ms while its health check kept saying ok, so the mod used fallbacks for days without saying so. The Laya client now keeps the failures in a row and the newest reason, and the viewer can show "questions failing" in red (the mod passes it; local wiring). The motives runner now asks the model to pick only among motives that could act, after Emily was asked the same "news or hurt?" question every in-game hour. 862 tests green. Then (Sid's call): waves and greeting bubbles no longer use the town's daily attempts and have their own cap of 2 per villager, and the town's cap went from 6 to 12, to come down after testing if need be. 863 tests green. DeepSeek found the cause in the server's output: the GPU context was lost (`CUDA error: unknown error`, 257 times), which `/health` can't see. Every fallback now records why (viewer call log, playtest `model` records, a "fallbacks by reason" table in the summary tool), and the run scripts keep the server's output in `sidecar/laya.log`. 869 tests green. Short playtest after that (2026-10-02, summer 1 to 5): Laya answered all 123 questions, none fell back; 1 to 9 motive acts a day (12 waves, 5 bubbles, 3 walk-ups, 3 queued lines, 2 letters, 1 glare), no day reached a cap, and the top reasons to pass were motives too weak for a letter and the player out of reach. Regard changes too small to show are no longer logged, and the PowerShell script reads the server's output as UTF-8. 870 tests green. History at install's seeding rule is built (`RegardHistory`, D29): gifts by taste, heart events and relationship status become regard toward the player, once per save, added onto earned regard and leaving out what the mod already saw; the game reader is local work. 881 tests green. The ladder no longer re-asks a question its draw just turned down (Jodi was asked "Approach now?" every tick while the player was on the farm): the same step and lead wait 6 ticks. 882 tests green. Sid chose emotes and speech bubbles as the first live behavior, friendly and hostile (D30, ahead of the planned lines): `src/NpcLive` holds the switches, the emote and line choice, the last-moment gate, the circuit breaker and the ignored-act record; the mod wiring is local work. 889 tests green. The viewer's cards were redesigned for a glance (Sid: hard to see the important values): a status pill in words, the feeling as a big number, an act ladder naming every act instead of the unlabelled green bar, four tiles (regard, mood, tries, saw you), the rest behind tabs, calm villagers shaded and last, and header counts per state that filter the grid. First live test (2026-10-03, emotes on): a villager that waved after the day's talk waited for an answer the player couldn't give (the game opens no second conversation that day) and counted it as ignored; such acts now wait for nothing. Acts decided while the player was busy (walking into the Saloon, mid-conversation) were held back by the gate after the runner had counted them; now nothing in person is decided while the player is busy. Waves and greeting bubbles are never counted as ignored (Sid: reacting to every one would make the game unfun). 892 tests green. The urge ladder is retired (D31): the mod no longer runs it, the motives decide every attempt, asking around and the heartbeat read the motives (`MotiveDrive`), and the viewer drops the urge. 893 tests green. Speech bubbles speak in each villager's own voice (`BubbleVoices`, 34 villagers, short lines per feeling, Sid's to edit). 896 tests green. Villagers notice the player between ticks (D32): about once a second a villager who just came within range records the sighting and decides at once, so running past one no longer slips between two ten-minute ticks. 899 tests green. A suspected double wave in the test of that change turned out to be Haley and Leah, one tick apart. Still, the live layer now holds back a second act from the same villager within 3 ticks as a safety net, and each `[live]` line names the tick the act was decided in. 900 tests green. Gossip juiciness (D25, built as D33): villagers volunteer only stories juicy enough for the listener, retell them hop by hop at 0.7 of their juiciness, let them fade by the day (faster for scandals), tell one story to at most 3 listeners a day and never twice to the same one; a plain sighting never spreads on its own, and second-hand hearsay stays a passing feeling until the person it happened to confirms it or the listener sees the same thing within a week. A villager at 8+ hearts takes stories about the player twice as hard, and hearing that the player gave someone else a gift they liked makes them jealous. 920 tests green. Single-player guard (Sid asked, 2026-10-04): joining someone else's game as a farmhand, the mod logs one line and stays off (it can't read or write the host's save data); a split-screen guest's screen is ignored; a host in multiplayer is warned once that other farmers are ignored. 925 tests green. Day-9 playtest (2026-10-05): gossip ran as designed (Haley's loved Sunflower reached Alex from Haley herself, confirmed; Marnie told three villagers about a gift she saw Leah get, the daily cap). Fixes from the log: a gossip record now carries its real hops and the tick it was told, as do the stress and regard records of hearsay (they appeared 30 ticks back); the game's two "Mister Qi" count once (his ledger entry flipped between them every tick); a greeting bubble's decision says "a greeting needs no answer", not "a wave". 927 tests green. Characters the player can't befriend (the Bouncer, Mister Qi, Gunther, Marlon, Birdie, the Henchman) are no longer watched: they stand in their maps from day one, so the mod tracked them, and they could "chat" about a player they had never met (Sid: "I haven't met Qi yet"). 931 tests green. Day-10 playtest: Gus queued a telling-off over a fish Shane disliked, which he only heard about. Now hearsay about what the player did to someone else moves the mood only, unless it is a scandal like rummaging in the trash, which may still have several villagers telling the player to stop (D34). 932 tests green. A full diary now forgets what matters least first: plain sightings that witnessed nothing and entries about people the villager cares little about go before events and before anything from the last two days (Sid; D35). 938 tests green. Audit of PRs #29 to #34 (Claude, 2026-10-05): every `VERIFY` in the code settled in the decompile (`stardew-source-notes.md`, "Audit pass"; `NPC.CanSocialize` is right, and Sandy is watched only after her Oasis introduction), the docs brought back in step with the retired ladder, a farmhand's return to the title no longer throws, and edge-case tests for gossip keys, confirmation, diary trimming and split-screen. The bugs it found are waiting on Sid (decisions.md, "Open work"). Emily's double hearing on day 10 was a reload. 945 tests green.

**From here on**, the plan lives in `docs/spec/`: a spec for every feature and a prioritized roadmap (`docs/spec/roadmap.md`) with the decisions still open for Sid.

## Side ideas

- **Sims 4:** script mods are Python and hook autonomy. Wrap the autonomy pick: take the top 8 to 12 legal candidates, ask Laya to choose, and fall back on timeout. Run the model in a sidecar process, off the sim thread.
- **Civ 6:** weak fit. Its AI problem is competence, not character. Diplomacy judgments are the one place it might help.
