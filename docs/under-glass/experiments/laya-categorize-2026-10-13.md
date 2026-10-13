# Can Laya read a typed line? First probe, 2026-10-13

For the planned [player speech](../specs/player-speech-spec.md): the player types the Newcomer's
line and Laya reads it into one of the acts the engine offers, and a claim into one of the things
the listener knows of. Laya only weighs options; it writes nothing. This probe asks whether it can
read at all, before anything is built on it. It is a variant of the design's E4a, on authored lines
rather than Sid's own, and a diagnostic, not a pass mark.

## Setup

- **Laya:** loopback `laya-serve` 0.3.22 on the RTX 4070 Ti (CUDA), checkpoints `typed-decisions`
  (revision `7b928d82…`, what the game adapter uses) and `english` (from the local cache, as a
  control). No fallbacks, no truncation, no collapsed options in 3,956 requests; the largest
  question was 811 characters of Sid's 1,250 budget. Median request: 72 ms for fifteen rotations
  (v1 wording), 39 ms for six, 37 ms for a claim.
- **Packets** follow the game adapter: the line in `state` (`The newcomer says: "…"`), short
  instructions, slot labels, plain-English options. **Balanced:** every option sits in every slot
  once, normalised and averaged as the game does. Unlike the game, which sends one request per pass,
  the probe sends a line's rotations as separate questions in one request (Laya scores each question
  as its own sequence), so the budget applies per question. The base order is hashed per line, so
  "raw" (the first rotation alone) shows each kind in a varying slot.
- **Arms:** all 14 act kinds plus small talk (15 options; chance 7%) in two wordings (v1 short, v2
  saying what tells neighbours apart), and six options (chance 17%): the true kind, small talk
  (which the game always offers) and others to make six.
- **Lines**, each judged blind by three readers (Claude subagents with different framings, labels
  hidden behind opaque numbers):
  - the speech catalog's 359 act lines, labelled by their authors (readers agreed with 95%);
  - 234 player-style lines, 6 per kind for 13 kinds (no Welcomed: the player is the new one), in
    three typing styles (terse game chat, roleplay, plain) and three difficulties (explicit,
    implicit, hard), 78 per difficulty (readers agreed with 99%);
  - 45 small-talk lines (readers agreed with all).
- **Claims:** 64 cases: a listener, four things they know of plus "none of these", and a line the
  newcomer says; 22 direct mentions, 18 paraphrases, 12 word traps, 12 about nothing listed
  (readers agreed with all).

Tool: `sim/tools/laya_categorize_probe.py`; cases, receipts and results under ignored
`out/laya-probe-2026-10-13/` (a first run, before the review's corrections, is kept in its
`first-run/`); [summary JSON](laya-categorize-2026-10-13.json).

## Results

Top-1 accuracy, Balanced, `typed-decisions` (the `english` control in brackets):

| Lines | 15 options (v1) | 15 options (v2) | 6 options | Readers |
|---|---|---|---|---|
| Player-style (234) | **60%** (62%) | 62% (56%) | **76%** (76%) | 99% |
| Small talk (45) | 53% (58%) | 49% (71%) | 87% (84%) | 100% |
| Speech catalog (359) | 47% (47%) | 46% (44%) | 59% (61%) | 95% |

- **Difficulty**, player lines only (78 each), 15 options: explicit 86%, implicit 53%, hard 42%.
  With six options: 94%, 72%, 63%.
- **Top three** holds the right act for 79% of player lines with 15 options, 92% with six.
- **Tone**, whether hostile, friendly or small talk is right even when the act is wrong: 77% of
  player lines with 15 options.
- **By kind**, player and small-talk lines, 15 options: Complimented 18/18, Apologised 17/18,
  Argued 16/18, Thanked 16/18, PlayedGame 12/18, Comforted 11/18, HelpedSomeone 11/18, GaveGift
  10/18, TreatedToDrink 10/18, Joked 7/18, Mocked 7/18, StoodUpFor 5/18, Squabbled 1/18; small talk
  24/45.
