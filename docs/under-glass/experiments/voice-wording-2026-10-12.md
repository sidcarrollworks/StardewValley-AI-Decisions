# Fixed-intention voice wording — 2026-10-12

Case data: [`sim/voice-wording-cases.json`](../../../sim/voice-wording-cases.json). This is a small **private-thought wording experiment**, separate from the simulation. It contains Vincent, Maru and George in two common prepared situations: receiving useful help, or receiving a liked gift. In every case the already-chosen intention is to thank Newcomer. The experiment does not decide whether the character should be grateful, select a different action, or execute one.

## What stays fixed

The two scene templates, target, relationship and stance are identical across characters except for the actor name. The motive is gratitude for the stated kindness; no debt, new task, invented item, hidden feeling, reciprocal gift, therapy, apology or changed relationship is supplied. The help task and gift item are deliberately unspecified. A generated thought must preserve the plan to thank; it must not replace that plan with remembering the kindness, directly deliver the spoken thank-you, or claim thanks already happened.

The help case also ends in `Thanked`. Under Glass currently gives `HelpedSomeone` a minimum age of ten while its prototype Vincent is seven; these cases do not turn a wording experiment into an exception to that gate. The numbers describe the simulation prototype, not canonical Stardew ages.

There are two freshly authored comparison thoughts per case. They are possible human-written controls, not canonical utterances or the only correct answers. They stay out of every model prompt. In particular, the retired cadence examples from the earlier profile experiment must not be put back into the generator through this comparison. Generated arms should differ only in the inclusion of the selected game-dialogue references, with the same scene, plan, sampling settings and output budget.

## Source selection and observations

This selection follows the earlier full recognized-corpus review and a fresh reread of the three ordinary dialogue tables. It uses four short, explicitly attributed ordinary `Characters/Dialogue` records per actor. Each reference names the exact corpus `recordId` and zero-based parsed `nodeIndex`; every selected node is index 0, `kind=text`, `role=npc`, with no parse warnings, conditional text, alternate random branch or gender variant. Structural later pages in a record are not silently flattened into the selected excerpt.

- **Vincent:** short complete clauses, excited wants, concrete choices, and a child's view of adults. His everyday lines can reason about competing wants. Persistent baby talk, invented misspellings, and making every thought about toys would be an exaggeration. The selected weekday and friendship-tier lines demonstrate cadence, not a new scene involving his mother, brother or teacher.
- **Maru:** clear everyday explanations and practical evaluations, but also ordinary irritation and simple pleasure. Technical vocabulary is contextual; a thank-you does not need a fabricated machine, repair, experiment or scientific metaphor to qualify as her voice.
- **George:** reserved, economical remarks with appraisal and occasional warmth. A small sincere thank-you can remain terse. His gruffness must not turn a fixed gratitude plan into a refusal, demand, insult, or invented reconciliation. Retirement and older age are supported; no exact canonical age is asserted.

The narrow, shared gratitude plan leaves limited room for obvious character differences. A plausible ordinary thought might fit more than one person. Do not reward a catchphrase or new fact just because it makes identity easier to guess, and do not treat weak separation in six cases as proof that a model cannot express any distinct voice.

## Exact reference handles

The committed files contain keys and derived observations, not game dialogue. The CLI loader resolves actual text from the global `out/stardew-dialogue-2026-10-12/corpus.json` and cross-checks it against `raw-assets.json` for the ignored local report. The per-character files were reading aids during curation, not the CLI input. File membership alone is insufficient: attribution and the selected node must match the actor. Preserve source asset hash, record-level conditions, node-level conditions and variant in the receipt. A reference's conditional context is never the prepared scene's history.

| Actor | Corpus record ID | Node | Retained record conditions |
| --- | --- | --- | --- |
| Vincent | `Characters/Dialogue/Vincent:Mon` | 0 | `{"rawKey":"Mon"}` |
| Vincent | `Characters/Dialogue/Vincent:Wed` | 0 | `{"rawKey":"Wed"}` |
| Vincent | `Characters/Dialogue/Vincent:Sat` | 0 | `{"rawKey":"Sat"}` |
| Vincent | `Characters/Dialogue/Vincent:Thu2` | 0 | `{"rawKey":"Thu2"}` |
| Maru | `Characters/Dialogue/Maru:Mon` | 0 | `{"rawKey":"Mon"}` |
| Maru | `Characters/Dialogue/Maru:Sat2` | 0 | `{"rawKey":"Sat2"}` |
| Maru | `Characters/Dialogue/Maru:fall_Wed` | 0 | `{"rawKey":"fall_Wed"}` |
| Maru | `Characters/Dialogue/Maru:fall_Tue` | 0 | `{"rawKey":"fall_Tue"}` |
| George | `Characters/Dialogue/George:Mon` | 0 | `{"rawKey":"Mon"}` |
| George | `Characters/Dialogue/George:Fri` | 0 | `{"rawKey":"Fri"}` |
| George | `Characters/Dialogue/George:Sat` | 0 | `{"rawKey":"Sat"}` |
| George | `Characters/Dialogue/George:Mon6` | 0 | `{"rawKey":"Mon6"}` |

