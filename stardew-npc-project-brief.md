# Stardew NPC Mod: Project Brief

## Paste as Project instructions

This project is a SMAPI (C#) mod for Stardew Valley 1.6 that makes NPCs feel less scripted. NPCs remember what happened, start conversations, and look for each other and the player using limited, aging knowledge. Typed decisions come from Jev (TypeSafe, hosted) or Laya (Convai, open-weight, local). Neither model generates text.

Working rules:
- Separate what is verified in source or docs from what is recalled. Mark recalled items "verify".
- The game's true positions for the player or any NPC must never be inputs to an NPC decision. Only ledger entries, aged and coarsened, may be.
- Decay and coarsening are done by deterministic code, not by a model.
- Any generated text must be sanitized before it reaches a dialogue string (see Constraints).
- Prefer shadow mode (log what the mod would do, change nothing) before any behavior goes live.
- Read `stardew-source-notes.md` before answering questions about game internals.

## Goal

NPCs that:
1. Carry a private diary of what they witnessed and did.
2. Say something new the day after something happens (an "intent" stored at sleep, delivered next morning).
3. Start contact with the player instead of waiting to be clicked.
4. Track each other and the player through a "last seen" ledger, asking neighbors when someone is missing.
5. Learn each other's routines from co-presence, with family and friends starting with a rough prior.

## Decision layer

- **Jev** (typeSafe.ai): hosted, early access. State plus typed questions in, probabilities out, 70 to 500 ms, choice cardinality up to 255. Claims are self-reported by the vendor.
- **Laya** (Convai Innovations): open weights, local. Three question types: `choice`, `score` (ordered), `noul` (yes/no probability). Default context 512 tokens. Needs Python 3.10+, so run it as a separate local process and call it over localhost.
- Prototype with Jev. Ship with Laya so players carry no API cost.
- Text: a short sentence from an LLM overnight, or templates. Jev and Laya only decide what, who, and how.
- Caution: the Hugging Face Laya article is a community post that links mostly to thejevai.com, which is not TypeSafe's domain. Rely on the Laya GitHub repo and model card.

## Design decisions so far

1. **Diary.** The game keeps only counters and flags. The mod keeps a per-NPC event log and saves it through SMAPI's save-data API.
2. **Overnight intents.** At sleep, decide which NPCs have something to say and about what. On day start, push the line with `addExtraDialogues` or `setNewDialogue(text, add: true, clearOnMovement: true)`. Each line must cite a diary entry. Cap at 1 to 3 NPCs a day. Add a novelty bonus and cooldowns. Reject lines too similar to vanilla dialogue or the NPC's last ~20 lines.
3. **Initiation ladder** (mildest fitting step): emote, bubble (`showTextAboveHead`), approach (`moveTowardPlayer` or `PathToOnFarm`), queued line, mail, forced dialogue box (rare). Per-NPC urge value. Daily caps. Ignored attempts lower urge and enter the diary.
4. **Last-seen ledger.** One entry per subject per observer, including the player. Recorded on the ten-minute tick when co-located. Detail degrades with age: named spot, then location, then region, then "earlier today", then gone the next day. Gossip passes on the already-coarsened view and never adds detail. Two-hop cap on asking. Simulate silently off-screen. Render text only when the player is in the location.
5. **Routines.** Counts by time block and region, learned from co-presence. Family and friends start with a coarsened, time-jittered prior built from real schedules. Pairs unlock after enough co-presence, replacing a global silent week. Hearts set how fast an NPC learns the player's routine.
6. **Newcomer week.** News of the player spreads through the ledger at hearsay level. Some NPCs visit to introduce themselves with a small gift. Visitors vary per save (seeded). A letter announces the visit. Shy NPCs send a note. Gift items are granted in code, never by generated text.
7. **Day length.** Target 24 real minutes: `Game1.realMilliSecondsPerGameMinute = 1200` and `realMilliSecondsPerGameTenMinutes = 12000`. Test 18 to 20 minutes first.

## Constraints and risks

- Dialogue strings can run trigger actions and grant items. Strip `#`, `$`, `%`, `{`, `[` from generated text.
- Cross-location travel uses a precomputed route table plus queued schedule paths. `ignoreScheduleToday` makes `checkSchedule` return immediately, and `PathToOnFarm` sets it. Any custom travel must restore the schedule afterward.
- The friendship record is created the first time the player clicks an NPC. A visit with no click may not register for the introductions quest. Test early.
- Homogenization: one model writing every NPC tends to blur voices. Give each NPC a short voice sheet.

## Open questions

- How shop opening hours are enforced under a longer day (not yet read).
- The code that moves an NPC once `isWalkingTowardPlayer` is set (not found in NPC.cs).
- Where the per-item gift log lives in the Farmer class.
- Whether off-screen NPCs' `currentLocation` updates in real time (the ledger depends on it).
- `Ledger.Gossip` currently overwrites a listener's fresher first-hand memory with older hearsay; decide whether a fresher first-hand sighting should win (and how hop count interacts).

## Next steps

1. ~~Schedule extractor: raw schedules to region-by-time-block counts.~~ **Done** (see README): simulates a full year per NPC mirroring the verified 1.6 key order and command semantics, outputs region x block counts + routine priors; 61 tests; real 1.6 fixtures unpacked from the game.
2. ~~Ledger record and the age-based view function (C# stub).~~ **Done** (see `src/NpcMemory`): `Diary` (event log), `Ledger` (last-seen with age decay + two-hop gossip), `RoutineBelief` (co-presence routine learning + prior seeding + pair unlock); 79 tests. Built by three parallel subagents on deepseek-flash against a parent-authored contract.
3. Shadow-mode logging harness.
4. SMAPI skeleton with `Saving` / `DayStarted` hooks and the sidecar client.

## Side ideas

- **Sims 4:** script mods are Python and hook autonomy. Wrap the autonomy pick: take the top 8 to 12 legal candidates, ask Jev or Laya to choose, and fall back on timeout. Run the model in a sidecar process, off the sim thread.
- **Civ 6:** weak fit. Its AI problem is competence, not character. Diplomacy judgments are the one place it might help.
