# Invitations: asking for company, and a real answer

## Status

Built as an opt-in slice, 2026-10-13, the first part of the agreed order's step 3 (broader actions
with accept and decline as real outcomes). Switch `Invitations` (`--catalog invitations`), off by
default and only with `Company` on; with it off, every run is byte-identical to before.

Before this slice a game or a drink was one-sided: the one who started it simply made the other
busy for its length (`Begin`, "S1: the other party takes part"). Now the other person is asked
first and answers, through the general `Ask` that batch 2 seamed and only the apology used
(`AskKind.Invitation`, `Simulation.Ask.cs`).

Not built: invitations for later (batch 2's `Invited` with a `Promise(Date)`, `MetUp`, `StoodUp`),
parties, inviting through reflection, Laya deciding close calls (`FeelingOptions.CloseCall` is not
consulted by `Ask`), and invitations the player types (the [player speech spec](player-speech-spec.md)).

## The rule

When a game (`PlayedGame`) or a drink (`TreatedToDrink`) is about to begin toward a person, by any
route that is not injected (the gate's motives or the per-head draws), the person is asked first:

- `Ask(asked, asker, Invitation)`: obliged = 0.5 x regard for the asker (if positive) + 0.3 x
  familiarity + 0.2 x chattiness, + 0.2 if fond of the asker, + `InviteKinBond` (0.2) for family or
  a shared home; against cost = 0.15 + dislike x (0.5 + retention) + 0.3 x withdrawal + the asked
  person's state (below). A margin beyond the close-call band decides; inside it, a draw keyed by
  what was asked, the act's id and the asked person, tilted by mood. The log line is
  `ask {asked} Invitation {asker} act {id} yes|no margin {m}`.
- **Yes:** the game or drink begins exactly as before, with the asker as actor, and the asked
  person's acceptance is recorded beside it.
- **No:** instead of the game or drink, the asked person begins `Refused` toward the asker:
  "Penny turned down Sam's invitation". It is batch 2's `Refused` row (acts-batch2 spec 3.2) as far
  as its numbers go: trivia (juiciness 1.0, valence -1), one minute, where the asking happened. It
  has no gate, so unlike batch 2's plan it is not a light act and counts toward no light-act cap.
  The asker takes part: they know who said no, however little they know them, and feel it as a small
  slight (-0.1, plastic 0.15) toward that person. (A given deed done to someone face to face, `TargetIs.Given`
  with the target as patient and within reach, now makes them a participant the way a chosen one
  does; one done about someone out of reach does not. `Refused` is the only such row in the town.) Anyone else in sight sees it as they see any act and remembers it; at trivia
  1.0 it is never volunteered in gossip.
- **Not again for a while:** after a refusal the asker does not invite the same person to either
  company act for `InviteAgainDays` (7, batch 2's `AskAgainDays` for invitations).

Injected acts (scenarios and tests that place a game) are not asked: they are the fixture's word.
The motive or draw that led to the asking is spent either way, as the gate's slot is: asking was
the act.

## What the answer weighs besides regard

Sid (2026-10-13): "refusals should be more likely to happen when someone is tired, busy or low.
This should also be affected by the 2 persons relationship with each other. Someone eager to please
will more likely do something when their feeling low. Penny doing things for her mom would be a
good example of this."

- **The asked person's state** (`AskState`, their own, never anyone else's): tired (the body's own
  tiredness, the measure that sends them to bed, 0 to 1), due at work (they leave for work within
  `InviteBusyMinutes`, 60), and low (how far their mood is below even). Each costs up to its weight
  (`InviteTiredCost`, `InviteBusyCost`, `InviteLowCost`, 0.3 each).
- **The bond** with the asker (`AskMath.Bond`): whole for family or a shared home, otherwise half
  their liking and half their familiarity.
- **Eagerness to please** (`AskMath.Eagerness`): low self-regard and low boldness, equally. Penny
  0.75, Harvey 0.65, Shane and Sebastian 0.6, the Newcomer 0.55, Pam, Haley and Leah 0.5, Abigail and
  Emily 0.4. It is read from existing traits, so it is an approximation to check in scenes (VERIFY):
  low self-regard also lifts Shane and Sebastian.
- **Together** (`AskMath.StateCost`): the state's cost is shrunk by eagerness times the bond. Penny,
  tired and low, asked by her mother keeps a quarter of it; asked by a stranger, nearly all of it.
  Someone not eager to please finds family no easier on a bad day, though family still adds to the
  obligation.

Across six seeds of 28 days (the shipped town with returns, company, welcome, repair, sides and
invitations on; 65 or 66 invitations in all), the share of noes by the state's weight:

| Weight | Noes |
|---|---|
| 0 (before) | 9% |
| 0.3 (default) | 30% |
| 0.5 | 49% |
| 0.8 | 60% |

Tiredness carries almost all of it. Company happens in the evening, so nobody is due at work, and
in these runs nobody's mood fell below even by more than 0.05: the shipped town's moods sit mostly
above even (the speech library heard no low mood in a month). Busy and low will matter more for
asks by day (favours, step 4) and in towns where moods fall. Invitations within a family are rare
(one in the six runs), because the gate does not aim acts at kin or housemates; the Penny-for-her-
mother case is mostly a favour, and the rule is written in `AskMath` for that slice to use.

## First measurements

Seed 7, the observer pack's flags (`--feel on --desire on --catalog
returns,company,welcome,repair,sides,late,invitations --reflection authored --reflection-chance 0.35
--dream-chance 0.15 --character-inspiration sim/character-inspiration.json --speech`):

- seven days: 8 invitations, 5 accepted; with the switch off the same week's log hash is unchanged
  (`aa8959477b340cee`);
- 28 days: 22 invitations among 16 pairs from 13 people, 19 accepted (86%); before the asked
  person's state counted, the same month had 20 accepted, and both refusals were close calls
  involving the barely known Newcomer.

Seed 7's month is a light one for refusals; across six seeds the default weights give 30% (above).
Batch 2's 50-80% accepted (b2-2) was written for dates, and is a diagnostic here, not a target.

## Speech

With `--speech`, both sides speak:

- On a yes, the asker's line is the company act's own (the library's game and drink lines are
  already phrased as offers), and the asked person's reply is a `PlayedGame.Accepted` or
  `TreatedToDrink.Accepted` line, recorded on the same act with `Reply` set.
- On a no, the `Refused` act carries the asker's invitation line (from the company kind, spoken by
  the asker) and then the refusal, a `PlayedGame.Refused` or `TreatedToDrink.Refused` line spoken by
  the one who said no, with `Reply` set.

Reply lines are chosen like every line (the speaker's archetype, relationship and mood bands, the
neighbouring-band rule, the speaker's recent lines avoided) on speech's own stream. The reply lines
were drafted one author per archetype plus a plain author, copy-checked against the dialogue corpus
and judged blind by three readers per line, with the speech library's rules
([speech spec](speech-spec.md), "Curation rules"); they await Sid's curation like the rest.

## Recording and the viewer

- `invitations` in the recording lists every asking: tick, asker, asked, the company kind, yes or
  no, the margin, the act that followed (the game or drink, or the refusal), and why: the asked
  person's tired, busy and low, their bond with the asker and their eagerness to please. The
  viewer's act detail says it in words ("George said no (tired, barely knows them)").
- `Speech` gains `Reply` (appended, default false); the viewer shows a reply after the line it
  answers, in Stories, the act detail ("Answered"), the timeline hover, Happening now and the
  selected person's moment.
- The viewer words `Refused` as "turned down {asker}'s invitation"; reflection's memory verb is
  "turned down".

## Tests

`InvitationSceneTests` (seed 7, the shipped town with returns, company, welcome, repair and sides
on, 28 days, unless a bullet says otherwise):

- the switch needs Company, adds only the `Refused` row, and is named for the runner;
- with it off (seven days), nothing is asked, no refusal happens, and company still happens unasked;
- every invitation's yes is the company act by the asker toward the asked at that minute, and every
  no is `Refused` by the asked toward the asker with no company act between them then; each is
  logged as `ask … Invitation … act … yes|no`; there are as many refusals as noes;
- the asker of every no knows who turned them down (a witnessed belief naming them) and their
  regard for that person falls; a refusal is never recorded as the asker's own gate act;
- after a no, the asker does not invite the same person again within `InviteAgainDays`: a
  two-person room where every asking is a clear no, for ten days, run with the week (asked again
  only after seven days) and with a one-day wait (asked again within the week), so the test fails
  without the rule;
- with speech on, each invitation's act carries two lines, the asker's offer of that company kind
  and the asked person's `.Accepted` or `.Refused` reply, marked `Reply`; no other line is a reply;
- seed 11 for seven days gives the same invitations and lines twice, and speech on or off gives
  the same log;
- with the state's weights at 0.5, seed 3 for 28 days has more noes than with them at 0, the noes
  were more tired on average than the yeses, and each invitation records its state, bond and
  eagerness (`AskMathTests` pins the maths: Penny's eagerness, the bond, and that her mother asking
  costs her a quarter of what a stranger asking does);
- an injected game (a scenario, in a two-person room for one day) is not asked;
- a seven-day recording carries `invitations` (with its camelCase fields) and `reply` on speech rows
  only with the switch on.

The viewer's Node test `test-viewer-speech.cjs` holds that an answer appears after its line, only
once the clock reaches it, and never for an older recording.

## Next

Inviting through reflection (company as a response choice), Laya on close calls, invitations for
later with a kept or broken appointment (batch 2's dates), and the player's typed invitation.
