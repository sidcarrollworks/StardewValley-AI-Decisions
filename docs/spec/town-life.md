# 20. Town life: NPCs dealing with each other

**Status: not started** (gossip in memory is partly done; the design below is agreed with Sid on
2026-10-01). The town should feel alive when the player isn't involved: friends meeting up, small
frictions, NPCs looking for each other — and, beyond scenes, an off-screen social life that
advances every day whether or not the player witnesses it. Today NPCs only pass knowledge along
silently ([ledger-gossip.md](ledger-gossip.md)).

## Player-visible behavior

Rare, small scenes the player may stumble on, only when the player is in the same location (the
brief: "simulate silently off-screen, render text only when the player is in the location"):
- **Chatting:** two friends co-located for a while exchange short bubbles ("Did you hear about the
  farmer's cauliflower?"). The bubbles quote real gossip from their memory ([ledger-gossip.md](ledger-gossip.md)).
- **Meeting up:** a character walks over to a friend's usual spot at a free hour and they stand
  together for a bit (same travel pattern as visits, [find.md](find.md)).
- **Looking for someone:** a parent goes looking for a child at dinner time, asking others on the way
  (the NPC-subject Find, [find.md](find.md)).
- **Frictions:** two characters with a known tension (a small table: e.g. rivals) exchange a
  sharper line; nothing else changes.
- Each scene becomes a diary entry for both, so later lines can mention it ("I ran into Leah at the
  river; she says hi").

Off-screen, the same things happen in memory only (diary and ledger), with no movement or text.

## Data model

- **Regard** replaces the planned `NpcBond` (2026-10-01): `Regard(A, B)` is directional (A's view
  of B), signed -1..1, saved sparse in `MemoryStore`, and is the same record the motives keep for the
  player ([motives.md](motives.md), "Regard"). Seeded on first load from `FriendsAndFamily`
  (household = family 0.8, listed = friend 0.5, else none), then from the game's own content: how
  each villager's dialogue talks about the others and who appears together in heart events, mined
  offline into a reviewed fixture (`fixtures/game/regard/`, [vanilla-sources.md](vanilla-sources.md));
  plus a small `data/bonds.json` for tensions (negative seeds) and hand overrides. Moved afterward by
  the plastic share of what A feels about B.
- Diary kinds `ChattedWith` (subject: the other NPC; news weight 1, or 2 if the player saw it),
  `Argued`, `MetUpWith`, `LookedFor`.

## Opinions and the off-screen town (designed with Sid, 2026-10-01)

- **Opinions are the motives' physics pointed at NPCs** ([motives.md](motives.md)): what A has
  seen B do, and heard about B, runs through the same stressor profiles. The elastic part is
  recomputed from A's recent diary; the plastic part is `Regard(A, B)`, saved, so an old falling-out
  survives the diary being trimmed. Characters hold different views of each other because they saw
  different things, feel them differently (sensitivity) and keep them differently (retention): Sam
  and Shane disagree about people.
- **Temperament compatibility** is a small elastic term each time they share a span: two
  anger-leaning characters grate a little, two warm ones warm a little.
- **Characters get upset with each other.** A snub, a missed meet-up or an argument is the same
  `Hurt` machinery as with the player: repeated small strains yield into regard, giving a cooling
  bond and a sharper greeting that a later good scene can mend. A hostile act between NPCs needs the
  same extra boldness as toward the player. Shadow: `[shadow] Sam is cooling on Shane (regard
  0.40 -> 0.31: argued at the Saloon; snubbed twice)`. How it looks in-game (a sharper bubble) is a
  later, live rendering question.
- **Social events on the live spans.** Off-screen NPCs move every tick on the host
  (`stardew-source-notes.md`, "NPCs, schedules, movement": `Game1.UpdateLocations` updates every
  location), and `CollectPresences` already reads every location in `Game1.locations`, so the
  town's real day is already observed; nothing is replayed from schedules. (The 2026-10-01 morning
  draft's day-end schedule replay assumed the game freezes off-screen NPCs; it doesn't, and a
  replay would double-count the day and miss rain schedules, festivals and marriages.) When two
  NPCs' co-located span ends (`Observe` already tracks spans), `TownLife` draws, deterministically,
  whether they chatted (the ambient-gossip draw, [ledger-gossip.md](ledger-gossip.md)) and, rarely,
  whether they argued (`ArgueChance` x tension, from negative regard and anger biases); it writes
  the diary entries for both and the stresses follow. One chat and one argument per pair per day.
- **What this doesn't see**, accepted for now: farm building interiors (the spouse and children;
  not in `Game1.locations`), festivals (the clock stops and the actors are clones), and the rest of
  the evening when the player sleeps early.
