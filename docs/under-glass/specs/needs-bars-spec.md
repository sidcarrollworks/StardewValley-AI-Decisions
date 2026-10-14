# Bars: hidden company, fun and hunger, as pressure on what people already choose

## Status

Proposed, 2026-10-13, with Sid's answers of that day built in (For Sid); nothing is built. It is the
roadmap's step 4 revisit ("then, revisit needs bars") and decides Penny's concern for her mother's
drinking with it. Paths are under `sim/UnderGlass.Sim/` unless they say otherwise; line numbers are from
`a9eadaf`, which `claude/needs-bars` (`d0f0c63`, docs only since) still matches.

Each person keeps hidden bars only they know, as they know their own tiredness. A bar never chooses: in
its band it scales, by a capped amount, numbers the town already uses, and a crossing may open a private
reflection. Penny's concern comes from her history: a **core memory**, starting data like the starting
tensions, seeds what experience would build, and a **place feeling** lets a place where someone keeps
being hurt pull them less. Nothing is keyed to a named person; all is behind switches, off by default;
off, every run pin holds (the grown towns' census pins move once).

## In brief

- **Two hidden bars first, company and fun.** Only their owner knows them. Each is measured against the
  person's own usual life, so a loner is not lonely for being alone. The player never sees them; the
  observer's viewer does.
- **A bar never decides.** When someone is lonelier or more bored than usual, it makes the things they
  already choose between lean a little: the saloon or the beach a bit more likely, a friend a bit more
  missed, an invitation a bit easier to accept. It is capped, and it adds no dice, money, act or routine
  log line. Tiredness stays as it is: it still sends people to bed.
- **Hunger is your choice:** a thin personal hunger, or (recommended, later) a household pantry that runs
  low on its own and gives errands a cause.
- **Penny stays away from the saloon because of her history, not a rule with her name on it.** She starts
  with a core memory: growing up ashamed of her mother's drinking. That memory is a lasting shame, and a
  place where someone keeps being hurt or shamed pulls them less, so the evening gathering at the saloon
  stops drawing her. She never drinks with or for Pam, Pam's drinking makes her ashamed rather than
  colder toward her mother, and on mornings she expects Pam to go drinking she asks her to stay in. Anyone
  with the same history and traits would do the same, and a relative who keeps seeing a parent drink can
  form the same concern from experience: that is how a new generation gets its own.
- **A bar running low can start a private reflection,** in the person's own words, at most one a day.
- **Law 1 is amended** to say bars are the body's pressures on the one striving, not a second list of needs.
- **Rollout:** watch only first (record what the bars would change, change nothing), then pressure on, then
  reflections, then hunger. Every piece is behind its own switch and off by default.
- **Still yours to decide:** eleven questions in "For Sid", each with a recommendation; the ones that
  matter most are the shape (1), hunger (2), how strong (4), whether Pam's yes to staying in is a promise
  (6) and the errands questions (7, 8).

## Where it comes from

**Sid.** 2026-10-06: "Not taken: needs bars (hunger, bladder, hygiene)" (design 11c). The reasons were
not written down; what was said around them: bars feel mechanical when they are the whole decision, and
constant drains fill the log with routine trips. 2026-10-13: "The needs bars are also important in
creating motive and may lead to some inspiration. These need bars can be hidden to the player if we
want." On Penny (D52): "she is ashamed of her mom's alcoholism so she despises it", "she would do
anything for her mom besides give her alcohol", "She tries to minimize her moms drinking". **Design**
11c's shape, followed here: bars known only to their owner, pressure rather than the decision, only bars
that lead to other people. Rule 18, "Core memories": every character starts with a history; behaviour
comes from traits, ties and experience.

**Elsewhere** (recalled, VERIFY): The Sims lets the emptiest need dominate; RimWorld's recreation need
has a dead band; Tynan Sylvester: hidden state nobody perceives is wasted. Stardew (1.6.15): no needs;
Pam at the Saloon every evening, Penny never (`Characters/schedules`); Penny's side is concern and shame
(`Characters/Dialogue/Penny:Mon4`, `:Fri4`; `Pam:Thu4`). Of three designs judged, this takes the pressure
design's skeleton and the story design's self-relative bars.

## Names

"Need" already means the money shortfall (`NeedPressure`), errands' `Need` and batch 2's
`DesireKind.Need` (`Model.cs:328`), so the system is **bars**, `BarKind { Hunger, Company, Fun }` (append
only); "company" is Sid's "social". "Vice" is temptation's (`Generation/TownGen.Builder.cs:681`), so a
concern is about an **indulgence**. "Association" is F10's (`FeelingOptions.Association`, the route
"Association"), so a place that pulls less has a **place feeling**. No name is used twice across types:
switch `Bars`, data `TownData.BarBook`; record `BarPull`, number `BarPullGain`; `AskKind.StayIn`, switch
`StayInAsks`, record `StayInAnswer`; switch `HungerBar`, enum `BarKind.Hunger`; record `CoreMemory`,
switch `CoreMemories`; switch `PlaceFeelings`, table `PlaceFeelingDays`.

## The design

| Bar | Taken | Why |
|---|---|---|
| Company | yes, first | Its fill exists (every chat, `Simulation.cs` `Socialise`) and its readers (the hubs, Fond, Missing, Curious, the invitation answer) lead to other people. |
| Fun | yes, first | Its fills (an evening out, a game, a drink) and readers (pastime weights, per-head draws) exist. Boredom is Stardew's first reason for Pam's evenings. |
| Hunger | Sid's choice, A or B | Nothing feeds anyone: groceries are a weekly transfer that never fails (`Simulation.Money.cs` `Payday`), homes have no kitchen. |
| Energy | already a bar | Unchanged; the recording gains it. |
| Bladder, hygiene, a craving for drink | no | Chores that lead to no one; Pam's drinking is where her fun, company and habit already lead. |

**Only its owner knows a bar**, as tiredness in `AskStateOf` (`Simulation.Invitations.cs:55`): private
state on `Person` (`Simulation.cs:224`) behind `Own(int self)`, called with the person deciding (the
chooser in `Decide`, the holder in `FondMotives` and `CuriousMotives`, the asked in `Ask`, the one drawn
for in `StartPerHead`); the replay reads `BarsOf(who)` beside `Mood()` (`Simulation.Feelings.cs:72-73`),
which no rule calls. Others learn of a bar only from deeds; the player sees none of it (design 6).

### Company and fun

