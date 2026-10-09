# Inheritance, mutation and change of character: research for Under Glass (rule 18, 11e)

> Research for later generation mechanics, not the next milestone. The [roadmap](roadmap.md)
> puts concrete encounters, responses and follow-through first. Preserve these findings for
> that later feature without treating its build plan as the present work queue.

Scope: the six traits of law 12 (chattiness, boldness, understanding, self-regard, sensitivity, retention), each 0..1; children of two parents; 5-10 simulated years between runs, across generations. Every claim names its source. "Verify" marks anything recalled or seen only in an abstract or search summary, not checked in the paper itself.

## 1. How heritable are traits like these?

**Overall.**
- Vukasović & Bratko (2015): a meta-analysis of 62 independent samples (more than 100,000 people) found personality about **40% heritable** and 60% environmental.
  - Study design matters. Twin studies give **0.47**; family and adoption studies give **0.22**.
  - Neither the personality model nor sex changed the estimate.
- Polderman et al. (2015): a meta-analysis of 17,804 traits from 50 years of twin studies.
  - Average heritability across all traits was 0.49.
  - For 69% of traits, the twin correlations fit a purely additive model.
  - Overall, the data did not support large shared-environment or non-additive effects.
- Plomin et al. (2016) list among the most replicated findings in the field that every psychological trait is heritable, typically **30-50%**, and none comes close to 100%.

**Trait by trait.** The mapping to the game's traits is approximate.

| Game trait | Closest measured trait | Heritability | Source |
|---|---|---|---|
| chattiness | extraversion | about 0.4-0.5 (twin studies); a large non-additive share | Vukasović & Bratko 2015; Keller et al. 2005 |
| boldness | sensation seeking | 0.48-0.63 in adolescent twins, best fit by an additive model; 0.29-0.65 by subscale in an extended twin design | Koopmans et al. 1995; Stoel et al. 2006 |
| understanding | cognitive empathy | **0.27**, plus about 0.12 shared environment on performance tests. Emotional empathy is higher, at 0.48 | Abramson et al. 2020 (meta-analysis of twin studies) |
| self-regard | self-esteem | substantial heritability with no significant shared environment. It shares a general heritable factor with negative emotionality | Neiss et al. 2002, 2006, 2009. Verify: an exact pooled figure (I recall 0.3-0.5) was not confirmed |
| sensitivity | neuroticism | about 0.4-0.5 (twin studies); a large non-additive share | Vukasović & Bratko 2015; Keller et al. 2005 |
| retention | rumination (brooding) | **0.17-0.21** in adolescent twins | twin studies of 12-14-year-olds (Moore et al. 2013 / Johnson et al.; verify the authors) |

On retention: I found no twin study of forgiveness or grudge-holding. It is probably the least heritable of the six traits.

**Additive versus non-additive genetic variance.**
- Polderman's broad result favours an additive model.
- Twin-only designs are poor at detecting non-additive variance. Keller et al. (2005) added siblings to the twin design (9,672 twins and 3,241 siblings). For extraversion, neuroticism, novelty seeking and harm avoidance, broad-sense heritability came out **two to three times** the narrow-sense (additive) heritability.
- This explains why twin estimates (0.47) are about double the family and adoption estimates (0.22):
  - identical twins share all gene combinations;
  - parents and children share only half the additive part, and almost none of the interactions.
- Family studies put parent-child correlations at roughly **0.10-0.16**, and the regression of child on midparent at about 0.15. These numbers come from search summaries of family studies; verify the exact papers. They agree with h² ≈ 0.22 from family designs.

**Shared versus non-shared environment.**
- For personality, the environment that makes siblings alike (shared environment) is near zero in most studies (Plomin et al. 2016; Neiss et al. on self-esteem).
- Almost all environmental variance is non-shared: experiences unique to each child.
- Cognitive empathy is a small exception (about 0.12 shared; Abramson et al. 2020).
- Twin studies count measurement error as "non-shared environment". With typical scale reliability around 0.8 (recalled; verify), true-score heritability is somewhat higher than the reported figures. The game measures its traits without error, so its heritabilities should sit slightly above the published ones.

## 2. A deterministic model of a child's starting traits

**What this means for the game.** Children resemble their parents only weakly. A parent-child correlation of about 0.15 means a child is mostly a fresh draw that leans slightly toward the parents. Siblings correlate about 0.15-0.20; non-twin sibling resemblance is typically 0.15-0.20 (from a search summary of the twin literature; verify the exact source).

A naive "average the parents and add noise" model gets siblings wrong:
- With the midparent slope at a realistic 0.3, two siblings share only the midparent term. Their correlation is about a²/2 ≈ 0.05, far too low.
- The fix is to give each person a hidden **genetic value**, separate from their current character. This is the standard infinitesimal model of quantitative genetics (Fisher 1918; recalled, not re-checked).

