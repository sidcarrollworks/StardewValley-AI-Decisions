# Lives: personal history and memory foundation

## Status

First built 2026-10-15 as an independently testable component in
[`History.cs`](../../../sim/UnderGlass.Sim/Lives/History.cs) and
[`Memory.cs`](../../../sim/UnderGlass.Sim/Lives/Memory.cs), namespace `UnderGlass.Sim.Lives`.
This is the first foundation for the [Lives assessment](../lives-gap-assessment.md), principally
sections 2 and 9.2–9.5. It can be exercised directly with synthetic inputs.

The follow-up [free-time routing increment](lives-routing-spec.md) now connects this kernel to
`Simulation`, Replay and `--lives-routing`, all opt-in. It learns associations from the holder's
perceived feelings and can use explicit history in existing destination choices. This is not the
full Lives pilot, and no reviewed canon history pack is shipped or loaded implicitly. Tests use
invented people and explicitly synthetic evidence; a test source classified `Canon` exercises
admission rules without asserting anything about Stardew. Background anchors in
`character-inspiration.json` remain private model prompt context and are not converted into these
records. The new memory is not added to reflection/model context in this increment.

The memory kernel stores and recalls knowledge. Its separate routing consumer chooses among
existing free-time destinations; the kernel itself calls no model, sets no mood or regard,
discovers no person's location, and does not classify expectation outcomes or implement invitations
and planned evenings. Law 1 and the unresolved needs-bars direction are
unchanged. The constants below are experimental implementation settings, not canon facts or
new product decisions.

## History input and admission

`HistoryPack.Read(json)` reads version 1; `CopyValidated()` validates programmatic input and
returns detached, read-only collections in ordinal order. The input separates three things that
must not become interchangeable:

| Record | Meaning and fields |
|---|---|
| `HistorySource` | An authoring citation: ID, source key, and `Canon`, `Authored` or `ProductDecision` standing. A citation is not an in-world observation. |
| `HistoryFact` | The shared identity of a `Fact`, completed `Past` or recurring `Practice`, with citations. Its content is expressed in the holders' accounts; it carries no invented date, repetition count or shared feeling. |
| `HistoryAccount` | One holder's text about one fact, its sources and cues, and how that holder knows it: `Lived`, `Witnessed`, `Told` with a teller, or `Inferred`. |
| `HistoryReading` | An interpretation of exactly one account, with text, object, stance, origin, admission rule, review, citations and cues. `Expects` also carries an expectation key and an expected success/failure. |

Two people can have different accounts of one fact. One person's account or reading does not
create knowledge for another person. There can be only one account for each holder/fact pair;
overlapping citations belong on that account. A reading inherits its holder through `AccountId`.
An inference can be preserved as an account without becoming witnessed evidence.

Readings explicitly name `CanonStated` or `Authored` origin. A canon-stated reading requires
`R1` or `R2` admission; an authored interpretation requires explicit `null` admission, since it
has no canon admission rule. A `Reviewed` reading requires a reviewer and a valid calendar
date. `Proposed` and `Rejected`
readings remain in the input for audit but are absent from runtime recall and seeding. Their
underlying accounts remain separate recallable records. A reviewed authored reading can be
recalled, and the input pack retains its declared stance, but it supplies no numeric warmth,
wariness or expectation.

Only reviewed, canon-stated readings seed numbers:

- `Warm` and `Wary` create separate positive and negative associations.
- `Expects` creates one initial entry in its specific expectation key.
- `None` creates no numeric seed.
- A completed past additionally requires `R1` and a canon citation in `PresentLinkSource`, also
  present in that reading's sources: its holder's own stated link to a present attitude.

Validation checks that the source exists and has the required standing. It does not inspect game
dialogue or prove who spoke it. Establishing that the cited statement is the holder's own link
remains the content reviewer's responsibility. An authored causal interpretation is not made
canon merely by citing the event that inspired it.

