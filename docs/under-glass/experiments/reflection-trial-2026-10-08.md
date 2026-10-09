# Contextual reflection: first local Laya trial

Laya responds to the changed memories in these scenes, but formatting also changes its answer substantially. This is a working integration and a useful measurement, not evidence that it understands a character consistently. Do not tune a friendship or confrontation quota from this result.

## What ran

- Local date: 2026-10-08. Windows, NVIDIA GeForce RTX 4070 Ti, Laya 0.3.22, PyTorch 2.14.0+cu130, CUDA.
- Existing cached `typed-decisions` checkpoint; health revision `7b928d828b7b0e022f929d9bd2e44165aa270148`. Server reported CUDA placement and zero CPU fallbacks. No model was downloaded or installed for this trial.
- `UnderGlass.ReflectionTrial`: five controlled pairs, each baseline → variant → exact baseline repeat, 15 sequential calls. No generative model. The thought is held fixed and the explicit suggested-choice hint is omitted from Laya's input.
- All 15 answers came from Laya, with zero authored fallbacks. All packets preserved full thought, memory, context and candidate lines within the 1,250-character working budget. Server usage reported no dropped state tokens or truncated questions.
- Adapter round-trip latency: median **20.7 ms**, range **19.0–94.3 ms**, including HTTP/serialization. The first call was the slowest. This small GPU sample is not a throughput or CPU benchmark.

The [raw JSON](reflection-trial-2026-10-08.json) contains all requests, submitted packets, answers, timing and audits. The [generated report](reflection-trial-2026-10-08-tables.md) has the per-line comparisons. These are fictional scenes from the test fixture.

## Results and interpretation

Total variation (TV) is half the summed absolute probability differences. It ranges from zero (unchanged) to one (no shared probability mass). Comparisons below align corresponding response meanings, even when labels change. These are model weights normalized for sampling, not calibrated probabilities of human behavior.

| Changed input | TV | Interpretation |
|---|---:|---|
| Memory: praised → mocked | 0.0687 | Help falls from 40.23% to 35.08%; private confrontation rises from 19.65% to 24.76%. A plausible local shift. |
| Context: trusts → distrusts | 0.0379 | Private confrontation falls from 29.42% to 26.14%; public confrontation and help rise slightly. Context matters, but the preferred interpretation is not prescribed. |
| Swap the private/public confrontation lines between IDs | 0.1031 | The same private line goes from 19.65% to 29.80% under its other ID/position. This is an invariance failure, not proof of deeper semantic understanding. |
| Reverse choice order | 0.0330 | Placement alone moves almost as much mass as the trust edit. |
| Rename opaque choice IDs | 0.0643 | Arbitrary labels move nearly as much mass as the memory edit. |

All five exact baseline repeats have TV 0. One repeat per scene establishes observed stability here, not a statistical bound. The installed Laya source renders criterion IDs into the model text and preserves their order, so those controls are necessary. Its separate tokenizer budget can also truncate even a packet inside our character budget; the adapter now rejects responses that report such truncation.

Help remains the most likely individual response in every packet, at roughly 32–40%, but that does not mean a sampled character always helps. Every pair concerns Penny/Pam and the shared thought itself mentions offering help. This trial cannot establish a global reconciliation bias, personality fidelity or the model's general worldview.

## Seven-day observer replay

Seed 7, seven days, default town and reflection opportunity rate, final contextual catalog:

| Measurement | Authored control | Authored thoughts + Laya |
|---|---:|---:|
| Reflections | 25 | 27 |
| Selected gift / help / confront | 6 / 10 / 3 | 11 / 10 / 0 |
| Deferred / rejected | 3 / 3 | 4 / 2 |
| Accepted / reshaped | 12 / 7 | 11 / 10 |
| Acts performed by end of recording | 14 | 18 |
| Outcomes recorded by end | 9 | 13 |
| Expired without an encounter | 2 | 1 |

Both runs use one seeded draw per answer. Their different actions change later memories and opportunities, so their later scenes are not controlled pairs. Neither total establishes interestingness. The live run used seven of the nine catalog entries, all 27 responses were `authored+laya`, and all retained full context/memory/lines with no reported server truncation. Recording took about 1.5 seconds total. Tape replay matches every output field except the command-derived run label; live log hash `e69aa2224a9956ee`, authored `be45a40354e2b89a`.

Examples to inspect in Inner life:

- Leah begins suspicious of Jodi's kindness, chooses a thank-you gift, later meets Jodi and receives a returned outcome. The reply visibly reshapes the proposed motive; it does not prove why Laya did so.
- Alex rejects the idea of offering Jas help after an earlier gift. Jas later rejects a reciprocal gift idea. Neither thought produces an act.
- Gus wants to spend more time with Emily by helping, but the intention expires before a suitable encounter. A thought need not become an event.

The sample's lack of selected confrontations remains visible. It is a reason to inspect more scenes and initial motives, not to silently boost aggression until a target rate appears.

## Reproduce

Start the existing local Laya server as documented in `sidecar/README.md`, then from the repository root:

```bash
dotnet run -c Release --project sim/UnderGlass.ReflectionTrial -- --laya-url http://127.0.0.1:8000 --model typed-decisions --out trial.json --report trial.md
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection laya --out live.json --html live.html
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection tape --reflection-tape live.json --out tape.json
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection authored --out authored.json
```

Fresh live answers may vary with server/checkpoint settings; use recorded answers for exact replay. The nine-entry catalog is embedded from `sim/UnderGlass.Sim/reflection-catalog.json`. Editing and rebuilding it changes future requests and intentionally invalidates mismatched old tapes.

Next: investigate label/position sensitivity across more personalities and initial motives, then assess complete watched scenes. Compare a real generator against the authored proposals only after that. Dreams and automatic reconsideration remain later work; a generator has not been live-validated here.
