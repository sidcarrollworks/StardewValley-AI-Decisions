# Life chains viewer

## Status

Built 2026-10-14 as a read-only preview of the proposed Lives 0.1 observer contract. `sim/viewer/chains.html` uses a frozen illustrative Penny–Pam–Gus fixture. No Lives engine, runtime recorder, counterfactual execution or model trial service is implemented by this change. Claude's assessment, source corrections and contract remain separate work.

## Interaction

The main view is time, moving left to right on one marked timeline. Its playhead, event markers and ruler share the same scale. Three-hour, day, week and season windows keep long recordings inspectable. Nearby moments cluster into a count; clicking a cluster zooms into it and moves the clock to its first event. Window navigation also moves the playhead into that window. Starting history is never placed among run events. Event creation, choice answers, attempt resolution, outcome progress and personal acquisition each get their own recorded moment. Future markers disclose only a recorded moment/count, never its text or outcome.

The selected moment shows at most four compact event cards, with pages for additional simultaneous events. The Latest event controls select and highlight their card and its marker. Playback follows the latest recorded event automatically. Hover or keyboard focus previews the event's causal neighborhood without moving the clock; clicking pins it. Hovering an older marker uses the projection at that marker's own time. Back to now or Follow time restores playback focus. The default thread filter is All events; individual threads remain available.

A focused cause-and-effect tree replaces the category columns. One event sits between its recorded inputs and immediate consequences. Clicking a branch changes the focus; it does not make transitive descendants look like direct children. Only explicit links produce arrows. Attempts' outcome references point toward the consequence, not backward as a cause. Event branches show three items per page. Pre-run records are collapsed under Earlier life; memories formed during the run have a separate branch. Both are paged. Supporting/conflicting counts and colored badges describe recorded roles for the chosen option; unchosen-only citations are not treated as reasons for it. Recalled context remains distinct from support.

The tree stays small regardless of how many records a thread contains. Full details open only through the focused card or Open details, and close with the icon or Escape. Probabilities, chosen actions, attempt status and learning changes remain visible on expanded tree cards; exact keys, source data and explanations stay in disclosures. Blue/up, pink/down, neutral/equal and orange/new represent status or direction rather than good/bad. Symbols and values accompany colors. Acceptance remains distinct from completion; unavailable outcomes stay unknown. Mobile stacks the focused event and branches without horizontal page scrolling.

Shared navigation carries recording, time and person. A local import has no reusable server URL. Isolated comparisons remain behind an explicit observer-only control.

## Knowledge and time

`chains-model.js` is a pure adapter with exact 0.1 acceptance and basic structural checks, separate from DOM code. It does not reimplement the experiment's formula checker. Missing Lives blocks remain unavailable; unknown versions fail explicitly. Projection is immutable.

The adapter gates each record by `visibleAt`, records/plans by each holder's acquisition time, decisions by choice time, attempts by resolution time, outcomes by settlement and individual timeline entries, and expectation snapshots by `asOf`. Expectation displays apply only already-recorded learning updates. An earlier state missing from the excerpt is labelled unavailable rather than reconstructed. Plan windows can describe future intentions.

Character mode permits personal recalls, choices, attempts, perceptions and learning; shared records require `knownBy`. A targeted attempt is not automatically known: it needs an explicit acquisition or the person's own perception. This conservatively hides some recipient attempts where the excerpt lacks a receipt, including busy/no-ask. Private thought text is taken only from the matching actor's reflection tape and after the choice gate, labelled imagination rather than evidence.

Character mode has no raw-record inspector. Unknown fields, source/debug prose, world outcomes, other decisions, `notPerceived` and `observerLinks` are omitted. References only navigate to elements available under the same lens and clock. Observer mode is explicitly privileged. These are inspection lenses, not authorization boundaries: the complete local JSON remains on the user's machine.

## Comparisons and provenance

Observer mode offers an explicit Open isolated comparisons control after their own time gate. The fixture contains two fixed-decision history removals: care changes probabilities without changing the sampled action; the whereabouts expectation changes both. Both arms use the fallback with the same random input. The separate illustrative run choice used invented Laya probabilities. Neither arm runs forward, and nothing measures actual Haiku/Qwen + Laya quality here.

The UI shows recorded probabilities, selected alternatives and separate change indicators, then discloses reasons, interventions, fixed inputs and unavoidable derived differences. It does not manufacture missing comparisons. Real output needs a compatible contract and data; v0.2 requires a deliberate adapter update.

Archetypes remain separate from personal history. The fixture contains IDs without an immutable profile artifact; the UI says the definition is unavailable instead of substituting a current Studio draft. The frozen fixture retains its original illustrative labels and provenance, documented beside it. The shared loaders refuse illustrative excerpts in the full dashboard/speech tools.

## Verification

Thirty-five focused adapter tests cover missing/unknown formats, choice and thought gates, other minds and secret diagnostic text, unknowns and guesses, busy targets, acquisition gates, outcome settlement, attempt resolution, expectation rewind, thread summaries, dependency visibility, both comparison outcomes, formula independence, immutability, readable expectation/association labels, unknown-key fallback and personal versus observer attribution, and learning metric directions, missing priors and separate mood effects, choice/attempt status transitions and knowledge-safe current-moment highlights, chosen-option path roles, full downstream tracing, pending and future gates, shared-memory branch exclusion, cycle safety and projection immutability, event indexing/acquisition gates, time-specific marker summaries, all-event filtering, bounded six-month timeline clustering and immediate causal neighborhoods. Shared navigation adds the full-recording boundary and chain round trip. Browser review covers marked playback/rewind, cluster zoom, keyboard preview, pinning, latest-event selection, collapsed history pagination, detail opening/closing, responsive layout, and a synthetic month-six choice with 3,000 additional pre-run memories. The density fixture is a UI stress case, not a real simulated life.

## Next

Adopt the agreed revised contract and recorded pilot output, pin immutable archetype provenance, then connect the same inspector/comparison components to fixed-scene Studio trials and dashboard selection. Compact/lazy recording storage is not implemented; this preview indexes moment IDs per lens and filters a selected thread or all events. It bounds markers and rendered branches, but still loads and projects the recording in memory. Population-scale performance still needs measured real recordings.
