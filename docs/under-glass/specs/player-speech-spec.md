# Player speech: the Newcomer's words, how the town reads them, and what a claim is worth

## Status

Planned, 2026-10-13. Only the first step is built: a probe that measures whether Laya can read a
typed line (`sim/tools/laya_categorize_probe.py`, offline tests in
`tests/test_laya_categorize_probe.py`), with its first measurements in
[the probe write-up](../experiments/laya-categorize-2026-10-13.md). Everything else below is a
plan, written down before it is built so the pieces have names.

Sid's decisions:

- (2026-10-12) "The newcomer should get a voice of it's own. Ideally players get to determine how
  the character sounds. I'd like them to be able to type and have laya categorize the response."
- (2026-10-13) "Words aren't facts and that's why the actual game state tracking will be important
  for context. Characters will need the ability to verify claims." His example: the player says
  "I brought you the fish you wanted", and depending on the character the answer is "Okay, let's
  see the fish", "Give it to me" or "Oh! Thank you so much, I have something special planned."

These extend the design's existing decision (design 5a, Sid, 2026-10-05) that the player writes
free text, that villagers can misread it, and that Laya turns the words into a typed payload
rather than writing anything. They set where the agreed order is heading; they do not reorder it.
Player speech needs step 3 (asks with real accept and decline, so a typed invitation has something
to become) and step 4 (needs with real items, so a claim has something to be about). The roadmap's
text REPL moves from "later" to the end of that order (roadmap, "The agreed order", amended
2026-10-13).

Not built: the Newcomer's own voice, any player input to a run, Laya readings in the simulation,
claims, verification, per-pair trust, lie exposure, and the viewer for any of it.

## What it adds

- **The Newcomer is the player's character.** Its voice is whatever the player types, not one of
  the character pack's archetypes. The pack already leaves the Newcomer out
  (`CharacterInspirationPackTests`), so nothing has to be undone.
- **Typed words in, typed meaning out.** The player's line goes to Laya with a small set of
  options the game offers; Laya weighs them; the engine does the rest. Laya never writes text, and
  every reply a villager speaks is authored (D48).
- **Words are not facts.** A line can assert anything. What it changes depends on what the
  listener knows, how far they trust the speaker, and what they can check, never on the world's
  truth directly (design rule 2: decisions read memory only).

## Scope and build order

0. **The probe** (built). Can Laya tell what a line is doing, and which known thing a claim refers
   to? Measured on the speech catalog's labelled lines, player-style lines, small talk and claim
   cases, each judged blind first. This is a variant of the design's E4a; it is a diagnostic, not
   a pass quota (roadmap, "What must hold, and what we measure").
1. **The invitation** (agreed order step 3). Asking for company is an `Ask` (`AskKind.Invitation`,
   already scored in `Simulation.Ask.cs`) that the asked person answers. Yes starts the game or the
   drink; no is a refusal the asker feels. The asked person's reply line is authored and keyed to
   the recorded answer. This is the first place a villager answers rather than only being acted on.
2. **A need with a real item** (agreed order step 4). One person needs one named thing; someone is
   asked; an agreement records who undertook what for whom by when; the thing is bought, held and
   handed over, or the promise breaks. One held-item record per open promise is enough; no general
   inventory.
3. **A placeholder Newcomer voice.** For runs without a player, a reserved voice `newcomer` with
   arrival-side lines (introducing oneself rather than welcoming), so the Newcomer stops saying
   "Welcome to town" to the people who live there.
4. **Scripted player lines.** A file of what the Newcomer says, to whom, and when, read by Laya,
   run through ordinary eligibility and recorded as player speech. Batch runs and exact replays
   come before live typing.
5. **Claims and checking**, on top of 2.
6. **Live typing** as the Newcomer (the REPL), on top of 4 and 5.

## Reading a typed line (planned contract)

