# Quiet reflection and the observer prototype

## Status

Built as an opt-in vertical slice, 2026-10-08. The default town and existing replay format remain unchanged when reflection is off. The first product is an in-depth social simulation watched from above; player gameplay comes after its inhabitants are worth following.

Implemented: quiet-time and optional waking opportunities, nine editable daytime/dream proposals, evaluation of actual authored lines, bounded reconsideration after new experiences, a persistent intention, an ordinary later encounter, and an inspectable outcome. `UnderGlass.Minds` provides optional local HTTP adapters. The viewer's **Happening now**, **Next moment** and **Inner life** controls follow that chain and reveal each step only when the replay clock reaches it.

Not implemented: general dialogue generation, a large tagged speech library, seeking a person to fulfill an intention, long-term ambitions, or model tuning. The slice offers gifts, help and confrontation through existing act kinds. A thought cannot introduce an executable action the simulation does not support.

Observer update, 2026-10-09: the timeline, clock, playback and Previous/Next moment stay visible
while scrolling. Selecting a person adds a row for performed/received encounters, their own
thoughts/outcomes and relationship changes. Hover lists nearby recorded events; clicking pauses
at the nearest marker's exact minute. Future markers are dimmed and show only their time until
reached. Tooltips follow playback and rewinding, render model content as text, and preserve the
current-clock boundaries. Scrolling to a reflection reserves the bar's measured height.

The authored catalog contains nine thoughts, their distinct dream variants, and five response profiles. Real local trials exercise raw, canonical and balanced evaluation, plus four additional personalities. The pinned Qwen3-0.6B generator has also run on the local GPU. Balanced evaluation removes incoming label/order effects by construction; this does not establish model understanding. Generated thoughts still need qualitative review. See the [first trial](../experiments/reflection-trial-2026-10-08.md) and [reliability and generator measurements](../experiments/reflection-reliability-2026-10-08.md). The 1,250-character working limit is Sid's constraint, not a universal server context specification.

## Purpose and design decision

Ordinary language should mostly come from authored, editable lines tagged by voice, mood and context. Occasional imagination proposes something a character could do; a separate evaluator considers whether that person would accept, reshape, defer or reject it. “Consciousness” and “ego” are design metaphors, not claims about either model.

The imaginative proposal is grounded in the person's memory. It may change an intention, but it is never inserted as a witnessed fact. Laya sees the actual candidate lines and returns weights; one seeded draw chooses among them. Choosing a different executable line means reshaping the proposal. There is no retry until the model agrees and no blend back toward a preferred social outcome.

Conflict, withdrawal, failed reconciliation and unusually quiet or hostile towns can be interesting results. Existing cohort metrics remain regression diagnostics. Neither a quota of friendships and feuds nor a cap on extreme towns establishes that a relationship is interesting. Judge the observer experience by whether the person, the cause, the uncertainty and the consequence are understandable and worth following.

## The implemented loop

1. An awake, free person spends 30 quiet minutes at their current destination, outside work hours. Small idle wandering counts toward quiet time; travel and encounters interrupt it. A thought is requested only while stationary.
2. At most once per eligible day, a seeded roll with default chance 0.35 opens a quiet opportunity. An accepted but unperformed intention blocks another thought; an intention that has acted releases the slot while its outcome continues to be observed. These chances describe opportunities, not a daily production rate.
3. Choose among up to six recent, unreflected memories from the past seven days. Eligible sources are their own completed targeted deeds or firsthand beliefs about something done to them. Hearsay, unknown actors and `Patient.Actor` authority/family events are excluded in this slice; those events need explicit reversed-role templates. A mistaken identity stays mistaken in the request; world truth never corrects it behind the character's back.
4. Copy the memory, subject, mood, regard and temperament into a request. Structured source facts distinguish doing from receiving kindness or hostility. Choose a tagged authored proposal from the applicable catalog entries using a separate seeded stream. Offer complete response lines for supported gift/help/confront acts, plus defer and reject. Lines fit the source perspective and vary with expression, chattiness and boldness.
5. Obtain a thought and weights between simulation minutes. Apply the answer at the next minute regardless of wall-clock latency. Waiting for inference pauses simulation advancement; this is an offline recorder, not a claim of real-time background inference.
6. Rejection and deferral are visible settled records. The latest deferred idea about a person may return after a distinct later own deed or firsthand encounter with that person, at least one full day after the prior answer. At most two continuations share one root; mood drift or elapsed time alone never resubmits it. The new act supplies the causal source; the prior thought stays explicitly imagined, with root/prior IDs. Rejection is final for that chain. Acceptance or reshaping retains an intention for up to three days.
7. An intention can act only at a natural encounter. Both people must be free, stationary, outside work and in reach; age, location, scene eligibility, pair slots and hostile cooldown still apply. This route does not require the ordinary desire score to approve the same decision again.
8. The existing act, perception and feeling systems produce the consequences. The act's `About` points to the source memory. The thought records the resulting act ID and later outcome. An expired intention never fabricates an encounter.

