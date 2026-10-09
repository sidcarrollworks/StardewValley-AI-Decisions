# Reflection reliability, continuity and local inspiration

The observer loop is easier to follow, and both local models work. The small generator still produces repetitive or confused thoughts. These measurements establish integration and expose its limits; they do not establish interesting relationships or realistic judgment.

## Evaluation without incoming label/order effects

Same machine and cached Laya checkpoint as the [first trial](reflection-trial-2026-10-08.md): Windows, RTX 4070 Ti, Laya 0.3.22, CUDA, `typed-decisions` health revision `7b928d828b7b0e022f929d9bd2e44165aa270148`. No Laya checkpoint change. Each pair runs baseline, variant, exact baseline repeat. TV aligns response meanings and is half the summed absolute probability differences.

| Smoke trial | Decisions / calls | Median decision ms | Memory TV | Trust TV | Line swap TV | Order TV | Rename TV |
|---|---:|---:|---:|---:|---:|---:|---:|
| Raw | 15 / 15 | 19.14 | .0687 | .0379 | .1031 | .0330 | .0643 |
| Canonical | 15 / 15 | 23.41 | .0492 | .0532 | 0 | 0 | 0 |
| Balanced | 15 / 75 | 127.28 | .0377 | .0421 | 0 | 0 | 0 |

Canonical groups identical `(Kind, Line)` alternatives, sorts them, and uses neutral labels. Balanced rotates each distinct alternative through every label/slot once and averages normalized weights. Duplicate alternatives split their semantic mass across original IDs. Both remove incoming label/order effects **by construction**. Zero controls are not evidence of intrinsic model understanding. Balanced reduces exposure to a single placement and costs five calls here. The character still samples only once. Failed or timed-out passes discard the entire result; there is no partial average or retry toward a preferred answer.

All three smoke trials have zero fallbacks, incomplete packets, reported truncations or unverified usage. Every exact baseline repeat has TV zero in this sample. [Raw balanced receipts](reflection-balanced-2026-10-08.json) include every pass and label mapping; the [summary JSON](reflection-reliability-2026-10-08-summary.json) retains all comparisons and hashes of the local input files.

The expanded social suite adds four fictional personalities: guarded, proud, remorseful and warm. Each independently changes perspective, inspiration and order. **51 decisions / 291 passes**, median **115.76 ms**, all audited without fallback or truncation. Perspective TV ranges .0174–.0481; inspiration TV .0223–.1584. The largest shift is the guarded character's changed thought. Private confrontation remains modal in most scenes; public confrontation remains a substantial alternative. There is no prescribed golden answer or agreeable-character quota. [Complete social receipts](reflection-social-2026-10-08.json).

## Two-week observer recordings

Seed 7, default town, quiet chance .35, waking dream chance .15, balanced evaluation. These are separate evolving towns: their actions change later opportunities, so totals are **not controlled comparisons of the same later scenes**.

| Recording | Thoughts | Dreams | Deferred / rejected | Intentions performed | Outcomes recorded | Expired | Revisited ideas |
|---|---:|---:|---:|---:|---:|---:|---:|
| Authored | 82 | 15 | 8 / 21 | 40 | 23 | 10 | 0 |
| Authored + Laya | 82 | 19 | 11 / 11 | 45 | 29 | 7 | 2 |
| Qwen + Laya | 86 | 22 | 5 / 18 | 48 | 34 | 5 | 1 |

Live Laya recorded in **9.7 s**, and hybrid in **105.2 s**. All 82 Laya answers and all 86 hybrid answers came from their configured models; no fallback in these final recordings. Hybrid made 410 evaluator passes. Its median generator service latency was **952.33 ms**, excluding adapter overhead and Laya. The authored recording took 1.3 s. These are small offline GPU runs, not real-time or CPU performance guarantees.

Deferred ideas return only after a distinct later known encounter with the same believed person and a full-day cooldown, at most twice per root. Rejection is terminal; mood drift alone cannot trigger a retry. Accepted intentions release their pending slot after acting while their consequences continue to be observed. Dreams require a recorded wake and knowledge held before that sleep; dream imagery is never ledger evidence.

The viewer now has Happening now cards, meaningful-moment navigation, a current-intention inspector, earlier-thought links and dream/source badges. Stories follow the clock unless Whole-run analysis is explicitly enabled. Manual browser checks verified dream display after waking, raw pass details, rewinding away future thoughts, and Space toggling the analysis checkbox without starting playback. The preview launcher has a reusable morning page and refuses to stop unrelated listeners.

Hybrid tape replay reproduces every JSON field except the command-derived label. Log hashes: authored `16b0adc14ab3ea1b`, Laya `6319b11f62df694f`, hybrid `a9f42bfdc31a146d`. Local full recordings and tapes remain in ignored `out/reflection-morning-*.json`; the root demo HTML files are saved local outputs.

## Small generator: working, still limited

The optional service pins official [Qwen/Qwen3-0.6B](https://huggingface.co/Qwen/Qwen3-0.6B) revision `c1899de289a04d12100db370d81485cdf75e47ca`, disables thinking, and uses temperature .7. The checkpoint was downloaded explicitly from the official repository; the service subsequently loads cache-only. Local runtime: Python 3.13.1, Transformers 5.17.0, PyTorch 2.14.0+cu130. Raw response, seed, revision, device, input/output counts and timing are preserved. Invalid JSON/choice/length produces explicit authored fallback. Cancellation requests worker termination; the single inference slot stays busy until the worker exits.

The first seven-day generation run produced 27 model thoughts and one overlength fallback. Most prose was clinical third-person analysis. A revised prompt requests concrete first-person silent self-talk, an uncertain future action, no copied speech and no unsupported past events. The final three-day high-opportunity probe produced 16 valid answers, but **6 copied offered dialogue verbatim and 15 selected the first offered executable action**. One thought ambiguously reversed a received-gift perspective. No prompt retries or action-frequency correction was used.

The two-week hybrid recording remains similarly repetitive. For example, Evelyn thinks “I need to be clear about that gesture, so I'll choose to argue instead of giving a gift.” The thought is structurally valid but self-conscious about action selection, rather than convincing inner speech. Structural validation cannot establish factual grounding or character voice. Review the authored and Laya recordings first, then use hybrid to identify generator failures. The next model experiment should isolate generator choice-order bias and source-perspective fidelity before enlarging the speech/action library.

## Reproduce

Commands and preview/runtime setup are in [sim/README](../../../sim/README.md). Trial CLI defaults to raw for measurement; Replay defaults to balanced. Use `--suite social --evaluation balanced` for the expanded trial, `--dreams` for the prepared dream setting, and identical simulation flags for tape replay. No social quality claim follows from passing tests or producing more events.
