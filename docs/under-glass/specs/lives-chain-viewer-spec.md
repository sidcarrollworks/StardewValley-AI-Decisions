# Life chains viewer

## Status

Built 2026-10-14 as a read-only preview of the proposed Lives 0.1 observer contract. `sim/viewer/chains.html` uses a frozen illustrative Penny–Pam–Gus fixture. No Lives engine, runtime recorder, counterfactual execution or model trial service is implemented by this change. Claude's assessment, source corrections and contract remain separate work.

## Interaction

The initial view is Penny's first decision, under Character knowledge. Stage columns contain history, recall/opportunities, choices, attempts/plans, perceptions and learning; Observer mode adds world outcomes. Only recorded dependency references create graph links. Selecting a node highlights its dependencies and consequences, draws their links when both nodes are displayed, and opens a details tray below the full-width graph. The tray starts closed, closes with its icon or Escape, and never opens automatically during playback. Numbered stages fit the desktop width; narrower screens stack stages vertically without sideways scrolling. Four recent nodes per stage are initially shown; earlier nodes expand on request, and a selected node's immediate supporting/consequence nodes are revealed automatically. Directional arrows highlight recorded links, with unrelated nodes subdued.

Cards describe expectations as questions and associations in plain language; exact expectation keys and association objects remain under Record details. Character mode omits the selected person’s repeated ownership header and explicit label prefix, keeping other people’s names and observer attribution intact. Unrecognized expectation keys use a neutral label rather than guessing their meaning.

Playback advances between recorded moments or scrubs by minute within the excerpt. A thread selector limits the display to its stage members and recursively referenced supporting records. Character thread summaries are built from visible personal elements, never observer thread titles. Shared navigation carries the recording, time and person. A local import has no reusable server URL.

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

Seventeen focused adapter tests cover missing/unknown formats, choice and thought gates, other minds and secret diagnostic text, unknowns and guesses, busy targets, acquisition gates, outcome settlement, attempt resolution, expectation rewind, thread summaries, dependency visibility, both comparison outcomes, formula independence, immutability, readable expectation/association labels, unknown-key fallback and personal versus observer attribution. Shared navigation adds the full-recording boundary and chain round trip. Browser review covers playback/rewind, both lenses, linked details, comparisons, and responsive themes.

## Next

Adopt the agreed revised contract and recorded pilot output, pin immutable archetype provenance, then connect the same inspector/comparison components to fixed-scene Studio trials and dashboard selection. Compact/lazy recording storage is not implemented; this preview indexes the loaded file once and filters one thread for rendering. Population-scale performance still needs measured real recordings.
