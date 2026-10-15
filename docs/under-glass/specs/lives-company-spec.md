# Lives: seeking company and sharing a game

## Status

Built 2026-10-15 as an opt-in extension of [history-informed routing](lives-routing-spec.md).
It connects a recalled warm experience to looking for someone or offering an immediate game,
an actual answer, a shared activity that can finish or be interrupted, and expectations read by
later choices. The existing routing and memory foundation remain separate switches and APIs.

This is a small implemented thread within the [Lives assessment](../lives-gap-assessment.md),
not the complete Penny–Pam–Gus pilot. There is no future evening agreement, scheduled plan,
follow-through judgment, new reflection trigger, Laya call, or generated dialogue in this increment.
There is no shipped, individually approved canon history pack. The tests use fictional people and
explicitly synthetic histories; their `Canon` source category exercises admission rules and is not
evidence about Stardew characters.

The current routine baseline is retained: habitual bedtimes remain weighted preferences, the body
decides sleep and waking, and the default bedtime weight remains 9. This feature adds no bedtime
penalty to invitation answers. The accepted O2/O3 pilot decisions remain in the assessment; this
increment neither admits the proposed canon readings nor implements a disappointing future evening.

Implementation is in `Lives/Company.cs`, `Simulation.LivesCompany.cs`, and
`Simulation.LivesCompany.Activity.cs`. `Simulation.Ask.cs` exposes the existing invitation margin
internally so this offer can use those reasons without creating an act or asking twice.

## Configuration

Call `ConfigureLivesRouting(...)` and then `ConfigureLivesCompany(...)` before running. Company is
off by default. Enabling it requires enabled Lives routing, active feeling steering, the existing
`Company` and `Invitations` act options, and the `PlayedGame` and `Refused` catalog rows. Watch-only
acts do not satisfy that requirement. Custom towns must supply the catalog rows as well as the flags.
Replay adds rows through its normal catalog option. History record IDs beginning `company:` are
rejected because that prefix identifies new live causes; the routing adapter also reserves `act:`.

| Setting | Default | Meaning |
|---|---:|---|
| `Enabled` | false | Enable the behavior. |
| `Record` | true | Keep company observer receipts. |
| `SeekBase` | 0.1 | Base weight for each possible search. |
| `OfferBase` | 0.1 | Base weight for an immediate offer. |
| `TravelMinutes` | 120 | Maximum time to reach the remembered search location. |
| `SearchMinutes` | 15 | Time to look after reaching the search spot. |
| `SightAgeMinutes` | 4320 | Maximum age of a personal sighting used as a destination. |
| `MaxCandidates` | 3 | Maximum search candidates, admitted in stable name order. |
| `AnswerScale` | 5 | Strength of the answer probability calculation. |

These are experimental implementation settings, not sourced preferences or calibrated laws.
Turning the feature off leaves the previous run unchanged. Turning recording off leaves behavior,
movement, feelings, and learned state unchanged.

## What can open a possibility

A bounded recall must contain a warm cause concerning the other person or the `PlayedGame`
activity. Warmth toward the place alone does not propose a game with every person there. Each
option requires that a suitable warm record be recalled; it does not bind to one permanent record
ID. An ordinary encounter can open an offer;
there is no requirement to have gone looking first. Empty memory alone does not create an offer.
Relevant existing lived feelings or an admitted starting reading can supply the opening.

The new choice still reports warm and cold separately. After the recall gate, all retained numeric
associations to the option's person, place and activity contribute through the existing choice
kernel; they are not limited to the top recalled records. Multiple projections of one experience
count as one cause. The choice uses current boldness and mood through the routing kernel and
retains a `carry-on` alternative. No outcome is forced toward agreement or completion.

For a search, carrying on has the combined base weight of the existing free-time menu. A selected
`carry-on` returns to that menu's normal routing decision. A search destination is the holder's
latest sufficiently recent personal sighting, or a known shared home when no such sighting exists.
A person's authored job, home at an unrelated household, private intention, or current remote
position cannot reveal their whereabouts. The rule considers at most three known candidates in
name order; it does not rank the whole population by remembered affection.

A search owns the asker's goal, has a travel deadline, and then a bounded search window. Bodies,
work, detention, errands, and existing obligations can interrupt it. A habitual bedtime checkpoint
ends the search and returns to the normal routine reconsideration; it does not force bed or sleep.
Local sight and line of sight can find someone and allow approaching their actually seen position.
Arrival elsewhere, or an expired travel window before arrival, is not evidence that they refused.

## Offer, answer and activity

There is at most one active company episode for each reserved participant. Seeking reserves the
seeker; pending and playing reserve both people. A selected search or considered ordinary offer
uses the unordered pair's opportunity for that day, including a choice to carry on at the offer.
The reciprocal partner cannot generate another ordinary offer to bypass this cap.
Legacy game invitations share this pair/day cap, and their existing refusal cooldown is respected
by new company offers, including at home. Drinks retain their existing rules.

An offer requires both people to be awake, physically free, near each other and mutually in view,
with an available game location. Household members aged five or older can play in their shared
home without an authored game card or a similar-age restriction. Outside that case, the existing
game's place, time and age-fit rules still apply. This does not change ordinary legacy game draws.

The offer enters `Pending` without an act ID. The other person answers on the next simulation
minute if the encounter still permits it. Losing contact or becoming unavailable before that
answer ends the attempt without inventing an answer. A yes starts one normal witnessed game act;
a no creates the existing `Refused` act; not-now creates neither. The accepted game bypasses the
old invitation path so it is not asked a second time.