A typed line is `PlayerLine(Id, Tick, Listener, Text)`; the speaker is always the Newcomer. The
text is the player's own words: trimmed, at most 160 characters, no control characters, stored and
shown as typed and always rendered as text, never markup. It is never sent anywhere except the
loopback Laya endpoint.

Laya answers two independent questions about it. Each is its own question in one request, so
neither sees the other's options (Laya scores each question as its own sequence).

- **What is the line doing?** The options are the acts the Newcomer could begin toward that
  listener right now, by the engine's own eligibility (`Fits`, reach, caps and the switches that
  are on), plus "small talk". Offering only what is possible keeps the question small and stops
  the reading from asking for an act the rules would refuse. Under today's rules that includes
  Welcomed toward anyone the Newcomer has not met; for the Newcomer that option is worded as
  introducing oneself, since the player is the one arriving. Small talk and introducing oneself are
  the pair most likely to be confused (the probe read small talk as a welcome often).
- **What, if anything, is it about?** The options are short phrases built from the listener's own
  memory: their open needs and requests, promises with the speaker, recent encounters with the
  speaker. At most five, plus "none of these". A listener who never heard of the fish cannot read
  a line as being about it, which is the design's misreading-is-a-feature from the listener's side.

Both questions use the game adapter's rules: plain options with slot labels, the line in the state,
the Balanced presentation (every option in every slot once, averaged), Sid's 1,250-character budget
per question, a five-second deadline, loopback only, and a recorded fallback. With Laya off or
failed, the player picks the act from the offered list (the design's built-from-parts fallback)
and no claim is read: the words stay flavour.

**Confidence, from the probe.** With `typed-decisions`, Laya's probability for its own answer was
honest: at 0.7 or above it was right 94% of the time on player-style lines. So a reading at 0.7 or
above is taken; below it, the player is shown Laya's top three acts and picks one. For those lines
the top three held the right act 69% of the time with all fifteen options offered and 90% with six,
so offering only the acts that are possible matters for the confirmation too. A claim's match is taken only above a
minimum probability (to be set in scenes; the probe matched a third of claims about nothing to
something); otherwise the claim is read as about nothing unless the player points at the thing.
Misreading stays possible, as the design wants, but mostly where the line really is unclear.

The reading's act still has to happen through the engine: if the listener has walked off or the
act is no longer allowed, nothing happens and the line is recorded as said to no effect. A small
talk reading starts no act, but it counts as time spent together (Sid, 2026-10-13), through the same
rules as a chat between villagers: company's small lift to both moods (a small sadness with someone
disliked, `Company` in `Simulation.Feelings.cs`) and the minutes together toward a day's contact
(`ContactMinutes`). Nothing is sampled from Laya's weights: a reading above the
confidence line (below) is taken as it is, and below it the player picks. In a scripted run there is
no one to ask, so the top reading is taken and recorded as unconfirmed. The player's text, every
reading and every pick go on the tape, and replay checks the whole request (text, listener, offered
options, the listener's offered memory) before using a recorded answer.

## Claims, and how a listener answers one

A claim is a line whose reading points at something the listener knows of. The first slice reads
one kind of claim, the one follow-through creates: **"it's done"**, as in "I brought you the fish",
"I fixed the fence" or "I paid you back". Other claims (denials, accusations, reports about third
people) come later and need their own rules.

A claim is recorded as `Claim(Id, Tick, Speaker, Listener, LineId, About, State)` where `About` is
the need, promise or act it refers to. Whether it was true is written for the observer only: no rule
reads it, and a test will hold that.

The listener answers with one of four authored responses, chosen from what they know:

| Response | When | Example (Sid's) |
|---|---|---|
| Take it on trust | they trust the speaker and nothing they know contradicts it | "Oh! Thank you so much, I have something special planned." |
| Ask to see it | the thing could be shown now, and they want to check | "Okay, let's see the fish." |
| Demand it | the thing could be shown now, it is owed to them, and they are bold about it | "Give it to me." |
| Doubt it | they know something that contradicts it, or they do not trust the speaker | "What fish?" |

The inputs are the listener's credence in the speaker (`Feelings.Credence`: familiarity, regard,
understanding), trust in that particular speaker (below), boldness, and whether the claim
contradicts their own memory. A deterministic rule gives the baseline; Laya may weigh the four
authored responses in the listener's context the way reflection weighs candidate lines. The reply
lines are authored per response and archetype in the speech catalog.

**Checking is an event.** Asking to see it or demanding it makes the speaker show the thing, as an
ordinary act others can witness. If the speaker holds it, it is handed over: the need is met, the
promise kept, and trust rises. If not, the claim fails in front of the listener: a witnessed act
that travels as gossip like any other, trust in the speaker drops to nothing, and regard falls.

**A lie taken on trust waits.** The listener believes the thing is done and stops looking for it.
The lie comes out only through perception, under the same rules as any other knowledge:

- first-hand, when the listener sees something that contradicts it (the thing never turns up at a
  hand-over they are at, or they see the speaker still holding it): the lie is exposed, and it
  costs more than being caught on the spot, because it was believed;
- by telling, when someone who knows tells them: that is hearsay (design rule 9), so it moves
  mood only, and becomes exposure only once confirmed first-hand or told by a second, independent
  teller.

It never comes from the simulation comparing the claim with the truth (the backlog's lies,
`FakedSick` and S10, work the same way).

**Trust in a particular person** is design rule 14, unbuilt: it starts at the seed regard (0.2 for
the Newcomer), rises 0.05 per help, kept appointment or repaid loan and 0.1 per confidence kept for
seven days, and drops to 0 on a betrayal. This spec proposes one addition for Sid to decide: a
claim that checks out counts like a kept appointment, and a lie caught or exposed counts as a
betrayal. In code today, trust in a speaker exists only as credence (`Feelings.Credence`): it scales
hearsay's effect on mood, and it weighs each teller's account when the mayor judges (`Trust` in
`Simulation.Authority.cs`, used by `Authority.Weigh`). The claims slice builds rule 14 as its own
record; the numbers are the design's and need tuning in scenes (VERIFY).

## How the town comes to know the Newcomer

Sid (2026-10-13): what the player types shapes how villagers come to describe the Newcomer. The town
does not read the player's text; it reads Laya's readings of it, and only where a villager heard
them:

- each listener keeps their own impression of the Newcomer from the readings of the lines said to
  them or in their hearing: how often the Newcomer joked, complimented, mocked, apologised, made
  small talk, made claims that held or failed, most recent first;
- a misreading counts as what the listener took it to be (a joke read as mockery is remembered as
  mockery), which is how the design wants misreading to matter;
- the impression enters that villager's own reflection context ("the newcomer jokes a lot, and
  once mocked you") and how they speak of the Newcomer when they tell others, where it travels as
  hearsay under rule 9, never as the hearer's own;
- nobody's impression is built from lines they did not hear, and no impression is built from the
  player's words directly.

## What the town and the observer see

- The typed line is the Newcomer's speech for its act, marked as the player's, under the same
  clock as every other line. The viewer shows the reading beside it: the act options and their
  weights, the claim's offered references and the one read, and anything the reading refused.
- The listener's response and reply line, the check if there was one, and the claim's later fate
  (kept, caught, believed, exposed), each linked to the events that caused it. Nothing is shown
  that cannot cite an event (design 6).
- Villagers learn about the Newcomer only through what they perceive and are told, as now.

## Determinism and safety

- Every switch is off by default; with player speech off, every pinned run stays byte-identical.
- Readings use their own seeded streams, so they move no other system's dice.
- A scripted or typed session replays exactly from its tape; a changed line, listener memory or
  option list invalidates the tape on purpose, as reflection's tapes do.
- The player's text is shown as text. Nothing in the simulation parses it for facts; only Laya's
  typed reading enters the rules, and only as a choice among the options the engine offered.

## The probe

What it measures, against labels judged blind by three independent readers:

- **Act reading:** each line with all 14 act kinds plus small talk as options (two wordings of the
  options), and with the true kind plus five others. Lines: the speech catalog's 359 lines
  (labelled by their authors), player-style lines in three typing styles (terse game chat,
  roleplay, plain speech) at three difficulties, and small talk.
- **Claim reading:** cases where the newcomer tells a listener something, with four things the
  listener knows of plus "none of these"; direct mentions, paraphrases, word traps and claims about
  nothing listed.
- **Controls:** every option in every slot (Balanced rotation, as the game uses; the probe sends a
  line's rotations as separate questions in one request, the game one request per pass), the raw
  first-rotation reading for comparison (options in an order hashed per line), two Laya checkpoints (`typed-decisions` and `english`),
  refused replies on any truncation, and the readers' own agreement as the ceiling a reading
  could be expected to reach.

```bash
pwsh -File sidecar/run-laya.ps1    # loopback Laya on 8000
python sim/tools/laya_categorize_probe.py --cases out/laya-probe-2026-10-13/cases.json --models typed-decisions english --out out/laya-probe-2026-10-13
python -m unittest discover -s tests -p test_laya_categorize_probe.py
```

**First results (2026-10-13).** No fallbacks or truncation in 3,956 requests (median 72 ms for
fifteen rotations, largest question 811 characters). Player-style lines were read right 60% of the
time with all fifteen options and 76% with six, small talk still offered (readers: 99%); explicit
lines 86%, hard ones 42%. Compliments, apologies, thanks and arguments were read well; mocking,
squabbling and standing up for someone poorly; small talk was often read as a welcome. The
catalog's own in-character lines were harder (47%). Claims were matched right 83% of the time
(direct 91%, paraphrase 94%, word traps and claims about nothing 67%). `typed-decisions` and
`english` read about equally well, but only `typed-decisions`' confidence was honest. With the
options' order varied per line there was no large position effect. Hence the confidence rule
above, offering only possible acts, and keeping `typed-decisions`. Details, tables and limits: [the probe write-up](../experiments/laya-categorize-2026-10-13.md).

## Verification scenes (planned)

- A typed line read as an offered act becomes that act through ordinary eligibility; an act that
  is no longer possible leaves the line said to no effect.
- Small talk starts no act; an unreadable line is recorded with the reading that refused it.
- A true claim, checked: the thing is handed over, the need met, trust up.
- A lie, checked: caught in front of the listener, gossip follows, trust gone.
- A lie taken on trust, exposed days later by a sighting or a telling, never by the truth itself.
- A doubt from contradiction: the listener saw something that does not fit.
- A listener with no memory of the thing reads the claim as about nothing.
- Laya off: the act is picked from parts, no claim is read, the run still replays.
- A packet over budget falls back with a recorded reason rather than being cut.

## Decided, and still open

Decided by Sid, 2026-10-13: small talk counts as time spent together (above); what the player types
shapes how villagers describe the Newcomer, through the readings they heard (above).

Still open:

- **Should the probe gate the next step?** Sid asked what a gate would look like. Proposed: none
  before scripted lines (step 4 above), which change nothing for a player; one before live typing
  (step 6), measured on Sid's own lines rather than authored ones (the design's E4a: 50 lines typed
  the way he would, labelled by him). It would pass when, at the 0.7 confidence line, the readings
  taken are right at least 90% of the time and the top three hold the right act for at least 80% of
  the rest; if not, fine-tune `typed-decisions` on half of his lines and test on the other half
  before live typing. Awaiting Sid.
- When does the emotion wheel's reading (design 6) join the typed payload: with the first scripted
  lines, or with live typing?

## Next

The invitation (step 1 above), with reply lines; then the need with a real item; then scripted
player lines read by Laya.
