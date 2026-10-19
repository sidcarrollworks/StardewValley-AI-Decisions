# Model coverage in Run Viewer

## Status

Built, 2026-10-19. Open a recording in the site's Run Viewer and select **Model coverage**.
This is a read-only browser companion to PR #108's command-line coverage report. It requires
no command, Python endpoint, model service or new recording fields. It does not alter choices
or implement the separate review fixes. Standalone exported HTML keeps its existing offline
behavior; this optional tab loads when the viewer is served by the site.

## Architecture and decisions

`coverage-model.js` indexes the recording without mutating it; `coverage.js` renders through
the current Run Viewer clock and character callbacks. `coverage.css` uses the viewer's shared
theme tokens. The main viewer adds only a tab, a render hook and optional asset loading.
The shared navigation recognizes the coverage tab when carrying context between tools.

The default scope is the selected character through the playhead. **Whole recording (spoilers)**
explicitly includes future receipts. Live snapshots remain capped at their received `live.at`.
Evening answers become visible at opportunity time + 1, when the engine can apply them;
reflections require their `considered` event. Rewinding removes counts and details together.
This is an observer diagnostic, not the character-knowledge lens in Life chains.

Evening coverage deduplicates `choices[].evaluation` and `decisions[]` by opportunity identity.
Conflicting duplicates and mismatched fallbacks produce an error, not a best-guess percentage.
Unmatched evaluations are disclosed separately and never inflate the denominator. Missing
blocks/lists are not recorded; present empty lists are zero. Missing call flags and omission
lists remain unknown. No recorded evaluation does not establish why the evaluator was skipped.

Cards distinguish backend `laya`, authored/fallback, no evaluation and other/unknown. They show
the actual evaluated selection when present, not the fallback selection. The expanded receipt
keeps all three model strings separate (configured, answer, reported), the fallback selection,
request size, omitted recall IDs, HTTP flag and note. Names do not verify a model checkpoint;
a taped HTTP flag does not prove a fresh request. A selection does not prove an executed act.

Routing, company and reflection counts are separate consumers, never part of the evening
coverage denominator. Reflection pass responses/errors and generator responses are distinct;
passes are deduplicated and do not imply HTTP counts. Reflection backend labels are available
in the expanded section (last 20 considered receipts). Ordinary acts lack coverage receipts.
No overall town coverage, latency or quality score is inferred.

The list is paginated in groups of 20. Choice details start closed; **Go to this choice** pauses,
selects the character, seeks to its application minute and returns to playhead scope. Indexed
data is reused until the loaded recording changes, including replacement live snapshots.

## Validation

`node --test sim/tools/test-viewer-coverage.cjs` exercises duplicate receipts, actual selections,
fallbacks, unknown provenance, unmatched evaluations, character/time gates, rewind, live
snapshot replacement, reflection timing, separate consumers and navigation integration.
Its independent CI workflow avoids edits to the review-fix workflow.

Broader architecture/roadmap cleanup remains with the parallel review fixes. This feature's
implementation contract is kept here so it can merge independently.
