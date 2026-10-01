# Design decisions: what we chose and why

This is the "why" behind the code. `docs/architecture.md` explains how things work; the brief
(`stardew-npc-project-brief.md`) holds the goals. Read the decision before you change the code it
covers. If you change one, update this file in the same commit and say what replaced it.

Each entry says what was decided, why, and what breaks if you undo it. "Audit" refers to an
independent review of steps 1-5 (Sep 2026) whose findings drove many of these.

---

## Ground rules

### D1. Shadow mode first
**Decision.** Every behaviour ships first as a `[shadow]` line in the SMAPI log that says what an
NPC *would* do. Nothing changes game state (no dialogue pushed, no NPC moved, no mail sent).
**Why.** You can't judge NPC behaviour from unit tests; you have to watch it over real days. A
save is hard to repair once the mod has changed it. Shadow logs are cheap to read and to tune.
**If you undo it.** Any live behaviour needs a config switch that defaults to off, and its own
shadow period first. The csproj keeps `EnableModDeploy=false` for the same reason (D19).

### D2. True positions never feed an NPC decision
**Decision.** Live positions are read in exactly one place: `ModEntry.CollectPresences` builds a
`Presence` list, and `MemoryStore.Observe` turns it into ledger entries, diary lines and routine
counts. Everything that *decides* (overnight intents, the initiation ladder, Find) reads only
memory: ledger views (aged and coarsened), diaries and routine beliefs.
**Why.** NPCs that always know where you are feel like surveillance, not people. Limited, aging
knowledge is what makes "asking a neighbour" and "going to look for you" meaningful.
**Where the line is.** "The NPC can see you right now" is allowed because it is the NPC's own
first-hand ledger view at age 0 (`IsNear` in the ladder). Reading `Game1.player.Tile` in a decision
is not.
**If you undo it.** You break the premise of the mod. Don't.

### D3. Co-located means same location and within 8 tiles
**Decision.** Two characters see each other when they are in the same `GameLocation` and within 8
tiles on both axes (Chebyshev square, `Proximity.WithinRadius`). Same region is never enough.
**Why.** A region like Town includes buildings: someone standing in Town can't see Pierre inside
the SeedShop. The audit found the shadow harness got this wrong. The harness now uses the same
location but no distance, because schedule tiles are destinations, not live positions.
**Tuning.** 8 is a placeholder (`MemoryStore.CoLocationRadius`).

### D4. Memory belongs to the NPCs
**Decision.** Each NPC observes the player and the other NPCs near it. The ledger is keyed by
observer (an NPC), subject (the player, recorded as `"Player"`, or another NPC). The player is
never an observer.
**Why.** The goals are about NPCs knowing things: finding the player, asking neighbours, learning
routines. The first live version recorded what the *player* knew about NPCs, which nothing used.
**Cost.** About 30 NPCs x 30 subjects of ledger entries and beliefs. That's fine.

### D5. Determinism
**Decision.** The same inputs and seed always give the same output. Sampling uses seeded `Random`
or FNV-1a hashes (`NpcSchedules.Fnv1a`), never `string.GetHashCode`, which .NET randomizes per
process. Iteration over NPCs is in name order (ordinal, case-insensitive).
**Why.** Tests and shadow logs must be reproducible, or you can't tell a bug from noise.

---

## Time and memory

### D6. Absolute ticks include the year; a day starts at 6:00
**Decision.** A tick is ten game minutes. A day is ticks 0..119 (6:00 to 2:00).
`GameClock.AbsoluteTick` counts from spring 1, year 1, and keeps counting across years.
`GameClock.DayIndex` is the calendar day. 1:50 AM belongs to the day that started the previous
morning.
**Why.** The first version flattened a single year. On spring 1 of year 2, every year-1 memory
read as age 0, which the audit flagged as the top bug. Saves from before the fix are migrated
(D18).