- **The day digest** ([debug-tools.md](debug-tools.md)): the viewer shows "while you were away",
  built at `DayEnding` from the day's diary: the chats and arguments the player didn't see, regard
  changes, and how far the player's own news travelled. It only summarizes; it simulates nothing.
- **Texture stays subordinate to consequence.** Most of what happens is small; the player's own
  actions are the biggest stresses, so the town echoes the player more than it makes its own drama.
  (Sid: the world should feel alive *and* like the player matters.)

## Triggers and game hooks

- Each tick after `Observe`, a `TownLife.Tick` (game thread, memory only) settles the spans that
  ended this tick (chat, argument) and proposes rendered scenes for pairs co-located now when the
  player is there: chat if regard >= 0.4 and the pair hasn't chatted today.
- The day digest is built at `DayEnding`, before the overnight plan job, from memory only.
- A scene is **rendered** only if the player is in that location: bubbles via `showTextAboveHead`
  (the text from `i18n/`, [text.md](text.md)). Otherwise it's recorded silently.
- Meet-ups are planned overnight and use a schedule for that one day, like farm visits
  ([invitations.md](invitations.md), [vanilla-sources.md](vanilla-sources.md)): the game paths both
  NPCs to the meeting place, so planned meet-ups don't wait on the travel spike. Same-day
  looking-for still does ([roadmap.md](roadmap.md), step 11). Behind `Live.TownLife`.

## Laya questions

| Question | Type | Fallback |
|---|---|---|
| "Would <A> go and meet <B> now?" (meet-ups only, at most a few a day, batched overnight into a plan for tomorrow) | `noul` | 0.5 |

Chats and arguments are deterministic (regard, temperament, seeded draws): they happen every tick
for many pairs and must stay cheap and on the game thread.

## Deterministic rules

- At most `MaxScenesPerDay` (6) rendered scenes a day in total, at most 1 per NPC; silent scenes
  aren't capped (they're cheap), except one chat per pair per day.
- Bubble lines only quote facts the speaker actually has (a gossip entry); otherwise small talk from
  the line bank.
- Span settling iterates pairs in name order with a per-(save, pair, span start) FNV seed; the
  same save replays the same day.
- Never during events, festivals or when either NPC is in a scripted path.

## Tuning constants

`MaxScenesPerDay` 6, regard seeds above, rendered-chat regard threshold 0.4, `ArgueChance` 0.02
per span at full tension (small: vanilla characters rarely fight), stresses per the stressor table
([motives.md](motives.md)).

## Acceptance tests

- Scene proposal is deterministic; caps hold; bubbles only when the player is present (a fake
  "player location" in tests).
- A chat that quotes gossip only uses entries the speaker has.
- Span settling: a fixture span of two NPCs at the Saloon produces a `ChattedWith` with the tuned
  chance; a tension pair writes an `Argued` and moves regard; the same seed replays the same entries.
- Two fixture diaries about a third NPC give A and B different opinions of them; repeated snubs
  move regard and a week of good spans recovers some of it ([motives.md](motives.md)).
- An old save gains seeded regard for `FriendsAndFamily` pairs exactly once.
- In-game: stand in the Saloon on a Friday evening; one or two chats appear as bubbles; diary entries
  are written for both; the viewer's day digest lists what happened off-screen.

## Status

Not started. Builds on ambient gossip ([ledger-gossip.md](ledger-gossip.md)), motives
([motives.md](motives.md)) and the travel spike.

## Open questions

- Which tensions (if any) belong in `data/bonds.json`? Recommendation: keep it tiny and mild;
  vanilla characters rarely fight.
- **The town message board** (Sid's idea, to think about): characters get the impulse to post
  there, and posts receive likes and dislikes from other villagers. It would ride on the same
  machinery (a post is a diary entry with juiciness, the reactions are elastic stresses), and
  the vanilla bulletin board only shows the player's own quests, so a real board would need its
  own rendering — a later, live feature. Keep the data shape in mind: a `PostedToBoard` diary
  kind is all the sim needs to hold it up.
- How the player meets the off-screen history: the digest is for development; in-game, lines that
  cite an unseen event ("I heard about your quest from Jodi") are the payoff, and they work
  through existing `Heard` lines. No new UI in v1.
