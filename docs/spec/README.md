# Full specification

This folder specifies every feature of the mod: what exists, what is planned, and exactly how the
planned parts should work. It is written for whoever builds the next piece, including cheaper
models, so each file can be read on its own.

- **How the code works today:** `docs/architecture.md`. **Why:** `docs/decisions.md` (D1..D28).
  **Rules:** `AGENTS.md`. This spec does not repeat them; it links to them and adds what is not
  built yet. When the spec and the code disagree about something marked **done**, the code wins:
  fix the spec.
- Written against `main` at `91d41f2`: step 9 (Find) plus DeepSeek's console heartbeat and
  plan-collection logging (PR #3), which changed only logging.
- **verify** marks a game or SMAPI fact recalled rather than confirmed in decompiled source, docs or
  in-game. Treat it as a hypothesis: confirm it before relying on it, and keep a `VERIFY` comment in
  code until you have.

## Verifying game facts

On 2026-09-30 the spec's game and SMAPI facts were checked against a local decompile of 1.6.15 and
SMAPI 4.5.2; what it settled is in `stardew-source-notes.md`, "Checked in the 1.6.15 decompile". The
few **verify** marks left are about Laya and other mods, which the decompile can't answer, and some
facts only the running game can show are listed as in-game checks. Ways to settle something new,
best first:

1. **Decompile the game Sid actually runs** (1.6.15 with SMAPI 4.5.2). The modding wiki's Get
   Started guide (stardewvalleywiki.com/Modding:Modder_Guide/Get_Started, read 2026-09-30)
   recommends ILSpy, with "Always qualify member references" on, or its command-line version:
   ```bash
   dotnet tool install --global ilspycmd --version 8.2.0.7535
   ilspycmd -p --nested-directories -r "<game path>" -o "<output folder>" "<game path>/Stardew Valley.dll"
   ```
   The version is pinned because the installed .NET SDK (6.0.300) can't install newer `ilspycmd`
   packages. Put the output **outside this repo**: decompiled game code must never be committed.
   **On Sid's PC this is done:** `ilspycmd` is installed (Sid agreed on 2026-09-30) and the decompile
   is in `%USERPROFILE%\stardew-decompiled\`, with `game\` (`Stardew Valley.dll`, 1.6.15.24356),
   `gamedata\` (`StardewValley.GameData.dll`: `Data/*` models such as `CharacterData`, `ShopData`)
   and `smapi\` (`StardewModdingAPI.dll`, 4.5.2). Search it with grep. Redo it after a game update.
2. The online decompiles listed in `stardew-source-notes.md` (1.5.6 and an unspecified 1.6 build).
3. The modding pages on the wiki (SMAPI APIs, data formats).
4. In-game, with a build that logs what you need, for behavior rather than signatures.

When a fact is settled, remove "verify" from the spec and the `VERIFY` comment from the code, and
add the fact with its source to `stardew-source-notes.md`.

## Conventions from the modding guide

From the Get Started guide, and already true here unless noted:
- Target `net6.0`: it is what the game ships, and players may not have anything newer.
- Build file paths with `Path.Combine(Helper.DirectoryPath, ...)`; never hard-code `\` or look up
  the assembly's location. Asset names (`Data/mail`) always use `/`; SMAPI normalizes the ones passed
  to its APIs (`PathUtilities.NormalizeAssetName` for comparisons).
- `Pathoschild.Stardew.ModBuildConfig` deploys a build into the game by default. This repo turns
  that off on purpose (`EnableModDeploy=false`, D19).
- Player-facing text goes in `i18n/` ([text.md](text.md)).

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
| 6 | [find.md](find.md) | Finding the player; NPCs coming to find the player (visits, 1-2 a week) | partial (asking done; visits not started) |
| 7 | [newcomer-week.md](newcomer-week.md) | Newcomer week: visits, letters, gifts | not started |
| 8 | [day-length.md](day-length.md) | Longer days | not started |
| 9 | [laya.md](laya.md) | The Laya decision layer: questions, state format, 512-token budget, sidecar lifecycle | partial |
| 10 | [text.md](text.md) | Text generation (templates) and sanitizing | partial |
| 11 | [rollout.md](rollout.md) | Shadow-to-live rollout, switches, safety invariants | not started |
| 12 | [persistence.md](persistence.md) | Save data, versions, migrations, size | partial |
| 13 | [config.md](config.md) | Every setting, current and planned | partial |
| 14 | [multiplayer-compat.md](multiplayer-compat.md) | Multiplayer (and what it would take), other mods, custom NPCs | not started |
| 15 | [invitations.md](invitations.md) | Letters in the NPC's voice, invitations to meet, later requests and quests | not started |
| 16 | [motives.md](motives.md) | Why a character acts and whether it dares: motives, boldness and act costs, elastic stresses and saved regard, mood, hurt, grudges and friendship loss, weather and seasons | not started |
| 17 | [trades.md](trades.md) | Villager-style barter offers through vanilla barter shops | not started |
| 18 | [debug-tools.md](debug-tools.md) | A live NPC Minds viewer in the browser, an "NPC Minds" tab in the game menu, console commands, a playtest digest | viewer built; tab and commands not started |
| 19 | [romance.md](romance.md) | Dating, engagement, marriage, divorce: partners, jealousy, milestones, spouses at home | not started |
| 20 | [town-life.md](town-life.md) | NPCs chatting, meeting up and looking for each other, rendered only when the player is there | not started |
| 21 | [temperament.md](temperament.md) | Seed personality values per villager (warmth, sensitivity, forgiveness, chattiness, curiosity, boldness) and Ekman emotion biases (anger, disgust, fear, happiness, sadness, surprise) from their dialogue and game traits | partial (tool and draft table; not wired) |
| 22 | [vanilla-sources.md](vanilla-sources.md) | What the game already gives: signals to read (dialogue answers, heart events, festivals, movies, conversation topics), character data to mine, and vanilla channels to act through (emotes, one-day schedules, quests, phone calls) | not started (verification running) |
| 23 | [notice-board.md](notice-board.md) | A cork board in the town square: villagers walk there to pin notes, the player pins typed notes, and Laya chooses how each reader reacts | not started (roadmap step 21) |
| 24 | [journal.md](journal.md) | A short bedtime journal offered every night (skippable, time stopped); a page can be stolen when the player is knocked out or passes out, and turns up on the notice board | idea, stored for later (Sid, 2026-10-02) |
| - | [roadmap.md](roadmap.md) | Prioritized build order, and the decisions only Sid can make | - |
| - | [references.md](references.md) | Links to keep (SMAPI API pages, wiki data pages, Laya), mapped to the specs that use them | - |

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
   a feature that is live and says so here. Planned exceptions, each behind its own switch: the
   newcomer gift ([newcomer-week.md](newcomer-week.md)), trades from `data/trades.json`
   ([trades.md](trades.md)), and the grudge penalty, the only friendship change
   ([motives.md](motives.md)).
8. Harmony patches are read-only postfixes ([diary.md](diary.md), "Harmony").