Duplicate numeric seeds are rejected per holder/object/stance, and per holder/expectation key.
Warm and wary toward the same object coexist; multiple citations do not add extra seed strength.
Authoring provenance is mandatory and every reference must resolve to the correct record kind.
IDs are globally unique within the pack. Account and reading IDs cannot use the runtime
`association:` or `expectation:` prefixes.

The parser rejects missing required fields, null collections or entries, unknown properties,
duplicate JSON properties, malformed UTF-16 identifiers/text, unknown enum values, and integer
or numeric-string enum encodings. Valid supplementary Unicode characters remain supported.
Enum names are explicit, such as `Warm` or `CanonStated`; property names accept either casing.
Text is limited to 2,000 characters, IDs and keys to 200, each main list to 20,000 entries and
each citation/cue list to 128. JSON input is capped at 8,388,608 characters and depth 16. These
are input bounds, not a claim that a pack near those limits has been performance-tested.

## Per-person state

`PersonalMemory(holder, pack, options, sink)` copies the validated pack and imports only that
holder's accounts and reviewed readings. A holder absent from the pack begins without history.
The constructor creates no simulation event or mood change. Pre-run records use `KnownAt = -1`
to distinguish them from new acquisitions.

Objects are typed as person, kind of person, place, activity or situation. Expectation keys
separately identify whereabouts, availability, answers, follow-through or how an activity goes,
with an explicit subject and context. Keeping these keys separate permits later outcome adapters
to distinguish a refusal from a failed meeting. The component does not infer those classifications.

Default `MemoryOptions`:

| Setting | Default |
|---|---:|
| Initial association strength, `SeedStrength` | 0.05 |
| Lived association retention per night, `NightlyRetention` | 0.975 |
| Active associations per object | 3 |
| Active associations per holder | 96 |
| Lived entries retained per expectation key | 6 |
| Normal recall count | 3 |
| Consecutive agreeing entries for a summary, `DeriveAfter` | 3 |

The two signs are retained separately. `Sides(objects)` reports warm and cold amounts with their
causes and routes; it never subtracts them into one regard value. When the same cause projects to
several requested objects, it contributes the largest warm and largest cold amount once each.
Different readings of one starting account retain their reading-specific routes while sharing
the account as their association cause. New experience uses its supplied `CauseId`.

## Learning and bounded active associations

`Learn(MemoryExperience)` takes one holder's first acquisition of a cause. The caller supplies a
monotonically increasing sequence, acquisition time, stable cause ID, knowledge route and all
association/expectation projections together. It validates the entire input before changing
state. One acquisition cannot project twice to the same object or expectation key.

New learned experience accepts `Lived`, `Witnessed` and `Told`; it rejects `Inferred` and `Reading`.
A told acquisition requires its teller and retains credence. Association amounts are already
felt amounts supplied by the caller, including any hearsay adjustment; the kernel does
not discount them a second time. Stored credence does not weight the expectation formula.
No mechanism here turns a model's imagined scene or inference into an acquired outcome.

For each object, trimming retains the strongest entry of each present sign, then fills remaining
slots by absolute effective strength, with ordinal ID ties. If the holder limit is still
exceeded, the kernel removes the **whole object** with the smallest sum of absolute effective
strengths. This is an explicit implementation policy: it preserves opposing sides while an
object remains active, but permits the entire object's associations to leave active memory.
It is not a guarantee that every association survives indefinitely.

An initial association's effective strength is:

```text
initial signed strength / (1 + cumulative learned associations for this object)
```

The count includes accepted causes whose active entries have since been evicted. Forgotten
lived entries therefore do not make the initial reading regain authority. Its recall strength
uses the same dilution, even if its seed association was trimmed. Correctly retracting an
acquisition undoes that cause's dilution contribution once. Immutable history text remains;
retraction does not restore an evicted active entry.

Lived association amounts fade by `NightlyRetention` for each elapsed night. Initial readings
are diluted by experience rather than faded nightly. The kernel neither derives feelings from
an old event automatically nor reinstalls seed associations removed by trimming.

