# 24. The player's bedtime journal

**Status: idea, stored for later** (Sid, 2026-10-02: "We can talk more about this later"). Not on
the roadmap's order yet; listed under "Later, not scheduled". Nothing below is decided; it records
the idea as Sid gave it and the questions to settle when it is picked up.

Sid: *"During the transition it might be interesting to provide a journal for the player to write
in. It would be right before sleeping and game time would be paused. It would be skippable if they
don't want to write in it. We would also cap the length of the entry to be small."*

## The idea as given

- **When:** at the end of the day, right before sleeping, during the transition to the next day.
- **Time is paused** while the journal is open.
- **Skippable:** one button (or key) closes it without writing, every night.
- **Short:** the entry has a small length cap.

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
- **Every night, or only some nights** (after a notable day, or once a week), so it doesn't become a
  chore even with a skip button?
- **Where exactly it opens** (verify in the decompile): when the player confirms "Go to bed?"
  (the sleep question's answer), before the day-end fade, so no game state has moved yet; or at
  `DayEnding`, which may be too late for a menu. Whether time really stops with a menu open in
  single-player, and what happens in multiplayer ([multiplayer-compat.md](multiplayer-compat.md)).
- **Passing out at 2:00** or sleeping elsewhere: skip the journal, or offer it the next morning?
