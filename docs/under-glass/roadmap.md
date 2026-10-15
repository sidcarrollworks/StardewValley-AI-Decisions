# Under Glass: current development roadmap

Updated 2026-10-12 after Sid selected hybrid and requested source-derived character voices; amended
2026-10-13 to say where the agreed order leads (player speech, below), and 2026-10-14 for Sid's
history-first direction: the Lives pilot is next, needs bars are unresolved (D53).
This is the current order of work for the independent game. It supersedes older build orders,
event-volume budgets and social-outcome acceptance thresholds. Existing code, regression tests
and dated measurements still describe what was implemented; a planned change is not already live.
The Stardew mod has its own [roadmap](../spec/roadmap.md) and policies.

## Product and method

The first product is an in-depth social simulation watched from above. An observer can follow
what a person knows, imagines, chooses, attempts and experiences. Player gameplay comes later.
Sid selected local inspiration plus Laya as the development direction. Continue
that direction while making people distinct, encounters more specific and expanding what people can actually do.
Working integration and positive scene review do not settle every model-quality question.

Ordinary language is authored, editable and tagged for archetype, mood, purpose and context.
A configurable generator supplies occasional private inspiration during quiet time or dreams.
Laya reads the actual response lines and the character's known scene, then supplies typed weights.
The engine samples once and enforces eligibility, movement, costs and consequences. A model can
suggest a reading of known events; it cannot make an imagined event become evidence.

The ego/consciousness metaphor describes these roles, not a claim that either model is conscious.
Authored controls remain useful for comparison and fallback. They are not a target that model
answers must converge to. Keep suspicious, generous, hostile, hesitant and mistaken possibilities.
Quiet, hostile or socially collapsed towns are valid when their causes make sense.

## Where we are

- Built: a deterministic .NET 8 town, perception and beliefs, feelings and motives, money,
  optional act-catalog slices, recorded runs and an observer dashboard.
- Built, opt-in: contextual authored inspiration, a local generative provider, Laya evaluation,
  waking dreams, deferred ideas revisited after new knowledge, intentions and later outcomes.
- Built, opt-in: reviewed pre-farmer background anchors and source-derived voice context: private
  prompt context for the generator and Laya, separate from witnessed memories; they create no act,
  belief, feeling or regard and reach a choice only through the models' reading. Reusable archetypes
  carry no donor biography. Histories that shape behaviour are planned (the Lives pilot).
- Built: current-moment cards, source/consequence navigation, character timeline markers and
  hover descriptions, and playback controls that remain visible while scrolling.
- Built: supported encounter facts, private cause receipts, acquisition-time perceived accounts
  and authored appraisals. Stories shows contextual reasons and groups everyday repetitions.
- Built: reflection also offers source-specific thanks (Returns) and apologies (Repair), with
  source/eligibility checks and actual catalog consequences. Company/comfort reflection remains planned.
- Current limits: other richer actions exist in `ActCatalog` behind switches.
  Generic acts do not record what help or a gift concretely involved. Gossip tiers remain fixed
  by action-kind juiciness; the new observer appraisal does not yet choose what NPCs retell.
- Current model limits: some generated ideas disagree with their suggested action or reverse
  remembered roles. The next work improves grounding and observability alongside the world;
  it does not wait for a perfect generator or start with fine-tuning.

See [reflection status](specs/reflection-spec.md), [implementation reference](../../sim/README.md)
and the dated [reliability measurements](experiments/reflection-reliability-2026-10-08.md).

## Immediate: distinct private voices and reliable interpretation

Use the [character inspiration slice](specs/character-inspiration-spec.md) to review the same known
scenes with profiles disabled/enabled. Its source baseline is each person's established backstory
before the farmer arrives (Sid, 2026-10-12). Read the full extracted dialogue as voice evidence,
retain source coverage/conditions, and admit only individually reviewed background anchors. The
anchors are private background context for the models, not memories: they create no act, belief,
feeling or regard (D44), and one anchor can mix a fact, a past, a practice and a reading. Histories
that shape behaviour are planned separately, as sourced records with an account per holder, admitted
by the [history audit](experiments/history-evidence-audit-2026-10-14.md)'s rules and counted once
([Lives assessment](lives-gap-assessment.md), section 2). Generic descendants reuse tendencies and
voice; named backstories are not inherited.