## Expectations learn at the first outcome

Each key retains its initial reading entry, if any, and the latest six lived outcomes by default.
The chance is exactly the assessment's proposed formula:

```text
(successful retained entries + 1) / (all retained entries + 1)
```

With no entries it is 1. An initial failure gives 0.5. A further failure lowers that to 1/3; a
success instead makes it 2/3. Every accepted outcome enters this formula immediately, without
waiting for a summary; a success at an already certain key can leave its value unchanged. This
optimistic empty-key prior is experimental and is not a calibrated
probability claim.

When the last `DeriveAfter` lived entries agree, the snapshot exposes a boolean summary and the
supporting cause IDs. Mixed entries can remove that summary. It does not add another observation
or alter the chance. This foundation does **not** yet create a separately authored `Derived`
reading or a summary sentence. The original reviewed reading remains recallable beside the
current expectation state.

Expectation cues are rebuilt from entries still in that key's window. Expired outcomes cannot
leave an ever-growing union of cues attached to the key. Keys themselves have no global count
limit in this foundation; a future adapter must use stable, meaningful contexts.
Recalling an expectation labels it as a `Reading` of the retained window. It does not borrow
the latest entry's evidence classification: each underlying entry keeps its own acquisition
route, teller and credence in the expectation snapshot.

## Recall and cues

`Recall(cues, at, lowMood)` looks up candidates through the holder's cue index rather than
scanning a run-long event archive. The index contains immutable accounts/reviewed readings,
active lived associations, and current expectation states. It is rebuilt when this state changes.

For each matched candidate, a cue explicitly citing an applicable canon source scores 2; an
authored cue scores 1. A sourced cue must cite a source belonging to its record; an account may
also use a canon citation from its shared fact. Reading and association objects receive an
automatic object cue at ordinary weight. Current expectation candidates use ordinary-weight
cues from their retained entries; the source-weighted original reading remains separate.

Candidates rank by total overlap, then strength times `(1 + personal significance)`, then ordinal
ID. The default returns the top three records, reserving a slot for a matching pre-run record if
none would otherwise survive. A sourced cue is stronger, not an exclusive trigger. An unrelated
pre-run record is not inserted when no cue matches it.

For this first ranking implementation, accounts and reviewed readings start at `SeedStrength`
(numeric association readings subsequently dilute), active associations use their absolute felt
amount, and expectation states use 1. These are recall ranking values, not evidence confidence
or a calibrated comparison of psychological importance. They need review in actual choices.

With low mood, one additional positive candidate may be returned by warmth alone from the
**already matched pool**. This implementation does not search all history for comfort. No mood
is read automatically: the caller supplies `lowMood`.

The three normal results are **records, not necessarily three distinct experiences**. An account,
its reading and a related expectation can share a cause and occupy separate recall slots.
Cause-aware numeric deduplication in `Sides` does not imply recall diversity. These receipts keep
that overlap visible. The first routing consumer gates on nonempty recall but weighs all retained
associations to an option's destination; the top recalled records are not its complete numeric
evidence set. See the routing spec for this explicit experimental policy.

## Clock, retries and corrected attribution

The kernel starts on day 0. Its caller must call `AdvanceToDay(day)` before learning, correcting,
recalling or retracting anything on a later day. Advancing applies all elapsed nights exactly once and rejects
backwards movement. Acquisition/retraction times must also respect the latest acquired state;
past-time viewing uses recorded snapshots rather than rewinding the live store. Explicit day
advancement prevents newly acquired evidence from being aged retroactively.

Retries must preserve the original sequence and cause ID. Old/repeated sequences do not learn
again. A minimal dictionary of acquisition receipts also remembers every accepted cause, so
eviction or retraction cannot let an ordinary `Learn` retry re-enter under a new sequence. Each receipt
retains the sequence, projected objects and whether it has been retracted. This dictionary grows
with the holder's known causes; it is **not bounded active memory** and is not used as a recall
archive. Cumulative object counters and expectation keys also persist separately from the
bounded association list. Larger-population lifetime cost remains to be measured.

