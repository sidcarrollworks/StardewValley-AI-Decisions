# Contextual encounters and event appraisal

## Status

Built first observer slice, 2026-10-09, on `codex/contextual-events`: supported encounter facts,
explicit roles, private motive/reflection causes, perceived accounts, acquisition-time authored
appraisals and clock-aware dashboard grouping. Simulation decisions, feelings, gossip selection
and authority behavior are unchanged. `ActKind.Tier` remains the legacy gossip classification;
`HelpedSomeone` is still News at juiciness 2 there. Stories now distinguishes Everyday,
Relationship moment, Public concern and Context unavailable.

Still planned: concrete tasks/items/effort, use of the new context in model requests, and
listener-specific gossip selection. The first interest score is an observer baseline for the
holder who learned this account, not a teller's prediction of every possible listener.

Sid's example is Evelyn and George helping each other at home. Three recorded events on d1
at 16:50, 17:28 and 17:33 were all News despite no retellings in the prepared hybrid run.
Ordinary care can matter to the people involved without being news for the town.

## Scope and build order

1. Add structured context and time-stamped provenance to existing help, gift and argument
   events, beliefs and replay records where appropriate. Establish the data/knowledge boundary.
2. Add an explainable authored appraisal baseline and observer labels/grouping using that context.
3. Integrate listener-specific interest into gossip in a separate change, with spread and belief
   regression tests. Changing dashboard labels alone does not change what NPCs want to retell.

Do not implement emergencies, task completion, gift inventories or promises by inventing details
at rendering time. A concrete task/help-request mechanic follows in roadmap step 3. New action
options follow in step 2. This slice supplies the shared contracts those features will fill.

## Record contracts

Implemented types are in `sim/UnderGlass.Sim/Encounters.cs`; acquisition hooks are in
`Simulation.Encounters.cs`. The contracts below are implemented to the extent stated here.
`EncounterContext` records act ID/time, roles, location, declared duration and existing causal
link. Activity, item, effort and amount remain null because these acts have no concrete mechanic
for them. `EncounterCause` records the actual selected desire motive or reflection receipt and
choice; it is a separate private observer record and is never copied into a perceived account.

| Record | What it holds | Who may read it |
|---|---|---|
| Encounter context | Act ID, participant roles, supported activity/purpose, actual item/amount/effort when recorded, and causal act/obligation IDs | Observer truth view; NPCs only through their perceived projection |
| Private cause | Accepted intention, motive or imagined interpretation that actually led to the act, with its recorded source | That actor and observer; others only if an act communicates it |
| Perceived context | What this holder saw, heard or did; believed identities/roles, known details, confidence, source and acquired time | The holder's decisions and appraisals |
| Appraisal | Holder/listener, time, personal significance, interest to this listener, reason codes and supporting known record IDs | That holder; observer shows the viewpoint and supporting evidence |

Snapshot the relevant known history/relationship at appraisal time. Do not reconstruct an earlier
judgment from end-of-run regard. Own actions and received actions use their actual roles; an
official's warning or an accident must not become a voluntary deed by the person undergoing it.
Preserve a mistaken believed identity instead of correcting it from hidden world truth.

Unknown purpose, cost or subject detail stays unknown. A generic gift is not a named item unless
the simulation recorded one. A private plan to make amends is not public proof of remorse.
An observer may inspect that recorded intention but must be told which information is private.

Imagination is a possible interpretation, not an event or a measured feeling. For example,
an eligible suspicion proposal after kindness does not prove Penny actually felt uneasy.
The viewer should distinguish the source facts, proposal eligibility/selection, evaluator choice
and recorded reaction. Explanation comes from those records, not generated justification afterward.

Append optional fields with compatibility defaults. Older recordings show generic encounters and
unknown context; they must not acquire invented reasons. If persisted simulation data changes,
provide an explicit version/migration and tests. Record/request changes invalidate incompatible
model tapes visibly rather than silently accepting them.

**As built:** the run remains format version 1 with additive `encounterVersion: 1`,
`appraisalRevision: authored-context-1`, `encounters`, `encounterCauses` and `appraisals` fields.
Old files need no rewrite and display Context unavailable. No persistent simulation save format
or model request changed. Replaying the existing 91-answer hybrid tape reproduces every old
recording field except the command label, including hash `569d3d45ee037fbd`.