- **Confusions**: small talk read as a welcome (14 of 45), standing up for someone read as arguing
  (9), squabbling as arguing (7), mocking as a compliment (5), a gift as a compliment (4), a joke as
  mocking (4).
- **The catalog's own lines are harder than a player's.** They are written in character and often
  indirect: no catalog joke was read as a joke (0/28), and few treats (3/24) or gifts (8/28) were.
- **Style:** roleplay lines were read best (68%), terse game chat (58%) and plain speech (55%) less.
- **Wording:** the longer v2 option descriptions made no consistent difference (two points better for
  `typed-decisions` on player lines, six worse for `english`).
- **Position:** with the options' order hashed per line, the raw first-rotation reading was as
  accurate as Balanced on average (player lines 60% either way), and answers spread over the fifteen
  slots fairly evenly (27 to 56 per slot, against 43 if even). A first run with one fixed order
  seemed to show a large position effect; that was the fixed order, not position.

**Confidence can be trusted with `typed-decisions`, not with `english`.** Player lines, 15 options,
by Laya's probability for its own answer; "top three below" is how often the top three hold the
right act on the lines below the line, which the player would be asked to confirm:

| Laya at least | `typed-decisions`: taken, right; top three below | `english`: taken, right; top three below |
|---|---|---|
| 0.5 | 63%, 79%; 63% | 82%, 71%; 51% |
| 0.6 | 51%, 87%; 64% | 71%, 76%; 59% |
| 0.7 | 40%, 94%; 69% | 61%, 81%; 60% |
| 0.8 | 27%, 97%; 73% | 54%, 84%; 64% |

With six options and `typed-decisions`, at 0.7: 29% taken and 99% right, and the top three hold
the right act for 90% of the rest.

**Claims** (64 cases): `typed-decisions` 83%, `english` 84%. Direct 91%, paraphrase 94%, word
traps 67% (`english` 75%), about nothing listed 67%. Of `typed-decisions`' 11 misses, four were
word traps pulling toward the wrong item, four were claims about nothing matched to something, two
were direct mentions read as about nothing, and one paraphrase went to the wrong item.

## What it means for the build

- **Offer only what is possible.** Six options instead of fifteen added 16 points on player lines,
  with small talk still offered. The spec offers only the acts the engine would allow toward that
  listener at that moment, usually far fewer than fifteen. Under today's rules that can include
  Welcomed for the Newcomer, worded as introducing oneself; small talk read as a welcome was the
  biggest single confusion, so that pair needs watching.
- **Use Laya's confidence.** With `typed-decisions`, take a reading at 0.7 or above (right 94% of
  the time); below it, ask the player to pick from Laya's top three. With fifteen options the top
  three hold the right act for 69% of those lines, with six for 90%, so offering fewer acts matters
  for the confirmation too. Misreading stays possible, as the design wants, but mostly where the
  line really is unclear.
- **Keep `typed-decisions`.** Accuracy is the same as `english`, and only its confidence is honest.
- **Keep Balanced** as the game does, for readings that do not depend on the options' order; this
  probe found no large position effect to correct.
- **Hostile kinds need care.** Mocking, squabbling and standing up for someone are the weakest;
  they are also the acts with the biggest consequences. Confirmation below the confidence line
  matters most there.
- **Claims are promising but need a guard both ways.** A third of claims about nothing were matched
  to something, and two plain mentions were read as about nothing: a minimum probability for any
  match, and confirming a match the player did not make explicit, keeps a vague line from
  fulfilling a need.

## Limits

- The player-style lines and the claim cases were written by Claude subagents, not by a player, and
  the readers are Claude subagents too: the readers' 95-100% is a ceiling for these lines, not
  human agreement. Sid's 50 notes (E4a) remain the real test.
- One authoring per style; 18 lines per kind; 64 claim cases. Differences of a few points between
  arms are within noise.
- The options were read with no scene: the game will also give Laya the listener and the moment,
  which may help or hurt.
- `typed-decisions` was trained on synthetic workflows, not on intent reading (the package's
  `laya/router.py` lists four); a fine-tune on Sid's labels (design E4a) is untried.