`Thu2`, `Sat2` and `Mon6` are source relationship-tier keys; `fall_*` keys are seasonal. The corpus retains these raw keys rather than pretending its parser has reconstructed every game dispatch condition. Weekday, season, friendship and topic restrictions belong to the source example. They do not establish the test's current date, intimacy, employment, relatives, or other events.

## Life-stage provenance

The installed game data-model enum `StardewValley.GameData.Characters.NpcAge` declares `Adult=0`, `Teen=1`, `Child=2`. This was checked in the local decompile under `%USERPROFILE%/stardew-decompiled/gamedata/StardewValley/GameData/Characters/NpcAge.cs`, against the installed data-model assembly. `Data/Characters` supplies Vincent=2, Maru=1, George=0. These are broad game categories, not ages in years. George's `Sat` and `Fri4` dialogue additionally support retirement and older adulthood; Maru's game `Teen` label is not silently replaced by the prototype's numeric age.

| Source | SHA256 |
| --- | --- |
| Installed Stardew Valley assembly | `7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE` |
| Installed StardewValley.GameData.dll | `9C03497C2D2AC24C94E2F25B3C2FC39ECDE1BC97341E514C5F9FDCC1E759CB81` |
| `Data/Characters` | `3B9519BF4887B557FB73C68834871C16755616A7EF26D84DC0BDBD95447F64A8` |
| `Characters/Dialogue/Vincent` | `4CAE344333DE0679B65B4652CAAAE615D6F31CBB3C245304C326D0388881C7A3` |
| `Characters/Dialogue/Maru` | `E6FFD98625E829C65630819CFEDC2770AB2A9E7CA54F74D20D4AEA30E0B83A22` |
| `Characters/Dialogue/George` | `C509FB0EAD16C20748D94159D20CBA4E79F05B9BACE0C251D07BBBD62287A901` |

## Review and limits

Review each result for plan preservation, scene grounding, private-thought form, plausible life stage, and cadence. Mark pass, fail or unclear with the actual offending or supporting words. Report copying separately: a short generic expression can be normal overlap, while reproducing a distinctive reference phrase or importing its family/task is not successful new wording. Naturalness and recognizability should be read blind to arm/model where practical.

JSON validity, nonempty text, or matching one keyword is not a voice-quality score. The authored controls are also subject to criticism; their function is to make a fixed-plan comparison inspectable, not to define an automatic pass string. The selected lines are curated, not a random sample, and named characters can activate a model's prior knowledge independently of the references. Preparation checks alone say nothing about model quality; the recorded runs are reviewed below.

Validation at preparation: six unique cases, three actors with two matched source situations each; twelve authored private thoughts; four references per actor, all resolving to clean, explicitly attributed nodes; same target/relationship/stance across all cases. The exact `VoiceWordingPack.Read` and `VoiceWordingTrial.LoadReferences` code also passed a local smoke check on the case data and ignored global corpus/raw manifest: six cases, twelve unique selected nodes, with no model calls. Each reference's raw text and source hash matched the supplied export manifest (`RawSourceManifestVerified`); this is not independent verification of the parser or installed XNB bytes. The initial `George:Thu6` selection was rejected for a player-name placeholder and replaced by clean `George:Mon6`. The existing runtime inspiration pack was not edited.

## Recorded method and receipts

Two local runs completed on 2026-10-12. Each contains six curated cases × two arms (`profile-only`, `dialogue-reference`) × two replicates = 24 replies. Within a run, the reference arm adds only the four selected dialogue excerpts; scene, intended thanks, profile voice, seed and sampling settings match. Authored comparison thoughts and personal biography do not enter either prompt. No Laya evaluation, action selection, simulation action or fallback is involved.

The first run used a structured JSON scene frame, internally `json-frame-v1`; its older output has no explicit `promptVersion` field. Its preserved files are named `structured-v1`. The second and final run, `plain-frame-v2`, presents the same scene and constraints as direct self-talk instructions, explicitly saying that the thanks have not happened. This is one revised framing attempt, not an isolated test of a single changed sentence. Exact HTTP requests and raw responses remain in both JSON artifacts.

