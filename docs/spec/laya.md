# 9. The Laya decision layer

**Status: mostly done.** The typed client, the fake, the timeout-and-fallback wrapper and the HTTP
client for `laya-serve` are done and tested against a fake server. On Sid's PC `laya-serve` 0.3.22
runs on the GPU and answers (~45 ms a question, measured in the eval below); a full in-game session
with `DecisionBackend: Laya` is still to be reviewed. PR #7 added: `DecisionState` (whole-line
budget cutting), the NPC card (built on the game thread, passed as a copy), batching (one request
per speak+pick pair), the varied fake, the health re-check with warm-up and short-circuit, budget
pass-through, the morning wait, heartbeat counters, and the eval set — run once, results in
`sidecar/eval/RESULTS.md`. The sidecar is still started by hand, and the ladder still builds its
state ad hoc (see Status). Brief, "Decision layer"; D12, D13, D14; architecture, "Decisions";
`sidecar/README.md`.

Laya facts below come only from github.com/NandhaKishorM/laya and huggingface.co/convaiinnovations/laya
(`AGENTS.md`, rule 8). The model card was re-read for this spec on 2026-09-29.

## What Laya is (from the repo and model card)

| Fact | Value | Source |
|---|---|---|
| Checkpoints | `laya` (English, ModernBERT-large, 421M, **512 tokens**), `laya-multilingual` (mmBERT-base, 322M, 1,024 tokens, expandable), `laya-typed-decisions` (ModernBERT-large, 421M, 1,024 tokens) | model card |
| Question types | `choice` (softmax over options), `score` (ordinal levels), `noul` (yes/no probability) | model card, repo docs (client comments) |
| Limits | at most 100 options per choice (413 above); score 2..32 levels; state up to 50,000 chars at the HTTP layer | repo `serve.py` / `docs/http-api.md` at v0.3.22 (as recorded in `LayaDecisionClient`) |
| Quality notes | `score` is the weakest primitive (model card: SST-5 accuracy 0.372); choice quality degrades above about 20 options | model card |
| Batching | one request may carry several questions (`questions: {id: question}`) about one state | repo (client comments) |
| Latency | one question 33-40 ms on a T4 GPU; 10 batched 72-159 ms; CPU a few hundred ms | model card |
| Latency on Sid's PC | CPU: ~250 ms a question; GPU (RTX 4070 Ti, CUDA PyTorch): ~35 ms, and a yes/no plus a choice batched in one request still ~36 ms | measured 2026-09-30, `sidecar/README.md` |
| State | free text or JSON; no required format | model card |

Not stated anywhere we trust: which side is truncated when state is too long, the tokenizer's exact
characters per token, and whether the question and options share the context window with the state
(verify all three against a running server; see "Token budget").

## Player-visible behavior

None directly. With Laya running, NPC choices stop being uniform: who speaks tomorrow, which news
they pick, and whether they try to reach the player depend on their temperament and memory instead
of a coin flip. With Laya down, the mod behaves exactly like the fake, and says so once in the log.

## Data model

Done: `IDecisionClient { Choose, Score, YesNo }`, `FakeDecisionClient`, `ResilientDecisionClient`,
`LayaDecisionClient`, `LayaOptions` (`src/NpcDecision`).

Planned:
1. **`DecisionState`** (`src/NpcDecision/DecisionState.cs`): the one way to build a state string.
   A list of `(priority, line)` sections, rendered in priority order and cut **by whole lines** to a
   character budget, so the least important facts drop first instead of the end of whatever was
   written last. Every caller (planner, ladder, newcomer) builds its state with it.
