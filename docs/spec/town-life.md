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

- `NpcBond(A, B)`: how two NPCs feel about each other, 0..1, seeded from `FriendsAndFamily`
  (household = family 0.8, listed = friend 0.5, else 0.1) and a small `data/bonds.json` for
  tensions; grows a little each time they chat. Saved per pair (sparse).
- Diary kinds `ChattedWith` (subject: the other NPC; news weight 1, or 2 if the player saw it),
  `MetUpWith`, `LookedFor`.

## Opinions and the off-screen town (designed with Sid, 2026-10-01)

- **Opinions are motives pointed at NPCs** ([motives.md](motives.md)): a view over what A has
  witnessed B do — the `Saw` entries about B, the shared `Festival`, a `ChattedWith` — run through
  the same stressor profiles, with temperament compatibility folding in as a small elastic term.
  No new saved state. Characters hold different views of each other for free: Sam and Shane
  disagree about everyone because they saw different things and feel them differently.
- **Characters get upset with each other.** A snub, a missed meet-up, an argument are the same
  `Hurt` machinery as with the player: repeated small strains cross the yield point and become
  plastic — a cooling bond, a sharper greeting, a grudge between NPCs that a later apology or a
  good scene can mend. Shadow: `[shadow] Sam is cooling on Shane (snub 0.42, argument yesterday)`.
  How it looks in-game (a sharper line, "angry chat bubbles fly") is a later, live rendering
  question.
- **The off-screen sim.** The game freezes NPCs outside the player's location, but the mod already
  knows every villager's schedule (the same extraction the family priors use,
  [routines.md](routines.md)). At `DayEnding`, a deterministic pass **simulates the day for the
  town it never saw**: from the schedules, who was co-located with whom each hour; who chatted
  (`ChattedWith`, using the ambient-gossip rules and magnitude), who argued (bond tensions +
  temperament clash draw), who met up; writes the diary entries and moves opinions, bonds and
  rumours. Cheap (schedule lookups + FNV draws), off the render path, and the player meets a town
  with a history they did not author: someone is grumpy about a thing that happened while the
  player was in the mines, and the player cannot tell whether it was scripted.
- **The day digest** ([debug-tools.md](debug-tools.md)): the viewer shows "while you were away"
  — today's unseen chats, arguments, opinion shifts and how far the player's own news travelled.
- **Texture stays subordinate to consequence.** Most of what the sim produces is small; the
  player's own actions are the biggest stressors, so the town echoes the player more than it
  soaps itself. (Sid: the world should feel alive *and* like the player matters.)

## Triggers and game hooks

- Each tick after `Observe`, a `TownLife.Tick` (game thread, memory only) proposes scenes for pairs
  co-located this tick: chat if bond >= 0.4 and the pair hasn't chatted today.
- The off-screen pass runs once at `DayEnding`, before the overnight plan job, in the same
  `[shadow]` style; it touches memory only.
- A scene is **rendered** only if the player is in that location: bubbles via `showTextAboveHead`
  (the text from `i18n/`, [text.md](text.md)). Otherwise it's recorded silently.
- Meet-ups and looking-for need movement: live only after the travel spike ([roadmap.md](roadmap.md),
  step 11), behind `Live.TownLife`.

## Laya questions

| Question | Type | Fallback |
|---|---|---|
| "Would <A> go and meet <B> now?" (meet-ups only, at most a few a day, batched overnight into a plan for tomorrow) | `noul` | 0.5 |

Chats, frictions and the off-screen pass are deterministic (bond, temperament, seeded draws): they
happen every tick for many pairs and must stay cheap and on the game thread.

## Deterministic rules

- At most `MaxScenesPerDay` (6) rendered scenes a day in total, at most 1 per NPC; silent scenes
  aren't capped (they're cheap), except one chat per pair per day.
- Bubble lines only quote facts the speaker actually has (a gossip entry); otherwise small talk from
  the line bank.
- The off-screen pass iterates NPCs in name order with a per-(save, date, pair) FNV seed; the same
  save replays the same day.
- Never during events, festivals or when either NPC is in a scripted path.

## Tuning constants

`MaxScenesPerDay` 6, bond seeds above, chat bond threshold 0.4, `ChatBondGain` 0.02,
`ArgueChancePerMeeting` 0.02 (small: vanilla characters rarely fight), opinion plasticity per the
stressor table ([motives.md](motives.md)).

## Acceptance tests

- Scene proposal is deterministic; caps hold; bubbles only when the player is present (a fake
  "player location" in tests).
- A chat that quotes gossip only uses entries the speaker has.
- The off-screen pass: two schedules that share a morning at the Saloon produce a `ChattedWith`
  with the tuned chance; a tension pair with a clash draw writes an argument and a plastic
  opinion shift; the same seed replays the same entries.
- Two fixture diaries about a third NPC give A and B different opinions of them ([motives.md](motives.md)).
- In-game: stand in the Saloon on a Friday evening; one or two chats appear as bubbles; diary entries
  are written for both; the viewer's day digest lists what happened off-screen.

## Status

Not started. Builds on ambient gossip ([ledger-gossip.md](ledger-gossip.md)), motives
([motives.md](motives.md)) and the travel spike.

## Open questions

- Which tensions (if any) belong in `data/bonds.json`? Recommendation: keep it tiny and mild;
  vanilla characters rarely fight.
- **The town message board** (Sid's idea, to think about): characters get the impulse to post
  there, and posts receive likes and dislikes from other villagers. It would ride on the
  off-screen sim (a post is a diary entry with magnitude, the reactions are opinion nudges), and
  the vanilla bulletin board only shows the player's own quests, so a real board would need its
  own rendering — a later, live feature. Keep the data shape in mind: a `PostedToBoard` diary
  kind is all the sim needs to hold it up.
- How the player meets the off-screen history: the digest is for development; in-game, lines that
  cite an off-screen event ("I heard about your quest from Jodi") are the payoff, and they work
  through existing `Heard` lines. No new UI in v1.