### D7. Ledger decay: coarser through the day, gone at the next 6:00
**Decision.** Within the day a sighting coarsens by age: NamedSpot under 2h, Location under 8h,
Region under 16h, then EarlierToday. Anything from an earlier calendar day is Gone.
**Why.** The brief says "gone the next day". The first version used 120 ticks, so a 00:20
sighting was still a NamedSpot at 6:00 the next morning and "earlier today" on the following
evening.
**Also.** The thresholds (`SpotTtl`, `LocationTtl`, `RegionTtl`) are not saved with the ledger.
When they were, retuning them had no effect on existing saves.

### D8. NamedSpot means an actual spot
**Decision.** A sighting records the subject's tile as its `Spot` ("x,y"). Only a view with a
spot can be a NamedSpot; without one the finest detail is Location.
**Why.** Before, NamedSpot and Location both returned the location name, so the finest stage
carried no extra information.

### D9. Gossip only ever loses detail and never overwrites fresher knowledge
**Decision.** `Ledger.Gossip(speaker, listener, subject)`:
- The listener gets the speaker's current view at one more hop, capped at the speaker's detail. A
  told fact can never be recalled more precisely than the teller had it.
- A tip can travel at most two hops.
- A tip is kept only if it is fresher than what the listener has: a later sighting wins, and for
  the same sighting fewer hops win.
- Gossip to yourself, and passing on a Gone view, are refused.
- The spot is handed over only while the cap is still NamedSpot.
- The tip remembers who told it (`ToldBy`).

**Why.** The audit reproduced older hearsay overwriting a fresher first-hand sighting, and
self-gossip demoting first-hand knowledge to hearsay. `ToldBy` is what lets the log say "Shane
saw you there".

### D10. Diaries: one line per co-located span, capped
**Decision.** An NPC's diary gets one "Saw" line when a span of being together starts, not one
per tick. A gap in ticks, or the night, starts a new span. Each diary keeps its newest 500
entries.
**Why.** Per-tick logging meant an evening in the Saloon wrote about 400 entries into the save.
The planner's "newest 5" options were then five copies of the same line.
**Known gap.** The only kinds written today are `Saw`, plus the ladder's `TriedToReach` and
`IgnoredBy`. Conversations, gifts, quests and festivals are not recorded yet (see Open work).

### D11. Routine beliefs are "where I usually see you", and hearts speed learning
**Decision.** Each (observer, subject) pair keeps counts by region and 2-hour block, learned from
time spent together. Each tick with the player adds `1 + 0.25 x hearts`; each tick with an NPC
adds 1. `BestGuessAt(block)` gives the top region for an hour, with its share and the evidence.
**Why.** The brief says hearts set how fast an NPC learns the player's routine. The observer
only learns from time together, so the guess is biased toward places that NPC goes itself.
Pierre mostly sees you in his shop. That bias is deliberate: it is what that NPC would know.

---

## Decisions (the model) and threading

### D12. Typed decisions only; the model never writes text
**Decision.** `IDecisionClient` has three question types: choice (probabilities over options the
game supports), score (a number on a scale) and yes/no (a probability). Lines the player could
read are templated (`LineRenderer`), use display names (`PlaceNames`), and are sanitized.
**Why.** Dialogue strings can run commands and grant items. One text model voicing every NPC
blurs them together. Typed answers are fast, cheap and testable.
**Sanitizer.** It strips `#`, `$`, `%`, `{`, `[`. If LLM-written text is ever added, also consider
`@`, `^`, `*` and `|`, which Stardew dialogue treats specially (verify).

### D13. Laya only, run locally; Jev dropped
**Decision.** The real backend is Laya (Convai Innovations, Apache-2.0, open weights). It runs as a
separate Python 3.10+ process (`laya-serve`), and `LayaDecisionClient` calls it over localhost.
The fake backend (`FakeDecisionClient`) is the default until Sid switches `config.json`. Jev was
dropped (settled Sep 2026).
**Why.** Laya is open source and local, so it can be modified if needed, and players carry no API
cost. It needs Python, so it can't live inside the game process.
**Sources.** Only the Laya GitHub repo and the Hugging Face model card are trusted. The community
Hugging Face blog post links mostly to a domain that isn't TypeSafe's.
**Open.** The default checkpoint is `typed-decisions`. Whether `english` answers NPC questions
better is untested; A/B them once the server is running.