Existing constraints may still suppress interesting possibilities; this slice makes that visible without retuning the whole town. Acceptance without an opportunity is itself an observable outcome.

### Waking dreams

`DreamChance` defaults to zero. `--dreams` enables a 0.15 waking chance; `--dream-chance` accepts an experimental probability in 0..1. An opportunity requires the person's actual recorded `Sleep.WokeAt`, is attempted once for that sleep, and draws only from memory known before `SleptAt`. It follows the same subject, firsthand, age, source-once and continuity rules as quiet reflection. There is no added read of hidden positions or other people's memory.

Dream rolls and memory selection have separate seeded streams. A successful dream and quiet reflection share one submitted thought per actor per day; a missed waking roll leaves the daytime opportunity available. Dream proposals use distinct authored `DreamThought` variants and an immutable `dream` tag. The request appends `ReflectionOpportunity(Kind, SleptAt, WokeAt)`; a null value means quiet time. The applied record includes `dreamed`, followed by `considered`. Dream imagery never enters the evidence ledger. Both providers receive an explicit imagination marker, and tape replay checks the exact sleep interval.

## Providers and determinism

| Mode | Thought | Evaluation |
|---|---|---|
| `authored` | Contextual authored proposal from the catalog | Fixed baseline weights, sampled with the seed |
| `laya` | The same contextual authored proposal | Laya evaluates the full candidate lines |
| `hybrid` | Configured local generative model | Laya evaluates the full candidate lines |
| `tape` | Previously recorded answer | Previously recorded weights; no model call |

The authored baseline gives the proposal's suggested act weight 3 and every alternative weight 1. It is an uncalibrated control, not evidence of model judgment. Fallback is labelled `authored-fallback`, with a reason.

Edit `sim/UnderGlass.Sim/reflection-catalog.json` and rebuild to change the authored content. Each thought declares an ID, own/received perspective, kindness/hostility/neutral tone, regard interval, required choice and descriptive tags. Overlapping regard intervals intentionally permit contradictory motives: reciprocity or suspicion, connection or boundaries, pride or repair, regret or doubling down. Applicability uses copied known facts; it never consults hidden world truth. Tags describe provenance rather than acting as an additional scoring system. The catalog falls back to a neutral memory-based possibility when an applicable act is unavailable. This is a small reflection catalog, not the general speech library.

The .NET 8 adapter has no Stardew or SMAPI dependency. It uses the existing source-verified Laya `/v1/systemone` protocol and an OpenAI-compatible `/v1/chat/completions` interface for a separately configured local generator. It installs nothing. Endpoints must be loopback HTTP(S); authentication is not implemented in this slice.

Each HTTP call has a five-second deadline including the response body, a bounded response size, and no retries. Generation proposes a short thought and an allowed choice ID. Laya must return finite, nonnegative weights for every supplied choice, with a positive total. The evaluator receives the thought but no explicit suggested-choice hint. A server response that reports truncation is rejected. Failure replaces both proposal and weights with the authored control. Caller cancellation stops the run rather than becoming fallback; the async runner checks before initialization and each minute, and after callbacks/answers, so cancellation cannot advance an extra minute or return success after the final callback.

Evaluation has three modes. `raw` preserves IDs and order; `canonical` groups identical `(Kind, Line)` alternatives, sorts them and assigns neutral labels; `balanced` rotates each distinct alternative through every neutral slot and averages normalized per-pass weights. Duplicate alternatives split their semantic mass equally across original IDs. All passes share the evaluation deadline. Partial results are discarded on failure, and only one simulation draw occurs. The API default remains raw for compatibility; Replay defaults to balanced and ReflectionTrial defaults to raw for measurement. Each pass records its exact prompt, raw response, label-to-original mapping and normalized weights/error. Legacy prompt/response fields describe the last pass.

The optional local service is `sim/tools/reflection_generator.py`, launched with `sim/run-generator.ps1` using the existing sidecar environment. It pins Qwen/Qwen3-0.6B revision `c1899de289a04d12100db370d81485cdf75e47ca`, uses non-thinking inference, and loads from cache unless explicitly asked to download. It accepts only loopback traffic, serializes inference, bounds request bytes/context/tokens, and requests worker cancellation at four seconds. Busy requests fail promptly; a worker retains its slot until it actually exits. The adapter supplies temperature 0.7 and a nonnegative SHA-256-derived request seed. Raw generator responses, including invalid content, are retained for audit. A thought must be valid JSON, at most 220 characters, and suggest an offered executable choice. The prompt asks for concrete first-person private self-talk grounded in known memory, without invented past events or hidden intentions. This is a prototype contract, not a guarantee against plausible-looking inventions.

