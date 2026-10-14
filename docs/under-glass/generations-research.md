# Addiction, passed-on memories and inborn likes: research for Under Glass (rule 18, the needs bars spec)

> Research for later mechanics, not the next milestone. The [roadmap](roadmap.md) puts concrete encounters, responses and follow-through first; the [needs bars spec](specs/needs-bars-spec.md) puts addiction at its step 5, and generations belong to 0e. This answers three of Sid's notes on that spec (2026-10-13): addiction (fun drains twice as fast, the addictive item cures boredom faster, and "the item cure factor [should] shrink over time, requiring more. Maybe up to a set limit"); core memories, which "also have a chance of being somewhat passed on"; and a newborn's like and hate, "not exactly random", with "a high mutation factor", unknown to the child until met. It sits beside [inheritance-research.md](inheritance-research.md), which covers traits.

Scope: alcohol, because drinking is the town's indulgence (`Indulgence("drinking", ...)`); memories and their passing from parent to child; strong likes and dislikes and how they are found. Pam and Penny are worked examples only: no rule here reads a name. Every claim names its source. **Verify** marks anything recalled or seen only in a search summary or secondary report; the source lists give how far each source was read: [full] full text, [abstract], [page] a web page, [summary] a search summary or secondary report. Figures from an abstract should be checked in the paper before anything leans on them. Read in full: Koob 2008, King et al. 2014, Dawson et al. 2006, Moos & Moos 2006, Kirshenbaum et al. 2009, Hisler et al. 2022, van IJzendoorn 1995, Svob & Brown 2012, Baudet et al. (preprint), Sher 1997, Rossow et al. 2016, Verhage et al. 2020, Smith et al. 2016, Knaapila et al. 2012, Eriksson et al. 2012. Abstracts re-checked for this document (Europe PMC or the publisher, 2026-10-13): Verhulst et al. 2015, Schuckit 1994, Grant et al. 2015, Luczak et al. 2006, Harburg et al. 1990, Rozin 1991, Assink et al. 2018, Madigan et al. 2025. Could not open: Solomon 1980, Hunt et al. 1971, Cook 2007, Lally et al. 2010, and Garb & Stunkard 1974 (the publisher refused). Every constant in the models is game tuning, checked against the calibration targets, not an empirical rate.

## Part 1. Addiction, tolerance and proneness

### 1.1 What repeated use does

| Account | Claim | For a drinker | Source |
|---|---|---|---|
| Opponent process | a drug's pleasure (the a-process) is followed by an opposite low (the b-process). The a-process shows tolerance; the b-process builds slowly, decays slowly and grows with use | each dose nets less and the low between doses grows: tolerance and withdrawal as one mechanism | Solomon & Corbit 1974, Solomon 1980, as described in Koob 2008 (originals not read) |
| Allostasis | the reward set point shifts and stays shifted; stress systems are recruited | drinking less for pleasure and more to get back to normal; natural rewards lose their pull | Koob & Le Moal 2001; Koob 2008 |
| Incentive sensitization | wanting and liking are separate. Use sensitizes wanting, especially toward cues, in some people and for a long time; liking does not rise with it | the pull of the drink and its places grows and outlasts abstinence | Robinson & Berridge 1993, 2025 |
| Habit | use moves from goal-directed to habitual, then compulsive | drinking at the usual time and place, wanted or not | Everitt & Robbins 2005. Disputed by Hogarth 2020: human evidence for habit is weak; drug choice looks goal-directed, growing under stress and low mood |

**For the game:** the four barely compete. The opponent process is Sid's faster drain; allostasis says ordinary fun fills less; sensitization gives a slow, cue-bound pull that explains relapse. Habit is the weakest: simple daily behaviours reached 95% of their automaticity plateau after a median 66 days, range 18-254 (Lally et al. 2010, via search summaries; verify), and Tiffany's 1990 model of urges from a blocked routine (recalled; verify) was not clearly supported (Bradizza et al. 1995). The spec's 14-evening habit share already covers habit; nothing here justifies a compulsion mechanic.

### 1.2 Tolerance: what falls, and what does not

- **The effects fall.** Tolerance to alcohol's impairing and sedative effects is well established:
  - within one session, people feel less drunk at the same blood level on the way down (the Mellanby effect; a 2017 systematic review found the evidence heterogeneous; search summary, verify);
  - over weeks, rats on a rising daily dose reached maximal tolerance in a mean of 46 and 55 days on two tasks, and their sensitivity halved (Bird et al. 1985); at the lowest dose little developed, at the highest it came fastest (Pohorecky & Roberts 1992);
  - metabolically, blood alcohol falls by about 15-20 mg/100 mL an hour after food, and by 25-35 in alcoholics in detoxification: up to about twice as fast (Jones 2010);
  - by learning: tolerance is partly a conditioned response to the usual surroundings, and drug-paired cues changed heroin mortality in rats (Siegel et al. 1982); the human version, overdose in an unfamiliar place, is recalled (verify);
  - faster the second time: rats with withdrawals between cycles acquired tolerance faster than rats dosed continuously (Maier & Pohorecky 1987).
- **The pleasure does not.** King et al. 2014 followed 104 young heavy social drinkers for 6 years: more stimulation, liking and wanting, and less sedation, at a lab challenge predicted more alcohol use disorder (AUD) symptoms, and those who worsened kept reporting more liking. At 10 years (King et al. 2021; 190 drinkers, 21% with past-year AUD), stimulation and wanting had *risen* in those with the most symptoms.
- **Reconciled:** a dose nets less because the low it must first cancel has grown, not because the high has shrunk (Koob 2008). Relief takes over in those who seek help: of 140 heavy drinkers not in treatment, 100 drank mainly for reward, 19 for relief and 21 by habit (Grodin et al. 2019); among 169 seeking treatment, relief drinkers craved more and drank more over 12 weeks (Grodin et al. 2024).
- **The learned part recovers slowly.** After 6 months without alcohol, rats kept a 1.7-fold shift of a 2-fold tolerance, far longer than usually reported (Bird et al. 1985; Bird & Holloway 1989). Rats whose cocaine intake had escalated came back to the higher level after a month off (Ahmed & Koob 1998).

**For Sid's refinement:** "the cure shrinks, requiring more, up to a set limit" fits tolerance if what shrinks is how much of the low one dose cancels, with a limit near twice the dose (Bird's halved sensitivity, Jones's doubled elimination). It should not be the drink becoming less liked: in those who became dependent, liking held and wanting rose (King).

### 1.3 Withdrawal, craving and relapse

- **After the last drink,** in dependent drinkers: withdrawal begins at 6-24 hours, seizures are possible at 24-72 hours in severe cases, and acute symptoms resolve within 7 days (Muncie et al. 2013); low mood and anxiety abate over 3-6 weeks, while relapse risk lasts far longer (Heilig et al. 2010); craving at cues *rises* for up to 60 days ("incubation": 80 male inpatients, highest at day 60; Li et al. 2015), and in daily life craving near alcohol grew with days abstinent (266 drinkers; Treloar Padovano & Miranda 2021). Withdrawal belongs to heavy, regular drinkers and is rare in occasional ones (AAFP 2004).
- Craving has a 24-hour rhythm, lowest about 08:00 and highest about 20:00, with a bigger swing in frequent drinkers (Hisler et al. 2022: 215 drinkers, six reports a day for 10 days). It was higher on imposed dry days and fell below its pre-drink level after a drink (Mayhugh et al. 2018, 32 drinkers).
- Boredom: trait boredom susceptibility predicted how often 414 students drank (Doering et al. 2023); in 181 AUD outpatients, boredom proneness went with drinking at baseline, not after treatment (Brown et al. 2026). Weak, but in the fun bar's direction.
- **Relapse comes early.** About two thirds of treated heroin, smoking and alcohol patients relapsed within 3 months (Hunt et al. 1971, via secondary summaries; verify). Fitting 20 alcohol and tobacco treatment studies, relapse risk is highest at the start and near zero after about 100 days of unbroken abstinence (Kirshenbaum et al. 2009).
- **In the long run** (Moos & Moos 2006: 461 people, 16 years): of those remitted at 3 years, 60.5% of those without help relapsed against 42.9% with help; with 0, 1, 2 and 3 or more risk factors, 22%, 45%, 70% and 86%.
- **Recovery without treatment is common** (Dawson et al. 2006; NESARC 2001-02, dependence begun before the past year): 25.0% still dependent, 27.3% in partial remission, 11.8% risk drinkers without symptoms, 17.7% low-risk drinkers, 18.2% abstainers; only 25.5% ever treated. Still dependent: 64.9% within 5 years of onset, 6.9% after 20 or more. Being married went with recovery.

### 1.4 How much, and where

- **How often:** light social drinkers aged 21-35 drank on 1.7 days a week, 1.8 drinks a time, and heavy social drinkers (mean age 25) on 51% of days, bingeing on 28% (King et al. 2014); dependent people seeking treatment drank on about 75% of days, about 12 drinks a time (COMBINE, 1,383 people, via a search summary of a Cochrane review; verify); the top tenth of US adults drank about 74 drinks a week (Cook 2007, from NESARC 2001-02, via press coverage; verify).
- **Pam** is at the Saloon on 111 of 112 evenings (needs spec, Measurements): beyond the average person seeking treatment, plausible for a dependent drinker. The sim's drink is one 12g charge an evening (`Drinks`, `Simulation.Money.cs:214`); it stands for an evening's drinking, not one standard drink.
- **At home** (Sid asked): about half of Great Britain's alcohol was drunk at home in 2019 diaries (a search summary of a University of Glasgow paper; verify). From 2001 to 2019 pub occasions fell and drinking at home without company grew, mostly among older people (Hardie et al. 2023). A dependent drinker kept from the bar would realistically drink at home; the spec's addiction row already counts a beer at home as a dose.

### 1.5 Proneness

