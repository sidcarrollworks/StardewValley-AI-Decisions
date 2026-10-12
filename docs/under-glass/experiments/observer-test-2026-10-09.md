# Observer test — 2026-10-09

Protocol created October 9; recording verification resumed October 12. PR #82 remains the review unit. The question is whether an observer can understand a person's changing reasons and find an encounter worth following. A required number of friendships, feuds, thanks or apologies would not answer that question.

## October 12 refresh

The fresh pack in `out/observer-test-2026-10-12/` completed and was verified on October 12, using the same four-run settings after the source-link correction. All four JSON files match their manifest SHA-256 hashes; each HTML embeds its corresponding JSON exactly. Backend counts below were also counted directly from the recordings.

| Fresh recording | Recorded thoughts / backend | Authored fallbacks |
|---|---|---:|
| Authored, seed 7 | 249 `authored` | 0 |
| Authored control, seed 29 | 253 `authored` | 0 |
| Balanced Laya, seed 7 | 256 `authored+laya` | 0 |
| Local generator + balanced Laya, seed 7 | 250 `local-llm+laya` | 0 |

These establish completed integrations, not believable characterization or successful grounding. The four runs can diverge after decisions; the counts do not rank their quality.

The October 9 pack remains in `out/observer-test-2026-10-09/`. On October 12, all four JSON files matched their manifest SHA-256 hashes, and each HTML embedded its corresponding JSON exactly. Its hybrid run completed with **21 `local-llm+laya` thoughts and 233 `authored-fallback` thoughts**: 222 recorded generation HTTP 503 failures and 11 HTTP 504 failures. It is useful for studying fallback and integration behavior, but is not a clean generator comparison. The older authored seed 7/29 runs recorded 249/253 authored thoughts; Laya seed 7 recorded 256 `authored+laya` thoughts with no fallback.

## Record the bounded pack

From the repository root, with the existing local Laya and generator services running:

```powershell
# Inspect commands and source hashes without building or contacting a model.
pwsh -File sim/tools/record-observer-pack.ps1 -OutputDirectory out/observer-test-2026-10-12 -PlanOnly
# Four recordings, sequentially; no model installation or service startup.
pwsh -File sim/tools/record-observer-pack.ps1 -OutputDirectory out/observer-test-2026-10-12
# Or run unfinished backends separately:
pwsh -File sim/tools/record-observer-pack.ps1 -OutputDirectory out/observer-test-2026-10-12 -Backends authored
pwsh -File sim/tools/record-observer-pack.ps1 -OutputDirectory out/observer-test-2026-10-12 -Backends laya,hybrid
pwsh -File sim/tools/start-preview.ps1 -Open
```

The pack uses 28 days and seed 7 for authored, balanced Laya and hybrid, plus authored seed 29. Shared flags enable `returns,company,welcome,repair,sides,late`, feelings and motives, reflection chance 0.35 and dream chance 0.15. There is no injected event, extra 0d6 switch, trait override or tension override. Default model settings are `typed-decisions` at `http://127.0.0.1:8000` and `Qwen/Qwen3-0.6B` at `http://127.0.0.1:8080`; the runner exposes parameters to override them. Inspect the resulting plan if you do.

Outputs live in `out/observer-test-2026-10-12/`. Existing plans, recordings, manifests and logs are preserved: use a different output directory for another live attempt. PlanOnly prints the current invocation without replacing an existing plan. Each completed manifest contains actual backend and fallback counts, offered/chosen/executed focus-act counts, apology outcomes, a few timestamped scene bookmarks, recorded settings, the source commit, dirty status, SHA-256 hashes of tracked and untracked simulation source, and the recording hash. Counts describe what occurred; none is an acceptance quota. A zero can mean no eligible opportunity occurred. “Completed” means the recording finished, not that the model made sensible choices or avoided fallback.

## Watch before reading the bookmarks

Open `http://127.0.0.1:8766/sim/viewer/morning.html`. Start with hybrid seed 7 to see the local generator and Laya together, then use authored seed 7 as a control. Follow a person, and use **Next moment** and **Follow this thought**. Keep whole-run analysis off. The manifests contain spoilers, so use their bookmarks only after an unprompted viewing attempt.

- For a thank-you, identify the earlier kindness and whose belief connects them. Is the offered line appropriate to that encounter, or merely generically friendly? Was it chosen and then executed, or only offered?
- For an apology, follow the hurt, remorse, attempted repair and response. An accepted or refused apology should remain understandable without treating either outcome as success or failure for the simulation.
- Follow a deferred thought into a later encounter. Does genuinely new known evidence explain reconsideration? Can you see what changed without reading a technical log?
- Compare ordinary activity with socially significant encounters in Stories. Does grouping routine activity help you notice a meaningful departure, or hide something that mattered to one participant?
- Rewind before a thought is considered and before its outcome. Later thought text, receipts, gossip and outcomes should disappear. Dreams are private possibilities on waking, never evidence that the imagined event happened.