The [live character-voice trial](experiments/character-voice-trial-2026-10-12.md) shows that distinct
generated voices remain unresolved with the current small generator. A [first run with Claude
Haiku 5.5](experiments/voice-wording-haiku-2026-10-12.md) behind the same loopback contract kept
plan, private form and register on the wording probe and agreed with its own suggested act in a
hybrid week; it also showed pet phrases and invented small details. Sid selected it, thinking off,
as the generator for hybrid recordings; the pinned local service remains the offline control and
the shipped default. Imagined specifics may stay in private thoughts for now; whether they match
the context and what a player would see is a later concern.
Use the [fixed-intention wording probe](experiments/voice-wording-2026-10-12.md) to judge Vincent
against actual dialogue and authored alternatives before changing runtime prompts or background
anchors. It isolates wording from Laya decisions and preserves failed outputs.
Review cadence, vocabulary, conflicting impulses and failures against the source. Avoid replacing
one generic voice with a catchphrase for each character. Profiles must preserve grounded meaning
and the Laya budget. Then fix thought/action disagreement as a separate contract, using observed
failure scenes. This does not postpone concrete encounters or broader actions until models are perfect.

### The agreed order (Sid, 2026-10-12)

Sid's reading after the Haiku run: being a participant is further off than it looks, and the
repetitiveness of what people do and say is the thing to fix first; small talk, scene-specific
knowledge and broader actions will make the viewer worth watching before any player exists.
The order is:

1. **Scene-specific knowledge into the model requests** — built 2026-10-12. Each reflection
   request now carries a `ReflectionScene`: where the remembered encounter happened and how
   long ago, where the actor is now and the time of day, how they know the subject, the pair's
   kind and unkind acts this week, and the actor's own appraisal reasons as phrases. The
   generator gets it as structured fields; Laya gets one line when it fits the budget, left out
   whole otherwise and visibly so in the recorded packet. Every catalog kind an actor can do to
   someone now has a memory verb. Nothing is read from the subject's records or the world's
   truth; a changed request invalidates older tapes, as the tape rule requires.
2. **A tagged speech library** for routine lines — first library built 2026-10-12, opt-in with
   `--speech` ([speech spec](specs/speech-spec.md)). Every act to a person gets one authored line
   chosen deterministically from `speech-catalog.json` by kind, the speaker's archetype,
   relationship band and mood band, recorded beside the act and shown by the viewer under the
   same clock. The lines were drafted per archetype from the local dialogue evidence,
   copy-checked and judged blind; they await Sid's curation. A tag now names where a line belongs
   best, and a neighbouring band still fits (D49), which cut back-to-back repeats by more than half;
   the curation page `sim/viewer/speech.html` shows which kinds and voices are still thin and edits
   the lines in place. The welcome's side (the newcomer welcomes the town, with lines written for
   residents) is settled by Sid's decision that the Newcomer gets the player's voice (step 5, D50); a
   placeholder `newcomer` voice with arrival-side lines is planned in the
   [player speech spec](specs/player-speech-spec.md). Reply lines and scene tags are next.
3. **Broader actions where reflection already reaches**: company (a game, a drink), comfort, and
   the general Ask that b2-0 seamed (invitations, favour requests, loans), with accept and
   decline as real outcomes. Distinct consequences, not different wording. First part built
   2026-10-13, opt-in (`--catalog invitations`; [invitation spec](specs/invitation-spec.md)): a game
   or a drink is asked for through the Ask; a yes begins it, a no is a `Refused` act the asker feels
   and others see, and the asked person answers in an authored reply line. People mostly invite
   those they like, so noes are rare (2 of 22 in a seed-7 month). Still to come in this step:
   company and comfort through reflection, favours and loans.
4. **Follow-through**: one concrete need → request → agreement → attempt → outcome thread, so
   help has an object and a next day. After 3, so there are things to ask for. First thread built
   2026-10-13, opt-in (`--catalog errands`; [needs spec](specs/needs-spec.md)): someone needs one
   named thing (flour, a coffee, a bottle of beer), asks someone in reach, and a yes is a promise
   the other keeps by walking to the counter while its sign says open, buying it and handing it
   over, late, or never (a let-down the asker feels). A need nobody takes on lapses. Across six
   seeds, 4% of promises broke, all by people who work through the shop's hours. Penny won't fetch
   beer, even for her mother (Sid). Still to come:
   needs beyond shop goods, asking through reflection, and the player as the one asked.

   **Then, the Lives pilot** (Sid, 2026-10-14; [Lives assessment](lives-gap-assessment.md); D53).
   Histories, associations and meaningful actions come before any decision on how needs bars
   influence behaviour. One complete thread for Penny, Pam and Gus: history, recall, competing
   choices, an attempt, each person's perceived outcome and what they learn, refusals, postponements
   and failures included, on general mechanisms and character data with no name switches. Then the
   same mechanisms for Evelyn, George and Alex, before the population grows. Before it, seven
   anchors of the character pack that the [history
   audit](experiments/history-evidence-audit-2026-10-14.md) and its check found unsupported were
   corrected in their own PR (D53); the anchors stay background context for the models, not
   memories. Penny's sourced routine is built as character data (D54, 2026-10-14): no Saloon
   evenings, and her game's bedtimes as habits, not rules (Sid): from the hour, bed is a weighted
   free-time choice (the weight, 9, is ours: E); she sleeps until rested and can be up at 03:00 with
   nothing to do, gaps [recorded](experiments/penny-routine-2026-10-14.md) for the pilot rather than
   suppressed. Nothing of the pilot itself is built. The [pilot implementation plan](specs/lives-pilot-spec.md)
   now defines the build order, same-choice history comparisons and acceptance cases. Its
   [baseline capture](experiments/lives-baseline-2026-10-15.md) is built and checked against
   the merged routine: 35-day runs separate the current town from existing pilot prerequisites.
   Personal history/recall is the next runtime increment; the archetype editor is deferred.

   **Needs bars: unresolved** (Sid, 2026-10-14). Not scheduled and not rejected: how bodily
   pressures interact with the richer system is assessed once it exists, and no formula is chosen
   because it lowers aggregate boredom or loneliness. Law 1 stays as written. Sid's 2026-10-13 wish
   to revisit them, and the shape proposed then, stay on record in the [design](design.md), section
   11c. Penny's refusal to fetch beer, for anyone, her mother included, stays built (D52): Sid's
   product commitment, not extended to accepting or drinking alcohol without a separate decision.
   Her trying to minimize her mother's drinking no longer waits for bars; the pilot takes up her
   care for Pam outside bars (proposed).

