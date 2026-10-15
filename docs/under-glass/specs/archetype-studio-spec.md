# Archetype studio

## Status

Built 2026-10-14: `sim/viewer/archetypes.html` browses and edits schema-1 character inspiration packs using the shared observer UI. Fixed-scene trials and model execution remain planned. The separate Life chains viewer now previews proposed Lives 0.1 history/action graphs and isolated history comparisons with an illustrative fixture; no runtime Lives system is implied. This editor does not implement the lives assessment or change simulation behavior.

## Scope

The reusable fields are drives, coping, voice, evaluator cue and source references. IDs remain stable; a new or duplicated archetype gets a unique lowercase hyphenated ID. Duplication copies only reusable fields and source references. It assigns no characters, copies no personal history, and creates no speech lines. An empty new archetype needs its required fields completed before export.

The **Characters** view edits assignments, personal accents and background prompt lines in the same pack. Each line has a 120-character budget, optional relevant-person tags and source references disclosed on demand. Add/remove operates on a browser draft; new lines are explicitly labelled authored test content, not sourced canon. Character identities stay fixed. An Edit shared archetype link returns to the reusable profile. Background lines remain private model context, not simulated memories or evidence. Source references are not automatically verified against game assets. Authored speech examples remain separate from these prompts.

## Drafts and export

Edits save locally in the browser, including incomplete drafts. An exact JSON snapshot of the loaded source guards restoration: a changed source prompts recovery/download or an explicit discard before editing. Browser storage failure leaves the editor usable and warns that a download is needed. Import validates before replacing the current draft; imports and discards request confirmation when needed. Downloading does not clear the draft or write to the repository.

Export is the complete `character-inspiration.json` pack, preserving unchanged profiles, optional fields and source references. The editor appends an authored-revision note on the first edit. Pack provenance is editable. Version-2 browser drafts support personal edits; old version-1 drafts still restore under their previous limits. Changed sources still require recovery or discard. A saved draft is never a simulation result. Upload the downloaded pack under Mind in New run to test it; changing it invalidates old exact-answer tapes as before.

## Validation

The editor accepts canonical camelCase schema 1, rejects unknown fields and validates runtime limits: 300 characters per reusable voice field or personal accent, 90 for the evaluator cue, 360 for the composed voice including assigned accents, 120 for a background line, and the existing source/provenance limits. Conditional background remains invalid. Sources require at least one nonempty reference; authored content is identified as authored rather than claiming game evidence.

Twelve model tests cover pack round-trip, biography isolation during duplication, ID constraints, incomplete drafts, combined voice limits, schema/background rejection, change summaries safe draft recovery, personal-context edits, authored background creation, legacy drafts and malformed personal data. Shared navigation tests cover the recording/time/person round trip through the studio. Browser checks confirmed initial rendering, duplication, budget enforcement, export and draft reload. The exported pack passed the C# reader in a one-day authored replay. Follow-up browser checks confirmed inline discard cancellation and confirmation, draft cleanup after reload, character search, personal-context disclosures, light and dark themes, and a 390px layout without horizontal overflow. The speech editor panel meets the main content edge without overlap or a shadow; Remove buttons use a soft background without shadows.