The answer uses the existing invitation margin: regard, familiarity, chattiness, fondness and
family/household bond, against dislike, withdrawal and the existing tired/busy/low state costs.
The new three-way fallback samples these weights:

```text
yes      = exp(scale × margin)
no       = exp(−scale × margin)
not-now  = temporaryReason × exp(scale × temporaryReason)
temporaryReason = max(tired, busy, low)
```

Not-now is absent when there is no temporary reason; its weight approaches zero as that reason
disappears. This avoids giving an almost fully rested person a one-third deferral chance solely
because tiredness is microscopically above zero. Scale zero flattens yes/no, while retaining the
temporary reason's prior weight. Stable log normalization avoids overflow. The draw is keyed by
the run seed and episode ID. These authored answer probabilities are not Laya answers, and the
asked person's new Lives history does not yet enter this margin directly.

A game occupies both people for the existing 30-minute duration. Every minute checks continued
participation: sleep/collapse, detention, work, bodily tiredness, another owned activity, separation,
or loss of sight can interrupt it. Interruption releases only that game's owned time and removes
its completion watcher. It cannot later deliver completed-game joy or completion accounts.

At actual completion, each participant receives exactly one personal felt share:
`PlayedGame.Affect.Joy × sensitivity multiplier`. Each is recorded as `SharedActivity` /
`Participated`. Ordinary target-kindness feelings and the legacy actor completion bonus are
suppressed for these participants, including later retellings, so the same game is not paid twice.
Witnessed event accounts still use the normal finish path. The shared feeling becomes one cause
with person/place/activity projections; these projections do not add mood again.

## What the character learns

The live writer separates kinds of evidence rather than treating every unsuccessful attempt as
rejection. Learning begins at the first outcome, using the existing memory kernel and its bounds.

| What was acquired | Expectation written |
|---|---|
| An actual yes or no | `Answers(partner, company:PlayedGame)`, success or failure. |
| An actual yes | Also positive `Availability(partner, company:PlayedGame)`. |
| An actual not-now | Negative `Availability`, without an `Answers` failure. |
| Found them during a search | Positive `Whereabouts` for the perceived place and time bucket. |
| Saw them asleep, collapsing or occupied by a physical act | Negative `Availability`. |
| Did not see them throughout the deliberate search window | Negative `Whereabouts` only. |
| Actually completed or interrupted the game | Positive or negative `Activity(partner, PlayedGame:place)` for each participant. |

An internal company intention, upcoming work, or an execution gate's inability to proceed is not
alone an observed availability failure. A miss does not identify where the other person went,
create blame or sadness, or become a negative answer. An actual yes provides later positive
availability evidence so an earlier not-now does not remain an uncorrectable penalty.

Whereabouts use experimental three-hour weekday/weekend buckets. Search options read that
expectation alongside answer, availability and activity expectations. Immediate offers read the
last three. The current kernel multiplies the supplied expectation chances; this factorization
is a testable implementation assumption, not a claim that the events are independent. Generic
free-time routes still read place associations only. No `FollowThrough` expectation is written.

## Observation and history comparisons

Observer records are immutable episode updates, choices, answers and outcomes. Their lists never
feed decisions or learning. A company choice captures the same immutable personal-memory frame
used by routing. Requested history comparisons hold its scene, options, state and random unit fixed
and remove only the selected history dependency closure. They do not replay the character's past
or replace the actual sampled action.

Removing the sole memory that opens an offer can remove the alternative itself. Such a comparison
is explicitly non-comparable, with `eligible-alternatives-changed` or the applicable kernel reason;
it must not present probabilities as though the menus were identical. Independent lived evidence
and the actual outcome stay intact. The warm-recall predicate is reevaluated in each arm: if another
independent warm memory still opens the option, it stays eligible and its changed probability can
be compared normally.

Replay writes a separate experimental `livesCompany` block: `version: 1`,
`stage: "immediate-company"`, `backend: "authored-keyed-draw"`, settings, updates, choices, answers
and outcomes. `livesRouting` remains the source of memory-change receipts and optional validated
history snapshot. This is not the full Lives observer v0.2 contract and does not produce its
`lives` or `choiceQuestions` sections. The chain viewer does not yet render this new causal thread.
The ordinary Run Viewer can show actual game/refusal acts and movement.

For an unseeded exploratory run using feelings acquired during play:

```powershell
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --lives-routing --lives-company --catalog company,invitations --out lives-company-seed7.json
```

`--history <file>` and `--compare-history <id>` retain the routing meanings and require an explicitly
authored, reviewed pack. No example command implicitly approves a canon pack. `--no-company-record`
omits company receipts; `--no-lives-record` independently omits routing/memory receipts. Set both to
disable both observer blocks. Numeric company settings currently require programmatic options.

## Verification and remaining work

Focused tests cover off equivalence, configuration guards, private knowledge, held-choice history
removal, delayed answers, refusal and postponement, pair/occupation limits, actual completion,
interruption without premature or duplicate joy, later expectation reads, and recorder independence.
Fixed seeds with exaggerated offer weights exercise mechanisms; they are not curated evidence of
character fidelity or recommended production tuning.

Active search duration and candidate counts are bounded. Retained episodes, acquisition identities,
observer receipts and some memory-key collections still grow during a run. Current candidate and
encounter work scans people and personal sighting spans, and memory captures allocate. No
large-population performance or recording-size claim is established here.

Next work remains individual approval of pilot histories, the agreed meaningful evening and its
possibly disappointing consequences, connecting reflection and typed model choices, and exporting
the complete readable observer chain. The present thread provides a real action/outcome loop to
test those additions against without changing the routine baseline or resuming needs bars.