### D14. Model calls never run on the game thread
**Decision.**
- Overnight planning runs on a background task (`IntentPlanJob`) with a total budget.
- The ladder runs on one serial background worker (`BackgroundLadder`) with a backlog cap.
- Every call goes through `ResilientDecisionClient`: a per-call timeout, the budget token, and a
  deterministic fallback (uniform choice, mid-scale score, 0.5 yes/no).
- The game thread only polls, and saving never waits: the ladder's state is saved from its last
  finished snapshot.

**Why.** The first version called the model synchronously inside `Saving` with no timeout. A hung
sidecar would freeze the save forever. With about 30 NPCs and real latency, a healthy model could
still stall a save for several seconds.
**Ownership rule.** `MemoryStore` belongs to the game thread. The ladder belongs to its worker.
Hand data between them as copies or immutable records (`LedgerView`, `Whereabouts`). Never touch
`Game1` from a background thread.

---

## Behaviours

### D15. Overnight intents: plan at day end, cite only yesterday
**Decision.**
- **When.** At `DayEnding` the mod snapshots diaries and starts planning in the background. The
  plan is collected without blocking, usually at the 6:00 tick, which fires before the save.
- **What gets cited.** Only entries from the day that just ended. "Yesterday" is said only when it
  is true, and the ladder's `TriedToReach` notes are never cited.
- **How many.** At most 3 NPCs speak each day.
- **Not saved.** The plan survives sleeping but not quitting.

**Why.** The audit found every NPC would "remember" a single spring meeting every night forever,
and that lines used internal map names ("SeedShop").
**Known weakness.** With the fake backend every NPC ties, so the speakers are the first three
names alphabetically and the topic is random. Most diary lines are housemates seeing each other
at home. This was seen on Sid's first in-game day. The newsworthiness filter (PR #5) scores
housemates at home zero, and the news-ranking redesign (D21) ranks by news and blends news into
the pick, so with any backend the speakers are the NPCs with the best news and the topic is
news-proportional (with the model able to steer or veto). Richer diary kinds remain (Open work).

### D16. The initiation ladder: mildest step that fits, escalate only after being ignored
**Decision.**
- **Urge.** Each NPC has an urge in 0..1. It grows each tick for NPCs that know the player, faster
  with hearts, gets +0.25 once on a day the NPC has a planned line, and halves overnight.
- **Steps.** In order, with the urge each needs: emote 0.30, bubble 0.45, approach 0.60, queued
  line 0.70, mail 0.80, forced dialogue 0.95.
- **Choosing.** The NPC picks the mildest step at or above its current rung that is available and
  that its urge allows.
- **Being ignored.** The urge drops by 0.2, the rung moves past the ignored step, and a diary line
  is written — but only once that step is live (`RecordIgnoredBy`); while shadow, the penalty and
  escalation apply with no diary write, since the player never saw the attempt (in-game week
  review). A new day resets the rung.
- **Response windows fit the step.** One hour for anything done in person, ending at day end. A
  queued line waits for the rest of the day and quietly expires with no penalty, because the
  player never heard it. A letter waits until the end of the next day.
- **Caps.** 2 attempts per NPC per day, 6 per day in total, of which at most 2 queued lines and
  1 letter. At most 1 forced dialogue per week.
- **Conversations.** Any conversation relieves urge and starts the cooldown.
- **Order.** Open attempts are settled before the day rollover.

**Why.** Brief design decision 3, plus a review that found letters always counted as ignored an
hour later and used up the daily cap, and that an attempt left open overnight undid the new day's
reset.
**Warning.** Steps are saved as integers. Never insert a step in the middle of `InitiationStep`
or reorder it; add new steps at the end, or version the save.

### D17. A response is any conversation
**Decision.** The mod counts a response when a `DialogueBox` opens whose speaker is that NPC
(`MenuChanged`). This covers talking and the reaction to a gift.
**Why.** `Friendship.TalkedToToday` flips only once a day. It missed every conversation after the
first.

### D18. Finding someone: ask the people around you, then fall back on habits
**Decision.**
- **Who asks.** An NPC asks the NPCs it is with right now (`MemoryStore.AskAround`) when it misses
  the player: urge at least 0.45 and no first-hand sighting in the last hour. It asks at most once
  an hour. With nobody around, it asks as soon as someone arrives.