Write down one scene that held your attention and one that disappointed you, including recording, person and clock time. Was the disappointment caused by repetitive opportunities, a wrong source role, an implausible line, weak follow-through, or a viewer explanation that arrived too late? Which part could you understand without opening the receipts? What would you expect that person to do next, and did a different outcome still make sense?

Compare seed 29 before attributing every difference to the evaluator. Same-seed backends can diverge after different decisions; they are not permanently matched scenes. Hybrid voice and semantic grounding still need human review. Balanced option orders reduce one experimental confound; they do not establish human-like judgment.

## Recorded starting scenes (spoilers)

These bookmarks are from the **October 12 pack** and select actual reflection chains rather than the first generic town act of each kind. Day numbers match the event log: d0 is Spring 1. The numbers in parentheses are replay ticks for exact checks.

| Recording and person to follow | Start with the thought | Follow-through | What to inspect |
|---|---|---|---|
| **Hybrid seed 7: Kent → Newcomer** | d7 06:29 (10469) | Apology d8 12:34 (12274); refused at 12:39 (12279) | Recommended first scene. The generated thought proposes helping instead of arguing; the selected response reshapes that into an apology. Its source is Kent's argument at d5 12:48 (7968). Both the attempted repair and refusal should make sense without treating refusal as a failed simulation. |
| **Laya seed 7: Harvey → Maru** | d3 16:42 (5322) | Thank-you d5 11:16 (7876) | Source gift at d3 15:55 (5275), act 89. The source gift's life records resolve as `Returned` at 7876. The thank-you itself records `None` at 7877, meaning no further tracked response to that act; that is distinct from whether it answered the gift. |
| **Authored seed 7: Pierre → Caroline** | Waking thought d23 06:57 (33537) | Apology 07:07 (33547); accepted at 07:12 (33552) | The imagined help becomes an apology about Pierre's argument at d18 07:45 (26385). Keep the dream separate from the earlier witnessed event. This is an authored control, not generated prose. |
| **Hybrid seed 7: Emily → Newcomer** | d0 13:23 (803) | Intention expires d3 13:23 (5123); no inspired act occurs | A genuine generated thought becomes a gift intention, but no suitable encounter arrives. Does the viewer explain the unfulfilled intention clearly? |

For a grounding counterexample to discuss, follow **hybrid Maru → Caroline**, thought d4 08:30 (6270), thank-you d4 12:36 (6516). Her source says Caroline comforted her; the generated thought proposes comforting Caroline “again.” Check whether the supplied memory supports the repetition and role implied by that word. A structurally valid thought and zero fallback do not settle that question.

For clock checks, rewind Kent to tick 10468: the considered thought and model receipts should not yet appear. At 12278 the refusal is still in the future. For Emily, 5122 must still show the waiting intention and 5123 its expiry. Broader context and prior events should disappear again when rewound before their recorded times.

The manifests distinguish offered choices from chosen intentions and completed reflection acts. In authored seed 7, thank-you counts are **68 offered / 15 chosen / 12 executed** and apology counts are **6 / 1 / 1**. In Laya seed 7 they are **72 / 17 / 15** and **3 / 0 / 0**; in hybrid seed 7, **80 / 18 / 14** and **5 / 2 / 1**. These are observations, not quotas. Ordinary motive-driven apologies also exist; do not count every town apology as a reflection choice.

## Repeat recorded decisions without inference

Every manifest includes its exact `tapeCommand`. For the default hybrid run:

```powershell
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 28 --catalog returns,company,welcome,repair,sides,late --feel on --desire on --reflection-chance 0.35 --dream-chance 0.15 --reflection tape --reflection-tape out/observer-test-2026-10-12/hybrid-seed7.json --out out/observer-test-2026-10-12/hybrid-seed7-tape.json --html out/observer-test-2026-10-12/hybrid-seed7-tape.html
```

Use the same source, town, catalog and simulation settings. The tape rejects changed requests rather than applying an answer to a different scene. A live model rerun is not the determinism test. Compare causal records such as acts and reflections; whole JSON bytes may differ because the recording label includes CLI arguments. Exact generation responses retain model revision, device, seed and timing when the server supplies them.

Harness validation: plan-only and one-day authored seed 7/29 smoke runs exercised output, summaries and manifests without live model calls. Seven isolated checks in `sim/tools/test-observer-pack.ps1` cover plan and output preservation, partial-backend resume and the current-date default without builds or model calls. The completed October 12 pack passed the manifest-hash, embedded-recording and direct backend-count checks described above. All four exact-tape replays also passed: each of the 51 recording fields matched its original, excluding only the CLI label. The local report is `out/observer-test-2026-10-12/verification.json`. The October 9 pack remains preserved separately.
