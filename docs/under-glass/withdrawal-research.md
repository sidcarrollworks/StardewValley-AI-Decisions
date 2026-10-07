# Hermits, brawlers and moods that spread: research for Under Glass (0d.6)

Scope: Sid's request of 2026-10-07 (design 11e; section 12, question 7). In his words: "Very negative people should bring down other people just like good people bring others up. Maybe we can find some other research on how hermits and extreme personalities are born to help guide us. Shyness plus being left out should push to a hermit. Shy plus repeated negative events. It really depends on the person."

Where the town stands (phase 0d, `sim/README.md`):
- It makes about 3.5 brawlers a seed-year and no hermits.
- Company always lifts mood a little, whatever the other person's mood (`Company`, `CompanyJoy` 0.005 a chat x (0.5 + chattiness)). It turns into a small sadness only when the holder dislikes the other, at -0.2 or below.
- Stance moves only on hurts that would stir a motive: an argument from someone outside the household, being robbed as a keeper, or being named while innocent (`StirFrom`, `StirAccused`). `StirFrom` returns before `Hurt` for kin and housemates, so a row at home never moves anyone's stance. Nor do acts the person undergoes as actor (warned, taken in, a family row), because their feeling rows have `Patient.Actor`.
- Every kindness received removes the share `felt` of the stance (`StanceAfterKindness`).
- So the shy are rarely hurt where it counts, and their stance is pulled back to 0 almost at once. At boldness 0.02, Penny's stance ends the year at -0.01, and her hours out of home do not change.

**How the sources were read.** Five research passes and a fact-check, on 2026-10-07. The egress proxy blocked every full-text host, so every number below comes from an abstract, a publisher or university summary, or press coverage. Where the fact-check corrected a report, the correction is used. "Verify" marks anything the fact-check did not confirm, anything confirmed only at press level, and anything recalled. Every per-event or per-day constant in sections 2-6 is a game choice, not a value from a paper; no paper gives event counts or per-day constants for these effects.

## 1. What the research says

### 1a. Moods do spread, but less than the famous numbers say