`Retract(causeId, at)` removes that cause's retained lived associations and expectation entries,
and reverses its cumulative dilution contribution once. It does not erase the receipt, change
pre-run history, revive trimmed evidence, or infer a corrected culprit.

`Correct(MemoryExperience)` explicitly revises an already acquired cause with a newer sequence and
valid acquisition time. It validates all projections first, removes that cause's retained old
associations and expectation entries, reverses its old dilution once, then applies the replacement
under the same cause identity. The old and new states appear in one `correct` observer receipt.
Unknown causes fail; stale sequences are ignored as retries. A newer explicit correction can
re-establish a previously retracted cause without reversing its dilution twice. This preserves a
distinction between revised knowledge and an accidental repeated acquisition. It does not restore
unrelated evidence evicted earlier. The routing adapter uses this path when the holder's perceived
culprit, felt strength or acquisition provenance changes.

A correction alone may include a zero-amount association projection. It updates that cause's
object receipt and cumulative dilution, but stores no active association. This lets a fully faded
or forgotten cause be reattributed without restoring its emotion or giving an unrelated seed back
its original authority. First acquisitions through `Learn()` still reject zero amounts. The caller
supplies corrected amounts at their current age; `Correct()` does not infer elapsed decay itself.

## Frozen decision frames and history interventions

`Capture()` returns a `MemoryFrame` exposing `Snapshot`, `Recall`, `Sides` and `Expectation` through
the same production query implementations. It copies working collections and shares only validated
immutable record values. It does not copy the run-long acquisition receipts and has no mutation API,
model or observer sink. Later live learning, correction, retraction and decay cannot alter the frame.
Recall must remain within its captured day and cannot precede its latest known evidence.

`WithoutHistory(ids)` creates a new frozen frame. Its removal closure follows a fact to the holder's
accounts, then their readings and numeric seeds; removing just a reading leaves its account and
sibling readings intact. Seeded expectation entries are removed with their readings. Independent
lived evidence and cumulative dilution remain fixed. This is an intervention at the current choice,
not a re-simulation of the past or recovery of evidence previously lost to capacity limits.

`HistoryIds` lists the original holder-owned fact/account/reading IDs, including audit-only readings
and IDs already removed in an earlier intervention. `RemovedIds` lists the cumulative removal
closure. Source IDs, unknown IDs and other holders' private account/reading IDs are rejected
atomically. Repeated removals are idempotent. Capture, queries and removal emit no observer events;
the decision layer separately records its comparison results.

## Observation and remaining integration

The optional `ILivesSink` receives immutable copies through `Changed` and `Recalled`. Changes
include before/after state, cause and reason, dropped IDs and added associations, including an
association immediately evicted by the same insertion. Recall receipts include selected record
IDs, their causes and sources, knowledge route and ranking values. The sink returns no decision
data to the kernel; no simulation reader consumes these observer records.

These records are a **write-only observation boundary**, not an implementation of the
[Lives observer v0.2 contract](../contracts/lives-observer-recording-v0.2.md). The routing adapter
currently acquires associations from perceived feelings and exports a separate experimental
`livesRouting` version-1 block. Full observer export and chain-viewer integration remain to come,
as does live expectation-outcome classification. In particular, learning from an actual missed
meeting must establish what that holder could know; this store does not decide that an absence is
a rejection, a whereabouts observation or a broken agreement.

Focused tests cover validation, knowledge separation, two-sided causes, dilution and retraction,
bounded active state, first-outcome learning, indexed recall, timing, correction, frozen history
interventions and observation copies. Routing tests now demonstrate changed destination choices
with synthetic history. They establish mechanics, not character fidelity or a completed
Penny–Pam–Gus thread.