- **Heritability.** AUD is 0.49 heritable (95% CI 0.43-0.53), with 0.10 shared environment, from 12 twin and 5 adoption studies (Verhulst et al. 2015). Personality is about 0.40 ([inheritance-research.md](inheritance-research.md)). In US adults, 12-month DSM-5 AUD was 13.9% and lifetime 29.1% (Grant et al. 2015).
- **General and specific, both.** One mostly genetic "externalizing" factor links substance dependence, antisocial behaviour and disinhibition (Krueger et al. 2002, 1,048 twins); relatives of alcohol-dependent people had more dependence on other drugs too (Nurnberger et al. 2004). Yet two genetic factors fit 4,865 twins, one for illicit and one for licit drugs (alcohol, caffeine, nicotine), correlated +0.82, with large specific parts for nicotine and caffeine (Kendler et al. 2007). ADH1B variants are the strongest genome-wide hits for alcohol dependence, only partly shared with how much people drink (Walters et al. 2018). In East Asians, one ALDH2\*2 or ADH1B\*2 allele protects against dependence and a second adds nothing (Luczak et al. 2006; the abstract gives no effect size). ALDH2\*2's flushing and nausea after a drink is recalled (verify): an inborn dislike.
- **The first drinks predict what follows.** At about 20, 40% of sons of alcoholics, against under 10% of comparison men, had a low response to alcohol; a low response at 20 meant four times the risk of alcoholism about ten years later, and among sons of alcoholics 56% of low responders against 14% of high responders became alcoholic (Schuckit 1994). Greater stimulation and liking predict AUD too (King et al. 2014, 2021). Both describe a drink that costs little and rewards much when first tried. The heritability of the response itself is recalled at about 0.4-0.6 (verify).
- **Temperament** predicts only modestly: one standard deviation more extraversion multiplied the odds of moving from moderate to heavy drinking by 1.14 (Hakulinen et al. 2015, 72,949 adults).

| Game trait | Measured trait | Link to drinking | Source |
|---|---|---|---|
| boldness | sensation seeking | among impulsivity traits, the largest link to how much adolescents drink; enhancement drinking goes with heavy drinking | Stautz & Cooper 2013; Kuntsche et al. 2005 |
| sensitivity | neuroticism | high in AUD; coping drinkers have more problems; relief drinkers do worst | Malouff et al. 2007; Kotov et al. 2010; Grodin et al. 2024 |
| retention | rumination | ruminators drink to cope more (verify: a press release and a summary table) | Nolen-Hoeksema & Harrell 2002 |
| chattiness; understanding | extraversion; agreeableness, loosely | odds ratio 1.14 toward heavy drinking, and low extraversion goes with quitting; odds ratio 1.09 toward abstaining | Hakulinen et al. 2015 |
| none | low conscientiousness, disinhibition | among the strongest: disinhibition d = 0.72 in substance use disorders | Kotov et al. 2010; Malouff et al. 2007 |

**Motives are routes, and they match the bars.** Social motives go with moderate drinking, enhancement with heavy drinking, coping with problems (Cooper et al. 1995; Kuntsche et al. 2005): social is the company bar, enhancement the fun bar, coping the sad mood that the passive-vice tilt already reads (`Model.cs:174`). So the traits choose the *route*; the strongest predictor, self-control, is none of the seven. Proneness is better a hidden value per indulgence, a general part plus a specific part, inherited like a trait's genetic value (Part 4).

### 1.6 The model

One private record per person and indulgence, made at the first dose or from starting data. `AddictedDrain` and `ToleranceFloor` are the spec's names; the rest avoid names already taken ("craving" is the errands coin's, "need" money's and errands').

**State,** each 0-1: **p**, proneness to this indulgence, fixed for life and hidden like the genetic value g; **u**, recent use, the doses in the last 14 closed days ÷ (14 × `UseFull`), clamped, from the drink-night ledger `Concerns` keeps; **a**, adaptation, tolerance and the between-dose low together (the opponent process); **s**, the scar, wanting sensitized to cues and learned tolerance, slow.

**Dependence** a′ = clamp((a − `DependentFrom`) / (1 − `DependentFrom`), 0, 1): a dead band, like the bars' line, so a regular moderate drinker feels little.

**Each closed day** (`CloseDay`; arithmetic, no draw):
1. Target T = p × u: someone not prone settles low however often they drink.
2. If T > a: a += `AdaptUp` × (1 + s) × (T − a); else a −= `AdaptDown` × (a − T). The (1 + s) is "faster the second time".
3. s = max(`ScarKeep` × s, `ScarShare` × a).

**On the fun bar**, each tick:
- **Drain** (a′ > 0): `SinceFun` += 5 × (1 + (`AddictedDrain` − 1) × a′). Sid's "twice as fast" is `AddictedDrain` 2 at full dependence.
- **A dose** (a′ > 0; the evening drink, or a beer at home): the day's d-th dose leaves max(0, 1 − d / n) of the `SinceFun` the first dose found, with n = 1 + (1 / `ToleranceFloor` − 1) × a′. Full dependence needs 1 / `ToleranceFloor` doses for a full fill: "requiring more, up to a set limit".
- **Other fun** (a′ > 0; an outing, a game) leaves `NaturalLoss` × a′ of `SinceFun` instead of 0 (Koob: natural rewards lose value). This is what makes the indulgence cure boredom faster than anything else.
- **Pull at cues** (s > 0, so also when dry): in the holder's own usual dose hours (from their own record; about 20:00 for most drinkers, Hisler et al. 2022), options at the indulgence's place take pressure pk = min(1, `CueGain` × s), joining R2 as max(pc, pf, pk) under `BarCap`, never the sum. This is what brings a dry drinker back.
- **Left out on purpose:** withdrawal's low mood. It is real (Heilig et al. 2010), but as mood it would move power, daring and tone, which amended law 1 says bars never do.

| Number | Start (range) | Basis |
|---|---|---|
| `UseFull` | 1 dose an evening | the sim's drink is an evening's drinking |
| `DependentFrom` | 0.4 (0.3-0.5) | a dead band, as the bars have |
| `AdaptUp` | 0.03 a day (0.02-0.06) | the top matches Bird's 46-55 days on a heavy, rising dose; human dependence takes months of heavy drinking (recalled; verify) |
| `AdaptDown` | 0.05 a day (0.03-0.10) | withdrawal clears in 7 days, low mood in 3-6 weeks (Muncie; Heilig) |
| `ScarKeep` | 0.998 a day (0.995-0.999) | a half-life of about 346 days: Bird's 6 months; Moos & Moos's years |
| `ScarShare` | 0.5 (0.3-0.7) | tuning |
| `AddictedDrain` | 2.5 (2-3); Sid's 2 | withdrawal 6-24 hours after the last drink (Muncie); craving peaks about 20:00 (Hisler) |
| `ToleranceFloor` | 0.5 (0.33-0.67): at most 2 doses (1.5-3) | Bird's halved sensitivity; Jones's doubled elimination |
| `NaturalLoss` | 0.5 (0.3-0.7) | Koob 2008 |
| `CueGain` | 1.0 (0.5-1.5) | tuning |

Simulated with these numbers (a throwaway script, not the sim; `AddictedDrain` does not enter):

| Case | Result |
|---|---|
| nightly use from nothing, p 0.3 | a settles at 0.30: never dependent |
| nightly, p 0.5 / 0.7 / 0.9 | dependent (a′ > 0) after 53 / 31 / 24 days; a′ at 120 days 0.15 / 0.48 / 0.82 |
| every other night, p 0.9 | a′ 0.07 at 120 days |
| Pam (p 0.95, a 0.95, s 0.6) stops | a′ 0 on day 23, a under 0.1 on day 50; s 0.49 after 100 days, 0.29 after 365 |
| Pam resumes nightly after 60 dry days | a′ above 0.5 after 34 days, against 43 for someone as prone with no scar |

| Stage, smallest first | What | Cost |
|---|---|---|
| A, Sid's simple method | a and s as starting data only, no daily rule: a `Dependence` record beside `CoreMemory`, with sources. Faster drain; other fun fills less; one dose fills fully (n = 1) | two readers on the fun bar |
| B, tolerance | n grows with a′; `Drinks` allows ceil(n) charges a day (`_drank` allows one today) | **moves money:** Pam's second drink costs the Trailer 12g a night, about 1,330g in 112 days. The spec's addiction row accepts that; it should have its own switch |
| C, dynamics | u, a and s daily; p for everyone; the cue pull | anyone at the Saloon after 18:00 drinks, so regulars can become dependent: measure how many |
| D, generations (0e) | p inherited; first doses reveal the taste (Part 4) | with 0e |

**Pam.** Starting data p 0.95, a 0.95, s 0.6 (a′ 0.92), from canon with sources: the Saloon every evening (`fixtures/game/Pam.json`), loves beer (`DefaultTown.cs:331`), Penny:Mon4, Penny:Fri4, Pam:Thu4, D52. Her fun line is about 2.5 days (3,636 minutes at a share of 0.99).

