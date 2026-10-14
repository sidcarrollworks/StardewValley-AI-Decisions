# Character voice and initial self-knowledge

## Status

Built as an explicit opt-in reflection context, 2026-10-12. Hybrid is Sid's selected development
direction; authored and authored-plus-Laya remain controls and fallbacks. The pack is loaded with
`--character-inspiration sim/character-inspiration.json`. Without the flag, requests, prompts and
existing simulation behavior retain their previous shape. This is not a complete biography or a
new spoken-dialogue system. Ordinary response lines still come from the reflection catalog.

The [source review](../experiments/character-voices-2026-10-12.md) describes the derived profiles,
source coverage and excluded histories. Full extracted game text stays in ignored local output.
The extraction tools require an installed game; the simulator and parser tests do not.
The [live character-voice trial](../experiments/character-voice-trial-2026-10-12.md) records working
profile integration and unresolved generated voice quality.
The [fixed-intention wording experiment](../experiments/voice-wording-2026-10-12.md) separately
compares profiles alone with actual source dialogue references, starting with Vincent.
Its held-out authored controls are not model input, and the suite has no evaluator or simulation.
The [Claude Haiku 5.5 run](../experiments/voice-wording-haiku-2026-10-12.md) repeats that probe and
one hybrid week with a cloud generator behind the same loopback contract; it is an opt-in
comparison, not a change to the pack, the prompts or any default.

## Content and evidence

`CharacterInspirationPack` schema 1 has provenance, reusable archetypes and named profiles.
An archetype contains drives, coping, voice cadence, a short evaluator cue and source references.
It contains no named donor biography. A profile selects an archetype, adds a personal voice accent
with its own source references, and supplies reviewed background anchors. Each anchor has an ID, compact text, subject relevance
and source references. The starting baseline is established history before the farmer arrives,
as selected by Sid. Conditional histories are rejected by the runtime reader.

All dialogue may help describe a voice, including conditional event/marriage lines. That does not
establish that their circumstances occurred. A later dialogue recollection can support older
history only after manual chronology review. Player marriage, heart-event outcomes, Joja/community
choices, newly built homes and post-arrival recoveries are not initial memories. Unsettled facts
remain review notes rather than runtime anchors. Raw game commands, player answers, ambiguous
speakers and unsupported extraction cases retain their attribution/coverage status.

Generic future people may use `ForArchetype`; it returns no named character's accent or background.
The current Under Glass cast is a prototype rather than a chronological reconstruction of the
Stardew starting map. Presence in this cast does not justify seeding later story events.

## Runtime contract

Configure the pack before running. Validation freezes nested lists, sorts content canonically and
hashes the entire pack, including source references. A request copies only the actor's profile.
Choose one anchor matching the believed subject, otherwise a general anchor, with an independent
seeded draw; if neither exists, attach no anchor. Mistaken identities remain mistaken. Do not read
the subject's private profile, true intentions or future run events to fill gaps.

This self-knowledge is separate from witnessed acts and beliefs. It creates no source act, gossip,
regard prior, feeling or reflection opportunity. Reflection still requires an eligible actual
memory. The anchor is context for interpreting that memory, not something the simulation says
happened during this run. New run experiences continue to distinguish individuals over time.

The generator receives the full compact voice and the selected anchor. Instructions ask for
private self-talk with the actor's cadence and tensions, avoiding trait recitals, generic counseling
phrases and forced catchphrases. Laya receives the same anchor, the shorter disposition cue,
current context, actual known encounter, imagined thought and every complete candidate line.
Source references and hashes are receipts rather than prose instructions to either model.

Limits: composed generator voice 360 characters, evaluator cue 90 and each anchor 120. Laya's
1,250-character semantic budget includes state, instructions, option labels and descriptions.
Profile packets use compact framing; prior ideas and dream markers remain explicit. If required
history, ideas, minimum known context/memory and full lines cannot fit, use the explicit authored
fallback instead of silently dropping an anchor or candidate words. These limits are not a claim
that every maximal combination can fit.

## Recording, inspection and replay

Profile recordings add a pack hash/provenance receipt and copied `request.character` context.
The Inner life detail shows the actual voice and selected initial history with source keys after
the thought is considered. Existing clock gates still hide future thought/outcome content.
History is described as private self-knowledge, never shared evidence.

Exact-answer tapes compare the complete character snapshot and nested source lists. A changed
pack, including an unused edited anchor, changes its hash and invalidates the old tape. Replay
requires the same explicit pack as the original run. Recordings made without profiles remain
viewable and replayable with profiles disabled.

## Validation and next work

Tests cover validation, copied ownership, source-relevant/general selection, absent profiles,
generic archetype reuse, input immutability, no world-state effects from biography alone,
unchanged disabled hashes, exact tapes and mismatches, profile-aware HTTP packets, complete
lines, budget fallback and observer visibility. Offline corpus parsing tests use synthetic text;
the local source audit checks derived source keys against the installed game's export.

`UnderGlass.ReflectionTrial --suite voices` records eight identical hypothetical scenes with
profiles off/on, keeping the generator seed, source, mood and candidates fixed. It requires a
configured generator, Laya and Balanced evaluation. JSON preserves both original requests and
complete model receipts; Markdown and optional `--html` show paired lines. Incomplete routing,
seed, packet or backend receipts produce a nonzero exit code, never a model-quality verdict.

Review identical scenes with profiles off/on and the generator/evaluator held fixed. Judge voice
without names, grounding, cliché repetition, role reversals and whether a line could belong to
someone else. No quota of hostility or kindness establishes success. Source-derived descriptions
are editorial hypotheses, not an objective personality extraction or proof the model uses them.
Thought/action semantic agreement remains a separate known limitation; adding voice context does
not fix it. Larger tagged spoken templates and deeper initial histories remain later work.

`--suite wording` takes a reviewed case pack and the extracted global corpus. Each reference
selects an exact case-sensitive record ID and NPC text node; ambiguous attribution, player/control
nodes, parse warnings, unresolved placeholders and inconsistent sources are rejected before any
model calls. Optional `--dialogue-manifest` checks raw text/asset hashes against the exported
manifest, without claiming an independent audit of parser-derived wording or installed XNB files.
All frames and prompts are frozen and validated before the first call. Both arms share the scene,
intention, life stage, profile, seed and generation settings; only source dialogue differs.
Background anchors and authored alternatives never enter these prompts. Failed candidate text
remains inspectable, with no substitution. Format checks and overlap flags are technical aids;
meaning, private form, age register and voice require human review. The comparison viewer starts
with Vincent and shows sources, authored controls, both arms and exact requests/responses.

## Authoring UI

The [archetype studio](archetype-studio-spec.md) is built as of 2026-10-14. It edits schema-1 reusable profiles with local drafts, validation and export; it reads character accents/background and speech examples without editing them. This does not resolve the pending history evidence corrections or implement histories as simulation state.