5. **Player speech** (Sid, 2026-10-12 and 2026-10-13; [spec](specs/player-speech-spec.md)).
   The Newcomer is the player's character and gets the player's voice: the player types, Laya
   reads the line into one of the acts the engine offers (and, for a claim, into one of the things
   the listener knows of), and the engine executes it through ordinary eligibility. Words are not
   facts: a listener takes a claim on trust, asks to see, demands or doubts it from their own
   memory and trust in the speaker, a check is an event others can witness, and a lie taken on
   trust comes out only through perception. Built so far: a probe of whether Laya can read typed
   lines at all ([write-up](experiments/laya-categorize-2026-10-13.md)). It needs 3 (asks with
   answers) and 4 (needs with real items) first, and follows the Lives pilot (Sid, 2026-10-14:
   "The observer product comes first; player gameplay remains later"); scripted player lines come
   before live typing, and live typing waits for a gate on Sid's own lines (the spec's "The gate
   before live typing").

Then batch 2's visits, dates, peace and festivals as the backlog, each behind its own switch.
The text REPL in which Sid is the Newcomer is now the end of this order (step 5, amended 2026-10-13)
rather than unscheduled: it still waits for 3 and 4 and follows the Lives pilot, because before them
a typed line would have little to become.

## 1. Concrete encounter context and event appraisal

**First observer slice built (2026-10-09):** roles, existing causal links, separate private causes,
perceived accounts, provisional personal/listener appraisals, reasons and everyday grouping.
Review the prepared scenes before extending these rules. Concrete tasks/items/costs and
listener-specific gossip selection remain; neither has been inferred from generic act names.

Build [the contextual-events slice](specs/contextual-events-spec.md) around existing help, gift
and argument events before multiplying generic headlines.

1. Record roles, actual purpose/activity when supported, causal event links and relevant facts
   at the encounter's time. Separate public event facts, private intention and each person's
   perceived account. Missing details remain unknown; never decorate a generic favor with an
   invented emergency, item, task or hidden motive.
2. Separate **personal significance** from **interest to a particular listener**. Relationship,
   known recent history, repetition, actual cost and consequences can supply reasons. Being
   spouses is an expectation prior, not an unconditional exemption from meaningful events.
3. Explain highlights with recorded reasons. Ordinary reciprocal care belongs in everyday life;
   a supported attempt to repair a recent hurt can be a relationship moment. The same encounter
   may matter to George while giving a neighbor little reason to gossip.
4. Group repetitive everyday activity in the observer view without deleting events or memories.
   First change observer presentation, then integrate listener-specific appraisal into gossip
   in a separately verified change. Keep authority/reporting semantics explicit and separate.

**Done when:** an observer can distinguish routine care from a meaningful exception and open
the supporting history. Rewinding does not expose later context. NPC appraisals use their own
knowledge, including mistaken identities. Unsupported details are visibly absent. Tests cover
those boundaries, repeated events, causal links and older recordings.

## 2. Broader actions and context-tagged speech

**First response slice built (2026-10-09):** source-specific thanking and apologizing, two new
authored daytime/dream proposals, execution rechecks and recipient outcomes. A four-run 28-day
test pack enables the existing action catalog and records backend/settings receipts. Company,
comfort and concrete requests remain next; the generic help/gift still has no task/item detail.