| `AddictedDrain` | Her drain | Bored after her last drink | Full pressure |
|---|---|---|---|
| 1 (today) | ×1 | 61 h | 91 h |
| 2 (Sid's) | ×1.92 | 32 h | 47 h |
| **2.5 (recommended)** | ×2.38 | 25.5 h | 38 h |
| 3 | ×2.83 | 21 h | 32 h |

- **Why 2.5:** withdrawal starts 6-24 hours after the last drink and craving peaks about 20:00, higher in frequent drinkers, so a missed evening should press by the next one. Sid's 2 does that a few hours later; either answers "2.5 days might be too long".
- **A yes evening at 2.5:** Penny asks on Tuesday; Pam's last drink was Monday at 18:00. At Penny's bedtime (21:15, 27 hours) Pam's fun pressure is about 0.14; by Wednesday 08:00 about 0.98, and the stay-in cost (`StayInBoredCost` 0.2 × pressure, plus 0.1 for Tuesday's ask) takes her margin from about +0.37 to about +0.07, so a second yes in a row is a close call (the close band's width to verify). By Wednesday 18:00 her pressure is 1 and her Saloon options weigh ×2 (`BarCap`): one quiet evening, then the pull wins.
- **With Stage B,** n = 1.92: one drink fills about half her bar, and she needs two.
- **A second case** would show the record is general: Shane also loves beer (`DefaultTown.cs:331`) and is the town's most sensitive adult (0.74), a coping-route drinker; his history would be drafted from canon and chosen by Sid.

**Calibration targets** (measured in the sim): adults with a′ > 0 in a year, about 1 in 7, mostly mild (Grant et al. 2015; the severity split to verify); a dependence held for years while routine holds, still there in about two thirds within 5 years of onset and 7% after 20 (Dawson et al. 2006); after a stop, a′ to 0 in 3-4 weeks and relapses bunched in the first ~100 days (Heilig et al. 2010; Kirshenbaum et al. 2009); more than half relapsing in the long run without help (Moos & Moos 2006); children of a dependent parent at about twice the rate of a′ > 0, and more often abstainers too (section 2.6).

## Part 2. Core memories across generations

### 2.1 What makes a memory stand out

- **Emotion at the time.** Arousal acts through stress hormones on the basolateral amygdala, which strengthens storage elsewhere (McGaugh 2004, review). Strong emotion predicted later *belief* in a memory's accuracy, not its consistency (Talarico & Rubin 2003: 54 students, 11 September 2001).
- **Consequence.** Nearly a year after Thatcher's resignation, over 86% of UK participants had consistent flashbulb memories, against under 29% of non-UK participants (Conway et al. 1994). Surprise and consequentiality as triggers: Brown & Kulik 1977 (recalled; verify). Children list a parent's important events as mostly *transitions*: immigration, births, deaths, marriage (Svob & Brown 2012).
- **Self-defining memories** are vivid, intense, linked to similar memories, recalled often and tied to lasting concerns (Singer & Salovey 1993; secondary summaries, verify). Current goals shape which memories are kept and how they are rebuilt: a memory must fit what happened and who the person is now (Conway & Pleydell-Pearce 2000; search summary, verify).
- **One blow or many.** A single event leaves full, detailed memories; repeated ones lead to denial and numbing (Terr 1991, Types I and II; later reviews prefer "single-incident" and "complex", and DSM-5 did not adopt Type II: search summary, verify). Breadth matters as well as severity: four or more kinds of childhood adversity meant 4 to 12 times the risk of alcoholism, drug abuse, depression and suicide attempts (Felitti et al. 1998, 9,508 adults). Across 77 studies every PTSD risk factor predicted it; severity only modestly (weighted r = .23, from a table summary), lack of support and later stress among the strongest (Brewin et al. 2000, via a BMJ commentary; verify).
- **Age.** Children recalled 60% or more of events discussed at 3 when aged 5-7, under 40% at 8-9 (Bauer & Larkina 2014). The life story forms in adolescence (Habermas & Bluck 2000). Older adults recall most memories, and the most vivid and important, from ages 10-30 (Rubin et al. 1998); that bump holds for the happiest and most important, not the saddest and most traumatic, which grow with recency (Berntsen & Rubin 2002, 1,241 adults). Attitudes are most open in the late teens and twenties (Krosnick & Alwin 1989).

### 2.2 How it fades or holds

- **Content drifts; confidence holds.** Flashbulb and everyday memories lost consistency at the same rate, but only everyday ones lost vividness and belief (Talarico & Rubin 2003). Memories of 11 September faded fast in the first year and then levelled off for ten; confidence stayed high, and inconsistent details were repeated rather than corrected (Hirst et al. 2015; about two thirds consistent at one year and 60% at ten, from secondary coverage, verify).
- **The feeling fades, negative faster:** the fading affect bias (Walker et al. 1997, 2003; in mildly depressed people both fade evenly, verify). It is small for events important to the self or self-caused (Ritchie et al. 2006), so a core memory's feeling should fade slowly.
- **Telling** eases a negative feeling the more an event is told (Skowronski et al. 2004, as later papers report it; verify); an interactive listener eased an unpleasant memory and a non-responsive one sharpened it (Muir et al. 2015; search summary, verify). Recalling keeps memories (Roediger & Karpicke 2006). Shaping a story for a trusted audience changes one's own memory of it (Echterhoff et al. 2005), and listeners forget what the teller leaves out (Cuc et al. 2007).

### 2.3 Family stories

- **How often.** Memories pass between generations moderately often (52 of 100 as rated by tellers, 49 by receivers), far more between parent and child than grandparent and grandchild (by about 16 points), personal more than public (Baudet et al., preprint, 351 adults). At family dinners a story about the past came up about every five minutes, mothers telling more, and more warmly (Bohanek et al. 2009 and Merrill et al. 2015, as cited by Baudet; verify).
- **What children keep.** Asked for ten important events in one parent's life, 29 of 30 children of war-affected parents named a conflict event, only 19% of them lived through themselves; both groups' lists peaked at the parent's young adulthood; about 90% of events had been discussed at least rarely, 42-47% often (Svob & Brown 2012). Children carry what the parent *told*, mostly transitions in the parent's youth.
- **Told memories feel like memories,** more weakly (vicarious memories: Pillemer et al. 2015, verify). Most twins claimed at least one memory as both their own, rated more vivid than undisputed ones (Sheen et al. 2001). Hirsch's "postmemory" (2008, literary theory): children of survivors hold the parents' trauma as if it were their own.
- **Knowing family stories** goes with adolescents' well-being and identity (Duke, Lazarus & Fivush 2008; Fivush, Duke & Bohanek; correlational, small samples).
- **Silence passes a mood without a story:** a "conspiracy of silence" around survivors (Danieli) and "knowing-not knowing" in their children (Wiseman & Barber 2002, 2008); search summaries, verify.
- **Stories bend toward kin:** in 40 German families, grandchildren recast grandparents who were perpetrators or bystanders as heroes or victims (Welzer et al. 2002; "26 of 40" from a review, verify).
- **Lifespan:** everyday "communicative memory" lasts 80-100 years, three to four generations (Assmann; publisher synopsis, verify); Vansina's "floating gap" marks the same edge (recalled; verify).
- **In a town:** remembering together is how a group comes to share a memory, helping and costing at once (Hirst & Echterhoff 2012); in 14 communities of ten people, overlap depended on who talked to whom (Coman et al. 2016). Gossip chains already carry news, so the most-told version should win within a cluster, and families keep a softer version of their own kin's deeds, as rule 17's covering for kin already points.

### 2.4 Across generations: well supported and contested

- **Supported, modest, social.** In 32 samples (4,418 people), well-designed non-clinical samples showed no harm to children from parents' Holocaust experiences; effects appeared only in clinical samples, under other stress (van IJzendoorn et al. 2003). Grandchildren in 13 non-clinical samples showed none (Sagi-Schwartz et al. 2008, per a review; verify).
- **Cycles are real but mostly broken.** About a third of maltreated people maltreat their children, about six times the base rate (Kaufman & Zigler 1987); three times the odds overall (r = .289, OR 2.99, with signs of bias; Assink et al. 2018, 84 studies). Madigan et al. 2025 (30 studies, 76,047 pairs): maintainers 17.1%, breakers 23.6%, initiators 11.4%, neither 47.8%. Of maltreated parents about 42% continue the pattern and 58% break it; of the rest about 19% begin one.
- **Contested: biology.** FKBP5 methylation in 32 survivors and 22 of their children (Yehuda et al. 2016) is criticised for a small, mismatched control group and a design that cannot separate biology from parenting (commentary; verify). Inherited smell fear in mice (Dias & Ressler 2014) was called improbably clean and has no preregistered replication; a 2024 preprint found more smell neurons in offspring but no greater sensitivity (reviews; verify). Transgenerational epigenetic inheritance is shown in plants and worms and disputed in mammals, where genetic, ecological and cultural inheritance can mimic it (Horsthemke 2018). Lehrner & Yehuda (2018) propose three routes: social, in the womb and through the germline.
- **For the game:** nothing passes at birth. "A child inherits traits, not memories" (rule 18) stands; a memory passes only through what the child perceives.

### 2.5 Attachment and values

- A parent's attachment state predicted the infant's (d = 1.06; 75% agreement, κ = .49), but of a total effect of about .47 only about .11 ran through measured sensitivity: the *transmission gap* (van IJzendoorn 1995, 18 samples, N = 854). Later pooled at r = .31 (Verhage et al. 2016, as reported in 2020), and .21 for unresolved loss or trauma (search summary; verify). The interview scores how coherently a parent tells their childhood, not what happened (recalled; verify), and adults who tell a hard childhood coherently parent about as well as others (Roisman et al. 2002). What passes is how the parent *holds* the memory.
- **Values pass more than traits:** mid-parent-child 0.17 for preferences against 0.54 for values (Rozin 1991, section 3.3); "medium" for intergroup attitudes (Degner & Dalege 2013, 131 studies; about .3 recalled, verify); .59 for party identification (Jennings & Niemi 1968; verify), more in politicised homes with consistent cues (Jennings et al. 2009). Those who take up their parents' views readiest are likeliest to drop them later (Dinas 2014). About six in ten keep their parents' religion, warmth mattering most (Bengtson 2013; a reviewer's summary, verify). Children copy behaviour they watch (Bandura 1961; recalled, verify).

### 2.6 Children of drinkers, and Penny

| Finding | Numbers | Source |
|---|---|---|
| first-degree relatives of alcohol-dependent people | lifetime dependence 28.8% against 14.4% | Nurnberger et al. 2004 |
| children of alcoholics | 2-10 times as likely to develop alcoholism; raised risk of many problems, none in all of them, and no support for an "ACOA syndrome"; more expectations of alcohol, undercontrol and neuroticism; parent alcoholism predicted substance use disorders beyond other parental disorders | Sher 1997; Harter 2000; Sher et al. 1991; Chassin et al. 1999 |
| 18,115 Swedish adoptees | AUD in a biological parent OR 1.46, in an adoptive parent 1.40, additive: the home matters about as much as the genes | Kendler et al. 2015 |
| 21 cohort studies, 26,354 families | parental drinking predicted adolescent drinking, OR about 1.4-2.8; four studies supported a causal reading; none found children of heavier drinkers drink less | Rossow et al. 2016 |
| 17,592 students, about 10% with a problem-drinking parent | over-represented at *both* ends, abstinence and heavy episodic drinking; worst for affected mothers; about 23% met proxy abuse criteria | Weitzman & Wechsler 2000 |
| Tecumseh daughters of high-drinking mothers (small numbers) | polarized: about 60% abstemious and 35% high-volume, against 17% of all daughters; aversion mostly toward a problem-drinking parent of the other sex | Harburg et al. 1990 (the pattern re-checked in the abstract; the percentages not) |
| offspring of high-volume drinkers, 17 years on; daughters of heavy-drinking fathers | most drank less than their parents; 21 of 28 daughters married light or non-drinking men, and spouses' drinking correlated (tau-b 0.57) | Gleiberman et al. 1991, 1992 |
| what protects | a reasoned conviction against drinking, even with drinking parents; family rituals kept intact through the heaviest drinking; couples who kept some ties, dropped the drinking ways and built rituals of their own | Keller et al. 2024; Wolin et al. 1979; Bennett et al. 1987 (the record's match uncertain; verify) |

"Passing on" has three routes: **genes** (about half the liability; Verhulst), **the home** (as strong as a biological parent's AUD; Kendler 2015), and **the child's own reaction** to what they see, imitation or aversion (Harburg), carrying the parent's expectations of alcohol (Sher 1991).

**Penny** is a daughter of a mother with a drinking problem: more often than others both abstemious *and* heavy. Low boldness (0.2) means little drinking for enhancement; high sensitivity (0.67, `DefaultTown.cs:225`) is a coping-route risk; shame is the documented route to abstinence. The abstemious majority is the realistic reading, as canon has it; a heavy-drinking Penny is a real minority path, through sadness rather than pleasure. Harburg's aversion ran mostly across the sexes, but daughters of high-drinking mothers polarized rather than simply turning away. The evidence suggests a concern should also stop the holder's own drink, where the spec keeps Penny's 12g drink as the body's rule (For Sid 5): a question below. A child of hers would receive two currents: risk from Pam (an inherited proneness, and an example if the child sees Pam drink) and resistance from Penny (what she tells and visibly does).

### 2.7 The model

Seeded draws (Part 4); a year is 112 days; ages in years; nothing reads a name.

**The record.** The spec's `CoreMemory(Holder, About, Feeling, Over?, Place?, Strength, Sources)` gains fields at the end, with defaults: `Shape` (`Episode`, one event; `Theme`, a long concern), `HeldAs` (`Lived`, `Lesson` told, `Unease` shown, `Opposite` rejected, `Claimed` told but held as lived; saved as integers, append only), `Highest` (the strongest it has been), `AgeAt`, `DescendsFrom` (the parent's memory, so lineages can be measured) and `TimesTold`. `About` must allow null (a taste found alone, Part 3). Strength S is what readers use: the sentiment toward About over Over and the place feeling at Place, held at S rather than a fixed number. At most `CoreCap` 5 a person; a new one replaces the weakest only if its `Highest` is higher.

**Forming one in a life (`Lived`).** Age factor A: 0 under 3; 0.5 from 3 to 6, and fragile; 1 from 7; a positive memory also × 0.6 outside ages 10-30 (the bump). Consequence C: 1 if the holder underwent it or it concerns kin or a close tie (regard at least `CloseTieAt` 0.4); 0.5 if seen firsthand and about anyone else; halved again for hearsay (law 11). Susceptibility K = 0.5 + sensitivity.
- **Episode** (Terr's Type I, flashbulb): at an event felt at |f| ≥ `SevereAt` (0.7, `Feelings.cs:55`), draw once: p = `CoreChance` 0.6 × A × C × K; `Highest` = |f|. A typical adult touched by a severe event involving kin: 0.6; a sensitive one (0.9): 0.84; a typical onlooker to a stranger's: 0.3, near Conway's 86% against 29%.
- **Theme** (Type II, repetition): when a sentiment with an About or Over has been at or above `ConcernAt` (0.3) on `ThemeDays` 56 nights within a year, draw once that year: p = `ThemeChance` 0.25 × A × K; `Highest` = that night's strength. Over ten years of steady concern, the chance of never forming one is 20% at sensitivity 0.1, 6% at 0.5 and 1% at 0.9; a thick-skinned child often never reaches `ConcernAt` at all.

**Holding and fading.** S never falls below the floor, `FloorShare` 0.4 × `Highest`; above it, it fades by a yearly keep Y = 0.80 + 0.15 × retention, less 0.05 × (1 − sensitivity) for a negative memory (fading affect bias): each night S = floor + (S − floor) × Y^(1/112).
- **Fragile** (formed at 3-6): Y 0.7 and no floor until 10; at 10 it gains its floor if S ≥ 0.3, otherwise it is dropped (Bauer & Larkina).
- **Renewal** (rule 18's "changes stick while they keep being triggered"): a renewed sentiment sets S = max(S, sentiment); a severe renewal, or each further year of qualifying concern for a theme, raises `Highest` += 0.1 × (1 − `Highest`), and the floor with it.
- **Telling** to a warm listener (regard at least 0.4) eases a negative memory by `Ease` × (S − floor) and firms a positive one by `Ease` × (`Highest` − S); to a cold one (regard below 0) it stirs a negative one by `Ease` × (`Highest` − S); `Ease` 0.05.
- **Resolution:** a reconciliation with About (law 8) multiplies `Highest`, and so the floor, by (1 − 0.3 × understanding), as the attachment literature's "resolved" telling (verify).
- **Time:** within a run, hold S, as the spec does now. At a skip of n years, S = floor + (S − floor) × Yⁿ in one step, unless the memory would have been renewed throughout (About alive, still doing the Over, living in town with the holder).
- **Penny** (Theme, `Highest` 0.9, floor 0.36, retention 0.5, sensitivity 0.67, Y 0.859): near 0.9 while Pam drinks. If Pam quit, 0.61 after 5 years and 0.48 after 10, so the Saloon's weight for her rises from 0.1 to about 0.5. Reconciled (understanding 0.7), `Highest` 0.71 and floor 0.28. A whole 112-day run with no renewal would cost only 0.9 to 0.82.

**Passing it on: three routes, none at birth,** for a child of 3 or more who lives with the holder:
1. **Told.** Times a year T = `TellBase` 5 × S × expression × (0.5 + chattiness) × Open, with Open 0.5 for shame or a negative feeling about kin, else 1. A typical parent tells a typical core memory about three times a year. Tellings count 0 before 5, 1 at 5-11 and 1.5 at 12-20, when the life story forms; a grandparent's count half (Baudet's gap). In a run, a family chat at home could carry an actual telling.
2. **Shown.** Each year lived with a parent whose memory is active (S ≥ 0.3) and acted on (refusing the drink, avoiding the place, the stay-in ask) counts 0.5: the attachment gap and "knowing-not knowing".
3. **Lived.** The child's own experience forms its own memory as above; if a parent holds a matching core memory (same About or Over), the child's chance × (1 + 0.5 × E): the parent names what the child sees. A told version later lived becomes `Lived` and keeps the higher strength.

**The child's version.** At 14, draw once per parent memory with S ≥ 0.3:

```
told  = weighted tellings so far;   shown = 0.5 × years shown
E     = 1 - exp(-(told + shown) / 6)                 exposure
W     = clamp(0.3 + regard(child → parent), 0.1, 1)  warmth (Bengtson)
lands = 0.85 × E                                     the child knows it at all
if it lands:  Opposite with Rebel = 0.05 + 0.4 × boldness × min(1, told / 10) × (1 - W)
              Same     with W × (1 - Rebel);  nothing otherwise
Same is a Lesson with chance told / (told + shown), else Unease
S_child = S_parent × V × (0.5 + 0.5 × E),  V = 0.5 for a Lesson, 0.3 for Unease or Opposite
```

The child's own Y and the 0.4 floor share apply from then on. At leaving home (or 21), a `Lesson` or `Unease` turns `Opposite` with chance Rebel / 2 (Dinas). With chance 0.2 at each passing, a negative feeling toward kin softens one step (Ashamed, Hurt or Indignant to Wary; Wronged to Hurt), as stories bend toward kin (Welzer).

| Version | What it does |
|---|---|
| `Lesson` | the feeling over the Over, the place feeling at Place, and a feeling toward About if alive, halved if the child's own regard for About is at least 0.4 (Welzer); told on like any core memory |
| `Unease` | the place feeling only, with no About and no reason: "we don't go there" |
| `Opposite` | no avoidance; the family's lean on the Over is cancelled for this child, and trying the indulgence becomes likelier (Part 4) |
| `Claimed` (optional) | among Lessons, a rare chance 0.05 × K that the child holds it as their own, V 0.8 (disputed memories; postmemory) |
| (any, with a Place) | an inherited place feeling is a deliberate exception to the spec's firsthand-only rule: it comes from what the household does and avoids (Wolin's rituals), not from hearing about an event. Sid's to confirm |

**Worked examples.** Penny's child (born after a skip, living with Penny, Pam alive and drinking, regard for Penny 0.6, boldness 0.3): Penny masks (expression 0.25) and it is shame, so she tells it 0.51 times a year; by 14, 5.1 weighted tellings and 11 years shown, E 0.83, W 0.9.

| Child | Lesson | Unease | Opposite | Nothing |
|---|---|---|---|---|
| Penny's, warm (0.6), boldness 0.3 | 29% (S 0.41) | 31% (S 0.25) | 4% (S 0.25) | 36% |
| Penny's, cool (0) and bold (0.6) | 9% | 10% | 10% | 72% |
| an expressive, chatty parent's grateful memory (S 0.8, told 44 times by 14), warm child | 71% | | 6% | 23% |
| the same, cold child (regard −0.1) | 13% | | 18% | 69% |

Either of Penny's children may also form a `Lived` theme of their own from seeing Pam drink, with the framing boost. Unrenewed, with typical tellers and warm homes, Penny's 0.9 becomes a Lesson of 0.41 in her child, about 0.19 in a grandchild and about 0.07 in a great-grandchild, below `KeepAt` 0.1 and lost: three generations of holders, about Assmann's 80-100 years, longer only if drinking recurs and renews it.

**Calibration targets** (to measure, not quotas): severe events touching the holder that become episodes, about 60-85%, and about 30% for onlookers to a stranger's (Conway et al. 1994); take-up of a warmly, often told core memory about 0.6-0.7 (Bengtson 2013); `Opposite` among those reached about 5-10% in warm homes, more in cold ones, and overall below take-up (Bengtson; Dinas); of children who grew up beside a harm, a minority, about 40%, repeating it (Madigan et al. 2025); children of a drinker split both ways (Weitzman & Wechsler 2000); an unrenewed family story gone in 3-4 generations (Assmann; Baudet); siblings sharing exposure but drawing independently, so they differ.

**Gets right:** memories pass only through perception; masking parents pass a mood more than a story; warmth and exposure decide take-up; rejection is possible and likelier from bold, cool, over-told children; stories soften toward kin and fade over generations; experience can renew or replace them. **Missing:** no details of content (Hirst's drift becomes drift of feeling); no PTSD, repression or intrusive memories; no cultural memory (festivals, plaques) to carry a town story past four generations; no spouses or friends taking each other's memories.

## Part 3. Inborn likes and dislikes

### 3.1 How heritable are likes?

- **Food groups, young adults** (Smith et al. 2016: 2,865 twins aged 18-19, 62 foods): vegetables 0.54, fruit 0.49, meat or fish 0.44, dairy 0.44, snacks 0.43, starchy foods 0.32. Shared family environment was **zero for every group**. DZ correlations under half the MZ (vegetables 0.17 against 0.58) point to non-additive effects (a dominance term of 0.15 for fruit).
- **Children:** at 3 (Gemini, n = 2,686), 0.27-0.54 by group, with shared environment mattering for dairy, starches and snacks (Fildes et al. 2014, as reported by Smith); at 4-5, protein 0.78 to desserts 0.20 (Breen et al. 2006; search summary, verify). Food neophobia was 0.78 heritable at 8-11 with no shared environment (Cooke et al. 2007, 5,390 pairs); fussiness was 60% heritable at 16 months and 83% at 3, and fussiness itself peaked at 7 (Nas et al. 2025, 4,804 twins).
- **Single flavours** (Knaapila et al. 2012, 286 pairs, few of them DZ, so "familiality"): cilantro 0.38 by smell and 0.52 by flavour, ethanol liking about 0.20-0.25, sucrose 0.27; cilantro ratings stable (test-retest 0.88).
- **Pastimes:** about half of interest variance genetic (Lykken et al. 1993; search summary, verify); exercise participation 48-71% heritable in 37,051 pairs (Stubbe et al. 2006); musical pleasure about 54% (Bignardi et al. 2025, press release; verify); instrument and genre match more in MZ twins (Mosing & Ullén 2018, conference abstract; verify); genes and shared environment together about half of listening hours (Wesseldijk et al. 2020).
- **So:** likes are moderately heritable, about 0.3-0.55, like personality; the household shapes young children's likes, and that effect is gone by adulthood.

### 3.2 Single genes: real, famous and small

- **TAS2R38:** two haplotypes account for about 85% of PTC tasting (Kim et al. 2003, secondary; verify), with about 30% non-tasters among Europeans (textbook figure; verify). Stronger PROP bitterness went with more ethanol burn and less drinking (Duffy et al. 2004; press figures of 130, 180 and 285 drinks a year by genotype, verify), but a review of 50 studies found inconsistent links to preference and no feasible meta-analysis (Hejazi et al. 2024).
- **Cilantro:** among 14,604 23andMe customers, 13.7% found it soapy and 26.3% of 12,087 disliked it; the lead SNP near OR6A2 had an odds ratio of 0.81 and explained about 0.5% of the variance, all common SNPs 0.087 with an interval including zero (Eriksson et al. 2012). Dislike runs 3-21% by ancestry, lower where it is cooked with (Mauer & El-Sohemy 2012; search summary, verify).
- **Lesson:** the heritable part of a like comes from many small, interacting effects. One Mendelian gene per item would overstate how much likes run in families.

### 3.3 The family paradox, and Stardew

- **Rozin 1991** (118 students and their parents): mid-parent-child correlation **0.17** for preferences against **0.54** for values; children no closer to their mother, or their same-sex parent; parents who agreed with each other raised it only modestly: the "family paradox". DZ twins correlate only 0.08-0.23 on food groups (Smith). Diets resemble more than tastes, because families eat together: 0.20-0.35 parent-child (Wang et al. 2011; Pervin et al. 2023; search summaries, verify).
- **Emergenesis** (Lykken et al. 1992): a trait built from a particular combination of genes is shared by identical twins but "may not run in families". The food MZ/DZ ratios of 2.2-4.3 fit. This is the evidence for Sid's high mutation: most of a child's favourite is new to the family, though partly genetic.
- **Stardew agrees.** Comparing the personal loved and hated lists in `Data/NPCGiftTastes` (1.6.15) for 16 kin pairs against 545 other pairs among 34 villagers: a shared loved item in 12.5% of kin pairs against 14% of others; 1.69 items both love or like against 1.49; one loving what the other hates in one kin pair (6%) against 8% of others. That kin pair is **Pam and Penny**: Pam loves items 303, 346, 459 and 873 and Penny hates all four (from recall Pale Ale, Beer, Mead, Piña Colada; verify), along with, from recall, Wine, Hops, Grape and item 446, Rabbit's Foot, a universal love (verify). That inversion is learned, through Penny's core memory (D52): values pass between generations, preferences barely. Kent's taste lines (paraphrased) give the two discovery stories of 3.5: two dishes loved because his mother gave them to him as a boy, and foods hated from a prison camp.
- **Knowing.** In Stardew a villager always knows their own tastes; the player finds them out by giving (`Farmer.onGiftGiven`) or being told (`%revealtaste`, `Farmer.revealGiftTaste`). A gift moves friendship +80 loved, +45 liked, +20 neutral, −20 disliked, −40 hated (`NPC.cs:4948-4975`). Sid's design goes further: the villager does not know either. Under Glass villagers have no gift tastes yet, though design.md section 5 lists them as authored identity.

### 3.4 Exposure: how a household passes on likes without genes

- Flavours reach babies before birth and in milk, specifically (Mennella et al. 2001: carrot juice).
- Repeated tasting raises liking: eight tastings for red pepper in 49 children, a sticker doing no better (Wardle et al. 2003); familiarity is "perhaps the most important determinant" (Cooke 2007); 8-10 tastings for infants (USDA NESR), 10-15 for older children and adults (a review summary; verify). Mere exposure across 208 experiments, r ≈ 0.26 (Bornstein 1989), perhaps rising then falling (Montoya et al. 2017); secondary, verify.
- Children aged 2-5 ate more of a new food when an adult beside them ate a food of the same colour (Addessi et al. 2005).
- Neophobia goes with trait anxiety and against experience seeking (Pliner & Hobden 1992); its sensitive period runs from about 18 months to 6 years (Cashdan 1994, cited in a review; verify).
- Acquired tastes: chili lovers still feel the burn but come to like it (Rozin & Schiller 1980; secondary, verify). Bitter things like beer and coffee are usually disliked at first: love at first taste is unrealistic for them.
- Stability: nursery choices at 2-3 predicted preferences at 4-22 for most groups (Nicklaus et al. 2004); snack likes were modestly stable from 5 to 11, and early dislikes tended to warm (Rollins et al. 2010).

### 3.5 Finding out: discovery on first experience

- Some reactions are inborn: newborns relax at sweet and gape at bitter (Steiner; secondary, verify).
- One bad experience makes a lasting aversion: in 696 people, stomach illness was linked to 87% of aversions, one pairing was enough, aversions lasted years, and new ones peaked at 6-12 (30%), falling to 6% after 60 (Garb & Stunkard 1974; search summary, the paper could not be opened, verify). 37.5-84% of people report one in their life (a review; verify). Children on chemotherapy avoided a new ice cream eaten before treatment (Bernstein 1978).
- One good experience can set a direction: the "crystallizing experience" (Walters & Gardner 1986; secondary only, verify). Some people find nothing: specific musical anhedonia (Mas-Herrero et al. 2014), 1-5% (press; verify).
- For indulgences the first response predicts the habit: more reward and less sedation in young drinkers predicted escalation to bingeing (King et al. 2011); feeling relaxed after a first cigarette predicted dependence (DiFranza et al. 2007, press; verify).
- Early discoveries are the remembered ones: smell-cued memories date mostly from the first decade of life (Willander & Larsson 2006). People cannot predict their own: "I don't like it; I never tried it" (Birch & Marlin 1982; title only, verify).

### 3.6 The model

**Names.** "Love" and "hate" are regard's (`LoveAt`, `LoveShare`, `HateCap`, `Feelings.cs:40-42`), so **`Favourite`** and **`Aversion`**, record `InnateTaste`; none appears in `sim/` today. Two sources of strong likes stay apart: **innate**, the hidden pair drawn at birth (this model), and **learned**, from experience (Penny's beer through shame, Kent's prison food), which passes only socially (Part 2).

**The pool** (town data, never a person): `TownData.Tastes`, a list of `Taste(Name, Kind, FavouriteWeight, AversionWeight, Tries = 1, MinAge = 0)`, `Kind` being `Good`, `Pastime` or `Place`. A person is never a taste (that is regard); homes and workplaces never are. Sweet, fatty and sociable things lean favourite; bitter, alcoholic, sour and crowded ones lean aversion. Stardew's universal lists are a guide; a personal draw can go against them, as Penny's Rabbit's Foot does. All weights are guesses to tune.

| Taste | Kind | Favourite | Aversion | Tries | MinAge |
|---|---|---|---|---|---|
| sugar (sweets, baking) / bread / cola / rice | Good | 3 / 2 / 2 / 1 | 0.5 / 0.5 / 1 / 1 | 1 | 0 |
| vinegar (sour, pickles) | Good | 0.5 | 2 | 1 | 0 |
| coffee / beer | Good | 1 / 1 | 2 / 2 | 3 (acquired) | 13 / 18 |
| a game (`PlayedGame`) | Pastime | 2 | 1 | 1 | 5 |
| Beach / ForestPath | Place | 2 / 1.5 | 1 / 1 | 1 | 0 |
| Saloon / Square (crowds) | Place | 1 / 1 | 1.5 / 1.5 | 1 | 18 / 0 |

**What each person carries:** `InnateTaste(FavA, FavB, FavShown, AvA, AvB, AvShown)` plus `FavFound`, `AvFound`, `FavTries`, `AvTries`: two hidden "alleles" per side, one shown (inheritance-research's Model 2, one locus per side), as private state. No rule reads an unfound taste except the discovery hook; before discovery the owner's own choices see only "untried".

**At birth** (parents in name order):
1. From each parent, take one of their two alleles on that side (a coin).
2. With chance **μ = 0.5**, replace it with a fresh draw from the pool by that side's weight: the mutation.
3. One of the child's two alleles shows (a coin, no dominance).
4. If the shown favourite and aversion coincide, show the other aversion allele; if that is the same too, draw fresh until they differ (at most 8 draws).

A toy simulation (a throwaway script, not the sim: a 24-item pool weighted 3, 3, 2, 2, 2, 2, then 1s; 200,000 families of founder grandparents, parents and two children; k is the match above chance, comparable to a correlation):

| μ | Child shows a parent's shown favourite | A grandparent's | A sibling's | Shown item new to both parents |
|---|---|---|---|---|
| 0.3 | 22% (k 0.18) | 11% (k 0.06) | 17% (k 0.12) | 25% |
| **0.5** | **17% (k 0.12)** | **8% (k 0.03)** | **11% (k 0.06)** | **41%** |
| 0.8 | 10% (k 0.05) | 6% (k 0.01) | 6% (k 0.01) | 65% |

Two unrelated people share a favourite 5% of the time. **Why 0.5:** a single parent's k of 0.12 matches Rozin's mid-parent 0.17 (0.17 / √2 ≈ 0.12); two children in three show neither parent's favourite, Sid's "high mutation"; and generations before still show: 15.5% of children show a grandparent's favourite that neither parent shows, against 12.2% for unrelated people, the allele riding hidden in a parent. Penny's child could love what grandmother Pam loves. **Known gap:** siblings (k 0.06) come out less alike than real DZ twins (0.08-0.23); the household's exposure below supplies some of that, and a per-household draw could add more if measurement calls for it. **Founders:** the shown pair from Stardew's lists where the pool has the item (Pam: favourite beer), the hidden alleles drawn, seeded by name; both found (adults have met the town). Penny's innate pair is drawn separately from her learned beer aversion.

**Discovery: firsthand only,** never hearsay or watching (law 11; imagination never becomes evidence). A Good is experienced by consuming it (a purchase the holder uses, the nightly drink, being treated, a gift); a Pastime by taking part, as actor or partner; a Place by 30 minutes or more awake there in free time (not work, an errand or passing through).
- An aversion shows on the first try (inborn rejection; one-trial learning); a favourite on the first, or on try `Tries` (3) for an acquired taste, a compressed 8-15 tastings. `MinAge` gates the try.
- Saying yes to an untried taste on offer (a treat, a gift, a game) is × (0.4 + 0.6 × boldness) (Pliner & Hobden): bold people find their tastes sooner; a shy one may never find theirs, rule 18's niche-picking.
- **Felt:** one mood entry on a new route `Taste`, ±0.5 × (0.75 + 0.5 × sensitivity) × (1.3 under 13, 1.15 at 13-17, 1.0 adult); an optional −0.3 more when an aversion is found on a day the person `Collapsed` or later fell ill (Garb & Stunkard).
- **Known:** the found flag is set and the owner's choices read it: favourite options weigh ×2 (haunts and hubs at the place, the act's pastime weight, the good added to their uses), aversion options ×0.2, never home, work or errands; each later experience ±0.2, a gift's size, constant as Stardew's +80 and −40 are.
- **Others:** anyone who sees the reaction forms a belief ("Abby can't stand vinegar"), trivia that can be retold. A giver reads only their own belief, never the truth; a gift of an unfound favourite triggers discovery with the giver as `With`, a quarter of the extra joy becoming regard for them; an unfound aversion given unknowingly costs the giver nothing, knowingly it is a hostile act.
- **Memory:** a `TasteFound(Who, Taste, Day, Felt, Place, With)` row and a templated, authored diary line ("first tried coffee at the Saloon with Harvey: loved it"). Felt at `SevereAt` or more, it enters Part 2's episode draw (Part 4). On the base amount, children reach 0.7 from sensitivity 0.65, teenagers from 0.94, adults never without the illness pairing: vivid first experiences belong mostly to sensitive children.
- **Skips:** most newborns find their tastes off-screen. An everyday Good or Place is found at a seeded age between `MinAge` and `MinAge` + 6; a Pastime between 5 and 12, or never with chance 0.3 × (1 − boldness). Felt amount, core memory and row use the age at discovery.

**What passes:** the hidden alleles; exposure (a household's uses, haunts and drinks are what its children meet first; a taste never in reach may never be found); and modelling, a nudge of 0.1 toward a parent's shown sign when a child first tries a taste in sight of a parent who has found it (Addessi), kept small because preferences pass at 0.17 (Rozin). Never a parent's memory.

**Numbers to tune:** μ 0.5 (Rozin's 0.17; emergenesis; Sid's "high mutation"); the pool weights; tries 3 for an acquired favourite and 1 for an aversion (8-15 tastings compressed; one-trial aversion); the yes to an untried taste × (0.4 + 0.6 × boldness) (Pliner & Hobden); the felt amount's 0.5 (Stardew's love is 4× a neutral gift) and age weights (childhood peaks); the illness pairing −0.3 (Garb & Stunkard); later experiences ±0.2 (a gift's size); the pull once found ×2 and ×0.2 (the place floor 0.1 as a guide); joy to the giver 0.25 (Spinoza's love as joy with a cause); the modelling nudge 0.1 (Addessi; Rozin); the skip window `MinAge` to `MinAge` + 6.

Measure over 5-10 simulated years: kin match rates for found tastes against the table (parent 17%, sibling 11%, unrelated 5%); the share who never find their favourite (small, mostly shy); pool frequencies staying near their weights. Tests: the same seed gives the same tastes; a favourite never equals the aversion; no rule reads an unfound taste; discovery only firsthand; over many seeds a child shows each parent's favourite about (1 − μ) / 4 above chance; no rule names a person.

## 4. How the three fit together, and with trait inheritance

### 4.1 What a child gets

| What | At birth | Passed on by | Comes to light | Model |
|---|---|---|---|---|
| seven traits | a genetic value g per trait, plus d and e | mid-parent + segregation + rare mutation | as character, plastic all life, hardening | [inheritance-research.md](inheritance-research.md), Model 1 |
| proneness to each indulgence | a general part `Gen` (one per person) and a specific part `Spec` per indulgence, each a genetic value like g; p = 1 / (1 + e^−(0.7 Gen + 0.7 Spec)), fixed | the same Model 1 rule | never to its owner: only in how the first doses feel and how fast use becomes dependence | Part 1 |
| a like and a hate | two alleles per side, one shown | one allele from each parent, μ 0.5 fresh | on first experience | Part 3 |
| core memories | none | the home: told, shown, lived | a version by 14, reconsidered on leaving home | Part 2 |

- **Proneness variances** (a proposal mapping Verhulst's 0.49 heritability and 0.10 shared environment onto Model 1): VA 0.35, VD 0.14, a household term 0.10 shared by siblings, 0.41 the child's own, all drawn at birth because p is fixed for life; what life adds is carried by u, a and s. The two 0.7 weights (0.5-0.85) make two indulgences' proneness correlate about 0.5 (Kendler's licit and illicit factors correlate 0.82, with large specific parts). Founders back-fill `Gen` and `Spec` from their authored p as traits back-fill g. Mutation: the same chance as traits.
- **Expression**, the seventh trait, has no row in Model 1; use chattiness's variances until it is researched (a guess).

### 4.2 Where they touch

1. **Taste and proneness.** Beer is in the taste pool and the drinking indulgence's goods. Proposed: if an indulgence's good is the shown favourite, its specific part `Spec` gains `TasteProne` 1.0; if it is the shown aversion, `Spec` loses 1.5 and the first dose is aversive (one ALDH2\*2 allele protects and a second adds nothing, so an aversion allele on an indulgence's good could show whenever present: Luczak et al. 2006). Then the like is how the first drinks feel (Schuckit; King) and proneness is what that feeling does over months, and the two hidden draws never contradict. The alternative keeps indulgence goods out of the pool and lets `Spec` alone colour the first doses.
2. **Memory and addiction.** A concern, whether `Lived` or a `Lesson`, keeps the holder from taking a dose (if Sid agrees, 2.6): a concerned child never learns their taste for drink nor builds a, Harburg's abstemious majority, and Sid's "they will only find out if they experience it". An `Opposite` version lifts the family's avoidance and multiplies the yes to an untried indulgence by (1 + its strength); the child's own p then decides, Weitzman's heavy end. A dependent parent feeds both sides at once: inherited p and an example on one, a concern from seeing it on the other. The two-way split of children of drinkers is not drawn; it emerges, and is measured.
3. **Taste and memory.** A discovery felt at `SevereAt` or more enters the episode draw with C 1 (the holder underwent it), About the `With` (or null) and Over the taste. Childhood favourites skip the reminiscence-bump factor: smell-cued memories cluster in the first decade (Willander & Larsson), and Kent's mother's dishes are such a memory.
4. **Addiction and memory, the other way.** Dependence's deeds are what the household sees: the nightly drink, a second drink, beer at home. The more Pam drinks at home, the more often Penny's concern is renewed, so her memory holds its strength and a child of hers is shown more.

### 4.3 One life, in order

| Age | What happens |
|---|---|
| 0 | g, d and e for seven traits; `Gen`, and `Spec` per indulgence; taste alleles; no memories |
| 3-6 | fragile memories (A 0.5), which at 10 gain their floor or are dropped; everyday goods and places found in the neophobia window |
| 5-12 | tellings count ×1; pastimes found, or never for a shy child |
| 12-20 | tellings count ×1.5; the bump for positive memories; coffee from 13 |
| 14 | first checkpoint: each parent memory lands as a Lesson, Unease, Opposite or nothing |
| 18 | beer and the Saloon open; first doses reveal the taste; u, a and s begin |
| leaving home, or 21 | second checkpoint: a Lesson or Unease may turn Opposite |
| adult | traits harden; memories fade above their floors unless renewed; dependence follows routine |

**Two clocks.** Bodily processes (bars, doses, adaptation, withdrawal) count days, as the bars do, and the addiction constants are set on real days (dependence in weeks, relapse within about 100 days). Life-course processes (ages, memory fading, discovery windows) count 112-day town years. The scar sits between: 0.998 a day is a half-life of about a real year, about three town years. At a skip of n years: memories use the closed form unless renewed; tastes are found at seeded ages; a is set straight to its target p × u from the household's routine (it gets there within months), and s to the larger of its decayed value and `ScarShare` × a.

### 4.4 Seeding and rules kept

- **Draw keys:** trait values by `Fnv1a` of (child, trait), as inheritance-research; proneness `Rng.Unit(seed, "prone", child, indulgence or "general", part)`; taste alleles `Rng.Unit(seed, "taste", child, "favourite" or "aversion", step)`; forming a core memory `Rng.Unit(seed, "core", holder, memory key, checkpoint)`; a child's version `Rng.Unit(seed, "version", child, parent's memory key, "14" or "leave")`; a skipped discovery's age `Rng.Unit(seed, "found", child, taste)`.
- People and parents in name order; memories by (day formed, About, Over). The daily addiction rule draws nothing.
- No rule reads a name. Pam's dependence and Penny's memory are starting data with sources, like the starting tensions.
- Only the owner knows p, a, s, their tastes and their memories; others see deeds and form beliefs.
- Bars stay inside amended law 1: drain, fill and a capped pull; no withdrawal mood.
- Nothing passes at birth but hidden values; memories pass only through what a child perceives.

**Measured together** over 5-10 simulated years: parent-child resemblance of about 0.10-0.20 for traits, 17% for found favourites (5% unrelated), 0.6-0.7 take-up for warmly told memories; children of a dependent parent at about twice the rate of a′ > 0 *and* more often abstainers (Nurnberger et al. 2004; Weitzman & Wechsler 2000; Harburg et al. 1990); family stories gone in 3-4 generations unless renewed; the distribution of p and the pool's frequencies stable across generations.

## 5. Questions for Sid

1. **Drain:** your ×2 (`AddictedDrain` 2: Pam bored about 32 hours after her last drink) or the recommended 2.5 (about 25 hours, pressing by the next evening)?
2. **Tolerance moves money:** Pam's second drink costs the Trailer about 1,330g in 112 days. Stage B behind its own switch, after Stage A?
3. **Drinking at home:** the spec's addiction row counts a beer at home as a dose. Realistic for a dependent drinker, but Penny then sees far more drinks, her concern grows and her asks work less. Confirm?
4. **Penny's own drink:** the research suggests a concern stops the holder's own dose; the spec keeps her 12g drink as the body's rule (For Sid 5). Change it?
5. **Withdrawal's low mood** stays out under law 1. Agreed?
6. **Inherited place feelings:** a child's Lesson or Unease holds a place feeling it never felt firsthand. Accept the exception?
7. **Tastes:** goods only, or places and pastimes too? Indulgence goods in the pool and tied to proneness (4.2), or kept out?
8. **Penny's innate pair:** drawn apart from her learned beer aversion (recommended), or beer too? And should an adult ever discover a taste during a run?
9. **The scar's clock:** a half-life of about a real year (`ScarKeep` 0.998, about three town years) or one town year (0.994)?

## Sources

[full] full text; [abstract]; [page] web page; [summary] search summary or secondary report. "Re-checked": abstract re-read for this document.

**Part 1: addiction**
- Accounts: Koob 2008, Neuron [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC2748830/; Koob & Le Moal 2001 [abstract] https://pubmed.ncbi.nlm.nih.gov/11120394/; Solomon & Corbit 1974, Solomon 1980 [not read; via Koob 2008] https://pubmed.ncbi.nlm.nih.gov/4817611/; Robinson & Berridge 1993 [summary] https://web.as.uky.edu/biology/faculty/cooper/Bio380-Neurophysiology%20seminar/Robinson-Berridge-1993.pdf and 2025 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC11773642/
- Habit: Everitt & Robbins 2005 [abstract] https://pubmed.ncbi.nlm.nih.gov/16251991/; Hogarth 2020 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC7265389/; Lally et al. 2010 [summary] https://www.thebehavioralscientist.com/articles/how-long-to-form-a-habit; Bradizza, Lisman & Payne 1995 [abstract] https://pubmed.ncbi.nlm.nih.gov/7485814/
- Tolerance: Bird, Holloway & Carney 1985 [abstract] https://pubmed.ncbi.nlm.nih.gov/3936099/; Bird & Holloway 1989 [abstract] https://pubmed.ncbi.nlm.nih.gov/2496425/; Pohorecky & Roberts 1992 [abstract] https://pubmed.ncbi.nlm.nih.gov/1513866/; Maier & Pohorecky 1987 [abstract] https://pubmed.ncbi.nlm.nih.gov/3628538/; Siegel et al. 1982 [abstract] https://pubmed.ncbi.nlm.nih.gov/7200260/; Jones 2010 [abstract] https://pubmed.ncbi.nlm.nih.gov/20304569/; Mellanby review 2017 [summary] https://pubmed.ncbi.nlm.nih.gov/28277803/; Ahmed & Koob 1998 [abstract] https://doi.org/10.1126/science.282.5387.298
- Pleasure and relief: King et al. 2014 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC4280017/; King et al. 2021 [abstract] https://doi.org/10.1176/appi.ajp.2020.20030247 and coverage [summary] https://neurosciencenews.com/aud-pleasure-risk-17533/; Grodin et al. 2019 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC7306183/; Grodin et al. 2024 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC11082523/
- Withdrawal and craving: Muncie et al. 2013 [page] https://www.aafp.org/pubs/afp/issues/2013/1101/p589.html; AAFP 2004 [page] https://www.aafp.org/afp/2004/0315/p1500; Heilig et al. 2010 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC3268458/; Li et al. 2015 [abstract] https://pubmed.ncbi.nlm.nih.gov/24698092/; Treloar Padovano & Miranda 2021 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC7855265/; Hisler et al. 2022 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC9246905/; Mayhugh et al. 2018 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC5906009/; Doering et al. 2023 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC10683743/; Brown et al. 2026 [abstract] https://doi.org/10.1177/00332941261436752
- Relapse and recovery: Hunt et al. 1971 [summary] https://pubmed.ncbi.nlm.nih.gov/5115648/; Kirshenbaum et al. 2009 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC3151675/; Moos & Moos 2006 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC1976118/; Dawson et al. 2006 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC6527249/
- Amounts and places: COMBINE via the Cochrane acamprosate review [summary] https://cochranelibrary.com/cdsr/doi/10.1002/14651858.CD004332.pub2/references; Cook 2007 [summary] https://www.drvino.com/2014/10/03/americas-skewed-drinking/; Hardie et al. 2023 [page] https://eprints.whiterose.ac.uk/193338/; Grant et al. 2015 [abstract, re-checked] https://pubmed.ncbi.nlm.nih.gov/26039070/
- Proneness: Verhulst, Neale & Kendler 2015 [abstract, re-checked] https://pmc.ncbi.nlm.nih.gov/articles/PMC4345133/; Krueger et al. 2002 [abstract] https://pubmed.ncbi.nlm.nih.gov/12150417/; Nurnberger et al. 2004 [abstract] https://pubmed.ncbi.nlm.nih.gov/15583116/; Kendler, Myers & Prescott 2007 [abstract] https://pubmed.ncbi.nlm.nih.gov/17984400/; Walters et al. 2018 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC6430207/; Luczak, Glatt & Wall 2006 [abstract, re-checked] https://pubmed.ncbi.nlm.nih.gov/16822169/; Schuckit 1994 [abstract, re-checked] https://pubmed.ncbi.nlm.nih.gov/8296886/
- Temperament and motives: Hakulinen et al. 2015 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC4447572/; Stautz & Cooper 2013 [abstract] https://pubmed.ncbi.nlm.nih.gov/23563081/; Malouff et al. 2007 [abstract] https://pubmed.ncbi.nlm.nih.gov/18047183/; Kotov et al. 2010 [abstract] https://pubmed.ncbi.nlm.nih.gov/20804236/; Kuntsche et al. 2005 [abstract] https://pubmed.ncbi.nlm.nih.gov/16095785/; Cooper et al. 1995 [abstract] https://pubmed.ncbi.nlm.nih.gov/7473043/; Nolen-Hoeksema & Harrell 2002 [summary] https://news.umich.edu/women-think-men-drink

**Part 2: memories**
- Standing out: McGaugh 2004 [abstract] https://personal.utdallas.edu/~tres/memory/emotional/mcgaugh.pdf; Talarico & Rubin 2003 [abstract] https://scholars.duke.edu/publication/651206; Conway et al. 1994 [abstract] https://profiles.wustl.edu/en/publications/the-formation-of-flashbulb-memories/; Singer & Salovey 1993 [summary] https://frontiersin.org/articles/10.3389/fpsyg.2023.1266392/full; Conway & Pleydell-Pearce 2000 [summary] https://pmc.ncbi.nlm.nih.gov/articles/PMC5744072; Terr 1991 [abstract] https://www.qxmd.com/r/1824611; Brewin, Andrews & Valentine 2000 [summary] https://mentalhealth.bmj.com/content/4/2/61; Felitti et al. 1998 [abstract] https://augeo.nl/~/media/Files/Bibliotheek/ACE-Study-1998
- Age: Bauer & Larkina 2014 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC4025992; Habermas & Bluck 2000 [abstract] https://nutrition-evidence.com/article/22364/getting-a-life-the-emergence-of-the-life-story-in-adolescence; Rubin, Rahhal & Poon 1998 [abstract] https://scholars.duke.edu/publication/651321; Berntsen & Rubin 2002 [abstract] https://scholars.duke.edu/publication/707182; Krosnick & Alwin 1989 [abstract] https://pure.psu.edu/en/publications/aging-and-susceptibility-to-attitude-change/
- Fading and telling: Hirst et al. 2015 [abstract] https://scan.psych.columbia.edu/papers/Hirst_et_al_2015.pdf; Walker, Skowronski & Thompson 2003 [page] https://www.apa.org/news/press/releases/2003/06/happy-memory; Ritchie et al. 2006 [abstract] https://eprints.soton.ac.uk/44830; Skowronski et al. 2004 and Muir, Brown & Madill 2015 [summary] https://researchspace.bathspa.ac.uk/13168/; Roediger & Karpicke 2006 [abstract] https://psychology.ecu.edu/wp-content/pv-uploads/sites/216/2019/03/Roediger-Karpicke-2006.pdf; Echterhoff, Higgins & Groll 2005 [abstract] https://www.uni-muenster.de/imperia/md/content/psyifp/aeechterhoff/wintersemester2011-12/seminarsozialekognitionundattribution/echterhoffhigginsgroll_jpsp2005.pdf; Cuc, Koppel & Hirst 2007 [abstract] https://researchportal.port.ac.uk/en/publications/silence-is-not-golden-a-case-for-socially-shared-retrieval-induce/
- Family stories: Baudet et al. (preprint) [full] https://orbi.uliege.be/bitstream/2268/330521/1/Baudet_Cordonnier_Luminet_Bastin_Frequency%20of%20family%20memory%20transmission.pdf; Merrill & Fivush 2016 [abstract] https://humanrightscolumbia.org/memory-studies-portal/tags/narrative-identity; Svob & Brown 2012 [full] https://sites.ualberta.ca/~nrbrown/pubs/SvobBrown2012.pdf; Pillemer et al. 2015 [summary] https://lifestorylab.psych.ufl.edu/wp-content/uploads/sites/84/Vicarious-memory.pdf; Sheen, Kemp & Rubin 2001 [abstract] https://researchportal.hw.ac.uk/en/publications/twins-dispute-memory-ownership-a-new-false-memory-phenomenon/; Hirsch 2008 [summary] https://blogs.brighton.ac.uk/cappe/files/2024/03/PT029-01-05HirschFpp-5b40ed932d1a072f.pdf; Duke, Lazarus & Fivush 2008 [page] https://www.psychologytoday.com/us/blog/the-stories-of-our-lives/201611/the-do-you-know-20-questions-about-family-stories; Fivush, Duke & Bohanek [abstract] https://ncph.org/wp-content/uploads/2013/12/The-power-of-family-history-in-adolescent-identity.pdf
- Silence, kin, lifespan: Danieli [summary] https://sk.sagepub.com/ency/edvol/embed/trauma/chpt/conspiracy-silence; Wiseman, Barber et al. 2002 [summary] https://scholarlyworks.adelphi.edu/esploro/outputs/journalArticle/Parental-Communication-Of-Holocaust-Experiences-And/991004223565106266; Welzer, Moller & Tschugnall 2002 [summary] https://perpetratorstudies.sites.uu.nl/bibliography/welzer-harald-et-al-opa-war-kein-nazi-nationalsozialismus-und-holocaust-im-familiengedachtnis-20190826/ and https://shop.jungle.world/print/pdf/node/13671; Assmann [page] https://www.cambridge.org/core/product/identifier/9780511996306/type/book; Hirst & Echterhoff 2012 [abstract] https://nsuworks.nova.edu/cps_facarticles/583; Coman et al. 2016 [abstract] https://collaborate.princeton.edu/en/publications/mnemonic-convergence-in-social-networks-the-emergent-properties-o/
- Across generations: van IJzendoorn, Bakermans-Kranenburg & Sagi-Schwartz 2003 [abstract] https://openaccess.leidenuniv.nl/handle/1887/2352; tertiary traumatisation review [summary] https://psychotraumanet.org/en/node/3813; Kaufman & Zigler 1987 [abstract] https://www.ojp.gov/ncjrs/virtual-library/abstracts/do-abused-children-become-abusive-parents; Assink et al. 2018 [abstract, re-checked] https://pubmed.ncbi.nlm.nih.gov/30086419/; Madigan et al. 2025 [abstract and results, re-checked] https://www.cambridge.org/core/journals/development-and-psychopathology/article/abs/patterns-of-continuity-and-discontinuity-of-childhood-maltreatment-across-generations-a-metaanalysis/F4E2965748FFDC7334028D48436F584F; Yehuda et al. 2016 [summary] https://www.psypost.org/traumas-epigenetic-fingerprint-observed-in-children-of-holocaust-survivors/; Dias & Ressler 2014 and later work [summary] https://discovermagazine.com/mind/inherited-memories-too-good-to-be-true and https://elifesciences.org/reviewed-preprints/92882v2/reviews; Horsthemke 2018 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC6065375; Lehrner & Yehuda 2018 [abstract] https://resolve.cambridge.org/core/journals/development-and-psychopathology/article/abs/cultural-trauma-and-epigenetic-inheritance/8C1FC1DCFF459B4B07F574386627F9DD
- Attachment and values: van IJzendoorn 1995 [full] https://scholarlypublications.universiteitleiden.nl/access/item%3A2721612/download; Verhage et al. 2020 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC7324077; Roisman et al. 2002 [abstract] https://www.boingboing.org.uk/wp-content/uploads/2010/10/roiseman1.pdf; Degner & Dalege 2013 [abstract] https://dare.uva.nl/id/b7f4b30c-7d7a-4578-a4dd-95e4268221df; Jennings & Niemi 1968 [summary] https://ideas.repec.org/a/cup/apsrev/v62y1968i01p169-184_11.html; Jennings, Stoker & Bowers 2009 [abstract] https://www.icpsr.umich.edu/web/ICPSR/studies/4024/export; Dinas 2014 [abstract] https://ideas.repec.org/a/cup/bjposi/v44y2014i04p827-852_00.html; Bengtson 2013 [summary] https://www.historynewsnetwork.org/article/153855 and https://christiancentury.org/node/19146
- Children of drinkers: Sher 1997 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC6826809/; Rossow et al. 2016 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC4832292/; Harter 2000 [abstract] https://pubmed.ncbi.nlm.nih.gov/10779897/; Kendler et al. 2015 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC4351126/; Weitzman & Wechsler 2000 [abstract] https://pubmed.ncbi.nlm.nih.gov/10749279/; Harburg et al. 1990 [abstract, re-checked] https://pubmed.ncbi.nlm.nih.gov/2342364/; Gleiberman et al. 1991 [abstract] https://pubmed.ncbi.nlm.nih.gov/1917248/ and 1992 [abstract] https://pubmed.ncbi.nlm.nih.gov/1590731/; Sher et al. 1991 [abstract] https://pubmed.ncbi.nlm.nih.gov/1757657/; Chassin et al. 1999 [abstract] https://pubmed.ncbi.nlm.nih.gov/10066997/; Keller et al. 2024 [abstract] https://scholars.uky.edu/en/publications/motivation-to-abstain-from-drinking-as-a-moderator-of-association/; Wolin, Bennett & Noonan 1979 [abstract] https://ajp.psychiatryonline.org/doi/10.1176/ajp.1979.136.4b.589; Bennett et al. 1987 [abstract] https://pubmed.ncbi.nlm.nih.gov/3569499

**Part 3: likes**
- Heritability: Breen, Plomin & Wardle 2006 [summary] https://pubmed.ncbi.nlm.nih.gov/16750228/; Smith et al. 2016 [full; reports Fildes et al. 2014] https://europepmc.org/article/PMC/PMC4962164; Cooke, Haworth & Wardle 2007 [abstract] https://wrap.warwick.ac.uk/65915; Nas et al. 2025 [abstract] https://europepmc.org/articles/PMC11754699; Knaapila et al. 2012 [full] https://pmc.ncbi.nlm.nih.gov/articles/PMC3589946; Lykken et al. 1993 [summary] https://gwern.net/doc/genetics/heritable/1993-lykken.pdf; Stubbe et al. 2006 [abstract] https://doi.org/10.1371/journal.pone.0000022; Bignardi et al. 2025 [page, press release] https://www.aesthetics.mpg.de/en/newsroom/news/news-article/article/is-musical-enjoyment-in-our-genes-1.html; Mosing & Ullén 2018 [conference abstract] https://programme.exordo.com/bga18/delegates/presentation/111/; Wesseldijk, Ullén & Mosing 2020 [abstract] https://gwern.net/doc/genetics/heritable/correlation/2020-wesseldijk.pdf
- Genes: Kim et al. 2003 [summary] https://en.wikipedia.org/wiki/TAS2R38; Duffy et al. 2004 [abstract] https://pubmed.ncbi.nlm.nih.gov/15547448/; Hejazi et al. 2024 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC10835975/; Eriksson et al. 2012 [full] https://arxiv.org/abs/1209.2096; Mauer & El-Sohemy 2012 [summary] https://flavourjournal.biomedcentral.com/articles/10.1186/2044-7248-1-8
- Families: Rozin 1991 [abstract, re-checked] https://agris.fao.org/search/ar/records/65de42b87c7033e84bea5ae4; Wang et al. 2011 [summary] https://jech.bmj.com/content/65/2/177; Pervin et al. 2023 [summary] https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10251076/; Lykken et al. 1992 (emergenesis) [summary] https://en.wikipedia.org/wiki/Emergenesis
- Exposure: Mennella, Jagnow & Beauchamp 2001 [abstract] https://pmc.ncbi.nlm.nih.gov/articles/PMC1351272/; Wardle et al. 2003 [abstract] https://www.nature.com/articles/1601541; Cooke 2007 [abstract] https://pubmed.ncbi.nlm.nih.gov/17635306/; USDA NESR [page] https://nesr.usda.gov/what-relationship-between-repeated-exposure-timing-quantity-and-frequency-foods-and-early-food; Bornstein 1989 and Montoya et al. 2017 [summary] https://atticusli.com/replication-crisis/mere-exposure-effect/; Addessi et al. 2005 [abstract] https://agris.fao.org/search/en/records/65df770b0f3e94b9e5d8ae5d; Pliner & Hobden 1992 [abstract] https://scholar.uwindsor.ca/psychologypub/39; Dovey et al. 2008 (a review of food neophobia and picky eating) [abstract] https://pubmed.ncbi.nlm.nih.gov/17997196/; Rozin & Schiller 1980 [summary] https://www.scientificamerican.com/blog/thoughtful-animal/on-capsaicin-why-do-we-love-to-eat-hot-peppers/; Nicklaus et al. 2004 [abstract] https://agris.fao.org/search/en/records/65de37270f3e94b9e5cbf051; Rollins, Loken & Birch 2010 [abstract] https://pure.psu.edu/en/publications/stability-and-change-in-snack-food-likes-and-dislikes-from-5-to-1/
- Discovery: Steiner [summary] https://www.universalis.fr/encyclopedie/gustation/5-les-correlats-comportementaux-de-la-sensation-gustative/; Garb & Stunkard 1974 [summary; could not open] https://ajp.psychiatryonline.org/doi/10.1176/ajp.131.11.1204; lifetime aversion review [summary] https://revistes.ub.edu/index.php/Anuario-psicologia/article/download/8888/11214/14223; Bernstein 1978 [page] https://www.ssib.org/web/classic26.php; Walters & Gardner 1986 [summary] https://wicri-demo.istex.fr/Wicri/Musique/explor/DebussyV1/Site/fr/Main/Exploration/bibRecord.php?hk=000B03; Mas-Herrero et al. 2014 [abstract] https://diposit.ub.edu/dspace/handle/2445/181297; King et al. 2011 [page, press] https://www.uchicagomedicine.org/forefront/news/2011/april/stronger-alcohol-buzz-predicts-future-binge-drinking-problems; DiFranza et al. 2007 [summary, press] https://www.healthday.com/health-news/general-health/first-puff-can-turn-kids-into-smokers-study-608677.html; Willander & Larsson 2006 [abstract] https://pubmed.ncbi.nlm.nih.gov/16892988/; Birch & Marlin 1982 [title only] https://www.cambridge.org/core/journals/proceedings-of-the-nutrition-society/article/development-of-food-choice-during-infancy/3C8F2EB2FBEAB407110A9F1A54FA6B28
- Stardew Valley 1.6.15: `NPC.cs` (`getGiftTasteForThisItem`, gift friendship values), `Farmer.cs` (`revealGiftTaste`, `onGiftGiven`), `Utility.cs` (`%revealtaste`) in the local decompile; `Data/NPCGiftTastes` from `out/stardew-dialogue-2026-10-12/raw-assets.json`.
