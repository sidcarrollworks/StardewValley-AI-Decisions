# Lives: history, evening plans and typed evaluation

## Status

Built, opt-in, 2026-10-15. This extends the [company slice](lives-company-spec.md) with a
reviewed Penny/Pam/Gus history pack, future shared-home games, later follow-through choices,
perceived outcomes and Laya evaluation. It retains the selected routine baseline: existing
body rules, bedtime weight 9, and no added bedtime penalty on invitations. A habitual bedtime
does not forbid proposing a plan. Law 1 and needs bars are unchanged.

This is an experimental runtime slice, not the full Lives observer 0.2 contract. The review
page reads the separate `livesEvenings` receipts. The main chain viewer now also reads these
version-1 receipts through a presentation-only adapter. Authored choices plus Laya isolate the evaluator here; this trial adds no generated
inspiration or Haiku calls. A completed game still has the catalog's generic positive affect.

## History and knowledge

`sim/history/penny-pam-gus.json` contains individually reviewed sources, accounts and readings;
see the [source review](../experiments/pilot-history-review-2026-10-15.md). Loading is explicit.
Each holder gets only their own accounts. The pack adds no shared omniscient biography, fake
pre-run act, or fixed relationship set-point. Present warmth and wariness remain separate small
seeds. Completed pasts move numbers only through admitted own present links. Pam's hobby wish
opens an option when recalled; it supplies no invented prior enjoyment of games with Penny.

The engine writes memory; observer receipts never feed choices. Model text cannot become an
event or witnessed evidence. Different people can hold incompatible readings of one practice.
The source review distinguishes canon, product decisions and our numeric/mechanical assumptions.

## Executable thread

1. An ordinary local encounter between housemates can suggest an evening game from recalled
   warmth toward the person/activity or an explicitly reviewed wish for that activity. One
   considered offer per pair/day and one active plan/person bound repetition. A positive desire
   to help, blocked by a `WontFetch` commitment, can also suggest company during the next local
   encounter within 30 minutes. The commitment itself remains absolute and never reaches Laya
   as an option to override.
2. Offering competes with leaving the evening open. Its fallback reads warmth/wariness and
   separate answer, availability, follow-through and expected-whereabouts evidence. Disappointment
   toward a prior evening is not automatically dislike of the person and is not read by offers.
3. The other person answers in a subsequent minute. Yes, not now and no use the same eligible
   alternatives for the fallback and Laya. Not now requires an existing temporary reason; it
   changes availability, not acceptance. Yes also adds positive availability evidence. A lost
   encounter produces no invented answer.
4. Yes creates an informal agreement. An hour before the proposed window, each free participant
   independently chooses to head home or carry on with other plans. Keeping reads remembered
   company, activity and the hoped-for evening, plus intended benefit and learned expectations.
   Carrying on reads their own available routine places. It leaves ordinary destination selection
   intact; the present binary menu does not expose each alternate destination separately.
5. Both must actually arrive, remain available, choose to keep the plan and have enough time
   for the game. The existing managed activity can finish or be interrupted by work, tiredness,
   collapse, other activity or separation. Routine bedtimes still cause their ordinary reconsideration.
6. Starting proves both followed through even if the game is later interrupted. Completion and
   interruption separately teach activity success; only completion gives shared joy, one cause
   projected toward person/place/activity. An observer who waited at least 15 minutes but never
   saw the partner can learn a missed follow-through. Their own choice to do something else does
   not become evidence against the partner. Seeing someone without playing does not prove absence.

An unfulfilled evening can disappoint the participant who kept the plan and observed its window.
The experimental amount is `-D × expectedAtAgreement × clamp((intended + ownWarmth)/scale, 0, 1)`;
defaults are D 0.05, intended 0.2 and scale 0.25. Expectation is frozen at agreement, before the
outcome. Mood and the specific hoped-for evening change. There is no automatic rejection,
betrayal, blame, whereabouts inference or regard penalty. A planned window does not also write
an ordinary whereabouts failure for its participants.

Gus's path uses the same general expectation watcher as any holder. Only an existing positive
personal whereabouts entry can start observation. A local sighting proves presence; absence
requires being awake in that place throughout the three-hour band. A miss changes that expectation
only. It may prompt an intention to offer help at the next actual encounter, or to leave it alone.
Help waits for ordinary physical/rate/cap eligibility, can expire after three days, and reveals
nothing about where the other person went. The help is still the catalog's generic action.

## Laya, determinism and bounds

Model evaluations occur between simulated minutes; selected answers are applied the next minute
after rechecking execution eligibility. Only torn choices call the model, at most twice per
person/day. Every holder has a distinct request ID and keyed draw. Laya supplies weights over
existing options; it neither picks an extra random draw nor creates options. The same fallback
and draw are retained beside the model answer. HTTP failure, malformed output, timeout or request
overflow produces a recorded authored fallback. Cancellation stops the run.

The complete serialized request is limited to 1,250 characters. Actual candidate lines, state
constraints and numeric reasons are mandatory. Whole recalled texts fit in the remaining space;
omitted record IDs are explicit in the packet receipt. Exact requests and raw answers are taped,
including configured/reported model identity. Tape replay rejects changed requests, extra/missing
answers and unused entries. A successful live trial must reproduce its complete recording exactly.

## Configuration and review

Configure routing, company, then `ConfigureLivesEvenings`. `EveningOptions` defaults to off;
when on, the proposal window is 18:00–20:00, preparation starts 60 minutes earlier, ordinary offer
base is 0.1, and the model budget is 2. These are first guesses, not canon. `Participants` can
restrict this experiment to a small cast; the code has no named-person branches. Recording can
be disabled independently without changing behavior.

```powershell
dotnet run --project sim/UnderGlass.LivesTrial -- --suite evenings --out out/lives-evenings-review --laya-url http://127.0.0.1:8000
```

The fixed matrix uses seeds 7, 29 and 101 for 35 days, with a separate live Laya seed-7 run when
requested. Current routines remain untouched. History comparisons remove one selected own reading
while holding the scene, other state, option order and draw fixed; these are fallback-only
comparisons, not alternative future lives or predictions of Laya without that history.
Additional seed-7 observer repeats isolate care and whereabouts. Their ordinary world output
must be identical. The review export keeps the full ordinary town recording and only the pilot's
Lives observations; model tape verification occurs before that observer projection.

The chain viewer follows the evening choices, agreements, personal outcomes and learning, including
Gus’s observations and deferred concern. Only supporting memory updates appear; unrelated routing
and company decisions stay outside this adapter. The proposed full observer contract is unchanged.
History sits in expandable branches, learning marks timeline changes, and character views hide
other minds and privileged plan outcomes. A keep-plan selection is shown as an intention because
the current receipt does not separately prove that a journey began. Missing prior values remain
unknown. Laya weights and authored factors are distinguished, not presented as model reasoning.

Next: review actual threads and packet omissions, complete the full observer export, then test
transfer to another household and richer activities. Do not
tune toward a quota of successful evenings, or treat this pilot as completion of the assessment's
commitment framework, learned routines, generated possibilities, or population scaling work.
