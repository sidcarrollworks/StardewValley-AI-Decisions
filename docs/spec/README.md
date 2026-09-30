# Full specification

This folder specifies every feature of the mod: what exists, what is planned, and exactly how the
planned parts should work. It is written for whoever builds the next piece, including cheaper
models, so each file can be read on its own.

- **How the code works today:** `docs/architecture.md`. **Why:** `docs/decisions.md` (D1..D20).
  **Rules:** `AGENTS.md`. This spec does not repeat them; it links to them and adds what is not
  built yet. When the spec and the code disagree about something marked **done**, the code wins:
  fix the spec.
- Written against `main` at `5031f6f` (after step 9, Find). DeepSeek's PR #3 (console heartbeat,
  plan-collection summary) was open at the time and changes only logging.
- **verify** marks a game or SMAPI fact recalled rather than confirmed in decompiled source, docs or
  in-game. Treat it as a hypothesis: confirm it before relying on it, and keep a `VERIFY` comment in
  code until you have.

## Status legend

| Mark | Meaning |
|---|---|
| **done** | built, tested, running in shadow mode in the mod |
| **partial** | some of it is built; the file says which parts |
| **not started** | specified here only |
| **live** | changes game state in a real save (nothing is live yet) |

## Files

| # | File | Feature | Status |
|---|---|---|---|
| 1 | [diary.md](diary.md) | Diary, and enrichment: conversations, gifts, quests, festivals, being ignored, newsworthiness | partial |
| 2 | [intents.md](intents.md) | Overnight intents, line selection, novelty, delivery | partial (shadow) |
| 3 | [ladder.md](ladder.md) | Initiation ladder, and what each rung does when live | done (shadow) |
| 4 | [ledger-gossip.md](ledger-gossip.md) | Last-seen ledger, gossip, ambient news spreading | partial |
| 5 | [routines.md](routines.md) | Routine learning, family priors, decay, unlock | partial |
| 6 | [find.md](find.md) | Finding the player (and later each other) | done (shadow) |
| 7 | [newcomer-week.md](newcomer-week.md) | Newcomer week: visits, letters, gifts | not started |
| 8 | [day-length.md](day-length.md) | Longer days | not started |
| 9 | [laya.md](laya.md) | The Laya decision layer: questions, state format, 512-token budget, sidecar lifecycle | partial |
| 10 | [text.md](text.md) | Text generation (templates) and sanitizing | partial |
| 11 | [rollout.md](rollout.md) | Shadow-to-live rollout, switches, safety invariants | not started |
| 12 | [persistence.md](persistence.md) | Save data, versions, migrations, size | partial |
| 13 | [config.md](config.md) | Every setting, current and planned | partial |
| 14 | [multiplayer-compat.md](multiplayer-compat.md) | Multiplayer, other mods, custom NPCs | not started |
| - | [roadmap.md](roadmap.md) | Prioritized build order, and the decisions only Sid can make | - |

## How every feature section is laid out

Each file uses the same headings, so you can jump straight to the one you need:

1. **Player-visible behavior**: what someone playing would notice, in shadow and when live.
2. **Data model**: records, fields, and where they are saved.
3. **Triggers and game hooks**: which SMAPI events or game methods feed it, and on which thread.
4. **Laya questions**: every model question, with its fallback. "None" is a valid answer.
5. **Deterministic rules**: everything decided by code, not the model.
6. **Tuning constants**: names, defaults, and whether they are saved (normally not; see
   `AGENTS.md`, "Tuning is not saved").
7. **Acceptance tests**: unit tests to write, then what to check in-game.
8. **Status**: done, partial or not started, with file references.
9. **Open questions**: what is unknown or needs Sid.

## Invariants every feature must keep

These restate `AGENTS.md` in one place; a feature that breaks one is wrong even if its tests pass.

1. Shadow first. New behavior logs `[shadow] ...` and changes nothing until Sid turns it on
   ([rollout.md](rollout.md)). A live feature has its own config switch, default off.
2. Decisions read memory only (ledger views, diaries, beliefs, `Whereabouts`), never live positions.
   Only `ModEntry.CollectPresences` and `MemoryStore.Observe` read positions. New perception hooks
   (gifts, quests, festivals) write diary entries; they do not feed a decision directly.
3. Co-located means the same location and within 8 tiles.
4. Deterministic: seeded `Random` or `Fnv1a`, NPCs in name order.
5. No model calls on the game thread; all through `ResilientDecisionClient` with a fallback.
6. The model never writes text. Every string a player can see comes from a template and passes the
   sanitizer for its channel ([text.md](text.md)).
7. The mod never grants items, changes friendship points or edits game dialogue data except through
   a feature that is live and says so here (the only planned item grant is the newcomer gift).