| Setting | Recorded value, both runs |
| --- | --- |
| Generator | `Qwen/Qwen3-0.6B` |
| Pinned revision | `c1899de289a04d12100db370d81485cdf75e47ca` |
| Receipt device | `cuda` |
| Sampling | temperature `0.7`; maximum `120` output tokens; JSON-object response request |
| Thought contract | Exactly one `thought` string, 1–180 characters; private future plan |
| Prompt ceiling | 6,000 characters; references are not silently truncated |
| Deadlines | CLI `--timeout-ms 5000`; local generator default 4 seconds |
| Service preparation | Existing cached model, offline, launcher `-Device cuda -Warmup` |
| Completion receipts | All 48 report the requested model and seed, the revision above, and finish reason `stop` |

The twelve case/replicate seeds match across arms **and** prompt versions. Each request seed also equals its server receipt. They are deterministic hashes of case ID and replicate, not separately chosen favorable samples.

| Case | Replicate 1 seed | Replicate 2 seed |
| --- | --- | --- |
| `george-gift-thanks` | `1920288823704230465` | `3083664626311309045` |
| `george-help-thanks` | `8480874292811420136` | `1079911120932658259` |
| `maru-gift-thanks` | `70505070861885502` | `5563597633308837510` |
| `maru-help-thanks` | `2533741593142084168` | `6786596302410158426` |
| `vincent-gift-thanks` | `4434080816128553118` | `1030529970976033941` |
| `vincent-help-thanks` | `2433070821377865836` | `2176609511891616439` |

These are local ignored artifacts, including the dialogue text; the links require this checkout's `out/` files. Preserve the unsuccessful run alongside the revision:

- First run: [comparison HTML](../../../out/voice-wording-2026-10-12/structured-v1.html), [report](../../../out/voice-wording-2026-10-12/structured-v1.md), [JSON receipts](../../../out/voice-wording-2026-10-12/structured-v1.json).
- Final run: [comparison HTML](../../../out/voice-wording-2026-10-12/comparison.html), [report](../../../out/voice-wording-2026-10-12/comparison.md), [JSON receipts](../../../out/voice-wording-2026-10-12/comparison.json).

## Results: format improved; distinct voice remains unresolved

| Recorded measure | First structured frame | Final plain frame |
| --- | --- | --- |
| Replies | 24 | 24 |
| Format failures | 18: 17 over the length limit, one extra field | 0 |
| Automatically flagged source-copy replies | 0 under the old check | 6, all in the 12 reference-arm replies |
| Client latency, median (range), ms | 1,010.0 (542.6–2,672.3) | 473.4 (317.3–1,095.7) |
| Server-reported latency, median (range), ms | 995.3 (541.2–2,657.5) | 472.0 (316.0–1,094.5) |
| Input tokens per request, range | 309–406 | 303–382 |
| Output tokens per response, range | 26–71 | 14–45 |

These timings describe this warmed local service and these short requests, not a general hardware benchmark. The second run also produced shorter outputs, so the elapsed-time difference is not a controlled throughput comparison.

Reading the **raw** first-run replies found 22 third-person descriptions and two replies reproducing George's source dialogue. The old copy check ran only on format-valid text; both copied replies failed format validation, so its zero automatic flags did not establish zero copying. The six technically valid replies still used a narrator rather than private self-talk.

The final frame removed the third-person narrator pattern and all format failures. It did not establish semantic success. Its six flagged replies are genuine source copying: four George replies concatenate the selected `George:Mon` and `George:Fri` lines, while two Vincent replies reproduce `Vincent:Mon` about his older brother. Those source topics do not express the fixed thanks. The six responses produce ten individual reference matches because each George reply matches two source records. The overlap heuristic is evidence of these copies, not a complete detector or a guarantee that unflagged text preserves meaning.

An independent human review of the final 24 replies, checking the **combined** future-thanks plan, private-thought form and grounding, recorded:

| Arm | Clear pass | Unclear | Fail |
| --- | --- | --- | --- |
| Profile only | 0 | 7 | 5 |
| Dialogue references | 1 | 2 | 9 |
| Total | 1 | 9 | 14 |

These are judgments on this small set, not automatic quality scores. The nine unclear replies include imperative self-reminders and statements of gratitude that leave future thanks unstated; they are not all contradictions. The fourteen failures comprise six irrelevant source copies, four completed thank-yous, two present spoken/performed thank-yous, one unsupported prior thank-you implied by “again,” and one shift toward continuing a conversation. The sole clear meaning/form pass is still formal and generic; none clearly demonstrates novel, scene-faithful, distinctive character voice together.

Vincent makes the difference between validity, meaning and voice visible. All examples below are generated text or freshly authored controls, not quotations of the game's copied dialogue:

