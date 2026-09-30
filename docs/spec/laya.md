# 9. The Laya decision layer

**Status: partial.** The typed client, the fake, the timeout-and-fallback wrapper and the HTTP client
for `laya-serve` are done and tested against a fake server. Laya has never answered a real question
from the mod. The state format is ad hoc per caller, there is no token budget, no batching, and the
sidecar is started by hand. Brief, "Decision layer"; D12, D13, D14; architecture, "Decisions";
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
| Latency | one question 33-40 ms on a T4 GPU; 10 batched 72-159 ms; CPU a few hundred ms | model card; `sidecar/README.md` |
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
   temperament: manners neutral, outgoing, optimistic      (Data/Characters Manner, SocialAnxiety, Optimism)
   voice: sunny, a little vain, warms up slowly             (VoiceSheets)
   hearts with the player: 4 of 10
   today: spring 12 (Tuesday), sunny, 7:30 PM
   ```
   Then the caller's sections, for example news for the planner:
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
| Intents | does <npc> have something worth telling the player today? | noul | 0.5 | done (wording to update) |
| Intents | which of these would <npc> most want to bring up? | choice (≤5) | uniform | done |
| Ladder | should <npc> try to get the player's attention with <step> now? | noul | 0.5 | done |
| Newcomer | would <npc> go out of their way to welcome a newcomer in person? | noul | 0.5 | not started |
| Visit | would <npc> drop what they are doing right now and go looking for the player? ([find.md](find.md)) | noul | 0.5 | not started |
| Letters | what would <npc> write to the player about? ([invitations.md](invitations.md)) | choice (≤3) | uniform | not started |
| Letters | where would <npc> ask the player to meet? | choice (≤3) | uniform | not started |

`Score` has no caller and none is planned (weakest primitive). Keep it in the interface; don't add
uses without a reason.

Wording rules: name the NPC, ask one thing, present tense, no negations, no numbers the model has to
compare. The proposition for `noul` must be answerable from the state alone.

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

## Deterministic rules

- Every call goes through `ResilientDecisionClient` off the game thread (D14).
- Probabilities are clamped, never renormalized; NaN fails any threshold; sampling (never argmax)
  uses the caller's seeded `Random`.
- A fallback is always a legal, conservative answer: 0.5 yes/no, uniform choice, midpoint score.

## Tuning constants

`DecisionTimeoutMs` 1500, `PlanningBudgetMs` 20000, `LadderMaxBacklog` 6 (config); `MaxStateChars`
2000 (planned, was 4000), `StateBudgetChars` 1250 (planned), `HealthCheckTicks` 6 (planned).

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
  This is the A/B from D13.
- One in-game week with Laya on: fallback rate under 5% after warm-up, no tick drops logged by the
  ladder, overnight plan collected at 6:00.

## Status

Done: `src/NpcDecision/*`, `ModEntry.BuildModel`, `Guarded`, `sidecar/*`; tests in
`tests/NpcDecision.Tests` (50 at `c97a829`). Not started: `DecisionState`, the NPC card, batching,
the varied fake, periodic health, warm-up, short-circuit, budget pass-through, launch, eval set.

## Open questions

- Which checkpoint is better for these questions? Decide with the eval set, not by guess.
- Whether question text and options count against the same 512/1,024 window as the state.
- Whether to fine-tune later. Laya is open-weight, so possible, but not planned: typed questions with
  good state should be enough, and fine-tuning adds a training pipeline to maintain.