**Scale.** Work on a latent scale z per trait, with population mean 0 and variance 1. Show the trait as t = 1 / (1 + e^(-z)), so z = ln(t / (1 - t)):
- z = 0 shows as 0.5, z = 1 as 0.73, z = 2 as 0.88 and z = 3 as 0.95;
- extremes are possible without clamping, which leaves room for fringe people (11e).

### Model 1 (recommended): hidden genetic value plus seeded noise

Each person stores, per trait, a fixed genetic value g and their current character z. The plasticity rule moves z and never touches g.

At birth, with every draw from a `Random` seeded by Fnv1a of (child id, trait):
- **Additive part:** g_child = (g_mother + g_father) / 2 + N(0, √(VA/2)). The second term is segregation: each child gets a different half of each parent.
- **Non-additive part:** d = N(0, √VD), drawn fresh, so it is not inherited. Real siblings share about a quarter of it; for that, add a per-couple draw at weight 1/4.
- **Birth environment:** e = N(0, √VE_birth).
- **Starting character:** z_birth = g + d + e.

Suggested variances (total 1):

| Trait | VA | VD | VE_birth | Left for life events |
|---|---|---|---|---|
| chattiness, sensitivity | 0.25 | 0.20 | 0.25 | 0.30 |
| boldness | 0.40 | 0.05 | 0.25 | 0.30 |
| self-regard | 0.30 | 0.10 | 0.25 | 0.35 |
| understanding | 0.25 | 0.05 | 0.30 (includes a 0.10 household term shared by siblings) | 0.40 |
| retention | 0.15 | 0.05 | 0.30 | 0.50 |

The last column is the variance the plasticity rule should add by adulthood. It is a calibration target, not something drawn at birth.

**Founders** (the authored Stardew archetypes). Back-fill g from the authored z: g = VA·z + N(0, √(VA(1 - VA))), seeded by name. Under the model, this is the expected genetic value given the authored character.

**Calibration targets** for adults, measured in the simulator:

| Pair | Target correlation |
|---|---|
| parent and child | 0.10-0.20 |
| siblings | 0.15-0.20 |
| spouses | about 0.1, from who pairs up, not from convergence (section 4) |
| identical twins | about 0.45 |

**What Model 1 gets right:**
- Siblings differ, through segregation and their own noise.
- Children are not averages of their parents.
- Regression to the mean: the d and e parts are not passed on.
- Population variance stays stable across generations.
- Life changes are not inherited. A parent who became a hermit through rebuffs passes on their g, not their withdrawal; what they pass on is a lonely household.

**What it gets wrong:**
- It is continuous, with no Mendelian discreteness.
- Extremes are Gaussian, so they are rarer than real tails.
- Genetic drift in a small town is not modelled.
- The mean is anchored at 0 forever. That is a pull toward the middle at birth only, never during a life. It is real, and fits 11e because 11e forbids pulls on living people.

### Model 2 (alternative): explicit seeded alleles

Per trait:
- 20 loci, each with two alleles (0 or 1). The child takes one random allele per locus from each parent.
- G = the sum of 40 alleles, standardised to variance VA.
- Optional dominance per locus, where the heterozygote value is not the midpoint, gives non-additive variance.
- Phenotype as in Model 1.

**Pros:**
- Sibling variation, regression and rare extremes all emerge without being specified.
- A small town drifts: allele frequencies wander over generations.
- "Carrier" stories become possible, such as a recessive large-effect variant that appears when two carriers have a child.
- Storage is trivial: 240 bits per person.

**Cons:**
- Founders need alleles fitted to their authored values.
- Tuning is harder.
- Over many generations, drift in a population of about 30 loses variation unless mutation replenishes it. That is realistic, but it is one more thing to tune.

My recommendation is Model 1, unless Sid wants Mendelian stories.

## 3. Mutation: rare large deviations

- **De novo mutations are real and frequent, but mostly have no effect.**
  - Kong et al. (2012) sequenced 78 Icelandic trios. A child carries about **60 new mutations**.
  - The count rises by **about 2 per year of the father's age**: about 25 from a 20-year-old father, about 65 from a 40-year-old.
  - Rare, large-effect de novo variants do change behaviour, mainly through neurodevelopmental conditions. The share of children affected is small: a few per cent at most. Verify; I did not confirm a figure.
- **Non-additive effects are the common source of surprise.**
  - Keller et al.'s finding (broad-sense heritability two to three times narrow-sense) means children who resemble neither parent, especially in extraversion and neuroticism, are ordinary, not rare. Lykken called this "emergenesis" (recalled; verify).
  - Such deviations are mostly not passed on.

