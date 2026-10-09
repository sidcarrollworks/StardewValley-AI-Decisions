# Quiet reflection and the observer prototype

## Status

Built as an opt-in vertical slice, 2026-10-08. The default town and existing replay format remain unchanged when reflection is off. The first product is an in-depth social simulation watched from above; player gameplay comes after its inhabitants are worth following.

Implemented: a quiet-time opportunity, a private thought about a known encounter, evaluation of actual authored lines, a persistent intention, an ordinary later encounter, and an inspectable outcome. `UnderGlass.Minds` provides optional local HTTP adapters. The viewer's **Inner life** tab follows that chain and reveals each step only when the replay clock reaches it.

Not implemented: dreams, general dialogue generation, a large tagged speech library, reconsidering a deferred thought, seeking a person to fulfill an intention, long-term ambitions, or model tuning. The first slice offers gifts, help and confrontation through existing act kinds. A thought cannot introduce an executable action the simulation does not support.

The authored catalog now contains nine thoughts and five response profiles. A real local Laya trial completed 15 paired-scene evaluations without fallback or input truncation on 2026-10-08; a seven-day replay also completed with live evaluation. Context changes affect its weights, but arbitrary label and position changes also have substantial effects. This establishes working integration, not convincing character judgment. See the [measurements and limitations](../experiments/reflection-trial-2026-10-08.md). A real generative service remains unverified. The 1,250-character working limit is Sid's constraint, not a universal server context specification.

## Purpose and design decision

Ordinary language should mostly come from authored, editable lines tagged by voice, mood and context. Occasional imagination proposes something a character could do; a separate evaluator considers whether that person would accept, reshape, defer or reject it. “Consciousness” and “ego” are design metaphors, not claims about either model.

The imaginative proposal is grounded in the person's memory. It may change an intention, but it is never inserted as a witnessed fact. Laya sees the actual candidate lines and returns weights; one seeded draw chooses among them. Choosing a different executable line means reshaping the proposal. There is no retry until the model agrees and no blend back toward a preferred social outcome.

Conflict, withdrawal, failed reconciliation and unusually quiet or hostile towns can be interesting results. Existing cohort metrics remain regression diagnostics. Neither a quota of friendships and feuds nor a cap on extreme towns establishes that a relationship is interesting. Judge the observer experience by whether the person, the cause, the uncertainty and the consequence are understandable and worth following.

## The implemented loop

1. An awake, free person spends 30 quiet minutes at their current destination, outside work hours. Small idle wandering counts toward quiet time; travel and encounters interrupt it. A thought is requested only while stationary.
2. At most once per eligible day, a seeded roll with default chance 0.35 opens an opportunity. A person with an unresolved thought waits until its outcome is recorded; this can take the existing outcome window (normally seven days). The daily chance is therefore an opportunity probability, not a daily production rate.
3. Choose among up to six recent, unreflected memories from the past seven days. Eligible sources are their own completed targeted deeds or firsthand beliefs about something done to them. Hearsay, unknown actors and `Patient.Actor` authority/family events are excluded in this slice; those events need explicit reversed-role templates. A mistaken identity stays mistaken in the request; world truth never corrects it behind the character's back.
4. Copy the memory, subject, mood, regard and temperament into a request. Structured source facts distinguish doing from receiving kindness or hostility. Choose a tagged authored proposal from the applicable catalog entries using a separate seeded stream. Offer complete response lines for supported gift/help/confront acts, plus defer and reject. Lines fit the source perspective and vary with expression, chattiness and boldness.
5. Obtain a thought and weights between simulation minutes. Apply the answer at the next minute regardless of wall-clock latency. Waiting for inference pauses simulation advancement; this is an offline recorder, not a claim of real-time background inference.
6. Rejection and deferral are visible terminal records. Deferral currently means no action and no automatic reconsideration of that source. Acceptance or reshaping retains an intention for up to three days.
7. An intention can act only at a natural encounter. Both people must be free, stationary, outside work and in reach; age, location, scene eligibility, pair slots and hostile cooldown still apply. This route does not require the ordinary desire score to approve the same decision again.
8. The existing act, perception and feeling systems produce the consequences. The act's `About` points to the source memory. The thought records the resulting act ID and later outcome. An expired intention never fabricates an encounter.

Existing constraints may still suppress interesting possibilities; this slice makes that visible without retuning the whole town. Acceptance without an opportunity is itself an observable outcome.

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

Each HTTP call has a five-second deadline including the response body, a bounded response size, and no retries. Generation proposes a short thought and an allowed choice ID. Laya must return finite, nonnegative weights for every supplied choice, with a positive total. The evaluator receives the thought but no explicit suggested-choice hint. A server response that reports truncation is rejected. Failure replaces both proposal and weights with the authored control. Cancellation stops the run rather than becoming fallback.

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

In **Inner life**, follow one person, open the source encounter, inspect their imagined thought and chosen line, and jump to the later act or outcome. The card distinguishes private imagination from evidence and identifies authored, model and fallback sources. Debug details expose context, catalog ID/tags, every candidate, weights, exact requests and raw responses. Rewinding hides later answers and events; model text is rendered as text, never HTML.

The controlled trial uses the same adapter and makes no generation calls:

```bash
dotnet run -c Release --project sim/UnderGlass.ReflectionTrial -- --laya-url http://127.0.0.1:8000 --out reflection-trial.json --report reflection-trial.md
# Offline check of the harness only; explicitly labelled as authored.
dotnet run -c Release --project sim/UnderGlass.ReflectionTrial -- --authored --out authored-trial.json --report authored-trial.md
```

Five pairs change remembered conduct, trust context, line-to-ID assignment, option order and option labels. Every pair repeats its baseline afterward. Reports compare normalized distributions both by ID and by the corresponding response meaning; total variation is half the summed absolute probability differences. The line swap is an invariance test: the same line should not acquire a different weight merely because it moved to another ID/position. Live mode exits nonzero on fallback/non-Laya answers or incomplete/truncated/unverified packets. JSON retains every request and answer for inspection. No probability quota is treated as a quality target.

## Tests and the next decision

Scene tests cover private knowledge, misidentification, source perspective, acceptance, reshaping, rejection, deferral, physical opportunity, work and wandering, expiry, causal IDs, cancellation, model latency, recorded replay and disabled-mode behavior. Catalog tests check applicability, seeded ambivalence, context-specific lines, unsupported acts and real adapter packet budgets. Adapter tests cover the text budget, complete lines, HTTP schema, malformed/truncated answers, timeouts, cancellation and fallback. Trial tests check counterfactual isolation, comparisons and audits; viewer checks cover time filtering, safe text and outcome navigation.

Next, address the measured sensitivity to labels and placement before treating model weights as dependable characterization. Expand the trial across personalities and proposed motives, including ideas that start from anger or withdrawal. Follow complete replay scenes and review rejected/expired thoughts too. Then compare a configured generator plus Laya against these authored proposals. Dreams, a wider speech library and persistent reconsideration follow only when this loop is worth watching. Do not tune toward a fixed proportion of agreeable characters or interesting-looking event counts.