- **Answers.** They follow the gossip rules (D9). The player is never asked.
- **Where to look.** `MemoryStore.LookFor` answers from, in order: the NPC's own sighting today, a
  tip from today, the habit for this hour (at least 3 evidence and a 50% share), then unknown.
- **Going to look.** The ladder's Approach step becomes available from a distance when the NPC
  has a lead with a place. So an NPC keen enough (0.60) goes looking before it resorts to writing
  a letter (0.80).

**Why.** Sid's point: "I saw you there yesterday" is useless for finding someone, which is why the
ledger forgets it at 6:00. Finding uses today's sightings passed along, then habits. It covers
brief goal 4 ("asking neighbors when someone is missing") and the brief's approach rung. Asking
touches only memory, so it runs on the game thread with no model call.
**Limits.** Only the player can be looked for so far; the API takes any subject. Region names are
where the habit points, so a habit lead is coarse ("the beach").

---

## Persistence and delivery

### D19. Save format, and deploying by hand
**Save format.** All mod data lives under one SMAPI save-data key
(`squid.StardewNpcMod.memory`) as version 2: `{version, memory, ladder}`.
- **Version 1** (steps 4-5, ticks without a year) is migrated on load. Each old tick goes in the
  current year if it isn't later than now, else in the previous year.
- **Any versioned save** is read as the current format.
- **Not saved.** The overnight plan and Find's ask cooldowns.

**Deploy.** `EnableModDeploy=false`. A build never copies itself into the game. Someone copies
the build into the game's `Mods/StardewNpcMod` on purpose, using the command in `AGENTS.md`.
`manifest.json` comes from `mod/StardewNpcMod/`, not the bin folder.
**Why.** A build should never silently change what the player's game runs (D1). The version
field lets future formats migrate instead of wiping memory.

### D20. Data lives in tables, not code
**Decision.** Plain tables hold the data:
- region membership: `data/regions.json`, editable without rebuilding
- player-facing place names: `PlaceNames`
- voice sheets: `VoiceSheets`
- seed temperaments: `fixtures/game/temperament/temperament.json` (generated; hand edits in the
  overrides file beside it), D22

**Why.** These will be tuned often and by non-programmers. Each table has tests that pin known
entries. The fish shop was in the wrong region until an audit caught it (now Beach).

### D21. News decides the line; the model modulates it (in-game week review)
**Decision (PR #10).** The overnight plan ranks speakers by their best news score first, then the
model's yes/no answer, then name; the topic samples from blended pick weights (model probability
x news score, a zero probability a veto, a missing or degenerate answer the pure news weights).
`MinNews` 1.0 -> 2.0, so "nice talking" chit-chat never reaches the model while a player `Saw`
stays the everyday baseline. The speak threshold drops 0.5 -> 0.25: the rewording sweep on the
shipped plain-sentence states measures newsy answers at 0.15-0.51 on every wording, so a 0.5 gate
was vetoing real news (a quest state measured 0.49 on the deployed checkpoint); the gate is now a
hard-veto floor. The model states and pick options use plain sentences (`NewsPhrasing`), the
phrasing the eval set was measured on.
**Why.** The in-game week showed the yes/no band (0.47-0.60) barely moved the outcome, so a
birthday gift, a completed quest and a festival all lost to "I saw you" and "nice talking". The
deterministic news score is the reliable signal; the model keeps the veto, the steering between
close options, and every other question (bubble, emote, approach, pick direction).
**Known weakness.** `MissedFestival` still goes to every 4+ heart villager, including ones who
never attend festivals (tracked in `docs/spec/diary.md`).