Accounts snapshot holder, believed actor/target, role, source, confidence and teller chain when
self-knowledge or `Add` acquires an event. Updates replace that act within the holder's seven-day
appraisal history, so corroboration is not counted as repetition. Appraisal captures regard and
familiarity before the current belief's affect is applied. A warned/collapsed person is recorded
as undergoing an event, not voluntarily performing it. Source links in the observer truth view
do not grant those links or private intentions to witnesses.

## Appraisal rules

Personal significance and shareability are separate values with bounded, inspectable inputs.
The first baseline uses authored rules; model appraisal is a later controlled experiment.
These values are not affect magnitude, morality, legal reportability or a new kindness quota.

The first rules identify household care, recent known conflict followed by kindness, kindness
while regard is strained, direct adverse encounters, known-person relevance and repeated similar
valence. Scandal/upheaval kinds retain an explicitly labelled public-concern reason. Each reason
cites event IDs or the captured relationship input. Scores are provisional rule outputs, not
measured feelings or an established measure of interestingness. Cost, concrete needs, norms and
fulfilled obligations have no appraisal rule until their supporting mechanics exist.

- Familiar household care is normally everyday activity. Same-household status alone cannot
  suppress a supported exception such as a recent hurt, costly help or a consequential refusal.
- Repetition reduces novelty within the holder's known history. It does not erase the act's
  practical benefit or delete the memory. Existing affect adaptation is a separate mechanism.
- An act that answers a known hurt or need can be personally significant. Cite the actual source
  and outcome; a kindness after an argument is not necessarily forgiveness or reconciliation.
- A listener's ties, involvement and knowledge determine relevance. One person's important
  encounter is not automatically town news. Lack of retelling does not prove insignificance.
- Actual cost, public consequences and deviations from known behavior can supply reasons only
  when those facts are supported. Absence from a short memory window cannot establish
  "first kindness ever" or certain knowledge that a person normally behaves differently.
- Hearsay retains its source, confidence and uncertainty. Context spreading cannot grant access
  to a private intention or details the teller never learned.

Each reason names its input records and time. Do not require an event to cross a global interest
score to affect a participant. Keep the existing authority/crime contracts independent of the new
observer labels; reclassifying routine help must not change the mayor's reporting policy.

## Observer presentation

Use an everyday activity view plus highlights with explicit reasons and viewpoints. Group nearby
repetitions by participants/activity while preserving each source event and time in drill-down.
A highlight can say it answers a recent argument when that source link exists; it cannot announce
reconciliation merely because regard rose. The selected-character timeline and event navigation
must still reach the individual encounters, thoughts and outcomes.

All grouping, reasons, knowledge and outcomes follow the replay clock. Rewinding removes later
members of a group and later interpretations. Model text stays plain text. Debug views expose the
inputs/rules and distinguish authored appraisal, model interpretation and observed consequence.

**As built:** Stories defaults to all encounters, with separate contextual-highlight and
everyday filters; the legacy-news filter remains available. Everyday events with the same
unordered participant pair, kind and location within 90 minutes of the group's first event are
grouped. Each member retains its time and selectable detail view. The 90-minute window is a
presentation choice, not memory deletion or a simulation cooldown. Reasons, private cause links
and per-holder accounts appear in Context and perspectives; earlier evidence opens at its own
recorded minute. All accounts/group members follow the cutoff, including Whole-run analysis.

## Verification scenes

- Evelyn/George routine reciprocal help: everyday presentation, retained participant effects,
  no automatic town-news assertion; repetition grouping preserves all three event links.
- Comparable help with a recorded recent hurt: a supported relationship reason, without claiming
  forgiveness or repair succeeded before a response occurs.
- Different listeners with different ties/knowledge: different relevance, no hidden-history input.
- Mistaken identity and ambiguous observation: reasons use the belief's roles and confidence.
- Private accepted intention versus a witness: the witness never inherits the private purpose.
- Unsupported gift item, task, emergency or cost: absent details remain absent in speech/appraisal.
- Rewind and old recording: no future reasons/group members and no invented backfilled context.
- Gossip integration: retelling transfers only known details; source chains, decay and deterministic
  replay remain valid. Quantities of feuds, news or retellings are reported, not pass quotas.

Complete the first observer scenes before expanding the contextual rules. Review their readability
with Sid while keeping deterministic mechanics and privacy tests as required checks.

Validation: 11 new .NET encounter tests and five Node viewer tests cover these first contracts;
existing pinned hashes, replay, reflection and authority tests remain. The fresh authored demo
shows grouped Evelyn/George care and private decision navigation; the hybrid tape demo preserves
the original model decisions. Gossip-integration scenes apply to the later selection change.
