# Under Glass: current development roadmap

Updated 2026-10-09 after Sid reviewed the authored, Laya and local-generator recordings.
This is the current order of work for the independent game. It supersedes older build orders,
event-volume budgets and social-outcome acceptance thresholds. Existing code, regression tests
and dated measurements still describe what was implemented; a planned change is not already live.
The Stardew mod has its own [roadmap](../spec/roadmap.md) and policies.

## Product and method

The first product is an in-depth social simulation watched from above. An observer can follow
what a person knows, imagines, chooses, attempts and experiences. Player gameplay comes later.
Sid finds the reflection recordings promising, including local inspiration plus Laya. Continue
that direction while making encounters more specific and expanding what people can actually do.
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
- Built: current-moment cards, source/consequence navigation, character timeline markers and
  hover descriptions, and playback controls that remain visible while scrolling.
- Built: supported encounter facts, private cause receipts, acquisition-time perceived accounts
  and authored appraisals. Stories shows contextual reasons and groups everyday repetitions.
- Current limits: reflection offers gift, help or confrontation, plus defer/reject. Some richer
  actions already exist in `ActCatalog` behind switches, but reflection does not offer them.
  Generic acts do not record what help or a gift concretely involved. Gossip tiers remain fixed
  by action-kind juiciness; the new observer appraisal does not yet choose what NPCs retell.
- Current model limits: some generated ideas disagree with their suggested action or reverse
  remembered roles. The next work improves grounding and observability alongside the world;
  it does not wait for a perfect generator or start with fine-tuning.

See [reflection status](specs/reflection-spec.md), [implementation reference](../../sim/README.md)
and the dated [reliability measurements](experiments/reflection-reliability-2026-10-08.md).

## 1. Concrete encounter context and event appraisal — next

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
in its old batch order. Generations, large-town expansion, a text REPL and player farming are
not prerequisites for the next observer milestone. A Godot renderer remains a later engine
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