| | Company: lonelier than usual for me | Fun: a dry spell for someone with a pastime |
|---|---|---|
| State | `AloneFor`, waking minutes since a good chat; `Gaps`, each of the last 14 days' longest | `SinceFun`; `Outings`, a bit a day for 14 days; `Share`, good days over days recorded |
| Fills | to 0 at a chat with someone not disliked (`Company()`'s test, regard above -`LoveAt`), family included, or a game or drink done or undergone (`AfterAct`) | to 0 at a good outing: 60 minutes (`OutingMinutes`) at a pastime in one stay, free and awake, or a game or drink. Pastimes are data: the Beach, the Saloon 17:00-24:00, the Market hub; work never counts |
| Drains | +5 a waking tick, frozen asleep, counting at work | +5 a tick, awake or asleep (counted in days) |
| Who has it | everyone, unpressed for the first `BarsSettleDays` (7) | only with `Share` at least `RegularShare` (1/7): earned, and lost after weeks without |
| Band | lonely from max(`LonelyFloorMinutes` 360, `LonelyTimes` 2 x the median of `Gaps`) | bored from max(`BoredFloorMinutes` 2880, `BoredTimes` 2.5 x 1440 / Share) |
| Pressure, shown | `pc` = clamp((AloneFor - line) / (0.5 x line), 0, 1): 0 up to the line (the dead band), 1 at 1.5 times it; the viewer shows 1 - min(1, AloneFor / (1.5 x line)) | `pf` and its gauge, the same forms |

Measured usual gaps put Sam's line at the 6-hour floor, Pam's at 7.6 hours, Penny's 10.8, Harvey's 24. At
60-minute shares, Pam (99% of days) is bored after 2.5 days without an evening out, Penny (78%) after
about 3.2, Kent (52%) after 4.8; daily regulars only when something breaks the routine: the story.
Chronic loneliness stays with 0d.6's left out (X0/X4), missing one person with Fond and Missing (X12).
With 0d.6's dials on, company pull is x(1 + min(0, stance)), inline; off (`DefaultTown.cs:187`), the
self-relative line keeps hermits possible (11e). **Children** (under 13) feel no pressure.

### Hunger: two options

**A, thin personal hunger:** 1 to 0 over `HungerAwakeHours` (12) x `Body.MaxEnergy` / 100 waking hours
(Penny 10.8, Lewis 12.6), a third as fast asleep; `ph` = clamp((0.5 - h) / 0.4, 0, 1). No act, line or
money: at or below `EatAt` (0.75) one eats at home, in the middle `LunchMinutes` (180) of one's own
working day, or at the Saloon after the day's drink (`_drank`, `Simulation.Money.cs:82`). Own rate, own
last meal: no shared mealtime (design 9).

**B, a household pantry,** for the nine households with a staple user (`ErrandBook` + `Staples`):

| | |
|---|---|
| Use | a member awake at home who hasn't eaten at home in `MealGapMinutes` (360) takes a meal: arithmetic, no draw, trip or log line |
| Shop day | each household's own, authored (`BarBook.ShopDays`): Cottage Mon, HaleyHouse and Trailer Tue, SamHouse Wed (the Mart open, the Store shut), JoshHouse Thu, Ranch and ScienceHouse Fri, Saloon Sat, Manor Sun |
| Restock | 00:00 on it, to the household's usual week (median of its last `PantryMemoryWeeks`, 4) + `PantrySpareDays` (0.25) of a day. **No money moves**: groceries stay paid at Monday's `Payday` (`Simulation.Money.cs:152-166`). The first `PantrySettleWeeks` (2) only count |
| A need with a cause | below `PantryLowDays` (1) with a day still before the shop day: the first member by name with staple uses, at home and with no open need, needs a staple on the pantry's own key (`"pantry-item"`, not the coin's `"need-item"`, so the draws aren't correlated); else the shelf waits. The coin skips anyone with an open need (`Simulation.Errands.cs:80-81`), so nobody holds two. `Need` + `Cause = "pantry"`; one open a household |
| Coin, stretch | `NeedsArise`'s coin keeps cravings only (beer, coffee, cola); a staple handed over adds `StapleDays` (2) days |

Offline (B's meal rule on the four recordings, no feedback): **own shop day, own usual week + 0.25 day,
2.0 staple needs a week**, spread over the week and eight households (1-7 of 16 weeks each). A Monday
payday at 2.5 meals a head: 3.1, 49 of 50 on a Saturday (design 9's shared clock), the ScienceHouse 16 of
16, never the Trailer; own day at 2.5 a head: 3.3; no spare: 4.5, every household.

With B, staple needs fall from about 7 a week to 2, errands from 11.7 needs a week to 6.5, Penny's flour
from 2-4 a month to about 1, with a quiet cause; no money-to-food link (it would move `NeedPressure`). A
(estimated, not run) leans every afternoon at haunts toward home (Haley at 17:00: 33% of picks to 43% at
pressure 0.52), a routine nobody would read as a cause, lowering minutes out (`CountOut`).

### Which code reads each bar, and by how much

Each reader reads the decider's own bars. A factor is min(cap, 1 + gain x pressure), the cap a number of
its own (`BarCap` 2 on options and draws, `BarWishCap` 1.5 on wishes): the gain says how soon, the cap
how far. An option serving several bars takes the **largest** pressure, never the sum (the Saloon serves
all three). Off, no reader is entered.

| # | Bar | Code | Number it scales | Change |
|---|---|---|---|---|
| R1 | company | `Simulation.cs` `Decide`, the options (`:754-767`), before the one keyed draw `("haunt", name, m)` (`:773`) | each hub's `HubWeight`; a haunt at a company place while open (the Saloon 12:00-24:00) | x min(`BarCap`, 1 + `BarPullGain` 1.0 x pc) |
| R2 | fun | the same list | a haunt or hub at a pastime in its hours (the Evening hub is at the Saloon); the Market hub | as R1 with pf; serving both, max(pc, pf) |
| R3 | company | `Simulation.Desire.cs` `FondMotives`, after `Seeking` (`:463`) | Fond's wish, or Missing's with X12 | x min(`BarWishCap`, 1 + `BarWishGain` 0.5 x pc); daring rises by at most 0.25 x the wish |
| R4 | company | `Simulation.Acts.cs` `CuriousMotives` (`:286`) | `CuriousBase` x (0.5 + chattiness) | as R3 |
| R5 | company, fun | `Simulation.Ask.cs` `Ask`, Invitation and Party (`:81`) | the state cost (`AskMath.StateCost`) | - `BarAskEase` 0.2 x max(Lonely, Bored), from `AskStateOf`'s appended fields; not shrunk by eagerness |
| R6 | company, fun | `Simulation.Acts.cs` `StartPerHead`, the threshold (`:227`) | w x `PerDay` / 288 for PlayedGame and TreatedToDrink (`Invites`) | x min(`BarCap`, 1 + `BarPerHeadGain` 1.0 x max(pc, pf)) |
| R7 | hunger (A) | `Decide`, the home option (`:767`), outside `HomeWeight`'s early return | `HomeWeight` | x min(`BarCap`, 1 + `BarPullGain` x ph) |
| R8 | hunger (A) | `Decide`, Saloon options from 18:00, adults | as R1 | hunger joins the max |

A reader moves only where the same keyed draw lands (`Rng.Unit` is a stateless keyed hash; x1.0 is
bit-identical). Fills write private state only: after `_chatted.Add` (`Simulation.cs:1184`), in
`AfterAct` (`Simulation.Acts.cs:261`), in a new `KeepBars(m)` after `RunErrands` (`Simulation.cs:598`),
in `CloseDay` (`:1323`); `Decide` reads the previous tick's upkeep. **Never read:** `Conditions`, power,
mood, tone, contagion, `Show`, speech, `Temptation`, the stay draw (`:780`), home's weight for company
and fun. **Reached indirectly**, as by any choice: R1-R2 pull adults to the Saloon, where `Drinks`
charges 12g a day (`Simulation.Money.cs:214-230`); R6 raises treats, charged to the treater's household
(`Treat`, `Simulation.Acts.cs:307`; Pam spends the Trailer's purse); place feelings and Pam's yes
evenings change who pays there after 18:00 (Penny's 42 fewer evenings: about 500g in 112 days); purses
feed `NeedPressure` (`Simulation.Money.cs:272-277`), `Conditions` (`Simulation.Feelings.cs:155-162`) and
through them power, daring, the drinker tilt and contagion. Each step measures this against watch.

Held, bed, work, patrol and errands come first in `Decide` (`:727-749`), so **no bar keeps anyone from
work, an errand or sleep**, adds an option or cuts a stay. Bars stir no motive (they strengthen Fond,
Missing and Curious only toward someone in reach) and the gate still chooses (rule 10). A close call is
drawn at most once a day per pair and act kind (`_fondAsked`, `Simulation.Desire.cs:525`), so a rising
bar never redraws one; it can lift a later weighing that day into a clear yes, within `SlotsPerDay` (2,
`:484`).

### Law 1, amended

Sid: "We should amend rule 1." Law 1 (design 3a) keeps "Everything a villager wants comes from this one
striving." Bars contradict its last sentence, "There is no separate list of needs for social life", which
this spec's own PR replaces with the words below. No bar lowers the power of acting: that would move
everyone's daring, tone and speech.

> Tiredness sends a person to bed, as it always has. The body's other pressures (loneliness and
> boredom, and hunger if it is built as a bar: the hidden bars of the
> needs bars spec) are measures of the body itself, not a second list of
> needs that decides on its own. Known only to their owner, they press by a capped amount on the
> striving's existing channels (where to go, whom to seek, what to say yes to), may give a moment for
> a private thought, and choose nothing.

### Penny's history: a core memory, a concern and a place

**A core memory** is starting data beside the tensions (`FeelingOptions.Start`, `DefaultTown.cs:186`):
`CoreMemory(Holder, About, Feeling, Over?, Place?, Strength, Sources)`, `Feeling` a sentiment name
(`Feelings.SentimentName`), `Over` an indulgence or an act kind. With `CoreMemories` on, the run's start
seeds, held at the strength, what experience would build: a `Feeling` sentiment toward About over `Over`,
and a place feeling at `Place`. Only `Ashamed` over an indulgence is read now. Shipped, for Penny only:
`CoreMemory("Penny", "Pam", "Ashamed", "drinking", "Saloon", 0.9, [Penny:Mon4, Penny:Fri4, Pam:Thu4,
D52])`, growing up ashamed of her mother's drinking (chronology to VERIFY under the character pack's
rules), with `Indulgence("drinking", ["DrunkScene", "TreatedToDrink"], ["beer"])`. The record is general:
the rest of the cast's (Penny's pack anchor "my father left us when I was a little girl" is a candidate,
and not shame) are a later, separate piece, drafted from canon with sources and chosen by Sid. Held all
run is provisional: how a core memory fades or holds is 0e's (rule 18). The pack's anchors stay inert
context, as D44 has them; its note that "a few standout anchors become core memories"
(`character-inspiration-spec.md:52-55`) is reworded: core memories are a separate record of the town's
starting data (this spec), not anchors.

**The sentiment** is the existing `Sentiment` (`Model.cs:312`, `AddSentiment`,
`Simulation.Feelings.cs:468`): it fades 0.975 a night (`:666`), keeps three a pair (the trim, `:477`),
and is read only by `Why` (`:768`) and the replay. It gains `Over`, what it is about (null on every
existing sentiment, including the `Ashamed` that `Shamed`'s regard move leaves, `:594`; part of the key,
so a concern and a scandal's `Ashamed` stay apart, and the trim and `Revert` key by it), and `Held`, a
floor it never fades below; the trim and `Revert` (`:459-461`, which deletes the sentiments citing a
reattributed act) skip a held one; `Why` skips one with an `Over`. **This is the first rule to read a
sentiment** (one with an `Over`): 0c's "As built" (design.md:175), the record's comment
(`Model.cs:309-311`) and the glossary (`sim/viewer/index.html:1127`), which say no rule reads one, change
with it.

**From experience** (`Concerns`). Kin shame follows only scandals (juiciness 4 or more; `KinHears`,
`:559`), and a drunk scene is news (3). With the switch on, believing one's kin did an act of an
indulgence whose row saddens onlookers (DrunkScene) adds the kin-shame amount, `ShameJoy` x (1.5 -
self-regard) x (0.5 + sensitivity), weighed by presence (hearsay less, law 11), to an `Ashamed` sentiment
over it. **Seeing a relative drink** counts too: Pam's nightly drink is a purse charge in `Drinks`
(`Simulation.Money.cs:214`), not an act, so with `Concerns` on, `Drinks` also notes who of the drinker's
kin is there and sees it (awake, the same place, within `FarTiles`, in sight, as `See` perceives:
firsthand only), and each such kin takes the same amount, as seen close: one mood entry on route
`Concern` at that place, renewing the sentiment (so it also builds their place feeling there). No act,
no belief, no gossip: only the kin's own feelings move, and a stranger sees nothing worth feeling. At `ConcernAt` (0.3) it is a **concern**: what follows reads it and the holder's traits, never a
named row. A sensitive child who often sees a parent drink forms one, a thick-skinned one may not: **a child
inherits traits, not memories**; how a sentiment becomes core, and generations keep theirs, is 0e's.

| A concern (holder H, about A, over I) | Effect | Where |
|---|---|---|
| (a) no drinks between them | H never offers A a kindness of I (TreatedToDrink): H's own side of `KindFits`. A's offer to H, with Invitations on, reaches H's `Ask` (`Begin`, `Simulation.cs:982`; `Asked`, `Simulation.Invitations.cs:31-50`), per-head and gate alike, and H gives a forced no, as `WontFetchAsked` does (`Simulation.Ask.cs:98`): a real, recorded `Refused`. With Invitations off nothing is asked, so A's `Fits` passes over H: the one place A's choice reads H's concern, tested | `KindFits` (`Simulation.Acts.cs:41`, the arm at `:48`), under `Fits`, which the per-head target (`Simulation.Desire.cs:155`) and the gate (`:399`) use; `Ask` |
| (b) shame, not coldness | at A's act of I, H's regard change toward A and its spill are dropped (F7, `Simulation.Feelings.cs:269`); H's mood takes the worse of the onlooker's and the kin-shame amount, one entry weighed by presence on route `Concern`, renewing the sentiment; a treat A gives someone else costs H the shame amount instead of warming them | in `Feel`, computed inline: `Shamed` (`:579`) is not called, since it would also move regard toward A (`Move`, route "Shame", `:592-594`), add `Lasting` and `Underwent`, and count toward `ShameCap` |
| (c) asking A to stay in | below | `AskKind.StayIn` (`StayInAsks`) |

For Penny at a scene seen close the onlooker's amount is the larger (-0.18 against -0.07), so her mood is
as today and only her falling regard for her mother changes; Pam's treats are rare. `WontFetch` (Penny,
beer; D52) stays; a follow-up should derive it from the concern (the indulgence's goods).

**Place feelings** (`PlaceFeelings`; Spinoza's association, III P14-P15 and P15's corollary, VERIFY).
Each person keeps a feeling for each public place, -1 to 0, from what they felt there firsthand (seen or
undergone, never hearsay): half (`PlaceShare` 0.5) of each sad mood entry on a hurt or shame route
(`Direct`, `Undergone`, `Accused`, `Confronted`, `Shame` seen in public, `Concern`) lowers it; joy there
cancels it, never past 0; it fades 0.975 a night (`PlaceKeepPerDay`); a core memory holds it at minus its
strength. In `Decide`'s options (`Simulation.cs:754-764`), before the draw, each haunt and hub there
weighs x max(`PlaceFloor` 0.1, 1 + feeling), a `BarPull` with reader `place`; never home, work, errands
or the stay. Penny has no Saloon haunt: the Evening hub draws her (`DefaultTown.cs:99`, 18:00-23:00,
weight 2 for all, `HubWeight`, `Simulation.Growth.cs:163`) against home's 1; held at -0.9 it weighs 0.2.
Without joy cancelling, Pam would be pushed from the Saloon too (Measurements).

**(c) Asking her to stay in.**

| | |
|---|---|
| When H asks | both free, awake, at home, A in reach (`InReach`: the same place, 5 tiles, in sight), 06:00-17:00, not within `StayInAgainDays` (2) of a no, after H's first night's sleep, and only when H's knowledge says A will go out to I tonight: on `ConcernNights` (3) of the last seven evenings, by the household's drink nights or H's own sightings of A at I's place (where I's acts happen, the Saloon) after 18:00 (kept three days, `Simulation.Suspicion.cs:12`). Never where A is now. Then, once a day, a keyed draw: `Rng.Unit(seed, "stay-in", H, A, day)` < strength x (0.5 + H's boldness); Penny (0.9, boldness 0.2) about 0.63 a morning, a timid or faintly concerned holder seldom. They are home together mostly at breakfast, so it works whether or not H goes out |
| Drink nights | with `Concerns` on, `Drinks` (`Simulation.Money.cs:214`) notes the day and member each time it charges a purse; only closed days are read. Law 10 extended from "the purse was short" to "whose drinks it paid for" |
| A's answer | the general `Ask`, obliged and cost as for an invitation (0.5 x liked + 0.3 x familiarity + 0.2 x chattiness + 0.2 if fond + `InviteKinBond`, against 0.15 + dislike x keep + 0.3 x withdrawn + the state cost), plus her pull the other way: `StayInHabitCost` 0.4 x her share of 14 evenings with a drink, `StayInNagCost` 0.1 a time asked in seven days (her memory), with bars `StayInBoredCost` 0.2 x her fun pressure. The margin decides outside the close band, the Ask's keyed draw (with `StayIn` in the key) inside. Pam (regard for Penny 0.60-0.65, familiarity assumed 0.9, chattiness 0.8, habit near 1): about +0.37, less 0.1 an earlier ask that week |
| A yes | no act, no promise: A's own record (who asked, the place as she heard it, and until when: H's usual bedtime as H says it, the median of H's last 14 sleeps, H's self-knowledge; Penny about 21:15). Until then A's options at that place weigh x `StayInPull` (0.1), a `BarPull` with reader `stay-in`; `Decide` reads A's record, never H's sentiment. Pam's Saloon haunt goes 4 to 0.4, the Evening hub 2 to 0.2, against home's 1: later and shorter, not never. Going anyway breaks nothing (For Sid 6) |
| A no | `Refused` by A toward H, as an invitation's; H feels it and waits two days |

### Bar thoughts

Sid: "Yes it should be able to spark a private reflection." A crossing is a moment, as waking is for a
dream (D39), never a source: a thought still needs an actual memory, and D38 is unchanged.

| | |
|---|---|
| Switch | `ReflectionOptions.BarChance`, default 0, as `DreamChance` is; the replay's `--bar-thoughts` sets 0.5. Read only with `Bars` and reflection on |
| When | a company or fun spell begins; one keyed draw, `Rng.Unit(seed, "bar-thought", actor, bar, start)` < `BarChance`. A hit is taken at the first quiet free stretch (`QuietMinutes`, 30) while the spell lasts, that day |
| Allowed | no pending thought and no request submitted that day: its own test in `Reflect`, made before the quiet path's `_reflectionDays` skip (`Simulation.Reflection.cs:77`), so a failed daily roll, which writes `_reflectionDays` (`:98`), doesn't block it; it takes the day's one thought |
| Memory | `TryReflect` takes the bar as its own path: no daily roll, its own key (`"bar-memory"`; an `Opportunity` today means `"dream-memory"` and the sleep filter, `:91`, `:101`), no sleep filter, its own phase line. An eligible actual memory (an own deed or identified firsthand encounter of the last seven days, `ReflectionMemories`, `:153`) not yet reflected on; none, no thought. The crossing alone never reopens a deferred idea: as for a dream (`:122-127`), a continuation still needs a new known source past the cooling day (`DeferredContinuation`, `:135`) |
| Carries | an appended `ReflectionRequest.Bar`, `ReflectionBar(Bar, Since, Words)`, written only when set (the `Character` and `Scene` pattern, `Reflection.cs:108`): the actor's own state in their terms, at most 120 characters ("Lonely: no real talk since 14:20 yesterday, 13 waking hours; usually about 6."); the phase line "Lonelier than usual. A private thought from an earlier known encounter, not a witnessed event." Within Laya's 1,250 characters, else the authored fallback |
| Catalog | each of the 11 thoughts in `reflection-catalog.json` gains a lonely and a bored variant, as each has a `DreamThought` (`ReflectionCatalog.cs:9`, validated at `:101`); `Propose` uses them when the request carries `Bar` and adds the tag, as it adds `dream` (`:45-52`); validation requires them |
| Replay, how often | `RecordedReflectionMind` gains a check like `SameScene` (`Reflection.cs:201`), so tapes made without it replay. One per spell and one thought a day at most: at most about 3 a week town-wide |
| Boundaries | own state only; imagination never becomes evidence; with the Claude generator the words leave the machine, so D46's list gains them; a new D-number records the opportunity beside D39 |

### Data, records, switches

**Plumbing.** A new `TownData` property never reaches the long constructor the pinned and catalog tests
use (`CatalogTests.cs:170, 183`), so the long constructor (`Simulation.cs:301`) gains a last optional
`BarBook? barBook`, defaulting to `DefaultTown.BarBook()` when `places` is null, as the economy does;
`TownData` gains `BarBook`, set by `Default()` and forwarded (`Simulation.Town.cs:7`); identity tests run
on both and assert `SimResult.BarDays` is not empty (the replay builds only with `TownData`,
`Replay.cs:75`). Core memories and indulgences sit in `FeelingOptions`, already defaulted to
`DefaultTown.Feelings()` (`Simulation.cs:308`). **Grown towns:** `Builder.Finish` returns `_core with
{...}` with `DefaultTown.Feelings()` (`Generation/TownGen.Builder.cs:817`, `:822`). `Finish` sets
`BarBook = null`, as `Errands = null` (`:814`), and every bar switch needs the book, as `Errands` does
(`Simulation.Errands.cs:51`), so a grown town keeps no bars. It keeps `Core` and `Indulgences`: its core
people are untouched (`TheCoreIsUntouched`), and indulgences name acts and goods, not people, so the path
from experience runs there too. The three census pins (`TownGenTests.cs:22-24`) move once, re-pinned with
`TownGen.Version` 3 as their comment asks; the 31-person core's census hash changes (unpinned), its run
pin `db268a57b95bada2` holds while off. `TownJson` requires every settable property
(`TownJson.cs:112-113`): dump a hand-made town file again. `TownCheck` refuses unknown names, bad hours
or shop days, a strength outside (0, 1], a `Feeling` that is no sentiment name, a memory with no source.

| New record | Fields |
|---|---|
| `BarBook` | `Outlets` (`Outlet(BarKind Bar, Place, From, To, Hub?)`; shipped: company at the Saloon 12:00-24:00 and the four hubs, fun at the Beach, the Saloon 17:00-24:00 and the Market hub, with A hunger at the Saloon from 18:00), `ShopDays?` (B) |
| `BarPull` | `(Tick, Who, Reader, BarKind? Bar, Pressure, Factor, Applied, Picked, Plain, ActId = -1)`: the plain pick the same keyed unit gives without the factor, and the pick with it |
| others | `CoreMemory(Holder, About, Feeling, Over?, Place?, Strength, Sources)`; `Indulgence(Name, Acts, Items)`; `BarSpell(Who, Bar, From, To, Usual, Place, Doing)`; `BarDay(Day, Who, LonelyMinutes, BoredMinutes, LongestAlone, UsualAlone, OutingShare, HungryMinutes = 0, Meals = 0)`; `StayInAnswer(Tick, Asker, Asked, Place, Until, Accepted, Margin, ActId, Habit, AskedThisWeek, Bored, Tired, Busy, Low, Bond, Eager)`; `ReflectionBar(Bar, Since, Words)` |

**Appended**, with defaults: `Sentiment` + `Over`, `Held`; `FeelingOptions` + `Core`, `Indulgences`;
`AskState`, `Invitation` + `Lonely`, `Bored`; `Pursuit` + `Lonely`; `ReflectionRequest` + `Bar`;
`ReflectionOptions` + `BarChance`; with B, `Need` + `Cause`, `ErrandBook` + `Staples`; `SimResult` +
`BarPulls`, `BarSpells`, `BarDays`, `StayInAnswers`, `PlaceFeelingDays`, `Pantries`; `AskKind` +
`StayIn`. JSON fields and array rows (sentiments, pursuits; `Replay.cs:259`, `:261`) carry them only when
set. No new `DesireKind`, `ActKind`, `PromiseKind` or `NeedState`. Every number is a flat `ActOptions`
first guess, in the recording's settings (`Replay.cs` `Scalars`); kin shame reuses `ShameJoy`.

**Switches**, appended to `ActOptions.Switches` after `"Errands"` (`Acts.cs:160-165`), off by default and
needing steering, so in `EveryOn()` (`CatalogTests.cs:93`) for the watch and determinism tests:

| Switch | Turns on | Step | Under `Watch` |
|---|---|---|---|
| `Bars` | company and fun kept and recorded, with every pick they would have changed; reads nothing | 1 | keeps and records |
| `Concerns` | sentiments from kin's indulgences; a concern's (a) and (b); the drink-night ledger | beside 1 | inert |
| `CoreMemories` | core memories seeded at the start (read through `Concerns` and `PlaceFeelings`) | beside 1 | inert |
| `PlaceFeelings` | place feelings, kept and read in `Decide` | beside 1 | inert |
| `ErrandHoursBusy` | errands' own-hours busy (For Sid 7) | beside 1, if taken | inert |
| `BarPressure` | R1-R6 (needs `Bars`) | 2 | inert |
| `StayInAsks` | (c), needing `Concerns`; `ActCatalog.Batch1` adds the `Refused` row for it as for `Errands` (`ActCatalog.cs:31`), and the constructor's check (`Simulation.cs:327`) covers it | 2 | inert |
| `Pantry`, `HungerBar` | B (its needs only with `Errands`); A with R7-R8 (needs `Bars`); may be on together | 4 | keep and record |

### A week with it on (estimates; nothing here has been run)

- **Spells and picks.** About 5 lonely and under 1 bored spells a week; single-digit changed picks.
- **Penny's evenings.** With her core memory and place feelings on, from the first evening the hub weighs
  2 x 0.1 = 0.2 against home's 1: 56 x (0.2 / 1.2) / (2 / 3) = 14 of 112 evenings at the Saloon at 19:00,
  from 56; a lonely night lifts it to 0.4 at most. The 14 is set by the record's strength, not her traits
  or her run: anyone given the record would avoid the Saloon as much.
- **Penny without the core memory** (learning it). From Pam's scenes alone she never would: she learns of
  one a month, three in four by hearsay; one seen close adds 0.07 (0.05 x (1.5 - 0.3) x (0.5 + 0.67)),
  fading x0.975 a night, so near 0.1 against 0.3. With seen drinks she is with Pam at the Saloon on
  about half her evenings and sees her drink on many of them: at 0.07 a sighting, the concern forms in
  about four to five sightings, two to four weeks of her evenings out. From then each drink she sees costs
  her shame there, and the place feeling builds, but her good times there (chats, games) cancel some of
  it: whether she then stops, and how fast, is step 1's first measurement (For Sid 11).
- **Asks.** On about 0.63 of the mornings Penny could ask (both at home awake about 72 minutes a day):
  clear yeses, then close calls as the nag term grows; near 3 asks and 2 yeses a week.
- **Pam on a yes evening.** A toy model (the Saloon x0.1 until 21:30, about Penny's bedtime;
  60-180-minute stays, `Simulation.cs:780`; without a yes, 98% of evenings there from 16:55, 111 of 112
  measured): she still goes before then on two thirds of yes evenings, at a median 18:35, for 3.5 hours
  rather than 6.5, with her drink; her habit barely moves, so yeses don't compound.

## Measurements

None of this has been run. Seeds 1, 7, 13, 29, 28 days, the shipped town with
`--feel on --desire on --catalog returns,company,welcome,repair,sides,late,invitations,errands`; bars,
places and pantry worked out afterwards from where everyone was. Longest waking stretch without a chat,
median day: Harvey 12.0 hours, the Newcomer 10.5, Leah 9.7, Haley 7.2, Gus 6.6, Penny 5.4, Pam 3.8.

| Form | Spells a week, town | Who |
|---|---|---|
| any absolute six-hour line | most days for three people | the Newcomer 28 days of 28, Harvey 19, Leah 16, Marnie 10, Gus 7, Haley 6 |
| a draining bar against one's own average (company; fun, regulars only) | 4 to 17; 7.1 | two thirds the Newcomer, Harvey, Haley and Leah; Vincent, Jodi, Marnie |
| **twice one's usual longest gap, six-hour floor** | **5.2** | **21 people, none more than about once in two and a half weeks** |
| **fun: 2.5 times one's usual gap, two-day floor** | **0.6** | **Caroline 2; Demetrius, Harvey, Kent, Lewis, Pierre 1; no daily regular** |

Days with an hour or more at a pastime (`runs/fungap.py`): eight people 93-100%, Pam among them; nine
69-87%, Penny 78%; six 44-61%, Kent 52%; Evelyn, George and Gus under 20% (no bar). At half an hour, a
few points higher, 0.5 bored a week.

**Pam and Penny** (reproduced by a second reader). Pam is at the Saloon on 111 of 112 evenings from a
median 16:50, 5.9 to 6.6 hours; she sleeps at 00:33, Penny at 21:15. **Penny is at the Saloon on 56 of
112 evenings at 19:00**, at home on 47; at 21:00, home or asleep 40, the Saloon 31. Pam's regard for
Penny is 0.60-0.65, her power 0.53-0.56. Pam made 4 drunk scenes in the 112 days; Penny saw 1 and heard
of 3; Pam treated nobody (`scripts/pennyshame.py`).

**Place feelings, offline** (`scripts/placenet.py`: acts aimed at someone at a public place, felt at the
row's joy x (0.5 + sensitivity), half to the place, fading 0.975 a night; no feedback or chats). Hurts
only: 18.8 person-place-days a week below 0.8, Pam at the Saloon 47 of 112 (argued with by Lewis and
Alex, mocked by Alex), down to 0.1. **Cancelled by joy there: 6.8**, Sam at the Saloon 22, Pam 18, the
lowest 0.34 (Shane, the Saloon).

## For Sid

**Answered (2026-10-13), and built in above:**

- **Penny and the Saloon:** "Yes Penny should stop going to the saloon. Ideally her choice not to go
  comes from the system we create, but i'm not quite sure how we do that without hardcoding it. If these
  characters were to exist in the same way throughout the course of the game it would be fine but I want
  for traits to be passed on through the generations." Her core memory and place feelings.
- **Law 1:** "We should amend rule 1." The words are under "Law 1, amended".
- **Inspiration:** "Yes it should be able to spark a private reflection." Bar thoughts, step 3.
- **A history for everyone:** "all the characters should start with some sort of history ... 'Core
  memories' that the characters hold on to. Eventually we'll have to design the mechanics for new
  generations to keep core memories." In rule 18; Penny's first, the rest later, generations in 0e.

**Open:**

| # | Question | Recommendation |
|---|---|---|
| 1 | **The shape:** bars as hidden, capped pressure on numbers the town already uses; company and fun first; R1-R6 | Yes. If no, nothing about bars is built; Penny's history can still be |
| 2 | **Hunger:** A, B or neither | **B, as step 4**, confirmed by its watch run. Its own shop day and usual week keep design 9's dropped "shared hunger clock" (design.md :528) out: a Monday fill puts 49 of 50 low shelves on a Saturday, a flat 2.5 meals a head makes the ScienceHouse low 16 weeks of 16 and the Trailer never. A's cost is an estimate. If neither, the errands' coin stays |
| 3 | **Company and fun relative to one's own life**, family chats counting | Yes; if family didn't count, everyone who lives with family and stays home would be lonely |
| 4 | **How strong** | A rule for step 1's would-be changed picks a week (median of six seeds): under 10, raise `BarPullGain` to 2 and `BarCap` to 3 and watch again; still under 5, cut company and fun; over 60, or anyone over one a day, lower `BarPullGain` (the story design's 20-60, widened at the bottom). If step 2 moves short households' purses or power against watch, cut R6 to games |
| 5 | **Penny's strength and the place floor;** a core memory held all run; the indulgence's list; others knowing adding to her shame; her own drink | 0.9 and `PlaceFloor` 0.1: about one evening in eight at the Saloon (estimated; a `PlaceFloor` of 0.25, or a strength of 0.75, gives one in four, a strength of 0.25 about 50 of 112; Stardew's never needs a floor near 0, which stops capping the law), confirmed at step 1. Held at full strength, never trimmed, joy never softening its place: provisional until 0e decides how core memories fade or hold. The indulgence as written (drunk scenes, buying rounds, beer). Others knowing: not now. Her own 12g drink on the evenings she still goes stays the body's rule |
| 6 | **The stay-in ask:** the drink-night ledger as household knowledge (law 10 extended); no give-up timer beyond the two-day wait (canon's "out of my hands", `Rainy_Night_1`, would be one); Pam free after Penny's usual bedtime; **a yes is not a promise** | As described. Rule 13 (design.md :242) makes any deal with a future part a Promise; as one, Pam's going anyway (about two thirds of yes evenings) would break it each time, and Penny's regard would fall, the coldness (b) removes. The observer still sees "agreed, went at 18:40". Otherwise a `PromiseKind`, appended |
| 7 | **Errands, one's own hours** (`ErrandHoursBusy`) | Busy when the asked person's own working hours cover every minute the counter's sign is open until the errand is due, at the existing busy weight shrunk by eagerness times the bond; beside step 1. All 12 broken promises in the needs spec's runs had this cause (Robin 7, Lewis 3, Demetrius 2) |
| 8 | **Errands, unpaid as debts** | Later: a payback the purse can't cover becomes a `Tab` (`Promises.cs:4`) due at the next payday, with batch 2's Debts; measured meanwhile |
| 9 | **Place feelings for everyone:** with joy cancelling, about 7 person-place-days a week below 0.8, Pam's Saloon about one day in six | Accept and measure; if Pam's evenings thin against canon, `PlaceShare` 0.25. Homes and the joy side (a loved place pulling more) wait |
| 10 | **Names**, as above | Confirm |
| 11 | **Learning it without a core memory,** for generations: from Pam's scenes alone nobody would (4 in 112 days, 1 seen) | **Count a seen drink** (above): a relative who keeps seeing a parent drink forms the concern in weeks, a thick-skinned one may not, a stranger never. Step 1 measures whether Penny, without her core memory, then stops going and how fast; if her good times there keep cancelling the place feeling, the fix is to let joy shared with the person one is ashamed of not cancel it (a tuning question, measured before it is decided) |

## Steps

This spec's own PR carries law 1's words and the character pack's reworded note. Each step then has its
own PR with its docs (this spec's Status, design 11c and "As built", a new D-number, the roadmap,
`sim/README.md`, the needs spec's For Sid); step 1, which builds `Concerns`, also changes the three
places that say no rule reads a sentiment (sooner would call a plan built) and re-pins the grown towns'
census. The steps hook into errands code, only on `claude/needs` (`a9eadaf`, PR #90, which
`claude/needs-bars` stacks on), not `codex/reflection-prototype` (`779e3b8`): step 1 waits for PR #90 and
targets that branch, or stacks on `claude/needs`.

| Step | Switches | What it adds, and what it reports |
|---|---|---|
| 1 | `Bars`; beside it, each alone, `Concerns`, `CoreMemories`, `PlaceFeelings`, and `ErrandHoursBusy` if taken | Company and fun kept and recorded, energy for the first time; every pick or per-head draw a bar would have changed, with the would-be result (`BarPull`, Applied false); invitations and pursuits carry the pressure they would have had; the log byte-identical. Under the global `Watch` switch the haunt draw and Fond and Missing (R3) can be watched (`Pursue` still runs, and Fond acts on the shipped `GaveGift`, which no slice gates: `Fits`, `Simulation.Acts.cs:22`; `CatalogTests.cs:187`), Curious (R4) shows in the catalog's watch record, and per-head draws, invitations and errands don't run (`Simulation.Acts.cs:214`, `Simulation.Invitations.cs:27`, `Simulation.Errands.cs:51`). Six seeds of 56 days and a year of seed 1. Penny's Saloon evenings at 19:00 week by week (today 56 of 112) with the three history switches on, and with `CoreMemories` off (learning from seen drinks and Pam's scenes): whether she stops, and how fast |
| 2 | `BarPressure`, then `StayInAsks` | R1-R6, then the stay-in ask; new pins for bars on |
| 3 | `BarChance` | bar thoughts, after step 2, so the bars that prompt a thought also change deeds |
| 4 | `Pantry` or `HungerBar`, if taken | a watch run keeping both, to confirm the choice and tune `PantrySpareDays`; then the one chosen |

Measured at each step (diagnostics, not quotas): spells, changed picks, dead-band time; goal changes
against watch (at most +5%); hermit spells, minutes out, left out, `OutLate`; Pam's Saloon evenings,
hours, scenes and their lag-1 autocorrelation; Penny's evenings and regard for Pam; stay-in asks and yes
evenings; place feelings below 0.8; bar thoughts; cash to the gram; short households' purses,
`NeedPressure`, power, drinks, treats, paybacks below zero; with B, needs by cause, weekday and
household. A placebo run (bars read from another person) should collapse the story measures.

## Tests

| Area | What holds |
|---|---|
| Off | every pin: feelings off `e7f6653087ff7e18`, the placed scandal `388d128d7fd4cdf2`, the shipped year `e9fd83b284f5c1b6`, the 0c town `c0488cc6bf81e64f`, the gate variants, the 0d.6 steps, the slices; the 31-town's `db268a57b95bada2`. The grown towns' census pins (`8b18e34b4b315814`, `933d25e7f62ecdcd`, `6c3ef2b45d89c41f`) move once, with `TownGen.Version` 3, and a grown town has no book. No new tables |
| Kept, not read | `Bars` on gives `e9fd83b284f5c1b6` with `new Simulation(1, ...)` and `new Simulation(1, TownData.Default() ...)`, and on both `SimResult.BarDays` is not empty; `CoreMemories` alone seeds Penny's held sentiment on both; with every gain at 0, `BarPressure` gives the run without bars; `WatchingEverySliceChangesNothing` holds with the new switches in `EveryOn` |
| Maths | a pure `BarMath`, as `AskMath`: pressure 0 up to the line, 1 at 1.5 times it; the floors; the share rule; the largest pressure, not the sum; `BarPullGain` 2 reaches `BarCap` 2 at half pressure and never passes it, `BarCap` 3 reaches 3; a place factor within [`PlaceFloor`, 1] |
| Bar scenes | the errands' small world: Ann alone crosses at her line and her next pick weighs the hub x(1 + pc); a chat with Bob resets her, one with Dee, whom she dislikes, doesn't; asleep is frozen; Bob, usually alone 12 hours, isn't lonely at 10, Cal, usually 3, is at 6; a child is never pressed; a beach-goer held three days is bored on the third, an hour at the beach ends it; work is no outing; each reader moves by exactly its term and records it; a pressed worker at 8:55 still goes to work; a `BarPull`'s plain pick is the same unit without the factor; Fond rising through a day turns an earlier close no into a clear yes only within `SlotsPerDay`, never redrawing a close call |
| Knowledge | side by side, one lonely: the other's picks, wishes and answers are the same until a deed passes between them; no rule calls `BarsOf`; hearsay builds no place feeling; no rule names a person |
| History | a held sentiment and place feeling never fade below the strength, and neither the trim nor `Revert` removes a held one; a concern and a scandal's `Ashamed` toward one person stay apart; Ann, kin of a drinker, forms the sentiment from weekly seen scenes, a thick-skinned Ann doesn't in the same days; seeing a relative buy a drink counts only for kin, awake, in the same place, within `FarTiles` and in sight, never by hearsay, and makes no act, belief or log line; `TownCheck` refuses a core memory with no source; a hub where Ann was hurt weighs exactly x(1 + feeling), joy cancels it but never past 1, home untouched; Penny never offers Pam a drink; with Invitations on Pam's offer, per-head or gate, is a recorded `Refused` by Penny; with them off Pam's `Fits` passes over Penny; at Pam's scene Penny's regard for Pam is unchanged and her mood falls by the worse amount, one entry, route `Concern`, no `Lasting`, no regard move, `ShameCap` untouched, while Leah watching feels as today |
| Stay-in | Penny asks only at home, in reach, before 17:00, on expected nights, the same whatever Pam's true place; the start draw is exactly strength x (0.5 + boldness), so a bolder Ann asks more often; a yes weighs Pam's Saloon options x0.1 until Penny's usual bedtime, read from Pam's own record (removing Penny's sentiment after the answer changes nothing); the nag term grows by exactly 0.1 an ask; a no is `Refused` and Penny waits two days |
| Bar thoughts | `BarChance` 0 changes nothing; a hit opens one thought at the next quiet stretch from an eligible memory, never with none, never a second that day, not blocked by a failed daily roll; a crossing with no new source never reopens a deferred idea; the catalog refuses a thought without its lonely and bored variants; the words only when set, at most 120 characters; a tape made with it off replays |
| Pantry, errand hours | the restock moves no money; each shelf fills on its own day to its own week plus the spare; nothing is noticed while settling; a low shelf makes a staple need with cause `pantry` on the `"pantry-item"` key, from a member with no open need; the coin draws cravings only. A weekday nine-to-five worker asked on a Monday for Store flour due Wednesday night is busy, for Saloon beer not, with the switch off neither |
| Recording | the new tables only with their switches; an old recording shows none; `sim/tools/test-viewer-bars.cjs` holds the gauges, the pull sentence, the stay-in story and the history line |

Each test is checked, in the repo's style, by planting the defect it guards against: a rule reading
another's bar or place; a factor with its switch off; a sum for the max; an uncapped factor; a child
pressed; Penny reading Pam's place; shame through regard or `Shamed`; Pam's nag count or stay-in read
from Penny; Pam's draw reading Penny's concern with Invitations on; the book left on a grown town; a held
sentiment fading, trimmed or reverted; hearsay building a place feeling; joy lifting a place past 1; a
staple on the coin's key; a crossing alone reopening a deferred reflection.

## Recording and the viewer

**Recording** (`Replay.cs`), each table only with its switch (so also under watch), as the errands'
(`:301-308`): `bars` hourly at minute 59 beside `mood` and `power` (`:116-119`), company, fun and energy
through `BarsOf` (energy is new: `Where()` returns it, the sample discards it, `:108`); `barSpells`,
`barPulls`, `barDays`, `stayInAnswers`, `placeFeelingDays`, `coreMemories`, with B `pantry`; existing
tables gain their new fields when set.

**Viewer** (`sim/viewer/index.html`), guarded on its tables and clock-gated: the inspector
(`drawInspector`, `:2987`) gains "Hidden: only they know" gauges with a band word ("lonely: alone 13
waking hours since a chat at 14:20 yesterday, usually 6") beside mood, power and stance (`:3016-3031`),
the history with its sources ("a core memory: ashamed of Pam's drinking; Penny:Mon4, ...") and places
that pull less; the life record lists pulls ("stayed home: Penny asked her to, x0.1"); `popRow`
(`:2725-2739`) gains lonely and bored hours; under the clock, the stay-in story ("Pam said yes, and went
anyway at 18:40") and a bar thought's moment; the glossary says the player sees none of this.

## Risks

| Risk | Guard |
|---|---|
| Chaos: one changed pick changes everything after it | off enters nothing; with it on, compare distributions across seeds |
| Too subtle to see (Sylvester) | step 1 counts would-be changed picks; raise once, then cut (For Sid 4) |
| Routine trips | dead band; self-relative lines; nothing added or cut short; the pantry sized to each household's week and day |
| Money and power by the back door | indirect reach measured against watch; R6 can be cut to games |
| Hermits pulled to the middle; places emptied | self-relative lines; children unpressed; home untouched; place feelings floored, cancelled by joy, faded, measured |
| Counted twice: loneliness; the Saloon serving all three (the Joy Booth) | Fond and Missing scaled and capped, Seek must read this bar; the largest pressure, not the sum |
| A rule for one person | a core memory is data; kin shame, concerns and place feelings apply to anyone; a source test |
| Mechanical loops (Pam yes, yes, no) | varying inputs; lag-1 autocorrelation measured |
| A knowledge back channel | Penny reads closed ledger days and her own sightings, never Pam's place; places read only their holder's moods; Pam's `Fits` reads Penny's concern only with Invitations off, tested |
| Chores | glossary pointers drift (`GlossaryTests.cs:119`; `UNDERGLASS_REPOINT=1`); new code in `Simulation.Bars.cs`, `Simulation.History.cs` |

## Next

Unpaid errands as a `Tab`; `WontFetch` from the concern; Seek reading the company bar (so loneliness is
counted once); homes and the joy side in place feelings; the pantry's money link; boredom in temptation
(rule 17 lists "bored" under thrill); a concern for Shane, Stardew's case of cutting back with a friend's
help; the cast's other core memories, and 0e's; what a player-controlled Newcomer is shown of their own
bars.
