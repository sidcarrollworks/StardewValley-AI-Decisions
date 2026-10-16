# Lives observer recording: contract v0.1 (proposed)

Proposed 2026-10-14 for coordination between the Lives simulation work and Codex's archetype and viewer
work, as Sid's fourth direction asks ("a proposed, versioned observer recording contract and a small
example JSON fixture early, before implementing the whole slice"). Revised the same day after an internal
review and reconciled with the assessment's fourth revision, before any reader used it, so it stays 0.1
(section 2.3). **Nothing here is
implemented.** No Lives switch, record, rule or recorder exists in code; the fixture beside this file is
hand-built and labelled illustrative. This contract changes no code and must not be read as a description of
current behaviour.

Files, all in this folder:
- `lives-observer-recording-v0.1.md`: this contract.
- `lives-observer-example-v0.1.json`: an illustrative fixture (Penny, Pam and Gus; one day: a refusal and
  an agreed evening at home, an errand ask that finds Penny busy, a favour declined, a game offered at the
  Saloon).
- `check-lives-observer.cjs`: a dependency-free checker (`node check-lives-observer.cjs <file>`).
- `lives-observer-v0.1-tools/`: the fixture's generator (`build-example.cjs`) and a fault suite
  (`mutate-example.cjs`), so the fixture can be changed at its source and rechecked.

## 0. Labels

Four kinds of statement are kept apart throughout.

