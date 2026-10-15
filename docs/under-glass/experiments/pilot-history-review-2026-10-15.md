# Penny, Pam and Gus: starting-history admission review

Reviewed 2026-10-15 by Codex against the local, case-sensitive English dialogue export from
Stardew Valley `1.6.15.24356`. This admits the small explicit
[`sim/history/penny-pam-gus.json`](../../../sim/history/penny-pam-gus.json) pack for the pilot.
It does not convert the character inspiration pack or copy anyone's biography into an archetype.

Sid approved the policy of individually reviewed, modest present readings in the
[Lives assessment](../lives-gap-assessment.md), section 0, and requested that the next pilot connect
these histories to evening plans and Laya. The pack records **Codex** as the individual content
reviewer; it does not claim Sid personally approved each paraphrase. All entries are optional
input, never silently loaded into an ordinary town.

## Source check

The source read directly was the existing local export
`out/stardew-dialogue-2026-10-12/raw-assets.json`, SHA256
`1396930b5fe40578e1a50f9ac07221a017426645d4c79f6234f454f020e27e30`.
This matches the [history audit](history-evidence-audit-2026-10-14.md). No fresh game extraction
or external wiki was substituted. Raw game dialogue remains in ignored local output; the pack
contains short paraphrases, not a redistributed dialogue corpus or newly authored spoken lines.
These are remembered accounts and readings; they are not alleged verbatim quotations.

There are **9 facts/pasts/practices, 12 personal accounts, 21 readings and 24 source keys**.
Every key in the pack was read. The table below uses `Penny:Mon4` as shorthand for the exact
stored `Characters/Dialogue/Penny:Mon4` key. The branches and admission limits matter as much as
the source names.

| Reading or account | Source checked | Admission and limit |
| --- | --- | --- |
| `ppg-penny-care` | `Penny:fall_Thu6`, `Mon8` | R2: she worries about Pam, wants her happy and also wants to move out. A small warm association toward Pam represents care. This is a present reading placed at the start by our approved policy; it does not turn kinship into affection or claim the worry began at a known date. Her wish to leave stays text. |
| `ppg-penny-burden` | `Penny:Fri4`, `Sun4` | R2: her complaint about dishes, cleaning alone and Pam's headaches supports a wary association toward Pam. It does not establish that Penny witnessed the drinking. |
| `ppg-penny-saloon-wary` | `Penny:Mon4`, first statement only | R2: she calls Pam's excessive visits a problem. None of the following farmer question, choices or advice is admitted. This shares the burden's account/cause, so considering both Pam and the Saloon cannot count that experience twice. |
| `ppg-penny-pam-away-weekday`, `ppg-penny-pam-away-weekend` | `Penny:Mon4`, `Fri4` | R2: one negative whereabouts seed per applicable evening key. The 18–21 band and weekday/weekend encoding are our coarse model of her account, not a sourced schedule or two remembered nights. She does not know where Pam is tonight. |
| `ppg-pam-penny-warm` | `Pam:fall_Thu`, `winter_Tue4`, `winter_Fri` | R2: protective care, hopes for Penny and pride in her. Three citations support one association, not three reinforcements. No family warmth is inferred from the relationship alone. |
| `ppg-pam-saloon-warm` | `Pam:Saloon`, `Saloon_Entry`, `Saloon_Thu` | R2: pleasure, anticipation and returning for her usual drink. Recurring practice, with no invented drink count or dated drinking episode. |
| `ppg-pam-saloon-wary` | `Pam:summer_Thu4` | R2: her own judgment that repeatedly ending up there is unhealthy. It remains beside her warm reading, without netting the two into a single label. |
| `ppg-pam-hobby-wish` | `Pam:Fri2` | R2, stance **None**: she wishes for a hobby and considers solitary checkers. The generic game activity and wish cue are our action vocabulary. She has not said she enjoys games, wants to play with Penny, or has already played with her. The wish may open a possibility but gives no initial game warmth. |
| `ppg-gus-pam-wary` | `Gus:Thu8` | R2: he judges Pam's beer consumption excessive while restraining his judgment of customers. The eight-heart disclosure does not establish the start of this feeling; carrying it back is explicitly our baseline choice. No reciprocal friendship warmth is inferred. |
| Four `ppg-gus-pam-saloon-*` readings | `Gus:Thu` | R2: one positive whereabouts seed for each 18–21/21–24 and weekday/weekend key. Broad evening bands are ours, not a claim he saw her in every band on every night. A later observed miss affects only its particular key. |
| `ppg-penny-reading-warm` | `Penny:Thu` | R2: she enjoys reading as a way to set aside life's difficulties. The object is **Reading**, not her house or a game. The source does not prove she likes every evening at home. |
| `ppg-penny-teaching-warm` | `Penny:Wed` | R2: teaching Vincent and Jas and valuing their opportunities. The activity is separate from managing Pam; it does not manufacture a completed lesson in the event log. |
| `ppg-penny-father-autumn` and its account | `Penny:fall_Fri6` | R1, stance **None**: childhood departure and his reason as Penny reports it. The autumn cue is her stated connection. No numeric stance toward Pam, no invented departure date, no explanation of Pam's drinking and no conversion of a present absence into abandonment. |
| Three `ppg-pam-truck-*` readings | `Pam:fall_Sun4` | R1: her own stated missing of past truck work supplies the required present link. Travel, meeting people and radio are separate activity objects with **one shared cause**, so a query over all three still contributes one 0.05 warmth. These activities are not yet available as pilot actions. |
| Pam's bus-job account, no numeric reading | `Pam:Wed`, unrepaired-bus branch | R1 past: laid off from bus work. The repaired-bus response is excluded. Her wish to have it back stays in text; this pack adds no numeric association. |
| Penny's bus-work account, no numeric reading | `Penny:summer_Fri`, `Tue4`, unrepaired-bus branches | Her account of the broken bus, Pam's unemployment and difficult saving is kept separately. Filing this account as Inferred is our conservative acquisition classification: no source establishes a conversation or scene in which Penny learned all of it. It does not establish that the broken bus caused the layoff, or that unemployment caused drinking. |
| `ppg-pam-gus-warm` | `Pam:Sun` | R2: her positive judgment and birthday beer practice. This is Pam's account only; Gus receives no invented memory or feeling in return. |