2. **The NPC card**, a shared first section built on the game thread and passed as a copy:
   ```
   npc: Haley
   temperament: manners neutral, outgoing, optimistic      (Data/Characters Manner, SocialAnxiety, Optimism;
                                                            later plus words from temperament.md)
   voice: sunny, a little vain, warms up slowly             (VoiceSheets)
   hearts with the player: 4 of 10
   today: spring 13 (Wednesday), sunny, morning
   ```
   The "today" line describes the DELIVERY day (tomorrow morning: the plan runs at DayEnding),
   built in English (the checkpoint's language) with tomorrow's weather and a fixed morning time,
   so the same diary always produces the same state. Then the caller's sections, for example news
   for the planner:
   ```
   news:
   - yesterday the player gave Haley a sunflower; she loves it
   - yesterday Haley saw the player at Pierre's General Store
   ```
   Rules: player-facing names only (`PlaceNames`, item display names), never internal ids; plain
   words over numbers where the number is a judgment ("2 hours ago", not "12 ticks"); no live
   positions (D2); ASCII-safe; no instructions to the model inside the state.
3. **Batch questions:** `IBatchDecisionClient.Ask(string state, IReadOnlyList<Question> questions)
   -> IReadOnlyList<Answer>` where `Question` is a small union (Choice with options, YesNo with a
   proposition, Score with levels). `LayaDecisionClient` sends them in one request; the fake and
   the resilient wrapper implement it by looping or falling back per question. The planner's
   speak/which pair becomes one request (half the round trips overnight).
4. **Seeded fake** (`VariedFakeDecisionClient`): answers from FNV-1a of (state, question), so shadow
   logs show varied choices without a model. It is the answer to `docs/decisions.md` "Fake backend".
   Selected with `DecisionBackend = "Varied"`; the plain `Fake` stays the default and the test
   baseline.

## Token budget (512)

Design every state to fit the smallest checkpoint, `laya` at 512 tokens, so switching checkpoints
never silently cuts facts.

| Part | Budget |
|---|---|
| NPC card | about 60 tokens (~250 chars) |
| caller sections | about 250 tokens (~1,000 chars) |
| question + options | about 150 tokens (at most 5 options of ~100 chars) |
| margin | ~50 tokens |

- `DecisionState` cuts the state at `StateBudgetChars` = 1,250 characters (about 4 characters per
  token for English: a planning estimate, verify with the tokenizer).
- `LayaOptions.MaxStateChars` stays as the client's hard stop but drops from 4,000 to 2,000.
- Options: the planner already caps at 5; keep every choice at 20 options or fewer (quality note).
- A test asserts that every state builder, fed the worst case (longest names, 5 news items with long
  item names), stays under budget.
- Verify on a running server: send a state of known length and watch whether answers change when a
  fact is moved past 512 tokens; record the finding in `sidecar/README.md`.

## Question catalog

Every model question in the plan. Anything not listed here is not asked.

| Feature | Question | Type | Fallback | Status |
|---|---|---|---|---|
| Intents | does <npc> have news for the player? | noul | 0.5 | done (wording chosen by the rewording experiment, `sidecar/eval/speak_experiments.md`) |
| Intents | which of these would <npc> most want to bring up? | choice (≤5) | uniform | done |
| Ladder | should <npc> try to get the player's attention with <step> now? | noul | 0.5 | done; replaced by the motives' close-call question when motives land |
| Motives | which of these would <npc> act on first? ([motives.md](motives.md)) | choice (≤5) | the strongest motive | not started |
| Motives | would <npc> <act> toward <subject> now? (close calls only; the day's mood shifts the answer before a cut at 0.5) | noul | `0.5 + margin / (2 x ClearBand)` | not started |
| Motives | would <npc> hold this against the player? | noul | 0.5 | not started |
| Newcomer | would <npc> go out of their way to welcome a newcomer in person? | noul | 0.5 | not started |
| Visit | would <npc> drop what they are doing right now and go looking for the player? ([find.md](find.md)) | noul | 0.5 | not started |
| Letters | what would <npc> write to the player about? ([invitations.md](invitations.md)) | choice (≤3) | uniform | not started |
| Letters | where would <npc> ask the player to meet? | choice (≤3) | uniform | not started |

`Score` has no caller and none is planned (weakest primitive). Keep it in the interface; don't add
uses without a reason.

Wording rules: name the NPC, ask one thing, present tense, no negations, no numbers the model has to
compare. The proposition for `noul` must be answerable from the state alone.

## Speed: the GPU, and a morning wait (Sid, 2026-09-30)

Sid: "speed is important", either by running Laya on the GPU or by holding the next day until
everything is ready. Both are in:

1. **GPU (done on Sid's PC).** The CUDA build of PyTorch is installed in `sidecar/.venv` and Laya runs
   on the RTX 4070 Ti. A night's plan drops from ~15 s to ~1-2 s, well inside the 20 s budget, and a
   ladder question is ~35 ms. Install steps: `sidecar/README.md`. A release that starts Laya itself
   ([Launching](#sidecar-lifecycle) below) must detect a GPU and pick the right PyTorch build.
2. **A morning wait, as the safety net** for CPU-only players and slow nights. At `DayStarted`, if
   the overnight work isn't finished (the plan, plus the 6:00 jobs: letters' text, trade offers),
   the mod opens a small menu, "The valley is waking up...", with a spinner. In single player the
   game clock doesn't run while a menu is open (`Game1.shouldTimePass`, confirmed in the decompile),
   so nothing is missed. It closes itself when the work is done, or after `MorningWaitMs` (10 s), at
   which point the remaining decisions take the fallback. The player can close it early (Esc); that
   also takes the fallback. It's a menu on the game thread that polls the job each frame; nothing
   ever blocks the game thread.
   - Why not stretch the save itself: the save runs on the game's own schedule and blocking inside
     `Saving` is exactly the freeze D14 forbids.
   - With the GPU it should almost never appear. The heartbeat logs how long each night took so a
     slow machine shows up in the log.
   - Config: `MorningWaitMs` (0 disables the wait: the old behavior, lines arrive when ready).
3. **Batching** ([Data model](#data-model), item 3) halves the round trips on either device.

## Sidecar lifecycle

| State | Detected by | Mod behavior | Log |
|---|---|---|---|
| Not configured | `DecisionBackend` is not `Laya` | fake | start-up line names the backend (done) |
| Down | health check fails | every call falls back | `Laya is not answering at ...` once, then only on change |
| Warming | health ok, first real calls slow | calls may time out and fall back | none |
| Up | health ok | real answers | `Laya is up at ...` (done) |

Planned:
- **Re-check health** every in-game hour (6 ticks) on a background task while the backend is Laya, and
  log only when the state changes. Today it is checked once at start-up.
- **Warm-up:** after a healthy check, send one throwaway `noul` so the first real call is not the
  slow one.
- **Short-circuit when down:** while the last check failed, `ResilientDecisionClient` falls back
  immediately without an HTTP attempt, so a dead server doesn't cost a full timeout per call.
- **Pass the budget token** into `LayaDecisionClient` so the planning budget cancels in-flight HTTP
  calls (`docs/decisions.md`, "Laya and the budget").
- **Launching (decided 2026-09-30):** manual while we develop (`sidecar/run-laya.ps1`); a public
  release starts it with the game. `LaunchSidecar` (default false now, true in a release build)
  starts the server as a hidden child process at `Entry` and stops it at exit. Before a release this
  needs its own design: where the Python environment and weights come from on a player's PC (the
  first run downloads PyTorch and ~1.7 GB of weights: size verify), what happens without Python, the
  loopback bind and a random API key per launch, and killing the process if the game crashes. Until
  then, players of a release would still need the manual steps in `sidecar/README.md`.
- **Counters in the heartbeat:** calls, fallbacks, median and p95 latency, added to the
  heartbeat line (`src/NpcInitiation/Heartbeat.cs`, from PR #3).

## Character spread: does the card change the answer? (2026-10-02)

Sid's concern: every villager is judged by the same model, so their choices may trend alike. The
model can't drift (its weights never change and it keeps no memory between calls; the same text
always gets the same answer), but it can **start out flat**: if it barely reacts to the card's
personality words, every villager gets roughly the model's idea of an average villager. The eval
already shows it: shy Penny vs outgoing Sam differ by only 0.06-0.07 on the emote and bubble
questions (`sidecar/eval/RESULTS.md`), and the speak answer sits in one compressed band.

**When it matters** (Sid, 2026-10-02): less in the first season. Early on few villagers know the
player, vanilla already supplies many introductions and scripted interactions, and players are busy
with the farm. Low familiarity also makes most early calls clear "no"s under the act rule, so few
reach the model. Flatness matters most from mid-game on, when many villagers sit at 4+ hearts with
several motives near their act costs and close calls are common. One early exception is newcomer
week ([newcomer-week.md](newcomer-week.md)): first impressions are where shy and outgoing should
differ most, and its welcome question goes to the model. So the spread eval uses mid-game
situations (hearts 4 and 6) as its reference, plus a 0-heart newcomer case, and its results gate
the corrections before motives' close calls go live, not before then.

The motives design limits the damage, because personality lives in code (boldness, costs,
sensitivity, retention, regard, mood) and the model only decides close calls
([motives.md](motives.md), D24). This section adds the means to see the problem and correct it.

1. **Measure: the spread eval** (`sidecar/eval/run_spread.py`). For each question type, one fixed
   reference situation is asked once per villager, with only the NPC card changing (all 34 from
   the temperament table). Per question it reports the spread (90th minus 10th percentile of the
   answers), the median, and whether the order follows the trait the question should depend on
   (Spearman's rank correlation with that trait: boldness for attention and approach questions,
   chattiness for the news question, forgiveness, inverted, for "hold it against"). Results go in
   `sidecar/eval/RESULTS.md`, and the per-question medians and spreads go in a fixed calibration
   file, `data/laya-calibration.json`, so the mod's use of them is deterministic and versioned.
2. **Watch: the viewer's spread panel** ([debug-tools.md](debug-tools.md)): the same numbers from
   real play, per question, live.
3. **Correct, per question type, only where the data says so:**
   - **Relative answers.** When a question is flat in absolute terms but its order follows the
     trait, use the answer relative to the town: `p_rel = 0.5 + (p - median) / spread x
     RelativeScale`, clamped, with the median and spread from the calibration file. The 0.5 cut
     for close calls ([motives.md](motives.md)) then reads p_rel.
   - **Temperament prior.** When a question is flat and its order doesn't follow the trait either,
     blend in a prior from the trait: `p' = w x p + (1 - w) x prior(trait)`, with `w` per question
     in the calibration file (1.0 means the model alone).
   - **Text first.** Before either, check the card: the traits must be in plain words ("shy;
     rarely starts a conversation"), and the card must never be the part cut by the 512-token
     budget (it has the highest priority in `DecisionState`).
   Both corrections default off (`RelativeScale` and `w` absent from the calibration file) until
   a spread run says a question needs them.

## Deterministic rules

- Every call goes through `ResilientDecisionClient` off the game thread (D14).
- Probabilities are clamped, never renormalized; NaN fails any threshold; sampling (never argmax)
  uses the caller's seeded `Random`.
- A fallback is always a legal, conservative answer: 0.5 yes/no, uniform choice, midpoint score.

## Tuning constants

`DecisionTimeoutMs` 1500, `PlanningBudgetMs` 20000, `LadderMaxBacklog` 6, `MorningWaitMs` 10000
(0 disables the wait) (config); `MaxStateChars` 2000, `StateBudgetChars` 1250,
`HealthCheckTicks` 6 (code).

## Acceptance tests

Unit (`tests/NpcDecision.Tests`):
- `DecisionState` keeps whole lines, drops lowest priority first, never exceeds the budget, and
  renders deterministically.
- Batch: one HTTP request for N questions; answers matched by id; a missing answer falls back for
  that question only.
- The varied fake is deterministic per (state, question) and not uniform.
- Short-circuit: with the health flag down, no request is sent and the fallback is immediate.
- Budget token passed through: a cancelled budget aborts an in-flight fake-server request.

With a running server (manual, documented in `sidecar/README.md`):
- `smoke_test.py` passes; the mod logs `Laya is up`.
- A small golden set in `sidecar/eval/` (20-40 states with the expected direction, for example a shy
  NPC should score lower for `Bubble` than an outgoing one, and loved-gift news should be picked
  over seeing a housemate) run against `typed-decisions` and `laya`; record agreement and latency.
  This is the A/B from D13. **Done 2026-09-30** (6 cases, `sidecar/eval/RESULTS.md`):
  `typed-decisions` 5/6 vs `english` 3/6.
- One in-game week with Laya on: fallback rate under 5% after warm-up, no tick drops logged by the
  ladder, overnight plan collected at 6:00. **Done 2026-09-30** (save `BUNKO_450391925`, spring
  10-16): 0/171 fallbacks, 0 drops, plan collected every morning, median ~30 ms. The week also
  produced the review findings that the bug-fix PR #9 and the ranking redesign PR #10 address:
  festival notes were logged but never saved; a quest without a target made a `null` diary; shadow
  ladder outcomes wrote `IgnoredBy` diary lines; the speak-probability-first ranking let "I saw
  you" beat a birthday gift; habits claimed "100% of the time" after one day.

## Status

Done: `src/NpcDecision/*` (incl. `DecisionState`, `NpcCard`, `IBatchDecisionClient`, the varied
fake, the health gate, budget pass-through, latency counters), `ModEntry.BuildModel`/`Guarded` +
the health re-check, warm-up, short-circuit and the morning wait menu, the planner's batched
speak/pick pair and card-carrying state, `sidecar/eval/*` (run: typed-decisions 5/6, english 3/6,
see `sidecar/eval/RESULTS.md`), the heartbeat counters; Laya on Sid's GPU (2026-09-30); tests in
`tests/NpcDecision.Tests` (111) and `tests/NpcIntents.Tests` (139).
Not started: launching the sidecar from the mod (decided: manual while we develop);
`DecisionState` adoption in the ladder's context (its state is small by construction and already
fits the budget; do it when the ladder's inputs grow); the tokenizer verification and the
speak-question rewording experiments the eval points at.

## Open questions

- Is the model flat on personality? The spread eval answers it per question; the corrections above
  wait on its numbers. A choice question may separate characters better than several yes/no
  questions ("what would Shane do: write a note / say nothing / walk over"); add that variant to
  the spread eval.

- ~~Which checkpoint is better for these questions?~~ Answered 2026-09-30 by the eval set:
  `typed-decisions` agreed with 5/6 expected directions vs `english` 3/6; the mod keeps
  `typed-decisions`. See `sidecar/eval/RESULTS.md` for the per-case numbers.
- Whether question text and options count against the same 512/1,024 window as the state.
- ~~The speak question is weak on both checkpoints~~ (answers cluster at 0.15-0.4, below the 0.5
  speak threshold — the observed empty plans). Addressed by the rewording experiment
  (`sidecar/eval/run_speak_experiments.py`): "does X have news for the player?" beats six
  alternatives, and it is now the planner's question. A re-run of the sweep on the shipped
  plain-sentence states (PR #10) then showed the noul band is compressed (0.15-0.51) on EVERY
  wording, so the answer no longer decides the speakers: news-first ranking does (D21), and the
  speak threshold is 0.25 as a veto floor, not 0.5. If a live week still shows empty plans, the
  next lever is the state (more news kinds), not the wording or the threshold.
- Whether to fine-tune later. Laya is open-weight, so possible, but not planned: typed questions with
  good state should be enough, and fine-tuning adds a training pipeline to maintain.