| Mark | Meaning |
|---|---|
| **[built]** | In the code at base `779e3b8` (`codex/reflection-prototype`), as the assessment defines it, with a base path and line. Viewer lines are base's; Codex is editing that file, so they may move |
| **[#90]**, **[#91]**, **[#92]** | Only in that open PR (errands; the bars spec, docs only; the bars experiment), cited at `b3224f4` (`claude/bars-experiment`) |
| **[proposed]** | This contract, or the assessment's proposed rules 1-16 (cited "rule N", section 9.3 of the assessment, fourth revision) |
| **S**, **A**, **E**, **NA**, **D**; **S·R1**, **S·R2** | Evidence standing, as in the assessment: sourced canon (key given), Sid's dated product decision, ours (editorial), not admissible, for the run to derive; sourced content admitted under the audit's rule 1 or placed at the start by our rule 2 choice |
| **illustrative** | Fixture content: invented values (Laya probabilities, sampled units, prompts, thought text, act ids, coordinates, states such as tiredness) |

Sid's decisions are cited by direction (1 to 4, 2026-10-14) or decision id (D52); the assessment's
remaining product decisions are **O1** (Penny's bedtime), **O2** (present readings at the start; its
second question, whether a stated wish gives warmth) and **O3** (where an agreed evening's disappointment
lands), its "Decided" list (A, section 0) and its "can wait"
list (section 14). Evidence keys (p1-p9, m1-m7, g1-g3) are the assessment's section 7; the audit is
`docs/under-glass/experiments/history-evidence-audit-2026-10-14.md`, cited, never changed.

## 1. What the block records

The Lives block is an observer's account of one causal graph per person:

```
history fact / account / reading / commitment
   -> opportunity (why now) -> recall (which records came to mind, by which cues)
   -> decision (eligible and excluded alternatives, reasons, constraints) -> choice (method, probabilities, draw)
   -> attempt (what the person tried) -> world outcome (what happened; observer only)
   -> perceptions (what each participant noticed, did not notice, or guessed)
   -> learning (which association or expectation changed, from what to what, on whose evidence)
```

Every arrow is an explicit id link (section 3). The block is **descriptive only**: the simulation never
reads it (section 11). It serves three readers: the viewer (Codex), the reviewer reading a personal thread
(Sid), and the checker.

## 2. Where it sits in the recording

### 2.1 The recording today [built]

`Replay.Record` builds one JSON object (`sim/UnderGlass.Sim/Replay.cs:182-309`): `format`
("under-glass-run"), `version` (`Replay.Version = 1`, `:45`), `seed`, `days`, `label`, `hash`, `clock`,
`settings` and `defaults` (every option class's scalars by reflection: `Settings`, `:387-396`; `Scalars`,
`:398-422`), `traitNames`, `names` (the cast in name order; people everywhere are indices into it),
`people`, `places`, `links`, `hubs`, `kinds`, `acts` (rows `[id, tick, actor, kind, place, x, y, target,
about, flags, witnesses]`), `moves`, `mood`, `power`, `regard`, `baseline`, `stance`, `minutesOut`,
`withdrawal`, `ties`, `beliefs`, `tellings`, `routes`, `feelings`, `personKinds`, `kindFeelings`,
`sentiments`, `motives`, `pursuits`, `stirred`, `roles`, `outcomes`, `life`, `constable`, `accounts`,
`verdicts`, `confrontations`, `townCash`, `events`. Written only when their switch is on: `reflectionOptions`
and `reflections` (`:279-284`), `characterInspiration` (`:285-287`), `speechOptions`, `speechCatalog`,
`speech` (`:288-296`), `invitations` (`:297-300`). Always written, additively: `encounterVersion` (1),
`appraisalRevision`, `encounters`, `encounterCauses`, `appraisals` (`:301-308`); the object is returned at
`:309`.

**Only in the open PR**: `needs`, `favours` and `promises` with errands on [#90] (`Replay.cs:301-308` at
b3224f4), `barDays` with bars on [#92] (`:309-312` there). The fixture carries `needs` and `favours`
because the slice assumes errands [#90]; a reader of base recordings will not find them.

The viewer refuses any top-level `version` but 1 (`sim/viewer/index.html:1254`) and reads optional tables
with `?? []`, so additive fields are already the house pattern. It keys invitation rows by the act that
followed (`invitationByAct`, `:1265`).

### 2.2 What this contract adds [proposed]

| Addition | Where | Written when | Read by |
|---|---|---|---|
| `lives` | top level | the proposed `Lives` switch is on | the viewer and the checker; **never** the simulation |
| `choiceQuestions` | top level | Lives on and at least one torn choice was answered without a reflection | the recorded mind for replay, matched against a freshly built question (section 7.4); the viewer through `lives` links |
| `reflections[].request.recalled`, `.alternatives` (with reasons), `.excluded`, `.constraints`, `.alternativeSetHash`; new `opportunity.kind` values | existing table | a Lives opportunity opened a reflection | replay's match (they join `RecordedReflectionMind`'s comparison, `Reflection.cs:159-205`) |
| `reflections[].answer.model`, `.evaluationMode` | existing table | an answer came from a model | the viewer; replay ignores them |
| `invitations` rows from Lives plan asks | existing table, Invitations on | a plan ask answered yes or no (not "not now": section 16, point 1) | as today; Lives links the row (section 8) |
| `settings["LivesOptions.*"]` | existing `settings` | always, once a `LivesOptions` class is added to `Scalars` (`Replay.cs:402-422`) | the viewer's settings list |

The top-level `version` stays 1; nothing existing is renamed or removed. One existing field gains a value:
a Lives plan ask answered yes writes its `invitations` row with `actId: -1`, as no act follows at the
answer (the planned act begins later, if at all); `invitationByAct` ignores it. **Links run one way**:
Lives records point at base rows (`act:412`, `favour:412`, `invitation:13512:Penny:Pam`, `need:36`); base
rows never point at Lives ids, so an older reader loses nothing and a newer one needs no base change.

### 2.3 Versioning

- `lives.contract` is `"under-glass-lives-observer"`; `lives.version` is a `"major.minor"` string.
- **0.x is pre-agreement.** A reader accepts only the exact 0.x version it knows; the sim writes exactly
  one. Changes during 0.x bump the minor and say what changed. This revision of 0.1 came before any
  reader or writer existed, so it keeps the number; the next change is 0.2.
- **From 1.0** (once Sid and Codex agree): a minor bump is additive only (new optional fields, tables or
  enum values); readers accept the same major with any minor at or above theirs, ignore unknown fields,
  and show an unknown enum value as "unknown kind", never guess. A major bump may break, and readers
  ignore a major they do not know (with a notice), as they ignore a missing block.
- `lives.rules` names the rule revision that produced the records (like `appraisalRevision`); a change
  to a rule's meaning changes it even when the shape does not.
- **Never backfilled.** A recording without `lives` stays unclassified: the viewer must not rebuild Lives
  records from regard, sentiments or acts (the appraisals precedent, `Replay.cs:301-302`).
- Fixtures set `"illustrative": true` at the top level and in `lives`, with a note saying they are not
  output from an implemented system; a real run sets neither and carries a `runId`.

## 3. Identifiers

Every id is a string `<type>:<body>`, deterministic for a given seed, town and options, never reused
within a run. Bodies use the minute and a person's **name** (names are unique and stable when the cast
grows; indices are not); fields that hold a person use the recording's **index** into `names`, as every
other table does. Ids appear only as whole string values, so prose never needs parsing. **The checker
enforces every form below.**

| Type | Prefix and form | Example (fixture) | Rule |
|---|---|---|---|
| Run | `run:<seed>:<days>:<hash>` | `run:1:28:illustrative` | `hash` is the recording's log hash; comparisons across runs cite it |
| Character | index into `names`, plus `lives.characters[].name` | `2` (Penny) | the checker verifies the pair |
| Source | `src:<slug>` | `src:penny-mon4` | canon key, decision id, code path or editorial mark |
| History fact | `h:<slug>` | `h:pam-saloon-evenings` | authored in the proposed `sim/histories.json`; the assessment's `h:` |
| Account | `acct:<fact slug>:<Holder>` | `acct:pam-saloon-evenings:Gus` | one per holder of a fact |
| Reading | `rd:<slug>` | `rd:penny-pam-care` | one per holder, object and stance |
| Commitment | `cmt:<slug>` | `cmt:penny-no-alcohol` | |
| Association | `as:<Holder>:<object>:<sign>:<name>` | `as:Penny:person:Pam:+:worried-care` | the name as a slug. Rule 2 allows up to three entries an object, so two can share a sign; names are unique per holder and object (sentiments are keyed so, `Simulation.Feelings.cs:468-470`) |
| Expectation | `ex:<Holder>:<Subject>:<key>` | `ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday` | the key is the id |
| Opportunity | `opp:<minute>:<Holder>:<n>` | `opp:13510:Penny:0` | `minute` is its `visibleAt`; `n` counts that holder's opportunities that minute, from 0, in table order |
| Recall | `rc:` + its opportunity's body | `rc:13510:Penny:0` | one per opportunity |
| Decision | `dec:` + its opportunity's body | `dec:13510:Penny:0` | one per opportunity, posed at its minute |
| Alternative | `alt:<decision body>/<slug>` | `alt:13510:Penny:0/offer` | eligible and excluded alike |
| Attempt | `att:<minute>:<Actor>:<n>` | `att:13511:Penny:0` | as opportunities, counted per actor |
| Plan | `plan:<minute>:<Asker>:<Asked>` | `plan:13512:Penny:Pam` | |
| World outcome | `out:<minute>:<About>:<n>` | `out:14040:Pam:0` | `about` is the person whose answer, availability or presence it turns on |
| Perception | `per:<minute>:<Holder>:<n>` | `per:14101:Penny:0` | as opportunities |
| Learning update | `lu:<minute>:<Holder>:<n>` | `lu:14101:Penny:1` | as opportunities |
| Comparison | `cmp:<decision body>:<intervention>` | `cmp:13510:Penny:0:without-p6` | arms carry local ids (`arm:with`) |
| Thread | `thr:<slug>` | `thr:penny-pam-evening` | an index for disclosure, nothing more |
| Model | `model:<family>:<name>` | `model:laya:typed-decisions` | |
| Base row | `act:<id>`, `favour:<actId>`, `invitation:<tick>:<Asker>:<Asked>`, `need:<id>`, `reflection:<request id>`, `question:<request id>` | `favour:412`, `invitation:13512:Penny:Pam` | a favour is keyed by the act that followed it, as the viewer keys it (`favourByAct`); an invitation by its minute and pair, since a Lives yes has no act at the answer |

Request ids on the tape: a Lives reflection is `"<seed>:<opportunity id>"` (`1:opp:13510:Penny:0`); a
torn question without a reflection is `"<seed>:q:<decision body>"` (`1:q:14010:Pam:0`). Today's
quiet-reflection ids (`{seed}:{minute}:{actor}:{source act}`, `Simulation.Reflection.cs:117`) are unchanged.

## 4. Knowledge and provenance

**Who knows, how, and from when.** Every record carries `knownBy`: a list of `{who, how, availableAt}`,
with `teller` when `how` is `Told`, `basis` when `Inferred`, and `account` when the knower holds an
account of a fact. `availableAt` is a run minute or `"pre-run"`.

| `how` | Meaning |
|---|---|
| `CanonStated` | pre-run: canon gives this person this account or reading (S, or S·R2 when carried to the start by our choice) |
| `ProductDecision` | pre-run: a product commitment Sid decided (A), e.g. D52; standing A |
| `Authored` | pre-run: an authored interpretation we place (E), such as a link its holder does not state (the assessment's `Authored` origin, 9.2; direction 4.1's "authored interpretation"); standing E, never A |
| `Seeded` | the engine's own association or expectation, written at the start from the holder's reading (rule 1) |
| `Lived` | the holder's own act, answer or state in the run |
| `Witnessed` | seen in the run |
| `Told` | heard in the run; `teller` names who |
| `Inferred` | worked out from effects; `basis` cites what |
| `Unknown` | someone took part but their knowledge is not supplied |

A record may instead say `othersKnowledge: "unknown: ..."`. A participant is never made a knower by being
a participant: the fixture's `h:penny-father-left` has Penny as its one knower and says Pam's knowledge
is not asserted (the audit's "one participant's account does not supply everyone else's").

**An account's `mode`** (Lived, Witnessed, Told, Inferred) is how the holder relates to the fact as canon
or the run describes it, separate from how we know it: Penny's account of Pam's evenings is filed
`Inferred`, origin `authored`, standing E, because canon gives her reading and the effects at home, not how
she knows (assessment p5); Gus's is `Witnessed` (g2, g3); Pam's is `Lived`.

**Shared records hold only shared knowledge.** A record or plan known by several people may hold only ids
each of them knows from their `availableAt` (an entry inside one knower's `knownBy`, such as their
`account`, is checked for that knower alone). This is what lets the character lens (section 12) show a
record without leaking another person's decision or a world outcome.

**Provenance.** `lives.sources` lists every source once: `kind` (canon, decision, code, editorial),
`key` (as the assessment cites it: `Characters/Dialogue/Penny:Mon4`, `D52`, a code path), `standing`
(S, A, E, `built` or the PR that holds it, such as `#90`), and a short `note` (for example "first
statement only"). Records cite source ids. Editorial content carries standing E and cites the assessment
section that made the call. Observer provenance never counts as a character's knowledge: reasons separate
`cites` (what the holder knows) from `provenance` (where our mechanism comes from).

## 5. Records

Common fields: `id`, `type`, `holder` (index; facts have `participants` instead), `standing`, `origin`
(`canon`; `product-decision`, standing A; `authored`, standing E; `seeded`; `run`), `sources`, `knownBy`,
`visibleAt`, `cues`, `label`.

| Type | Holds | From the assessment |
|---|---|---|
| `fact` | `kind` (Fact, Past, Practice), `admission` (R1, R2), participants, `absentParticipants` (people outside the town, as text), place, `time` (`known: false`, an optional relative phrase, never a date), `recurrence` (never a count canon lacks), `stillTrueAtStart`, `countOnce` | `HistoryFact` |
| `account` | `fact`, `mode`, `qualifications` (reported speech, a guess kept a guess) | `HistoryAccount` |
| `reading` | `object` (a record id or a cue key such as `place:Saloon`), `stance` (warm, wary, expects, none), `certainty` (stated, guess), `seeds` (the association or expectation ids it seeded), `effect` for stance-less readings | `Reading` |
| `commitment` | `scope`, `to`, `force` (Absolute or a cost), `revisable`, `since`, `support` (sources that support but do not prove), `excludes` | `Commitment`, section 9.7 |
| `association` | `object` (person, kind, place or activity; or `situation:<slug>`, cue keys held together, listed in `situation`), `sign`, `name`, `strength`, `causes` (record, plan, act or perception ids) | `Association` (sentiments extended) |
| `expectation` | `subject`, `key` (whereabouts, availability, answers, followThrough, with place, band and day type), `initial`: `{asOf, entries, lived, seeded, chance}`, the state as of `asOf` (the excerpt's start, or the record's creation) | `Expectation` |

**An expectation's window (rule 9; 9.2).** A key a reading seeded keeps its **seeded entry** (source
`Reading`) for good, beside at most `settings.livedWindow` (6) **lived** entries; a new lived entry pushes
out the oldest lived one, never the seeded one. So after six lived entries a reading is one entry in
seven (the assessment's test). `chance` = (successes + 1) / (entries + 1), seeded entry included.

**Cues** carry an `origin`: `object` (the record's own people, places and activities), `sourced` (a cue
the character states, with its source and `weight`, `settings.sourcedCue` 2) or `proposed` (ours, E).
Examples: Penny's "this time of year" (p2) is `season:fall`, sourced; Pam's wish (`Pam:Fri2`) names "that
saloon every night", so `place:Saloon` on her wish reading is sourced; `relationship:parent` on Penny's
father's leaving would match Pam, and any tie from him to her mother is ours (p2), so it is proposed (E).
Direction 4.1: a documented seasonal cue may be strong without restricting recall (`exclusive: false`).

**Originals are never edited.** Learning writes only associations and expectations (section 9); a
historical fact, account or reading stays as authored, and updates list it under `preserved` when they
revise what it seeded. Count once: one practice with several accounts is one fact (the fixture's
`h:pam-saloon-evenings`, three accounts); m1's four sources are one reading.

## 6. Opportunities and recall

An **opportunity** (`opp:`) records why something comes to mind now: `trigger` (refused-by-commitment,
asked, follow-through, encounter, outcome-contradicts, plan-broken), the proposed `rule` (10, 8a, 12, 11,
8b, 8c), `causes` (ids the holder knows), and `reflection` or `question` when one opened, or a note why
not (budget used, not torn). Observer-only links go under `observerLinks`.

A **recall** (`rc:`) records the cue match, bounded:

```json
{ "id": "rc:13510:Penny:0", "holder": 2, "opportunity": "opp:13510:Penny:0", "visibleAt": 13510,
  "active": ["person:Pam", "good:beer", "place:Saloon", "place:Home:Trailer", "season:spring", "..."],
  "bound": { "top": 3, "reservePreRun": 1, "lowMood": false, "max": 4 },
  "items": [ { "record": "rd:penny-pam-burden", "slot": "top", "matched": ["person:Pam", "place:Home:Trailer", "place:Saloon"],
               "score": { "overlap": 3, "strength": 0.05, "significance": 0.6 } } ],
  "runnersUp": [ { "record": "rd:penny-abandoned", "overlap": 0, "why": "its sourced cue (fall) is not active in spring" } ] }
```

- Items hold **only** `record`, `slot`, `matched`, `score`, a short `why` and `cueOrigin`: ids and cue
  keys, never a copy of a history. Text lives once, in the record.
- Bound (rule 3, first guesses): three by overlap (a sourced cue counts 2) then strength x (1 +
  significance), one of the three kept for a pre-run record; one more by warmth alone when mood is low
  (`slot: "low-mood"`); at most four. At most two `runnersUp`, to answer "why not that?" without listing
  the index.
- The tape's request carries the same ids as `recalled: [{id, origin}]`, so replay and the viewer agree on
  what the model saw.

## 7. Decisions

### 7.1 Alternatives, reasons, constraints and the fallback's formulas

```json
{ "id": "dec:13510:Penny:0", "holder": 2, "opportunity": "opp:13510:Penny:0", "recall": "rc:13510:Penny:0", "visibleAt": 13510,
  "alternatives": [ { "id": "alt:13510:Penny:0/offer", "slug": "offer", "template": { "kind": "plan-offer", "asked": 1, "...": "..." },
      "components": { "formula": "rule5", "base": 0.6, "chance": { "whereabouts": 0.3333, "answers": 1, "followThrough": 1 },
                      "warm": 0.05, "cold": 0.05, "boldness": 0.2, "intended": 0, "cost": 0 },
      "reasons": [ { "kind": "supports", "code": "warm", "value": 0.05, "via": "recall", "cites": ["rd:penny-pam-care", "as:Penny:person:Pam:+:worried-care"] },
                   { "kind": "conflicts", "code": "chance:whereabouts", "value": 0.3333, "via": "expectation", "cites": ["ex:Penny:Pam:..."] },
                   { "kind": "shown", "code": "wary-place", "via": "recall", "cites": ["rd:penny-saloon-wary"], "effect": "none: not this option's object" } ] } ],
  "excluded": [ { "id": "alt:13510:Penny:0/fetch-beer", "slug": "fetch-beer", "why": { "code": "commitment", "cites": ["cmt:penny-no-alcohol"] } } ],
  "constraints": [ { "code": "schedule", "value": "teaching at the Square 10:00-14:00 on weekdays; she leaves about 09:35", "cites": ["h:penny-teaches"] } ],
  "torn": { "value": true, "rule": "6", "why": "warm and cold toward Pam both above TornAt" } }
```

- **Every eligible alternative** with its typed reasons: `supports`, `conflicts`, or `shown` (recalled
  or relevant but not entering the number, with `effect` saying why). `via` names where the reason enters:
  recall, option-side (the alternative's own object), expectation, opportunity, plan, ask, state, routine,
  rule. `cites` must be the holder's own knowledge at that minute; `provenance` may cite sources.
- **Warm and cold stay apart** to the point of evaluation (direction 2.4): each side is its own reason,
  and `components` keeps them separate in the formula.
- **Excluded alternatives** keep a typed `why` (commitment, no-eligible-good, not-open, closed, physical,
  one-open-plan-a-pair, no-reason-holds). An exclusion is shown, never silently dropped.
- **Constraints** list what bounded the set, each with a `code` and a `value`: schedules, windows,
  eligibility, commitments, bounds.

**The fallback's formulas**, stated once here and recomputed by the checker from `components`:
- **Picks and offers (rule 5).** `formula: "rule5"`: weight = base x chance x e^(k x (warm - cold x (1 -
  boldness) + intended - cost)), chance the product of the listed terms, k = `settings.k` (5).
- **Answers to an ask (rule 7).** `formula: "rule7"`, three alternatives:
  - **yes**: `obliged` and `costs` (named parts), `margin` = obliged - the sum of costs; weight e^(k x margin).
  - **no**: the same `margin`; weight e^(-k x margin).
  - **not now**: `reason: {code, value}`; weight e^(k x value), eligible only while a reason holds (low
    mood, tiredness, a duty due); otherwise excluded as `no-reason-holds`. Rule 7 names the reasons but
    not their size: the contract sizes a reason as **the Ask's own state cost** (`StateCost`,
    `Simulation.Ask.cs:30-31` [built]: 0.3 x (tired + busy + low) x (1 - eagerness x bond)), the same
    number that is the yes side's `costs.state`. That sizing is our reading (E), a first guess.
  - The parts come from the Ask as coded: `Ask(Invitation)` [built] (`Simulation.Ask.cs:76-79`) for plan
    answers, `Ask(Errand)` [#90] (`:89-93` at b3224f4: 0.2 x understanding, not chattiness) for errands,
    plus rule 11's displaced-habit cost on a plan answer. The base row the answer writes (`invitations`
    or `favours`) holds its margin and state; the decision links it in `choice.base` instead of copying
    it, and the checker requires the same margin, answer and minute.
  - For Pam's 09:11 answer this gives yes 0.80, not now 0.17, no 0.03 (her low mood is a reason).

### 7.2 One set for both methods (direction 4.4)

The alternative set is built once, before any method is chosen, and both methods answer it:
- `choice.fallback` is **always** recorded: `weights`, `probabilities`, and `wouldSelect` under the same
  draw, even when Laya answered. The viewer can show "the fallback would have chosen keep" without
  running anything.
- `choice.laya.probabilities` (when Laya answered) has exactly the eligible ids.
- The tape's request carries the same set as the decision: `alternatives` (slug and every reason's kind,
  code and value), `excluded` (slug and reason code) and `constraints` (code and value), so Laya sees what
  the fallback weighs.
- `provenance.alternativeSetHash` is FNV-1a 64 (as `Rng.Hash`, `Rng.cs:11-24`) of the canonical JSON of
  `{eligible: [{id: slug, reasons: sorted [kind, code, value]}] sorted by slug, excluded: sorted [slug,
  code], constraints: sorted [code, value]}`. The request carries the same hash, and the checker
  recomputes it from the decision and from the request.
- "Not now" and "no" are eligible answers for the fallback as for Laya. The fixture's fallback answers
  include a weighed no (Pam to Gus's flour) and a not now (Gus to Pam's game).

### 7.3 The choice

```json
"choice": { "visibleAt": 13511, "backend": "laya", "selected": "alt:13510:Penny:0/offer",
  "probabilities": { "alt:...offer": 0.46, "alt:...keep-evening": 0.54 },
  "sampled": { "stream": "lives-choice", "parts": ["dec:13510:Penny:0"], "unit": 0.163, "order": ["alt:...offer", "alt:...keep-evening"] },
  "fallback": { "weights": { "...": 0.2103 }, "probabilities": { "...": 0.1407 }, "wouldSelect": "alt:...keep-evening" },
  "laya": { "probabilities": { "...": 0.46 } },
  "provenance": { "alternativeSetHash": "fnv1a64:...", "tape": "reflection:1:opp:13510:Penny:0",
                  "requestHash": "fnv1a64:...", "model": "model:laya:typed-decisions", "thought": { "backend": "authored", "imagined": true } } }
```

- `backend`: `laya` or `fallback-keyed-draw`. Laya answers only torn choices (rule 6), at most
  `settings.modelQuestionsPerPersonDay` (2) a person-day; a torn choice past the cap is answered by the
  fallback with `cap: {reached, limit, used}`, never silently. A reflection that opens on a choice that is
  **not** torn gives only the thought: the keyed draw answers, the tape keeps the thought with the
  fallback's weights, and the decision cites that reflection with no model (Gus at 18:20 in the fixture).
- `sampled`: one draw, `Rng.Unit(seed, "lives-choice", decision id)` (`Rng.cs:35-37`), walked through
  `order`. **Every Lives choice uses this key, whether or not a reflection opened**; today's quiet
  reflections keep `reflection-choice` and their request id (`Simulation.Reflection.cs:289`), which Lives
  choices never use, so opening a reflection never changes the draw. The key depends on history only if
  history changes whether the opportunity or decision exists (rules 8a-8c open on recalled records; rule
  4 gives today's draw when nothing is recalled): a comparison where it does is invalid (section 10). The
  fixture's units are invented and say so (`unitSource`).
- `selected` is the **intention**; what was tried is an attempt (section 8).

### 7.4 Replay and model provenance

- The tape is the recording's `reflections` (today, `RecordedReflectionMind`, `Reflection.cs:159-205`:
  matched by id and the whole request, refused on any difference) plus the new `choiceQuestions`, which
  have the same shape (`request`, `answer`, `choice`) and the same refusal rule. Tapes are model
  answers, not observer records (section 11).
- `requestHash` is FNV-1a 64 of the tape request's canonical JSON (keys sorted), so the viewer and the
  checker can confirm the decision points at the request the model saw.
- **Answers reuse `ReflectionAnswer`'s fields** (`Reflection.cs:124-127`): `weights`, `backend`, `note`,
  `layaPrompt`, `layaResponse` and `evaluations` (each pass's prompt, response, label mapping and weights,
  `ReflectionEvaluation`, `:118-120`), in `reflections[].answer` and `choiceQuestions[].answer` alike. New:
  `model` (a `lives.models` id) and `evaluationMode` (`Raw`, `Canonical` or `Balanced`,
  `ReflectionMindOptions.cs:5, 18`; today it appears only inside the note string,
  `ResilientReflectionMind.cs:91`). The checker requires the last pass's weights to be the answer's. The
  fixture's prompts and responses are placeholders.
- Today the recording keeps a backend string (`"authored+laya"`, `ResilientReflectionMind.cs:89`) but no
  model name or server version; the default name is `typed-decisions` (`ReflectionMindOptions.cs:11`) and
  the sidecar was checked against laya-serve 0.3.22 (`sidecar/README.md`). A real run should record what
  the server reports (VERIFY at implementation).
- A generated or authored **thought** is recorded as imagined (`thought.imagined: true`); it is never
  evidence and is never cited by a reason, perception or learning update.

## 8. Intention, attempt, world outcome, perceptions

Four things are kept apart:

| Element | Holds | Fixture example |
|---|---|---|
| **Intention** | `decision.choice.selected` | Pam selects `alt:14010:Pam:0/saloon` |
| **Attempt** (`att:`) | `actor`, `target`, `kind`, the `decision` and `alternative` it follows (or `decidedBy` for a base mechanism), `attempted`; from `resolvedAt`: `status` (answered, busy, completed, unsuccessful), `world` (what physically happened to the attempt), `outcome` | Penny waits at home from 18:01; at 19:01 the attempt resolves unsuccessful |
| **World outcome** (`out:`) | `about`, `attempts`, `plan`, `participants`, `observerOnly: true`, **no knowers**; from `settledAt`: `kind`, `world` (observer truth, such as whereabouts spans), `mustNotWrite`, `determinedBy`, `base`, `settledLabel`; `timeline` entries each from their own `at` | `out:14040:Pam:0`: visible at 18:00 with only what is true then (neither is home yet); settles at 21:20 as `agreed-absent` |
| **Perception** (`per:`) | `who`, `of`, `causes`, `perceived`, `unknown` (status "unknown"), `inferences` (`guess`, `certainty`, `basis`, `establishes: false`), `notPerceived`, `notAsserted`, `observerLinks` | Penny: agreed, absent; where Pam went unknown; guesses the Saloon, establishing nothing. Gus: Pam missed 15-18, seen arriving 18:05. Pam: on her way to the Saloon; Penny's wait not perceived |

**Plans** (`plan:`) hold only the terms both members share: `members` (who and role), `window` (from,
until and its basis, intended future minutes), `place`, `activity`, `answer`, `origin` (the asker's
attempt), `formal: false` and `knownBy` (both members, from the answer). An agreed evening is informal,
not a `Promise`, and may still disappoint (A, direction 4.3). Each member's follow-through belongs in their
own decision and attempt, never in the plan, and the plan never links its outcome: the outcome points at
the plan.

**Lives plan asks and the `invitations` table.** A plan ask is `Ask(Invitation)` (rule 11). With
Invitations on, a yes or a no writes an `invitations` row at the answer minute (base shape,
`Simulation.Invitations.cs:7, 40-44`): a no carries the `Refused` act, as today; a yes carries `actId:
-1`. A not now writes no row (section 16, point 1). Lives links rows as `invitation:<tick>:<Asker>:<Asked>`
from the decision's `choice.base` and the outcome's `base`.

**Outcome kinds** (rule numbers from the assessment's 9.3-9.4; `mustNotWrite` lists what each must not
produce):

| Kind | Arises | Must not write | Fixture |
|---|---|---|---|
| `refused-by-commitment` | the asked person's commitment decides, whatever the margin; it answers at once (9.7: it short-circuits as line 99 does; that no pending minute applies is our reading of rule 7) | a probabilistic yes; coldness in the refuser's records | Penny to Pam's beer |
| `declined` | a weighed no (fallback or Laya), with the base `Refused` act | an association for the asker; a fixed wait | Pam to Gus's flour, by the fallback (about 1 in 10 at that margin) |
| `not-now` | a weighed not now, either method (rule 7) | the `Refused` act; a base row | Gus to Pam's game, mid-shift (the reason is contingent: below) |
| `busy` | an errand asker's draw lands on someone at their job place in working hours (rule 16, errand asks only, as the assessment reads #91's answer 7); no ask is made | an answer, a refusal, regard, dread | Gus would ask Penny for flour at 10:20; she is teaching at the Square |
| `agreed` | a yes; a plan for both | | Pam to Penny's evening |
| `agreed-absent` | a yes, then the other not at the place in the window | a refusal, rejection or betrayal; `LetDown`, `StoodUp` or a `Promise(Date)`; where they went, except as Inferred; regard; a feeling for one who expected nothing | Pam at the Saloon from 18:03 |
| `not-found` | a search that did not find | anything but whereabouts | |
| `together` | the planned act completed | company-per-chat warmth | |

**Whether being at work stops or weighs a non-errand ask is rule 16's open point** ("can wait" in the
assessment). The fixture reads Gus's being mid-shift as a not-now reason for a game (`busy` 1 in his
`AskState`, a duty in hand; today `busy` means work due within 60 minutes, `Simulation.Invitations.cs:60-61`)
and marks that answer contingent: if work stops asks, it would be `busy` instead.

