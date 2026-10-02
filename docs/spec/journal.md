# 24. The player's bedtime journal

**Status: idea, stored for later** (Sid, 2026-10-02: "We can talk more about this later"). Not on
the roadmap's order yet; listed under "Later, not scheduled". Decided so far (Sid, 2026-10-02):
it is offered every night, it opens after the player says yes to bed with time stopped, entries are
about two sentences, and a page can be stolen (by chance, anonymously, with no warning beforehand)
when the player is knocked out in the mines or passes out, then pinned on the notice board.

Sid: *"During the transition it might be interesting to provide a journal for the player to write
in. It would be right before sleeping and game time would be paused. It would be skippable if they
don't want to write in it. We would also cap the length of the entry to be small."*

## The idea as given

- **Every night, as an option** (Sid, 2026-10-02). Never forced; one button (or key) skips it.
- **When:** the player answers "yes" to going to bed, time stops, then the journal prompt appears,
  before the night's transition.
- **Short:** about two sentences, nothing long (a cap around 200 characters; the exact number when
  the text box is chosen).

## Stolen pages (Sid, 2026-10-02)

*"Say you die in the mines and have to be brought back. You could get a page from your diary
stolen and posted on the bulletin board for characters to react to. I think the same thing could
happen when you pass out at 2am."*

- When the player is knocked out in the mines (or Skull Cavern) or passes out at 2:00 away from
  bed, a page of their journal can go missing, like the items and gold the game already takes then.
- The page turns up pinned on the town notice board ([notice-board.md](notice-board.md)), and
  villagers who pass by read it and react, exactly as they react to a note the player pinned on
  purpose: Laya picks each reader's reaction to the text, the reaction is a templated line, emote
  or memory, never generated text.
- This ties the journal to the board: the board is how a private page becomes town news, and it is
  the reason the journal matters to the world without the mod reading the player's diary itself.

Decided (Sid, 2026-10-02):
- **Which page:** a random one to start (seeded, deterministic). Later, possibly Laya chooses the
  entry, as a typed `choice` over recent entries ("which page would cause the most talk?"); the
  model still writes nothing.
- **No warning beforehand.** It is part of the surprise.
- **Not guaranteed:** a chance on each knock-out or pass-out (seeded), not every time.
- **The player is told only after it happens:** for example a line on waking, "A page is missing
  from your journal." (wording with the other lines, [text.md](text.md)).
- **No villager is blamed.** The page simply turns up on the board; there is no thief character.

Still open:
- The chance per knock-out, and whether it grows with how many pages are written.
- Only pages written since the last stolen one, or any page?
- Can the player take the page down from the board, and does that change how readers react?

## How it would fit the rules (first thoughts)

- **The model never writes text** (AGENTS.md rule 6). The journal is the player's own free text,
  like the player's notes on the notice board ([notice-board.md](notice-board.md)): the model may
  only read it as data in a state and answer typed questions about it.
- **Shadow first** (AGENTS.md rule 1): whatever the journal feeds would log `[shadow]` lines before
  it changes anything a villager does.
- **Saved per save**, as a new additive save-data key ([persistence.md](persistence.md)).
- Entries pass through the same character filter as other text (`LineSanitizer`) before they are
  stored or shown.

## Open questions (for Sid)

- **What is it for?** Options to discuss:
  1. a keepsake only: the player's own record of the year, readable later (a menu page, or the
     viewer);
  2. a signal for the mod: what the player cared about today (someone they want to see, something
     they regret) nudges the overnight plan or tomorrow's motives, read by Laya as typed questions
     ("does the player want to see <npc> tomorrow?");
  3. both.
- **Do villagers ever know?** A journal is private, so the natural answer is never directly; only
  through the player's own actions. Option 2 would let it shape the world without anyone "reading"
  it.
- **Length cap:** a tweet-sized line (about 140 characters) or a few short lines?
- **Passing out at 2:00** has no "yes, go to bed": offer the journal the next morning, or skip it
  that night (its page may be stolen instead, above)?

## To verify in the game's code (for DeepSeek)

- Where to open a menu right after the player answers yes to the sleep question (the bed's
  question dialogue and its answer handler) and before the night's fade starts, so no game state
  has moved yet; whether a menu opened there pauses time in single-player, and what it does in
  multiplayer ([multiplayer-compat.md](multiplayer-compat.md)), where sleeping waits for everyone.
- Whether `DayEnding` is too late for a menu.
- The hooks for being knocked out in the mines or Skull Cavern and for passing out at 2:00 (what
  the game takes, and an event or postfix that fires once), so a stolen page can be decided then.
- A text-entry menu the game already has that can be reused (as the naming screens do), and its
  length limit.
