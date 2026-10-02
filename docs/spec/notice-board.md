# 23. The town notice board

**Status: idea, not scheduled.** Sid, 2026-10-02. Stored for later; nothing here is decided beyond
the shape below.

Sid: *"One of Stardew Valley's story and world aspects is unplugging from technology. Instead of a
digital board we could add a new board in the downtown square that people can leave notes on. To
leave a note a character has to walk to the board, potentially sparking more interaction from
crossed paths. We could type and leave notes as well; the characters don't need to understand the
words. We would make a request to Laya giving it our note and having it choose how the person
would react to it. This might be the real draw for the mod."*

It replaces the "town message board" idea in [town-life.md](town-life.md), "Open questions": a
physical board in the square, not a digital feed.

## Player-visible behavior

- **A cork board in Pelican Town's square**, near the fountain and Pierre's (exact tile **verify**
  against the map). Interacting with it opens a page of pinned notes: who wrote each, and when.
- **Villagers post notes.** A villager with something to say to the town (news, a request, an
  invitation, a complaint) walks to the board and pins a note. The walk is the point: they cross
  paths with whoever is around, so posting a note is also a chance meeting. Notes come down after
  a few days.
- **The player can post too.** The player types a note in their own words and pins it.
- **Villagers react to the player's note.** Whoever passes the board in the next days reads it.
  Each reader reacts in character: amused, touched, annoyed, curious, indifferent. The reaction
  shows as an emote at the board, a line the next time the player talks to them, sometimes a reply
  note pinned beside it, and a stress in their memory like any other event
  ([motives.md](motives.md)).
- **Villagers react to each other's notes** the same way, so the board carries small town
  conversations the player can read over a few days.

## How it fits the rules

- **The model still never writes text** (AGENTS.md rule 6). Villagers' notes and replies are
  templated lines in their voice ([text.md](text.md)). The player's note is the only free text, and
  the model only **reads** it: it goes into the state as data, and Laya answers a typed question.
- **Laya question:** "how would <npc> react to this note?", a `choice` over reaction kinds (for
  example: amused, touched, curious, annoyed, offended, indifferent). State: the NPC card, the
  note's text, who wrote it, and the reader's regard for the writer. Fallback: indifferent. The
  reaction becomes a stress row (`ReadNote`, with the reaction as its emotion) and, for strong
  reactions, a reply note or a planned line.
- **The player's text is sanitized and capped** before it reaches the state: a length limit that
  fits the 512-token budget ([laya.md](laya.md)), the same character stripping as lines, and it is
  framed as a quoted note so it can't read as instructions.
- **Walking to the board** is a one-day schedule stop, like meet-ups ([vanilla-sources.md](vanilla-sources.md)):
  the square is in town, inside the game's normal NPC routing, so no travel spike is needed.
- **Shadow first:** a villager "would post" a note, and "would react" to the player's note, as
  `[shadow]` lines and viewer entries, before anything appears in the game. The board itself (a
  new object on the map, a menu, a text box) is a live feature behind its own switch, `NoticeBoard`.

## Data model (sketch)

- `Note(Id, Author, Text or TemplateKey, PostedTick, ExpiresTick, ReplyTo?)`, saved under a new
  additive save-data key `board` ([persistence.md](persistence.md)). Player notes store their text;
  villager notes store a template key and its tokens.
- Diary kinds: `PostedNote` (the author), `ReadNote` (the reader; `reaction`, `author`), with rows
  in the stressor table. A note's juiciness feeds gossip ([ledger-gossip.md](ledger-gossip.md)):
  "did you see what the farmer pinned up?"
- A new motive source: wanting to tell the whole town something makes the board one of the acts a
  motive can choose, with its own boldness cost (lower than walking up to someone; a shy villager
  might post where they would never speak up).

## Things to verify before building

- **Placing the board:** adding an object and a tile action to the town map without breaking other
  mods that edit `Maps/Town` (a map patch through `AssetRequested`, or a placed furniture or
  big-craftable object). Check the 1.6 map-edit API and common compatibility practice.
- **The menu:** a read view for pinned notes and a text box for the player's note. The game has a
  text-entry component (used for naming animals and the farm); confirm what it offers and how it
  handles controllers.
- **Who reads the board:** villagers whose day already passes the square read it for free; others
  need a stop added. Decide how often reading happens, so it stays a small daily ritual, not a
  crowd.

## Open questions

- How many notes can be up at once, and for how long? A board with 3 to 6 notes, each up for 3 to 5
  days, keeps it readable.
- Should villagers ever take a note down, or answer one in person ("I saw your note about the
  missing cat")? Probably both, later.
- Does the player get a hint when someone reacted, or only discover it by reading the board and
  talking to people? Discovering it fits the game better.
- Order of work: this needs one-day schedules (step 19), the motives stress table (step 14) and a
  live switch, so it comes after those. Its shadow half, Laya reacting to typed notes, could be
  tried earlier from the console as an experiment, to see whether the reactions feel right.
