# Lives: a gap assessment and the smallest complete slice

Fourth revision, 2026-10-14, corrected after its check: the third revised for Sid's fourth direction, relayed by
Astra. **Approved** by Sid's fifth direction the same day (Appendix E): the pilot proceeds history-first, with O1-O3
decided (section 14); the mechanics below stay proposed until the pilot PR builds them. It answers his four directions of 2026-10-14
(verbatim in the appendices) and takes in Codex's history audit, `experiments/history-evidence-audit-2026-10-14.md`
(uncommitted; cited, never changed), with seven inspections, an evidence check and a thread design, all outside the
repo. **Measured**: the scaling inspection's shipped-town replays (seed 1, 112 days, errands and bars off) [base], unless
stated. Every mechanism below is proposed unless marked built.

**Labels.** Evidence has a *kind* (the audit's editorial distinction, not a runtime enum): **Fact** (identity, kinship,
possession, role), **Past** (completed), **Practice** (recurring), **Reading** (an interpretation, desire or
preference); and a *standing*: **S** sourced canon, with its key (a character's own stated reading is S, theirs); **A**
Sid's dated product decision (the 2026-10-14 ones via Astra); **E** ours (a link from a past to the present that its
holder doesn't state is an *authored interpretation*, E); **NA** not admissible as a starting state; **D** for the run
to derive. R1-R3 are the audit's admission rules (2); "S·R2" is sourced content placed at the start by our choice. Keys
are `Characters/Dialogue/<Name>:<key>` unless shown. **Code**: [base] on `codex/reflection-prototype` (779e3b8); [#90],
[#91], [#92] only in that open PR (errands; the bars spec and law 1's amendment, docs only; the bars experiment, draft);
*proposed*: no code. "On" means on in `DefaultTown.Feelings()` (`DefaultTown.cs:186`): feelings, the gate, Fond, stance,
power weight 1; catalog slices, errands, Pity and reflection are off. Paths are under `sim/UnderGlass.Sim/`; line
numbers are b3224f4's, earlier on base where #90 and #92 insert (up to 20 in `Simulation.cs`, 41 in `DefaultTown.cs`
after 321, 28 in `ActCatalog.cs`, 11 in `Acts.cs` after 56, 8 or fewer elsewhere).

## 0. What the fourth direction and the history audit change

**The fourth direction** (Appendix D) keeps law 1 unchanged, bars experimental and the thread, and revises five things,
each marked "revision n" where it lands: (1) a holder's own account tying a **completed past** to a present attitude
supports an association or expectation, never a disposition, and other links are authored interpretation (E) (rules 1,
3; sections 3, 7, 10); (2) **learning starts at the first outcome**, mixed ones included, "three agreeing" only a
tunable heuristic (rule 9, 9.5, 8); (3) **an agreed evening** may disappoint, the observable outcome apart from each
person's interpretation (9.4, 9.5, 8; O3); (4) **Laya and the fallback share one action space** (rules 4-7, 9.4,
9.8); (5) **ordinary encounters start threads** in the first slice, nothing forced (rule 11, 8, 10, 12).

**Decided, 2026-10-14 (Sid via Astra; A).** Earlier decisions stay dated history (D52 and #91's answers, 2026-10-13).
- **Cast**: Penny, Pam and Gus pilot; then Evelyn, George and Alex as the transfer test, on the same mechanisms (6, 10).
- **Routines**: unsupported starting routines are corrected as character data, Penny's evening place first (13), not by
  strengthening history weights against an arbitrary Saloon pull (her records alone cut it about x0.82 an evening at
  first guesses); later experience can change routines (rule 5), keeping Sid's 2026-10-13 wish that her choice "comes
  from the system"; exact bedtime fidelity is a separate choice (O1).
- **Penny's absolute refusal to provide alcohol** is Sid's product commitment (9.7), not extended to accepting or
  drinking alcohol without a separate decision.
- **The seven evidence corrections** (2) go in a separate PR before `histories.json` reuses them as executable history.
- **Law 1** unchanged, its original sentence governing; conflicting amendments on hold; bars unresolved, not
  prohibited, and the bars experiment (#92, a draft) not the recommendation (13). **Today's population measurements are
  the baseline**; added costs are measured apart (11).

**Decided by the fifth direction (Sid, 2026-10-14; A; Appendix E).** Proceed history-first: history, recall, competing
choices, attempt, perceived outcome, learning; law 1 unchanged; bars aside; the observer product before gameplay.
- **O1**: Penny's sourced routine is the baseline, with canon's bedtimes; the pilot's evening is one on which the
  activity is feasible, never a day extended to make it work; the sim's later bedtime is kept as an explicit
  comparison. Implementation ambiguities the sourced routine exposes are named, not filled with invented canon.
- **O2**: individually reviewed present readings are seeded modestly, keeping their source and admission standing;
  lived outcomes can outweigh them. Pam's stated wish for a hobby makes the activity possible; it gives no prior
  enjoyment or warmth toward a game with Penny.
- **O3**: an agreed evening's disappointment goes to mood and the hoped-for evening, with no automatic judgment of
  rejection or betrayal. A later interpretation aimed at Pam needs Penny's own supported interpretation and must not
  count the outcome twice.
- The seven evidence corrections land first, in their own PR; the observer contract is revised to 0.2 before any
  runtime writer; the pilot is built from general mechanisms and character data, never name switches; a second
  household follows before any population expansion, with CPU, recording size, memory and model use measured apart.

**What the audit changed** (the third revision): its model of histories (2), conflict cases (3) and knowledge boundaries
(7), its counts reproduced by the evidence check, brought the Trailer's cast (6), the thread (8), the `Commitment` (9.7)
and Penny's present care, admitted only by our R2 choice (audit finding 5; O2). The slice waits for #90.

**One limit, narrowed** (revision 1). Numbers move through present readings carried to the start (S·R2, O2: p4-p6,
m1-m4, g2-g3), Sid's commitment (A) and completed pasts (R1) their holder ties to a present attitude: in the pilot only
Pam's truck job (m6), a warm entry whose activities the town lacks. Penny's father's leaving (p2) is tied by her account
to a time of year, not to anyone present: it reaches reflection and reasons, moving no number. The audit's case 3 (a
past moving a present choice) is met by George (10) and Marnie (a format test); in the pilot, by reasons and Laya alone.

## 1. The chain as built

| Link | Built | Partial, or built and off | Missing |
|---|---|---|---|
| **Experience** | Acts are perceived into beliefs (`Belief`, `Model.cs:264`; writer `Add`, `Simulation.cs:1141`) and felt from the belief, with a believed cause (`Feel`, `Simulation.Feelings.cs:202`; `FeltRecord`s). Mood keeps its source act; acts keep their `Location` [base] | Company after a chat: a small joy with no source act (`Simulation.Feelings.cs:169`) [base] | Experience that is not an act toward someone. Anything before the run: `Act` ids index the run's own list |
| **Remembered association** | Regard per ordered pair; kind-regard; familiarity; fear as hostile hits over 28 days (`Hit`, `Simulation.Desire.cs:247`); stance [base] | Sentiments: named, both signs, a cause, the strongest three a pair (`AddSentiment`, `Simulation.Feelings.cs:469`); no rule reads them (`Model.cs:311`); a renewal overwrites the cause (`:472`) [base]. Good-time counts per place [#92] | Associations with places, activities and situations (#91's negative-only place feelings are a spec: proposed); contradictions a rule reads; recall by cue |
| **Imagined possibilities** | Nothing on as shipped | Reflection: one of the 6 most recent memories of 7 days, drawn uniformly (`TryReflect`, `Simulation.Reflection.cs:89-103`); a fixed menu toward that person (`:208-225`) [base, off] | Possibilities from associations: go, seek, invite, stay in; anticipation of how it would go |
| **Competing desires** | Motives tried strongest first; the first past the gate acts (`Pursue`, `Simulation.Desire.cs:422`; `Weigh`, `:472`); daring reads `P`; a close call is drawn. The `Ask` weighs obligation against cost (`Simulation.Ask.cs:71`) [base] | Opposite motives toward one person coexist, but net regard decides. A reflection intention bypasses the gate (`ReflectAct`, `Simulation.Reflection.cs:303`) [base, off] | Alternatives compared side by side, the routine and home included, each keeping its reasons |
| **Action** | Free time is a keyed weighted draw over authored haunts, gatherings and home (`Decide`, `Simulation.cs:726`); acts toward someone in reach (`StartActs`, `:910`) [base]. The errand runner looks for the asker from memory (`LookFor`, `Simulation.Errands.cs:337`) [#90, off] | Fond wants only someone in reach (`Simulation.Desire.cs:453`). Invitations to a game or drink (`Asked`, `Simulation.Invitations.cs:33`) [base, off] | Going toward what a desire is about; activities with consequences; plans kept or broken |
| **Consequences → expectations** | Regard, fear, stance, avoidance, familiarity; a conviction raises a theft's believed risk (`Simulation.Money.cs:307`); observers learn others' outdoor hours (`Habit`, `Simulation.Habits.cs:37`) [base] | Life-record outcomes, read only by an off rule (`TallyOutcome`, `Simulation.Withdrawal.cs:394`); a refusal brings a fixed 7-day wait (`Simulation.Invitations.cs:47`) [base] | Expectations of where someone is, whether free, how they answer, whether they follow through; outcomes told apart; a starting state experience can revise |

**Power, corrected (direction 2).** `Weigh` reads `P` in the pursuit gate (`DesireMath.Effective`,
`Simulation.Desire.cs:489`; `Feelings.cs:497-500`) [base]: up to ±0.5 against a clear band of ±0.15, so `P` decides
whether a pursuit happens. The larger gap is upstream: where possibilities are made (haunts, gatherings, `HomeWeight`,
Fond in reach, reflection's menu), nothing reads mood, sentiments or who is usually where.

**The missing connections.** (1) Experience → association runs only toward people and kinds. (2) Nothing recalls an
experience when its person, place or activity recurs. (3) Only avoidance moves people (`Withdraw`). (4) No hope or fear
enters a choice; reflection's intention skips the gate. (5) Outcomes return as one regard number. (6) History: none.

**The "pure Spinoza" result.** #92's mode 3 adds `BarSadness` to `Conditions` (`Simulation.Bars.cs:274`) [#92], so it
lowers `P`, daring and the rate tilt. With no route to what might relieve sadness (III P13, P28, P36; VERIFY), it could
only make people sadder and less daring: the ninefold rise in low-power days measures that missing link, not law 1.

## 2. Histories: the audit's evidence model

**Seeded today**, all shaping behaviour [base]: traits on cards (`DefaultTown.cs:220-249`; first guesses, the mod's
fixture, Sid's readings for Penny and Pam); kin and households (`Ties()`, `:377`; canon, but Shane "is not Marnie's
kin", `:375`, though `Data/Characters` makes him her nephew); friends (unsourced), tensions ("VERIFY against heart
events") and familiarity (`SeedFamiliarity`, `Simulation.cs:366-374`), as **set-points** (`CloseFeelings`,
`Simulation.Feelings.cs:651-655`: a tie formed in the run decays back to the card); haunts, jobs and gatherings
(guesses shaped by schedules; Penny's evenings to become her data, A); `WontFetch` [#90] (D52, A). Named exceptions
(`WontFetch`; `CoreMemory`, Pam's addiction numbers [#91]) have no recorded past. **History is text**: the pack (12
archetypes, 34 profiles, 101 anchors, `sim/character-inspiration.json`; reviewed in D44, now audited) shapes nothing;
an anchor reaches a run only as "Initial own history" in a reflection request (chosen by `Resolve`,
`CharacterInspiration.cs:126`; written at `sim/UnderGlass.Minds/ResilientReflectionMind.cs:324`) [base], and D44
forbids it to create an act, belief, feeling, regard prior or gossip.

**The audit's model, adopted for histories** (proposed, for a reviewed `sim/histories.json`):
- **Not memories.** The anchors are private prompt context; one holding several kinds is split before use.
- **Admission.** **R1** an explicit old experience (childhood, a prior home, a completed move, an old accident) is
  admitted undated, with only its old content, never the later disclosure scene. **R2** an established role or routine
  is a baseline fact or practice; carrying a present-tense source to the start is our choice (E). **R3** a current
  reaction or change needs independent pre-arrival evidence, else stays out. No date, count or weight follows.
- **Knowledge boundaries.** Heart keys date the hearing, not the fact (game `NPC.cs:4058-4071`). One participant's
  account gives neither another's feelings nor their knowledge (Evelyn's account of meeting George gives George no
  feelings). Authoring provenance is not in-world acquisition; `subjects` is a relevance hint, not who knows. An account
  may be partial or wrong; a stated guess stays a guess.
- **Count once.** A fact or practice held by several is one record with an account per holder (`jas-shane` with
  `jas-parents`; Leah's one move, two reasons; Pam's Saloon nights, 7). Overlap never reinforces twice.

**Qualifications on the pack's evidence** (all four confirmed; corrected in their own PR before reuse, A):

| Anchor | Sourced (S) | Ours in the pack (E) | Treatment |
|---|---|---|---|
| `maru-clinic` | `Maru:Wed`: Harvey likes having her around in case equipment fails; she works Tuesdays and Thursdays | a completed repair | employment and an anticipated role, as Maru reports Harvey's wish; no repair |
| `linus-tent` | `Linus:Fri`: his tent was destroyed before (no agent named); a plea not to destroy it | "someone"; the causal "makes me wary of visitors" | the destruction as his account, perpetrator unknown (Past, R1); the plea kept separately as voice, not a starting state, as it is addressed to the farmer (our departure from the audit's "keep", E); general wariness an interpretation to review |
| `demetrius-valley` | `Demetrius:Tue`: the ecosystem was one reason he was excited to move | the move's purpose, folded in from `Introduction`'s present work | the ecosystem appealed to him when he moved; not why he moved |
| `alex-childhood` | `Town:2481135`: father left, mother died, move to grandparents; no age stated | "when I was young" | corroborate with `Data/Events/Beach:288847/f Alex 2000/p Alex/w sunny/G !IS_PASSIVE_FESTIVAL_TODAY SquidFest` ("too young" to thank her; its "twelve years ago" floats, so no date), or drop the qualifier |

Three more, from the evidence check: `clint-pressure`'s "not the future I wanted" is not in `Clint:Mon4` (cite `Sun8`'s
weaker "didn't really dream of"); `jas-parents` also cites `Jas:Mon`, silent on her parents; `george-evelyn-meeting`
gives George a first-person account sourced only to Evelyn (`winter8:Evelyn`). Records are in 9.2; no named exceptions
(a name-swap test); card seeds stay, marked unsourced, unless a history covers the pair.

## 3. Associations, and conflicting feelings kept apart

**As built** [base]. Regard (`_regard`, `Simulation.Feelings.cs:16`), kind-regard (`:17`) and fear (`_hits`) are one net
number each, warmth and embarrassment cancelling. Sentiments are named, separate and caused, three a pair (three warm
names can evict a fading dread), and read by no rule. Places have only #92's counts. Missing: meaning for places,
activities and situations; conflict held together and read; any pull on what comes to mind.

**Test cases from canon.** Each keeps the experience apart from its holder's reading. Where canon gives two sides (Leah,
Clint, Alex) both stay separate entries, recalled together, never netted (12); Marnie's is the audit's one-sided case.

| Case | Sourced (S) | Ours (E), or not admissible | Proposed records |
|---|---|---|---|
| **Marnie and the dock** | `Data/Festivals/summer28:Marnie_y2`: a childhood fall through a rotten dock; creaking wood makes her tremble; her own word is "traumatized". `summer28:Marnie`: stays near the beach, away from the crowded old dock | carrying the festival avoidance to the start is R2; extending it beyond the cue is ours; nothing sources warmth toward the beach | a Past (R1); her stated link supports a wary association toward a situation, the cue (creaking wood), and an expectation of trembling there (a "how it goes" key, 9.2; revision 1); no general fearfulness inferred; no warm entry seeded (only lived ones could add one). No dock in town: a format test |
| **Leah and the valley** | `Data/Events/LeahHouse:50/f Leah 500/p Leah`: a childhood visit with her parents; she knew she had to come back; its artistic potential. `LeahHouse:51`: leaving the city and Kel's pressure | the art-show and internet suggestions are farmer-caused (NA); her parents' feelings and Kel's motives are unsourced | one visit (Past), one move with two readings, a pull (warm, the valley) and a push (wary, the city): the move counted once |
| **Clint and smithing** | `Clint:Mon4`: blacksmith because his father pushed him; prefers outdoor work. `Sun8`: much pride; didn't dream of it as a boy; wants to be the best | his father's intent; that he dislikes all smithing; placing the 8-heart pride at the start (S·R2: a late disclosure doesn't date its onset) | the pressure as his Past (R1); pride and the preference as Readings, S·R2: warm and wary toward one activity, both recalled, torn when both are strong |
| **Alex and his father** | `Data/Events/Town:2481135/f Alex 1000/t 900 1600`: his father was often gone, drank, called him worthless; his mother died; he moved. He guesses at his father's motive and marks it a guess. `Beach:288847`: his mother's care, lunches, playing catch | treating the guess as fact; linking his mother's catch to George's wish (`George:Wed8`): no source joins them | Pasts (R1); the motive a Reading with certainty "guess", never a fact about his father; warmth toward his mother and catch, coldness toward his father, on different objects |

## 4. Actions

33 act kinds, 17 town kinds, 12 catalog rows, 4 errand rows [base, #90], many label variants of one mechanic (a gift has
no object; `PlayedGame` has no content, `Simulation.Acts.cs:262`). Against Sid's examples: **making**, nothing (errand
goods are never used [#90]); **learning**, familiarity and observers' hours; **caring**, `HelpedSomeone` [base, on: a
feelings-only kindness], `Comforted` [base, off], errands [#90], none following a refusal; **sharing**, a contentless
game [off]; **seeking company**, Fond in reach, invitations [off], `LookFor` [#90]; **repair**, none at home;
**solitude**, home weighted by avoidance, nothing to do there. The slice joins caring, sharing and seeking company.

## 5. Source philosophy versus our laws

| Spinoza (wording VERIFY) | Our law (`design.md` 3a) | As built [base] |
|---|---|---|
| Striving is each thing's essence; every desire is that striving (III P6-P7; Def. Aff. 1) | 1: `P` from 0 to 1; all wanting from this one striving | `P` = conditions plus mood; motives stirred by events; `P` scales daring |
| Joy and sadness: passages to greater or lesser perfection (III P11 schol.) | 2: mood is the recent trend of `P` | the reverse: `P` derives from 3 days of mood |
| Love and hate: joy and sadness with the idea of a cause, even an accidental one (III P13 schol., P15 cor.) | 3: regard toward the believed cause | people and kinds only |
| Association: what was felt together returns together (II P18 schol.; III P14-P16) | none | not built |
| The saddened mind recalls what excludes the sadness (III P13); we strive toward imagined joy (P28), long for recollected joy (P36) | 4: keep what brings joy, remove what brings sadness | motives toward a person, stirred by an event |
| Hope and fear from a doubtful image; disappointment, sadness at a past thing that turned out against hope (III P18 schol. 2; Def. Aff. 17); expectation from repeated sequences (II P44 schol.) | none | fear hits; temptation's risk; observers' hours |
| Vacillation: love and hate toward one thing (III P17 schol.) | none | regard nets; sentiments hold both, unread |
| People differ by constitution and by history (III P51, P57) | 12: trait multipliers | static traits; history plays no part |

We judge good what we desire (III P9 schol.): no rule maximises `P`, and none should. Ours, not Spinoza's: `P` in [0,1],
3-day mood, thresholds, trait multipliers, #91's amendment, #92's mode 3, every proposition cited in section 9.

## 6. The cast decision

| | **Penny, Pam, Gus** | **Ranch** (pre-audit choice) | **Evelyn, George, Alex** | **Carpenter's household** | **Emily, Haley** |
|---|---|---|---|---|---|
| Admissible evidence after the audit | strong and two-sided in each person; one practice with three accounts; R1 pasts: the father's leaving, the layoff, the truck job | narrowed: h5 NA (`Shane:Wed8`, R3); h6 (`Jas:Sun2`, its one number-moving history) an R2 reading carried back, a profile source in the pack (`character-inspiration.json:751`), not an anchor; godparent and bereavement one line; the stay recent (`Marnie:Tue`) | the strongest completed histories: George's accident; Alex's childhood, both sides; Evelyn's account of meeting George | mostly present feelings (`Maru:Sat`, `summer_Tue4`; `Sebastian:Thu4`, `fall_Tue6`); `robin-house`; two corrected anchors | friction (`Emily:Mon6`, `Haley:summer_Sun`); no sourced shared warm memory; the cushion event NA |
| A completed past at the choice | recall: the father's leaving, most strongly by her stated autumn cue, in spring only by a proposed one; the truck job, missed in Pam's words, a warm entry with no object in town; the layoff, uncued | recall only | George's account ties his accident to his legs (`JoshHouse:18`), his legs to a present wish (`Wed8`): his own links carry a wish and an expectation (revision 1) | Robin's house | the parents' absence, through chores only |
| Complete end to end | yes: ask, sure no, offer, answer, follow-through, game, learning for three | yes, with a new helping act; no follow-through to fail | yes, once rule 11's encounter offer opens on a warm reading or wish (each starting reading here has one sign) | only with the helping act | no: needs chores and cooking |
| A consequence [base] rules read, in 28 days | rare: with her evening corrected the purse stays above `WeekCost`, so `NeedPressure` is 0 and the 12 kept has no reader; a shorter evening moves about 0.02 of a scene; scenes reach Penny by testimony. Mostly the proposed records (where Pam was, who noticed) | Marnie's shift (the new act's own); Shane home 0.17 → 0.25 of a pick; Jas asleep by 17:30 | thin: time, company, associations | the helping act's shift | none built |
| Outcomes arising unforced | refused by commitment, agreed then absent, together, from a refusal or an encounter; a weighed no and not now in either method, rarely; busy needs a fixture | not found, busy, refused, together | refused, busy (Evelyn works 9-12), absent, failure | busy, refused, absent | busy, refused |
| Observation, testimony, inference | central and three-sided once her evening place is corrected (A); act testimony [base] reaches Penny from Gus and others | Jas sleeps through Shane's evening | the town contradicts Evelyn's reading: George goes out on 102 of 111 days [base, measured] | weak | Emily's belief about Haley's motive |
| New on the path | `Commitment`, rules 10-16, the game at home; waits for #90 | the helping act, seek by expectation | rules 11-14 and 16, the game at home | the helping act; repair at home | chores, cooking |
| Risks | alcohol at the centre; near #91's parked stay-in ask; natural threads rare; the game has no content | Shane's drinking nearby; no animals in town; kinship error | thin consequence; George's mobility unmodelled | four people; weak chronology | depends on making |

**Decided (A, 2026-10-14): Penny, Pam and Gus first**, as proposed. Each holds a sourced, two-sided account of one
household and one place, where the audit's knowledge rules matter most (7). The thread completes mostly on built parts;
over 28 days its outcome is where Pam spends the evening, who notices and what each records, its [base] consequences
rare (so its edge over the JoshHouse is small there). D52 stays a sure no, the refusal where other care begins (9.7).
Penny has her own pursuits, teaching (her job: it sets when she can ask or be asked) and reading at home (what the offer
displaces); Gus takes part only through real encounters (Pam's presence or absence, scenes he witnesses and retells
[base], the counter, his own errands [#90]).

**Where the evidence pulls the other way.** Evelyn, George and Alex have the stronger completed past at the choice (the
transfer test; revision 1) and no present alcohol; the Ranch needed neither #90 nor a routine correction. The helping
act comes next: it serves the carpenters, Evelyn's gardens, Penny's wish that Pam would help (`Fri4`), Emily and Haley.

## 7. The chosen cast's evidence

| # | Key | Paraphrase | Kind | Standing | Becomes (proposed) |
|---|---|---|---|---|---|
| p1 | `Data/Characters:Penny` (Pam `Relative_Mother`) | Pam is her mother | Fact | S | one relationship fact, an account each |
| p2 | `Penny:fall_Fri6` (`penny-family`) | her father left when she was little, saying he felt "trapped"; not seen since; this time of year brings it to mind | Past; Reading ("abandoned"; the autumn cue) | S·R1; cue S·R2 | a completed fact, her Lived account, his words as reported speech. Her own link is to a time of year: the autumn cue, sourced, strong, not exclusive; she states no attitude toward anyone present, so no stance is seeded. A proposed cue (a household member absent when expected) and any tie to Pam are ours (E): recall only, never making an absence mean abandonment |
| p3 | `Penny:Wed`, `Tue`; `Tue4` (start branch) | teaches Vincent and Jas; every child deserves a chance; paid tutoring; saving is hard with Pam out of work | Practice; Reading; Fact | S·R2 | her job [base]; one warm reading toward teaching; the pressure shown |
| p4 | `Penny:Mon4`, first statement only (the rest follow the farmer's answer, NA) | her mother has a problem with going to the Saloon too much | Reading about Pam's practice | S·R2 | a wary reading toward the Saloon; an *expects* entry: Pam away in the evening |
| p5 | `Penny:Fri4`, `Sun4`; `penny_dishes` (`fixtures/game/Penny.json`) | dishes; Pam always nursing a headache from late nights at the Saloon, so no help; cleaning alone | Practice; Reading | S·R2 | one recurring-burden fact; a cold reading toward Pam. "Late nights" is her stated cause; canon doesn't say how she knows it, and filing it as Inferred from effects is ours (E, per direction 2.5); "hangover" is our word |
| p6 | `Penny:fall_Thu6`; `Mon8` | Pam doesn't look after herself and she worries; the worry is a burden; she wants Pam happy; eager to move out | Reading, two-sided | S·R2: present feelings placed at the start by us (O2) | one warm reading toward Pam (care), kept apart from p5; "eager to move out" shown only |
| p7 | D52 (`docs/decisions.md`, 2026-10-13) [#90] | would do anything for her mom besides give her alcohol | product decision | A | the `Commitment`'s origin; not a source of warmth, which canon supplies (p6) |
| p8 | `Data/NPCGiftTastes:Penny`; game `NPC.cs:3667` | hates beer, wine, pale ale, mead and hops; when she names a loved gift of Pam's, the game re-rolls the drinks | Fact (game data and code) | S; read as a boundary, E | support for D52's characterization, not proof of an absolute refusal to fetch |
| p9 | `Penny:Thu`; `Pam:winter_Fri` | in a book it's easy to forget her life's realities; Pam: Penny likes old books | Practice; two Readings | S·R2 | a warm reading toward reading at home (hers); Pam's is Pam's |
| m1 | `Pam:Thu` (later page), `fall_Thu`, `winter_Tue4`, `winter_Fri` | Penny is her baby girl, the only one she has; hopes for her; proud of her | Reading | S·R2 | one warm reading toward Penny, four sources |
| m2 | `Pam:Trailer_Entry` | did Penny make dinner, do her chores | Reading (expectation) | S·R2 | an *expects* reading: Pam's side of p5, not a second burden for Penny |
| m3 | `Pam:Saloon`, `Saloon_Entry`, `Saloon_Thu` | enjoys it; looked forward to it all afternoon; "the usual" from Gus | Practice; Reading | S·R2 | a warm reading toward the Saloon |
| m4 | `Pam:summer_Thu4`, `Fri2`, `winter_Thu` | always ends up there; too close; "ain't healthy"; wishes for a hobby besides the Saloon every night, maybe checkers against herself; goes to warm up | Readings: self-judgment; a wish | S·R2 | a wary reading toward the Saloon beside m3's warm one. The wish is for a hobby of her own, perhaps solitary checkers (S); rule 11 lets any wish naming an activity open an offer to whoever is in reach (a general mechanism, proposed); "a game with Penny" is never her reading; warmth from the wish is O2 |
| m5 | `Pam:Thu4` | Penny says she spends too much time at the Saloon | Told (Penny to Pam) | S·R2 (4 hearts; chronology VERIFY) | Pam's Told account; seeds nothing for Penny |
| m6 | `pam-truck` (`fall_Sun4`); `pam-bus` (`Wed`, unrepaired branch); `Penny:summer_Fri`, `Tue4` | drove a truck route, liked the travel, people and radio; laid off as the bus driver (Pam); the bus stopped working (Penny); out of work (Penny) | Past; Reading | S·R1; the bus breaking as the layoff's cause is ours (E) | completed facts, the layoff with two accounts (Pam's; Penny's from two lines), recalled; her own "I miss" (`fall_Sun4`) ties the truck job to a present attitude: a warm entry toward its activities (travel, the radio, meeting people, her sourced cues), none in town; never the cause of her drinking |
| m7 | `Pam:Sun` | Gus is a good guy; free beer on her birthday | Reading; Practice (her account) | S·R2 | a warm reading toward Gus; Gus a participant, his feelings unsourced |
| g1 | `Gus:Introduction` (`gus-saloon`) | chef and owner | Fact | S | [base] keeper (`DefaultTown.cs:277`) |
| g2 | `Gus:Thu`, `Sun` (`gus-regulars`) | Pam and Clint come almost every night; he'd go out of business without them | Practice (witnessed); Reading | S·R2 | an *expects* reading: Pam at the Saloon in each evening band; the dependence shown |
| g3 | `Gus:Thu8` | Pam drinks way too much beer almost every night; he shouldn't judge his customers | Practice (witnessed); Reading, conflicted | S·R2 (8 hearts; chronology VERIFY) | a wary reading toward Pam; the restraint shown. Canon's witness of the drinking |

**Not admissible.** `Pam:Thu` and `Pam:Mon` first-hearing hangover pages (dated scenes, game `Dialogue.cs:749-770`);
`Data/Events/Trailer:35/f Penny 1000/p Penny` (a farmer-joined quarrel: voice only; Penny *infers* drink from a smell);
`Saloon:96` (resolved by the farmer); `Jodi:cc_Bus` (farmer-gated hearsay, canon's only layoff-to-drinking link);
`Gus:pamHouseUpgrade`. Candidate: `Penny:fall_Mon4`. **Derived (D)**: whether Pam goes out tonight; why she drinks;
whether Penny's inference is right on a given morning; any tie from her father to her mother.

**Boundaries, as canon draws them.** Penny never claims to see a drink: she sees headaches and mess and gives a cause.
Gus witnesses the drinking. Pam gives her own partial reasons (m4), unknown to Penny, and knows Penny's view (m5). Pam's
Saloon nights are **one** practice with three accounts, never several experiences each adding to Penny's dread.

## 8. The thread: the success criterion

**Since the fifth direction** (O1 decided): this weekday branch runs at the sim's bedtime, about 21:20, so it is now the
explicit comparison, not the baseline. On Penny's sourced routine a spring weekday leaves her 30 to 40 minutes after
18:00, so the baseline example moves to an evening where the game is feasible (a Saturday, bed at 20:00, in the
observer contract's 0.2 fixture). The mechanics and first guesses below are unchanged; the numbers that depend on the
window are recomputed in the pilot.

One weekday branch: the offer, a yes, the evening kept. First guesses (k 5, intended 0.2, R 0.05, `TornAt` below 0.05),
set by reading threads; assumes `Lives`, Company, Invitations, authored reflection, errands [#90], her evening place
corrected (A), the sim's bedtime (O1(a)) and the present readings at the start (O2). Chance terms follow rules 5 and 11
(whereabouts in the window × answers × follow-through). Ordinary encounters are the other trigger (below).

| Stage | Penny | Pam | Gus |
|---|---|---|---|
| **What happened before** | p1-p9; this week's run (Pam fetched her flour, say [#90]); scenes of Pam's she was told of [base] | m1-m7; the Saloon every evening [base]; a beer need noticed at a keyed minute (`NeedsArise`, `Simulation.Errands.cs:72`) [#90] | g1-g3; serves 11-24 daily [base] |
| **What they remember and associate** | Pam: warm (p6) and cold (p5, plus any told scene, source Told), kept apart. The Saloon: wary (p4). Teaching, reading: warm (p3, p9). Expects Pam away in the evening (p4, one entry) | The Saloon: warm (m3) and wary (m4). Penny: warm (m1). Gus: warm (m7). Expects Penny's housework (m2) | Pam: wary (g3). Expects her every evening band (g2) |
| **Why it comes to mind now** | 09:10, both awake at home (both are, before noon, on 110 of 111 days, a median 30 minutes: Pam wakes about 09:05, Penny leaves about 09:35; measured, to repeat over seeds 1-3). Pam asks for beer; the `Commitment` answers no while the `Ask`'s margin is about +0.6 (`Simulation.Ask.cs:89-99`). A commitment refusal with a positive margin opens her reflection (rule 10). Cued: p5, p4, p6; p2 in autumn by her own stated cue (strong, not exclusive) | Penny's offer, a minute later. Being asked with pre-run records about the asker opens hers (rule 8a). Cued: m1, m4, m3, and the morning's refusal | 18:00-21:00: Pam never seen in bands where he expects her (rule 14). That contradicts g2 (rule 8b): one reflection |
| **What they imagine** | a private thought, authored or generated above a significance threshold, any interpretation marked imagined. Menu: fetch the beer, shown excluded (origin A); ask Pam to spend the evening at home, a game, 18:00 to her usual bedtime; no offer, keeping her evening (reading at home). Teaching sets her availability (she leaves at 09:35), not a rival for the evening. No other good: Pam's only use is beer (`DefaultTown.cs:358`) [#90] | a thought; menu: yes, not now, no | a thought ("maybe she's cutting back", imagined, read by no rule); menu: a kindness toward Pam when next in reach, or nothing |
| **Which alternatives compete** | offer: refused margin 0.6 × chance 0.5 (whereabouts: p4's one "away"; no answers or follow-through yet, 1 each) × e^(5 × (0.05 − 0.8 × 0.05)) = **0.32**; its reasons: obliged and refused by commitment, warm worry, cold burden, the chance. Her evening: 1 × e^(5 × 0.05) = **1.28**; reason: reading at home (p9). Warm and cold toward Pam both above `TornAt`: torn, one Laya `choice` naming both. Fallback keyed draw: offer about 0.2 | the `Ask(Invitation)` [base]: obliged 0.90 (0.5 × regard 0.6 + 0.3 × familiarity 0.8 + 0.2 × chattiness 0.8 + kin bond 0.2, `Simulation.Ask.cs:81-85`) against cost 0.15 + state 0.02 + displaced habit 0.4 × her share of the last 14 evenings at the Saloon (1.0): margin **+0.33**, beyond `ClearBand` 0.15 even when low: the fallback's draw over yes, not now and no gives yes about 0.80, not now 0.17 (her state cost, 0.02, is a reason while it holds) and no 0.03 (rule 7). Torn (the Saloon's two sides): Laya weighs the same three | nothing now: he is at work |
| **What they attempt** | asks, through a pending ask (rule 7) | yes: a `Plan` for each (rule 12): home, a game, 18:00 to 21:20 | |
| **Follow-through** | home by 18:00 (her haunts close then). Uncorrected, keeping the plan is 0.64 of her pick against the Evening hub | about 17:28 (18:00 less a 32-minute walk of about 63 tiles at 2 a minute, `Simulation.cs:68`), at the Saloon since about 16:35: one follow-through pick (rule 12). Keep 1 × e^(5 × (0.05 + 0.05 + 0.2)) = **4.48** (Penny warm; the hobby wish as warm only if O2 says so, else 3.49; intended); stay: Saloon 4 × e^(5 × (0.05 − 0.3 × 0.05)) = **4.76**; the Evening hub is not on before 18:00 (`Simulation.cs:760`). Torn, within 10%: one Laya question. Fallback: keep about **0.48** (0.42 without the wish) | |
| **What actually happens** | 18:05 both home and free: she begins the game, no second ask (rule 13). Her share 0.08 × (0.5 + 0.67) = **0.094** | her share 0.08 × 1.06 = **0.085**. Later picks in the window: keep about 0.39 against the Saloon and the hub; branch: home until Penny sleeps, then home 1.73 against the Saloon's 7.1: at the Saloon by 21:40 | 21:40: Pam arrives, found in the 21-24 band |
| **Practical outcome** | an evening with Pam instead of reading alone; after 21:20 is beyond her | away from about 17:28 to 21:40: about 180 fewer Saloon minutes after 18:00 than her measured median of 320; a scene about 0.02 less likely that evening (`DrunkScene`, 0.3 a day at the Saloon, shared with Shane, `DefaultTown.cs:123`). `Drinks` still charges the Trailer 12 when she returns (`Simulation.Money.cs:214-230`); with her evening corrected the purse stays above `WeekCost` 180 all month (estimated), so `NeedPressure` (`:272`) is 0 either way [base] | takings unchanged in this branch (observer-visible only); what he notices is her absence (rule 14) |
| **What changes next time** | from this first outcome (rule 9): answers (Pam, evening plan): yes; follow-through: together, one entry (a planned evening writes no whereabouts, 9.5); warm entries for Pam, home and the game, one cause. Chance 0.5 × 1 × 1; next offer 0.6 × 0.5 × e^(5 × (0.025 + 0.094 − 0.8 × 0.025)) = 0.49, about **0.28** of the draw. Had Pam stayed out: chance 0.5 × 1 × 1/2 = 0.25, about **0.11** of the draw (0.08 after two); her disappointment is aimed at the evening, which the offer doesn't read (9.4, 9.5; a person target, O3, would lower it further). Mixed, one together and one absent in either order: 0.5 × 1 × 0.67 = **0.33**, between. A `Derived` summary at the heuristic's count (`DeriveAfter`, three agreeing, tunable); p4 and each revision's evidence stay on record | warm entries for Penny, home and the game beside the Saloon's two sides; keep at the next plan about 0.5. Answers (Penny, errand: beer): no, one entry a refusal; from that first refusal her chance of a yes from Penny is 0.5, then 0.33, 0.25, so her beer asks turn elsewhere step by step (rule 15); a `Derived` "Penny won't fetch beer" summarises at the heuristic's count, from her own experience, never from Penny's commitment record | whereabouts (Pam, Saloon, 18-21): missed, source seen-absent: his chance there falls from 1 (g2's entry) to 0.67, then 0.5, then 0.4, where rule 14 stops counting her absence; 21-24: found. A `Derived` summary at the heuristic's count |

**Other branches are complete endings.** No offer: her evening as routine. Not now (the fallback's 0.17 while her state cost
holds) or a weighed no (0.03): either method (rule 7). No: `Refused`, her small hurt [base], one "no" entry. Absent: she waits 60 minutes and writes one
follow-through entry, absent; where Pam went stays a guess in her thought; she is disappointed as far as she expected
and invested (9.4); Pam learns of the wait only if she sees or is told. Busy is rare: asleep is never asked (`InReach`
needs `Free`, `Simulation.Desire.cs:123-125`); Penny at lessons when Gus would ask (about 0.07 a seed-month) needs a
fixture. After two absents her offer is about 0.08 of the draw; habit may win twice; someone else may buy Pam's beer at
Gus's counter (`BuyAtCounters`, `Simulation.Errands.cs:218`). **Encounters** (rule 11, no beer ask): at their morning
overlap Penny's recalled warmth (p6) can open the same offer (0.1 × 0.5 × e^0.05, about 0.05 of that pick) and Pam's
warmth (m1) or wish (m4) one to Penny (0.1 × e^0.25, about 0.1; estimates), each through the same answer, follow-through
and learning: care and a shared evening without a crisis, free to be declined, broken or dull.

**How often, honestly.** Pam's need arises at 0.1 a day (`Acts.cs:142`); D52 measured 8 beer refusals in 6 seeds of 28
days: about 0.1-0.2 natural threads a seed-month, fewer as Pam asks Penny less, so the review needs a morning fixture.
Encounters may add a few offers a seed-month (estimated; one a pair a day at most); natural runs say whether that is
everyday. A 28-day review is spring (`Calendar.cs:4`): p2's autumn cue (days 56-83) needs 112 days or a fixture.

## 9. The smallest complete slice (all proposed)

### 9.1 Switches and data
`Lives`: new, off, byte-identical when off. A Lives run also turns on Company, Invitations, errands [#90] and reflection
(authored mind by default); the set-point (9.5) has its own switch, off in the first review. Data: `sim/histories.json`,
reviewed like the anchors, under section 2's rules; without it a town runs on lived records alone. `LookFor` is not
used: a housemate is at the shared home, a fact.

### 9.2 Records

| Record | Holds | Written by | Read by |
|---|---|---|---|
| `HistoryFact` | its own ids (`h:`); kind; who, with whom; place if known; time and recurrence known or not; still true at the start; sources with branch; admission rule; cue keys (people, places, activities, kinds, relationships held as facts, day type, season, a few situations), each sourced (its key) or proposed (E) | authored from canon | recall; `Thread` |
| `HistoryAccount` | holder, fact, how known (Lived, Witnessed, Told by, Inferred), sources | authored with the fact | seeding; recall |
| `Reading` | holder, object (a fact or cue keys), stance (warm, wary, expects, none), certainty, origin (`CanonStated` key; `Authored`, E, for any link its holder doesn't state; `Derived` record ids); one per holder, object and stance, citing every source | authored; rule 9; never a model | seeding; recall; a key's prior |
| `Association` (sentiments, extended) | holder, object (person, kind, place, activity, or a situation: cue keys held together, such as creaking wood, or the game at home with Pam), sign, name, strength, causes (act or history ids), origin, how known (Witnessed, Told with teller and credence, Lived, Reading) | felt amounts (rule 2); felt outcomes (9.4); seeding | recall; warm and cold sides, kept apart; the torn test |
| `Expectation` | holder, subject, key: whereabouts (place, band, day type), availability (working, asleep, not now), answers (ask kind and good), follow-through, how it goes (an activity or situation, for the holder: possible, as felt; written by its own course, never by a plan's outcome); its seeded entry and the last 6 lived ones a key: day, hour, outcome, source (Seen, seen-absent, Sought, Told, Public, Reading; never an inference), act or outcome id | classified outcomes (9.4); a throttled Seen writer, outside a plan's window with that person; seeding | the chance term; where to look; whom to ask |
| `Commitment` | holder, scope (provide: a goods category, alcohol), to whom, force (Absolute or a cost), origin, since, revisable or not | data (9.7) | `Holds` at the `Ask` and wherever a treat or gift would provide |
| `Plan` | with whom, place, activity, from, until, origin (the ask id), outcome | a yes (rule 12) | the follow-through pick; the planned act |
| `PendingAsk` | opportunity id (seed, minute, asker, asked, kind) | rule 7 | the answer; the act begun on it |
| `ChoiceQuestion` | id (seed, minute, holder), options with typed reasons, answer, backend | a torn comparison | answering; the replay tape |
| `Thread` (observer only) | what was recalled and why, options and reasons, torn or not, who answered, the attempt, the outcome kind, the records written | the rules | log, replay, viewer, under the observer contract (13); never a rule's or model's input |

### 9.3 Rules

| Rule (1-9 the third revision's, revised; 10-16 new) | What it does | Bounds |
|---|---|---|
| **1. Seeding** | A reading with a stance seeds what experience writes: *expects*, one `Expectation` entry (source Reading) per band of its key; *warm* or *wary*, one `Association` entry at R ÷ (1 + lived entries). A completed past seeds only through its holder's own stated tie to a present attitude (George's wish and "never", Marnie's trembling, Pam's "I miss"), citing the past, never a trait, mood or disposition from the event. Facts, stance-less, habit and authored-link (E) readings are only recalled | writes no mood, regard, motive or fear |
| **2. Associations from what was felt** | A perceived act's felt mood writes a signed entry to its person, place and activity, citing the act and how it was known (witnessed; or told, at `HearsayMood` 0.5 × credence, `Simulation.Feelings.cs:309` [base], teller kept), once per holder. A completed shared act gives each participant the row's joy × (0.5 + sensitivity) (today the asker's 0.005, `Simulation.Acts.cs:262`); none for a refused asker or chat company | at most 3 an object, one of each sign kept; lived entries fade 0.975 a night; `Revert` takes them back |
| **3. Recall** | At an opportunity (a pick, being asked, a refusal, an outcome), from a per-holder index by cue keys, never the run-long stores: by overlap (a sourced cue counts `SourcedCue` 2, a proposed one 1: strong, never exclusive), then strength × (1 + personal significance); when mood is low, one more by warmth alone (our adaptation of III P13) | the top 3, one slot kept for a pre-run record |
| **4. Options** | Nothing recalled: today's draw. Otherwise: carry on; stay home; a recalled place or person; a plan offer (rule 11); an answer when asked (yes, not now, no); an accepted intention. Physical eligibility and commitments filter first; Laya and the fallback get the same set and reasons (revision 4) | up to 5 |
| **5. One scale** | Weight = base × chance × exp(k × (warm − cold × (1 − boldness) + intended − going cost)), k 5, intended 0.2; warm and cold the object's sides, kept apart (fear hits join cold); chance the product of the relevant keys, each (successes + 1)/(entries + 1) (a plan's: whereabouts in the window, answers, follow-through, and how its activity goes where held). Typed reasons cite records; a keyed draw, not an argmax | regard never enters it, nor associations the gate |
| **6. Torn choices go to Laya** | Warm and cold both above `TornAt`; or warm above it with chance below 0.5; or the top two within 10%. One typed question naming both sides, over the fallback's own options, reasons and constraints; without Laya the keyed draw answers, with it the fallback's answer is taped beside Laya's (9.8) | at most 2 a person-day; an answer stands until a side moves by `AskAgainStep`; never dependent on who is watching |
| **7. Acting** | Choices become a `Decide` goal at the errand slot (`ErrandGoal`, `Simulation.cs:754`) [#90]. Asking becomes a **pending ask** (today `Asked` answers and begins in one minute, `Simulation.Invitations.cs:33-50`), answered next minute by the asked person's reflection if one opened, else by the fallback's keyed draw: yes e^(k×margin), no e^(−k×margin), not now e^(k×reason) while one holds (low mood, tiredness, a duty due) (first guesses; `Ask` unchanged with `Lives` off). A Lives "no" replaces the 7-day wait. An act toward a person is a stored motive the gate judges, not `ReflectAct`'s bypass | |
| **8. Reflection at meaningful opportunities**, not the quiet-minute poll | (a) Being asked while a recalled record about the asker or activity is pre-run or two-sided; (b) an outcome contradicting a recalled reading and (c) one that matters (refused, a third miss, a plan broken). The request gains `Recalled` (ids, origin) and the options with reasons; its id is the opportunity's | (a) one a person-day; (b) and (c) share one |
| **9. Learning from the first outcome** (revision 2) | Each classified outcome is one entry and moves its key's chance (rule 5) at once, agreeing, mixed or contradictory, so a yes after a no lands between. A `Derived` reading summarises a key at a tunable heuristic (`DeriveAfter`: three agreeing, experimental), citing its entries; it changes what is recalled, not the chance, the canon reading recalled beside it. Each revision's old and new value and evidence ids go to the observer record (13) | the seeded entry stays beside the six latest lived ones; the reading is never deleted |
| **10. Other care** | An `Ask` refused by a commitment while its margin was positive opens the refuser's reflection; the commitment and the margin are typed reasons. Menu: plan offers toward the asker (rule 11); an errand for another good the asker uses (the book's `Uses`); no offer. A negative margin opens nothing: the no is also a can't | one a person-day, its own budget |
| **11. Plan offer** | "Ask X to <activity> at <place> from <start> until <end>": X in reach; both free in the window by the asker's own knowledge; the activity eligible there; no open plan between them. Weight: base (rule 10: the refused margin; at an encounter `PlanBase` 0.1) × chance (a plan's keys, rule 5) × e^(k × (warm − cold × (1 − boldness))), the sides the person's. The answer is `Ask(Invitation)` plus a displaced-habit cost: 0.4 × the asked person's share of the last 14 days at the displaced place in that window (#91's `StayInHabitCost`, without bars). From the first slice (revision 5), at an ordinary encounter (no ask, no crisis) when recall brings warmth toward X (a reading or association), an interest (a warm reading toward an activity eligible with X there) or a wish naming an activity X could share; "both signs" only makes it torn (rule 6). Nothing favours yes or together | one open plan a pair; one offer a pair a day |
| **12. `Plan`, follow-through** | A yes writes a plan for each; "this evening" ends at the asker's usual bedtime as self-known. At start minus travel, a holder whose goal lies elsewhere gets one follow-through pick at the errand slot, before the stay check: keep (rule 5, intended 0.2; the sides those of the planned activity and its company, a disappointment toward that evening among them), stay, or another open option. Later picks in the window carry intended 0.2 | one question a plan |
| **13. Planned act** | At the first minute both are at the place, free and in reach, the asker begins the activity: no second `Asked`, no rate card. Physical rules hold | |
| **14. Passive absence** | Present at a place through a band, expecting someone there (chance at least 0.5), no span of them seen: one "missed" entry, source seen-absent | keys at 0.5 or above; never in a plan's window with them (9.5) |
| **15. Asks aimed by expectation** | `AskFavours`' target weight (`Simulation.Errands.cs:146`) [#90] × the asker's chance of a yes from that person, for that kind and good; 1 with no entries | |
| **16. Busy at work** | An asker who draws someone at their job place in working hours writes availability: busy for that person. Whether being at work also stops or weighs other asks stays open: D52 left one's own hours open; #91's answer 7 (`ErrandHoursBusy`, errands only) took a narrower rule, which the slice follows; a plan offer already needs both free by the asker's knowledge (rule 11). Today `Free` ignores work (`Simulation.cs:908`) and `AskStateOf`'s busy means "due within 60 minutes" (`Simulation.Invitations.cs:60-61`) [base] | the person drawn only |

### 9.4 Outcomes, told apart before anything is learned

| Outcome | Where it arises here | Writes | Must not write |
|---|---|---|---|
| Seen (passive) | a span in `_sightings` (`Simulation.Suspicion.cs:12-36`) [base], outside a plan's window with that person | whereabouts: found, source Seen; one a holder, subject, place, band and day | anything else |
| Not found (no agreement) | Pam not at the Saloon in a band Gus expects her (rule 14); a search that fails | whereabouts missed, nothing else | an answer, follow-through, regard, a hurt, dread, an eyewitness belief, where she went |
| Busy | rare: Penny at her lessons when Gus would ask her to fetch flour (a fixture); "asleep" only as a plan outcome, since `InReach` needs `Free` | availability: busy or asleep | refusal, regard, dread |
| Not now (new answer) | Pam tired or low when asked; in both methods, the fallback drawing it while a reason holds (rule 7) | availability: not now | the `Refused` act |
| Refused by commitment | Penny to Pam, beer, whatever the margin (A) | Pam: answers (Penny, errand: beer) no. Penny: her reflection opens (rule 10) | a probabilistic yes; any coldness toward Pam in Penny's records |
| Refused by weighing | Pam's no to the evening; in both methods, rare at her clear margin (the fallback about 0.03) | Penny: answers no, citing the `Refused` act; the hurt is the act's own [base] | an association for the asker; the 7-day wait |
| Agreed, absent (revision 3) | Pam said yes and stayed out | **Observable**, for each: Penny one follow-through entry, absent, beside her answers yes (no whereabouts entry, 9.5); Pam her own plan outcome. **Interpretation**, each by what they know, expected and invested: Penny's disappointment, D × expected (the plan's chance as she held it at the yes: 0.5 at the first plan, from p4's "away") × invested (her intended 0.2 plus her warmth toward Pam, over 0.25), 0.025 here (first guesses, the observer fixture's form, 13), as mood and one cold entry toward the hoped-for evening (the game at home with Pam, a situation; O3), cause the plan; Pam's only from what she knows (her yes; the wait if seen or told) | a refusal, rejection or betrayal; `LetDown` [#90]; `StoodUp` (planned, `acts-batch2-spec.md:383`; [base] has only a `Variety.cs:66` label); a `Promise(Date)` (a [base] type, `Promises.cs:4`, that no rule makes yet); where Pam went (a guess stays in the thought); regard (no act perceived); a feeling for one who expected nothing |
| Together | the game at home completes | answers yes; follow-through together (no whereabouts entry, 9.5); each participant's felt share to person, place and activity; the set-point share | company-per-chat warmth |

**Observation, testimony, inference.** Penny observes Pam at home or not; "late nights" (p5) is an account she holds as
Inferred (E), and "she's at the Saloon" on an absent evening a guess in her thought. Inferred is how an account is known,
never an `Expectation` source: no inference enters a chance or becomes a witnessed `DrunkScene` (`Source.Found`,
`Model.cs:253`, is an unnamed trace belief). Gus observes Pam and any scene, never a drink (`Drinks` is a purse
transfer) [base]. **Testimony of acts is built** [base]: `TryTell` (`Simulation.cs:1211-1256`) stops only a teller who
is the culprit's kin. Measured: of Pam's 7 scenes Penny witnessed 1 and was told of 3 (plain) or 4 (authored), from
16-19 tellings (2-3 Gus's); a told entry keeps source, teller and credence. No whereabouts are told.

### 9.5 What each person learns, and counting once

Each person's learning is section 8's last row (lessons and errands [#90] write the same way); Gus never reads his till.

| Record | Answers | Fed by | Read by | Never read by |
|---|---|---|---|---|
| Regard [base] | how I feel about this person | `Feel` | gate covers, `Weigh`, the `Ask`, target weights, `E` | where to go (rule 5) |
| Person association | which experiences with them come to mind, and as what | felt amounts; readings | recall; warm and cold sides; the torn test | the gate |
| Place, activity or situation association | how being there, doing it or meeting it has felt | felt amounts; readings | option sides; recall | the gate |
| Expectation | where they are, whether free, how they answer, whether they follow through, how a thing goes | classified outcomes; readings | the chance term; whom to ask | any valence |
| Set-point (`_setPoint`, new) | where regard drifts back to | `_baseline` at the start; a share (0.25) of each applied regard, under its own switch | the nightly drift | any decision |

- **Durable revision, from the first outcome** (rule 9). Each outcome moves its key; lived entries soon outweigh a
  reading, mixed ones land between, and the reading and each revision's evidence stay on record. Regard barely moves
  here (`WarmBudget`, `Acts.cs:94`: about 0.0025 of set-point), so the durable change is in expectations and
  associations. A set-point share would let a run tie outlast the card but changes nightly drift for every ordered pair
  (`CloseFeelings`, `Simulation.Feelings.cs:639-656`), seeded feuds included: hence its own switch, with feuds,
  friendships and withdrawal counted off and on (11).
- **Counting once.** Each decision reads one magnitude per object: where to go reads associations and expectations, the
  gate regard. A refusal is one hurt and one "no", no association, no fixed wait. A habit reading adds nothing to its
  haunt weight; chance never multiplies in a second negative; an act cited by a place and a person counts once; m1's
  four sources are one entry; the Trailer's 0.6 is a structural seed (`SeedRegard`, `Simulation.Feelings.cs:109`).
  Sentiments become the person and kind associations (per-sign floor; causes appended, `:472`). **An agreed evening**
  is one follow-through entry, never also a "no" or a whereabouts entry (the plan, not habit, set where each was to
  be; Seen and rule 14 skip its window), plus one disappointment toward the evening (9.4). An offer reads the person's
  sides and the chance, so the absence lowers it once, through follow-through; the evening's entry is read only by the
  holder's own keep pick at a later plan for that evening (rule 12), and recalled as a reason.

### 9.6 Household rules this thread needs, and only those
- **A plan with a housemate.** The pending ask and the `Plan` don't pass the gate, so `Close`'s exclusions
  (`FondMotives`, `Simulation.Desire.cs:458`) don't block them; Fond's own exclusion stays; the `Ask` already adds the
  kin bond for anyone `Close` (`Simulation.Ask.cs:82`) [base].
- **A game at home.** One household's members share `PlayedGame` at any age, at home, until the earlier bedtime (today:
  ages within 6 or both under 13, the Saloon after 17:00 or outdoors before 19:00, `Simulation.Acts.cs:44-46`); a
  planned act needs no rate card (Pam and Penny have none, `ActCatalog.cs:290-291`).
- **Busy at work** (rule 16). Not needed: repair inside a household (`Simulation.Acts.cs:336`); Pity, Sides, X2; kin
  shame; `Kin` values. Families' covering stays [base]. Penny's routine is data (A), not a household rule.

### 9.7 Penny's commitment, and other care
- **As built** [#90]: `ErrandBook.WontFetch` holds `["Penny"] = ["beer"]` (`DefaultTown.cs:361`); the `Ask` says no
  whatever the margin, still recording it (`Simulation.Ask.cs:99`); `ErrandTests.cs:286, 464`. Origin D52.
- **Its evidence, kept distinct.** D52 is Sid's product commitment (A). Canon supports it: her gift hates (p8; ids in
  game `SaveMigrator_1_6.cs:1897-1903`, 346, 303, 304; `:2171-2181`, 348, 459) and the game's exception keeping her from
  naming drinks as Pam's loved gifts (`NPC.cs:3667`). No Penny line addresses fetching alcohol: canon proves no absolute
  refusal (reading p8 as one is E). Records use canon's words, not "alcoholism".
- **Proposed `Commitment`**: provide alcohol (D52's word; beer the only one today), to anyone (#90's behaviour; our
  broadening of "her mom"), Absolute, origin Authored (D52, p8 as support), pre-run, not revisable. One reader,
  `Holds(holder, scope, to)`, replaces `WontFetchAsked` (line 99) and guards treats and gifts. **No weaker**: it
  short-circuits as line 99 does; `WontFetch` stays its data (tests 286 and 464 unchanged); a name swap refuses alike.
- **Other care** (rule 10): the refusal opens her reflection, the margin a reason; a no to alcohol is not a no to care.
  Pam learns only from Penny's answers (rule 15). **Not covered: drinking herself.** `Drinks` charges any adult at the
  Saloon after 18:00 who doesn't work there, and Pam's and Gus's `TreatedToDrink` cards [base, Company] can treat her;
  Sid kept her own drink as "the body's rule" (#91, answer 5); the commitment does not extend to accepting or drinking
  alcohol without a separate decision (A).

### 9.8 Reflection, Laya and the model rules
- **The link.** The refusal opens Penny's reflection; being asked, Pam's; a broken expectation, Gus's. Each request
  carries the recalled records (ids, origin) and the options with their attractions and reservations; a torn choice gets
  Laya's typed answer in the same request (one call, one tape entry), else the keyed draw. An accepted intention for
  later (Gus's kindness) becomes a stored motive the gate judges, after physical eligibility.
- **Comparing methods** (revision 4). Both weigh one option set, its reasons and constraints, and the tape keeps the
  fallback's answer beside Laya's (rules 4, 6). `authored` against `laya` isolates the weigher (the same authored
  thought, only the evaluation differs: `reflection-spec.md:58-63`), matched choice by choice; whole runs that diverge
  afterwards are reported apart. In `hybrid` a generated option, once eligible, joins the shared set before either
  weigher, so hybrid against authored compares whole minds (thought and weigher), never a method alone.
- **The engine is the only writer** of facts, accounts, readings, associations and expectations. The generator may write
  the thought, propose one possibility (an option only if eligible) and an interpretation that stays in the thought,
  marked imagined. Routine speech stays authored (`Simulation.Speech.cs:33-39`).
- **Replay.** A recalled history is only its records' fields (the prior thought's excerpt is left out); new fields join
  replay's check (`Reflection.cs:160-206`). Which records move numbers under the fallback is section 0's limit.

### 9.9 Present actions: used, changed, new; left out
**Used as built**: [#90] `NeedsArise`, `Ask(Errand)` and its margin, `Refused`, `BuyAtCounters`; [base]
`Ask(Invitation)`, `PlayedGame`, `AfterAct`, `Feel`, `AddSentiment`, `Revert`, `Decide`, `Drinks`, `NeedPressure`,
`DrunkScene`, `TryTell`, `_sightings`, the reflection minds and tape. **Changed**: line 99, `AskFavours` [#90]; `Asked`
and its answer, `KindFits`, busy at work, `CloseFeelings`, the sentiment trim, the poll [base]. **New**: 9.2's records,
rules 1-16. **Left out**: bars, `P`, `Conditions`, law 1's text, `Concerns`, `CoreMemory`, `StayInPull` [#91]; chores
and the helping act (next), then making (Penny's cooking); teaching's content; telling whereabouts; hangover traces.

## 10. The second household: the transfer test

Evelyn, George and Alex, after the Trailer's review, on the same code (rules 11-14 and 16, the game at home): no
commitment, rule 10 or 15, or alcohol in the thread; money and the Saloon only through Alex's own evenings (his haunt
19-24, `DefaultTown.cs:412`; `Drinks`). Plans arise at ordinary encounters, with no crisis, by rule 11's warmth,
interest or wish trigger, as each starting reading toward a housemate has one sign (George's sad wish; Alex's love for
his grandmother, the Grandpa line a joke; Evelyn's annoyance at George's television, wary toward his habit). **George**:
his mine accident and its cost to his legs (`Data/Events/JoshHouse:18/f George 1500/p George`; Past, R1; "30 years ago"
stays relative); his wish that his legs let him play catch with Alex, "that'll never happen" (`George:Wed8`; R2). His
own links seed a warm wish for catch with Alex and an expectation it won't happen (a "how it goes" key; revision 1),
never bitterness, frailty or dislike of help; `Town:34` (NA) shows him bristling at help, then thanking. **Evelyn**:
wishes he would go out (`Evelyn:Fri`). **Alex**: section 3's case; loves his grandmother (`Alex:Fri4`); his father's
drinking is history.

**What it must show.** (1) The same rules give an intelligible thread without the Saloon's economy, a commitment or a
crisis. (2) A past its holder ties to the present comes to mind when Alex is in reach and shapes what George considers
(audit case 3). `PlayedGame` has no content (`ActCatalog.cs:82-86`), so nothing yet excludes catch: that needs `Plan`
activities with a physical requirement and George's mobility as data (proposed; VERIFY), leaving room for another
activity; without them the wish still opens an offer, his expectation among its reasons (with them, a chance term).
(3) No private reading crosses between participants. (4) Outcomes differ across seeds. (5) The Trailer's records given
to this household move its picks alike. The town contradicts Evelyn: George is out on 102 of 111 days [measured], where
canon keeps him home (`fixtures/game/George.json`), an unsupported routine corrected as his data before the test (A).

## 11. Population scaling

**The same mechanism everywhere.** Every rule runs for everyone every day, watched or not. No decision branches on a
name: rules read associations, expectations, plans and commitments, data any holder can carry; without history a
character runs on lived records. Model calls come only at torn choices and meaningful opportunities, capped a
person-day, reading three or four recalled records, never a full history. The fallback needs no model.

**Reusable experience forms** (the audit's): typed `HistoryFact` slots (who, with whom, place, time and recurrence,
accounts, readings, default cues); recall reads forms, decisions never do. Classifiers file run experiences into them
(the Trailer's examples in brackets): *learning from someone*, practice with a teacher, later (p3); *losing someone*, a
household member leaving or dying (p2); *receiving care*, a kindness received (m2, m7); *experiencing pressure*, a purse
short past a threshold (p3, m6, g2); *sharing an activity*, a completed shared act (m3, m6, the game); *moving
somewhere*, a change of household (Alex); *recurring household burden*, once chores exist (p5).

**Biographies apart from archetypes** (Appendix D). Archetypes carry dispositions, coping, voice, source evidence,
character assignments, links to authored speech and empty slots; biographies (examples, source receipts), commitments
and lived histories stay per character, so duplicating an archetype never copies a life. A generated character's forms
start empty but for its own generated past; stories passed down (0e) arrive as `Told` accounts. **Bounded recall**:
three records and a pre-run slot from a per-holder index; at most 3 entries an object, a per-sign floor, 96 a person.

**Measured today** (seed 1, base paths, errands and bars off), 26 people against 60: 28 days take 2.0 s and 114 MB
against 4.0 s and 137 MB (runner), 3.1 s and 143 MB against 7.3 s and 251 MB (replay, authored reflection); 112 days
replayed, 8.5 s and 327 MB against 30.3 s and 993 MB; 44 against 91 hybrid model calls a day. Time is near linear
(log-log slope 0.84 runner, 1.04 authored); beliefs grow as n^1.6-1.8. Reflection's check rescans the run's acts each
quiet minute (`Simulation.Reflection.cs:86, 153`), +57% on a 60-person year; event triggers remove it. Model time
dominates (about 1.2 s a request, VERIFY: a 60-person hybrid month about 9 minutes, 7 s authored); the generator stops
at 60 (`Generation/TownGen.cs:46-73`).

**What the slice adds** (per person-day, estimates): association writes 1-3 (under 20 KB a person); 7.4 picks × up to 5
options; Seen entries 5-20; plans under 0.2; trigger 10 under 0.05; torn questions 0.15-0.75, at most 2; reflections
0.1-0.4. **Likely costs**, largest first: model time (generate only above a threshold; answer a minute's requests
concurrently, in request order); belief volume; the O(n²) pair loops, likely dominant from 120-240; whole runs held.

**A modest larger-population check**, beside the small-cast review, against today's baseline (A): off is off (pinned
hashes); authored reflection, seeds 1-3, 28 days, at 26, 31 and 60. **First** the switches a Lives run turns on
(Company, Invitations, errands [#90], reflection) with `Lives` off, reported apart, so their cost never lands on the
slice's; **then** history, recall, learning and model calls added singly and together, each cost reported apart; a
**load variant** giving every household the Trailer's record shapes and a commitment by the name-swap mapping (a
fixture, not canon); one 112-day replay at 60; the runner at 60, 16 seeds; with Laya, 7 days at 60 replayed from its
tape. Measure time, memory, JSON size; picks, recalls, plans, torn questions and reflections by trigger; two-sided
objects at days 28 and 112; learning outside the cast; feuds, friendships and withdrawal with the set-point off and
on. **Warnings, not targets**: any hash change off; at 60, on above 1.25x the switches-on step in time or 1.2x in
memory; torn questions above 1 or reflections above 0.4 a person-day, or rising with n; a per-pick path reading
run-long stores; two-sided objects collapsing; any replay that differs.

## 12. Evaluation and tests

**Threads first.** Six seeds of 28 days (spring: practices, readings, the commitment) and of 112 days (the autumn cue,
the purse); fixtures (the morning ask, an encounter, busy, not now, autumn) verify each mechanism, natural runs say how
often threads arise unforced and how they end. Every `Thread` of the three is read as their days: does each step cite a
record, a history come to mind because the situation cues it, outcomes stay distinct and differ across seeds, poor ones
included, anything trace to a name or model text? Rates are diagnostics only; nothing is tuned toward yes or harmony.

**Tests** (◆ marks the audit's six acceptance cases):
- **Off**: byte-identical, pinned hashes; Company, Invitations and reflection on with `Lives` off equal today's runs;
  with nothing recalled, a pick equals today's.
- **Evidence** ◆: a past recalls differently for different participants, no private reading crossing; Pam's Saloon
  nights add one entry to Penny's records, not three; Evelyn's account gives George no feelings; no record holds a date,
  count or diagnosis its source lacks; a guess stays a guess.
- **History** ◆: a completed past its holder ties to the present moves a choice without being replayed (audit case 3:
  George, with Alex in reach; Marnie, a format test); no disposition comes from an event; an authored link (E) adds cues
  only; p2 leads recall in autumn by its sourced cue yet can come in spring by a proposed one; a stance-less reading
  seeds no number; after six lived entries a reading is one in seven; seeding writes no mood, regard, motive or fear.
- **Conflict** ◆: section 3's four cases keep experience apart from reading, and Leah's, Clint's and Alex's both sides;
  three warm entries never evict the last cold one; Penny can be torn toward Pam while net regard is warm; no decision
  reads both regard and a person association.
- **Commitment**: alcohol (beer today) is a no at any margin; tests 286 and 464 pass unchanged; a name swap refuses
  alike; only a positive margin opens trigger 10; Pam's `Derived` reading comes from her own entries only.
- **Outcomes** ◆: not found writes only whereabouts; busy, no answer; a refusal, one count and the act's hurt, no
  association, no wait; "agreed, absent", one follow-through entry and no whereabouts, never a refusal, rejection or
  where Pam went; Penny's disappointment scales with what she expected and invested (none if nothing), on the evening
  (O3), Pam's with what she knows; "Pam at the Saloon" stays Penny's guess; a told scene reads Told, counted once.
- **Revision** ◆: the first relevant outcome moves its key and the start can't erase it; a together and an absent leave
  follow-through at 0.67; `DeriveAfter` 1, 3 or 5 moves only the summary; the reading and each revision's evidence stay
  inspectable; set-point on, regard stays above the seed 60 days after a lived kindness (the seed if reattributed).
- **Method and encounters**: Laya and the fallback get identical options, reasons and constraints (one option hash, a
  hybrid run's generated option included), the fallback's answer taped at torn questions; method comparisons pair
  `authored` with `laya`; with Laya off, not now and a weighed no occur; without a beer ask, Penny's warmth and Pam's
  wish open offers and the JoshHouse starts a thread; nothing raises yes or together.
- **Reflection and replay**: a recalled record reaches `Recalled` with id and origin; a matched choice with and without
  p4 (one comparison id; world, options, other records and the draw's key held) changes the offer's reasons and chance
  even where the sampled action stays; a stub's text changes no record and reaches no later request; a mismatched tape
  fails; nothing begins before a pending ask's answer; an intention becomes a goal only after bed, work and patrol.
- **Generality** ◆: a new character uses the same forms, inherits no donor biography and keeps living unobserved; the
  Trailer's history given to the JoshHouse moves their picks alike; watched and unwatched runs are equal.

## 13. Docs and coordination

Earlier decisions stay dated history: a D-entry is never rewritten, a later one cites what it supersedes; a new entry
records the 2026-10-14 decisions (Sid via Astra). Rows marked A are decided; the rest wait for the slice's agreement.

| Doc and place | Says | Change |
|---|---|---|
| `design.md:54`, law 1 [#91] | "Amended 2026-10-13", no D-entry, replacing "There is no separate list of needs for social life" | A: law 1 unchanged, that original sentence governing; the amendment kept beside it, marked "asked for 2026-10-13, on hold since 2026-10-14" |
| `needs-bars-spec.md` Status, Steps, `:190-250`; `generations-research.md` Part 1 [#91] | approved in shape; step 1 is `Bars` with `CoreMemories`, `PlaceFeelings`; addiction as a fun-bar drain | A: amendments conflicting with law 1 on hold; the spec "unresolved since 2026-10-14: not scheduled, not rejected"; history and association get their own spec; Sid's 10-13 answers stay on record |
| `experiments/law1-bars-2026-10-14.md:11, 70-78`; `Simulation.Bars.cs:23, 26, 273` [#92] | mode 2 "(the recommendation)"; "Spinoza done in full" | A: drop the recommendation; mode 3 was a drop in `P` alone, on an incomplete system; #92 stays a draft |
| `roadmap.md:123-134` [#90, #91]; `design.md:662-666`; `README.md:7` | "Then, revisit needs bars" after errands | the agreed slice next; bars "later, assessed against the richer system" |
| `roadmap.md:55-60`, `sim/README.md:9-18`, D44 (`decisions.md:829`); `character-inspiration-spec.md` [base] | the anchors are "a partial starting history" | "background context for models, not memories; engine histories are planned"; cite the audit's kinds and rules |
| `character-voices-2026-10-12.md:13-21, 451-461` [base] | the admission review | add R1-R3, the knowledge boundaries and count-once; A: the pack's seven qualifications (2), in their own PR before reuse |
| D52; `needs-spec.md:78-80`; `DefaultTown.cs:361` [#90] | Penny won't fetch beer; "alcoholism" | D52 kept as written; a new entry records the `Commitment`, its origin, canon's support and canon's words, and that it does not cover accepting or drinking (A) |
| D52 (`decisions.md:1064-1066, 1070-1071`) [#90]; `needs-bars-spec.md` (c), `:252-260`, answers 5-7, `:422-424` [#91] | Penny's "keeping Pam from the saloon" parked for the bars revisit; one's own hours open; the stay-in ask approved "as described"; her own drink "the body's rule"; `ErrandHoursBusy` | the plan offer takes D52's parked concern outside bars; (c) stays on record, on hold with the bars spec; the agreed evening is informal, disappointment possible (A); rule 16 cites answer 7 |
| `DefaultTown.cs:463` (her card), `:99` (Evening gathering); `Model.cs:147-150` [base] | Penny at the Saloon's evenings; a `Gathering` weighs households | A: her evening place as her own data, its own PR, pinned runs changing. Ours (E): how, a per-person hub weight (a new field); applying it to George's outings before the transfer test; bedtime per O1 |
| `design.md:326, 571`; `character-inspiration-spec.md:52-56` [#91]; `needs-spec.md:156-167` [#90] | core memories, "Penny's is the first" | marked proposed; replaced by histories anyone can hold |
| `design.md` 3a, laws 1-4 and 12; rules 10-11 [base] | laws mix Spinoza with our choices; law 2 reverses the code | split each law into "Spinoza says" and "our adaptation"; add association, recollected joy, hope and fear, vacillation, learned expectation, each "not built" until it is |
| `design.md:175`; `Model.cs:311, 348`; `reflection-spec.md:42, 46` [base] | sentiments and the life record read by no rule; recall by uniform draw; intentions skip the gate | changed in the slice's PR, under `Lives` |
| `DefaultTown.cs:375`; `design.md:293` [base] | Shane "is not Marnie's kin" | he is her nephew (`Data/Characters`), a separate fix; no `Kin` values |

**Coordination with Codex's work** (Appendix D). The versioned observer contract is proposed in new files under
`docs/under-glass/contracts/`, apart from the viewer: `lives-observer-recording-v0.1.md` (the contract),
`lives-observer-example-v0.1.json` (a small synthetic fixture, labelled illustrative, not output of a built system),
`check-lives-observer.cjs` (its checker) and `lives-observer-v0.1-tools/` (the fixture's generator and a fault suite).
It shapes the `Thread` record (9.2) as Appendix D asks; observer records are descriptive and never feed the simulation
(no rule, recall or model request reads them); Codex's viewer files are not touched; archetypes hold no life (11). It
uses this revision's labels (A, O1-O3, rule numbers) and agrees with it on rule 9 (a `Derived` reading summarises,
leaving the chance alone), 9.4 (disappointment's form and target, O3 as we lean) and 9.5 (an agreed evening writes one
follow-through entry and no whereabouts entry).

## 14. Product decisions: O1-O3 decided

**Decided 2026-10-14** by Sid's fifth direction (section 0; Appendix E): O1 as (b), canon's bedtime the baseline and (a)
the comparison; O2 as our lean, with each seeded reading individually reviewed and the hobby wish giving no warmth;
O3 as our lean, with a later person-directed reading requiring Penny's own supported interpretation. The questions
as they were put, for the record:

- **O1. Penny's bedtime.** As shipped she spends 89 of 111 evenings at the Saloon's Evening gathering, 80 with Pam,
  buying her own drink [base]; canon never puts her there but in green rain. Her evening place becomes canon's home as
  her data (A); the bedtime is separate. (a) The sim's, about 21:20 (E), as in section 8; or (b) canon's: 18:30 on
  Tuesdays, Wednesdays and Fridays, 18:40 on spring and autumn Mondays, Thursdays and Sundays (21:00 in summer and
  winter), 20:00 on Saturdays, 21:00 if it rains (`fixtures/game/Penny.json`; lookup `NPC.cs:5755-5890`), so most spring
  and autumn weekday plans last 30-40 minutes and the fixture moves to a Saturday, a summer or winter Monday, Thursday
  or Sunday, or rain. #91's answer 6 (the window ends at her bedtime) holds either way. Our lean: (a) first, (b)
  measured beside it, in the routine PR.
- **O2. Present readings at the start.** Revision 1 settles readings tied to completed pasts, not the present readings
  the pilot rests on (7; S·R2, our choice): the heart-gated p4 (`Mon4`), p5 (`Fri4`, `Sun4`), p6 (`fall_Thu6`, `Mon8`),
  m4 (`summer_Thu4`, `Fri2`), m5 (`Thu4`) and g3 (`Thu8`), with m3 and g2. Should each with a stance seed one entry that
  lived ones outweigh, `Mon8`'s "eager to move out" shown only (our lean); only some; or none, leaving Penny no sides
  toward Pam, no torn choice and a thread on lived records alone? And does Pam's own wish for a hobby (`Pam:Fri2`) give
  warmth toward the game it names (her keep pick 0.48 against 0.42), or only open offers (rule 11)?
- **O3. Where an agreed evening's disappointment lands** (revision 3 allows it; the observer contract's fixture takes our lean). Mood only; mood
  and one cold entry toward the hoped-for evening, read only by the holder's own keep pick at a later plan for it and
  recalled, never by offers to the person (our lean; 9.4, 9.5, as the observer fixture has it); or toward the person, so
  every later choice about Pam reads it beside the follow-through entry, which count-once (9.5) would forbid?

**Can wait, or follows by default unless Sid says otherwise**: accepting or drinking alcohol (#91's answer 5 stands);
answers 6 and 7 outside bars (O1; rule 16); (c), on hold with the bars spec; what follows the slice (the helping act, or
making and bringing); whether an authored link (E) may ever seed a number (here, cues only); George's outings as data;
the Pam-Gus friend seed (0.4; her side sourced, m7, his only g2 and g3), kept with his side marked unsourced, as seeds
are symmetric (`SeedRegard`, `Simulation.Feelings.cs:109-118`; `SeedFamiliarity`, `Simulation.cs:366-374`).

First guesses (k 5, intended 0.2, R 0.05, `TornAt`, `PlanBase` 0.1, displaced habit 0.4, the 60-minute wait, set-point
share 0.25, `DeriveAfter` 3, `SourcedCue` 2, disappointment's D 0.05 and scale 0.25) are set by threads, not aggregates.

**Corrections after the evidence PR's checks (2026-10-14).**
- O1's winter Mondays, Thursdays and Sundays are 18:40, not 21:00, except winter 4 (21:00) and winter 15 (the Night
  Market, 23:50): Penny's `winter` key goes to the Community Center at 10:30, and until its restoration (event 191393)
  the game runs her `spring` key instead (game `NPC.cs:5442-5443`, `:5669-5673`; `Game1.cs:12363-12367`).
- Canon's hours are when she leaves for bed; she shows asleep once she arrives.
- Her schedule never puts her at the Saloon but on year 1's green-rain day; it does not put her at home every evening.
  She is home and awake in the evening on summer Mondays, Thursdays and Sundays, the 9th and 23rd (16:00-18:40, while no
  farmer has 6 hearts with Penny or with Sam), winter 4 (from 19:00) and rain days, and, after the Community Center's
  restoration, on winter Mondays, Thursdays and Sundays, which the sim cannot represent; Saturdays she is in Town with
  the children until 20:00. A Saturday at home is ours (E), so under canon the pilot's evening is a summer Monday,
  Thursday or Sunday, and a Saturday is only an E-marked comparison; a pilot run lasts at least 35 days (sim days 28, 31
  and 34 in reach; day 34, summer 7, can be year 1's green-rain day, when canon keeps her at the Saloon all day).
- The sim's later bedtime with her evenings at home is about 20:30 (a prototype, seeds 1-3), not 21:20, which included
  the walk home from the Saloon.
- p8 (section 7): her hated gifts also include item 873, a drink Pam loves (`Data/NPCGiftTastes:Penny`; its name
  VERIFY). The game never has Penny suggest Pale Ale, Beer, Wine or Mead as a gift for Pam (`NPC.cs:3667`); it does not
  exclude item 873.

**After PR B (D54, 2026-10-14).** Sid decided that bedtimes are habits, not hard rules: no hold to 06:00, sleep until
rested, and no less available because the hour has passed. Measured ([write-up](experiments/penny-routine-2026-10-14.md),
seeds 1-3 x 112 days), for the pilot:
- Her summer Monday, Thursday and Sunday evenings at home run about 18:00-19:50 in the sim, not canon's 18:00-21:00:
  after a 04:30 waking her body sends her to bed before her 21:00 habit.
- She and Pam are awake at home together about half an hour on workday and Saturday mornings (Pam up at 09:02, Penny
  out at 09:30) and over two hours on Sundays, never at night; Pam is at the Saloon from about 16:40.
- With her drinks gone the trailer stays out of debt, and Pam's need-driven bin rummaging, with Penny's shame at it,
  no longer happens (20 seeds); the placed-scandal check keeps one such case (seed 14).
- Early waking leaves her about six hours a night alone at home with one option, home: gaps recorded for the pilot,
  not fixed.

## Appendix A: Sid's direction, 2026-10-14 (verbatim)

Keep law 1 as the guiding principle. Focus next on histories, associations, and meaningful actions before deciding how needs bars should influence behavior.
The law 1 experiment is useful, but its "pure Spinoza" mode tests sadness applied to an incomplete system. We have implemented diminished power more fully than the processes through which a person remembers, imagines, and pursues something that might help. Its results don't establish that the underlying principle needs changing. Keep PR #92 experimental; don't promote any of its modes into the settled design yet.
The next work should connect:
Experience → remembered associations → imagined possibilities → competing desires → action → consequences that change future expectations.
Some of these pieces already exist. Inspect their actual implementation and identify the missing connections before proposing replacements.
The priorities are:

1. Histories that influence behavior. Characters should begin with meaningful prior experiences, using established Stardew backstory before the farmer arrives. We already have starting relationships and background anchors for inspiration, but text supplied to a model is not equivalent to a history that shapes behavior. Starting history should seed the same kinds of attachments, expectations, fears, and habits that experience during a run can develop. Keep historical facts separate from the character's interpretation of them. Avoid named-character exceptions.
2. Associations with personal meaning. People should associate people, places, activities, and situations with remembered experiences. Frequency alone is insufficient: repeatedly visiting somewhere does not mean enjoying it. Preserve conflicting associations—affection alongside embarrassment, anticipated comfort alongside fear of rejection. These should influence what comes to mind and what seems desirable without guaranteeing a beneficial choice.
3. Meaningful actions and opportunities. Characters need enough distinct activities to express those differences. Examples include making something, learning, caring for someone, sharing an interest, seeking company, repairing a disagreement, and choosing solitude. Select a small, connected set with actual consequences and follow-through. More labels or dialogue variants for otherwise identical actions won't solve this.

The relevant philosophical direction includes memory and association, imagining what supports activity, desire directed toward conceived causes of joy or sadness, and conflicting attachments. Our numbered design laws are our adaptation of Spinoza; distinguish the source philosophy from implementation choices. Don't reduce the character to a conscious optimizer of one score.
Keep the hybrid model direction: authored routine speech, occasional generated private inspiration, and Laya evaluating grounded possibilities. Models can help suggest an interpretation or possible response. They must not invent a character's past, turn imagination into evidence, or substitute plausible prose for missing mechanics.
Bars remain an open implementation question. Later, assess how bodily pressures interact with this richer system. Don't choose a formula now because it reduces aggregate boredom or loneliness. Likewise, don't force histories to produce healthy coping, reconciliation, or stable towns. Characters may misunderstand, hesitate, repeat mistakes, withdraw, or escalate when their experiences and circumstances explain it.
Evaluate through inspectable personal threads: what the person remembered, what they associated with the situation, which possibilities occurred to them, what competing feelings mattered, what they attempted, and what they learned afterward. Metrics remain useful diagnostics, but interesting, intelligible lives are the objective.
Start with a gap assessment and a proposed small first slice for discussion. Clearly separate what is built from what is planned, explain how the slice connects to existing systems, and identify which docs conflict with this direction. Don't implement another bars variant or rewrite law 1 before we agree on that plan.

## Appendix B: Sid's revision direction, 2026-10-14 (verbatim)

Revise docs/under-glass/lives-gap-assessment.md around a complete thread connecting history, reflection, choices, meaningful action, and learning. Keep law 1 as the guiding principle and leave bars experimental.
The assessment identifies the gaps well, but the proposed first slice postpones too many essential connections. We can use a smaller cast to make the first demonstration manageable. That is a testing scope, not an architectural limit: scaling the population remains an important goal. The mechanisms must work for arbitrary characters and later generations without named-character rules or requiring model calls for every decision.
Please incorporate these revisions:
1. Histories must support completed experiences that still matter.
   Don't restrict history to ongoing episodes representable by existing act kinds. A childhood experience can remain influential after it ends. Historical records need supported facts, per-person accounts, provenance, and interpretations. When canon explicitly supplies a character's own interpretation, preserve it as sourced starting information, distinguished from an interpretation derived by the simulation. Don't invent events, exact dates, or repetition counts to manufacture the desired personality.
   Starting history and experiences during the run should feed compatible representations of associations and expectations. This does not require reenacting an entire childhood through today's action catalog.
2. Connect reflection in the first slice.
   At least one historical experience should be recalled because it matters to the current situation, inform a private reflection, and influence the alternatives the character actually considers. Avoid building a movement system whose decisions remain disconnected from the character's thoughts.
   Keep routine speech authored. Generated inspiration may propose possibilities or interpretations; it cannot invent historical evidence. Accepted reflection intentions must enter the relevant choice process and respect physical eligibility.
3. Complete one meaningful social thread.
   Seeking company is a reasonable starting point, but "found them" is an intermediate outcome. Include an invitation, an answer, a shared activity with a meaningful consequence, and an interpretation that changes a later expectation or choice. Reuse existing systems where they serve this thread.
   Making something and bringing it to someone remains a good subsequent slice. We don't need a broad catalog immediately.
4. Compare a small set of alternatives.
   Strongest-first can remain existing scaffolding elsewhere, but the pilot should compare a few possibilities, including continuing the routine or staying home. Preserve the distinct attractions and reservations behind each option. Don't store warmth and dread separately only to discard their meaning before evaluation.
   Use Laya selectively to evaluate grounded alternatives. Ordinary decisions should remain affordable and have a deterministic fallback.
5. Distinguish outcomes before learning from them.
   Not finding someone, finding them busy, being explicitly refused, and spending time together are different experiences. A failed search should update an expectation about whereabouts; it should not automatically become interpersonal rejection or dread.
   Observation, testimony, and inference must also remain distinct. Living with the visible effects of someone's hangover does not establish eyewitness knowledge of drinking elsewhere.
6. Let lived experience revise the starting state.
   The immutable relationship baselines are a material concern. We need not replace the entire relationship system in this slice, but demonstrate that experience can durably revise at least one attachment or expectation. Explain how the new records interact with existing regard, sentiments, and baseline drift, including how you avoid counting the same experience twice.
7. Preserve Penny's established boundary.
   Keep her refusal to fetch alcohol until a general replacement can represent it. History-driven behavior does not require every boundary to become probabilistic. A reusable commitment or refusal mechanism may have character-specific starting data and a recorded origin. Don't weaken Sid's earlier decision merely to remove WontFetch.
8. Include household behavior required by the pilot.
   A comprehensive family system can wait. If the selected example concerns a household, blanket exclusions must not prevent the care, conflict, or repair needed to complete its thread.
Population scaling
Choose a small demonstration cast, but design the same system to operate across the town:
- Retrieve a bounded set of personally relevant memories and possibilities.
- Trigger expensive reflection at meaningful opportunities rather than polling a model for everyone every tick.
- Keep individual histories and associations bounded without erasing important contradictions.
- Preserve seeded behavior and exact replay through recorded model answers.
- Measure runtime, memory use, and model calls as population grows.
- Don't make off-screen characters cease to experience consequences or learn.
Identify likely scaling costs and propose a modest larger-population check alongside the detailed small-cast review. Avoid premature infrastructure; demonstrate that the approach can grow.
Docs and evaluation
Correct the assessment's overstatement about power: DesireMath.Effective already reads it in the pursuit gate. The larger missing connection is how experience and feelings produce destinations and possibilities.
Preserve earlier decisions as dated history while clarifying the current direction. Law 1 stays; bars remain unresolved, not prohibited. Remove language treating the particular bars mode as the settled recommendation. Verify canon corrections independently and keep unrelated relationship types outside the slice unless needed.
Success is an inspectable thread:
What happened before → what this person remembers and associates → why it comes to mind now → what they imagine → what alternatives compete → what they attempt → what actually happens → what changes for next time.
Poor choices, refusal, withdrawal, and unresolved conflict are valid. We are evaluating understandable personal causation, not whether everyone becomes happier.
For now, revise the assessment and propose the smallest complete slice for discussion before implementation. Name the demonstration cast, identify the historical evidence it needs, explain the meaningful action and learning outcome, and show how the same mechanisms scale beyond those characters.

## Appendix C: Sid's third direction, 2026-10-14 (verbatim)

Please incorporate Codex's history audit into your assessment:
`docs/under-glass/experiments/history-evidence-audit-2026-10-14.md`
The audit is separate and uncommitted. It reviewed all 101 anchors across 34 characters against the local game sources. All 140 references resolve to 110 distinct source entries. It changed no character data, code, roadmap or existing assessment.
The direction remains: keep law 1 unchanged and focus on histories, associations, reflection, choices and meaningful consequences before deciding how the bars should work. A smaller pilot cast is acceptable, but the mechanisms must support a growing population.
Please incorporate these findings:

1. The anchors contain useful history, but they are not 101 implemented memories. Currently they supply private prompt context. Distinguish stable facts, completed experiences, recurring practices and personal interpretations. Some anchors overlap; don't count them as independent experiences that reinforce a feeling twice.
2. Completed histories must be admissible even when the action catalog cannot replay them. A childhood accident, bereavement or departure can influence present choices without implementing those historical events as runnable actions. The first slice needs meaningful present responses and consequences.
3. Separate experience from interpretation. Preserve conflicting feelings without cancelling them into one regard number. Useful examples:
   * Marnie explicitly connects a childhood dock accident to present fear of creaking wood.
   * Leah connects a childhood valley visit to her wish to return.
   * Clint describes both paternal pressure into blacksmithing and pride in his work.
   * Alex distinguishes remembered mistreatment from his conjecture about his father's motives.
4. Correct or qualify four anchors before treating them as mechanical evidence:
   * `maru-clinic`: the source establishes that Harvey values her availability if equipment fails, not a completed repair.
   * `linus-tent`: prior destruction and his present plea are sourced; generalized visitor distrust is our interpretation.
   * `demetrius-valley`: the ecosystem made him excited to move; that is narrower than establishing why he moved.
   * `alex-childhood`: add the corroborating Beach source identified in the audit for the age qualifier, or omit that qualifier.
5. Preserve chronology and knowledge boundaries. Later dialogue can reveal older history without making the disclosure scene a starting memory. Present feelings and later growth of attachment are not automatically pre-arrival states. One participant's account does not supply everyone else's feelings. Penny experiencing hangovers and household burdens does not mean she witnessed every drink or knows Pam's motives.

Pilot recommendation: consider Penny, Pam and Gus for the first complete thread. Their sources support different meanings attached to family, work, household obligations and the saloon. Penny also needs her own valued pursuits, including teaching. Preserve my existing decision that she will not fetch alcohol; distinguish that product commitment from what dialogue alone proves. Refusing alcohol should still leave room for other forms of care.
The pilot should connect:
history → recalled association → reflection and competing possibilities → choice → another person's response → practical outcome → revised expectation
Seeking or finding someone alone is insufficient. Include a meaningful action with follow-through, and distinguish absence, being busy, refusal and failure. New experience must be able to change expectations durably.
The audit suggests Evelyn, George and Alex as a second household to test whether the mechanism generalizes beyond alcohol and crisis. It also evaluates the carpenter's household and Emily/Haley; use your assessment to explain which cast best supports a complete first slice.
For scaling, propose reusable experience forms and bounded relevant recall. Keep named biographies separate from inheritable archetypes, avoid character-specific decision branches, and don't require model calls over everyone's full history at every choice.
Please update your assessment, referencing the audit rather than overwriting it. Identify which conclusions change, the smallest complete slice you recommend, and any remaining decisions that genuinely need my input. Keep sourced facts, editorial interpretations and proposed mechanics clearly distinguished. Stay at assessment stage for now; don't implement these changes yet.

## Appendix D: Sid's fourth direction, 2026-10-14, relayed from Astra (verbatim)

I agree with the revised assessment's overall direction: keep law 1 unchanged, leave bars experimental, and build a complete thread connecting history, reflection, choice, meaningful action and learning.
Please incorporate the following revisions into `docs/under-glass/lives-gap-assessment.md`. Keep the history audit untouched, distinguish proposed mechanics from implemented behavior, and preserve the separation between sourced evidence, my product decisions and our interpretations.
Revisions

1. Completed histories must be able to influence present choices.
Where a character's own account connects a completed experience to a present attitude, allow that reading to support an association or expectation. Don't infer a disposition automatically from the event. Any additional connection must be explicitly identified as authored interpretation. A documented seasonal recall cue can be especially strong without becoming an exclusive restriction on recall; distinguish sourced cues from proposed ones.
2. Learning starts with the first relevant outcome.
Treat "three agreeing outcomes" as a tunable experimental heuristic, not the definition of learning. New evidence should progressively revise expectations, including mixed or contradictory evidence. Preserve the original historical account and the evidence behind subsequent revisions.
3. Informal agreements can have emotional consequences.
An agreed evening need not become a formal promise, but that should not prohibit disappointment. Separate the observable outcome—agreed, then absent—from the person's interpretation. Any emotional response should depend on their knowledge, expectations and investment. Absence alone must not establish deliberate rejection, betrayal or where the other person went.
4. Laya and the deterministic fallback must receive the same eligible alternatives.
"Not now," refusal and other meaningful responses must not exist only when Laya is enabled. Both methods should operate on the same grounded options and constraints; their weighting may differ. Comparisons should isolate decision method rather than accidentally compare different action spaces.
5. Ordinary encounters must initiate threads from the first slice.
Include invitations arising from recalled warmth, an interest or a wish, alongside the beer-refusal trigger. Care and shared activity must be possible without a crisis. Use controlled fixtures to verify the mechanism and natural runs to establish whether it becomes part of everyday life. Don't force successful or harmonious outcomes.

Direction for the open decisions

* Proceed with Penny, Pam and Gus as the pilot, then Evelyn, George and Alex as the transfer test using the same mechanisms.
* Correct unsupported starting routines as character data. Don't strengthen history weights merely to overpower an arbitrary saloon attraction. Later experience should be able to change routines. Keep exact bedtime fidelity as a separately identified choice rather than silently resolving it.
* Preserve Penny's absolute refusal to provide alcohol as my product commitment. Do not extend it to accepting or drinking alcohol without a separate decision.
* Prepare the seven evidence corrections in a separate PR before reusing those records as executable history.
* Keep law 1 unchanged; mark conflicting amendments as on hold and remove language presenting the bars experiment as the selected recommendation.
* Treat current population measurements as the baseline. Measure the added history, recall, learning and model costs separately.

Coordinate with the archetype UI work
Codex will develop the archetype browser/editor alongside this work. It will begin with reusable dispositions, coping, voice, source evidence, character assignments and links to authored speech. Personal biography, commitments and lived histories remain separate from archetype definitions. Duplicating an archetype must not copy a donor character's life.
Please provide a proposed, versioned observer recording contract and a small example JSON fixture early, before implementing the whole slice. Put these in new, clearly named files separate from the existing viewer work, and report their paths. Coordinate through this contract; do not edit Codex's viewer files.
The example should support progressive disclosure and a graph linking:
history/account → recall → alternatives → choice → attempt → observed outcome → learning
Include:

* Stable run, character, record, decision, plan and outcome identifiers, with explicit links between them.
* Who knows each fact, how they know it, and when it becomes available to them.
* Recalled records and the cues that brought them to mind.
* Eligible alternatives, supporting and conflicting reasons, and relevant constraints.
* The selected alternative, decision backend and applicable replay/model provenance.
* Separate intention, attempted action, actual world outcome and each character's perceived outcome.
* What associations or expectations changed, their previous and new states, and the evidence responsible.
* Unresolved, declined, busy and unsuccessful paths without inventing missing links.

Provide a matched choice with and without one relevant history, linked by a comparison identifier. Hold the immediate world state, available actions, other personal state and random input constant where possible; explicitly list the intervention and any unavoidable differences. Distinguish this isolated comparison from whole runs that subsequently diverge. A history can change reasons or probabilities without changing the sampled action, so expose both.
Keep observer records descriptive: they must not become inputs that alter simulation behavior. Show how the viewer can respect the playback clock and distinguish an observer's privileged view from a character's knowledge.
Clearly label synthetic example content as illustrative, not output from an implemented system. Reuse existing recording fields where appropriate and identify additions. Design for bounded recall and larger populations without copying complete histories into every decision.
First return the revised assessment, remaining genuine product decisions, and the example contract/fixture. The fixture is the immediate coordination deliverable so the UI and simulation work can proceed together.

## Appendix E: Sid's fifth direction, 2026-10-14, relayed (verbatim)

Claude — next steps for the Lives pilot and observer contract

Sid has approved proceeding with the history-first direction. Keep law 1 unchanged, leave needs bars aside, and build the Penny–Pam–Gus pilot around history → recall → competing choices → attempt → perceived outcome → learning. The observer product comes first; player gameplay remains later.

Work separately from Codex's viewer branches and do not edit its UI files. Codex is building a read-only chain viewer and the two recorded history comparisons against a frozen, clearly illustrative copy of the v0.1 fixture. No fixture probabilities are evidence of runtime model quality. Share revisions via versioned contract/fixtures, not edits to the viewer's snapshot.

1. Land the seven evidence corrections in their own PR first. Keep the audit intact. Make routine corrections character data, with their source and behavior changes recorded. Update roadmap/spec statements that still put bars first or describe prompt anchors as lived histories.

2. Use the following pilot direction, agreed by Sid after Codex's recommendations:
- Prefer Penny's sourced routine as the baseline. Choose an evening on which the pilot activity is feasible; don't extend her day merely to make the example work. Keep the later simulated bedtime as an explicit comparison.
- Seed individually reviewed present readings modestly, retaining source/admission standing. Lived outcomes must be able to outweigh them. Pam's stated wish for a hobby can make the activity a possibility; it does not establish prior enjoyment or warmth toward a game with Penny.
- Initially disappointment belongs to mood and the hoped-for evening. No automatic judgment that Pam rejected or betrayed her. Later person-directed interpretations need Penny's own supported interpretation and must not double-count the outcome.
If the sourced routine exposes an implementation ambiguity, identify it without silently inventing canon. These are pilot choices, not claims that canon supplies our numbers.

3. Revise the observer contract (next revision 0.2) before publishing a runtime writer:
- Pin the exact character-inspiration pack used by the run: stable archetype ID plus pack content hash/version and a retrievable immutable artifact or snapshot outside character life records. Reuse existing recording provenance where possible. A path and ID alone cannot identify a profile after editing. Readers must show 'recorded profile unavailable' on mismatch rather than substitute today's draft. Archetypes still carry no life history.
- Keep busy/not-asked, not-now, refusal and acceptance distinct. Design an additive base-row answer/status field with explicit unknown handling for older data; don't encode these as false or infer refusal from a missing act. Resolve the actId:-1 indexing issue without colliding multiple plan answers.
- Define safe character-facing labels separately from observer/debug prose. In particular, notPerceived can describe secret truth ('Pam was home until 17:31'); hiding world nodes is insufficient if such fields or observer thread titles remain visible. Character summaries must be built only from clock-gated permitted elements. Targeting someone is not proof that they perceived an attempted ask: the busy/no-ask example must not tell Penny about Gus's attempt unless she actually noticed it.
- Keep per-field gates for decisions, resolution, expectations and summaries. Future plan windows are intentions, not outcomes. Specify how 'initial' expectation state is shown before its asOf, and preserve earlier states without backward leakage.
- Separate structural/version/reference/time/knowledge validation from experimental rule/formula checks. Both belong in tests, but the reader must not require one disappointment formula to accept a recording. Allow structural tests on real output without requiring illustrative labels or placeholder model answers.
- Keep v0.1 history comparisons explicitly isolated and fallback-only. They don't show hybrid counterfactual outcomes or later lives. Record the held-constant method, scene, options, random input and other personal state; recompute recall/reasons/probabilities as consequences of the intervention. If the opportunity disappears, record an unsupported/non-comparable case rather than manufacture a matched decision.
- For the first implementation retain readable objects and stable codes, with optional diagnostic labels. Put storage behind an adapter so compact encoding or person/time chunks can come later after measurement. Don't freeze a premature compact wire format into the UI.

4. Implement the smallest complete pilot on its own branch/PR after the evidence corrections. Use general mechanisms and character data, never Penny/Pam/Gus name switches. Keep mixed associations separately; allow ordinary encounters to open offers; provide identical eligible options to Laya and fallback, including refusal and not now. Agreements may disappoint. Learning begins at the first perceived outcome, preserving causes and earlier evidence. The recorder is a write-only observer sink and cannot drive behavior.

5. Acceptance: show one complete thread, including a failed or postponed path; the same decision with/without relevant history; probability/reason changes even if the sampled action stays the same; learned expectations affecting a later choice; no mistaken-inference-to-fact promotion; no future/private-knowledge leaks; recorder-on/off equivalence and exact replay; bounded recalls/model calls. Retain authored controls and hybrid (Haiku + Laya, with local Qwen + Laya available for comparison), recording exact model identity and receipts. Do not target a quota of kindness, conflict or successful evenings.

6. Transfer the mechanisms to a second household before population expansion. Measure CPU time, recording size, memory and model use separately. Current size numbers are fixture estimates, not measured runtime scaling.

Please return the updated contract/fixture with a concise change list and implementation sequence, then proceed with the authorized pilot work. Coordinate any incompatible schema change before Codex replaces its v0.1 adapter. Do not implement model trial launching, archetype editing, or chain-viewer UI; Codex owns those surfaces.
