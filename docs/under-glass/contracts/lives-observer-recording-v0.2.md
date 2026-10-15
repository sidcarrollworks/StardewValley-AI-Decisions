# Lives observer recording: contract v0.2 (proposed)

Proposed 2026-10-14, for Sid's fifth direction (section 3: "revise the observer contract (next revision 0.2) before
publishing a runtime writer"). **Nothing here is implemented.** No Lives switch, record, rule or recorder exists in
code; the fixture beside this file is generated and labelled illustrative. Contract 0.1 is frozen and unchanged
(`lives-observer-recording-v0.1.md`); Codex's chain viewer reads 0.1 only. This document is self-contained: a reader
need not open 0.1. What changed, why, and what it costs Codex's adapter is in `lives-observer-changes-v0.2.md`, with
the implementation sequence; breaking changes there are for Sid and Codex to agree before Codex replaces its 0.1
adapter.

Files, all in this folder (the index is `README.md`):
- `lives-observer-recording-v0.2.md`: this contract.
- `lives-observer-example-v0.2.json`: an illustrative fixture (Penny, Pam and Gus over Wednesday and Thursday, summer 3-4).
- `check-lives-observer-structure.cjs`: the structural checker; it must pass on real output (section 14).
- `check-lives-observer-rules.cjs`: the experimental pilot-rule checker; readers never require it (section 14).
- `lives-observer-changes-v0.2.md`: the change list from 0.1 and the implementation sequence.
- `lives-observer-v0.2-tools/`: the fixture's generator, `rng.cjs`, the real-like variant (`make-real-like.cjs`), the
  legitimate real-run shapes built from it (`variants.cjs`), two fault suites, `canon-vectors.cjs` (test vectors for
  the canonical JSON and hashes of 9.6) and `reader-lens.cjs`, a reference character-lens reader for section 4.3.

## 0. Labels