**Unresolved, unknown, omitted.** `lives.unresolved` lists what is open at the excerpt's end (needs,
plans), each with an `asOf` and a status; perceptions mark `unknown` and `notPerceived` instead of
inventing a link; `lives.excerpt.omitted` names what a fixture leaves out (here, among others, per-holder
plan copies: 9.2 writes a plan for each member, with its outcome; one shared plan stands in, and each
member's outcome is shown in their own perception). A missing link is written as missing.

## 9. Learning

```json
{ "id": "lu:14045:Gus:0", "who": 0, "visibleAt": 14045, "record": "ex:Gus:Pam:whereabouts:Saloon:18-21:weekday",
  "rule": "9.4 seen", "previous": {"entries": 6, "lived": 5, "seeded": 1, "found": 6, "chance": 1},
  "new": {"entries": 7, "lived": 6, "seeded": 1, "found": 7, "chance": 1, "last": {"day": 9, "outcome": "found", "source": "Seen"}},
  "evidence": ["per:14045:Gus:0"], "preserved": ["rd:gus-expects-pam-evenings"] }
```

- `record` is an association or expectation **held by `who`**; `evidence` is `who`'s own perceptions.
  A world outcome the learner did not perceive can never be evidence.
- `previous` and `new` are state summaries (for an expectation: entries, lived, seeded, successes,
  chance, with what entered and what left; for an association: strength and name). Each update's
  `previous` is the record's last state, and the seeded entry never leaves (section 5). `preserved` names
  the reading that seeded the key: here Gus's evening key goes from six entries to seven, seven found, and
  `rd:gus-expects-pam-evenings` stays.
- **Learning starts at the first relevant outcome** (direction 4.2): each outcome moves the chance at
  once. `settings.derivedReadingAfter` (3) is the tunable heuristic for when a `Derived` reading summarises
  a key, not the start of learning.
- **Disappointment (direction 4.3; the assessment's 9.4, proposed; O3 as it leans).** An agreed evening
  missed writes, for the one who waited, D x expected x invested (first guesses). *Expected* is the plan's
  chance as she held it at the yes: her whereabouts key for the window (0.3333), her answers key after
  Pam's yes (1) and her follow-through key with no entries yet (1), listed in `basis.expectedTerms` and
  each checked against that record as it stood at 09:12. *Invested* is her intended 0.2 plus her warmth
  toward Pam (0.05, `as:Penny:person:Pam:+:worried-care`) over `settings.disappointmentScale` (0.25): 1.
  D is `settings.disappointment` (0.05). So 0.05 x 0.3333 x 1 = 0.0167, written once, as mood and as one
  cold entry toward the hoped-for evening, a situation held as cue keys
  (`as:Penny:situation:evening-at-home-with-Pam:-:disappointed`, its `situation` Pam, the game, the
  Trailer and evening), cause the plan. That entry is read only by her own keep pick at a later plan for
  such an evening (rule 12), and recalled as a reason; an offer to Pam reads her sides toward Pam and the
  chance, so the absence lowers it once, through follow-through (9.5). The fixture's 0.3333 is below the
  assessment's 0.5 because this Penny also holds a lived miss from day 0 beside p4's entry. The update
  carries `notAsserted: ["rejection", "betrayal"]` and `interpretation: {formed: false}`; regard does not
  change (no act was perceived); no "no" entry and no whereabouts entry are written (the plan, not habit,
  set where Pam was to be, 9.5). Were Sid to choose the person as the target (O3), the entry's object
  would be `person:Pam` and every later choice about Pam would read it.

## 10. Matched comparisons

A comparison holds one decision's inputs fixed and changes one history:

```json
{ "id": "cmp:13510:Penny:0:without-p4", "scope": "isolated-choice", "decision": "dec:13510:Penny:0",
  "intervention": { "field": "history", "remove": ["rd:penny-saloon-wary", "rd:penny-expects-pam-away"], "evidenceKeys": ["p4"] },
  "validity": "valid: the opportunity (rule 10) and the decision exist in both arms",
  "heldConstant": ["worldState", "availableActions", "otherPersonalState", "randomInput", "method"],
  "arms": [ { "id": "arm:with", "inputs": { "history": [...], "randomInput": { "stream": "lives-choice", "parts": ["dec:13510:Penny:0"], "unit": 0.163 }, "...": "..." },
              "derived": { "opportunity": {...}, "decisionExists": true, "recall": [...], "reasons": {...}, "probabilities": {...}, "sampled": "..." } },
            { "id": "arm:without-p4", "inputs": {...}, "derived": {...} } ],
  "unavoidableDifferences": [ { "field": "recall", "why": "..." }, { "field": "chance", "why": "..." } ],
  "differs": { "reasons": true, "probabilities": true, "sampledAction": true },
  "wholeRun": { "simulated": false, "note": "..." } }
```

- **Inputs** are what a decision is built from: `history` (the intervention field), `worldState`
  (minute, place, who is present, the ask), `availableActions` (eligible and excluded ids),
  `otherPersonalState` (traits, mood, lived entries, commitments), `randomInput` (stream, parts, unit),
  `method` (backend and constants). Every input but the intervention is listed in `heldConstant` and is
  byte-equal across arms; the history differs by exactly `intervention.remove`.
- **Validity.** Each arm's `derived` states its `opportunity` and `decisionExists`. A comparison is
  invalid, and the checker fails it, when the intervention changes the opportunity or whether the decision
  exists: the alternatives, the draw's key and the method would then change with the history.
- **Derived** fields follow from the inputs by the rules: seeds, recall, the torn test, chance terms,
  sides, each alternative's `reasons`, weights, probabilities, the sampled alternative. Reasons, weights,
  probabilities and the sampled action are the effects being measured; any other derived difference must
  be listed in `unavoidableDifferences` with why. The "with" arm reproduces the decision's own reasons and
  fallback probabilities.
- **Reasons, probabilities and the sampled action are reported separately** (`differs`): a history can
  change reasons or probabilities without changing the draw. Removing p6 takes the offer's warm reason
  away and moves the fallback's offer from 0.141 to 0.113; both arms draw keep-evening. Removing p4 moves
  the offer's whereabouts reason from 0.333 to 0.5 (dropping its p4 cite) and its shown Saloon reason, the
  offer from 0.141 to 0.197, and the same unit (0.163) now draws offer.
- **Method is held constant.** Both arms use the fallback, because removing p6 also makes the choice
  stop being torn, which in a Laya run would change the method as well as the history; that is listed as
  an unavoidable difference. A method comparison (Laya against the fallback on one alternative set) needs
  no arms: every decision records both (section 7.2). The run's own choice used Laya (`runChoice`).
- **Isolated is not whole-run.** `scope: "isolated-choice"` arms are never run forward. A whole-run
  comparison (`scope: "whole-run"`, two `runId`s and the first minute their logs differ) is a different
  object, out of v0.1's fixture. The fixture's p4 note shows why the two must not be confused: run from
  the start without p4, rule 14 would have written misses on days 0 and 1, reaching the same 1/3 by day 9,
  so the isolated difference could vanish (reasoned, not run).

## 11. Descriptive only: enforcement and tests [proposed]

**Design.** The simulation writes Lives observer records through a write-only sink (an interface whose
methods return nothing), receiving immutable copies. Rules read only engine stores (facts, readings,
associations, expectations, plans, commitments). `SimResult` exposes the sink's records after the run;
nothing inside `Simulation` holds a reader for them. The only recorded data read back into a run is a
**tape** (`reflections`, `choiceQuestions`), read only by the recorded mind and only when a freshly built
request matches it exactly; tapes live outside `lives`.

**A precedent to avoid.** `appraisals` are described as observer data, yet `SceneOf` reads `_appraisals`
into reflection requests (`Simulation.Reflection.cs:195-196`) [built], so under a model they can shape a
choice. Lives requests are built from engine records, never from the sink or from appraisals.

**Tests to add with the slice:**
1. Recorder on and recorder discarding everything give the same `Metrics.LogHash` (the pattern of
   `EngineCapturesRolesAndUnknownDetailsWithoutChangingDeterministicRun`,
   `sim/UnderGlass.Sim.Tests/EncounterTests.cs:99`).
2. A sink that scrambles its copies (reverses lists, zeroes numbers) leaves the hash unchanged.
3. Architecture: the sink type exposes no member returning data, and no rule file references the
   observer record types (a reflection or source scan over `UnderGlass.Sim`).
4. Watched and unwatched runs are equal (no camera: the viewer's lens never reaches the sim).
5. `Lives` off is byte-identical to today (pinned hashes), and the recording has no `lives` key.
6. Replay: a recorded run replays to an identical `lives` block; an edited tape is refused.
7. The fixture's checker, run on recorded output in CI once the slice exists.

## 12. The playback clock and the two lenses

**Clock.** Every element has `visibleAt`: a minute, or `"pre-run"` (before minute 0). Nothing is visible
before its causes, and **nothing inside an element is later than the minute it is shown**: every minute
value (`at`, `from`, `to`, `until`, `arrived`, `waitedUntil`, `firstSeen`, `tick`, ...), every `day` and
every referenced id must be at or before its gate. The gate is the element's `visibleAt` except for these
declared time-gated fields:

| Element | Field | Shown from |
|---|---|---|
| decision | `choice` | `choice.visibleAt` (the answer: the next minute for a model or a pending ask, `Simulation.Reflection.cs:5-6`) |
| attempt | `status`, `world`, `outcome` | `resolvedAt` |
| world outcome | `kind`, `world`, `mustNotWrite`, `determinedBy`, `base`, `settledLabel` | `settledAt` |
| world outcome | each `timeline` entry | its own `at` |
| record | each `knownBy` entry | its own `availableAt` |
| expectation | `initial` | `initial.asOf` |
| thread | each `summary` line | its own `visibleAt`; `stages` is an index of ids, each shown from its own minute |
| `lives.unresolved` entry | all | its `asOf` |

**Terms** (an alternative's `template`, a plan's `window`) name minutes someone intends; they may lie in
the future and are exempt. The checker enforces all of this. These are different clocks: the observer
sees at 18:00 that neither Penny nor Pam is home; the outcome's kind (`agreed-absent`) shows only at 21:20,
when the window closes; Penny perceives Pam's absence at 19:01 and never where she went.

**Two lenses** (a proposal for Codex; the viewer already gates reflections and stories by the clock,
`reflectionAt` and `storyCutoff`, `index.html:2306` and `:2529`, with the note at `:2581`):
- **Observer lens** (privileged): everything at or before the clock under the gates above, world
  outcomes included, marked observer-only. Whole-run analysis (comparisons, later learning) unlocks only
  by an explicit control, as the stories panel already does.
- **Character lens** (one person): only records and plans listing them in `knownBy` with `availableAt` at
  or before the clock (these hold only what every knower knows, section 4); their own opportunities,
  recalls, decisions, attempts, perceptions and learning; attempts aimed at them; never world outcomes,
  other people's readings or decisions, or observer provenance. Where the character has an `unknown` or an
  inference, the lens shows that, not the truth.
- **Progressive disclosure:** a thread's summary line at the clock; then its stages (history, recall,
  alternatives, attempt, outcome, perceived, learning, comparison); then one element; then its reasons
  and cites; then provenance (sources, tape, model). Scrubbing backwards removes what is no longer
  visible. Index structures hold ids and minutes only, never future text (the viewer's rule today,
  `index.html:1353`).

## 13. Bounded recall and larger populations

Per decision the block holds ids, codes and numbers: recalled ids (at most four), alternative ids with
reason codes and cited ids, the fallback's components, probabilities and the draw. Histories are cited,
never copied; text is a short `label` the viewer could equally render from codes.

**Measured on the fixture** (compact JSON, labels and notes stripped): a decision about 2.3 KB, a recall
0.65 KB, an opportunity 0.17 KB, an attempt 0.36 KB, a world outcome 0.44 KB, a perception 0.31 KB, a
learning update 0.35 KB, a pre-run record about 0.44 KB, a comparison about 5.6 KB (it now carries each
arm's reasons), a tape entry about 1.9 KB with the fixture's placeholder prompts. The fixture's readable
form repeats long ids and labels.

**Estimate per person-day**, using the assessment's section 11 counts (7.4 picks, of which about 3-4 recall
something; asks under 0.5; plans under 0.2; 1-3 classified outcomes; 5-20 passive sightings):
- Readable form: about 4 decisions and recalls (12 KB), opportunities and attempts (1.4 KB), outcomes and
  perceptions (1-2 KB), learning (2-3 KB): **about 16-18 KB a person-day**, 110-120 MB for a 60-person,
  112-day year. Too large to embed whole, beside today's 72.6 MB for that run.
- **Compact rows** (as `acts` are: alternatives as arrays in a fixed order, slugs instead of full ids,
  reason codes as small integers, no labels): about a third, **4-6 KB a person-day**, 25-40 MB for that
  year, in line with the scaling inspection's 12-25 MB estimate for threads at its lower counts.
- **Aggregate passive sightings:** one learning update per holder, key and day when a sighting only
  confirms an expectation; without that, 5-20 sightings a person-day would dominate.
- **Load lazily:** the viewer reads one person or one week of `lives` at a time; a split file is a
  candidate for 0.2 (section 16).
- Model questions are capped (2 a person-day) and only torn choices ask; real tape entries are 3-10 KB
  each (scaling inspection), recorded beside `reflections`.

All of these are estimates to be measured with the slice: added time, memory and JSON size separately
from today's baseline, as direction 4 asks.

## 14. Archetypes

Codex's archetype browser and editor covers reusable dispositions, coping, voice, source evidence,
character assignments and links to authored speech. This contract keeps lives out of it:
- `lives.characters[]` references an archetype **by id only** (`archetype: "steadfast-carer"`; the ids
  are the pack's `characters[].archetypeId` in `sim/character-inspiration.json` [built]: Penny
  steadfast-carer, Pam weary-defender, Gus convivial-keeper). The block never contains an archetype
  definition.
- Facts, accounts, readings, commitments, associations and expectations belong to a character index.
  `lives.characters[].history` lists the pre-run records a character holds, by id.
- Duplicating an archetype copies its dispositions and evidence links and **no record**: a new character
  starts with no history but its own (generated under the same rules, later), and a donor's biography
  never travels with the archetype (the audit's acceptance case 6). The checker refuses a record that
  names an archetype or an archetype entry that carries history.

## 15. Every addition against the existing recording

| Field | Status | Reuses |
|---|---|---|
| `lives` (whole block) | new, additive, Lives on only | people as `names` indices; minutes as `clock`; acts, favours, invitations, needs by id |
| `lives.contract`, `.version`, `.rules`, `.runId`, `.status`, `.illustrative`, `.note`, `.assumes`, `.excerpt` | new | `format`, `version`, `hash`, `label` as models |
| `lives.settings` | new (also `settings["LivesOptions.*"]`) | the `Scalars` pattern |
| `lives.models` | new | backend strings in `reflections[].answer.backend` |
| `lives.external.archetypes`, `lives.characters` | new | `characterInspiration` provenance; the pack's archetype ids |
| `lives.sources` | new | the assessment's keys; the pack's `sources` |
| `lives.records` (fact, account, reading, commitment, association, expectation) | new | `sentiments` (associations extend them: named, signed, caused); `beliefs`' `Source` (Witnessed, Told, Found, `Model.cs:253`) mapped into `how` |
| `lives.opportunities`, `lives.recalls` | new | `reflections[].request.opportunity` (kind values added) |
| `lives.decisions` | new | `pursuits` (margin, call) as the gate's analog; `reflections` weights and draw; the Ask's parts; `invitations` and `favours` rows linked by `choice.base`, not copied |
| `lives.attempts`, `lives.plans` | new | `encounterCauses` (method, reflection id); `promises` [#90] (a plan is not one) |
| `lives.outcomes` | new, observer only | `life` outcomes (`Outcome`, `Model.cs:342`), `favours` [#90], `invitations` |
| `lives.perceptions` | new | `appraisals[].account` (one holder's account at acquisition) |
| `lives.learning` | new | `feelings` (cause-linked changes) |
| `lives.comparisons`, `lives.threads`, `lives.unresolved` | new | none |
| `choiceQuestions` | new top-level tape | the `reflections` record shape, answers included |
| `reflections[].request.recalled`, `.alternatives`, `.excluded`, `.constraints`, `.alternativeSetHash` | new optional fields | `ReflectionRequest` (`Reflection.cs:107-114`) |
| `reflections[].request.opportunity.kind` values `refused-by-commitment`, `asked` | new values | `ReflectionOpportunity` (`Reflection.cs:37`) |
| `reflections[].answer.layaPrompt`, `.layaResponse`, `.evaluations` | existing, reused for Lives answers and in `choiceQuestions[].answer` | `ReflectionAnswer` (`Reflection.cs:124-127`), `ReflectionEvaluation` (`:118-120`) |
| `reflections[].answer.model`, `.evaluationMode` | new optional fields | `ReflectionMindOptions.EvaluationMode` (`ReflectionMindOptions.cs:18`) |
| `reflections[].request.choices[].kind` values `PlanOffer`, `PlanAnswer` | new values; empty kind still means no act | `ReflectionChoice` (`Reflection.cs:20`) |
| `invitations` rows from Lives plan asks; `actId: -1` on a yes | new rows and one new value | `Invitation` (`Simulation.Invitations.cs:7`) |

## 16. Open points

**For coordination with Codex** (contract shape, not product):
1. Busy and not-now answers have no base row: `favours` and `invitations` mean refusal by
   `accepted: false` with a `Refused` act. v0.1 keeps them in `lives` only, and a Lives yes writes its
   invitation row with `actId: -1`; 0.2 could add an `answer` field to those rows.
2. Compact row encoding for `decisions`, `recalls` and `learning`, and whether `lives` may live in a
   sibling file loaded on demand.
3. Whether labels are stored at all in real runs, or rendered by the viewer from codes.
4. Thread summaries are observer text; under the character lens the viewer should build summaries from
   that character's own elements.

**Agreed with the assessment** (its fourth revision, reconciled with this contract on 2026-10-14):
- Pam's fallback answer is three-way under rule 7: yes 0.80, not now 0.17 (her state cost, 0.02, is the
  not-now reason while it holds), no 0.03 (its section 8 and 9.4).
- Disappointment is 9.4's D x expected x invested, one entry toward the hoped-for evening, a situation
  (O3 as the assessment leans; section 9 here).
- An agreed evening writes one follow-through entry and no whereabouts entry (9.5).
- 9.2's `Plan` is written for each member with an outcome; this excerpt keeps one shared plan and shows
  each member's outcome in their own perception (section 8), so the plan never holds a private
  follow-through.

**Product decisions.** This contract adds none. The fixture assumes what its `lives.assumes` lists: the
decided items (A, 2026-10-14: Penny's evening place as her data; the agreed evening informal and able to
disappoint), O1(a) (the sim's bedtime), O2 as the assessment leans (present readings with a stance seed
one entry; the hobby wish opens offers but gives no warmth), O3 as it leans (the hoped-for evening as
the target), and the "can wait" defaults (the Pam-Gus
friend seed 0.4; rule 16 for errand asks only, with Gus's mid-shift answer marked contingent on its open
point). It settles none of them.

## 17. The fixture and the checker

**The fixture** (`lives-observer-example-v0.1.json`, illustrative) is shaped like a recording: an excerpt
of base fields (three names, four places, two acts, one invitation row; two needs and two favours [#90];
three reflection and two question tape entries) and the `lives` block. Day 9 (spring 10, a Wednesday),
minutes from day 0:

| Minute | Clock | What |
|---|---|---|
| 13510 | 09:10 | Pam asks Penny for beer; the commitment says no at once (act 412); Penny's reflection opens; she recalls p5, p4, p6 |
| 13511 | 09:11 | Laya picks the offer (0.46; the fallback's 0.14 would have kept her evening); Penny asks Pam (a pending ask); Pam's reflection opens |
| 13512 | 09:12 | Pam says yes (Laya 0.71; fallback 0.80); an invitation row; the plan, 18:00-21:20 at home |
| 13580 | 10:20 | Gus's flour need, at the Square before his shift; his draw lands on Penny, teaching: busy, no ask (rule 16) |
| 14010 | 17:30 | Pam at home, follow-through: the Saloon (Laya 0.62; fallback 0.58); she leaves at 17:31 |
| 14028 | 17:48 | Penny at the Beach, follow-through: home (Laya 0.81) |
| 14040 | 18:00 | the window opens; neither is home yet; Gus records a miss for 15-18 |
| 14041 | 18:01 | Penny is home and waits |
| 14045 | 18:05 | Gus sees Pam arrive: "Pam arrives late"; his evening key keeps its seeded entry |
| 14055 | 18:15 | Gus asks Pam to fetch flour (a pending ask; her 8a budget is spent) |
| 14056 | 18:16 | the fallback answers no (about 1 in 10; act 431) |
| 14060 | 18:20 | Pam, ordinary warmth toward Gus (m7), offers a game (torn, past her model cap: the fallback); Gus's reflection opens, not torn |
| 14061 | 18:21 | the fallback answers not now: he is working (contingent on rule 16's open point) |
| 14101 | 19:01 | Penny's hour of waiting ends: agreed, absent; where unknown, a guess of the Saloon that establishes nothing; two learning updates: one follow-through entry, absent, and one disappointment toward the evening; no whereabouts entry |
| 14240 | 21:20 | the window closes; the outcome settles as agreed-absent |

Two comparisons on Penny's 09:10 choice: without p6 (reasons and probabilities move, the draw does not)
and without p4 (the draw changes).

**The checker** (`node check-lives-observer.cjs <file>`; exit 0 pass, 1 fail, 2 usage) checks: the shape
and versions; the illustrative labels and note; that every id is unique, has its section-3 form and every
whole-string reference in `lives` resolves, people included; the clock (no element before its causes;
every minute, day and referenced id inside an element at or before its gate, with the time-gated fields of
section 12; knowers not before the record exists); knowledge (every recall, reason, opportunity cause,
perception cause and inference basis is known to its character at that minute; learning changes only the
learner's own records on the learner's own perceptions; world outcomes have no knowers and are never
cited; shared records and plans hold only what each knower knows; `ProductDecision` only with standing A,
`Authored` only with E; a disappointment sized by 9.4's formula from the holder's own keys as they stood at
the yes, her intention and her warmth, and aimed at a situation; no whereabouts entry from an agreed
evening); expectation windows (lived entries within `livedWindow`, the seeded entry kept, each update
starting from the record's last state, chances against entries); recall bounds and that
recalled items carry no copies; one alternative set for both methods (probability keys, the set hash over
reasons, exclusions and constraints, and the tape request carrying the same); fallback weights recomputed
from rule 5 or rule 7, not-now sized as the state cost, base rows agreeing on margin, answer and minute;
the draw keyed by `lives-choice` and the decision id, against the unit for the method used and for the
fallback; Laya only on torn choices, within the daily cap, with a tape entry whose canonical hash matches
`requestHash`, whose alternatives, reasons, recalled ids, weights, model and evaluations agree; a fallback
choice naming at most a reflection tape without a model; attempts following the selected alternative;
comparisons (valid, held-constant inputs equal, only the intervention differing, by exactly the listed
history, derived differences listed, reasons and draw consistent, `differs` correct, the "with" arm
reproducing the decision's reasons and fallback); and archetypes referenced by id, carrying no life.

On the fixture it passes. A mutation run of 44 deliberate faults, at least one per check (among them each
problem the check of this contract found: a seeded entry dropped from the window, a result shown before
`resolvedAt`, an outcome's kind before it settles, a plan linking its outcome or holding a member's private
follow-through, Pam's `obliged` set to 0.1, a not-now reason that is not the state cost, an invitation row
with another margin, a draw keyed by the reflection stream, a tape without constraints, a misnumbered
opportunity, an association id without its name, an invalid comparison, `differs.reasons` misreported,
Sid's commitment filed as `Authored`, a disappointment sized by an earlier formula, one read from a key
as it was not at the yes, one aimed at Pam rather than the evening, a whereabouts entry written from the
agreed evening), was caught 44 of 44. The fixture's generator and the fault suite are beside the contract
in `lives-observer-v0.1-tools/` (`node build-example.cjs ../lives-observer-example-v0.1.json`;
`node mutate-example.cjs`).