### D22. Temperament is counted from the game's own text, not hand-picked or model-judged
**Decision (2026-09-30, draft for Sid's review).** Each villager gets six seed traits (warmth,
sensitivity, forgiveness, chattiness, curiosity, boldness), each tied to a factor a spec already has
(motives, ladder, gossip, newcomer week). They are computed by a counting method over the game's
data: `Data/Characters` Manner/SocialAnxiety/Optimism/Age set small offsets from 0.5, and the
character's dialogue pages (portrait moods, punctuation, a few word lists, gossip, words per page)
move them by z-score against the cast, at most 0.3. Hand edits live in a separate overrides file.
**Why.** Reproducible (same game files, same bytes), explainable in a review (the report shows each
character's signals and what the game traits alone would give), and consistent with "the model
never reads or writes free text" for anything that decides. Hand-picked numbers drift and can't be
redone for a game update or a modded cast; asking the model to judge personality from lines would
be a free-text task it isn't built for. The game traits alone are too coarse (Shane and Sebastian
share all three), which is why the dialogue matters.
**Emotions (Sid, 2026-10-01).** Six Ekman emotion biases (anger, disgust, fear, happiness,
sadness, surprise) sit beside the behaviour traits, computed the same way: the game's `$a`/`$s`/`$h`
portraits plus emotion words; fear, disgust and surprise have no portrait, so they use words only
and move half as far. The behaviour traits decide what a character does; the emotions decide how
it shows (hurt as anger or sadness, the line's tone and portrait).
**Not wired yet.** The mod still uses only the three game traits on the card; wiring comes with
motives (roadmap step 14).

### D23. A live viewer served from the mod, read-only
**Decision.** The mod serves a read-only "NPC Minds" page on `127.0.0.1:8765` (config
`MindsViewer`, `MindsViewerPort`) that Sid keeps open on a second monitor while playing. The game
thread builds an immutable snapshot after each tick from memory, the ladder's last finished state
and the plan; a background thread serves it with the newest model calls. A pass-through
`RecordingDecisionClient` copies every question and answer to a ring log without changing them.
**Why.** Watching changes over a test week is much easier than reading the `[shadow]` log
(Sid, 2026-09-30). A browser page can show every NPC at once, highlight what moved, and show the
model's probabilities, which the in-game tab in `debug-tools.md` can't do without covering the
game. A local HTTP page rather than a file: a browser can't poll a local file. A raw
`TcpListener` rather than `HttpListener`: no URL reservation or admin rights on Windows. Serving
on request rather than writing files keeps disk I/O off the game thread.
**Rules it keeps.** Shadow mode (it only reads; a test pins that building a snapshot leaves
memory unchanged), no model calls or `Game1` access off the game thread, loopback only (and
non-loopback `Host` headers refused), and every failure is caught so the game is never affected.
**Not decided.** Whether the in-game tab and console commands in `debug-tools.md` are still
wanted now that the viewer exists. Turn `MindsViewer` off for any release.

### D24. Social physics: diary-driven motives, resting urge, and the elastic/plastic mood model
**Decision (Sid, 2026-10-01).** The urge clock dies. Urge no longer accrues over the day for no
reason; it is `resting(temperament) + today's mood roll + the sum of motive stresses`, and an NPC
with nothing to feel sits at its resting level. Motives are a **view over the diary** — each diary
kind is a stressor profile (magnitude, decay, plasticity) and strengths are recomputed every tick,
never saved. Stresses sit on a spectrum from elastic (proximity, small talk: springs back fast) to
plastic (grudges, being stood up: accumulates), with a yield point where repeated elastic strains
become plastic — the same physics the ladder's ignore-escalation already implements. Resting urge
comes from the temperament seed (boldness, warmth, chattiness), spread below the first threshold
so personality decides who naturally approaches. Sensitivity is the amplitude (how much events
shake a character, and the bounds of the daily mood roll); boldness is the expression (how much of
that state reaches behavior). Each day rolls a deterministic mood offset — bounded by sensitivity,
skewed by emotion biases, dampened by the mood the player earned, with rare uncharacteristic tail
days. The same engine takes NPC subjects, so **opinions** between NPCs are the same physics over
diary entries about each other, with no new saved state.
**Why.** The spring 16-18 playtest: everyone climbed toward 0.8 urge on an empty day, Linus topped
the table without seeing the player, and ~25 gossip-asks fired in an afternoon. A flat 0.5 resting
for everyone would have recreated that in disguise; the spread below the first threshold makes
personality discriminate. Rejected: the old draft formula `BaseGainPerTick x (0.5 + Σ motives)`,
which kept half the clock. The diary stays the single source of truth (Sid); the response-dialogue
system (step 6+) later adds stressor *producers* (`Praised`/`BrushedOff`/`Criticized`), not new
plumbing.
**Notes.** The simulation core (memory, diary, stressor physics, deterministic rolls) is kept
game-agnostic on purpose: it is meant to port to a standalone town sim without Stardew's schedule
lock.

### D25. Gossip magnitude: news travels as far as it matters, then fades
**Decision (Sid, 2026-10-01).** A hard one-hop cap on event gossip is boring. Each shareable event
carries a **magnitude** equal to its news weight; a `Heard` entry can be retold while its
remaining magnitude is above 1, each retelling passes it on at magnitude - 1, and unretold entries
lose 1 magnitude at each 6:00. Weight-4 news (a quest helped) crosses three listeners; weight-2
news (a saw) dies at the first — today's behavior, but as the tail of a rule instead of a
hardcode. Dedupe per listener stays, so the fan-out never loops.
**Why.** "Gossip can get a magnitude value applied to it which helps determine how many times the
gossip hops or fades" (Sid). It also serves the alive-world goal: the player's deeds keep rippling
back through NPCs they never told, which is the payoff of the off-screen sim
([town-life.md](town-life.md)).
**Notes.** Positions do not get magnitude: a last-seen position stays under the D9 two-hop cap and
a rumour never refreshes anyone's last-seen view.

---

## Open work and known issues

- **Richer diary, part 3.** The remaining kinds wait on their features: the visit kinds (newcomer
  week), the romance kinds, the trade and town-life kinds. Parts 1 and 2 are done (PRs #5 and #6):
  `Talked`, the day-end notes, gifts (`GiftReceived`/`SawGift` via read-only Harmony postfixes),
  `QuestHelped`, `Festival`/`MissedFestival`, and the newsworthiness filter that skips housemates
  seen at home and ranks by news. (D10, D15)
- **Laya in practice.** Install and run `laya-serve` (`sidecar/README.md`), switch `config.json`,
  and compare the `typed-decisions` and `english` checkpoints. (D13) The eval set ran 2026-09-30
  (`sidecar/eval/RESULTS.md`): `typed-decisions` won 5/6 directional cases vs 3/6; the remaining
  work is the in-game week and the speak-question rewording experiments.
- **Fake backend.** The fake ties everything, so the shadow log is only informative about
  mechanics, not choices. ~~A seeded non-uniform fake would help.~~ Done (PR #7):
  `VariedFakeDecisionClient` (`DecisionBackend: "Varied"`), deterministic FNV-1a answers.
  (Audit finding 6)
- **Novelty.** The mod passes an empty `RecentLines`, so the planner's "already said" check never
  fires. Wire it once lines are delivered for real. The brief also asks for cooldowns and for
  rejecting lines too close to vanilla dialogue.
- **Laya and the budget.** The planning budget stops *new* model calls, but the mod doesn't pass it
  into `LayaDecisionClient`. An in-flight HTTP call is abandoned and ends at its own timeout
  (`DecisionTimeoutMs`) instead of being cancelled. It's harmless but wasteful.
- **Find for NPCs.** Let NPCs look for each other, and say where they are going
  (`showTextAboveHead`) once behaviour goes live. (D18)
- **Routine beliefs.** `RoutineBelief.Decay` is never called, and `Observe` ignores its tick.
  `UnlockThreshold` is still saved with each belief, the same problem the ledger thresholds had
  (D7). Family and friend priors (`SeedPrior`) are not wired into the live mod.
- **SDK.** The installed .NET SDK 6.0.300 is too old for SMAPI's analyzers, so warning CS8032 is
  expected and the NetField checks are off. Installing a newer SDK while still targeting net6.0
  turns them back on.
- **Verify in-game.** Items still marked `VERIFY` in the code:
  - whether off-screen NPC positions update in real time;
  - `Game1.locations` coverage of building interiors;
  - that `DialogueBox.characterDialogue.speaker` is the NPC for gift reactions.