**Game proposal** (game tuning, not an empirical rate):
- The d term in Model 1 already supplies frequent, non-heritable surprises.
- Add a true mutation:
  - for each child and trait, with probability p, add N(0, 1.0) to g, so the change is heritable;
  - p = 0.015 × n/60, where n = 25 + 2 × (father's age - 20), which is Kong's paternal-age slope;
  - this is about 1 child in 11 with at least one mutated trait.
- For heavier tails without a separate rule, draw e from a Student-t with 5 degrees of freedom instead of a Gaussian.

## 4. Change over the lifespan

**Hardening.**
- Roberts & DelVecchio (2000): 152 longitudinal studies. Over a fixed 6.7-year interval, rank-order consistency rises:

  | Age | Consistency over 6.7 years |
  |---|---|
  | childhood | 0.31 |
  | college years | 0.54 |
  | age 30 | 0.64 |
  | ages 50-70 (plateau) | 0.74 |

  Childhood temperament was less consistent than adult traits.
- Bleidorn et al. (2022) re-synthesised studies published after 2005 (189 samples). Stability rose through early life and plateaued in **young adulthood, with little further rise after 25**. A search summary describes an inverted U with some decline in old age; verify in the paper.
- Briley & Tucker-Drob (2014): stability is low in childhood and rises substantially into adulthood. Early stability is mostly genetic; environmental continuity accumulates with age. A search summary also gave heritability falling from about 70% in early childhood to about 35% in late adulthood; verify which Briley & Tucker-Drob paper carries those figures.

Converting Roberts & DelVecchio to yearly change (1 - r^(1/6.7)) gives a **relative plasticity multiplier**:

| Age | Yearly change | Multiplier |
|---|---|---|
| child | 0.16 | 1.0 |
| about 20 | 0.088 | 0.55 |
| about 30 | 0.064 | 0.40 |
| 50 and over | 0.044 | 0.28 |

These figures include measurement error, so a floor near 0.25 is reasonable. It matches Sid's "harden, but never fully". Bleidorn et al. suggest the curve flattens after about 25 rather than continuing to fall.

**Mean-level drift.** Roberts, Walton & Viechtbauer (2006): people rise in social dominance, conscientiousness and emotional stability, especially between 20 and 40. Bleidorn et al. (2022) found emotional stability rising across the whole lifespan. In game terms: sensitivity falls slightly with age, and boldness and self-regard rise slightly in young adulthood.

**Major events versus repeated small ones.**
- Bühler et al. (2024): a meta-analysis of 44 studies (121,187 people). Life events have **reliable, specific but small** effects.
  - Work events (graduation, a first job) were larger and more consistent than love events.
  - A new relationship, marriage and divorce were the strongest in the love domain.
- Jeronimus et al. (2014; 16 years, five waves): neuroticism and life experiences reinforce each other. Events produce small but lasting change, and neuroticism invites more negative events.
- The corresponsive principle (Roberts, Caspi & Moffitt 2003): the traits that select someone into an experience are the traits that experience deepens. This directly supports 11e's "fringe people emerge from feedback".
- Implication: one severe event (felt at 0.7 or more) should move a trait **moderately**, not hugely, and the change should fade unless daily life keeps reinforcing it. Lasting change comes from sustained changes in role and routine; this is "social investment theory" (Roberts et al. 2005; recalled, verify). This is Sid's "changes stick while they keep being triggered".

**Convergence toward others.**
- **Couples.** Partners are only slightly alike to begin with: r = 0.08 for extraversion and 0.11 for neuroticism, against 0.58 for political values (Horwitz et al. 2023; 199 studies and 79,074 UK Biobank couples). In 1,296 married couples, Humbad et al. (2010) found **no consistent convergence over the years of marriage**; aggression was the exception.
- **Friends.** The evidence is mixed. One adolescent study found friends grew more alike; another found no convergence, no correlated change and no lagged partner effects in friends or siblings (search summaries; verify the papers).
- **Conclusion.** Convergence of personality toward close others is at most small in adults, possibly larger in adolescence. Sid's "slow drift toward who you surround yourself with" should be subtle, with a larger rate for children and teenagers. Attitudes, habits and values converge or match far more than traits do.

## 5. Ideas the research suggests

| Idea | Evidence | Worth modelling? |
|---|---|---|
| **Niche-picking and the corresponsive principle:** traits choose experiences, and experiences deepen those traits | Roberts et al. 2003; Jeronimus et al. 2014 | **Yes.** It is already rule 18's fringe feedback. Make sure acts chosen from a trait feed back into the same trait |
| **Children evoke their environment:** a bold child provokes more confrontations | Plomin et al. 2016: measures of "environment" are about 0.27 heritable | **Yes, and free.** It emerges if events depend on traits |
| **Differential susceptibility:** sensitive children are changed more by both good and bad environments | Belsky & Pluess 2009 (recalled; verify) | **Yes, cheap.** Scale a child's plasticity by sensitivity, for example × (0.7 + 0.6 × sensitivity) |
| **Traits pass by genes; grudges and values pass socially.** Shared environment barely moves traits, but partners and families share values strongly (r = 0.58) | Plomin et al. 2016; Horwitz et al. 2023 | **Yes.** Keep trait inheritance separate from kin regard. Sid's "head start, not a rule" for grudges fits this |
| **Correlated traits:** self-esteem and negative emotionality share a heritable factor | Neiss et al. 2009 | **Probably.** Draw self-regard and sensitivity noise with a negative correlation, around -0.4 (a guess; verify) |
| **Mean-level maturation:** sensitivity eases and boldness rises slightly between 20 and 40 | Roberts et al. 2006; Bleidorn et al. 2022 | **Optional,** as a small yearly nudge |
| **Paternal age** raises the mutation rate | Kong et al. 2012 | **Optional flavour.** Use it only through p |
| **Twins:** identical twins share g and part of d; useful for stories and as a calibration check | standard twin-design logic | **Optional** (rare; verify the twin birth rate) |
| **Birth order** | No effect on any broad trait outside intellect, in about 20,000 people (Rohrer et al. 2015) | **No** |
| **Sibling contrast:** siblings pushed apart | Mostly an artefact of parents' ratings; observer ratings show ordinary additive patterns (MacArthur Longitudinal Twin Study) | **No** as a trait rule. At most a parent's *belief* ("the shy one") as gossip |
| **Assortative mating on personality** | Very weak (r ≈ 0.1) | Let courtship weigh traits only lightly, and habits and values more |

## Sources

- Vukasović & Bratko 2015, Psychological Bulletin. https://pubmed.ncbi.nlm.nih.gov/25961374/
- Polderman et al. 2015, Nature Genetics. https://www.nature.com/articles/ng.3285.pdf
- Plomin et al. 2016, Perspectives on Psychological Science. https://gwern.net/doc/genetics/2016-plomin.pdf
- Keller, Coventry et al. 2005, Behavior Genetics (non-additive variance). https://experts.colorado.edu/display/pubid_85645
- Koopmans et al. 1995 and Stoel et al. 2006 (sensation seeking). https://research.vu.nl/en/publications/genetic-analysis-of-sensation-seeking-with-an-extended-twin-desig/ and https://link.springer.com/10.1007/s10519-005-9028-5
- Abramson et al. 2020, Neuroscience and Biobehavioral Reviews (empathy). https://cris.bgu.ac.il/en/publications/the-genetic-and-environmental-origins-of-emotional-and-cognitive-/
- Neiss, Sedikides & Stevenson 2002, 2006, 2009 (self-esteem). https://eprints.soton.ac.uk/65697/1/eprintsNeiss_et_al_JP_2009.pdf
- Rumination twin studies. https://experts.azregents.edu/en/publications/genetic-and-environmental-influences-on-rumination-distraction-an/ and https://www.psychologicalscience.org/journals/clinical/2167702612472884/
- Kong et al. 2012, Nature (de novo mutations). https://ideas.repec.org/a/nat/nature/v488y2012i7412d10.1038_nature11396.html
- Roberts & DelVecchio 2000. https://stafforini.com/works/roberts-2000-rankorder-consistency-personality/
- Bleidorn et al. 2022, Psychological Bulletin. https://www.diw.de/de/diw_01.c.843254.de/s_13631.html
- Briley & Tucker-Drob 2014, Psychological Bulletin. https://labs.la.utexas.edu/tucker-drob/files/2015/02/Briley-Tucker-Drob-Psych-Bull-2014-Genetic-and-Environmental-Continuity-in-Personality-Development.pdf
- Roberts, Walton & Viechtbauer 2006. https://pubmed.ncbi.nlm.nih.gov/16435954/
- Bühler et al. 2024, European Journal of Personality. https://boris.unibe.ch/185539
- Jeronimus et al. 2014, Journal of Personality and Social Psychology. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC7080961/ (cited there)
- Roberts, Caspi & Moffitt 2003 (corresponsive principle). https://scholars.duke.edu/publication/688891
- Horwitz et al. 2023, Nature Human Behaviour. https://www.colorado.edu/today/2023/08/31/news-flash-opposites-dont-actually-attract
- Humbad, Donnellan, Iacono et al. 2010. https://pmc.ncbi.nlm.nih.gov/articles/PMC2992433
- Rohrer, Egloff & Schmukle 2015, PNAS. https://www.psychologicalscience.org/publications/observer/obsonline/birth-order-has-little-effect-on-narrow-personality-traits.html
- Sibling contrast and parent ratings. https://resolve.cambridge.org/core/journals/twin-research-and-human-genetics/article/parent-ratings-of-temperament-in-twins-explaining-the-too-low-dz-correlations/B02D300B0F9363DF1D7AE77948961623