| Fixed case / reply | Generated thought | Held-out authored comparison | Review |
| --- | --- | --- | --- |
| Gift, references, replicate 1 | “I want to thank the newcomer for the gift and express my gratitude.” | “They gave me something nice. I'll tell them thanks!” | Preserves the future plan, but “express my gratitude” supplies little distinctive child cadence. |
| Gift, profile only, replicate 2 | “I thanked the newcomer for the gift and expressed my gratitude for their kindness.” | “Ooh, I like my gift! I wanna thank them.” | Reports completed thanks instead of the supplied intention. |
| Help, references, replicate 2 | “I really appreciate the help, and I hope we can continue this conversation.” | “They helped me! I wanna tell them thanks.” | Adds a desire to continue a conversation and leaves the intended thanks behind. |

The authored versions are optional human wording controls, not proof of a uniquely correct Vincent voice. Maru also illustrates limited separation: both arms produce identical wording in two matched replicates, including one completed-action sentence. Stronger recognizable mannerisms would not excuse changes to the plan or copied source situations.

Stop after these two prompt frames. Keep the artifacts available for review; do not tune repeatedly against these six cases or promote this result into runtime generation. This experiment changes neither simulation decisions nor the runtime character profile pack. It supports a narrow conclusion: this model, these references and these two prompt frames have not yet demonstrated reliable, distinct private wording for the prepared intentions. Six curated cases with two replicates cannot establish general model capability, broad character coverage, or story quality.

## Input identity and reproduction

Both result files report case `packHash` `37e58b2ec12da0cf5c3d6f7a808ad960505d1535ebe1edc70f8f7b437f9b9c12`, profile `profilePackHash` `67aff67c40e47fb339d8ed304328b3e7647671a7cc6755496dd73ed4e4cc5c4e`, and corpus `corpusHash` `8db33c9a15d1d31949e4111c9b94a63f14bf37f5869c5eda6c768cf219258bda`. Pack content hashes are computed by the loaders; they differ from raw file hashes. The following SHA256 values identify the actual input and result files read for this review:

| File | File SHA256 |
| --- | --- |
| `sim/voice-wording-cases.json` | `8e835045cb30c24e96b3846299b9fa527f785b4fdb829ec228f8d47fb2288529` |
| `sim/character-inspiration.json` | `b1277a098bcf945d8a485bbb5ba7df9ca893aff095219c8875c509ee9382a63e` |
| `out/stardew-dialogue-2026-10-12/corpus.json` | `8db33c9a15d1d31949e4111c9b94a63f14bf37f5869c5eda6c768cf219258bda` |
| `out/stardew-dialogue-2026-10-12/raw-assets.json` | `1396930b5fe40578e1a50f9ac07221a017426645d4c79f6234f454f020e27e30` |
| `out/voice-wording-2026-10-12/structured-v1.json` | `11a192532a2aaa6217572bed46c21e2dbd8d3a8e5c0225e87210c727e38bbdd3` |
| `out/voice-wording-2026-10-12/comparison.json` | `22a7c4630bad23a151a9e4b1cec9763824fbe2ea70e82149230488e20aa116d5` |

For an intentional future rerun of the **final** frame, use the same cached, pinned generator service on `127.0.0.1:8080` and run from the repository root. The service was launched with `pwsh -NoProfile -File sim/run-generator.ps1 -Device cuda -Warmup`; this command runs it in the foreground. The trial command below writes separate rerun artifacts and includes the raw source manifest check:

```powershell
dotnet run --project sim/UnderGlass.ReflectionTrial -- --suite wording --llm-url http://127.0.0.1:8080 --llm-model Qwen/Qwen3-0.6B --character-inspiration sim/character-inspiration.json --voice-cases sim/voice-wording-cases.json --dialogue-corpus out/stardew-dialogue-2026-10-12/corpus.json --dialogue-manifest out/stardew-dialogue-2026-10-12/raw-assets.json --timeout-ms 5000 --out out/voice-wording-2026-10-12/rerun.json --report out/voice-wording-2026-10-12/rerun.md --html out/voice-wording-2026-10-12/rerun.html
```

There is no CLI switch to select the retired first frame; its exact request bodies are preserved in `structured-v1.json`. Matching seeds and revisions make the comparison inspectable, but do not promise identical results across different numerical runtimes or devices. Check result hashes, routing receipts and prompts before interpreting a rerun. The recorded final CLI exit code of zero means the structural checks passed, not that the model's meaning or voice passed human review.

The cases are reusable, but the current harness expects this local service's receipt contract: model identity, echoed seed, pinned revision, device and normal finish. Another endpoint must supply equivalent metadata or have its receipt adapter explicitly updated before its results count as technically complete; an arbitrary generation API is not interchangeable merely because it accepts a chat-completions request.