The Laya packet caps semantic text at 1,250 characters: state, question instructions, choice IDs and full descriptions. JSON punctuation and field names are not included. Complete lines, the thought and minimum memory/context are reserved first; an oversized packet falls back rather than silently truncating lines. Long memory/context can be clipped by the adapter; the paired-scene trial explicitly fails its completeness audit if this happens. Exact JSON requests and raw server responses are retained, including usage and routing when supplied. The installed server reported no truncation in the recorded trial; that is not a guarantee for other packets or checkpoints.

Seeded simulation is reproducible with the same recorded answers. A live model can return a different answer for an identical request, so a seed alone does not promise identical hybrid runs. Tape mode checks the full request, including context, candidate lines, structured source and authored proposal/tags, before applying each recorded answer. Reuse the same seed, duration, town, catalog and simulation flags. A mismatch fails visibly; tapes from the old generic catalog cannot silently substitute for this one.

## Run and inspect

```bash
# No server needed; inspect the Inner life tab and advance the clock.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection authored --out reflection.json --html reflection.html

# Existing local Laya server; default URL is http://127.0.0.1:8000.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection laya --laya-url http://127.0.0.1:8000 --html laya.html

# Supply the URL and model ID of a server you already run.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection hybrid --llm-url http://127.0.0.1:8080 --llm-model YOUR_MODEL_ID --out hybrid.json --html hybrid.html

# Recorded decisions, without either server. Match the original simulation flags.
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --reflection tape --reflection-tape hybrid.json --html replayed.html
```

`--reflection-chance` changes the daily opportunity probability for an experiment. `--laya-model` defaults to the existing adapter's `typed-decisions`. Reflection requires feelings and desire to act; observe/off settings are rejected rather than silently bypassed. The batch sweep runner has no reflection flags.

In **Inner life**, follow one person, open the source encounter, inspect their imagined thought and chosen line, and jump to the later act or outcome. **Happening now** prioritizes active acts and recent thoughts; **Next moment** moves to meaningful events without revealing their content early. The inspector shows the selected person's current intention. Cards distinguish private imagination, dream and remembered evidence, identify authored/model/fallback sources, and link earlier thoughts. Debug details expose context, catalog tags, candidates, every evaluation pass and raw generation responses. Rewinding hides later answers and events; model text is rendered as text, never HTML. Stories also follow the clock; **Whole-run analysis** explicitly reveals end-of-run knowledge and turns off when jumping back to a source encounter.

The controlled trial uses the same adapter and makes no generation calls:

```bash
dotnet run -c Release --project sim/UnderGlass.ReflectionTrial -- --laya-url http://127.0.0.1:8000 --out reflection-trial.json --report reflection-trial.md
# Offline check of the harness only; explicitly labelled as authored.
dotnet run -c Release --project sim/UnderGlass.ReflectionTrial -- --authored --out authored-trial.json --report authored-trial.md
```

Five smoke pairs change remembered conduct, trust context, line-to-ID assignment, option order and option labels. `--suite social` adds four personalities, each with perspective, inspiration and order comparisons: 51 decisions, 291 passes in balanced mode. Every pair repeats its baseline afterward. Reports compare normalized distributions by ID and corresponding response meaning; total variation is half the summed absolute probability differences. Live mode exits nonzero on fallback/non-Laya answers or incomplete/truncated/unverified packets in any pass. JSON retains every request and answer. No probability quota is a quality target.

## Tests and the next decision

Scene tests cover private knowledge, misidentification, source perspective, acceptance, reshaping, rejection, deferral, physical opportunity, work and wandering, expiry, causal IDs, cancellation, model latency, recorded replay and disabled-mode behavior. Catalog tests check applicability, seeded ambivalence, context-specific lines, unsupported acts and real adapter packet budgets. Adapter tests cover the text budget, complete lines, HTTP schema, malformed/truncated answers, timeouts, cancellation and fallback. Trial tests check counterfactual isolation, comparisons and audits; viewer checks cover time filtering, safe text and outcome navigation.

Next, judge the complete observer scenes, including rejection, expiry, dreams and revisited ideas. Compare small generated proposals with the authored control and inspect factual grounding, voice and generator option bias. A [controlled generator probe](../experiments/reflection-generator-order-2026-10-08.md) found order sensitivity and copied speech. The generator now receives only supported act IDs/kinds and structured known-source roles; complete spoken lines go to Laya. This removes the direct copying channel, but the follow-up still found thoughts disagreeing with their proposed act and reversing the known source perspective. Hybrid remains experimental. A broader speech library and deliberate ambitions should build on that review. Do not tune toward a fixed proportion of agreeable characters or interesting-looking event counts.
