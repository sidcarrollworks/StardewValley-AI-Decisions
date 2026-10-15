# Archetype studio

## Status

Built 2026-10-14: `sim/viewer/archetypes.html` browses and edits schema-1 character inspiration packs using the shared observer UI. Fixed-scene trials and model execution remain planned. The separate Life chains viewer now previews proposed Lives 0.1 history/action graphs and isolated history comparisons with an illustrative fixture; no runtime Lives system is implied. This editor does not implement the lives assessment or change simulation behavior.

## Scope

The reusable fields are drives, coping, voice, evaluator cue and source references. IDs remain stable; a new or duplicated archetype gets a unique lowercase hyphenated ID. Duplication copies only reusable fields and source references. It assigns no characters, copies no personal history, and creates no speech lines. An empty new archetype needs its required fields completed before export.

Character assignments, accents and background context are read-only. Source keys can be inspected but are not automatically verified against game assets. Existing history evidence corrections belong to their separate review. Authored speech examples come from the checkout catalog; they are not model predictions or modified when the archetype changes.

## Drafts and export

Edits save locally in the browser, including incomplete drafts. An exact JSON snapshot of the loaded source guards restoration: a changed source prompts recovery/download or an explicit discard before editing. Browser storage failure leaves the editor usable and warns that a download is needed. Import validates before replacing the current draft; imports and discards request confirmation when needed. Downloading does not clear the draft or write to the repository.

Export is the complete `character-inspiration.json` pack, preserving personal profiles, optional fields and source provenance. The editor appends an authored-revision note on the first archetype edit. Pack provenance is also editable. A saved draft is never a simulation result, and exporting never applies it to an existing run. Configure the downloaded pack explicitly for a new run; changing it invalidates old exact-answer tapes as before.

## Validation

The editor accepts the canonical camelCase schema-1 format, rejects unknown fields and validates the runtime limits: 300 characters for each reusable voice field, 90 for the evaluator cue, 360 for the composed voice including every assigned character's accent, and the existing background/source/provenance limits. Personal data remains immutable in the editing UI. Sources require at least one nonempty reference; an original authored archetype is identified as authored in the studio rather than claiming game evidence.

Eight model tests cover pack round-trip, biography isolation during duplication, ID constraints, incomplete drafts, combined voice limits, schema/background rejection, change summaries and safe draft recovery. Shared navigation tests cover the recording/time/person round trip through the studio. Browser checks confirmed initial rendering, duplication, budget enforcement, export and draft reload. The exported pack passed the C# reader in a one-day authored replay. Follow-up browser checks confirmed inline discard cancellation and confirmation, draft cleanup after reload, character search, personal-context disclosures, light and dark themes, and a 390px layout without horizontal overflow. The speech editor panel meets the main content edge without overlap or a shadow; Remove buttons use a soft background without shadows.
