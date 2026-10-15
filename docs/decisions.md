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
**Open.** ~~The default checkpoint is `typed-decisions`. Whether `english` answers NPC questions
better is untested; A/B them once the server is running.~~ Settled in two passes: the directional
eval (D13-era, 2026-09-30: typed-decisions 5/6 vs 3/6) and the character-spread eval (2026-10-02:
english follows personality better on the motives' close-call questions, which are not live yet).
D27 reaffirms typed-decisions and sets the revisit point.

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
  tip from today, the habit for this hour (at least 12 evidence and a 50% share), then unknown.
  Amended by the playtest review (PR #14): a sighting or tip must be fresh (`MaxLeadAgeTicks` 24,
  four game hours — 8-hour-old tips were sending NPCs across town; the trade-off against the
  ledger's location coarsening at 48 ticks is Sid's to revisit) and must not point into the
  seeker's own region as of its last observed tick (span-tracker memory; the same guard the
  habit branch already had). A rejected lead keeps its knowledge but loses its place: what an NPC
  knows never shrinks, it just stops being somewhere to go.
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
**Amended (playtest review, PR #14).** A player `Saw` is dropped when the day's diary also holds a
`Talked` entry: the NPC already talked to the player, so "I saw you at the saloon yesterday" from
the saloon conversation reads oddly (this also means the flat yes/no no longer picks among tied
saws for NPCs who actually spoke to the player; options (b) and (c) from the review remain open).
A one-option pick skips the pick question entirely: the model call is the speak yes/no alone.

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

### D24. Motives and boldness replace the urge; feelings are elastic or plastic
**Decision (Sid, 2026-10-01, second pass the same day).** No urge number decides any more. A
character acts only when it has a **motive** with a subject (no motive, no act) and its **effective
boldness** toward that subject (temperament boldness + familiarity + the motive's intensity)
reaches the act's **cost**. The ladder's steps become acts with costs; hostile acts cost much more,
and negative regard counts as familiarity for them only weakly. Feelings come from the diary as
**stresses**: an **elastic** part, recomputed from the recent diary and fading, and a **plastic**
part saved as signed **regard** per (observer, subject), the player included; the grudge is
negative regard toward the player. How much plastic stress a character keeps depends on the person
(`retention`; Pam forgets most slights, Robin doesn't), except severe ones. Feelings toward one
subject **net**, swayed by the day's mood. **Mood** is mostly earned from recent events, plus a
small deterministic daily roll; it tips close calls: clear calls skip the model, close calls ask
Laya and the mood shifts the answer before a cut at 0.5. Opinions between NPCs are the same
machinery with NPC subjects.
**Why.** The spring 16-18 playtest: urge grew on a clock, so everyone climbed toward 0.8 on an empty
day and Linus topped the table without seeing the player. The morning draft of this decision
(urge = a resting level from temperament + mood roll + stresses) was rejected: on the draft
temperament table the resting level put all 34 villagers between 0.35 and 0.64, past the first
threshold, which recreated the clock, and it counted boldness twice. Regard is saved, not
recomputed, because diaries are trimmed to 500 entries and a grudge must not vanish with its
causes. Sid: *"For a character to act they need a motive and a certain level of boldness. The
boldness required to do the act also depends on the relationship with the subject."*
**Notes.** All constants are first guesses; the playtest log (`docs/spec/debug-tools.md`) records
each decision's parts so they can be tuned from data. The simulation core (memory, diary, stresses,
regard, deterministic rolls) stays game-agnostic so it can port to a standalone town sim. Spec:
`docs/spec/motives.md`.

### D25. Gossip has juiciness; hearsay is elastic until confirmed
**Decision (Sid, 2026-10-01).** Replaces the fixed one-hop rule for event gossip. Each diary kind has
a base **juiciness**; disgust and surprise make a story juicy ("I saw X here" is 1, rummaging in
the trash is 4). A story is volunteered in a chat only at juiciness 2 or more for that listener
(a bonus when the listener knows someone in it); below that it is told only when asked
(`AskAround`). Each retelling passes it on at 0.7 of its juiciness, and juiciness fades fast (half
a day; slower for scandals). A teller tells one story to at most three listeners a day. The
listener's reaction scales with relevance: a liked gift is casual news, except to someone who loves
the giver. Hearsay moves only the listener's elastic feelings; it becomes plastic (regard) when
confirmed first-hand, by someone who was in the event, or (by half) by two independent tellers.
**Why.** *"Gossip exists on a scale... Unless gossip is crazy it really should fade fast"* and
*"hearsay would be an example of the elastic. If verified from the source it turns into plastic"*
(Sid). A morning draft gave every story a magnitude equal to its news weight, which limited depth
but not breadth: one Saloon evening could carry a story to most of the town.
**Notes.** Positions keep the D9 two-hop cap, and a rumour never refreshes a last-seen view. Spec:
`docs/spec/ledger-gossip.md`.

### D26. Lean on the vanilla game: its data to stay in character, its mechanics to act
**Decision (Sid, 2026-10-01).** Get as much information from the vanilla game as possible, because
it is the best data for keeping characters in character, and act through the game's own mechanisms
before building new ones. Concretely: read more player-action signals (answers to vanilla dialogue
questions, heart events seen, festivals, movies, garbage cans, conversation topics) as diary
entries; mine the game's content offline for seeds (how villagers talk about each other, who appears
together in heart events, gift tastes); and act through vanilla channels (emotes matched to the
emotion, a schedule for one day, quests, later phone calls). The first use: **farm visits by
appointment**. A letter says "I'd like to come by your farm on Thursday at 2"; that day's schedule
walks the NPC there with the game's pathing; it waits an hour by the door; if the player isn't
there it is a stand-up, remembered and mentioned ("I came to your farm to see you and waited for an
hour, but you weren't there").
**Why.** *"I'd like to get as much info from the vanilla game as we can, as it is the best data to
help influence our characters and keep them in character"* and *"editing the schedule is a good
idea, also plays well with sending mail... provides an opportunity for negative interaction"*
(Sid). A schedule for one day uses movement the game already does every day, so announced visits
and meet-ups don't wait on the travel spike, and an appointment gives the player a fair chance to
show up, which makes a stand-up a real choice.
**Notes.** Reading stays read-only (postfixes, daily diffs); every channel used to act has its own
switch. New keys (a mail id, a quest, a one-day schedule) may be added to game data; vanilla entries
are never changed. Whether to add dialogue keys for conversation topics, and portrait codes in our
lines, is still Sid's call. The facts were verified in the decompile the same day (DeepSeek, PR #18).
One changed the plan: the farm is excluded from NPC routing, so a one-day schedule brings the
visitor to the farm's edge and the mod walks it onto the farm (a small spike of its own). Spec:
`docs/spec/vanilla-sources.md`.

### D27. Keep typed-decisions until motives' close calls are live
**Decision (Sid, 2026-10-02).** The default checkpoint stays `typed-decisions`. English is NOT
switched in, globally or per question, despite the spread eval showing it follows personality on
7 of 9 question types.
**Why.** The questions where english looked better — the close calls, the hostile acts and the
choice variant — belong to the motives system (step 14), which is not built; today Laya only
answers the ladder's attention questions and the planner's news and pick questions. On those live
questions english is not clearly better: the speak question is a 0.25 veto that carries no
personality on either checkpoint, and english's approach answers barely varied at all. The
decision matters when step 14 lands and close calls start reading Laya's answers; by then the
viewer's spread panel will have real playtest data to compare against, per question.

### D28. How the motives runner paces attempts and uses motives up, in shadow
**Decision (Claude, building step 14 part 2, 2026-10-02; for Sid's review).** The spec leaves the
runner's bookkeeping open; these are the choices, each found by running a scratch day of the real
runner and reading its log.
- **Two attempt slots per NPC.** One in-person attempt (emote, bubble, walk-up, interrupt, visit)
  blocks new attempts until it is answered or its 6-tick window passes; one waiting attempt (letter,
  queued line, request) blocks only another of its sort. With one slot, as the ladder has, Pam's
  letter silenced her for two days: she didn't even wave when the player walked up.
- **A question answered "no" is not asked again** until the situation changes (the candidates or
  the act in question) or 6 ticks pass. A close call has no random draw, so asking again with the
  same inputs returns the same answer. A blanket cooldown after every pass was tried first and made
  NPCs deaf: Pam reacted 20 minutes late to the player arriving.
- **Shadow treats an attempt as having happened**, as the ladder does: any face-to-face act is the
  day's greeting; news and thanks count as delivered by an in-person act or a letter at once, and by
  a queued line only when the player comes to talk; an emote shows a feeling and delivers nothing.
  A talk with the player answers open attempts and clears frustration.
- **Worried needs a ledger entry** about the player: without one, a save from before the mod kept
  memory read as "999 days without news" for every villager at 4+ hearts.
- **Hearsay from the person it happened to is confirmed at once**, at half the original's lasting
  mark (`HearsayFactor`); from a witness it stays elastic.
- **Waves and greeting bubbles are never ignored** (Sid, 2026-10-03): "They happen often and it
  would just make the game unfun to have to react to every single one." Light acts open no
  attempt, so they can't be ignored and don't frustrate.
- **Nothing in person while the player is busy** (Sid's live test, 2026-10-03): acts decided during
  a warp's fade or a conversation were held back by the live gate after the runner had counted them,
  and two later read as ignored; the runner now doesn't decide them until the player is free.
- **After today's talk, acts wait for no answer** (Sid's live test, 2026-10-03): the game opens no
  second conversation with a villager that day, so a wave after the talk could never be answered and
  always counted as ignored, which in live also writes `IgnoredBy`.
- **Light acts are paced apart** (Sid, 2026-10-02, after the long playtest showed 3 to 5 acts a
  day, the reserve the top reason for a pass): an emote, or a bubble that only greets, uses none of
  the day's attempts and has its own cap of 2 per NPC; the town cap went from 6 to 12, to come
  down after testing if the town is too busy.
- **The grudge is drawn**, seeded, against the model's answer, once a day at most; the 0.3 rise in
  regard after it applies in shadow too (the mod's memory, not the game's friendship), or the log
  would repeat the same would-be penalty every week.
- **The walk-up close calls use the spread eval's wordings** ("walk over to greet the player",
  "confront the player"), so the spread panel compares live answers with the calibration file.
**Why.** Each keeps the shadow log close to what the live system would do, so tuning from it
carries over, and keeps model calls to the ones whose answer can change.
**Notes.** Built in `src/NpcMotives` (`MotivesRunner`, `RegardKeeper`); `docs/architecture.md`,
"Motives", has the full list; the spec (`docs/spec/motives.md`) was updated to match.

---

### D29. History at install seeds once per save, on top of earned regard
**Decision (Claude, building the seed, 2026-10-02; for Sid's review).** The spec said "seed when
the save has no `regard` value yet". Sid's own test save already has regard from the motives
playtests, so that rule would never seed it, and Haley's gifts from before the mod would stay
invisible. Instead:
- **Once per save, behind its own flag** (save-data key `historySeeded`), whether or not `regard`
  exists.
- **Added onto the regard already there**, not instead of it.
- **What the mod already saw is left out** (`NpcHistory.Except(RegardHistory.SeenInDiary(...))`):
  gifts and heart events in the diary already left their mark when they were written. The diary
  keeps the newest 500 entries per villager, so on a long save a few trimmed ones may count again,
  at half strength.
- **The numbers:** each gift and heart event leaves the mark the live rule would, halved
  (`HistoryFade`, their age is unknown), soft-capped at +0.8 for warmth and -0.5 for a grudge
  (below the 0.75 penalty threshold, so nobody arrives already past it). Dating, engaged and married
  set floors of 0.3, 0.4 and 0.5; divorced a lasting grudge of 0.4. One loved gift is about +0.09; a
  favourite with 31 gifts and 2 heart events about +0.77. Hearts are not used: familiarity already
  counts them.

### D30. Emotes and bubbles go live first, friendly and hostile, from the motives only
**Decision (Sid, 2026-10-02).** "Let's do emotes and bubbles first. We can do both friendly and
hostile." This changes the 2026-09-30 order, where the overnight-planned lines (`IntentLines`)
went first.
- **Two switches**, `Live.Emote` and `Live.Bubble`, off by default; hostile acts (a glare, a sharp
  line) come with them, not behind a later `HostileActs` switch.
- **Only the motives runner's acts go live.** The urge ladder keeps running in shadow until it is
  retired; its attempts never show.
- **A last check with live facts** (`LiveGate`): the one exception to AGENTS.md rule 2. It never
  chooses an act, only holds one back (an event, a menu, the player gone or too far, the villager
  busy or hidden, multiplayer, or more than a tick late). Added 2026-10-04 as a safety net:
  nor within 3 ticks of the last act the same villager showed, whatever the cause.
- **The lines are templates**, not the model's text, and the emote follows the motive; the hostile
  emote is angry from the bold and sad from the shy. First guesses for Sid to tune.
- **Being ignored becomes real:** a shown act the player ignores is written as `IgnoredBy`, which
  the shadow ladder never did (the player never saw those attempts).

**Why.** Emotes and bubbles are the least invasive acts (gone in seconds, nothing to undo), and
they are what makes the town feel alive.

### D31. The urge ladder is retired; the motives decide every attempt
**Decision (Sid, 2026-10-03: "Let's retire the old urge ladder").** The mod stops running
`BackgroundLadder`. It had kept running beside the motives in shadow and made its own model calls
(Jodi was asked "Approach now?" every tick on the farm), while the live acts already came only from
the motives.
- **Kept:** each tick's views and leads (`InitiationInput`, built by `BuildViews`); asking around,
  now triggered when a villager misses the player, worries or has news at
  `MotiveOptions.AskAroundStrength` (0.2; the ladder used urge 0.45); the heartbeat, now
  "strongest motive" and the motives worker's backlog (`MotiveDrive`).
- **Gone from the mod:** the ladder's model calls, its `[shadow]` lines and `ladder` playtest records,
  `TriedToReach` diary entries, the viewer's urge (the "Old ladder" tab and the urge sort), and the
  saved `ladder` value (an old one is left unread). `LadderMaxBacklog` keeps its name in
  `config.json` and now bounds the motives worker.
- **Not yet:** deleting `InitiationLadder`/`BackgroundLadder` and their tests.

### D32. Villagers notice the player between ticks
**Decision (Sid, 2026-10-03: "That sounds like a good fix to me").** Sightings and decisions ran
only at the ten-minute tick (about 7 real seconds), so a player running past a villager between two
ticks was never seen, and an act decided at the tick was often held back because the player had
already left. About once a second the mod now checks the player's location only; a villager who
just came within 8 tiles records the sighting (`MemoryStore.NoteMeetings`) and decides at once,
alone. Positions are read only to record the sighting, as `CollectPresences` does (AGENTS.md rule 2
now names both); the decision reads memory. Nothing else changes per second: no routine learning, no
NPC-to-NPC sightings, no second `Saw` line.

### D33. Gossip juiciness as built
**Decision (2026-10-04, building D25).** Stories spread by juiciness, hop by hop, as D25 says.
Three choices D25 left open, made here:
- **Fading counts whole days** since the teller got the story, not fractions. A story keeps its
  juiciness through the day it happened, so news at the volunteer level (a loved gift, a quest, at
  2) is told that day and fades from the next. Counted in fractions, it would be under 2 within
  minutes and spread only with the knows-someone bonus.
- **The teller never counts** as someone the listener knows: every chat partner has just seen the
  teller, so the bonus would apply to almost every story.
- **Kinds without a feeling** get a gossip value only: a gift seen by taste (liked 1.5, loved or
  disliked 2, hated 3), town news 2, a festival 1. A liked gift and a festival no longer spread on
  their own, as D25 intends ("I saw this person here" is a 1).
A retold story is confirmed by the person it started with (`of`), or first-hand: the listener
sees the same act by the same person within 7 days. A gift seen counts for a gift heard when both
pleased, or both didn't. Relevance applies to the player only for now (8+ hearts doubles a story
about the player, and a pleasing gift to someone else stirs jealousy), since the elastic stresses
don't see regard for NPCs. Two independent tellers are not built: dedupe keeps the second telling
out of the diary.
**Why.** Sid asked to continue in the cloud (2026-10-04); this is the next roadmap item (step 14).

### D34. Only a scandal heard secondhand is a reason to act
**Decision (Sid, 2026-10-05).** "Unless I actually did something really bad, the rest of the town
shouldn't tell me off. Say I dig around in the trash can and that spreads around town, I think that
warrants multiple people telling me to stop. But a bad gift isn't at that same level." In the
day-10 playtest Gus queued a telling-off because Shane told him about a fish Shane disliked.
- A story about what the player did to someone else moves the listener's mood, and its lasting
  mark in regard stays as D33 says, but it gives no `Hurt` or `Grateful` motive toward the player
  (`Stress.MoodOnly`).
- The exception is a scandal: a bad act at base juiciness 4 or more
  (`MotiveOptions.HearsayActsFromJuiciness`). Rummaging in the trash spreads, and everyone who
  hears it may tell the player to stop.
- Jealousy (a villager at 8+ hearts hearing of a pleasing gift to someone else) still acts: it is
  about the listener.
**Why.** Feeling hurt is about something done to you. A grudge built from many small stories still
counts, through regard, as before.

### D35. A full diary forgets what matters least first
**Decision (Sid, 2026-10-05).** "Non important information should leave first. 'I saw x here'
isn't worth holding onto unless they witness something happen. Entries that are about a person a
character has little regard for get thrown out sooner than others." Diaries were trimmed oldest
first, and Emily's filled with sightings of Haley.
- Over the 500 cap a diary forgets a tenth at once, lowest weight first (`DiaryKeep`).
- Weight: the kind (a plain `Saw` 0.1, an event 1; a `Saw` within 6 ticks of an event about the
  same person counts as a witnessing, 1), halved every 21 days, times `0.5 + |regard|` (at most
  1.5). Strong feelings either way are remembered.
- The last two days are never dropped while older entries remain: the motives, gossip and lines
  read them.
**Why.** Regard is applied when an entry is written, so forgetting an entry never changes a
feeling. What's lost is what a villager could still bring up.

## Under Glass (the independent game)

### D36. Observe a person's thought and its consequences before expanding the social catalog

**Decision (Sid, 2026-10-08).** Under Glass's first version is an in-depth social simulation
viewed from above. Player gameplay follows later. The next experiment follows a character from a
remembered encounter through inspiration, consideration, intention, action and outcome. Its
success is whether an observer can understand and wants to follow those choices. Counts of feuds
or friendships remain diagnostic; they do not establish that a relationship is interesting.
An unusually hostile or quiet town may be a valid outcome when its causes hold together. It is
not automatically a balance failure to tune away.

**Model roles.** Routine speech can be authored in voices and tagged for context. A local
generator may occasionally propose an imagined intention, and Laya evaluates the character's
response using their own context and the actual candidate lines. Laya is not limited to breaking
close numerical ties in this experiment. The engine samples once from its weights and supplies
all executable consequences. Accepting a thought, reshaping it, deferring it and rejecting it are
distinct outcomes. A thought does not become evidence merely because a model imagined it.

**First implementation.** Quiet reflection is opt-in, with an authored baseline for offline
comparison, authored inspiration plus Laya, or local generation plus Laya. Only a person's own
completed deed or an identified firsthand belief about them supplies a source; even a mistaken
identity stays as they remember it. The initial choices are an available gift, help or
confrontation, plus defer and reject. An accepted intention waits for an eligible encounter and
can expire. No path creates an unsupported act, teleports the subject or writes a fabricated
witnessed memory. This is a separate .NET 8 adapter and simulator change, not a change to the
Stardew mod's dialogue policy.

**Reproducibility and cost.** Each actor submits at most one reflection a day, and a source memory
is considered once. Model calls occur between simulation minutes, with bounded requests and
deadlines; processing speed cannot choose when an answer takes effect. Every proposal, weight
table, selected line and fallback is recorded for inspection. Replaying recorded answers
requires an exact request match. Determinism means the same seed plus the same recorded model
answers and world inputs reproduces the run, not that two fresh model calls must agree. The
1,250-character Laya budget is Sid's working constraint; prompt construction preserves the
candidate lines and reserves memory/context before using the remaining space.

**Current limit (updated by D38).** One unperformed accepted intention occupies the actor's
pending slot. Once acted, outcome tracking continues without blocking new thoughts. A deferred
idea can return only after distinct later personal knowledge and a full-day cooldown, with at
most two linked continuations. Optional waking inspiration and a pinned local generator are
now built (D39); a general speech catalog remains future work. Live Laya and generator calls
work, while the quality of model-driven social behavior remains unverified.

**Why.** More rules and aggregate stability had postponed testing the intended experience.
An inspectable causal sequence makes the model's contribution visible, including decisions an
observer dislikes or did not expect, without explaining the character afterward in invented prose.
See [the reflection spec](under-glass/specs/reflection-spec.md) and
[the game design](under-glass/design.md).

### D37. Contextual inspiration needs controls for labels as well as meanings

**Decision (2026-10-08).** Replace the generic authored thought with nine editable possibilities:
reciprocity or suspicion after received kindness, connection or boundaries after one's own
kindness, pride or repair after received hostility, regret or doubling down after one's own
hostility, and a grounded neutral fallback. Five response profiles fit those remembered scenes.
Overlapping regard ranges let the seed supply opposing possibilities; the evaluator still
chooses whether to accept, reshape, defer or reject. Prepared proposals carry IDs/tags and can
be overridden for a controlled comparison. Authored weights favor that proposal, not the first
candidate. Remembered identity and kind remain the character's own, even when mistaken.

**Measurement.** The first live seven-day seed-7 recording received 27 Laya answers without a
fallback in about 1.5 seconds total. A separate trial made 15 calls: five baseline/variant/repeat
triples, all with complete submitted context and no server truncation. Total variation was
0.0687 for a memory change, 0.0379 for trust, 0.1031 after matching the same line meanings across
a line swap, 0.0330 for order, and 0.0643 for renamed labels after matching their meanings.
Exact repeats were unchanged in this run. These are observations from a small trial, not
confidence bounds or quality scores. Label sensitivity is comparable to memory sensitivity;
clean evidence that the model follows social meaning has not been established.

**Consequence.** Keep both the complete request and raw response, inspect model routing and
truncation metadata, omit a proposed choice-ID answer hint, and treat explicit server truncation
as fallback. Keep semantic changes separate from label/order controls before tuning the model or
expanding its responsibility. Do not convert a successful HTTP run, an interesting anecdote, or
the desired town statistics into a claim of better character judgment. Local generation was
unverified at this trial; D39 records the later implementation.
[Experiment record](under-glass/experiments/reflection-trial-2026-10-08.md).

### D38. Control evaluator presentation and require new knowledge before reconsideration

**Decision (2026-10-08, within Sid's authorized observer-first development).** Keep `Raw` evaluation
for comparison and API compatibility. `Canonical` groups identical kind/line alternatives,
sorts them and supplies temporary labels. `Balanced` gives each distinct alternative every
position once, averages the mapped normalized probabilities, and then makes the same single
seeded draw. Equivalent original choices split their group's probability. The replay CLI uses
Balanced by default; the trial CLI retains Raw and can compare all three modes. This reduces
arbitrary presentation effects without forcing a socially preferred answer.

Every evaluator pass retains its prompt, raw response, temporary-label map and result. All
passes share a deadline; a failed or truncated pass causes a recorded authored fallback, never
a partial average. The earlier choice-ID answer hint remains absent. Live smoke probes reported
median evaluation times of 19.14 ms Raw, 23.41 ms Canonical and 127.28 ms Balanced for five
alternatives. Canonical/Balanced label/order controls reached zero or rounding noise because
the adapter presents the same semantic alternatives in the same evaluated arrangements.
That result verifies the control, not the model's intrinsic reasoning. Separate social probes
change perspective and inspiration while preserving the choices; judging those responses and
the resulting lives remains an experiment. [Reliability measurements](under-glass/experiments/reflection-reliability-2026-10-08.md).

**Continuity.** Deferring settles the current decision. A different own deed or identified
firsthand encounter with the same believed subject can reopen that idea only when it became
known after the previous decision and at least a full day has passed. Each root allows at most
two linked continuations. A number changing in the mood/regard tables is insufficient; rejection
remains terminal and expiration does not force a retry. Distinct new sources can still start
independent thoughts. An acted intention releases the pending slot while its outcome continues
to be recorded, so the ordinary outcome window no longer freezes someone's inner life.

The continuation names its previous thought and source, carries at most 120 characters of the
earlier imagined idea, and explains the later known encounter. The fresh memory stays the new
act's immediate cause. Neither idea becomes evidence, and mistaken identities remain mistaken.
Exact tape matching includes these links. The viewer shows the earlier thought, the changed
context and the later consequence at their own replay times, alongside a current-moments strip
and event navigation. Optional dreams follow in D39; automatic revival of expired intentions
remains outside this slice.

**Why.** A second random draw over unchanged information would manufacture agreement rather
than a change of mind. New personal knowledge gives a traceable reason to reconsider, while the
observer can follow ongoing lives without reconstructing them from repeated log lines.

### D39. Waking inspiration draws on earlier knowledge, with optional local generation

**Decision (2026-10-08, following Sid's dream/daydream proposal).** Dream inspiration is an
optional opportunity on an actual recorded wake transition, not a periodic thought relabeled
as a dream. `DreamChance` defaults to zero; replay's `--dreams` uses 0.15 unless explicitly
overridden. An independent seeded draw occurs at most once for a sleep interval. A failed roll
leaves quiet reflection available; a submitted idea shares the one-reflection-per-day limit
and the rule that a source is evaluated once. An unperformed accepted intention still blocks
new ideas. There is no new action kind or automatic retry of an expired intention.

Only a recent own deed or identified firsthand encounter known before sleep can ground the
dream. Waking-minute encounters, hearsay and unknown identities cannot supply missing facts.
The request records its own sleep interval, and the catalog supplies nine distinct editable
waking possibilities with dream tags. The prior encounter remains evidence; what the sleeper
imagines is not. The decision is evaluated after waking, applied at the next simulation minute,
and any act waits for ordinary eligibility. Exact tape matching includes the sleep opportunity.

**Local generation.** The optional helper pins `Qwen/Qwen3-0.6B` to revision
`c1899de289a04d12100db370d81485cdf75e47ca` and runs separately on loopback. The adapter derives a
seed from the request ID and records the full generation request and response, including usage
and revision metadata. It accepts only a short thought and a supported suggested act; Laya
still evaluates the actual response lines. Failures remain explicit authored fallbacks. Local
GPU smoke inference is roughly one second per thought, suitable for testing occasional
inspiration; this is a measured setup, not a speed guarantee or proof of useful imagination.
Generation seeds do not replace recorded-answer replay across runtime or hardware changes.
The generator sees allowed act IDs/kinds and structured own/received source facts; only Laya
sees the complete response lines. The controlled generator probe found speech copying and
option-order sensitivity. Removing dialogue inputs reduced copying, while semantic mismatches
and source-role confusion persisted. Hybrid remains an explicit experiment; structural JSON
validation does not establish thought/act agreement or grounding.

**Why.** Sleep can provide a distinct moment for imagining how a known relationship might go,
without granting new knowledge or making every routine utterance depend on generation. The
observer can inspect the real source, the imagined possibility, the decision and what followed.

### D40. Keep playback in reach and make a followed person's events visible on the ruler

**Decision (Sid, 2026-10-09).** The timeline and playback controls stay at the top while scrolling
the town, inner life and other views. Keep this bar outside the map's container so its sticky
range covers the page. Previous/Next moment lives in the bar too. Resize observation reserves
its actual height for scrolling to a thought, including narrower layouts.

A separate row highlights a selected person's performed and received encounters, their own
considered thoughts, outcomes and expired intentions, and their relationship changes. Hover
lists nearby events; clicking selects the nearest marker and pauses at its exact recorded minute.
Future markers are dimmed and expose only a timestamp until the clock reaches them. Descriptions
use recorded content as plain text, and refresh while playing or rewinding. This is observer UI;
it changes no simulation behavior or recording schema. The existing log remains in its column.

**Validation.** Six dependency-free Node tests exercise the shipped timeline helpers in CI;
the .NET suite guards the sticky containment and navigation controls. Browser checks cover
scrolling, selection, overlapping hover descriptions, marker clicks, playback and rewinding
past a thought without leaking its text.

### D41. Prioritize concrete encounters and contextual significance in Under Glass

**Decision (Sid, 2026-10-09).** Continue the observer-first social simulation and occasional
local inspiration plus typed Laya evaluation. The [Under Glass roadmap](under-glass/roadmap.md)
is the current work order: concrete encounter context and appraisal, broader executable responses
with authored speech, follow-through, and controlled model comparisons alongside scene review.
Player gameplay and larger ambition/generation systems come later.

The [next slice](under-glass/specs/contextual-events-spec.md) separates recorded event facts,
private intention, each person's perceived account, personal significance and interest to a
particular listener. Routine reciprocal help need not be news. A meaningful exception needs
known history or a supported cost/consequence, not a dramatic explanation invented afterward.
Start with observer presentation; changing gossip selection is a separate verified step.
Reuse the existing optional action catalog when expanding reflection.

**Why.** Feud counts, news volume and E1/E2 thresholds do not tell us whether the observer can
understand a relationship or wants to follow it. Keep those measurements as diagnostics and
the older plans as dated history. Coherent quiet, hostile and socially collapsed runs are valid.
Causal integrity, private knowledge, eligibility, resource conservation and recorded-answer
replay remain requirements. Existing regressions are updated only with an intentional behavior
change; this documentation reconciliation changes no runtime defaults or simulation behavior.
Generator quality remains under review while the world gains context; fine-tuning is not a
prerequisite for the next milestone.

### D42. Capture encounter appraisals from acquired knowledge and preserve every source event

**Decision (Sid, 2026-10-09).** Build the first observer slice of D41 without changing gossip,
feelings or act selection. `Simulation.Encounters.cs` records act facts and self-knowledge at
beginning, then perceived accounts when a belief is acquired. It snapshots believed identities,
source/chain/confidence and the holder's own relationship inputs before the current affect.
Private selected motive/reflection causes live separately; witnesses do not inherit them.
Imagination and a selected line are not proof of a feeling, intent attributed to another person,
or successful reconciliation. Unsupported task/item/effort/cost remain unknown.

An authored baseline separately explains personal significance and interest to the holder who
learned the account. Household care, recent known hurt, strain, repetition and relevance supply
inspectable reasons. These provisional outputs are neither a realism score nor a replacement
for gossip selection. The legacy kind tier and reporting rules remain intact.

Stories defaults to encounters and groups everyday acts by unordered pair/kind/place within
90 minutes of the first member. Every act is selectable. Appraisals, reasons, private causes,
evidence and group members follow the clock; old files show Context unavailable. Additive
encounter schema 1 preserves run version 1 and model requests. Replaying the existing 91-answer
hybrid tape gives the same hash and all previous fields except the command label.

**Validation.** Eleven new .NET tests cover roles, knowledge, repeated/corroborated events,
snapshots, private receipt links and deterministic recording. Five Node tests cover old files,
future appraisals, reciprocal grouping and rewind. Browser checks cover household care,
individual members, per-person reasons, private thought navigation and clock filtering.
Concrete task context and listener-specific gossip selection remain later slices.

### D43. Broader reflection responses keep their source and actual recipient answer

**Decision (2026-10-09; verified 2026-10-12).** Connect thanks and apologies to the existing
Returns/Repair catalog rather than adding parallel mechanics. Offer at most one extra response
for a suitable source, with complete voice lines and defer/reject preserved. Eleven authored
daytime/dream proposals cover the expanded set. Recheck switches, watch mode, age/place/Fits,
daily light caps, source roles/windows and duplicate responses when acting. A thought cannot
grant an ineligible act or invent a source hurt. Only the matching Return/Remorse is consumed.

An apology uses the existing recipient Ask and accepted/refused consequences; reflection waits
for the act to finish before settling. A sourced thank-you resolves only the named kindness
and matching participants, preserving mistaken belief without redirecting settlement to a true
giver. Apology does not resolve an unrelated reverse-pair encounter as generic returned kindness.
Default catalog-off runs are unchanged. Enabled-slice tapes must match the expanded requests;
older recordings remain viewable.

**Validation.** Source, eligibility, cap, duplicate and outcome regressions; six-choice real
adapter packets in raw/balanced modes fit the 1,250-character semantic budget with full lines.
Viewer checks cover new intention labels and completion/outcome clock boundaries. The bounded
28-day pack records settings, actual backend/fallback counts, source hashes and tape commands.
Model grounding and private thought quality still require scene review.

### D44. Reusable voices and personal starting histories are separate private context

**Decision (2026-10-12).** Sid selected hybrid and the pre-farmer backstory baseline. Read the
installed game's dialogue corpus to derive generic drives, coping and cadence; named profiles
add personal accents and individually reviewed source-keyed historical anchors. Conditional
dialogue is voice evidence, never automatic proof its event occurred. Do not copy named donor
biographies into future people or invent dates/episodes to make them feel experienced.

The explicit optional pack freezes content and hashes all source references/history. Reflection
copies one owner-only relevant/general anchor; no anchor creates an act, belief, feeling, gossip,
regard prior or opportunity. The generator and evaluator share the selected anchor, with compact
voice cues for Laya. Required facts and complete candidate lines are preserved or the adapter
falls back visibly. Profile-off requests/prompts preserve compatibility. Exact tapes reject
changed profile content. Observer details expose supplied context/provenance at the thought's
recorded time. This is a partial initial history and an editorial archetype interpretation,
not a claim of automatic canon reconstruction or established model quality. Ordinary spoken
templates and thought/action semantic agreement need separate work.

*Read with D53 (2026-10-14): the anchors are background context for the models, not a history; seven
were corrected.*

### D45. Test private wording with the intention already fixed

**Decision (2026-10-12, Sid).** Distinct voice cannot be established by longer personality
paragraphs or valid generated JSON. Compare actual character dialogue and withheld authored
alternatives on a fixed scene, target, motive and stance. Start with Vincent, with Maru and
George as contrasts, using two prepared gratitude plans per character.

The isolated wording suite compares profile-only and dialogue-reference prompts with matching
seeds and budgets. References select an attributed NPC node, retaining raw source, conditions,
variant and hashes; they supply style evidence, never scene history. Authored controls stay
outside the request. No evaluator chooses an action and no world state changes. Retain failed
candidate prose and full receipts; report format, meaning, register and source copying separately.
One retry may address a clearly observed prompt failure, preserving the original run. A curated
six-case probe does not establish general model quality or justify shipping its wording into
the simulation. See the [experiment](under-glass/experiments/voice-wording-2026-10-12.md).

### D46. A cloud generator is an opt-in behind the same loopback contract, never a default

**Decision (2026-10-12, Sid asked to try Claude Haiku 5.5 as the inspiration generator).** Add a
second generator service, `sim/tools/claude_generator.py`, that answers the Qwen service's
chat-completions contract from the Anthropic API through the official SDK. The .NET adapter,
the trial suites, the prompts and the loopback rule are unchanged: the adapter still talks only
to `127.0.0.1`, carries no credentials, and tells the services apart through the receipt. The
launched `--contract` selects a structured-output schema so a reply has exactly the fields the
caller checks. Sampling parameters and the seed are accepted for compatibility, not forwarded,
echoed for the receipt and declared unhonoured in every reply; exact replay is tape-only.
Credentials come from the environment or a key file outside the checkout and are never logged.

**Boundary.** This is the first path by which scene text leaves the machine: the generator
request (context, memory, known-source facts, act kinds, composed voice, one anchor, and in the
wording suite's reference arm the selected dialogue excerpts). Candidate lines, the evaluator cue,
hashes and request IDs do not go. The service runs only when launched for an experiment; the
pinned local service remains the default and the runtime defaults are untouched. A declined
request arrives with its real stop reason rather than as a thought.

**First measurements.** On the fixed-intention probe Haiku with thinking off passed 18 of 24 under
a blind three-judge review that passed all 12 authored controls and none of Qwen's 24; it copied
no source dialogue and its three voices were told apart 22 of 24 times. In a seven-day hybrid
week it agreed with its own suggested act in 48 of 64 thoughts and contradicted it in none, where
the profile-matched Qwen week was 19 and 31 of 62. It also showed pet phrases per character,
addressed speech with thinking on, and invented small details. Model-assisted review and six
curated cases rank the sets; they do not establish voice quality. See
[the experiment](under-glass/experiments/voice-wording-haiku-2026-10-12.md).

**Why.** The roadmap asks for generator comparisons with the evaluator held fixed and records of
model IDs, prompts, outputs and costs. Keeping the contract and moving only the service makes the
comparison controlled and keeps every existing test, tape rule and privacy boundary in force.

**Sid's decision on the result (2026-10-12).** Haiku 5.5 with thinking off is the generator for
hybrid recordings from here; its cost is acceptable. The pinned local service stays as the offline
control and the shipped default, so a recording still names its generator. Imagined specifics in a
private thought (a bit of bread, a shiny stone) may stay: the thought is imagination, not evidence.
The open concern is an imagined detail that does not correlate with the context or with what a
player would see; that is deferred, not dismissed.

### D47. A reflection request says where and when, from the actor's own records

**Decision (2026-10-12, the first step of Sid's agreed order: expanded actions and abilities
before any player).** Until now the generator and the evaluator saw one sentence about the
remembered encounter and a line of numbers. Each request now carries a `ReflectionScene`: the
encounter's place and age in calendar days, the actor's place and time of day now, how they know
the subject, the pair's kind and unkind acts this week with the last one, and the actor's own
appraisal reasons as phrases. It is built from the actor's encounter history and appraisals, so
it is what they know, including a mistaken identity; nothing is read from the subject's records or
the world's truth. Every catalog kind an actor can do to someone has a memory verb; "interacted
with (StoodUpFor)" had been read by a generator as being stood up.

**Packets.** The generator gets the scene as structured fields and a rule that a small imagined
detail must fit that place and the actor's life and never add a person, an event or an act. The
evaluator gets one line inside the 1,250-character budget only when the required context, memory,
anchor and full candidate lines still fit; otherwise the line is omitted whole, which the recorded
packet shows. The viewer shows the scene under "What informed this choice?".

**Tapes.** The scene is part of the exact-match request, so recordings from before this change
no longer replay; they stay viewable. The verified October 12 pack is the last one on the old
request shape.

**Why.** Scene-specific knowledge was the cheapest of the four agreed steps and every later step
reads better for it; it also answers Sid's deferred concern about imagined details by telling
the model what the scene contains instead of forbidding items.

### D48. What people say is authored, tagged and chosen from the speaker's side, and changes nothing

**Decision (2026-10-12, step 2 of the agreed order, built while Sid was away and awaiting his
curation).** Acts toward a person get spoken lines from an embedded, editable library,
`sim/UnderGlass.Sim/speech-catalog.json`, behind `--speech` (off by default). A line is tagged by
act kind, archetype, relationship band and mood band; empty tags mean any. Selection reads only the
speaker's own state, weights fitting lines rather than picking the most specific, avoids the
speaker's last three lines for the same kind, and draws on speech's own seeded stream keyed by the act
id. Nothing in the simulation reads a line, so the pinned runs are byte-identical with speech off
and every decision is identical with it on; the record says which library id spoke and why.

**Authoring.** The roadmap's method, applied: one Claude author per archetype wrote new lines with
the local dialogue export as style evidence only, a generic author wrote plain lines, every line
was copy-checked against the whole corpus (normalised equality or a 20-character containment either
way), and three readers judged each line blind for which voice it sounded like and for rule breaks.
A line the readers could not place kept its words and lost its archetype tag; a line heard as
another voice, or breaking a rule, was dropped. Turning away stays silent. No line names an item,
a task or a place, because the simulation does not know them.

**Why.** Sid named repetitiveness as the thing to fix before any player exists, and the viewer
could show no speech for ordinary acts at all. Authored, tagged lines keep the rule that routine
language is authored while the generator stays occasional; the blind judging keeps the voices from
collapsing into one. See the [speech spec](under-glass/specs/speech-spec.md).

### D49. A speech tag names where a line belongs best, not the only place it may be said

**Decision (2026-10-12, Sid asked for it after seeing why the library felt repetitive).** A line's
relationship and mood tags no longer rule it out everywhere else. A tag that names the speaker's
band doubles the line's weight as before; one that only names the neighbouring band (one rung of
familiarity, one mood band) halves it; anything further rules it out. Household lines never leave
the household, though a household speaker may borrow a close line, and a low line is never said in
high spirits. Archetype tags stay exclusive: another voice's line is never borrowed.
`SpeechCatalog.Weight` is the rule; the curation page `sim/viewer/speech.html` carries a copy, and
`sim/tools/fixtures/speech-weights.json` pins both.

**Why.** Counting the situations showed the cause. Across 14 kinds, 12 voices, 5 relationship
bands and 3 moods, a median of 3 lines fit a given speaker in a given state, and the selector
avoids their last three, so repetition was built in; adding lines evenly would have needed several
times the library. With neighbouring bands the median is 5, the situations with 3 or fewer fall from
most to 77 of the 2,376 that can happen, and the recorded week (same seed and flags, same log hash)
speaks 80 distinct lines where it spoke 60, with the same speaker repeating a line back to back 12
times instead of 27. The rest is authoring, and the page shows where: the thin cells are the ones
to write for.

**Not changed.** What a line is, how it is recorded, and that nothing in the simulation reads it.

### D50. The Newcomer speaks with the player's words; Laya reads them into what the engine offers, and words are not facts

**Decision (Sid, 2026-10-12 and 2026-10-13).** The Newcomer gets a voice of its own, and it is the
player's: "Ideally players get to determine how the character sounds. I'd like them to be able to
type and have laya categorize the response." A claim in those words is not a fact: "Characters will
need the ability to verify claims", answering by character ("Okay, let's see the fish", "Give it to
me", "Oh! Thank you so much, I have something special planned").

**Plan, not built.** The [player speech spec](under-glass/specs/player-speech-spec.md) sets it out:
the typed line goes to Laya with the acts the engine would allow toward that listener now, plus
small talk, and a second question with the things the listener knows of, plus "none of these";
the engine executes the reading through ordinary eligibility; a listener takes a claim on trust,
asks to see it, demands it or doubts it from their own memory and trust in the speaker; a check is
an event others witness; a lie taken on trust comes out only through perception. It extends
design 5a (free text, misreading as a feature, Laya never writing) and sets where the agreed order
leads: after asks with answers (step 3) and needs with real items (step 4), so a typed line has
something to become and a claim something to be about.

**First measurements (2026-10-13).** A probe ([write-up](under-glass/experiments/laya-categorize-2026-10-13.md))
on 638 lines and 64 claim cases, judged blind first: player-style lines read right 60% of the time
with fifteen options and 76% with six (readers 99%); claims 83%. `typed-decisions`' confidence was
honest (at 0.7 or above, 94% right), `english`'s was not. Hence: offer only possible acts, take a
confident reading, let the player pick from the top three otherwise, guard claim matches, keep
`typed-decisions`.

**The gate (Sid, 2026-10-13).** Nothing gates scripted lines. Before live typing, the reading is
measured on Sid's own 50 lines (design E4a): at the 0.7 confidence line, readings taken right at
least 90% of the time and the top three holding the right act for at least 80% of the rest; failing
that, `typed-decisions` is fine-tuned on half his lines and tested on the other half first.

**Not changed.** Laya writes nothing; every villager's reply is authored (D48); nothing in the
simulation reads the player's words except through Laya's typed reading of offered options.

### D51. Company is asked for, and the answer is real

**Decision (2026-10-13, the first part of the agreed order's step 3, which Sid confirmed as next).**
With the opt-in switch `Invitations` (needs `Company`), a game or a drink is asked for before it
begins: batch 2's general `Ask` with `AskKind.Invitation`, whose obliged-against-cost formula was
already written and unused. A yes begins the game or drink exactly as before; a no begins batch 2's
`Refused` row instead (the asked person's one-minute act toward the asker: the asker takes part,
so knows who said no and feels it, and anyone in sight can see it), and the asker waits
`InviteAgainDays` (7) before inviting that person again. For that, a given deed done to someone face to
face (`TargetIs.Given` with the target as patient, within reach) now makes its target a participant
as a chosen one does; one done about someone out of reach does not, and `Refused` is the town's only
such row. A refusal is not the asker's gate act: the gate's pursuit is
recorded as not acted, and the invitations table links the asking to the refusal. Acts a fixture injects are not asked. With speech on, the asker's offer and the asked
person's authored reply are both recorded on the act, the reply marked `Reply`.

**Why.** Before this, company was one-sided: the one who began a game simply made the other busy.
The roadmap asks for accept and decline as real outcomes with distinct consequences, and the Ask
was the seam built for it. Doing it at the moment the act would begin keeps every route (motives,
per-head draws) and every eligibility rule as they were.

**Differences from batch 2's plan.** It is company now, not a date later (no promise, no
`Invited`, `MetUp` or `StoodUp`); the asker feels the `Refused` row's affect rather than
`RefusedSting`; the row has no gate, so it is not light; there is no asking act to resolve. At
trivia 1.0 a refusal is seen and remembered but never volunteered in gossip. The acts-batch2 spec notes this beside the row.

**First measurements.** Seed 7 with the observer pack's flags: in 28 days, 22 invitations among
16 pairs, 20 accepted; both noes were close calls involving the barely known Newcomer. With the
switch off the week's log hash is unchanged.

**Sid's answer (2026-10-13).** Refusals should come more readily when someone is tired, busy or
low, and depend on the two people's relationship: someone eager to please will still do it when
low, as Penny does things for her mother. So the cost of saying yes now includes the asked person's
own state (tired, due at work, low; 0.3 each at full), shrunk by their eagerness to please (low
self-regard and low boldness) times their bond with the asker (whole for family or a shared home,
otherwise liking and familiarity), and family adds 0.2 to the obligation (`AskMath`). Across six
seeds of 28 days, noes went from 9% to 30% (49% at 0.5); tiredness carries nearly all of it, since
company is in the evening and the town's moods rarely fall below even. Each invitation records the
state behind its answer. See the [invitation spec](under-glass/specs/invitation-spec.md).

### D52. A need names one thing, and a promise to fetch it is kept, kept late or broken

**Decision (2026-10-13, the agreed order's step 4, follow-through; Sid: "start on the need with a
real item").** With the opt-in switch `Errands`, people come to need one named thing from their own
life (flour, a coffee, a bottle of beer: Stardew's goods, sold where Stardew sells them) and ask
someone in reach to fetch it, through the general `Ask` (`AskKind.Errand`). A no is a `Refused`, as
an invitation's is. A yes is an `AskedFavour` act and a `Promise` (kind `Errand`, appended, with the
thing and the act that bought it appended to the record): due at the end of the day two days on
(Stardew's item request gives two). The one who promised walks to the counter while its sign says
open (`Decide`, after work and patrol: `ErrandGoal`), buys it from whoever is serving (`Fetched`; the
money moves as groceries' does), and hands it over when the two meet (`HandedOver`, a kindness
aimed at the asker that answers the asking; the asker pays it back unless they share a home).
Late if after it was due; not by a day after that, the promise breaks: `LetDown`, begun unseen,
known and felt by the one let down. A need nobody promises lapses quietly, as Stardew's request
expires. Looking for the person reads only memory and public knowledge: where they work, in working
hours (even a home that is a workplace: the clinic, the ranch), then where the runner last saw them
out of doors; never into a home otherwise. Asking and handing over need the two in sight of each
other, as every act toward someone in reach does. A town file's errands book is checked when it is
read (`TownCheck`). The errands
book (`ErrandBook`: goods, counters' signs, who uses what, who won't fetch what) is appended to
`Economy`; the shipped town has one, grown towns not yet.

**Why.** The roadmap's follow-through asks for one concrete thread, need to outcome, that an observer
can follow across days, including refusal, failure and expiry, with consequences that cite actual
events. A shop good gives the need an object and a price, the counter's sign gives the attempt a
real obstacle (working hours), and the promise record batch 2 seamed holds it all. Stardew gives
the item and the window; The Sims gives needs from one's own life, chores in the family, the trip,
the hand-over in person, and a broken promise that is felt, unlike a lapsed want.

**Sid's answer on Penny (2026-10-13).** "She is ashamed of her mom's alcoholism so she despises it.
I think Penny is a case where she would do anything for her mom besides give her alcohol." With
reluctance as a cost (0.5), Penny had fetched beer for her mother five times of six; now what someone
won't fetch (the book's `WontFetch`) is a no whatever the margin, and the margin is still recorded.
Her trying to minimize her mother's drinking is proposed as a concern (won't enable it, shame rather
than coldness, keeping Pam from the saloon), to decide with the needs bars revisit.

**First measurements.** Six seeds of 28 days: 281 needs, 462 askings, 61% yes (92% within a home,
44% across homes); of 281 promises, 261 kept in time, 3 late, 12 broken. Every broken promise was
someone who works through the counter's hours; whether people should count their own hours when
asked is open. Penny said no to beer eight times, all to her mother, most at margins that would
otherwise have been clear yeses. See the [needs spec](under-glass/specs/needs-spec.md).

*Read with D53 (2026-10-14): the concern no longer waits for needs bars, which are unresolved.*

### D53. History before bars: the pack's anchors are context for the models, not memories, and seven are corrected first

**Decision (Sid, 2026-10-14).** His directions of that day are verbatim in the [Lives
assessment](under-glass/lives-gap-assessment.md), appendices A-E. For this entry:
- **Order.** Histories, associations and meaningful actions come before any decision on how needs
  bars influence behaviour. Next is the Lives pilot: one complete thread for Penny, Pam and Gus,
  from history through recall, competing choices and an attempt to each person's perceived outcome
  and what they learn, on general mechanisms and character data, never name switches. The same
  mechanisms then go to Evelyn, George and Alex before the population grows. "The observer product
  comes first; player gameplay remains later."
- **Law 1 unchanged.** Its sentence "There is no separate list of needs for social life" governs.
  The amendment asked for on 2026-10-13 (PR #91) is on hold.
- **Bars unresolved,** neither scheduled nor rejected: "Later, assess how bodily pressures interact
  with this richer system. Don't choose a formula now because it reduces aggregate boredom or
  loneliness." The bars experiment (PR #92) stays a draft and none of its modes is promoted: its
  "pure Spinoza" mode "tests sadness applied to an incomplete system", and "Its results don't
  establish that the underlying principle needs changing." This supersedes D52's "to decide with the
  needs bars revisit" and the roadmap's "Then, revisit needs bars".
- **The anchors are background, not history.** The pack's anchors (D44) are private context for the
  generator and Laya. As D44 says, they create no act, belief, feeling, gossip, regard prior or
  opportunity, so nothing in the sim yet starts with a history that shapes behaviour; an anchor
  reaches a choice only through the models' reading. Histories that do are planned in the
  assessment: facts, completed pasts, recurring practices and each holder's readings kept apart,
  admitted by the three rules of Codex's [history
  audit](under-glass/experiments/history-evidence-audit-2026-10-14.md) and counted once. The audit
  is cited and kept as written.
- **Seven anchors are corrected first, in their own PR, before any history reuses them.** Four said
  more than their sources: `maru-clinic` (her Tuesday and Thursday work, and Harvey's wish, as she
  reports it, to have her there in case his equipment breaks down; no completed repair: `Maru:Wed`),
  `linus-tent` (only that his tent has been destroyed before; no "someone", no wariness of visitors,
  and his plea to the farmer stays voice evidence: `Linus:Fri`), `demetrius-valley` (the ecosystem
  made him excited to move, not why he moved; `Introduction`'s present work no longer folded in:
  `Demetrius:Tue`) and `clint-pressure` ("not the future I wanted" is in no source; now `Sun8`'s
  weaker wording, that as a boy it wasn't really his dream, beside `Mon4`'s pressure). One was
  under-cited: `alex-childhood` ("young" rests on `Data/Events/Beach:288847`, now cited; the text is
  unchanged and no date is given). One was mis-cited and repeated another: `jas-parents` (drops
  `Jas:Mon`, which doesn't mention her parents, and "Shane was a friend of theirs", held once in
  `jas-shane`). One gave its holder another's account: `george-evelyn-meeting` (only Evelyn tells
  the meeting, `Data/Festivals/winter8:Evelyn`; George keeps the participant fact, a snowball at the
  Festival of Ice, and her report of his purpose and her reading of him leave it).
- **Penny and alcohol.** Her absolute refusal to provide alcohol is Sid's product commitment (D52,
  kept as written), not extended to accepting or drinking alcohol without a separate decision. Canon
  supports it as characterization but does not prove it: she hates beer, wine, pale ale, mead and
  hops as gifts, and item 873, a drink Pam loves (its name VERIFY; `Data/NPCGiftTastes:Penny`); the
  game never has her suggest Pale Ale, Beer, Wine or Mead as a gift for Pam (game `NPC.cs:3667`).
  Canon never calls Pam's drinking alcoholism; the admitted lines are milder: a problem with going
  to the saloon too much (`Penny:Mon4`), way too much beer (`Gus:Thu8`), not healthy
  (`Pam:summer_Thu4`). Penny's strongest words ("I don't want you destroying yourself!") come in a
  quarrel the farmer joins (`Data/Events/Trailer:35`), so they are voice evidence only. Her wish to
  keep her mother from the Saloon no longer waits for bars; the pilot takes up her care for Pam.

**Why.** Sid: "text supplied to a model is not equivalent to a history that shapes behavior."
Calling the anchors "a partial initial history" (D44), "a partial starting history"
(`sim/README.md`) or "historical anchors" (the brief) invited reading 101 anchors as 101 memories.
The audit found anchors that overlap (`jas-shane` and `jas-parents`; `leah-city` and `leah-kel`),
which would count one experience twice, and four needing correction; its check found three more.
Correcting them before reuse keeps unsupported detail out of anything that will later move a number.

**What changes.** `sim/character-inspiration.json` (seven anchors, and a dated sentence appended to
its provenance) and the docs. Without `--character-inspiration`, every run is byte-identical. With
it, the pack's runtime hash moves from `67aff67c…` to `dd6be675…`; every profiled request's snapshot
carries it, so every exact tape recorded with the earlier pack fails replay (the recording stays
viewable; replay it with `git show a0a40ef:sim/character-inspiration.json`). Prompt text changes
only where one of six anchors is drawn; `alex-childhood` changes only its sources, which no model
sees. No anchor changes holder, subjects or ID, so the same anchor is drawn as before. Every profile
still holds 2 or 3 anchors of at most 120 characters. Eight tests pin the corrected sources and the
claims removed. D44 and D52 stay as written, each with a pointer here.

**If you undo it.** The anchors would again say more than their sources, and calling them memories
would again invite seeding behaviour from prompt text.

### D54. Penny's sourced routine is her character data; bedtimes are habits

**Decision (Sid, 2026-10-14; the assessment's O1, and his answer on early bedtimes).** Unsupported
starting routines are corrected as character data, with their source and the change in behaviour
recorded, not outweighed by history weights; later experience should be able to change them (not
built: a card is fixed for the run, and an own weight of 0 removes the hub from the draw outright;
that learning is the pilot's). Penny's first. Her game schedule (`fixtures/game/Penny.json`) never
puts her at the Saloon except on year 1's green-rain day, and she leaves for bed at 18:30 on
Tuesdays, Wednesdays and Fridays, 20:00 on Saturdays, 21:00 on summer Mondays, Thursdays and Sundays
and 18:40 on the others (winter's Community Center schedule falls back to spring until the Center is
restored, and the sim has no Center). On what follows an early bedtime, Sid decided (verbatim):

> Pilot decision: bedtimes are habits, not hard rules.
> Keep Penny’s sourced bedtimes as routine preferences. Actual sleep and waking follow the energy rule: when rested, she wakes—even at 3 am—and can choose among the actions available to her.
> Don’t force sleep until 6 am, introduce a special Penny correction, or make her unavailable merely because her habitual bedtime has passed. Actual sleep can prevent conversation; the schedule alone shouldn’t.
> Record the habitual bedtime, actual sleep onset, waking time, energy, and what she chooses next. Distinguish sourced routines from our simulation assumptions.
> If early waking exposes missing activities or implausible decisions, report those gaps. We’ll assess them through the pilot rather than suppressing the outcome.

**As built.** Two optional fields any villager can carry, with the data in `DefaultTown`'s
name-keyed tables (built afresh for every town), never a name switch: `HubWeights`, a person's own
weight for a named hub in place of the hub's (`OwnWeight`; 0 or less: the hub is left out of their
choices), and `Bedtimes`, habitual bedtimes by weekday and season, the first covering a day
applying (`KeptHour`, its minute counted on the evening's day as canon writes it, 24:00 being
1440). Each datum carries its source and, where a sourced value is applied beyond what its source
says, our assumption; the record marks it "sourced", "ours" or "sourced+ours". From a habitual
bedtime until they next sleep, going to bed is one of their free-time choices, weighted
`HabitOptions.BedtimeHabitWeight` (9, ours; against home's 1 that is bed at the first pick 9 times
in 10 by the weights, and Penny picked it on 84-87% of nights at the hour). At the first free-time
decision after the hour, a pick in progress is reconsidered once. Nothing sets "tired" and there is
no rise hour: sleep starts on reaching the bed and ends when the bar is full, the body still sends
anyone earlier, and nobody is less free, in reach or askable because the hour has passed. Penny's
card carries `Evening = 0` (sourced: no key of her schedule puts her at the Saloon but green rain;
ours: "never", in every year, since the sim has no green rain) and her bedtimes, 18:40 in every
winter being ours (the sim has no Center to restore). The 0 removes the gathering from her choices,
not the place: only an errand takes her to the Saloon today. A recording gains a write-only
`nights` block, one row per sleep: the habitual bedtime and its source, when and why they set out
(habit or body), her energy and the options drawn from at the hour and at setting out, onset,
waking, energy and the next choice with the options it was drawn from. The sim's later bedtime
stays as the town `pelican-simulated-bedtime`, and the town as it shipped as `pelican-as-shipped`
(run hash for run hash; census hashes move with the generator's version). `TownGen.Version` is 3.

**Not represented, recorded.** Rain (the sim has no weather), the 9th and 23rd, winter 4 and 15,
festivals, green rain, Saturday in Town with the children until 20:00, her canon teaching days and
place (Tuesday, Wednesday and Friday at the museum; the sim keeps Monday to Friday at the Square),
her afternoons, and canon's 06:00 waking.

**Why.** As shipped she spent about 80% of evenings (90, 90 and 88 of 112 on seeds 1-3) at the
Saloon's Evening gathering, buying a drink each time, because at 18:00 her only choices were the
Saloon (weight 2) and home (1): a pull canon doesn't support, at the centre of the Lives pilot's
household. A bedtime kept as a rule (held asleep to 06:00) would have matched canon's evenings but
broken rule 1's sleep cap; Sid chose habits.

**What changes.** Every pinned run that includes the shipped cast (44 test cases, old and new hashes
in the commit that adds Penny's data; the as-shipped town keeps six of the old values as tests); the
grown towns' census hashes; two golden tests re-picked (G1's feud now seed 2; the shame check's
placed scandal now seed 14, Penny ashamed of Pam's rummaging in both towns). Measured in [the
write-up](under-glass/experiments/penny-routine-2026-10-14.md) (seeds 1-3 x 112 days): no Saloon
evenings; on her 18:30 and 18:40 days she is asleep a median minute after canon's hour, and wakes
rested at a median 02:50-03:06; her body sends her before her 20:00 and 21:00 habits (Saturdays
about 19:15, summer about 19:52); on waking her only option is home, and she sits there about six
hours alone before work. She misses Leah and Maru and receives about twice the kindness; her regard
with Gus falls to 0; she hears of Pam's scenes rather than seeing them. Without her drinks the
Trailer stays out of debt, and Pam's need-driven bin rummaging, and Penny's shame at it, no longer
happen (20 seeds). The gaps early waking exposes are recorded for the pilot, not suppressed or
tuned.

**If you undo it.** Penny back at the Saloon most evenings, against her sourced routine.

### Observer tools navigation (2026-10-14)

Sid requested connected testing tools while the history assessment proceeds. A tools landing page
and shared preview navigation connect the dashboard, speech editor and recording index. Links
carry the same server-hosted recording and the dashboard's time, character and tab; returning is
paused. Existing saved HTML can be opened in the current viewer by extracting its run JSON,
without rewriting the artifact or executing its scripts. Local picked files remain local and
require reopening in the destination tool. This adds no history or archetype mechanics; the
causal graph and archetype studio remain assessment work.

Sid then requested visual consistency with the dashboard and less introductory text. The tools
reuse its palette, typography, controls and persisted theme. Speech shows four
catalog-wide metrics with definitions and source details collapsed; these do not change with grid
filters. The recording index puts the observer runs first and keeps setup, spoilers and older
experiments in expandable sections. Every original recording and reference remains linked. Sid requested plain title sections without
corner marks, a sticky shared top bar, and no timeline shadow or Happening now top border. The
bar height is measured so nested sticky controls and the speech drawer remain below navigation.

### Archetype studio first slice (2026-10-14)

Sid authorized a separate archetype browser/editor while Claude develops the observer recording contract. The studio uses shared UI styles and edits only reusable drives, coping, voice, evaluator cue and source references. Personal accents, background and assignments are inspectable but read-only; duplicating an archetype copies no biography or speech. Source-aware browser drafts and validated pack downloads make edits reviewable without modifying the checkout or recorded runs. Fixed-scene trials and causal graphs are still planned. This is independent of the lives assessment and evidence corrections.

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
- **Verify in-game.** No `VERIFY` comments are left in the code (the last were settled in the
  decompile on 2026-10-05; `stardew-source-notes.md`, "Audit pass"). Two recalls are still marked
  "verify" in comments: the `PlaceNames` wording, and how `ModEntry.ResolveHomes` treats a `Home`
  entry's `Condition`. Still to see in the game: event dialogue as a `Talked`, a farmhand joining a
  host, and a split-screen session.
- **Found in the audit (2026-10-05), waiting on Sid** (details in the spec Status sections and the
  audit PR):
  - the saved plan is restored at load and then cleared again (`OnSaveLoaded`), so the motives lose
    the day's news after a reload (persistence.md);
  - one gift seen by several villagers is several stories to a listener (`Gossip.EventKey`), and
    each copy is confirmed separately (ledger-gossip.md);
  - chats are drawn once per direction, about 51% of spans instead of 30% (ledger-gossip.md);
  - a store loaded from save data has the default chat seed, not the save's (ledger-gossip.md);
  - the live ledger is not reset at the title screen (rollout.md);
  - history at install creates an empty diary for every character in `Data/Characters`, the ones
    who can't socialize included; they then get a motives weighing every tick and a viewer card.


### Local run studio and Newcomer speech testing (2026-10-14)

Follow-up: Sid requested character inspiration editing within Archetype Studio and selected
Haiku + Laya as the easy-start default, with Qwen as an alternative. Personal prompt context
is editable separately from reusable archetypes; neither is simulated memory. Installed model
services can start from New run, with explicit ownership and health status. Generation remains
unthrottled and awaits model answers between simulated minutes; playback speed is independent.

Sid requested a site tool with all run flags and a Newcomer speech-editor column. The local
preview now launches the existing Replay executable with typed, schema-described arguments,
one job at a time, into separate output folders. It does not start models or add a second
simulation implementation. Exported speech catalogs can be loaded for a run; the optional
Newcomer tag changes authored line selection only. Free-text player input remains planned.
The navbar uses **Run Viewer** for the dashboard.

### Life chains viewer boundary (2026-10-14)

Sid authorized the fixture-backed chain viewer and history comparisons alongside Claude's simulation work. The viewer uses a frozen illustrative Lives 0.1 fixture, exact version acceptance, and a separate pure projection adapter. Character views omit observer summaries, other minds, world outcomes and diagnostic secrets such as `notPerceived`; being targeted by an unspoken attempt grants no knowledge. Comparisons are explicitly observer analysis, isolated and fallback-only for this fixture. The UI displays recorded numbers without duplicating experimental formulas. No archetype definition is substituted from the current editable pack when the recording lacks an immutable snapshot.

### Life chains: time first, causes on demand (2026-10-14)

Sid found the stage columns disconnected from playback and unsuitable for a long-lived population. The viewer now uses a shared marked timeline and playhead, with a bounded causal tree around a hovered or pinned event. Starting history stays outside the timeline and opens only as relevant memory branches. Dense moments cluster, branches paginate, and selecting the latest-event control visibly selects the event rather than merely scrolling. Temporal order and causal links remain distinct: arrows use explicit references, not proximity on the timeline. This changes observer presentation only; it does not add Lives runtime mechanics.

### Lives personal-memory foundation (2026-10-15)

Sid authorized continuing the Lives pilot after the assessment and routine work, with the
archetype editor deferred. The first implementation increment was an independently testable
[history and memory library](under-glass/specs/lives-memory-spec.md). It shipped without a canon pack
or Simulation/Replay hook. This preserved D53's distinction between authoring
evidence, an individual's account and a reviewed interpretation before choices begin reading it.
Canon-stated numeric seeds require a reviewed reading; a completed past additionally requires
the holder's sourced present link. Authored interpretations are recall-only, with no canon
admission label. The test histories are explicitly synthetic, not approvals of proposed readings.

Implementation policies made explicit in this increment: active memory retains both signs per
object, and global pressure evicts a whole least-strong object rather than only its uncomfortable
side. Initial strength is diluted by cumulative acquired causes, so forgetting an active entry
does not restore the seed's influence. A cause's person/place/activity projections count once
per side when combined. Expectation learning starts at one outcome; a summary threshold changes
only the summary. Recall is indexed and bounded by records; aliases can still occupy several
slots, and the low-mood extra comes only from matching cues. These are inspectable experimental
policies, not new laws or evidence of character fidelity.

Immutable observer receipts never supply decisions back to the library. They are not the
versioned Lives export. Active association and per-key expectation windows are bounded; original
history, distinct expectation keys and the cause-receipt index are separate lifetime costs that
still need population-scale measurement. At this increment, choices, actual outcomes and the full
recorded pilot remained next; needs bars stayed unresolved and law 1 unchanged. The following
entry records the first integration without retroactively treating this foundation as a pilot.

### Lives free-time choices and held history comparisons (2026-10-15)

Sid authorized the next step: connect memories to opportunities and competing choices, then compare
the same decision with and without the relevant history. The first delivered integration is
[opt-in free-time routing](under-glass/specs/lives-routing-spec.md). It reads the existing physically
eligible destination menu after the existing duty and tiredness rules; it does not invent company
locations or replace routines with new plans. Relevant recall opens the assessment's experimental
weighting of base, warm and cold sides, boldness and a keyed draw. No relevant recall preserves the
exact original draw, and a menu above five remains wholly on that original path rather than being
truncated. Warm/cold weights use all retained associations to the named destination after the recall
gate, not only the three recalled context records. That implementation policy remains inspectable
in the receipts rather than being hidden as a claim about attention.

The live writer acquires already-felt amounts from the holder's existing perception and undergone
events, keeping one cause across person/place/activity projections. Told knowledge retains its teller
and does not gain the hidden event location. Witnessing an event does not make the believed identity
certain; confidence, clarity and presence basis are separately recorded. Revised attribution uses
`Correct` to replace the prior acquisition under the same cause, reversing its old dilution once.
A newer explicit correction may re-establish a retracted cause; an ordinary `Learn` retry still
cannot. The adapter writes no expectation outcomes yet, and generic routes read no expectation keys.

`Capture` creates an immutable frame using the production memory queries, without copying the
run-long retry ledger. History ablation removes the requested holder-owned fact/account/reading and
its dependencies, leaving independent lived evidence and dilution fixed. The same-choice comparison
holds the scene, options/order, other personal state, settings and random unit fixed, then recomputes
recall, eligibility, reasons and probabilities. Requested IDs and the full removal closure are both
recorded. A vanished opportunity, legacy-only side or changed eligible set is non-comparable; no
counterfactual action is manufactured or used to direct the run. This does not claim how a later
life or a model response would have differed without that history.

Replay adds a separate experimental `livesRouting` block, version 1, stage `free-time-routing`.
Its history snapshot hash is SHA-256 of this implementation's validated-pack JSON, not JCS or the
full observer contract's hash definition. Turning its observer record off does not change routing.
It is not a Lives 0.2 export, and the chain viewer does not read these receipts yet. No canon history
pack is shipped or implicitly enabled; tests use explicitly synthetic histories. There are no new
Laya calls, reflection/model-context changes, company-seeking actions, plans or expectation outcome
classifiers. Meaningful shared outcomes, reviewed pilot content and the full recorded thread remain
next. The archetype editor remains deferred; law 1 and the unresolved bars decision are unchanged.

### Lives immediate company and the retained routine baseline (2026-10-15)

Sid authorized the next steps and explicitly selected keeping the current routine baseline:
existing sleep rules and bedtime weight, with no additional bedtime penalty on invitations.
`--lives-company` builds one bounded executable thread before future evening plans: a recalled
warm person/activity cause can suggest seeking company or offering a game during an encounter.
No named-character exception or canon numeric seed is added. The assessment's larger pilot,
including later appointments, disappointment and Laya choices, is still planned.

Seeking uses only the seeker's recorded sightings (subject to the existing three-day retention)
or a known shared home. It does not consult remote positions or a partner's current schedule.
Current local perception can reveal that the person is present, asleep or occupied. Travel and
search windows are separate; an aborted journey does not manufacture evidence of absence. A
completed search with no sighting changes whereabouts expectations only, never acceptance or
regard. One active episode per person and one considered offer per unordered pair per day bound
repeated attempts. Legacy game invitations share the pair/day cap; their existing refusal cooldown
also gates new offers. Seeking candidates are capped at three in name order, a current limitation.

At a free-time decision, company competes with carrying on at the sum of the current routine
weights (including the existing bedtime weight). Carrying on uses the unchanged destination
selection path. An offer has its own carry-on alternative. A required recalled warm cause gates
each new option, and is reevaluated per comparison arm so another recalled warm cause can replace
one that was removed. Removing the only supporting history can therefore remove eligibility; such a
comparison is marked non-comparable instead of inventing a counterfactual action.

An offer receives a next-minute answer without creating a fake act ID. The offline three-way
fallback uses the existing Ask margin and state; yes/no have weights exp(±scale × margin), and
not-now has weight reason × exp(scale × reason), where reason is max(tired, busy, low). It is absent
at zero reason and grows continuously from zero. This is an experimental policy, not a claim that
the proportions match Laya. A no creates a real refusal; not-now changes availability only.
An acceptance also contributes positive availability evidence, so old postponements are not an
irreversible penalty. There are no new model calls in this slice.

Accepted games reserve both participants for the existing duration. Shared-home games can cross
ages without requiring an authored game card; elsewhere existing game place/age rules apply.
Continuity is checked through completion. Work, tiredness, collapse, another act or lost contact
can interrupt; only completion pays one sensitivity-scaled shared feeling to each participant.
Its person/place/activity projections share one cause. Later company choices read specific
whereabouts, availability, answer and activity expectations. A completed game currently always
has the catalog's positive affect; richer activity content and disappointing evenings remain next.

`livesCompany` version 1 is a separate experimental observer block, not Lives 0.2. Observer lists
never direct the run, and disabling their recording preserves behavior. The offline review uses
normal towns without history seeds plus clearly labelled synthetic households; no trial result
is an approval of a sourced character history or a social-outcome quota.

### Sourced histories, future evenings and Laya (2026-10-15)

Sid asked to connect the Penny/Pam/Gus pilot's histories to evening plans and Laya. The explicit
`sim/history/penny-pam-gus.json` pack records each holder's own reviewed accounts/readings. Current
feelings seed small separate warm/wary entries; a hobby wish opens an option without inventing
enjoyment. Past events without an admitted present link remain recall-only. Provenance and the
24 exact source keys are checked in the accompanying history review and tests.

The opt-in evening extension adds informal agreements and each participant's later choice to keep
them or carry on. It preserves the selected routine baseline, including bedtime weight 9 and no
new invitation penalty. Unlike the assessment's proposed bedtime-bounded window, the initial
window is an experimental fixed 18:00–20:00: a habit cannot make proposing a game impossible.
The body and ordinary bedtime reconsideration still govern actual participation.

No agreement guarantees an outcome. Starting together supports follow-through; an interrupted
activity can fail independently. A holder choosing other plans cannot turn that into evidence
against the partner. Disappointment reads the holder's frozen expectation and investment and
affects mood plus the hoped-for situation, never automatic rejection, whereabouts or blame.
Own observed minutes and whether the partner was seen remain in the outcome receipt. Gus's
ordinary whereabouts windows use only his existing expectation and local observation; they do
not reveal others' private agreements. A missed window can prompt deferred help, gated at a later
real encounter. The underlying help/game content remains generic.

Laya weighs the same eligible authored options and numeric reasons as the fallback at torn
choices, capped at two evaluations per person/day. Calls occur between minutes, answers apply
the next minute, and the engine reuses its original keyed draw. Complete requests fit 1,250
characters or fall back without a call; omitted whole recall texts are explicitly recorded.
The exact request/answer tape must reproduce a complete live recording before any export-only
projection. This trial isolates the evaluator and introduces no new generator/Haiku calls.

`livesEvenings` remains a separate experimental observer block, not a claimed full Lives 0.2
export. Same-choice history comparisons recompute only the fallback, never an alternative life
or hypothetical model response. The dedicated review filters detailed Lives export to the three
watched people after simulation; ordinary town output stays intact. Full observer integration,
learned routines, the complete commitment framework, richer activities and population transfer
remain future work. See [the implementation spec](under-glass/specs/lives-evenings-spec.md).


### Actual evening pilot in Life chains (2026-10-15)

Sid asked to follow the actual pilot histories through choices and outcomes in the main chain
viewer. A separate read-only runtime adapter accepts the version-1 evening receipts and their
supporting memory/company blocks. Its internal projection is not a claim that the proposed Lives
0.2 export is implemented. The illustrative 0.1 fixture remains supported and clearly labelled.

Links use recorded plan, choice, holder and cause identities. Personal outcomes stay separate from
world stages; a missed plan never reveals another person’s private choice or destination. Rewind
gates every record, citation and comparison. Keep-plan selections are intentions, with actual
completion/interruption/miss shown only when recorded. Expected chances and feeling changes use
recorded values; absent prior states stay unknown. Authored probability comparisons do not re-ask
Laya, and its weights are not presented as its reasoning.

Starting histories stay in expandable branches. Timeline markers show learning rather than a
duplicate learned-memory card. Unrelated routing and immediate-company choices are outside this
pilot adapter; supporting recalled experiences are retained. The trial report links directly to
Life chains at the relevant character and moment. No simulation behavior changes.


### 2026-10-15 — Preserve continued choices and expose incomplete opportunity chains

Sid observed that Penny's life stopped after her first evening in the chain viewer. The recording
contained ongoing company/routing choices that the narrow adapter omitted. The evening consumer
also stopped asking because overlapping expectation summaries occupied bounded recall, excluding
warmth/wishes even at eligible encounters. We now admit at most one estimate in the first recall
pass, filling remaining slots from other matched records and finally unused slots with estimates.
Ranking is valence-neutral; no positive result or invitation is guaranteed. Existing routines,
choice weights, sleep rules and needs bars remain unchanged.

Observer-only daily offer-gate counts diagnose silence without turning execution truth into a
character belief. Life chains now includes company/routing and personal outcomes with explicit
causal references. It groups unchanged whereabouts confirmations and hides automatic decay from
the primary timeline, while retaining their evidence for inspection. A route selection is not
labelled an arrival, an unavailable gate is not a private explanation, and older recordings are
never rewritten to imply the recall correction applied to them.

### 2026-10-15 — Author experimental lives and watch generation

Sid requested history authoring beyond the three-person pilot and real-time hybrid experiments.
The workbench covers the existing cast and keeps histories separate from reusable archetypes.
Editing canon creates an explicitly experimental draft account; it does not change source truth.
A new ExperimentalStated origin represents the author deliberately setting the character’s own
present attitude. It uses existing small seeds, while Authored interpretations still seed nothing.
Completed pasts remain recall-only here, requiring a separate present attitude rather than an
invented link. Canon admission rules, law 1, routines and sleep remain unchanged.

Each run freezes its inputs. Live viewing/pacing reads observer snapshots and changes only elapsed
wall time; it cannot change the keyed draws or model-answer order. Full snapshots are an initial
transport, not the final population-scale design. Haiku inspiration and Lives Laya remain separate
consumers; editing histories does not rewrite the character inspiration background.