- **Face to face, over minutes.** Barsade (2002, *Administrative Science Quarterly* 47:644) placed an actor in small work groups to play one of four moods. The others' moods moved toward the actor's, both in their own reports and in coders' ratings. Bad moods did **not** spread more than good ones (verify: not checked by the fact-check). Hatfield, Cacioppo and Rapson (1993) describe the mechanism as mimicry, then feedback, then a shared feeling.
- **Large networks.** Fowler and Christakis (2008, *BMJ* 337:a2338; N = 4,739; confirmed):
  - a happy friend within a mile raises your chance of being happy by 25%;
  - a coresident spouse by 8%; a sibling within a mile by 14%; a next-door neighbour by 34%;
  - coworkers and friends further away, not at all;
  - by degree of separation, about **15%, 10% and 6%** (the fact-check corrected a report that used 25% for degree 1).
  
  Loneliness (Cacioppo, Fowler and Christakis 2009, *JPSP* 97:977; confirmed at press level): a lonely direct tie makes you 52% more likely to be lonely, 25% at two degrees and 15% at three. It spreads more between friends than family, and lonely people drift to the edge of the network and lose ties. Depression (Rosenquist et al. 2011, *Molecular Psychiatry*; confirmed in the authors' summary): 93%, 43% and 37% at one, two and three degrees.
- **The criticism is serious.** Cohen-Cole and Fletcher (2008, *BMJ*) used the same method to "find" contagion of acne, headaches and height, and these disappeared once shared surroundings were controlled. Shalizi and Thomas (2011) show that choosing similar friends, influence and shared surroundings cannot be separated in such data. The strongest causal design found is randomly assigned college roommates (Eisenberg et al. 2013, *Health Economics*). It found no overall contagion of mental health, none for happiness, and modest evidence for anxiety and depression. Online, one exposure does almost nothing: d as small as 0.001 in 689,003 Facebook users (Kramer et al. 2014; verify, not checked).
- **Verdict.** Mood probably spreads a little between people who spend a lot of time together. The Framingham percentages are upper bounds.

### 1b. Bad spreads more strongly at home; elsewhere the evidence is mixed

Evidence that bad spreads more strongly:
- Hill, Rand, Nowak and Christakis (2010, *Proc. R. Soc. B*; confirmed) fitted an infection-style model to Framingham. Each discontented contact raises your yearly chance of becoming discontented by 0.04. Each content contact raises your chance of becoming content by only 0.02. Contentment lasts about 10 years; discontent about 5.
- Saxbe and Repetti (2010, *JPSP* 98:92; 30 couples sampled 4 times a day for 3 days; confirmed):
  - a partner's negative mood predicted one's own;
  - positive moods were not linked;
  - **marital satisfaction moderated the negative link**: in happier couples, bad moods crossed over less.

Evidence that good spreads as much, or more:
- Hill, Griffiths and House (2015, *Proc. R. Soc. B*; confirmed): among adolescents, depression did not spread but healthy mood did. Five or more healthy friends halved the risk of becoming depressed, compared with none.
- Ferrara and Yang (2015; confirmed): on Twitter, about 20% of users were highly susceptible, and they were about 4 times more affected by positive content than negative.
- Barsade (2002), above, found no difference by valence.

Susceptibility varies. In Sels et al. (2016), 64% of 50 couples showed no strong emotional interdependence (verify, not checked).

**Verdict.** Use an asymmetry for close ties at home only, softened by love. Elsewhere, spread bad and good alike.

### 1c. Shyness alone delays people; shyness plus being left out withdraws them

- **Shyness is common, and mostly not harmful in itself.**
  - About 15-20% of infants are highly inhibited (Kagan; secondary summary).
  - Inhibited children have about seven times the risk of social anxiety disorder: 43% against about **13%** (Clauss and Blackford 2012; fact-check corrected 12% to 13%). Meta-analysis: odds ratio 5.84 for social anxiety and 2.80 for any anxiety (Sandstrom et al. 2020; confirmed).
  - Still, 40-60% of inhibited children never develop the disorder.
  - As adults, inhibited children are reserved and late to partner and to work, but mostly without distress (Asendorpf et al. 2008; Caspi, Elder and Bem 1988). The "only the top 8% had internalizing problems" figure is unverifiable, and the "18% stay inhibited at every visit" figure (Kagan) is unverifiable too.
- **Shy plus excluded is what escalates.** This is the result that most directly supports Sid's idea.
  - Gazelle and Ladd (2003, *Child Development*; N = 388): anxious-solitary children who were also excluded stayed withdrawn and had the highest depressive trajectories. Shyness without exclusion did not.
  - Gazelle and Rudolph (2004, *Child Development*; N = 519, three waves over a year; **confirmed**, so the earlier "verify" is dropped):
    - shy children who were highly excluded kept or worsened their avoidance and depression;
    - shy children with low exclusion **moved toward others** and grew less depressed;
    - the interaction mattered more for anxious-solitary youth than for others.
  - Oh et al. (2008; N = 392; confirmed): withdrawal rose with friendlessness, unstable friendships and exclusion, and fell when exclusion was low.
- **The loop runs both ways.** Withdrawal draws exclusion and exclusion deepens withdrawal (Boivin, Hymel and Bukowski 1995). Victimisation predicts internalizing, and internalizing predicts victimisation (Reijntjes et al. 2010; the pooled r values are unverified).
- **One friend is the strongest protector found.** Frenkel et al. (2015; confirmed): the path from inhibition to adult anxiety was β = .43 when adolescent peer involvement was low, and not significant when it was high. Laursen et al. (2007) and Bukowski, Laursen and Hoza (2010) found that isolation predicted rising problems only in friendless children (both unverified by the fact-check). The friends of withdrawn children tend to be withdrawn and victimised themselves, and the friendships are of lower quality (Rubin et al. 2006).
- **Adults are barely studied.** Almost all of this is about children and adolescents. Pelican Town's hermits would be adults, so this whole pathway is an extrapolation.

### 1d. Being left out hurts everyone at once; the shy recover more slowly

- **The immediate hit is large and nearly universal.** A meta-analysis of 120 Cyberball studies (N = 11,869) found d > 1.4 across ages, countries, durations and group sizes (Hartgerink et al. 2015; confirmed). The claim that the immediate effect cannot be moderated was not supported.
- **Recovery speed differs by person.** Highly and less socially anxious people were equally hurt at first, but only the anxious were still affected 45 minutes later (Zadro, Boland and Richardson 2006; verify, not checked).
- **Rejection makes people more aggressive and less helpful.** Quarmley et al. (2022; N = 3,864; confirmed):
  - aggression rises, d = 0.41;
  - prosocial behaviour falls, d = 0.59;
  - the d = 0.71 figure is unverified.
  
  This casts doubt on rejected people reliably "trying to be liked".
- **Chronic exclusion leads to resignation over months.** Riva et al. (2017; cross-sectional) found the chronically excluded the most alienated, helpless and depressed of the groups they compared. Marinucci and Riva (2021): exclusion predicted resignation over 6 months (unchecked).
- **Loneliness feeds on itself.** Lonely adolescents were hypersensitive to exclusion and hyposensitive to inclusion. They put inclusion down to luck and exclusion to themselves (Vanhalst et al. 2015; verify). The loneliness "hypervigilance" to threat failed a pooled participant-level test in 2025, so treat it as a bias in interpretation, not in attention.
- **What lifts loneliness.** Interventions that correct the misreading of others did about three times better than those that only gave more chances for contact: d = 0.60 (Masi et al. 2011; verify).

### 1e. Hermits are rare, mostly young men after a social failure, and many come back

- **Prevalence.** Hikikomori is withdrawal at home for more than 6 months (Saito 1998). Lifetime prevalence is **1.2%** in Japan (Koyama et al. 2010; confirmed). 54.5% had another disorder, with mood disorder 6.1 times more likely. The "65.5% male" figure is unverifiable.
- **Triggers.** The most common trigger at ages 15-39 was difficulty with relationships (20.8%); at 40-64 it was leaving a job (44.5%) (Japan Cabinet Office 2022; press report).
- **History.** Each point of school adversity raised the risk by 29%: bullying items 37%, teacher items 23%. School adversity mattered; family adversity less so. For depression and anxiety the figures were +44% per school point and +24% per family point (Wakuta et al. 2023; confirmed, with the fact-check's correction). The design is retrospective and cross-sectional.
- **Recovery.** About half of 104 Hong Kong hikikomori in contact with services returned to work within 12 months (Yuen et al. 2019). Shorter withdrawal goes with fewer symptoms. The anxious and depressed subtype relapses more (Malagón-Amor et al. 2018).
- **A pathway.** Shy temperament plus parental rejection leads to ambivalent attachment, which plus peer rejection predicted hikikomori (Krieg and Dickie 2013). The design is retrospective case-control, so the evidence is weak.

### 1f. Brawlers: aggression is learned in small exchanges that pay off

- **Coercion.** Escalation that makes the other side give in is rewarded on both sides, and repeated many times it builds aggression (Patterson; Smith et al. 2014). No effect size was found.
- **Hostile attribution bias.** Reading ambiguous acts as hostile predicts aggression, r = .17 (Orobio de Castro 2002). The association is strongest when the person is provoked (Verhoef et al. 2019: 111 studies). In 12 cultural groups, a child who read an act as hostile was about 5 times more likely to say they would retaliate (Dodge et al. 2015; verify).
- **Two pathways.**
  - Life-course-persistent antisocial behaviour: 10.5% of males and 7.5% of females in the Dunedin cohort (Odgers et al. 2008; confirmed).
  - A larger adolescent-onset group (19.6% and 17.4%) mostly grows out of it.
  - A "childhood-limited" group often becomes withdrawn and anxious instead, which links the two extremes.
- **Maltreatment.** It raises the risk of a violent arrest only from about 14% to 18% (Widom and Maxfield 1996; mostly confirmed). More than 80% of maltreated children were never arrested for violence.

### 1g. "It depends on the person"

- **Two models.**
  - *Diathesis-stress:* a vulnerable trait makes bad events hurt more and changes nothing in good times.
  - *Differential susceptibility* (Belsky and Pluess 2009): the same people are hurt more by bad surroundings and helped more by good ones.
- **The groups.** In self-reports on the Highly Sensitive Person scale, 31% were high, 40% medium and 29% low (Lionetti et al. 2018; confirmed). High scorers were higher in neuroticism and lower in extraversion.
- **The evidence is not settled.** Negative emotionality marked susceptibility to parenting in both directions, but the effect did not survive correction for publication bias (Slagt et al. 2016). Formal tests often fail to tell the two models apart (Roisman et al. 2012).
- **What the specific studies show.** Gazelle and Rudolph is itself a differential-susceptibility result: the shy withdraw more when excluded and approach more when included.

### 1h. Lasting change is small and follows lasting conditions

- **Size.** Life events move traits by about 0.05 SD on average (Haehner et al. 2025, N = 196,256; Bühler et al. 2024).
- **Chronic difficulty.** Long-term difficulties raise neuroticism persistently, β = 0.18 over multi-year waves, and improving life lowers it, β = -0.13 (Jeronimus et al. 2014; confirmed). This is a standardized path, not a per-season dose.
- **Loneliness and traits.** Loneliness lowers extraversion and emotional stability by β of about 0.02-0.04 over 4 years, and the reverse holds too (Schellenberg et al. 2025).
- **The game.** A visible slide within a game year needs coefficients exaggerated well beyond these. That is a game choice and should be labelled so, as `inheritance-research.md` section 4 already says. Its rule fits too: one severe event moves a trait moderately and fades, while lasting change needs lasting conditions.

### 1i. Showing a feeling and holding it in (Sid's "nature factor")

Sid asked for a trait for how much of a feeling shows (design section 12, question 7). It has its own research note, `masking-research.md`, which replaces what follows: masking hides the expression, not the feeling (Webb et al. 2012); it costs closeness (Srivastava et al. 2009), less so when it is done out of care (Le and Impett 2013); held hurt building into depression is not established (Larsen et al. 2013); and suppression is about a third heritable (McRae et al. 2017). The search budget here ran out before this could be checked, so what follows was recalled:
- People who habitually suppress the expression of emotion report less positive emotion, more negative emotion, lower well-being and less closeness to others (Gross and John 2003, *JPSP*; VERIFY).
- In conversations between strangers, the partner of someone told to suppress felt less rapport, and their blood pressure rose (Butler et al. 2003, *Emotion*; VERIFY).

If both hold, suppression has two effects: it keeps a feeling from spreading by sight, and it costs the holder closeness. That fits Sid's reading of Penny, who masks what Pam lets out.

**Where it is weak, overall:**
- The child-to-adult extrapolation.
- Small cohorts for the moderation effects.
- Contested network contagion.
- Retrospective hikikomori data.
- No source anywhere for how many events, over what span, tip someone.

## 2. Mood contagion for the simulator

### 2a. What mood is now

- **Mood.** Each person's mood is `Squash(Σ amount x weight)`. Each felt affect is an entry, and an entry's weight falls linearly to 0 over `MoodDays` = 3 days.
- **Typical entries.**
  - An argument received: -0.3 x (0.5 + sensitivity).
  - A gift received: +0.2 x (0.5 + sensitivity).
  - A curt greeting (tone, off): -0.1 x (0.5 + sensitivity).
  - A chat: +0.005 x (0.5 + chattiness), so 10-20 chats a day add about +0.05 to +0.15 a day.
- **The power of acting** is 0.5 + conditions + 0.4 x mood. The town runs with `PowerWeight` 1, so power moves daring in the gate one for one.
- **What already reads someone else's state.** Imitation, Sympathy and Antipathy (`Feelings.Base`) do, but they react to **acts**: a share of what the patient of an act felt goes to those who love them, hate them, or are like them. Nothing reads another person's **mood**.

### 2b. The rule (C1)

Contagion goes inside `Company`, so it happens per chat. The number of chats already grows with time together (one chance per 120 minutes side by side) and with both people's chattiness. Totterdell (2000) found linkage grew with commitment to the group; time together stands in for that.

For a chat between A and B, A gets one more mood entry, and B gets the mirror one:

```
e(A <- B) = K x S(A) x T(A,B) x (shown(B) - mood(A)) x Neg
```

- **K = 0.01.** A game choice: at 4-6 chats a day between housemates and a gap of 0.4, about 0.02-0.03 a day, against the 0.05-0.15 a day company already gives.
- **S(A) = 0.5 + sensitivity.** The existing `Sens`, read as susceptibility both ways (section 5).
- **T(A,B), the tie.**
  - 1.0 for kin and housemates;
  - 0.6 when A's regard for B is 0.4 or more;
  - 0.3 for anyone else.

  Fowler: neighbours count and coworkers did not. But in a town where everyone meets at three hubs, being in the same place and chatting is the condition, so coworkers get 0.3, not 0.
- **shown(B) = mood(B)** until the shown and held trait exists (step 0d.6h). After that it is expressivity(B) x mood(B).
- **Neg, the asymmetry at home.** When shown(B) < mood(A) and the tie is kin or housemate, Neg = 2 x (1 - 0.5 x max(0, regard(A,B))). Otherwise Neg = 1. This follows Hill 2010's 0.04 against 0.02 and Saxbe and Repetti's moderation by satisfaction. Elsewhere it stays symmetric (Barsade; Hill 2015).
- **Cap.** The day's contagion entries for one person sum to at most ±0.05. A crowd cannot flip someone in a day (Kramer: single exposures do almost nothing).
- **Company stays as it is.** The plain joy of company stays at `CompanyJoy`, because being with people is good for the lonely in all of this research. But contagion makes the company of a low person a net loss when the gap is large. Between housemates who are not close (regard 0, so Neg = 2), a gap of about 0.3 outweighs one chat's joy (0.02 x 0.3 = 0.006, against 0.005 x (0.5 + chattiness)); between others it takes a much larger gap.

The difference form `(shown(B) - mood(A))` pulls toward the other person and never past them. Two people at the same mood change nothing. A gloomy pair cannot spiral below its lower member on contagion alone. This is what keeps it from tipping towns.

### 2c. Not counting twice

- **Shared acts.** Imitation and Sympathy already give A a share of what B felt at an act, at the moment A learns of it. If A felt the same act, B's mood carries that act again.
  - So when computing what B transmits to A, leave out B's entries from acts A has a felt record for (`_felt` has (holder, act)).
  - This needs the mood entries tagged with their source: an act id, or Company, Tone or Contagion. `_affects` holds only (tick, amount) today.
- **Echo.** Contagion entries are passed on like any other part of mood, so second- and third-degree spread emerges on its own at about K x T per step. That falls off faster than Fowler's 15/10/6. Given the confounding, modelling direct ties only is what the fact-check advises.
- **Regard.** Contagion moves mood only, never regard. Regard comes from acts, and contagion has none.

### 2d. What it does to the laws already built

- **The power of acting.** Its spread should widen.
  - Pam's trailer runs short and she drinks most evenings, so her mood is low. She will pull Penny down at home.
  - Penny's school children and Vincent's family pull Penny the other way.
  - Watch the loop in which the sad drink more, by `PowerFactor`. Pam drinks, is sadder, pulls Penny down, and Penny gets less daring.
- **The gate.** At `PowerWeight` 1, a contagion swing of 0.1 in mood moves daring by 0.04. Those living with someone low will answer, return and give a little less.
- **Steering by mood.** Gifts and help go to the glad, drinking to the sad, and close calls are tilted by mood. All of these now carry other people's moods too.
- **The first-greeting tone (off).** If Sid turns it on, contagion and tone form a loop.
  - What the tone is: on a day's first chat between two people from different households, each reads the greeting as warm with chance 0.06 x power x (1 + regard), or as curt with chance 0.12 x (1 - power) x (1 - understanding) x (1 - regard).
  - A warm greeting is a +0.1 x (0.5 + sensitivity) mood entry. A curt one is the same amount negative, a small regard loss, a hurt that moves stance, and an Answer motive.
  - So with the tone on, a low mood makes greetings read as curt, the curtness lowers mood and moves stance, and contagion carries the low mood to housemates. They then read their own greetings as curt more often.
  - This is the misreading loop of the loneliness and hostile-attribution literature (1d, 1f), so it may be wanted later. But it should be measured together with contagion, not added on top of it.
- **E2.** Contagion is a new law, and it must earn its place by moving a story metric by 20% or more when switched off.

## 3. How a hermit forms

### 3a. Pathways

**Pathway W1: shy plus left out** (Gazelle and Ladd; Gazelle and Rudolph; Oh). This is the main one.

*Left out*, E(i), from what the simulator records over a rolling 28 days:
- **Kindnesses received:** life-record `Undergone` events of kind acts (a gift or help) by non-kin.
- **Meetings sought by others:** days with 60 or more minutes together with a non-kin person, from `_together` and `_lastContactDay`, kept per person.
- **Kindness not returned:** the person's own kind acts whose outcome was `Ignored`. This is the sharpest signal of being left out: they reached out and nobody answered.

```
E(i) = clamp( 0.4 x (1 - kind_received / town_median)
            + 0.3 x (1 - contact_days / town_median)
            + 0.3 x ignored_share, 0, 1 )
```

Using town medians makes "left out" relative to how everyone else lives. A quiet town does not make everyone a hermit.

Nightly, with shy(i) = 1 - boldness:

```
stance -= Kx x E(i) x shy(i)^2 x S(i) x (1.5 - selfRegard)
```

- Kx = 0.01 a day, a game choice.
- shy squared gives the interaction: boldness 0.02 gets 0.96, boldness 0.5 gets 0.25, boldness 0.8 gets 0.04. That is Gazelle and Ladd's "small without shyness".
- (1.5 - selfRegard): the lonely put exclusion down to themselves (Vanhalst).

**The other side** (Gazelle and Rudolph). When E(i) < 0.2 and shy(i) > 0.5, stance moves toward 0 by an extra 0.01 a day. The included shy approach.

**Pathway W2: shy plus repeated bad events.** Hurts that do not stir a motive today should still count.
- Rows at home, by a housemate or kin: `StirFrom` returns early for these today.
- Being warned, taken in, questioned, set to service, a family row, a mishap.

For these, call `Hurt` without stirring a motive. Weight the row at home by 0.5, because kin forgive and are forgiven; this is a game choice.

Repetition sensitizes, for losses of standing and income. Repeated unemployment hurt more each time, but this was shown for life satisfaction, not traits (Luhmann and Eid 2009). So in W2, multiply `felt` by 1 + 0.25 x (number of earlier W2 hurts in 28 days), capped at 2.

**The friend buffer, on both pathways** (Frenkel; Laursen; Bukowski). If the person has a mutual friend (regard 0.4 or more both ways) whom they were with in the last 7 days:
- W1 and W2 are multiplied by 0.3;
- and by 0.6 if that friend's own stance is -0.3 or below (Rubin 2006: the friends of withdrawn children tend to be withdrawn themselves).

Losing that friend (regard falls below 0.4) counts as a W2 hurt of 0.3.

**Inclusion discounted.** `StanceAfterKindness` today removes the share `felt` of any stance. For a withdrawn stance (below -0.3), multiply that share by 0.3, unless the kindness comes from a mutual friend or reconciles a quarrel.
- This is what stops one gift from undoing weeks of being left out.
- Lonely people discount inclusion (Vanhalst), and contact alone helps less than being understood (Masi).

Combative stances keep the full share. That holds the brawler count and is a choice, not a finding.

### 3b. What a withdrawn stance changes

These are read-only effects of the fast state. None of them changes a trait.

- **Home.** `HomeWeight` = 1 + avoided + `StanceHome` x (-stance). At `StanceHome` 1 this is at most double, which is too weak to move hours out. Raise the dial to 3, measured.
- **Daring.** Already in place: `Effective` adds 0.5 x min(0, stance) to every act's daring.
- **Chattiness in use.** The chat chance uses chattiness x (1 + 0.5 x min(0, stance)). The withdrawn talk less, so they get less company joy and less contagion either way.
- **Seeking.** Fond's wish to give is multiplied by (1 + min(0, stance)). The withdrawn stop seeking even those they love. Resignation lowers the drive to approach (Williams; Riva).

Each of these lowers the meetings and kindnesses that E counts, which closes the loop (Boivin; Cacioppo 2009) with a gain below 1. Stance still fades by 0.975 a night, and the friend buffer and the included side pull it back.

### 3c. Fast state against lasting change

- **0d (now): stance is the fast state.** It fades with a 27-day half-life when nothing keeps it going. Sid's answer (design 12.7): it stays separate from 0e.
- **Hermit, as a measure.** Stance at -0.5 or below for 28 days in a row, **and** hours out of home in that season below 60% of the person's own first season.
  - Both parts are needed. The hikikomori definition is about behaviour at home, not a feeling.
  - The current measure, -0.5 or below ever, counts a bad week.
- **0e (later): the set point.** Following Jeronimus's split between state and set point:
  - at each season's end, boldness and chattiness (on the hidden z scale of `inheritance-research.md`) move by -0.1 x (the season's mean of min(0, stance)) x plasticity(age);
  - a whole season at -0.5 moves z by 0.1 x 0.5 x 0.4 = 0.02 at age 30 (plasticity 0.4, from the table in `inheritance-research.md` section 4), and by 0.05 for a child;
  - nothing moves when stance is near 0.

  This is "changes stick while they keep being triggered" (design rule 18). Real coefficients are 10 or more times smaller (Schellenberg). This is a game choice, to keep across runs of 5-10 years.

### 3d. Recovery

- **A friend.** Kindness from a mutual friend eases stance at full strength, and the friend buffer cuts new hurts.
- **Being understood.** A reconciliation, or a MakeUp answered, eases at full strength (Masi). A stranger's gift eases at 0.3.
- **A fresh start** (Gazelle and Faldowski 2019; unchecked, VERIFY). At a season change, E's 28-day window starts again empty.
- **Targets.**
  - About half of hermits back above -0.3 within a season of game time. This compresses Yuen's half in a year.
  - The anxious relapse more. Those with high sensitivity and high retention take longer.
- **0e.** A trait shifted in 0e recovers only through the reverse conditions, such as a season of company and kindness. It is never reset.

## 4. How a brawler forms

### 4a. Whether the current rule matches the research

`StanceAfterHurt` adds felt x (2 x boldness - 1). The bold who are hurt grow combative, and combativeness raises only hostile daring. In part this matches:
- Rejection raises aggression (Quarmley, d = 0.41).
- Answering a slight is a reading of it as hostile (Dodge).
- Fear (+0.1 cost per hostile act received) caps escalation.

Where it does not match:
1. **Rejection also lowers kindness** (Quarmley, d = 0.59), and the stance does not. Today a combative stance leaves kind daring untouched.
2. **What reinforces aggression is winning** (Patterson). Today being hurt builds the stance; prevailing does not.
3. **The count is a fast state.** "Brawler" means +0.5 or above ever, so 3.5 a seed-year (about 13% of the town) counts heated weeks. Moffitt's persistent group is about 9% of people in a lifetime, and the passing group is larger still. The count is not out of line, but it measures a week, not a person.

### 4b. Additions (each behind a switch)

- **B1, kindness falls too.** A combative stance lowers kind daring by 0.25 x stance, half the weight it adds to hostile daring. This may slow reconciliations, so measure them.
- **B2, the coercion ratchet.** When someone's argument is met by avoidance, withdrawal or turning away (outcome `Avoided`), their stance rises by 0.05 x felt. Escalation won.
  - When it is answered or reconciled, nothing changes.
  - This joins the two extremes: a brawler grows by driving the shy away, and the shy one withdraws because of it.
- **B3, a sustained measure.** Brawler: stance at +0.5 or above for 28 days in a row. Report it beside the current measure.

0e for brawlers mirrors 3c: the set point of boldness rises, and retention may rise too, by the season's mean of max(0, stance).

## 5. "It depends on the person"

Recommendation: **differential susceptibility** as the default. It is symmetric, which matches Sid's "good people bring others up". Gazelle and Rudolph's shy children show it directly.

How each trait sets susceptibility in both rules:

| Trait | Role | In the rules |
|---|---|---|
| boldness | direction | Hurt makes the bold combative and the shy withdrawn (existing). shy² in W1. |
| sensitivity | size, both ways | Already scales every felt amount through `Sens`, so W2 and kindness are covered. Apply S(i) = 0.5 + sensitivity only to the new terms that have no felt amount: W1, contagion and the included side. Never twice. |
| retention | how long | Stance keeps 0.975 + 0.02 x (retention - 0.5) a night: a half-life of about 21 days at 0 and about 35 at 1. From Zadro: the anxious stay hurt longer. |
| self-regard | how personal | W1 x (1.5 - selfRegard). |
| understanding | misreading | Already in the tone's curt chance (1 - understanding). If tone and contagion are on, the low in understanding feed the loop more. |
| chattiness | exposure | More chats mean more company and more contagion, and less E. |
| expressivity (proposed, 0d.6h) | what shows | Others read shown mood. The held part stays in mood and also moves stance toward withdrawn, whatever the person's boldness. |

**Diathesis-stress as a switch.** To try the other model, apply S(i) only to entries that lower mood or stance, and use 1 for those that raise them. Roisman's warning applies: the two models are hard to tell apart even in real data, so choose by what makes stories.

## 6. Build plan for 0d.6

Each step has its own switch in `FeelingOptions`, off by default. With every step off, the pinned hash `e9fd83b284f5c1b6` (P3: the shipped gate with its starting tensions, seed 1, a year) holds. (This note first quoted `b6a87fe3b8fc0916`, which is the pin of the same town with Fond at 10 days, before 0d.5 moved it to 14.) Each step also runs in observe mode first, logged and not applied.

**Gates for every step** (200 seed-years and 400 seeds x 14 days, as in 0d):
- 0a band: 52% or more of witnessed scandals in 40-70%, 28% or less over 70%;
- E1: 60% or more of seed-years;
- no war towns and no dead towns;
- regard below -0.2 at 1-5% from the second season;
- money 0.000 g;
- all tests pass.

| Step | Switch | What | Pass criteria |
|---|---|---|---|
| a | none | **Measure only.** Per person per season: E(i), kindnesses received, contact days, ignored kindnesses, hours out, the sustained hermit and brawler measures, and the power spread. A `--log` line for the shyest five. | Hashes unchanged. Baseline recorded: hermits 0 and Penny's stance -0.01 should reproduce. |
| b | `HomeHurtOn` | W2: hurts at home and undergone hurts move stance, with no motive; repetition up to x2. | Penny's mean stance over the year is lower than today. Sustained brawlers no more than 1.2 x baseline. Gates hold. |
| c | `ContagionOn` | C1 with source-tagged mood entries, the shared-act exclusion, the asymmetry at home and the daily cap. | Mean power within ±0.03 of today. Its spread (SD) rises by no more than 50%. No more than 10% of person-days below power 0.35. Nobody's 28-day mean power below 0.3 in more than 5% of seed-years (no sinks). E2: switching it off moves a story metric by 20% or more. |
| d | `LeftOutOn`, `InclusionDiscountOn` | W1 with the friend buffer, the included side, and the discount for a withdrawn stance. | **0.2-1.0 sustained hermits a seed-year** (Sid to set the band; question 1). Hermits come from the shyest third in 80% or more of cases. In the scene where the shyest is left out, hours out fall by 25% or more from season 1 to season 4. A shy person with zero exclusion is never a hermit, on any of 200 seeds. |
| e | dials | `StanceHome` 1 to 3; chattiness in use; Fond damped. | The hours-out fall in step d holds at a 1-in-5 town level, not only in scenes. Trivia and news within ±15% of today. |
| f | `RecoveryOn` | Full-strength easing from friends and reconciliations; the fresh start at season change; retention in stance fading. | 40-60% of hermits back above -0.3 within 28 days of the measure ending. No hermit lasts the whole year in more than 20% of seed-years unless their exclusion is still above 0.6. |
| g | `CoercionOn`, `RejectedLessKindOn` | B1 and B2. | Sustained brawlers stay within baseline ±50%. Reconciliations no lower than 0.5 a year. Feuds stay at 8 a year ±25%. |
| h | `ShowOn` | Expressivity, after Sid's answers (question 7). | Penny (masks) gets more withdrawn stance and less outward spread than Pam (shows). Gates hold. |

**Tests**, as scenes in the style of `DesireSceneTests` and `DesireGraftTests`: small worlds, set traits with `SetTrait`, injected acts, and an assertion on a number.

Contagion:
1. `ASadHousemateBringsYouDown`: Pam's mood set low by injected entries, with Penny at home beside her. Penny's mood falls with `ContagionOn`, and is unchanged with it off.
2. `AGladFriendLifts`: the same, upward, at friend strength 0.6.
3. `EqualMoodsChangeNothing` and `NoChatNoContagion`: together but no chat, or equal moods, give no entry.
4. `LoveSoftensTheBadAtHome`: regard 0.8 halves the negative pull compared with regard 0.
5. `ASharedActIsNotFeltTwice`: A and B both see the same argument. A's mood equals the Sympathy share alone.
6. `TheCapHolds`: five low people chatting with one person in a day give no more than -0.05.

Hermits:

7. `TheShyLeftOutWithdraw`: boldness 0.02, no kindness and no contact for 56 days. Stance at -0.5 or below for 28 days, and hours out fall.
8. `TheShyIncludedDoNot` (invariant): over 20 seeds, a person given a kindness and a day together each week is never a hermit.
9. `TheBoldLeftOutBarelyMove`: boldness 0.8 in the scene of test 7 ends above -0.1.
10. `RowsAtHomeWithdrawTheShy`: rate arguments from a housemate move stance down with `HomeHurtOn`, and stir no motive.
11. `AFriendBuffers`: the scene of test 7 plus one mutual friend seen weekly keeps stance above -0.3.
12. `AStrangersGiftIsNotEnough`: one gift from a stranger to a hermit eases a third of what a friend's gift eases.
13. `SensitivityCutsBothWays`: high sensitivity withdraws faster when left out and recovers faster when included.

Brawlers:

14. `WinningMakesBolder`: an argument met by avoidance raises the arguer's stance (B2).

General:

15. Determinism: same seed, same hash. Each switch off reproduces the pinned hashes.

## 7. Questions for Sid

Answered on 2026-10-07 (Sid):
- **1. How many hermits:** 0.2-1.0 sustained hermits a seed-year, as proposed.
- **3. Content loners:** yes. Someone who prefers solitude (low chattiness, not shy) is not harmed by being alone: time alone does not count toward being left out for them, and they are a content loner, not a hermit.
- **5. Recovering alone:** yes: the fresh start at a season change, as well as friends and reconciliation.
- **6. 0e:** a season as a hermit is more likely to lower boldness for good than chattiness.
- **B1 (question 5), restated by Sid:** it is the people dealing with a combative person whose kindness toward them runs out, after a couple of rounds, not at the first one: patience drops off quickly, by how patient the person is. Confirmed: a per-pair patience that each hostile round from the same person uses up, refilling slowly, sized by understanding and sensitivity; when it runs out, kind daring toward that person falls.
- **2. Bad moods heavier at home:** yes, at home only, softened by love (after scenarios: the trailer, Penny at school, Emily and Haley, the saloon). Elsewhere good and bad spread alike.

Also answered: question 7 (a seventh trait, expression; and the held part's pull toward withdrawal is to be designed with `masking-research.md`), and question 8 in part (Sid likes the tone, read through expression). Sid also answered one this note did not ask: households argue, so the gate may act inside a household when regard there is bad, and rows at home (W2) come through the gate as well as through the rates.

1. **How many hermits?** Real lifetime prevalence is about 1.2%, which is 0.3 people in a town of 26 over a lifetime. A game wants them visible. Proposed: 0.2-1.0 sustained hermits a seed-year, so one appears in some years and not in most. Higher or lower?
2. **Bad moods heavier only at home, or everywhere?** The research supports the asymmetry for couples and families, not for friends or groups. Proposed: at home only, softened by love.
3. **Moods past direct ties?** Proposed: no rule for second and third degrees; let them emerge. The famous three-degree numbers are the most contested part.
4. **A happy loner?** The research separates the shy (want company, fear it) from the unsociable (prefer solitude, unharmed). Should someone like Linus be a content hermit, with a different rule from a shy one who was left out? VERIFY Linus's canon before using him.
5. **Rejected people turn less kind** (Quarmley, d = 0.59). Adopt B1, knowing it may slow reconciliations?
6. **Hermits recovering alone.** Proposed: a fresh start at season change, plus friends and reconciliation. Or only through a person?
7. **What shows and what is held.** A seventh trait (expressivity), or derived from existing ones, for example low self-regard plus high understanding? Should the held part push toward withdrawal even in the bold?
8. **The first-greeting tone**, now that its loop with contagion is known (2d). Still off for the starter season? Or on once contagion is measured, as the misreading that deepens a hermit or a brawler?
9. **0e.** Should a season spent as a hermit lower boldness and chattiness for good, at the exaggerated game rate (3c), so it carries into the next run?