Connect existing catalog actions to reflection before duplicating their mechanics. Start with
thanking and apologizing, then company and comfort where eligibility and source facts support
them. Add requests, offers and refusals where there is an actual supported situation to answer.
More actions are useful when they produce distinct consequences, not merely different wording.

Each option needs a complete authored line, an executable action contract, source requirements,
opportunity/cost rules, a recipient response and an observable outcome. A changed idea must
agree with the offered act. Preserve defer/reject and use context to offer a small appropriate
set rather than squeezing the whole catalog into every Laya request.

**Done when:** several comparable encounters can lead to substantively different responses
and consequences. The packet fits Sid's 1,250-character working budget without sacrificing full
lines or required known context. Disabled slices, age/location limits and source requirements
still hold; model output cannot bypass them. New recordings identify the enabled catalog slices.

## 3. Follow-through and everyday situations

Give help something concrete to accomplish. Start with one small shared-task or help-request
mechanism with an actual need, effort and completion state; choose its task domain in its feature
spec. A request can be accepted or declined. An agreement records who undertook what, for whom,
by when, and whether it was fulfilled. No debt or promise exists just because an LLM mentioned it.

Build one continuing thread: need/request -> agreement/refusal -> attempt -> fulfillment or
failure -> remembered response. Later reflection can draw on what the person learned about it.
Carry unresolved hurts and favors forward through identified events instead of resetting each
encounter or manufacturing drama after the fact. Seeking someone to act is a later extension
of this slice and must use believed whereabouts.

**Done when:** the observer can follow a concrete obligation across days, including failure,
refusal and expiry. Fulfillment changes the modeled situation. Consequences cite actual events;
resource accounting and private knowledge remain sound.

## 4. Controlled model comparisons, alongside scene review

Keep authored, authored-plus-Laya and generator-plus-Laya paths. Compare generator changes with
the evaluator held fixed, then evaluator changes with the same prepared scenes/proposals.
Existing model/endpoint configuration is the starting point; do not hard-code one model as the
character's permanent mind. Record model IDs/revisions when supplied, prompts, outputs, weights,
fallbacks, costs/latency and the sampled decision.

An equal world seed controls starting conditions, not the subsequent history once choices diverge.
Use identical decision scenes or matched run prefixes to isolate a model effect. Use recorded
answers for exact replay. Compare interpretations, unsupported claims, repeated phrasing, changed
choices and what follows; no prescribed share of kindness, conflict or agreement is a quality goal.
Prompt/catalog improvements come before tuning. Fine-tuning is a later choice based on recurring
failure cases and an independently reviewed held-out set.

**Done when:** the observer can identify which model proposed an idea, how the evaluator treated
it and what the engine executed. Differences and limitations remain inspectable rather than
being summarized as one realism score.

## Later

Longer ambitions, confidences and secrets, invitations, romance, community decisions and life
events can build on concrete encounters and follow-through. The broader
[action inventory](actions-and-twists.md) is a backlog, not a commitment to implement every row
in its old batch order. Generations, large-town expansion and player farming are not prerequisites for the
next observer milestone; the text REPL is scheduled as step 5 of the agreed order. A Godot renderer remains a later engine
direction; current experiments stay on .NET 8.

## What must hold, and what we measure

Required: valid causal and source links, knowledge/privacy boundaries, eligibility and costs,
resource conservation, bounded/cancellable model work, truthful fallback labels, safe display,
clock-aware inspection, and exact replay with the same inputs and recorded answers.

Diagnostics: feud/friendship counts, war/dead-town labels, event volume, reach distributions,
relationship drift, ablations and variety measures. Investigate unexplained feedback loops and
broken mechanics, but do not tune away a coherent extreme outcome to pass a social quota.
An ablation's 20% effect is evidence about aggregate behavior, not a universal rule for keeping
or removing a feature. A small change can still make a character's experience more intelligible.

Review complete scenes, including ordinary care, rejection, disappointment and failed plans.
The question is whether an observer can follow the person's perspective and wants to see what
happens next. Repeated spectacle and a required number of dramatic events do not establish that.

## Documentation precedence and preserved history

For priorities use this roadmap; for an implementation use its current feature spec and source.
Older specs, research plans and dated experiment results are retained to explain the code and
earlier choices. Their batch order, E1/E2 thresholds, news caps and REPL-first gates do not govern
new observer work. Do not rewrite measured results to make the new direction appear already built.
The mod's shadow mode, template-only dialogue and game-thread restrictions remain mod policies.

Work uses `codex/reflection-prototype` as the ongoing base and reviewable PRs targeting it, as
specified in [AGENTS.md](../../AGENTS.md). The current reflection PR remains the review unit
for the prototype and this roadmap; starting a new feature follows the repository branch rules.