The shared Saloon practice also cites `Gus:Sun` for his stated business reliance on regulars.
That business reading stays in Gus's account, not in a fabricated purchase, till observation,
warm association or exact financial forecast. The source supplies no private reading for Penny
or Pam merely because Gus holds it.

## What is shared and what stays personal

`ppga-saloon-practice` identifies **one recurring practice** with exactly three accounts:

- `ppga-penny-saloon`: **Inferred**. Penny experiences the housework and headaches and states a
  cause. Assigning Inferred to the composite account is our conservative representation of the
  uncertain acquisition route, not a claim that canon names her reasoning method. She is not an
  eyewitness to Pam's drinking.
- `ppga-pam-saloon`: **Lived**. Pam's pleasure, ambivalence and wish for a hobby belong to her.
- `ppga-gus-saloon`: **Witnessed**. His own account describes her presence and drinking in his
  workplace. This starting knowledge does not allow later simulated purse charges to become
  witnessed drinks: new evidence still needs a real perception path.

Penny's care is a different account, `ppga-penny-care`, so burden and care retain independent
causes. Penny's two wary projections share the Saloon account and contribute once when combined.
Pam's opposite Saloon signs are preserved together on her one account. Source overlap cannot
reinforce either person several times. A shared fact ID does not grant access to the other
holders' accounts, and removing one holder's history cannot erase another person's knowledge.

The pack stores no forced pre-run dates, repetition counts, acts, commitments, model thoughts or
past agreements. Accounts are available at `KnownAt = -1`. The dialogue's friendship and seasonal
conditions are authoring provenance, not invented conversations with the farmer in this run.

## Numeric initialization and consumer contract

The existing memory kernel supplies the same default **0.05** association strength to each
reviewed warm/wary reading. This experimental number is ours, not in the JSON and not implied by
canon's wording. A first lived 0.08 experience can exceed it; lived experience also dilutes the
initial association by `1 / (1 + lived entries for the object)`. The pack changes no traits,
regard, mood, age, bedtime, daily routine or gathering weights.

Expectations use the existing `CompanyPolicy.WhereaboutsKey` format:

- Penny: subject `Pam`, `Home:Trailer:18-21:weekday` and `:weekend`, initial failure, giving 0.5.
- Gus: subject `Pam`, `Saloon:18-21:weekday`, `Saloon:21-24:weekday` and both weekend equivalents,
  initial success, giving 1.0 under the current optimistic prior.

There are no seeded answers, availability, follow-through or game-outcome expectations. An
evening yes, a missed agreement, an observed absence and a completed game must produce their own
distinct evidence later. The model must not treat the 1.0 prior as a certainty supplied by canon.
These same choices and their observations should be inspected with the relevant seed removed.

`ppg-pam-hobby-wish` is a reviewed, stance-None reading of Activity `PlayedGame`, with the
explicit ordinary-weight cue `wish:activity:PlayedGame`. A consumer can require that record to
occupy an actual bounded recall slot before using it to open a game option. The cue and mapping
from solitary checkers to the generic game menu are ours; neither is marked as a sourced cue.
This gives permission to consider a possibility, without predicting enjoyment or forcing a yes.

Stable comparison targets include `ppg-penny-care`, `ppg-penny-burden`,
`ppg-penny-pam-away-weekday`, `ppg-pam-saloon-warm`, `ppg-pam-saloon-wary`,
`ppg-pam-hobby-wish` and `ppg-gus-pam-saloon-18-21-weekday`. Removing a reading removes its own
seed. Removing an account removes its readings. Removing a shared fact removes only the current
holder's dependent history in a personal frame. Scene, option legality and draw must otherwise
stay fixed; a wish removal that closes an option is an eligibility comparison, not a matched
odds comparison.

## Explicit omissions and checks

Penny's alcohol-provision commitment remains the existing product decision and `WontFetch`
character data. This pack neither makes that refusal probabilistic nor extends it to accepting
or drinking alcohol. No source here proves an absolute alcohol prohibition. No numeric history
is seeded for Pam's supposed reason for drinking, a farmer-mediated family quarrel, the Saloon
debt resolution, a completed game, reciprocal feelings for Gus, or a particular hangover.
Pam's first-hearing `Thu`/`Mon` hangover pages are not used at all; her daughter reading has
independent sources. Future populations inherit no named pasts through archetype cloning.

`LivesPilotHistoryTests` validates admission and protects these boundaries: separate holders,
one shared practice, independent care/burden, wish without warmth, past with an explicit present
link, autumn recall without a Pam link, distinct expectations, first-outcome learning, and a
frozen Penny choice whose odds change when burden alone is removed. The tests do not substitute
for source review and do not claim the new evening runtime or a Laya response has been verified.