| Mark | Meaning |
|---|---|
| **[built]** | In the code on `origin/codex/reflection-prototype` (779e3b8). Line numbers are the errands branch's (a9eadaf), where the pilot is built; the cited code is the same on base except where marked |
| **[#90]** | Only on the errands branch (needs, favours, promises), open PR #90, cited at a9eadaf |
| **[proposed]** | This contract, or the assessment's proposed rules 1-16 (`docs/under-glass/lives-gap-assessment.md`, section 9) |
| **VERIFY** | Recalled or read from an installed copy, not confirmed in source; settle before relying on it |
| **illustrative** | Fixture content that is invented: Laya probabilities, prompts and responses, receipts, thoughts, act and need ids, states such as tiredness, walking times |
| **S, A, E, NA, D; S·R1, S·R2** | Evidence standing, as in the assessment: sourced canon, Sid's dated product decision, ours, not admissible, for the run to derive; sourced content admitted under the audit's rule 1, or placed at the start by our rule-2 choice |

Paths are under `sim/` unless shown. Sid's decisions: directions 1-5 (2026-10-14), D52, and his bedtime decision
(2026-10-14: bedtimes are habits, not hard rules; section 6.5); O1-O3 are decided by the fifth direction (the
assessment's section 14). The audit
(`docs/under-glass/experiments/history-evidence-audit-2026-10-14.md`) is cited, never changed.

## 1. What the block records

The `lives` block is an observer's account of one causal graph per person:

```
history fact / account / reading / commitment
   -> opportunity (why now) -> recall (which records came to mind, by which cues)
   -> decision (eligible and excluded alternatives, reasons, constraints) -> choice (method, probabilities, draw)
   -> attempt (what the person tried) -> world outcome (what happened; observer only)
   -> perceptions (what each person noticed, did not notice, or guessed)
   -> learning (which association or expectation changed, from what to what, on whose evidence)
```

Every arrow is an explicit id link. The block is **descriptive only**: the simulation never reads it (section 15).
Its readers are Codex's chain viewer, Sid reading a personal thread, and the two checkers.

## 2. Where it sits, versioning, storage

### 2.1 The recording today [built]

`Replay.Record` writes one JSON object (`UnderGlass.Sim/Replay.cs:182-317`): `format` ("under-glass-run"), `version`
(1, `:45`), `seed`, `days`, `label` (the command's flags), `hash` (the log hash), `clock`, `settings`, `names` (`acts`
and the other compact array tables hold people as indices into it, `P(...)`, `:233`; the tables serialized from records
hold names: `invitations`, `needs`, `favours`, `promises`, `speech`, `reflections` and the encounter tables, e.g.
`Simulation.Invitations.cs:7`, `Simulation.Errands.cs:8`, `Reflection.cs:108`), `acts` (rows `[id, tick, actor, kind,
place, x, y, target, about, flags, witnesses]`, `:231-236`; an act's id is its position), and further tables. With
their switches: `reflections` (`:280-284`),
`characterInspiration {schema, hash, provenance}` (`:285-287`), `invitations` (`:297-300`); and [#90] `needs`, `favours`,
`promises` (`:301-308`). The dashboard refuses any top-level `version` but 1 and reads optional tables with `?? []`.

### 2.2 What 0.2 adds [proposed]

| Addition | Where | Written when |
|---|---|---|
| `lives` | top level | the proposed `Lives` switch is on |
| `choiceQuestions` | top level, beside `reflections` | Lives on: a torn choice was answered without a reflection |
| `favours[]` and `invitations[]`: `id`, `answer`, `askMade` | existing rows, at the end | Lives on (section 8) |
| `characterInspiration.snapshot`, `.snapshotHash`, `.artifact` | the existing receipt | Lives on and a pack configured (section 11) |
| `reflections[].request`: `recalled`, `alternatives`, `excluded`, `constraints`, `alternativeSetHash` | existing request | Lives on: a Lives opportunity opened a reflection |
| `reflections[].answer`: `model`, `generatorModel`, `evaluationMode`, `receipt`, `fallback` | existing answer, at the end | Lives on, each only when set (section 10) |

Every addition is written only with Lives on, so a Lives-off recording, with or without a pack or reflection, is
byte-identical to today's (section 15). The answer additions are properties at the end of `ReflectionAnswer`, omitted
when null, set where the answer is recorded and only with Lives on. A replay returns the recorded answer unchanged
(`Reflection.cs:192`), additions included, so the tests that pin a replay's reflections to the original's
(`CharacterInspirationPackTests.cs:58`, `CharacterInspirationTests.cs:231`, `ReflectionContinuityTests.cs:91`,
`ReflectionDreamTests.cs:269`) hold with Lives off unchanged and, with Lives on, still compare equal; replay provenance
is the run's (`lives.run.tapeSource`, section 10), never an answer field. The top-level `version` stays 1. Base rows
never point at Lives ids; Lives elements point at base rows.

### 2.3 Versioning

- `lives.contract` is `"under-glass-lives-observer"`; `lives.version` is `"0.2"`. **Readers accept exact 0.x versions
  only** and refuse any other with a notice; 0.1 stays readable by 0.1 readers, which refuse 0.2.
- From 1.0 (once Sid and Codex agree): a minor bump is additive; readers accept the same major at or above their minor.
- `lives.rules` names the rule revision that produced the records; a change to a rule's meaning changes it.
- **Never backfilled.** A recording without `lives` shows "no Lives data"; no reader rebuilds Lives records from regard,
  sentiments or acts. The structure checker passes such a recording after checking that its base rows are readable
  (section 8).
- A fixture sets `illustrative: true` at the top level and in `lives`, with notes saying it is not output from an
  implemented system. A real recording sets neither, and carries `lives.runId` (`run:<seed>:<days>:<log hash>`) and
  `lives.run.source.commit`.

### 2.4 Storage: a logical model behind an adapter (direction 5.3)

This contract defines the **logical model**: readable JSON objects, stable codes and ids, numbers. Text is optional:
`characterLabel` and every prose field under `observer` are diagnostics a writer may omit. **No compact wire format is
frozen.** The writer emits through a write-only sink (section 15) into a storage adapter; the first adapter writes the
objects here into the one-file recording. Compact rows, a sibling file loaded on demand, or person and time chunks may
come later, after measurement, as other adapters that yield the same logical objects. Readers should go through an
adapter (Codex's `chains-model.js` is one) and never depend on byte layout. A chunk declares itself as
`lives.excerpt {from, to, chunk: {adapter, index}}` and behaves as an excerpt does (section 6.4). The sizes 0.1 quoted
were fixture estimates, not measured runtime scaling; section 16 gives this fixture's sizes, also not a runtime
measure.

## 3. Identifiers

Every id is a string `<type>:<body>`, deterministic for a seed, town and options, never reused in a run. Bodies use the
minute and a person's **name**; fields that hold a person use the index into `names`. Ids appear only as whole string
values. The structure checker enforces every form below.

| Type | Form | Fixture example |
|---|---|---|
| Run | `run:<seed>:<days>:<log hash>` | `run:7315:35:illustrative` |
| Source | `src:<slug>` | `src:penny-schedule` |
| Fact, account, reading, commitment | `h:<slug>`, `acct:<fact slug>:<Holder>`, `rd:<slug>`, `cmt:<slug>` | `acct:pam-saloon-evenings:Gus` |
| Association | `as:<Holder>:<object>:<sign>:<name slug>` | `as:Penny:person:Pam:+:worried-care` |
| Expectation | `ex:<Holder>:<Subject>:<key>` | `ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday` |
| Opportunity, attempt, perception, learning, outcome | `opp:`, `att:`, `per:`, `lu:`, `out:` + `<minute>:<Name>:<n>` | `per:45190:Penny:0` |
| Recall, decision | `rc:`, `dec:` + the opportunity's body | `dec:45190:Penny:0` |
| Alternative | `alt:<decision body>/<slug>` | `alt:45190:Penny:0/offer` |
| Plan | `plan:<minute>:<Asker>:<Asked>` | `plan:45192:Penny:Pam` |
| Comparison | `cmp:<decision body>:<intervention>` | `cmp:45190:Penny:0:without-commitment` |
| Thread | `thr:<slug>` | `thr:penny-pam-evening` |
| Model | `model:<family>:<name>` | `model:laya:typed-decisions` |
| Base rows | `act:<id>`, `need:<id>`, `fav:<tick>:<Asker>:<Asked>:<n>`, `inv:<tick>:<Asker>:<Asked>:<n>`, `reflection:<request id>`, `question:<request id>` | `inv:45192:Penny:Pam:0` |

`<Name>` is the person the element belongs to (holder, actor, who, or the outcome's `about`); `<n>` counts that person's
elements of that table in that minute from 0, in `seq` order. A favours or invitations row's `<n>` counts rows with
that minute and pair, in table order, so several answers in one minute never collide. Request ids: a Lives reflection
is `"<seed>:<opportunity id>"`, a torn question without a reflection `"<seed>:q:<decision body>"`.

**`seq`.** Every element carries `seq`: the sink's emission counter, unique in the block and increasing in the order
the simulation wrote the elements (pre-run records first). Readers order the elements of one minute by `seq`; a cause
shown the same minute always has the smaller `seq`. `seq` is ordering metadata: a character-lens reader orders by it
and never displays it, since its gaps count the hidden elements emitted between a person's own.

## 4. Two lenses, and the line between them

### 4.1 The partition (direction 5.3)

Every element separates **character-safe** content from **observer-only** content:

- **Character-safe**: the element's fields outside any `observer` object, from a **closed vocabulary**. Every key is
  declared for that element and nesting level (section 4.2), and every value is an id, a person index, a minute, a
  number, a boolean, or a code (letters, digits and `_ . + @ -`, parts joined by `:`; no spaces). An association's
  `name` is the one spaced value: the holder's word for a feeling, lowercase, at most four words. An undeclared key at
  any depth, or prose where a code belongs, is a structure failure: it belongs under `observer`. The optional
  `characterLabel` is the only character text.
- **Per viewer.** Character-safe content is shown subject to the field gates (section 6) and to the viewer: an element
  with one owner (opportunity, recall, decision, perception, learning update, personal record) is shown whole to its
  owner; an attempt is shown whole to its actor, and a perceiver of it sees only `actor`, `target`, `kind` and `terms`;
  a record or plan shows each knower only their own `knownBy` entry. The "Other viewers see" column of 4.2 is part of
  the rule: a reader that only hides `observer.*` would show Pam Penny's `decision`, `alternative` and label on the
  attempt Pam perceived, and show each knower of a shared fact the other knowers' accounts. `reader-lens.cjs` applies
  the rule; on the fixture it shows no id outside the lens, while a reader that only hides `observer.*` shows 14, and
  the structure suite asserts both.
- **Observer-only**: everything under an `observer` object, at any depth: diagnostic labels, notes, rule names,
  standing and provenance, sources and cues, secret truth (`notPerceived`, `notAsserted`, `observerLinks`, `heard`),
  world truth on attempts, the asker's link to a base row (`observer.rows`), method and draw (`choice.observer`), an
  opportunity's tape link, recall scores, thread titles and summaries.
- `label` at the top level is gone in 0.2 (it moved to `observer.label`), so a 0.1 reader cannot show it by accident.
- `characterLabel` is written only from the element's viewers' knowledge; on a shared record or plan it must hold for
  every knower, never in one knower's kinship voice ("Mom", "my girl"). It never repeats observer-only text and never
  names a typed id. **Label content is not machine-verified beyond that**: a paraphrased secret or a label naming what
  someone the viewer cannot see did passes the structure checker, so labels need review. Without a label, a reader
  builds text from codes ("asked by Pam: errand, beer").
- **Observer-only tables**: `outcomes`, `comparisons` and `threads` are never shown in the character lens and carry no
  `characterLabel`; nor are `lives.unresolved` (pending bookkeeping: `{ref, asOf, status: open | settled}`) and
  `lives.characters` (the archetype panel's input and each person's history index); `sources` and `models` are
  provenance. Their structured truth may sit at the top level as ids and codes (an outcome's `world` and
  `determinedBy.rule` included: a deliberate exception to D2's "world truth and rule names under observer", because
  the whole table is observer-only); their prose goes under `observer` (a model entry's `sources` and `verify` too).

### 4.2 Character-safe fields, by element

Anything else at these levels, or inside these shapes, is a structure failure; it belongs under `observer`. Every
element may also carry `id`, `seq`, `visibleAt`, `characterLabel` and `observer`.

| Element | Character-safe fields (nested shapes in braces) | Other viewers see |
|---|---|---|
| fact | `type`, `participants`, `kind` (Fact, Past, Practice), `place`, `time` {`known`, `at`}, `recurrence` (once, recurring, job), `stillTrueAtStart`, `knownBy`, `causes` | each knower: their own `knownBy` entry only |
| account | `type`, `holder`, `fact`, `mode` (Lived, Witnessed, Told, Inferred), `knownBy`, `causes` | nobody (personal) |
| reading | `type`, `holder`, `object` (an id or a cue key), `stance` (warm, wary, expects, none), `certainty` (stated, guess), `knownBy`, `causes` | nobody (personal) |
| commitment | `type`, `holder`, `scope` {`provide`, `goodsToday`}, `to`, `force`, `revisable`, `since`, `knownBy`, `causes` | nobody (personal) |
| association | `type`, `holder`, `object`, `sign`, `name`, `situation` (cue keys), `causes`, `knownBy`, `snapshots[]` {`asOf`, `state` {`strength`}} | nobody (personal) |
| expectation | `type`, `holder`, `subject`, `key`, `knownBy`, `causes`, `snapshots[]` {`asOf`, `state` {`entries[]` {`at`, `outcome`, `source`, `ref`}, `lived`, `seeded`, `successes`, `chance`}} | nobody (personal) |
| opportunity | `holder`, `trigger`, `causes` (its tape link is observer-only: `observer.reflection` or `observer.question`) | nobody |
| recall | `holder`, `opportunity`, `items[]` {`record`, `slot` (top, low-mood), `matched`} | nobody |
| decision | `holder`, `opportunity`, `recall`, `question`, `alternatives[]` {`id`, `slug`, `template` {`kind`, `asked`, `place`, `activity`, `routine`, `from`, `until`, `arrive`}, `components` (section 9.2), `reasons[]` {`kind`, `code`, `value`, `via`, `cites`, `effect`}}, `excluded[]` {`id`, `slug`, `why` {`code`, `cites`}}, `constraints[]` {`code`, `value`, `terms` {`from`, `until`}, `cites`}, `choice` {`visibleAt`, `selected`, `thought` {`tape`, `imagined`}, `base`} | nobody |
| attempt | `actor`, `target`, `kind`, `decision`, `alternative`, `decidedBy` {`mechanism`}, `base`, `terms` {`place`, `activity`, `from`, `until`, `waitUntil`}, `knownBy`, `resolvedAt`, `status` | a perceiver of it (5.2): `actor`, `target`, `kind`, `terms` only; no `characterLabel` |
| plan | `origin`, `answer`, `formal`, `place`, `activity`, `window` {`from`, `until`}, `members[]` {`who`, `role`}, `knownBy` | each member: their own `knownBy` entry only |
| perception | `who`, `of`, `causes`, `perceived` (by kind, below), `unknown[]` {`about` (whereabouts, reason, answer), `subject`, `status`}, `inferences[]` {`about`, `subject`, `guess`, `certainty`, `basis`, `establishes`} | nobody |
| learning | `who`, `record`, `evidence`, `previous` and `new` (the record's own state shape, nothing else), `added`, `dropped` (entries), `preserved`, `mood` {`change`}, `interpretation` {`formed`, `about`, `code`, `supportedBy`} | nobody |
| outcome (observer-only) | `about`, `attempts`, `plan`, `participants`, `timeline[]` {`at`, `event`, `attempts`}, `settledAt`, `kind`, `world` (ids and codes), `base`, `determinedBy` {`rule`}, `mustNotWrite` | not in the character lens |
| comparison (observer-only) | `decision`, `scope`, `status`, `question`, `intervention`, `heldConstant`, `arms`, `unavoidableDifferences`, `differs`, `nonComparable`, `notShown`, `runChoice`, `wholeRun` (ids, codes, numbers) | not in the character lens |
| thread (observer-only) | `stages[]` {`stage`, `ids`}; `observer` {`title`, `people`, `summary[]` {`visibleAt`, `label`, `cites`}} | not in the character lens |

`knownBy[]` entries hold `who`, `how`, `availableAt`, and `teller`, `basis`, `account` or `via` (section 5.1). Prose
keys (`label`, `title`, `summary`, `note(s)`, `settledLabel`, `notPerceived`, `notAsserted`, `observerLinks`,
`qualifications`, `verify` and the like) are refused anywhere outside an `observer` object, block metadata included:
the `lives` block holds only its declared keys (block prose goes under `lives.observer`), `lives.settings` holds the
run's numbers only, and `lives.clock` carries no prose key.

**What a person perceived** (`perceived`): a closed vocabulary by `kind`.

| `kind` | Its other keys |
|---|---|
| `asked` | `by`, `ask` (errand, plan), `good`, `activity`, `place` |
| `refused` | `answer` (no), `act` |
| `agreed` | `answer` (yes) |
| `not-now` | `answer` (not-now), `source` |
| `busy` | `subject`, `activity`, `place`, `source` |
| `found`, `away` | `subject`, `place`, `band`, `source` |
| `agreed-absent` | `absentFrom`, `absentTo`, `source` |
| `not-kept-by-me` | `place` (where the perceiver was) |
| `together` | `subject`, `activity`, `place` |

`source` is `Seen`, `Sought`, `Told` or `Lived`. An answerer's reasons are theirs: an asker's perception or expectation
entry records the answer and how it was perceived, never the reason code the answerer weighed (Pam hears Gus's "not
now", `source: Told`; "working" stays in his decision).

### 4.3 What a person's lens shows (reader rules)

- **Who sees an element** (section 5.4) and **which of its fields** (the last column of 4.2): a record's or plan's
  knowers from their `availableAt`; an attempt's actor; a person holding their own perception of the attempt (who sees
  only its `actor`, `target`, `kind` and `terms`, never the actor's `decision`, `alternative`, `base`, `status` or
  label); the holder of an opportunity, recall or decision; the owner of a perception or learning update. Nobody sees
  outcomes, comparisons, threads, `lives.unresolved` or `lives.characters`.
- A knower is shown **only their own `knownBy` entry** (their own `account`, `teller`, `basis`, `via`), never another's.
- **Tapes.** The character lens shows a tape only as `answer.thought`: to the holder of the decision whose
  `choice.thought` names it, from that decision's `choice.visibleAt`. The request (memory, context, choice lines,
  recalled ids, character block) and the rest of the answer (choice, weights, passes, receipts, backend, model, note)
  are observer-only. Question tapes are never shown.
- **Base rows** are shown only to the asked person, and only if an ask was made (section 8).
- `seq` orders a minute's elements and is never displayed.
- **Node time is acquisition time**: the minute this person came to know it (`knownBy[].availableAt`, their
  perception's minute), not the element's creation.
- **Thread text is never stored for characters.** A reader builds a person's threads from their own permitted,
  clock-gated elements (for example by following their links); it never uses observer `threads`: not their ids,
  membership, timing, titles or summaries.
- **The thought** is shown to its holder from `choice.visibleAt`, labelled "Private imagination · not evidence"
  (section 9.5).
- Where the person has an `unknown` or an inference, the lens shows that, never the truth.

**Observer lens** (privileged): everything at or before the clock under the gates of section 6, world outcomes marked
observer-only; comparisons only behind an explicit control.

## 5. Knowledge

### 5.1 Who knows, how, from when

Records and plans carry `knownBy`: `{who, how, availableAt}`, with `teller` when `how` is `Told`, `basis` (ids) when
`Inferred`, and `account` when the knower holds an account of a fact.

| `how` | Meaning |
|---|---|
| `CanonStated` | pre-run: canon gives this person this account or reading (S, or S·R2) |
| `ProductDecision` | pre-run: Sid's product commitment (standing A), e.g. D52 |
| `Authored` | pre-run: our interpretation (standing E, never A) |
| `Seeded` | the engine's own association or expectation, seeded from the holder's reading (rule 1) |
| `Lived`, `Witnessed`, `Told`, `Inferred`, `Unknown` | in the run: the holder's own act or answer; seen; heard (with `teller`); worked out (with `basis`); unknown |

**In-run knowledge names what gave it.** A `Lived`, `Witnessed` or `Told` entry whose `availableAt` is a minute carries
`via`, an id the knower can see at that minute: for `Witnessed`, the knower's own perception; for `Lived`, their own
perception, decision, opportunity, attempt or learning update, an act they are party to, or a base row they were asked;
for `Told`, a telling act from `teller` to the knower. The pre-run kinds (`CanonStated`, `ProductDecision`, `Authored`,
`Seeded`) appear only before the run. An attempt's own `knownBy` (its actor, from its `visibleAt`) needs no `via`.

**No inference is promoted to a fact.** A fact known through a perception that leaves one of its participants unknown
or only guessed is refused; a `found` or `away` entry rests on a sighting of that person; a perception that lists
someone's whereabouts as unknown perceives no place for them; an inference says `establishes: false`.

A participant is never a knower by being a participant. Personal records (account, reading, commitment, association,
expectation) are known only by their holder. An account's `mode` (Lived, Witnessed, Told, Inferred) is how the holder
relates to the fact: Penny's account of Pam's evenings is `Inferred` (E: canon gives her reading and the effects at
home, not how she knows), Gus's `Witnessed`, Pam's `Lived`.

### 5.2 Attempts: being the target is not knowledge (direction 5.3)

An attempt's `knownBy` is exactly its actor, `Lived`, from its `visibleAt`. Anyone else knows it only through **their
own perception** (`per:` with `of` the attempt). A target who really was asked has one (Penny at 09:10 on Thursday:
`per:45190:Penny:0`, asked for beer). A target who was **not** asked has none: Gus's flour draw lands on Penny while she
teaches (`att:43820:Gus:0`, status `busy`); no ask is made, Penny perceives nothing, and nothing visible to her refers to
the attempt or its row. A bystander's perception of someone else's ask needs a witnessing rule this contract does not
define (the structure checker refuses it).

This holds in codes too. A perception of an ask or its answer (`perceived.kind` `asked`, `refused`, `agreed`, `not-now`,
`busy`) names the attempt in `of`, and the perceiver is its target (for `asked`) or its actor (for the answers); `asked`
is refused when no ask was made. `of: null` is for passive sightings only (`found`, `away`). An `unknown` or an
inference is about a party to what was perceived: the attempt's actor or target, the plan's members, or the person
sighted. So nothing in Penny's lens encodes Gus's never-made ask, with or without an id.

### 5.3 Shared records hold only shared knowledge

A record or plan known by several people holds, in its character-safe content, only ids every knower can see from when
they know it. A shared fact's `characterLabel` is neutral ("Pam's evenings at the Saloon"); each knower's own account
carries their reading ("what she lives with at home: late nights, then headaches"). A plan holds only the terms both
members share; one member's private basis (why the evening ends at 21:00: her habitual bedtime) is observer-only, and
each member's follow-through lives in their own decision and attempt. The same holds for model input: a member's tape is
written from that member's knowledge and the shared terms (Pam's reflection reads "from 18:00 until 21:00", never why
21:00).

### 5.4 References stay inside the lens (direction 5.3)

For every element and every person who can see it, every id in its character-safe content must be visible to that
person at the minute the field is shown to them (the later of their acquisition time and the field's gate). Visibility:
a record or plan through `knownBy`; an attempt as in 5.2; an opportunity, recall, decision or alternative to its holder;
a perception or learning update to its owner; an act to its actor and target; a need to its holder; a favours or
invitations row to its **asked person only**, and only if `askMade` (it holds their answer, their state and the Ask's
margin: section 8); a reflection tape to the holder whose `choice.thought` names it, from that choice's minute; a
question tape never. Outcomes, comparisons, threads, sources, models, `lives.unresolved` and `lives.characters` are
never visible: ids of those kinds appear only under `observer`. A reference to an association or expectation also
needs a recorded state at that minute (section 6.3).

A tape's structured request is held to the same lens: its `recalled` ids are visible to its holder at the request's
minute, and its `choices` are the decision's eligible set (9.4). Its free text (memory, context, choice lines) cannot
be machine-checked; the writer builds it only from the holder's own records and the shared terms (section 15's tests).

## 6. The clock

### 6.1 Gates (direction 5.3)

Every element has `visibleAt`: a minute (from midnight of day 0, spring 1, a Monday) or `"pre-run"`. Nothing inside an
element is later than the minute it is shown: every minute value and every referenced id is at or before its gate. A
missing gate never arrives. The gate is the element's `visibleAt` except:

| Element | Field | Shown from |
|---|---|---|
| decision | `choice` (`selected`, `thought`, `base`, and `choice.observer`: `probabilities`, `sampled`, `fallback`, `laya`) | `choice.visibleAt`: the answer, the next minute for a model or a pending ask (`Simulation.Reflection.cs:5-6`) |
| attempt | `status`; `observer.world`, `observer.outcome` | `resolvedAt` (status is the result as the actor experienced it) |
| outcome | `kind`, `world`, `base`, `determinedBy`, `mustNotWrite`, `observer.settledLabel` | `settledAt` |
| outcome | each `timeline[]` entry | its own `at` |
| record or plan | each `knownBy[]` entry, and the element for that knower | that entry's `availableAt` |
| association or expectation | each `snapshots[]` state | its `asOf` |
| reflection tape, character lens | `answer.thought` only | the owning decision's `choice.visibleAt`; every `events[].tick` at or before it |
| thread | each `observer.summary[]` line | its own `visibleAt`; it `cites` the elements it summarises, each visible by then (`stages` is an index of ids, each shown from its own minute) |
| `lives.unresolved` entry | all | its `asOf` |

**Pending elements.** A decision without `choice`, an attempt without `resolvedAt` and `status`, or an outcome without
`settledAt` and `kind` is pending: a recording or chunk ended before its gate. A pending element (or the outcome or plan
it belongs to) is listed in `lives.unresolved` with `status: open`; anything else missing a gate is a fault.

**Terms are intentions, never outcomes.** An alternative's `template`, a plan's `window`, an attempt's `terms` and a
constraint's `terms` name minutes someone intends; they may lie in the future and are exempt. A plan never links its
outcome; the outcome points at the plan.

### 6.2 Order within a minute

Readers order a minute's elements by `seq`. A reference in an element's creation-time content to another element shown
the same minute points at a smaller `seq` (resolution-gated fields, `knownBy` and `observer` are exempt).

### 6.3 Snapshots: an association's or expectation's state, forward only

An association or expectation holds its states as `snapshots: [{asOf, state}]` in `asOf` order. A snapshot is the
state at the start of minute `asOf`, before any learning update shown that minute. An expectation's state is
`{entries, lived, seeded, successes, chance}`; each entry is `{at, outcome, source, ref?}` and carries its own minute
(`"pre-run"` for a seeded entry); `entries` is always that array, never a count. An association's state is
`{strength}`; mood is not part of it (a learning update records its own `mood {change}`, defined in section 12).

- A snapshot is valid only at `t >= asOf`. Before a record's first snapshot a reader shows **"earlier state not
  recorded"**; it never reconstructs one.
- **Real writers write a record's first snapshot at its creation** (`asOf = visibleAt`), the state before the update
  that created it (an empty expectation; an association at 0), so real output has no gap. In an excerpt, a record created
  before `lives.excerpt.from` may have its first snapshot at `from` instead: the fixture's Gus key does.
- Learning updates apply only when their `visibleAt <= t`, in `seq` order, never backward. Each update's `previous`
  equals the record's state then; for an expectation `{count, lived, seeded, successes, chance}` with the entry `added`
  and any entry `dropped`; for an association `{strength}`. `previous` and `new` hold that shape and nothing else.
  Earlier states stay readable in each update's `previous`.
- A later snapshot must equal the state the recorded updates give, except at or before an excerpt's start.

### 6.4 Excerpts

`lives.excerpt {from, to}` declares a recorded window: a fixture's, or a storage chunk's (`chunk {adapter, index}`). A
real recording declares an excerpt only as a chunk. Everything recorded lies inside the window: elements, resolutions,
settlements, acts, rows, needs, tapes and unresolved entries. Outside an excerpt every act id is its position and every
record's first snapshot is at its creation.

**The calendar.** Minutes count from midnight of day 0, spring 1, a Monday; the recording's `clock` gives the minutes a
day, days a week (7) and days a season (28) (`Replay.cs:190`). An excerpt's `days[]` (`{day, season, date, weekday}`) is
metadata a reader may show: each entry agrees with the clock, and together they are the window's days in order. The
structure checker holds both.

### 6.5 Habits are intentions; sleep is recorded (Sid, 2026-10-14)

Sid's bedtime decision: bedtimes are habits, not hard rules; actual sleep and waking follow the energy rule; nobody is
unavailable merely because their habitual bedtime has passed ("Actual sleep can prevent conversation; the schedule alone
shouldn't"). In the recording:
- **A habitual bedtime is an intention.** It may end a plan's `window`, an alternative's `template.until` or a
  constraint's `terms`, and a constraint may name it (`value: habitual-bedtime`, the fixture's window). Like every
  intention it may lie in the future (6.1), and it is marked as a habit: a sourced hour (canon's, with its source) or
  ours (E). It never says the person is asleep, gone to bed or unavailable at that hour.
- **Sleep is base data.** Actual sleep onset and waking are the moves stream's asleep bit (`Replay.cs:94-115`, `:237`,
  `:322-339` [built]: a person's row is written whenever their place, tile or sleep changes, at 5-minute ticks) and the
  log's `sleep` and `wake` lines (`Simulation.cs:665-686`). PR B's proposed `nights` block adds, per sleep, the
  habitual bedtime and its source, when and why the person set out, onset, waking, energy and the next choice; it is
  base data too, not Lives.
- **In the fields that say where someone is or why they cannot act**, a habit is never a state: an exclusion's
  `why.code`, a `busy` perception's `activity` or `place`, an expectation entry's `outcome`, an outcome's timeline
  `event` or a world span's `place` never carries a bedtime code (`bed`, `bedtime`, `in-bed`, `to-bed`,
  `habitual-bedtime`, `past-bedtime`). `asleep` (or `sleeping`) there holds only for someone the moves stream shows
  asleep at that minute: the busy perception's subject, the expectation's subject, the span's person, an outcome's
  participant, or, for an exclusion, someone other than the decision's holder.
- The structure checker holds the last rule (check `sleep`: a bedtime code in those fields is refused, and `asleep`
  there needs the moves stream's onset), and the structure suite plants each fault; that a habit is marked as sourced
  or ours is review, through its source. A recording with an `asleep` event at a recorded onset passes (`variants.cjs`:
  Penny asleep at 20:30 inside the window, the habit's 21:00 unchanged). The fixture's excerpt ends at the window's
  close, 21:00, and records no sleep: nothing in it makes Penny asleep or unavailable, and when she actually sleeps is
  outside it.

## 7. Records

| Type | Holds |
|---|---|
| `fact` | `kind` (Fact, Past, Practice), `participants`, `place`, `time` (`known: false`; no date canon lacks), `recurrence` (once, recurring, job; never a count canon lacks), `stillTrueAtStart` |
| `account` | `fact`, `mode`, its holder |
| `reading` | `object` (a record id or a cue key such as `place:Saloon`), `stance` (warm, wary, expects, none), `certainty` (stated, guess) |
| `commitment` | `scope` (`{provide: "alcohol", goodsToday: ["beer"]}`), `to`, `force` (Absolute or a cost), `revisable`, `since` |
| `association` | `object` (person, kind, place, activity, or `situation:<slug>` with its cue keys in `situation`), `sign`, `name`, `causes`, `snapshots` |
| `expectation` | `subject`, `key` (`whereabouts:<place>:<band>:<day type>`, `availability:...`, `answers:<ask>[:<good>]`, `followThrough:plan`), `snapshots` |

Observer-only on every record: `standing` and `origin` (canon, product-decision with standing A, authored with
standing E, seeded, run), both required; `sources`, `support`, `cues` (each `{key, origin: object | sourced | proposed,
source, weight, exclusive}`), `seeds`, `qualifications`, `othersKnowledge`, `countOnce`. A pre-run history (fact,
account, reading, commitment) also carries, as structured fields that real writers keep:
- `sources`: at least one `src:` id;
- `admission`: `R1`, `R2`, or `null` where the audit's admission rules do not apply (game data, Sid's decision, our
  interpretation); it agrees with the standing (`S·R1`, `S·R2`);
- `review {status, by, date}`: `proposed` (filed, not yet individually reviewed), `reviewed` (with `by` and `date`) or
  `rejected`.

The structure checker requires them. A seeded association or expectation names the reading it was seeded from (its
`causes`, or its seeded entry's `ref`), and nothing is seeded from a rejected reading. Day types in keys are `weekday`
(Monday to Friday) and `weekend` (Saturday and Sunday) (ours, E; a first guess): a key's entries fall on its day type,
and a recall's active day cue is its own day's (the rules checker's `daytype`).

**An expectation's window (rule 9).** A key a reading seeded keeps its seeded entry beside at most `settings.livedWindow`
(6) lived entries; `chance = (successes + 1) / (entries + 1)` (proposed; checked by the rules checker). **Originals are
never edited**: learning writes only associations and expectations; a fact, account or reading stays as authored, and an
update lists the reading it revises under `preserved`. Count once: one practice with several accounts is one fact.

**Present readings at the start (O2, decided).** Each individually reviewed present reading with a stance seeds one
entry modestly (an association at `settings.seedStrength`, 0.05; one seeded entry for an expectation) and keeps its
standing, admission and review under `observer`; lived entries outweigh it. The fixture's fourteen readings are all
`proposed`: filed by the assessment and awaiting Sid's individual review (section 17). Pam's hobby wish (`Pam:Fri2`) is
a stance-less reading: it opens an offer (rule 11) and gives no warmth toward a game with anyone. The rules checker
holds seeds to `settings.seedStrength` and refuses a `supports` or warm reason that cites a stance-less reading.

## 8. Base rows: answers kept distinct (direction 5.3)

`favours` [#90] and `invitations` [built] rows (`Simulation.Errands.cs:8-10`, `Simulation.Invitations.cs:7-8`) gain, at
the end of the record, three fields, written when Lives is on:

| Field | Meaning |
|---|---|
| `id` | `fav:<tick>:<Asker>:<Asked>:<n>` or `inv:...`: the stable row id every Lives reference uses |
| `answer` | `yes`, `no`, `not-now` or `busy` |
| `askMade` | `false` only for `busy`: the draw found the person busy and **no ask was made** |

| `answer` | `accepted` | `actId` | `margin` and state fields |
|---|---|---|---|
| `yes` | `true` | the asker's asked-for act; `null` when no act begins at the answer (a plan yes) | the Ask's |
| `no` | `false` | the asked person's `Refused` act toward the asker, that minute | the Ask's |
| `not-now` | `null` | `null` | the Ask's (it was asked) |
| `busy` | `null` | `null` | `null` (nobody was asked) |

- **Readers link rows by `id`, never by `actId`.** `actId` is `null`, not 0.1's -1, when no act exists; two rows never
  share an `actId`. A `not-now` or `busy` row is never `accepted: false`.
- **A row is the asked person's.** It holds their answer, their state then (`tired`, `busy`, `low`, `bond`, `eager`;
  for favours `wontFetch`, `short`, `running`) and the Ask's margin, "recorded so an observer can see why"
  (`Simulation.Invitations.cs:3-6`, `Simulation.Errands.cs:3-7`). In the character lens a row is visible only to the
  asked person, and only if `askMade`. The asker knows the answer only through their own perception (`perceived.answer`);
  the asker's own elements link the row only under `observer` (a busy attempt's `observer.rows`). A busy row is visible
  to nobody: nobody was asked.
- **`margin` is the Ask's own**: obliged less the Ask's parts (base, state, errand), as the code writes it
  (`Simulation.Invitations.cs:38-43`). A Lives decision's yes margin also subtracts Lives-only costs (rule 11's displaced
  habit, rule 16's working term), so the two differ where those apply: Pam's yes is 0.33 in her decision, 0.73 on the
  row.
- **Older rows** (no `answer`): `accepted: true` reads as `yes`; `accepted: false` with an `actId` that is a `Refused` act
  reads as `no`; anything else is `unknown`. Refusal is never inferred from a missing act. Every row written before 0.2
  has an act at its `actId` (the act is always begun after the row: `Simulation.cs:982-985` for invitations,
  `Simulation.Errands.cs:179, 183` for favours; four real recordings confirm it), so older rows are provable; an unknown
  row is a fault the structure checker reports.
- **Name clashes (facts).** The existing `asked` field is the asked person's **name**, so the not-asked flag cannot be
  called `asked`: it is `askMade`. The existing `busy` field is `AskStateOf`'s (`Simulation.Invitations.cs:55-63`): 1
  only on a work day when the minutes until the person must leave for work (`job.Start` less the commute, less now) lie
  between 0 and `InviteBusyMinutes` (60, `Acts.cs:130`; `Simulation.Invitations.cs:60-61`), that is, shortly before
  work begins; never once work is under way. It is not the `busy` answer, and its meaning never changes under Lives.
- **What today's code gives Gus at 18:20 on Thursday, and rule 16's open point.** Gus works the Saloon 11:00-24:00
  (`DefaultTown.cs:431`), so mid-shift the time until he must leave is negative: `busy: 0`, and with `tired` and `low`
  at 0 his Ask state cost is 0 (`Simulation.Ask.cs:30-31`). His row's margin is the Ask's +0.36, beyond `ClearBand`
  0.15 (`Feelings.cs:164`, `:517`): today's `Ask` says a clear yes, and under rule 7 alone not now would have no reason
  and be excluded (`no-reason-holds`). The fixture's not now ("he is working") exists only through a Lives-only cost,
  `components.costs.working` (0.2393, section 14.1), which assumes rule 16's open point is settled as "work weighs an
  ask". Nothing in today's code produces it, and it never changes `busy` or the row's margin. If Sid settles the point
  the other way (work stops asks: rule 16, today errand asks only, extended to invitations), that draw is a `busy`
  answer with no ask made; if work is ruled not to matter, not
  now is excluded and the fallback says yes with probability 0.97 (e^(5 x 0.36) against e^(-5 x 0.36)).
- **Lives off stays byte-identical.** The writer omits `id`, `answer` and `askMade` when they are unset (Lives off), so
  today's recordings and the two tests that pin the row field names (`InvitationSceneTests.cs:159-160`,
  `ErrandTests.cs:540-541`) are unchanged; with Lives on those tests must change in the same PR. In `invitations` the
  serializer has no enum converter (`Replay.cs:297-300`) while `favours` has one (`:304`): `answer` is written as a
  string in both (VERIFY with a test).
- **Writer hazards.** A row with no act must not reserve `_acts.Count` (`Simulation.Invitations.cs:37`,
  `Simulation.Errands.cs:164`), must not key the speech maps (`Simulation.Speech.cs:26-41`) or leave the desire gate's
  reserved id pointing at an unrelated act (`Simulation.Desire.cs:571-577`), and must not key the Ask's close-call draw
  by a shared sentinel (`Simulation.Ask.cs:102-104`, logged at `:107`, which enters the log hash).
- **Existing readers.** The dashboard joins act to row by `actId` (`sim/viewer/index.html`, `invitationByAct`): rows with
  `actId: null` are simply invisible to it, nothing crashes (in JS `null >= 0` is true, so a `>= 0` guard does not stop
  a null; only -1 would). Codex's chain viewer reads no base rows. The 0.1 checker keys favours by `actId` and would
  reject two null rows as duplicates; it reads only 0.1.

## 9. Opportunities, recall, decisions

### 9.1 Opportunity and recall

An **opportunity** (`opp:`) records why something comes to mind now: `trigger` (refused-by-commitment, asked,
follow-through, encounter, outcome-contradicts, plan-broken) and `causes` (ids the holder knows). The tape it opened is
linked under `observer.reflection` or `observer.question`: the tape holds the answer, so a character-safe link would
show it before the choice (section 4.3). The rule (10, 8a, 12, 11, 8b, 8c), budget and any note are observer-only.

A **recall** (`rc:`) is bounded: `items[]` hold only `record`, `slot` (`top` or `low-mood`) and `matched` (cue keys);
scores, the active cues, the bound and at most `settings.recall.runnersUpShown` runners-up are under `observer`. At most
`settings.recall.max` items (4): `top` (3), one more by warmth alone when `observer.bound.lowMood`. The tape's request
carries the same ids as `recalled: [{id, origin}]`.

### 9.2 Alternatives, reasons, constraints

- Every **eligible alternative** with typed `reasons`: `supports`, `conflicts`, or `shown` (relevant but not entering the
  number, its `effect` a code such as `context-only`, `not-option-object`, `no-warmth`). `via` names where it enters
  (recall, option-side, expectation, opportunity, plan, ask, state, routine, rule). `cites` are the holder's own
  knowledge at that minute; where our mechanism comes from is `observer.provenance` (source ids).
- **Excluded alternatives** keep `why: {code, cites}` (commitment, no-eligible-good, not-open, closed, physical,
  one-open-plan-a-pair, no-reason-holds); an exclusion is shown, never dropped.
- **Constraints** `{code, value, terms, cites}` bound the set: a window, a schedule, travel, eligibility, counters.
  `value` is a code or number; future minutes go in `terms`.
- **Warm and cold stay apart** to the evaluation: each side is its own reason. **A side is the holder's
  associations**: a `warm` or `cold` reason's `value` is the summed strength of the associations it cites, as they
  stood at the decision's minute, and a rule-5 alternative's `components.warm` and `components.cold` are the sums of
  its warm and cold reasons. A stance-less reading (Pam's hobby wish) may be `shown` or open an offer, never support or
  warm one. The rules checker holds all three.
- `components` (optional) are the fallback formula's inputs: rule 5 or rule 7 (section 14). They are the holder's own
  numbers, character-safe; only the rules checker reads them, and the pilot's writer emits them so it can.

### 9.3 The choice

```json
"choice": { "visibleAt": 45191, "selected": "alt:45190:Penny:0/offer",
  "thought": { "tape": "reflection:7315:opp:45190:Penny:0", "imagined": true },
  "observer": { "backend": "laya", "probabilities": { "...offer": 0.46, "...keep-evening": 0.54 },
    "sampled": { "stream": "lives-choice", "parts": ["dec:45190:Penny:0"], "unit": 0.187, "order": ["...offer", "...keep-evening"] },
    "fallback": { "weights": {}, "probabilities": { "...offer": 0.1749, "...keep-evening": 0.8251 }, "wouldSelect": "...keep-evening" },
    "laya": { "probabilities": {} },
    "provenance": { "alternativeSetHash": "fnv1a64:...", "tape": "reflection:7315:opp:45190:Penny:0", "requestHash": "fnv1a64:...", "model": "model:laya:typed-decisions" } } }
```

- `selected` is the intention; what was tried is an attempt. `base` lists the favours or invitations row the answer
  wrote; its `answer` equals the selected slug and its `tick` the choice's minute.
- `backend` is `laya` or `fallback-keyed-draw`. Laya answers only torn choices (rule 6), at most
  `settings.modelQuestionsPerPersonDay` (2) a person-day. A fallback answer says why in `fallbackReason`, and the reason
  must hold:
  - `not-torn`: the choice is not torn;
  - `cap`: torn, past the person-day cap (`cap: {reached, limit, used}`);
  - `no-evaluator`: torn, in a run whose `lives.run.method` names no evaluator (an authored control, an acceptance run:
    every torn choice is answered this way and no question tape exists);
  - `model-failed`: torn, and the model call failed: `provenance.tape` names the failed tape (a reflection or a
    question), whose answer is an `authored-fallback` recording `fallback {stage, code, httpStatus}` (section 10).
  A Laya answer names a tape whose answer an evaluator gave and that did not fail. A fallback answer may name a
  reflection tape whose evaluator ran anyway (today's mind evaluates whenever Laya is configured, 9.5) only for
  `not-torn` or `cap`: the evaluator's weights stay on the tape, unused. The structure checker holds the shapes; the
  rules checker holds rule 6 (that `not-torn` is used exactly when the choice is not torn, and the other reasons only
  when they hold).
- **One draw**: `unit = Rng.Unit(seed, "lives-choice", decision id)` (`Rng.cs:36-37`), walked through `order`, for every
  Lives choice whether or not a reflection opened; quiet reflections keep `reflection-choice`
  (`Simulation.Reflection.cs:289`). `fallback` is always recorded, with `wouldSelect` under the same unit.

### 9.4 One option set for both methods (direction 4.4)

The set is built once and both methods answer it, including refusal and not now. Both hashes below are defined
byte for byte in 9.6 (canonical JSON, then FNV-1a 64):
- `alternativeSetHash` is the hash of `{eligible, excluded, constraints}` where `eligible` is `[{id: slug, reasons:
  [...]}]` sorted by slug, each alternative's `reasons` the list of the canonical texts of `[kind, code, value]` (value
  `null` when absent), sorted; `excluded` the sorted canonical texts of `[slug, why code]`; `constraints` the sorted
  canonical texts of `[code, value, terms]` (`null` for absent value or terms). Strings sort by UTF-16 code units.
- The tape's request carries the same `alternatives` (`[{id: slug, reasons: [{kind, code, value}]}]`), `excluded`
  (`[{id: slug, why: code}]`), `constraints` (`[{code, value, terms}]`) and `alternativeSetHash`.
- `requestHash` is the hash of the request object exactly as the recording holds it: every member, those written as
  `null` included (`source`, `proposal` and `continuity` are written null when unset, `Replay.cs:282-283`; `character`
  and `scene` are omitted when null, `Reflection.cs:112-115`). Receipts are outside both hashes.

**The option lines the evaluator sees.** Laya reads the request's `choices` (`ResilientReflectionMind.cs:277` and
`:321-322`, both from `request.Choices`): one `{id, kind, line}` per eligible alternative, `id` its slug, refusal and
not now included. In Canonical and Balanced mode it reads them grouped: choices with the same `(kind, line)` are one
option (section 10). Each label's criterion is `"<kind>: <line>"`, or `"no act: <line>"` for an empty kind. A question
tape (`choiceQuestions`) carries the same `choices`, so it says what the evaluator was shown. The structure checker
requires the `choices` ids, the answer's `weights` keys and every evaluation pass's labels to cover exactly the eligible
set, each pass's labels to be exactly the ones `BuildPasses` assigns, and in real output each pass's raw prompt to offer
exactly those criteria and its raw response to give its weights (section 10).

**What a comparison holds constant** is the set's identity, `identityHash`: the same canonical hash over eligible slugs,
`[slug, why code]` exclusions and constraints, without reasons, because an intervention changes reasons. Each arm
records its own `setHash` (its alternatives with its own reasons); the arm with the full history has the decision's
`alternativeSetHash`.

### 9.5 The thought

`choice.thought` points at the holder's own reflection tape (never a question tape), `imagined: true`. It is private
imagination, never evidence: no reason, perception or learning update cites it.

**A reflection on a choice that is not torn** gives only the thought; the keyed draw over the fallback answers
(`fallbackReason: not-torn`). The tape records what the mind returned, which is never the answer:
- An authored thought (`authored`): `AuthoredReflectionMind`'s weights, 3 for its suggested choice and 1 for each other
  (`Reflection.cs:153`), normalised as the simulation records every answer (`Simulation.Reflection.cs:284-288`): 0.6,
  0.2, 0.2 for three choices. The fixture's two such tapes carry exactly that. The same baseline is what an
  `authored-fallback` carries after a failed call (`ResilientReflectionMind.cs:101`, `Simulation.Reflection.cs:280`).
- A generated thought with no evaluator call (`local-llm`, **proposed**): today's mind cannot do this, since generation
  requires a Laya endpoint (`ResilientReflectionMind.cs:43-44`) and, once Laya is configured, every reflection is
  evaluated (`:56`, `:85-89`). The pilot may add a thought-only path for Lives requests that are not torn; its weights are
  then the mind's (normalised), never the answer.
- Until it does, a Laya or hybrid run records what today's mind returns: `authored+laya` or `local-llm+laya`, with the
  evaluator's passes and weights. The decision sets them aside (`not-torn`, or `cap` past the cap) and counts no Laya
  answer; the call still counts in the run's model use (the acceptance runs report it:
  `lives-observer-changes-v0.2.md`, implementation sequence, the acceptance runs).
Both encodings are legitimate real output, and the structure suite proves each passes (`variants.cjs`).

### 9.6 Canonical JSON and the hashes (one definition for the C# writer and every JS reader)

Every hash in this contract is computed over **canonical JSON as RFC 8785 defines it** (the JSON Canonicalization
Scheme, JCS):
- object members sorted by their names compared as sequences of UTF-16 code units (`string.CompareOrdinal` in C#;
  JavaScript's default sort), at every depth; arrays in their order; no whitespace;
- `null`, `true`, `false`; numbers written as ECMAScript's `Number::toString` writes them (`26540`, `0.05`, `1e-7`,
  `1e+21`, `-0` as `0`), never as .NET's default formatting (`1E-07`);
- strings in double quotes with only `"`, `\` and U+0000-U+001F escaped (`\b \t \n \f \r`, other controls as `\u00xx`
  in lowercase hex); every other character as itself, apostrophes and non-ASCII included. `JsonSerializer`'s default
  encoder (it writes `\u0027`, `\u00E9`, `\u003C`, upper-case hex) is not canonical;
- a string holding a lone surrogate has no canonical form (RFC 8785 takes I-JSON input): the writer refuses to write
  one into a hashed object, and the structure checker refuses it.

Then:
- `requestHash`, `alternativeSetHash` and `identityHash` are `"fnv1a64:"` and 16 lowercase hex digits of FNV-1a 64
  over the canonical text's UTF-16 code units, followed by one 0x1F (that is `Rng.Hash` called with the canonical text
  as its only part, `Rng.cs:11-24`);
- `snapshotHash` is 64 lowercase hex digits of SHA-256 over the canonical text's UTF-8 bytes.

The structure checker's `canon` is this definition (`JSON.stringify` for strings and numbers is exactly JCS on
well-formed strings). The C# writer needs its own JCS serializer (a .NET JCS library or a small writer; VERIFY the
library choice); the pilot tests it against `lives-observer-v0.2-tools/canon-vectors.cjs`, which prints these vectors
and, for contrast, what `JsonSerializer`'s defaults would hash:

| Vector | Canonical text | FNV-1a 64 |
|---|---|---|
| `{b: 1, a: "I'll grab it tomorrow"}` | `{"a":"I'll grab it tomorrow","b":1}` | `fnv1a64:61885a03e482c944` (`JsonSerializer` defaults: `fnv1a64:f663faa007a83193`) |
| members `é`, `z`, `Z`, `a`, U+1F600, U+FB01 | order `Z`, `a`, `z`, `é`, U+1F600, U+FB01 (UTF-16 order puts the surrogate pair before U+FB01) | `fnv1a64:8420f9f45cdeca2d` |
| `{s: "quote\" backslash\\ tab\t ... <tag> & + ' U+2028 U+1F600"}` | only `\"`, `\\`, `\t`, `\n` and `\u0007` escaped | `fnv1a64:7998cfc149d66b81` (defaults: `fnv1a64:ecca44ce29c2310d`) |
| `{n: [0, -0, 1, -1.5, 0.05, 0.1, 0.1+0.2, 26540, 1e21, 1e-7, 1.2345678901234568e20, 5e-324, 1.7976931348623157e308, 1e-6]}` | `[0,0,1,-1.5,0.05,0.1,0.30000000000000004,26540,1e+21,1e-7,123456789012345680000,5e-324,1.7976931348623157e+308,0.000001]` | `fnv1a64:136ea5f74382b492` (.NET's `1E+21`, `1E-07`, `-0`: `fnv1a64:a7664ffa4f89bf7d`) |
| the fixture's request `7315:opp:44295:Pam:0` | 2,397 bytes | `fnv1a64:3e50f052de1c9c0a`, the fixture's `requestHash` (defaults: `fnv1a64:64bedb7e31549559`) |
| the fixture's archetype snapshot | 1,287 bytes | SHA-256 `ac6a6ad96b409409a52cade95e995106cbb130ff9b9ce0fd1c9c33d344ef623a`, its `snapshotHash` |

The .NET formatting shown for contrast is emulated in JS (`make-real-like.cjs`, `csharpJson`): VERIFY against .NET when
the pilot's serializer test is written. The fault suite plants a request hashed over `JsonSerializer`'s default text, one
with .NET-formatted numbers, and lone surrogates in a request and in the snapshot; the checker refuses each.

## 10. Models, tapes and receipts (direction 5.5)

- **Tapes** are the recording's `reflections` and `choiceQuestions` (same `{request, answer, choice}` shape; a question
  tape is **proposed**). Tapes live outside `lives`. Replay matches a tape by request id and then compares a **fixed
  list** of request fields, naming each that differs (`RecordedReflectionMind`, `Reflection.cs:160-193`: tick, actor,
  subject, source act, memory, context, source facts, proposal, continuity, opportunity, character, scene and the
  candidate lines, `:175-189`). It does not compare the Lives additions (`recalled`, `alternatives`, `excluded`,
  `constraints`, `alternativeSetHash`) and knows nothing of question tapes: the pilot must extend it to compare those
  fields and to replay question tapes, and section 15's "an edited tape is refused" test covers each added field.
- **Answers reuse `ReflectionAnswer`** (`Reflection.cs:125-128`): `thought`, `suggestedChoice`, `weights`, `backend`,
  `note`, `layaPrompt`, `generationPrompt`, `layaResponse`, `evaluations` (`ReflectionEvaluation {pass, prompt,
  response, labelsToChoiceIds, weights, error}`, `:119-121`), `generationResponse`; these are written even when null
  (`Replay.cs:282-283`). The raw strings stay the source of truth. Every answer's `weights` cover exactly the request's
  choices, normalised as the simulation records them (`Simulation.Reflection.cs:276-288`). Added at the end, with Lives
  on and only when set (section 2.2): `model` (the evaluator's `lives.models` id), `generatorModel`, `evaluationMode`
  (Raw, Canonical, Balanced), `receipt`, and `fallback`. Replay provenance belongs to the run, not to answers:
  `lives.run.tapeSource {runId, sha256}` (the source recording's run id and the SHA-256 of its exact bytes) when
  `method.reflection` is `tape`, and only then.
- **Backends**, and which tape records each:

  | Backend | Status | Tape | Names |
  |---|---|---|---|
  | `authored` | [built] `Reflection.cs:153` | reflection | no model; weights the authored baseline (9.5) |
  | `authored+laya` | [built] `ResilientReflectionMind.cs:89` | reflection | its evaluator (`model`) |
  | `local-llm+laya` | [built] `:89` | reflection | its evaluator and its generator (`generatorModel`) |
  | `authored-fallback` | [built] `:103`; `Simulation.Reflection.cs:281` | reflection, or a failed question | what failed (`fallback`); the evaluator or generator that failed may be named |
  | `laya` | **proposed** | question (`choiceQuestions`) | its evaluator |
  | `local-llm` | **proposed** | reflection | its generator; no evaluator call (9.5) |

  Haiku is written as `local-llm+laya` today, and trials count hybrids by that string, so the generator's identity
  lives in `generatorModel`, not the backend string.
- **Failures are recorded explicitly.** An `authored-fallback` answer carries `fallback {stage, code, httpStatus}`, and
  no other answer carries `fallback` (**proposed**: today the failure is only in `note`, `:104`):
  - `stage`: `Laya` or `Local generation`, the stage `ResilientReflectionMind` names (`:64`, `:73`, `:84`), or
    `Validation` when the simulation's own check replaced an invalid answer (`Simulation.Reflection.cs:274-281`);
  - `code`: for a model stage, `timeout` (`TimeoutException`, `:196-199`, `:458-461`), `http` (`HttpRequestException`,
    a non-success status, `:450-451`), `invalid-json` (`JsonException`), `invalid-data` (`InvalidDataException`: a
    malformed or truncated reply, an oversize response, or a packet over Laya's text limit, `:294`, `:340`, `:391`,
    `:435`, `:444`), `io` (`IOException`); for `Validation`, `invalid-answer`;
  - `httpStatus`: the status for `http` (parsed from "HTTP {n}."), otherwise null.
  The weights are the authored baseline's for a reflection (`ResilientReflectionMind.cs:101` keeps the baseline), and
  the decision's fallback weights for a failed question, which has no authored proposal. The passes recorded before the
  failure stay, the failed one last with `error` and no weights (`:177-181`), and `layaPrompt` / `layaResponse` are the
  last recorded pass's (`:105`, `:108`). The decision answers by the keyed draw with `fallbackReason: model-failed` and
  names this tape (9.3).
- **Evaluation passes, exactly as `BuildPasses` builds them** (`ResilientReflectionMind.cs:119-147`). Raw: one pass,
  each choice its own label, the label its id. Canonical and Balanced: the request's choices grouped by `(kind, line)`;
  groups ordered by kind, then line, and the ids in a group, all by ordinal comparison; labels `o0`, `o1`, ... by slot;
  Canonical runs one pass (rotation 0), Balanced one pass per group, pass `r` (numbered `r + 1`) putting group
  `(s + r) mod n` in slot `s`. Two alternatives with the same kind and line are one option to Laya and share one label.
  Each pass's weights are its reply's probabilities over the labels, normalised (`ParseWeights`, `:394-418`), a label's
  mass split equally between its group's ids (`:172-173`); the answer is the one pass's weights, or the mean of the
  passes' weights normalised (`:184-190`). 0.1's "the last pass's weights are the answer's" would refuse real Balanced
  output, which every hybrid run so far used.
- **Real output is checked against the raw text** (never against an illustrative fixture's placeholders): each pass's
  `prompt` is the JSON sent to Laya, whose `questions.q.criteria` offer exactly `{label: "<kind>: <line>"}` for that
  pass's labels (`:277`, `:306-312`; `:321-322`, `:348-353`); its `response` gives the pass's weights from
  `answers.q.probabilities`; `layaPrompt` and `layaResponse` are the last pass's (`:92`); each pass's receipt agrees with
  the response's `routing` and `usage`; a generated thought's receipt agrees with the generator's raw reply (model,
  seed, tokens), and the reply echoes the request's seed (`:263`).
- **`lives.models[]`**: `{id, role: evaluator | generator, provider, requested, served: {model, repo}, revision, server,
  device, reproducibility}`, plus optional `service {sourceCommit, sourceSha256}`, `settings`, `health {atStart, atEnd}`
  and, for a cloud model, `pricing` (cost is always derived from tokens, never presented as billed); prose about the
  entry (`sources`, `verify`) under its `observer`. Absent means "not recorded"; readers never fill it from what a
  server says today. `served.model` is null only for a model that never answered in the recording (a generator down
  all run: every call an `authored-fallback`). Laya's reply names `laya-rl-agent` as its model (as a real recording shows), so identity is
  `routing.model` and `routing.repo` plus the revision `/health` reports (VERIFY against upstream laya).
- **`receipt`**: `{evaluations: [per pass {pass, servedModel, repo, inputTokens, stateTokens, stateTokensDropped,
  truncated, serverMs, clientMs}], generation: {model, revision, device, provider, requestId, messageId, seed,
  finishReason, inputTokens, outputTokens, serviceMs, clientMs} | null}`. A receipt is observer data: it never feeds the
  run and is excluded from `requestHash`, the option-set hash, the log hash and recorder on/off equivalence. A
  wall-clock time may be recorded in it for that reason only. In real output the receipt must agree with the raw text
  it digests.
- **`lives.run`**: `{method: {reflection: authored | laya | hybrid | tape, thought, evaluator, generator,
  evaluationMode}, source: {commit, dirty}, tapeSource?}`; a Laya or hybrid run names its evaluator, a hybrid run its
  generator. An authored control is a method too: no model entries and no model named, identified by the source commit;
  every torn choice is answered `no-evaluator`. A replayed run (`tape`) keeps the source run's `lives.models`, so every
  answer's model ids resolve, and its answers and receipts are the source run's.

## 11. The archetype pin (direction 5.3)

```json
"characterInspiration": { "schema": 1, "hash": "dd6be675...a7ea", "provenance": "...",
  "snapshot": [ { "coping": "...", "drives": "...", "evaluatorCue": "...", "id": "steadfast-carer", "sources": ["..."], "voice": "..." } ],
  "snapshotHash": "ac6a6ad9...", "artifact": { "uri": "git:38dd92d4...:sim/character-inspiration.json", "sha256": "8ab11274...9152" } }
```

- **Reused receipt** [built]: `schema`, `hash` and `provenance` (`Replay.cs:285-287`). `hash` is the pack's
  `ContentHash` (`CharacterInspiration.cs:43-44`): lowercase hex SHA-256 over the UTF-8 bytes of
  `JsonSerializer.Serialize` of the validated copy with .NET's default options (`:124`): property names as declared
  (PascalCase), in declaration order, the default encoder's escaping (`\u0027` and the like), no indentation; the
  validated copy orders archetypes by id and profiles by name (`:117-121`). It is **not** this contract's canonical JSON
  (9.6), and no JS reader can recompute it without reimplementing that validator and serializer. The pack has no
  revision field; its version is `schema` plus `hash`.
- **`snapshot`**: the exact reusable archetype objects the run used: every archetype a character or a tape's request
  names, quiet non-Lives reflections included (a person with no profile has no `character` block,
  `CharacterInspiration.cs:126-129`, `Reflection.cs:112-113`, and names none). As validated (sources in ordinal order),
  each exactly `{id, drives, coping, voice, evaluatorCue, sources}`: no profile, accent, background, assignment or
  history. Archetypes carry no life.
- **`snapshotHash`**: SHA-256 of the snapshot's canonical JSON, defined once in 9.6 (with its test vectors). The C#
  writer computes it with the same JCS serializer as `requestHash`, never with `JsonSerializer`'s defaults.
- **`artifact`** (optional): an immutable copy of the whole pack, `{uri, sha256}` of its exact bytes. The fixture names
  the corrected pack's git blob at 38dd92d (#96), whose bytes (LF) hash to `8ab11274...`; a Windows checkout of the same
  file with CRLF hashes to `57226132...`, which is why the pin is a content hash and the artifact names exact bytes. A JS reader can check the
  artifact's `sha256` and that the snapshot's archetypes appear in it unchanged; recomputing `hash` from the artifact
  needs .NET (`CharacterInspirationPack.Read(bytes).ContentHash`), so the pilot adds a test that the recorded artifact
  reads back to the recorded `hash`.
- **`lives.characters[i].archetype`** is `{id, packHash}`; `packHash` equals `characterInspiration.hash`. 0.1's
  `lives.external.archetypes` (a path and ids) is gone: a path and id cannot identify a profile after editing.
- **Reader rule.** If the receipt or its snapshot is missing, the snapshot does not hash to `snapshotHash`, `packHash`
  differs from `hash`, or the id is not in the snapshot, the panel says exactly **"Recorded profile unavailable"**, and
  the rest of the recording stays readable. A reader never looks up today's pack, a Studio export or a browser draft by
  id. A run with no pack shows "no character profile configured".
- **Cross-check.** Each request's `character` (`CharacterInspiration.cs:23-25`) names the same `packHash` and
  `provenance`, the snapshot's archetype id and `evaluatorCue`; its `voice` starts with the archetype's composed voice
  (`Drives: ... Coping: ... Voice: ...`) and may continue with the profile's accent (`ComposeVoice`, `:144-145`); its
  `sources` include the archetype's and may add the profile's own (`:140-142`). The snapshot alone proves what the
  recording says the run used; tying it to the whole pack named by `hash` needs the artifact.

## 12. Intention, attempt, plan, world outcome, perception, learning

| Element | Holds |
|---|---|
| **Attempt** (`att:`) | `actor`, `target`, `kind` (errand-ask, plan-offer, go, stay, begin-planned-act), the `decision` and `alternative` it follows or `decidedBy {mechanism}`, `terms`; from `resolvedAt`: `status` (answered, busy, completed, unsuccessful); world truth and the outcome link under `observer` |
| **Plan** (`plan:`) | `members`, `window`, `place`, `activity`, `answer`, `origin` (the asker's attempt), `formal: false`, `knownBy` (both members, from the answer). An agreed evening is informal, not a `Promise`, and may disappoint (A, direction 4.3) |
| **World outcome** (`out:`) | `about`, `attempts`, `plan`, `participants`, no knowers; from `settledAt`: `kind`, `world`, `base`, `determinedBy`, `mustNotWrite` (codes); each `timeline[]` entry from its own `at` |
| **Perception** (`per:`) | `who`, `of`, `causes`, `perceived` (codes by kind, section 4.2), `unknown[]` (status `unknown`), `inferences[]` (`guess` a cue key, `establishes: false`); `notPerceived`, `notAsserted` under `observer` |
| **Learning** (`lu:`) | `who`, `record` (their own association or expectation), `evidence` (their own perceptions only), `previous`, `added`, `dropped`, `new`, `preserved`, `mood {change}` (the mood this update moves, defined below), `interpretation {formed, about, code, supportedBy}` (an interpretation of a person, formed only on the holder's own supporting ids); the rule and any formula basis under `observer` |

**Outcome kinds** (rules 7, 9.4, 9.7, 16; proposed): `refused-by-commitment` (no; a commitment answers at once, as
`Simulation.Ask.cs:98` does [#90]), `declined` (a weighed no, with the `Refused` act), `not-now` (no act, no refusal, a
`not-now` row), `busy` (no ask made; errand asks only, rule 16), `agreed`, `agreed-absent` (a yes, then the other not at
the place in the window: never a refusal, rejection or betrayal, never where they went except as an inference, no
regard), `not-found`, `together`.

**Disappointment (O3, decided).** An agreed evening missed writes, for the one who waited, one learning update: one cold
entry toward the hoped-for evening (an association whose object is `situation:<slug>`, its cue keys in `situation`,
caused by the plan; never toward Pam) with `mood {change}` (negative) and `interpretation {formed: false}`. Its size is
the assessment's 9.4 first guess, D x expected x invested (section 14.1), recorded under `observer.basis` so the rules
checker can recompute it; readers never require it. The rules checker finds a disappointment structurally, never by its
basis or label: any association update whose evidence is a perception of a missed evening (`perceived.kind:
agreed-absent`, `of` a plan), or a cold association caused by a plan. It allows one per perception, and mood moves once.
A later interpretation aimed at the person (`interpretation {formed: true, about, code, supportedBy}`) rests on the
holder's own supported interpretation, named in `supportedBy`, and never again on that perception: the outcome counts
once. An agreed evening writes one follow-through entry and no whereabouts entry (9.5).

**Mood (`learning.mood`), defined.** The engine's mood is not a stored number: it is the squashed, decaying sum of a
person's mood affects (`MoodOf`, `Simulation.Feelings.cs:146-152`), each added once with its minute (`AddMood`,
`:140-144`). `mood {change}` records the one affect this learning update adds to its holder (**proposed**: the pilot
writes it through `AddMood`, VERIFY its source argument when built):
- `change` is a finite, signed number, the affect's amount at the update's `visibleAt`; it is not the difference of
  two mood readings and says nothing of later decay;
- it belongs to the update's holder and is character-safe (their own feeling); `mood` holds `change` and nothing else;
- at most one update moves mood for one perceived outcome (the rules checker counts it once per piece of evidence);
- a disappointment's `change` is minus its size (Penny's -0.0167); a mood the engine moves for other reasons (felt acts,
  `Simulation.Feelings.cs`) is not recorded here.

## 13. Comparisons: isolated, fallback-only (direction 5.3)

```json
{ "id": "cmp:45190:Penny:0:without-p4", "decision": "dec:45190:Penny:0", "scope": "isolated-choice", "status": "comparable",
  "intervention": { "field": "history", "remove": ["rd:penny-saloon-wary", "rd:penny-expects-pam-away"], "evidenceKeys": ["p4"] },
  "heldConstant": { "method": { "backend": "fallback-keyed-draw", "constants": {} }, "scene": { "minute": 45190, "place": "Home:Trailer", "present": [1, 2] },
    "options": { "eligible": [], "excluded": [], "constraints": [], "identityHash": "fnv1a64:..." },
    "randomInput": { "stream": "lives-choice", "parts": ["dec:45190:Penny:0"], "unit": 0.187 }, "personalState": { "traits": {}, "mood": "even", "lived": [] } },
  "arms": [ { "id": "arm:with", "history": [], "consequences": { "opportunity": {}, "decisionExists": true, "recall": [], "components": {}, "reasons": {}, "setHash": "fnv1a64:...", "weights": {}, "probabilities": {}, "sampled": "..." } },
            { "id": "arm:without-p4", "history": [], "consequences": {} } ],
  "unavoidableDifferences": [ { "field": "recall", "code": "slot-refilled" }, { "field": "chance", "code": "seeded-entry-removed" } ],
  "differs": { "reasons": true, "probabilities": true, "sampledAction": true },
  "notShown": ["hybrid-counterfactual-outcomes", "laya-answers", "later-lives"], "wholeRun": { "simulated": false } }
```

- **Held constant**, recorded once: the method (always the fallback), the scene (minute, place, who is present, the
  ask), the options (eligible, excluded, constraints, the set's `identityHash`, without reasons: 9.4), the random input
  (stream, parts, unit) and other personal state, lived entries included (the fixture holds Penny's day-28 'away' in
  both arms: an isolated comparison re-runs nothing, so whether a run without the history would have written it is a
  whole-run question). Each equals the decision's own.
- **The intervention** removes histories; the arms' histories differ by exactly that list.
- **Consequences** are recomputed per arm: opportunity, whether the decision exists, recall, torn, chance terms, sides,
  components, reasons (keyed by alternative id), the arm's own `setHash`, weights, probabilities and the sampled action.
  Any consequence other than components, reasons, `setHash`, weights, probabilities and the sample that differs is
  listed in `unavoidableDifferences`. The arm with the full history holds the decision's own reasons, its
  `alternativeSetHash` and its fallback probabilities.
- **`differs`** reports reasons, probabilities and the sampled action separately: a history can move reasons and
  probabilities without moving the draw.
- **`status: "non-comparable"`** when the intervention removes the opportunity or the decision: `nonComparable {reason,
  detail}`; the arm without it has `opportunity: null`, `decisionExists: false` and nothing else; no matched decision is
  manufactured, and there is no `differs`. Without Penny's commitment, Pam's beer ask is a clear yes at +0.605 (beyond
  `ClearBand` 0.15): no refusal, no rule-10 opportunity, no decision.
- **Not shown**: hybrid counterfactual outcomes, Laya answers and later lives. Arms are never run forward; a whole-run
  comparison is a different object, out of this contract. The run's own choice may have used Laya (`runChoice`).

## 14. Validation: structure, then rules (direction 5.3)

**`check-lives-observer-structure.cjs`** checks what readers rely on, and must pass on real output: version and shape
(exact `"0.2"`, arrays, `seq`), the excerpt or chunk, ids and their forms, references and people, the clock and every
gate of section 6 (including `seq` within a minute, snapshot entries, thread summary lines, tape events and pending
elements), knowledge (5.1: personal records, learning on one's own records from one's own perceptions, how and
standing, `via` for in-run knowledge, no inference promoted), the lens partition (4.1-4.2: every nested key allowlisted,
strings as codes, `perceived` by kind, prose only under `observer`; no `characterLabel` on observer-only tables, and
none that repeats observer-only text verbatim, names an id or speaks in one knower's voice on a shared record), references
inside the lens (5.4, base rows and tapes included), ask perceptions and attempts' knowers (5.2), snapshot replay
(6.3), the calendar (6.4), sleep recorded and never scheduled (6.5), bounds against the recording's own settings
(recall, lived window, one seeded entry, associations per object, model questions per person-day), base-row answers
(section 8), the archetype pin (section 11), one option set for both
methods and the tape agreeing with it, choice lines included (9.4), every hash over canonical JSON and no lone
surrogate in a hashed object (9.6), the fallback's reason and what the named tape may be (9.3), models and receipts
(section 10: backends by tape kind, failures recorded explicitly, the authored baseline's weights, each pass's
labels exactly as `BuildPasses` assigns them, weights by mode and group; in real output the raw prompt's criteria,
the raw response's probabilities, the last pass's raw text and the receipts against it), the method (an authored
control names no model; only a replay names a `tapeSource`), the thought (9.5), comparison structure (section 13), that
attempts follow the selected alternative, and pre-run provenance (section 7: standing, origin, sources, admission,
review). It never requires the illustrative flag, labels, `characterLabel`s, placeholder model text, or any formula.
Label content beyond those checks and a tape's free text are not machine-verified (4.1, 5.4). A recording with no
`lives` block passes after its base rows are checked (2.3).

**Legitimate real runs it must accept**, each built from the real-like variant and passed in the structure suite
(`lives-observer-v0.2-tools/variants.cjs`): an authored control (no models; every torn choice `no-evaluator`; no
question tapes); Laya timing out on a torn question and failing mid-way through a torn reflection's passes
(`model-failed`, the failed tape named); Haiku + Laya as the mind runs today (Laya also evaluating the choices that are
not torn, its answer set aside) and with the proposed thought-only path (`local-llm`); a hybrid whose generator never
answers (every reflection an `authored-fallback`, the generator's `served.model` null: nothing was served); quiet
non-Lives reflections, one by a person with no archetype; Canonical and Raw evaluation; a replayed run; Lives off; a
storage chunk that ends mid-evening with its plan, outcome and Penny's wait pending; and an actual sleep onset recorded
in the moves stream and named in an outcome's timeline (6.5).

**`check-lives-observer-rules.cjs`** holds the experimental pilot checks: rule 5 and rule 7 weights recomputed from
`components` (not now sized as the asked person's state, `Simulation.Ask.cs:30-31`, plus any Lives-only working term;
base-row margins as the Ask's own), fallback probabilities, the keyed draw against `Rng.Unit` (recomputed with
`lives-observer-v0.2-tools/rng.cjs`), Laya only on torn choices and, for a torn choice the fallback answers, a reason that
holds (rule 6: the cap reached, no evaluator in the run, or a failed tape; `not-torn` only when it is not), seeds at
`settings.seedStrength` and warm and cold reasons against the associations they cite (O2), rule 9's chance and seeded
entry, day types (7), the disappointment found structurally, its formula and its target (9.4, O3) with mood moved once
and no later interpretation resting on the same perception, counting once (9.5), and each comparison arm recomputed. Every
comparison is explicit about missing numbers: a missing value fails, never passes as NaN. Readers never require it. Both
run in tests; a recording can be structurally valid and fail the rules, and both suites show it. The rules suite also
runs the legitimate shapes: each passes rule 6, and those whose story is unchanged pass every rule (the authored
control keeps the fixture's selections, so its 09:10 draw, which the fallback alone would answer "keep the evening as
it is", is checked for rule 6 only).

### 14.1 The pilot's formulas (proposed; first guesses, set by reading threads)

These are what the rules checker recomputes; none is a reader's business, and canon supplies none of the numbers.
- **Rule 5** (picks and offers, `components.formula: "rule5"`): weight = base x chance x e^(k (warm - cold (1 -
  boldness) + intended - cost)), chance the product of the listed terms, `k = settings.k` (5), `intended` 0.2.
- **Rule 7** (answers to an ask, `"rule7"`): yes e^(k x margin), margin = obliged - the sum of the named costs (the
  Ask's own parts: `Ask(Invitation)`, `Simulation.Ask.cs:81-84`; `Ask(Errand)` [#90], `:88-92`; plus rule 11's displaced
  habit on a plan answer, and, contingent on rule 16's open point, a working term for an ask made at work); no e^(-k x
  margin); not now e^(k x reason) while a reason holds, the reason sized as the asked person's state: the Ask's own
  state cost (`StateCost`, `:30-31`: 0.3 x (tired + busy + low) x (1 - eagerness x bond)) plus the Lives-only working
  term, sized like the Ask's busy term (Gus mid-shift: 0.3 x 1 x (1 - 0.45 x 0.45) = 0.2393), our reading (E); with no
  reason, not now is excluded as `no-reason-holds`. The fallback normalises the weights and draws with the keyed unit.
- **Rule 9**: an expectation's chance = (successes + 1) / (entries + 1), successes the entries found, yes, together or
  free; the seeded entry never leaves; an update lists the seeding reading under `preserved`.
- **9.4, disappointment**: D x expected x invested, D = `settings.disappointment` (0.05); expected = the plan's chance
  as the holder held it at the yes (her whereabouts, answers and follow-through keys then); invested = (intended + her
  warmth toward the person, her own warm association then) / `settings.disappointmentScale` (0.25). Penny's: 0.05 x
  0.3333 x 1 = 0.0167 (her whereabouts key held p4's seeded 'away' and one lived 'away': 1/3). Written once, as mood
  and one cold entry toward the evening (O3).
- **Rule 6**: Laya answers a choice only when it is torn (warm and cold above `TornAt`, or warm above it with chance
  below 0.5, or the top two within 10%), within the person-day cap; a torn choice the fallback answers names a reason
  that holds (9.3: the cap reached, no evaluator in the run, or a failed tape), and `not-torn` is used only when it is not.

Usage: `node check-lives-observer-structure.cjs <file>`, `node check-lives-observer-rules.cjs <file>`; exit 0 pass, 1
fail, 2 usage or parse error; each prints which checks ran.

## 15. Descriptive only: the write-only sink

The simulation writes Lives observer records through a write-only sink (methods return nothing), passing immutable
copies; rules read only engine stores. The only recorded data read back into a run is a tape, read by the recorded
mind when a freshly built request matches it exactly. Lives requests are built from engine records, never from the sink
or from `appraisals` (which `SceneOf` reads into reflection requests today, `Simulation.Reflection.cs:196-197`, the
same lines on base). Tests to add with the pilot:
- recorder on and discarding give the same log hash, on against off as `SpeechSceneTests.cs:65-69` compares speech
  (`Metrics.LogHash(off) == Metrics.LogHash(on)`); a scrambling sink leaves it unchanged; the sink type exposes no
  member returning data; watched and unwatched runs are equal;
- `Lives` off is byte-identical (pinned hashes; no `lives` key, no new row fields, no archetype snapshot and no answer
  additions, all of which are written only with Lives on: section 2.2); the replay-identity tests of 2.2 keep passing;
- a recorded run replays to an identical `lives` block; an edited tape is refused, for each field
  `RecordedReflectionMind` compares today and each it must learn to compare (`recalled`, `alternatives`, `excluded`,
  `constraints`, `alternativeSetHash`; section 10), and for question tapes;
- the C# canonical serializer and both hash functions give the vectors `canon-vectors.cjs` prints (9.6), apostrophes,
  non-ASCII, .NET-formatted numbers and lone surrogates included; and the recorded artifact reads back to the recorded
  pack `hash` (section 11);
- each Lives request's text (memory, context, lines) is built only from records its holder can see at the request's
  minute and the shared terms (the structure checker sees the request's ids, not its prose);
- the structure checker runs on real output in CI, an authored control and a Laya run at least, and the rules checker on
  the pilot's runs.

## 16. The fixture

`lives-observer-example-v0.2.json` is illustrative and generated (`lives-observer-v0.2-tools/build-example.cjs`).
Seed 7315 is the smallest seed whose keyed draws give this story (`--find-seed` searches; the build stops if the story no
longer holds): every unit is a real `Rng.Unit`, so the rules checker recomputes them; a real run never chooses its seed.

**Which days, and why.** Penny's routine is her sourced one (O1, decided; canon `fixtures/game/Penny.json`, looked up as
game `NPC.cs:5755-5890` does in the 1.6.15 decompile): Tuesday, Wednesday and Friday teaching in the ArchaeologyHouse,
leaving for bed at 18:30; summer Mondays and Thursdays (and Sundays, by `summer_Sun`) in Town from 08:10 and home from
18:00 for the dishes, leaving for bed at 21:00; spring Mondays, Thursdays and Sundays leaving for bed at 18:40; Saturdays
in Town until 20:00. In a dry year (the sim has no weather) her evenings at home and awake through the 18-21 band are
summer Mondays, Thursdays and Sundays (besides winter 4, from 19:00; on the 9th and 23rd she is home from 16:00 but
leaves for bed at 18:40: the corrections note PR #96 adds to the assessment, section 14), so the fixture runs over
**Wednesday summer 3 (day 30)** and **Thursday summer 4 (day 31)**, and the recording's `days` is 35, as that note says
a pilot run's must be. A Saturday at home is ours (E), only a comparison. The agreed evening runs from 18:00 to her
habitual bedtime, 21:00, as she knows it: a sourced habit and an intention, not a rule (6.5). The sim's own bedtime with
her evenings at home (about 20:30, a prototype measure, seeds 1-3) is a run-level comparison, not in this fixture.
Present readings are seeded at R 0.05 with their standing, admission and review status (all `proposed`) under
`observer`.

| Minute | Day, clock | What |
|---|---|---|
| 43820 | Wed 10:20 | Gus's flour need at the Square; his draw lands on Penny, teaching (a lesson day in canon and in the sim): `busy`, no ask, a `busy` favours row; she perceives nothing |
| 44280 | Wed 18:00 | Gus sees Pam at the Saloon in the evening band: a passive sighting on his weekday key, whose lived window is full, so its oldest sighting leaves; the seeded entry stays |
| 44295-96 | Wed 18:15-18:16 | Gus asks Pam at the Saloon; she perceives the ask; her reflection opens (8a), not torn; the keyed draw says no (0.0996; unit 0.9208); act 388 |
| 45190 | Thu 09:10 | Pam asks Penny for beer at home (the sim's weekday morning); Penny perceives the ask; her commitment says no at once (act 412; the Ask's margin +0.605, her lessons at ten making `AskStateOf`'s busy 1); her reflection opens (rule 10); she recalls p5, p4, p6 |
| 45191 | 09:11 | Laya picks the offer (0.46; the fallback's 0.1749 would keep her usual evening); Penny asks; Pam perceives it; her reflection opens |
| 45192 | 09:12 | Pam says yes (Laya 0.71; fallback 0.8006); an `invitations` row with `answer: yes`, `actId: null`; the plan, 18:00-21:00 at home |
| 45688-89 | 17:28-17:29 | Pam at the Saloon since 16:35: walk home for the plan, or stay? Laya: stay (0.62; fallback 0.5772) |
| 45708-09 | 17:48-17:49 | Penny at the Store (a sim guess): home now for the plan, or her usual evening? Laya: the plan (0.81; fallback 0.4878, her whereabouts key at 0.3333); she walks home |
| 45720 | 18:00 | the window opens; Pam stays at the Saloon (her own perception: not kept by me); Gus sees her there, his evening key confirmed again |
| 45721 | 18:01 | Penny is home and waits |
| 45740-41 | 18:20-18:21 | Pam, warm toward Gus, offers a game (torn, past her model cap: the fallback, 0.0973); Gus perceives it; his reflection opens, not torn; the fallback says not now (0.5821): a `not-now` row with `busy: 0` (his shift is under way, not due), no act; "working" is a Lives-only cost, contingent on rule 16's open point; Pam hears "not now" (`Told`), not his reason |
| 45781 | 19:01 | Penny's wait ends: agreed, absent; where Pam is unknown, a guess of the Saloon that establishes nothing; one follow-through entry; a disappointment of 0.05 x 0.3333 x 1 = 0.0167 toward the evening, mood -0.0167 |
| 45900 | 21:00 | the window closes at Penny's habitual bedtime; the outcome settles as `agreed-absent`. The habit ends the plan, not her evening: nothing makes her asleep or unavailable, and the excerpt records no sleep |

**Penny's whereabouts key.** p4 seeds one 'away' (0.5). Summer 1, a Monday (day 28), was the first evening her sourced
routine had her at home and awake through the 18-21 band; Pam was at the Saloon, so rule 14 wrote one lived 'away'
(seen absent), and at 0.3333 the key fell below 0.5, where rule 14 stops counting her absence. In spring she left for
bed at 18:30 or 18:40. The entry is before the excerpt, so the key's state at the excerpt's start is a snapshot (6.3).
It lowers both the offer's chance and the disappointment's expected term.

Three comparisons on Penny's 09:10 choice: without p6 (the offer 0.1749 -> 0.1417; both arms draw keep), without p4
(0.1749 -> 0.2413: the key keeps Monday's lived 'away', held constant, so its chance rises from 0.3333 to 0.5, not to 1;
the same unit, 0.187, now draws the offer), and without the commitment (non-comparable: the opportunity does not arise).

**Tapes, as the code would write them.** The two reflections on choices that are not torn (Pam's at 18:15 on Wednesday,
Gus's at 18:20 on Thursday) carry the authored baseline's weights, 0.6 for the suggested choice and 0.2 for each other
(9.5), not the fallback's; the keyed draw answers both. The evaluator passes of the four model-answered tapes are labelled
as `BuildPasses` labels them (`o0`, `o1`, ... over the `(kind, line)` groups, rotated by pass), with invented weights
whose mean is the invented answer. Lives requests write `source`, `proposal` and `continuity` as null, as a
`ReflectionRequest` is recorded. Raw prompts, responses and receipts are placeholders here; the real-like variant fills
them in the services' shapes and the structure checker holds them to the request in real output.

**Implementation ambiguities the sourced routine exposes** (named, not filled with canon; the fixture lists the same
under `lives.observer.ambiguities`). PR B's numbers here are its prototype's (seeds 1-3, 112 days), not the pilot's:
VERIFY when PR B lands.
- **Where Penny teaches:** canon's ArchaeologyHouse (the museum), 09:00-14:00; the sim's Square, 10:00-14:00
  (`DefaultTown.cs:462`). The fixture keeps the Square, as PR B's design does.
- **When she teaches:** canon Tuesday, Wednesday and Friday; the sim Monday to Friday. Wednesday is a lesson day in both,
  so the busy case holds either way. Thursday is one only in the sim, whose morning the fixture follows: her lessons at
  10:00 make `AskStateOf`'s busy 1 at 09:10, so the beer ask's margin is +0.605 (canon's Thursday, with no lessons,
  would give +0.68).
- **Where Gus is at 10:20 on Wednesday:** the sim's cards put him at the Square, his 08:00-11:00 haunt (`:432`), where
  Penny teaches; canon has him at the Saloon from 08:00 (his spring key runs in summer). The busy draw needs the sim's:
  a mechanism fixture, not a canon scene.
- **Thursday morning:** the 09:10 ask follows the sim's weekday morning (PR B's prototype: Pam up at a median 09:01,
  Penny leaving for her lessons at about 09:30). Canon's summer Thursday has Pam's day start at 08:00 and Penny leave
  for Town at 08:10, so their only canon overlap at home is 08:00-08:10.
- **Her afternoons are sim guesses:** canon's summer key has her in Town (tiles 96,18 from 08:10; 63,15 from 13:00),
  which has no sim place; the fixture puts her at the Store, one of her 14:00-18:00 haunts in the sim (`:463`).
- **Her bedtime:** canon's 21:00 is when she leaves for bed (she shows asleep on arriving); the fixture reads it as the
  end of the evening as she knows it, an intention. PR B's prototype has her body send her to bed before 21:00 on summer
  Mondays, Thursdays and Sundays (median 19:52, its gap G3): whether she is awake through the window is the pilot's run
  to show, as recorded sleep (6.5).
- **Day 28's 'away'** assumes she was home and awake through that Monday's evening band, as canon's summer key has it;
  under the energy rule the pilot confirms it or not.
- Pam's arrival at the Saloon (16:35) and both walks are illustrative; canon's spring key, which runs in summer (Pam has
  no summer key), has her leave for the Saloon at 16:00.

**Sizes, measured on this fixture only** (not runtime scaling): 188,571 bytes (0.1's fixture 135,610); the `lives` block
120,749 bytes compact, 63,284 without `observer` and `characterLabel`; per element without them, a decision about
1.6 KB, a recall 0.33 KB, an attempt 0.29 KB, an outcome 0.43 KB, a perception 0.20 KB, a learning update 0.35 KB, a
pre-run record 0.28 KB, a comparison 4.9 KB; tape entries average 3.8 KB with placeholders; the three-archetype snapshot
1.3 KB.
Runtime time, memory and size are to be measured with the pilot, apart from today's baseline.

## 17. Open points

**For Sid.**
1. Rule 16's open point stands: the fixture reads Gus being mid-shift as a not-now reason for a game, a Lives-only
   working cost (contingent; the row's `busy` stays the Ask's, 0); if work stops asks, that answer is `busy`.
2. The archetype snapshot proves what the recording says was used; tying it to the whole pack needs the artifact. Is a
   git blob at the run's commit acceptable as the immutable artifact, or should runs copy the pack beside the recording?
3. The routine ambiguities of section 16 go to PR B (Penny's sourced routine as character data), whose design keeps
   the sim's lessons and lists her canon teaching days and place, her afternoons and Saturday in Town as not represented.
4. A pack can be configured only with reflection on (`Replay.cs:80`). A Lives run turns reflection on (the authored
   mind by default, the assessment's 9.1), so it can pin; were reflection off, its characters would carry
   `archetype: null`. Keep that coupling?
5. Day types in expectation keys (`weekday`, `weekend`) and the 0.05 seed strength are ours (E), first guesses; the
   rules checker holds keys and recall cues to the calendar (`daytype`).
6. The fourteen present readings are all `review.status: proposed`. O2 seeds individually reviewed readings: the pilot
   seeds a reading only once Sid marks it `reviewed` (or drops it as `rejected`).
7. Character labels and a tape's free text are reviewed, not machine-checked (4.1, 5.4); the pilot adds a writer test
   that builds request text only from the holder's own records (section 15).
8. Reflections on choices that are not torn, in Laya and hybrid runs (9.5). Today's mind evaluates every reflection
   once Laya is configured, and generation needs Laya; the decision sets that answer aside. The pilot can keep that
   (model calls spent that the rule does not use, reported) or add the proposed thought-only path (`local-llm`, or the
   authored mind alone). The contract and the structure checker accept both; which should the pilot build?
9. Failures become structured: `answer.fallback {stage, code, httpStatus}` (section 10) is proposed; today the stage
   and message are only in `note`. The question tape and its backend `laya` are proposed too.
10. The C# writer needs an RFC 8785 serializer for every hash (9.6): a .NET JCS library or a small writer of our own
    (VERIFY a library before relying on one); either way it is tested against `canon-vectors.cjs`.
11. The fixture now follows PR #96's canon checks: its evening is a summer Thursday (day 31), at home from 18:00 until
    her habitual bedtime, 21:00, and the run is 35 days (section 16); 0.2's fields did not change for it. Two questions
    stay for PR B and Sid. (a) The morning: the fixture's 09:10 ask follows the sim's weekday morning; under canon's
    summer Thursday mother and daughter share only 08:00-08:10 at home, and PR B's prototype has Pam waking at a median
    09:01. (b) PR B's gap G3: on summer evenings her body sends her to bed before her 21:00 habit (median 19:52), so the
    pilot's evenings at home may run about 18:00-19:50; whether a habit should also hold off bed until its hour is a
    design question for Sid. The recording shows whichever happens: sleep as recorded (6.5), never inferred from a habit.
12. Habits and sleep (6.5): the structure checker reads actual sleep from the moves stream; PR B's proposed `nights`
    block would give onset, waking and energy directly. A Lives element that a real sleep ends (a plan's wait cut short)
    could then cite the night; not needed for 0.2, proposed for when the pilot meets it.

**For Codex** (coordination before replacing the 0.1 adapter; the change list names each field and the adapter line
it touches, at aef9d7f):
1. Accept exactly `"0.2"`; read `observer.*` only in the observer lens; in the character lens show `characterLabel`
   or text built from codes, never require a label (the top-level `label` is gone).
2. Order a minute's elements by `seq`, never displayed; in a person's lens a node's time is their acquisition minute.
3. Expectations and associations replay `snapshots` and learning (`added`, `dropped`, `previous`, `new`, in `seq`
   order), not `initial` or a top-level `strength`; "earlier state not recorded" before the first snapshot. A feeling
   is new when its update's minute is the record's creation: `previous` is then `{strength: 0}`, never null.
4. Per-viewer projection (4.2's last column): perceivers of an attempt see only `actor`, `target`, `kind`, `terms` (no
   `characterLabel`); a knower of a shared record sees only their own `knownBy` entry.
5. Thread text in the character lens built from the person's permitted elements; observer threads (ids, membership,
   timing, text) never used there.
6. The thought through `choice.thought`; method and draw under `choice.observer`.
7. Comparisons: `heldConstant` object, `arms[].consequences`, reasons keyed by alternative id, `question` a code; a
   `non-comparable` case shows `nonComparable` and has no `differs`.
8. The archetype panel: verify the snapshot hash (asynchronously in a browser; `crypto.subtle` on exported `file://`
   pages is VERIFY) over canonical JSON exactly as 9.6 defines it (`canon-vectors.cjs` has vectors), then show the
   archetype or "Recorded profile unavailable".
9. Base-row ids (`fav:`, `inv:`) for any later link from a decision to its row; rows only in the asked person's lens.
10. Tapes in the character lens only as the thought, from the choice's minute; an opportunity's tape link is
   `observer.reflection` or `observer.question`.
11. Mood is `learning.mood.change` (0.1's `new.moodChange` is refused); learning's rule is `observer.rule`; an
   association's `new` is `{strength}`.
12. `perceived` is a closed vocabulary by kind (4.2): show the keys 4.2 lists for its kind, as codes.
13. Habits and sleep (6.5): show a habitual bedtime as the end of an intention (a plan's window), never as the person
   going to bed or becoming unavailable; show sleep only where the base moves stream (or PR B's `nights` block) records
   it. The fixture's last minute, 21:00, is the window's close at her habitual bedtime, not her going to bed.
