# Actions and twists: what life sims teach Under Glass

> **Current direction (2026-10-09):** this is a research inventory and historical batch plan.
> Use the [current roadmap](roadmap.md) for priorities and acceptance criteria. Concrete
> encounters and contextual appraisal come next; the old news ceiling and E1/E2 quotas below
> describe earlier experiments, not requirements for new work.

*Research, 2026-10-08 (task C9 in issue #46). It draws on five research reports (Sims, Sims mods, simulation games, cozy and research games, variety across runs) and on the repo's specs (`acts-spec.md`, `town-spec.md`, `design.md`, `sim/README.md`). The shared web-search budget ran out before this pass, and direct page fetches failed at the proxy. So the facts here come from the reports' search-result text. Anything recalled but not confirmed there is marked (verify).*

*What became of it: `Variety.cs` builds section 3's story events and variety measures (step 0 of section 4; `sim/README.md`, "Variety"). The batch 2 spec (`specs/acts-batch2-spec.md`) specifies the former step 2; its scenario harness, story measures and forks are built, but its remaining actions are planned. Other entries remain a catalog to draw on. See `sim/README.md` for current implementation status.*

**Sid's answers this builds on (2026-10-08):**
- Grow Pelican Town, starting at 60 people.
- A scandal's reach depends on context, so the fixed band goes.
- Forgetting follows the strength of the relationship.
- Acts follow every recommendation in the acts spec:
  - gate acts come from traits;
  - hearing about yourself counts at corroboration strength (5b);
  - lies and vandalism come last, are off by default, and can always be found out (7b);
  - places and promises come next (8a);
  - news may rise at most 10% (9a).

**Tone.** Dark themes are fine. There is no murder, no sexual violence and nothing sexually explicit. Romance stops at kissing, courting, engagement and marriage. Excluded mechanics are named only to rule them out.

---

## 1. Inspiration catalog

**How to read a row.**
- **Fit** gives the motive, the class and tier with base juiciness (J), and who would do it.
- J under 1.5 is never retold. J 1.5-1.9 is told on the day to people who know the person. J 2-3.9 is news. A bad act at 4 or more is a scandal.
- **Status** is one of: shipped, batch 1, batch 2, backlog (acts spec section 10), or new.
- **Earlier shortlist** marks the 18 originally recommended after batch 1; they are backlog
  candidates, not the current next milestone.
- New motives used below:
  - **Smitten:** attraction above a threshold, from a seeded chemistry per pair.
  - **Jealous:** design 11a, III P35.
  - **Strive:** a life goal (section 3).

### 1.1 Everyday

Already in the town: LateForWork (batch 1); Stumbled and Collapsed (shipped).

| # | Act | Source | What it makes happen between people | Fit | Status |
|---|---|---|---|---|---|
| 1 | Visited, JoinedThem | Sims home visits; LittleMsSam Social Activities | A walk to someone's door, or over to someone sitting alone. Finding nobody home teaches the visitor the host's hours. | Fond, Pity, Curious, Seek; Light; anyone | batch 2, **Earlier shortlist** |
| 2 | AteTogether | Sims family meals; LittleMsSam Call to Meal | Supper as a fixed time when the household meets, and so a time for rows | Fond; Light 0.5; households | backlog |
| 3 | BroughtFood | Animal Crossing sick villagers; Sims 4 Life & Death support (verify) | Care after an illness, a birth or a death | Pity; Story 1.5; the kind and understanding | new |
| 4 | RanErrand (go-between) | Stardew: Clint's gifts for Emily; Animal Crossing delivery errands | A third person carries a gift or note, and can be late with it or open it (row 61) | Fond (sender), Return; 1.0; the shy send, the chatty carry | backlog |
| 5 | Pastime | Sims 2 FreeTime hobbies; Sims 4 likes | People who share a hobby at a haunt bond over it | Fond; Light 0.5; hobby carriers | backlog |
| 6 | Mended, Taught | Sims 4 Get Famous (repairing and mentoring raise reputation); CK3 mentorship | Work made visible, and a debt of gratitude | Return, Pity; 1.0-1.5; Robin, Maru, Penny (verify) | backlog |
| 7 | Vented | Sims 4 Vent and Whine; Vanilla Social Interactions Expanded (RimWorld mod) | Eases the speaker's mood. It also spreads the speaker's version of who hurt them. | low mood with a friend in reach; 1.5; the expressive | new |
| 8 | WanderedOff, HidAway | RimWorld minor breaks (sad wander, hide in room) | A visible absence that people notice and talk about | withdrawn stance plus low power; 1.5; the shy | new |
| 9 | Binged | RimWorld binge; CK3 coping trait (Drunkard) | A run of saloon nights after a bad stretch. More drunk scenes follow. | low power; feeds DrunkScene; the sad who drink | new |
| 10 | FakedSick | Cozy report: "called in sick", then seen at the beach | A lie that sightings undo | want, low power; news 2.0 once found; night owls | new (a lie: built last) |
| 11 | StoppedComing | acts spec | A keeper notices that a regular has gone | derived from the keeper's ledger; 1.5 | backlog |
| 12 | BoughtFromRival, Bought, FoundItClosed | design rule 12; Stardew's Joja coupons scene | Pierre against the chain, in public | errands; Ledger and Light | batch 2, **Earlier shortlist** |

### 1.2 Friendly

Already in the town: HelpedSomeone and GaveGift (shipped).

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 13 | Thanked | Sims friendly socials | Closes kindness the record now marks Ignored | Return; Light 0.5 | batch 1 |
| 14 | Complimented | Sims friendly socials | A small return, and the shy's answer | Return, MakeUp, Fond, Remorse; Light 1.0 | batch 1 |
| 15 | Welcomed | Sims Welcome Wagon (verify) | How newcomers and new neighbourhoods meet | Curious; 1.5 | batch 1 |
| 16 | Comforted | Sims 4 consoling | A bad day becomes the start of a friendship | Pity, Remorse; 1.5 | batch 1 |
| 17 | DeepTalk, Confided | RimWorld deep talk (+15 opinion for 20 days); Stardew: Shane opens up at 2 hearts; CK3 confidants; design rule 14 | A long private talk. Past a trust tier, the listener is handed a secret, and the secret is now theirs to keep or spend. | Fond; needs familiarity 0.5 and nobody within 8 tiles; 1.5 (the secret is the payload); the chatty and trusting | backlog (Confided), **Earlier shortlist** |
| 18 | AskedAbout | Sims 4 "Ask About Another Sim": the answer follows how the person asked feels | A question that comes back with an opinion of a third person | Curious; Light 0.5; the chatty | new (needs answer 5b) |
| 19 | GaveAdvice | Sims 4 "Friendly Advice"; Neighborhood Stories (friends phone before a decision) | The adviser's own regard colours the advice, so advice can steer | Pity, Fond; 1.0; the understanding | new |
| 20 | SpokeWellOf | Sims 4 Talk Up; design law 7 | Praise reaches a third person | Fond; 1.0; the warm | backlog, **Earlier shortlist** (with row 50) |
| 21 | Reminisced | CK3 memories; Sims 2 memories talked about | Old friends recall a past read from the life record | Fond; Light 0.5; elders and old friends | new |
| 22 | Mediated | Animal Crossing villager fights; Tomodachi Life (a mutual friend settles a fight) | A friend of both sides brings a feud to the table | Pity or Fond toward both; news 2.0; the understanding and bold | new |
| 23 | OfferedPeace, MadePeace | acts spec batch 2; design law 8 | A way out of a long feud | MakeUp, Remorse; Story | batch 2, **Earlier shortlist** |
| 24 | LentMoney, then Repaid, ForgaveDebt or CalledInDebt | design rule 13; LittleMsSam ATM Cards (loans) | A Promise with a due date. An unpaid debt becomes a hook, and later a scandal (BrokePromise, base 4). | Pity, Fond; 1.0-1.5; the hard-up ask | backlog, **Earlier shortlist** |
| 25 | Hosted (and LeftOffTheList) | Sims parties; NPC Party Invites mod; CK3 feasts with intents | A guest list. Being left off it is a snub. Feuding guests end up in one room. | Fond, Strive, an inspiration; news 2.0; the warm and bold | new, **Earlier shortlist** |
| 26 | Invited, then MetUp, StoodUp or Refused | Sims dates; design rule 13 | The first Promise. StoodUp is base 3. | Fond, MakeUp, Return; Story | batch 2, **Earlier shortlist** |

### 1.3 Funny

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 27 | Joked | Sims Funny socials | The "only joking" thread | Fond; Light 0.5 | batch 1 |
| 28 | PlayedGame | Sims; RimWorld chitchat | Friendships among the young | Fond; Light 1.0 | batch 1 |
| 29 | Pranked | Sims 3 Generations pranks (a teen caught is grounded); Deeper Social Autonomy (playful versus mean mischief) | Playful or spiteful, depending on the motive. A teen who is caught gets a family row. | Fond or Retaliate; news 2.0; teens and jokers | backlog |
| 30 | Performed | Sims 4 Get Famous and the Great Acoustics lot trait; Sam's band in Stardew (verify) | A public turn at the saloon that can go well or badly | Strive, an inspiration; news 2.0; Sam, Abigail, Leah (verify) | new |
| 31 | Blundered | Sims 3 Embarrassed moodlet; the Stardew Luau soup | A public failure that people remember. Mocked can follow. | a mishap (no motive); 1.5, news at a festival; anyone | new (a festival version of Stumbled) |
| 32 | Danced | Stardew Flower Dance (verify); Sims dancing together | A pairing the town reads as courting | Fond, Smitten; 1.5 at festivals; the young | new |
| 33 | Wagered, CheatedAtGame | acts spec backlog; Qi's casino in Stardew (verify) | Bets on PlayedGame. Cheating that is found out becomes a scene. | want, thrill; news 2.5; Pam, Shane (verify) | backlog |

### 1.4 Romantic (non-explicit)

All of these use design 11a: the same rules for everyone, and the same rules for the player.

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 34 | Flirted | Sims Romance socials; Sims 4 Lovestruck attraction | A visible sign of interest that onlookers and partners read | Smitten; Light 0.5; the bold and warm | new, **Earlier shortlist** |
| 35 | ConfessedFeelings, then Courting or TurnedDown | Tomodachi Life (rejection brings a slump; a second try can get a different answer); Sims 3 Confess Attraction; Prom Week (the responder accepts or refuses) | The Ask. Acceptance starts courting, a life choice held for a minimum time. Refusal hurts, and can push the shy toward withdrawal. | Smitten above a threshold; news 2.5; anyone free | backlog (Confessed), **Earlier shortlist** |
| 36 | Courted (in stages) | CK3 romance scheme; Harvest Moon rival heart events | Steps over weeks (a gift, a dance, a date) that the town watches | Fond and Smitten; 1.5 a step | backlog (courting) |
| 37 | KissedInPublic | Sims | Makes a couple news. It is a scandal when one of them is spoken for. | Fond; news 2.5; couples | new |
| 38 | JealousScene | Sims 2 Nightlife; Portia's jealousy devlog; Tomodachi jealousy fights; Lumpinou (jealousy graded by what was seen) | A partner confronts someone after seeing a flirt. Graded: seen chatting, flirting, kissing. Whoever turned an advance down is not blamed. | Jealous; news 2.5; the sensitive and the retentive | new, **Earlier shortlist** |
| 39 | Proposed, then Engaged or RefusedProposal | Sims; RimWorld (a refused proposal hurts for about two seasons, forum report); Harvest Moon | Done at a festival, a refusal is a public humiliation | Fond in a steady courtship; news 3.0 | new |
| 40 | BrokeUp | Sims; RimWorld; Tomodachi (after a split, a lasting state from "enemies" to "would try again") | Friends take sides | low regard; upheaval | backlog (upheavals) |
| 41 | SecretAdmirer | Lumpinou hand-written love letters | An unsigned gift starts a "someone" mystery | Smitten plus shyness; 1.5; the shy | new |
| 42 | Introduced (matchmaking) | Sims 2 Nightlife matchmaker (verify) | Two people come to know each other through a third | Fond toward both; 1.0; the chatty | new |
| 43 | Meddled | Sims 4 Get Together "Ask to Break Up" (verify) | Someone urges a couple to split | a rival's Smitten, or Retaliate; news 2.0; the bold | new |
| 44 | Eloped | CK3 Elope scheme | A couple goes ahead against the family. Family rows follow. | Fond against regard for kin; upheaval | new |

Excluded: WooHoo, RimWorld's "Lovin'", CK3 Seduce, and paternity plots.

### 1.5 Mean

Already in the town: Argued, Snubbed and TurnedAway (shipped); Mocked and StoodUpFor (batch 1).

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 45 | MadeScene (including a thrown drink) | Sims Throw Drink; Nightlife fury | Loud: heard through walls without knowing who | Answer at intensity 0.6 or more | batch 2, **Earlier shortlist** |
| 46 | Banned, ShowedTheDoor | design rule 16; Sims 3 (rude visitors are thrown out) | The keeper's answer. The home version is new. | the keeper's Retaliate | batch 2, **Earlier shortlist** |
| 47 | Mocked, StoodUpFor | Sims Mean socials | A target, a defender and someone who comforts | Answer, Defend | batch 1 |
| 48 | Gloated | Sims 3 Evil trait (taunting others in misfortune); law 5 (glad at a hated person's sadness) | Spite made visible | Retaliate after the other's misfortune; Light hostile; the bold who are low in understanding | new |
| 49 | FrozeOut | Stardew group 10-heart event (about a week of cold shoulders); Royalty & Legacy shunning | Several people with a shared grievance turn someone away together | a shared grievance found in talk; news 3.0 | new |
| 50 | BadMouthed | Sims 4 trash talk (verify); BitLife rumours | A true story told with malice. Under answer 5b, the target may hear of it. | Retaliate, Answer; J by content; the chatty and retentive | backlog, **Earlier shortlist** |
| 51 | Complained to the mayor, then HadAWord | Animal Crossing "Discuss a resident" (reports conflict on whether it pushes a villager out) | Slow collective pressure, and a way for a story to reach the authority | Retaliate; news when the mayor acts | backlog, **Earlier shortlist** |
| 52 | AskedToTakeSides | Façade affinity games | Forces a mutual friend to choose | Answer aimed at a third person; news 2.0 | new |
| 53 | TargetedInsults | RimWorld major break (an insulting spree aimed at one pawn) | A combative break aimed at the believed cause | combative stance plus low power; news 3.0 | new |

**Scuffle** (RimWorld social fight: 4% per insult, multiplied by drink) is within the stated limits. The specs have kept violence out so far, so it is Sid's call. It is not proposed here.

### 1.6 Mischief

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 54 | Snooped, Eavesdropped | Sims 4 For Rent (eavesdropping, snooping); Royalty & Legacy (trash rummaged to find secrets) | Learns a secret. A scandal if caught. | Curious, envy; news 2.5 if caught; a "nosy" hook | new (needs rule 14; build last) |
| 55 | OpenedTheirParcel | Animal Crossing (a present you were asked to deliver, opened, irritates the sender) | A small breach of trust that may reveal a secret | Curious; 1.5, news if found | new |
| 56 | KeptFoundItem, ReturnedLostItem | Animal Crossing lost items; rule 15 already lists lost items as an occasion | A moral choice that leaves a trace: the item is seen in their home later | want, or a wish to return it; 1.5, or a scandal at 4 if found | new, **Earlier shortlist** |
| 57 | RanUpTab, then PaidOffTab | design story 4 (Joel's tab); Pam in Stardew (verify) | A Promise at the saloon. The keeper tells someone. It gives Pam a way out besides the bin. | need; 1.5, then BrokePromise at 4 | new, **Earlier shortlist** |
| 58 | Vandalised | Royalty & Legacy (defacing a monument); Nightlife fury vandalism | Damage to property only, which leaves a trace | Retaliate at a keeper or an official; scandal 4.0 | backlog (answer 7: last, off) |
| 59 | LiedAboutWhereabouts | Drama Mod (MizoreYukii) "Lie About Where You've Been" | Sightings undo the lie, since everyone remembers who they saw where | covering another act; news | new (7: last, off) |
| 60 | Slandered, ShiftedBlame | Sims 3 Late Night (shifting the blame); Dwarf Fortress false reports; Talk of the Town lies | Blame moves to someone else, until a listener checks | Retaliate; as retold | backlog (7: last, off) |
| 61 | PostedAnonymously | Life's Drama "Burn Book" notes for the whole town to read | A true secret on the board reaches everyone at once | Retaliate; reaches the whole town | new (off by default) |
| 62 | Swiped | Sims 4 Kleptomaniac (tense when they haven't stolen in a while) | A small theft inside a home, once visits exist | want, thrill; scandal 4.0 | new (a version of Stole) |

### 1.7 Scandalous

Already in the town: RummagedInBin and Stole (shipped). Section 2 gives each kind's reach.

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 63 | SkimmedTheTill | Dwarf Fortress villains (embezzlement); Animal Crossing public works | Takings or a town fund go missing. A recount finds it, as a trace. | need, want; 4.5; keepers' households, organisers | new |
| 64 | TookBribe, LetOff | Dwarf Fortress corruption; CK3 hooks; Stardew (Lewis pays tokens to hush the shorts) | An official sways a verdict. Rule 16 already lets Lewis off someone close 2% of the time. | Fond for the accused, or a hook; 4.5 | new |
| 65 | RiggedContest | Rune Factory contests; Stardew Grange (Lewis judges, verify) | A judge or an entrant cheats at a festival | pride, a rival; 4.0 | new |
| 66 | SpoiledThePot | Stardew Luau soup, tasted by the Governor | One act judged by everyone at once. Sabotage and carelessness look alike. | spite or carelessness; news or scandal | new |
| 67 | Blackmailed | Royalty & Legacy extortion; For Rent blackmail; CK3 (refused blackmail is exposed); Law & Disorder (Lumpinou: blackmail is reportable) | Silence traded for money or a favour | want, Retaliate; 4.5 when exposed | new (last, off) |
| 68 | RevealedSecret | design rule 14 (Betrayed 0.8); CK3 Expose | The confidence is broken and blame moves along the tellers | Retaliate, the gossip vice; 4.0 | backlog, **Earlier shortlist** |
| 69 | BrokePromise | design rule 13 | An unpaid debt or a missed order | derived; 4.0 | backlog (with row 24) |
| 70 | TwoTimed | Stardew group 10-heart event; Sims 2 and 3 cheating; Tomodachi love triangles | Courting two people at once, found out when they compare notes | Smitten for both; 4.0 | new |

### 1.8 Community

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 71 | Campaigned | CK3 Sway; design 6a influence | Canvassing before the constable vote | Strive; news 2.0; candidates | new |
| 72 | ProposedRule, Petitioned | Sims 4 Eco Lifestyle Neighbourhood Action Plans (votes by influence; 5 signatures repeal a plan) | The town changes its own rules, and sides form | Strive, grievance; news; the bold | new (design 6a) |
| 73 | Protested | Life's Drama protests; Sims 3 University rebels (verify) | A public stand against the chain, a ban or a verdict | Retaliate at an institution; news 3.0 | new |
| 74 | Donated, Volunteered | Sims 4 Get Famous (donating raises reputation); Animal Crossing public works | Public kindness; standing rises | Fond for the town, Strive; 1.5 | new |
| 75 | EnteredContest, then WonContest or SoreLoser | Rune Factory contests; Stardew Grange; Sims 4 Cottage Living fair (verify) | Status at a festival, and envy | Strive, pride; news 2.0-2.5; the competitive | new, **Earlier shortlist** |
| 76 | FilledRequest | Stardew Help Wanted (+150 friendship, 3× price); design section 7 board | Someone answers a posted need | Pity, Return; 1.5 | new |
| 77 | ClubMeeting | Sims 4 Get Together (club rules apply only while the club meets) | A group's norms bend behaviour while it meets | Fond; Light; members | new (design 11c) |
| 78 | Vouched | Dwarf Fortress witnesses (verify); The Guild trials (verify) | Speaks up for a suspect who is not kin | Defend, Fond; news 2.0 | new |
| 79 | CalledAFestival | Rune Factory 4 Orders and 5 Directives (verify) | An official or a winning proposal sets a gathering | Strive; an occasion | new |

### 1.9 Life events

All need phase 0e unless noted.

| # | Act | Source | What it makes happen | Fit | Status |
|---|---|---|---|---|---|
| 80 | Arrived | Animal Crossing campsite; RimWorld population intent; Neighborhood Stories moves | A newcomer's first weeks: Welcomed, Curious, prejudice by kind | an occasion; Sid's forgetting model (new faces) | new |
| 81 | LeftTown, AskedToStay | Animal Crossing (move-outs weighted toward low friendship; a warning first, verify); Harvest Moon (Karen leaves) | An upheaval with warning, which friends can answer | long pressure; Fond for AskedToStay | backlog (upheavals) |
| 82 | LostJob, Promoted | Sims 2 chance cards (18% a workday, odds per choice); Neighborhood Stories careers | Status changes, with shame or pride | pressure; news 2.5 | new |
| 83 | FellIll, Recovered | Sims 1 guinea pig disease; Slice of Life colds | An occasion; visits and BroughtFood follow | rule 15 illness; 1.5 | new |
| 84 | Windfall, Inheritance | Sims 4 Eco Lifestyle inheritance call; Life & Death wills | A household's pressure changes, and a will can split kin | an occasion; news 2.5 | new |
| 85 | Engaged, Married | Sims weddings (Royalty & Legacy "Abandoned at the Altar", Medium) | A public event with everyone present | upheaval | new |
| 86 | Birth | Sims; Memorable Events "Meet the Baby" | Visits, and a family that grows | upheaval | new |
| 87 | DiedOfOldAge, Funeral | Sims 4 Life & Death (grief kinds, eulogies); design 11c | Grief and a gathering where old feuds meet | upheaval; natural deaths only | new |
| 88 | Retired | Neighborhood Stories retirements | A shop or a role changes hands | life choice; news | new |

**Excluded:**
- accidental or violent deaths;
- child neglect, and children taken away (design 11a puts abuse out of scope);
- drug-dealing mods.

---

## 2. Scandals and reach

### 2.1 Fourteen kinds for a small town

Each has a motive, so scandals still come from pressure, not dice.

| # | Kind | Tier | Made public by | Kept small by | Inspiration |
|---|---|---|---|---|---|
| S1 | **Affair found out** (TwoTimed, or kissing someone spoken for) | scandal | the two compare notes; seen at a hub or festival; a JealousScene in public | one witness who keeps quiet; asked silence | Stardew group event; Sims 2 and 3 cheating |
| S2 | **A public figure's secret courtship** | news, or a scandal if someone is spoken for | the evidence object is found (Lewis's shorts); a confrontation at the meeting | the couple asks the witness for silence. Keeping it builds trust, and a hook. | Stardew Lewis and Marnie (+50 kept, −100 told, per the report) |
| S3 | **Broken engagement, jilted at the wedding** | upheaval | everyone is at the wedding | called off quietly before the date | Royalty & Legacy (Medium); Sims weddings |
| S4 | **Secret debt revealed** (unpaid tab or loan) | scandal (BrokePromise 4) | the creditor tells, or raises it at the bar | paid off before the due date | rule 13; design stories 3 and 4 |
| S5 | **Skimmed the till or the fund** | scandal 4.5 | a recount (a trace); a public verdict | quiet repayment; kin cover | Dwarf Fortress; Animal Crossing public works |
| S6 | **Bribe or partial verdict** | scandal 4.5, about the authority | the victim sees no consequence; a town meeting; an election | the official's standing; few who know | Dwarf Fortress; CK3; rule 16 sway |
| S7 | **Rigged contest** | scandal 4.0 | the festival crowd; a sore loser who checks | the judge and the cheat both hold it as a secret | Rune Factory; Stardew Grange |
| S8 | **Spoiled the pot** | news to scandal | everyone tastes it, so it reaches the whole town at once | blame is unclear and falls on whoever stood near | Stardew Luau |
| S9 | **Public humiliation** (a scene, a refused proposal, a blunder) | news; travels like a scandal at a festival | the venue | an apology; friends comfort | Royalty & Legacy humiliation; Sims 3 Embarrassed |
| S10 | **Lie exposed** (false alibi, faked sick) | scandal when it covered a wrong or misled the constable | sightings; an interview | nobody checks | Drama Mod; LittleMsSam outings (last, off) |
| S11 | **Vandalism** (property only) | scandal 4.0 | a trace seen in daylight at a hub | repaired before morning | Royalty & Legacy; Nightlife fury (last, off) |
| S12 | **Family secret** (an old debt, a past elopement, a disowned child, a will) | scandal or news | a retold confidence; a will read out | families cover; it stays a family row | CK3 secrets; Life & Death wills. No parentage secrets |
| S13 | **Blackmail exposed** | scandal 4.5; the blackmailer becomes the culprit | the target refuses and reports it | paid silence | Royalty & Legacy; For Rent; CK3; Law & Disorder (last, off) |
| S14 | **Shop closed or sold** | upheaval | a board notice; the door shut | a rescue (Donated, a loan) before the date | design upheaval tier; Stardew Joja |

### 2.2 What decides reach

| Factor | How it works in the simulator | Status |
|---|---|---|
| **Witnesses and venue** | the exposure at birth: Private (0-1 witnesses, a home or the edge of town), Seen (2-4), Crowd (5 or more, or a hub), Town (a festival or the meeting) | Witnesses and hubs exist. Festivals as town-wide gatherings are new (the dates are already in `Calendar`). |
| **Juiciness** | base J by kind, faded by the existing rules. +0.5 for a public figure (mayor, constable, keeper, doctor, teacher). Wonder (law 13): more when the act is unlike the person. | the bonuses are new |
| **Who is involved** | credulity: a listener closer to the accused than to the teller holds it at low confidence and doesn't retell it (Prom Week). Kin feel shame. Strangers are only "someone". | credence exists; the closeness test is new |
| **Evidence and traces** | traces exist (a scattered bin, missing stock). New: an object trace that lasts (a kept item, the shorts) and that a visitor finds later, which gives a delayed exposure. | partly built |
| **Kept in the family** | kin never report, retell, suspect or confront | built |
| **Settled quickly** | restitution plus an accepted apology before the second retelling. Later retellings carry half the juiciness and go only to people who know the culprit. | new |
| **Covered up (hushed)** | the culprit or kin asks each known holder for silence (the Ask). Those who agree stop retelling and hold it as a secret at a trust tier. If a holder's regard for the culprit later falls below 0, it can leak, and a leak adds the cover-up (+1 J). | new (needs rule 14) |
| **Denied** | the denial travels with the story. Listeners who trust the accused more than the teller don't believe it. | new |
| **Confronted or announced** | a public confrontation, a verdict read at the meeting, or a board post: everyone present learns it at once | the confrontation exists; announcements are new |

### 2.3 Scenario checks, replacing the 40-70% band

Each check places a chosen kind in a chosen scene: a harness option with a kind, a place, a time, a witness count and the events that follow. Reach is counted over the actor's circle, meaning everyone at familiarity 0.2 or more toward the actor (town spec, question 2). At 60 people the whole-town share is reported too.

| # | Scenario | Pass (first guesses) |
|---|---|---|
| C1 | Done at a festival | 90%+ of the circle within 2 days, in 90%+ of seeds |
| C2 | A crowd at a hub (5 or more witnesses) | median 50-90%; 25%+ of seeds above 70% |
| C3 | One loner witness at the edge of town, no trace | 20% or less in 70%+ of seeds; the story dies in half (design story 1, seed B) |
| C4 | Found only from a trace | median 30% or less |
| C5 | Settled within a day | median at most half of the same scene left unsettled |
| C6 | Seen only by kin | nobody outside the household knows by day 7 in 95%+ of seeds. Leaks through an overheard row are reported. |
| C7 | Hushed, every holder agreeing | at most the holders plus one for 14 days in 80%+ of seeds. Leaks are reported; aim for some, as a twist. |
| C8 | Denied: a well-liked culprit against a disliked one | 30%+ fewer believers for the well-liked one |
| C9 | Announced at the meeting or on the board | 90%+ of the town in 1 day |
| C10 | Witness count against reach | rises with witnesses (rank correlation 0.5 or more) |
| C11 | Natural scandals over 200 seed-years | median 40-70%; 10%+ reach 90%; 15%+ stay under 20%. That is many small and a few town-wide: Watts's "robust yet fragile" cascades (PNAS 2002). |
| C12 | At 60 people | a scandal inside one neighbourhood stays mostly local (circle share at least 2× the town share); a festival scandal crosses neighbourhoods within 2 days |
| C13 | Today's placed scandals | 52%+ still land in 40-70%, now reported rather than gated |

C3, C4, C10, C11 and C13 can run today on the town's two scandals.

---

## 3. Twists and turns: making seeds differ

**Today's town repeats itself:**
- Sam and Shane feud in 109 of 200 seed-years.
- Pam commits most tempted scandals.
- The newcomer is withdrawn in 374 of 400 seed-years.
- Raising a trait often changes nothing.

So the lever is situations, not traits. **The rule throughout: dice make situations, and minds make choices.** Every draw below is keyed by name (`Rng.Unit(seed, "deal", …)`), so a switch that is off draws nothing and every pin holds.

| # | Mechanism | How it fits a seeded simulator | Cost | Measure |
|---|---|---|---|---|
| 1 | **Story events and a variety gate** (Felt and Winnow-style patterns over the log: Feud, SecretOut, BlameMoved, Upheaval, and so on) | reads logs only | low | V1-V8 below |
| 2 | **The deal: a different start**. Drawn per seed: 2-5 tensions from a list of 10-15, each with a cause both people remember; which households start short or in debt; per-pair chemistry and compatibility (RimWorld, Sims 2 Nightlife); one hook per person (nosy, proud, competitive, romantic, spendthrift; Sims Medieval flaws, verify; Wildermyth hooks). Canon fixed points stay. | written at day 0 only, into cards and old memories. Extend the test that occasions can't write minds. | low-medium | V1: Sam and Shane from 55% to 30% or less; V2 |
| 3 | **Seeded secrets** (rule 14): 1-3 per adult, plus some shared by pairs, each leaving traces | day-0 facts and beliefs; needs Confided and RevealedSecret | medium-high | secret-out events per seed-year; share of twists that are fair |
| 4 | **Life goals** (aspirations: open a café, win the Grange, become constable, clear a debt, marry, reconcile with someone, leave town) | Strive feeds existing motives; goals can fail | medium | goals met and failed; entropy of which goals succeed |
| 5 | **Upheavals from weak points, with warning.** A weak point (old roof, lease, debt, a buyer's interest) is drawn at setup. Pressure raises a daily chance. Warnings come days ahead (board, letter). At most one at a time, two a year, 28 days apart. | pressure, not dice | medium per kind | upheavals per year; kinds across seeds; warning lead time |
| 6 | **Storyteller temperament** over occasions only: Steady (Cassandra), Gentle (Phoebe), Erratic (Randy, with a forced event after 13 quiet days) | a schedule drawn at setup that weights weather, prices, visitors, illness and lost items. It never chooses acts or minds, and never forecasts. | medium (needs the occasion catalog) | V7 |
| 7 | **Rare spikes.** Inspirations at high power (host, make, woo, reconcile). Breaks by stance at low power, with catharsis afterwards. | thresholds on power and stance; keyed draws | low-medium | spikes per seed-year, each citing its cause |
| 8 | **Named rivals and loves**: one rival and one romance per person, with hysteresis and a remembered reason (Wildermyth; CK3 relations) | thresholds over regard | low | V5 |
| 9 | **Arrivals and departures.** Weak ties raise the chance of leaving (Animal Crossing). Newcomers come when the town is small or stale (RimWorld population intent). Festival tourists feed forgetting. | phase 0e; reuses T1 forgetting | high | turnover; entropy of who left |
| 10 | **Fixed dates, open outcomes**: votes, contests, meetings | `Calendar` exists | low-medium | entropy of winners (the constable is already 8 people; Pierre 37%) |
| 11 | **Town mores per run** (RimWorld Ideology; Dwarf Fortress ethics): how much drinking, chain shopping, gambling or open courting offends | multipliers on J and onlookers' joy | low code, high risk to readability | which kinds become scandals across seeds |
| 12 | **Surprise in gossip** (law 13) | J plus how far an act departs from what the observer expects | medium | recheck C11 |

**The variety gate** (from the variety report; first guesses over 200 seed-years):

| # | Measure | Target |
|---|---|---|
| V1 | Same named feud pair, culprit or hermit across seed-years | 30% or less |
| V2 | Effective number (exp of Shannon entropy) of each year's headline story event | 20 or more |
| V3 | Seed pairs that share 80% of their story events | under 5% |
| V4 | Seed-years holding an event that appears in under 2% of seed-years | 60% or more |
| V5 | Each person's commonest arc | in 50% of seed-years or fewer |
| V6 | Twists (reversals and revelations) per seed-season | median 2 or more |
| V7 | Copies of a run split at day 28 that differ in a major event by day 112 | 25-60% |
| V8 | Conflict × cohesion clusters | 3 or more |

**Fair-twist rule.** A twist counts only if:
- its cause chain has 2 or more links;
- at least one earlier trace was visible.

---

## 4. Historical recommended order (2026-10-08)

0. **At the time, alongside acts-0.**
   - Build the story events and the variety baseline (V1-V8).
   - Build the scenario harness and run C3, C4, C10, C11 and C13 on today's town.
   - Nothing changes behaviour.
1. **Batch 1** as specified.
2. **Batch 2, places and promises** (Sid's choice 8a).
   - Its kinds: Visited, Invited, MadePeace, MadeScene, Banned, BoughtFromRival.
   - Festivals become town-wide gatherings.
   - New kinds riding the same machinery: Hosted, RanUpTab, LentMoney, KeptFoundItem and ReturnedLostItem, EnteredContest.
   - New reach levers: Settled and Announced, which make C1, C5 and C9 testable.
   - First new scandals: S4 (debt), a scene at a festival (S9), and S6 from the 2% let-off.
   - **First twist: the deal (mechanism 2)**, built in parallel as new files.
3. **Batch 3, secrets and talk.**
   - Acts: DeepTalk and Confided, RevealedSecret, BadMouthed and SpokeWellOf, Complained (answer 5b).
   - Rule 14's trust, and the Hushed and Denied levers.
   - **Twist: seeded secrets.**
4. **Batch 4, courting.**
   - Acts: Flirted, ConfessedFeelings, the courting life choice, JealousScene.
   - Couples forming and splitting as upheavals.
   - Scandals S1-S3, with chemistry taken from the deal.
5. **Upheavals with warnings, the storyteller over occasions, then life goals.**
6. **Phase 0e:** arrivals, departures, births, deaths and time skips.
7. **Last, each off by default (answer 7b):** lies (rows 10, 59 and 60), Vandalised, Blackmailed, PostedAnonymously and Snooped, each with a way to be found out.

**Earlier experiment budgets, superseded as product gates.** The 10% news ceiling, E1 bands,
three-year drift targets and E2's 20% threshold were the criteria for this batch plan. Keep the
measurements and explain changes; they do not establish whether an encounter is interesting.
New slices use the roadmap's scene review and required causal, privacy, resource and replay
checks. Existing regression tests remain until an intentional implementation change updates them.

**Historical open questions for the relevant backlog features:**
1. Is a non-lethal scuffle allowed?
2. May the storyteller react to measured town tension, or only follow a schedule drawn at setup?
3. How far may the deal move a core villager?
4. Should a new game avoid repeating your recent runs?

---

## Sources

From the reports' search-result text; no page could be fetched.

- **Sims scandals and secrets:**
  - EA Help, "How Scandals Work": https://help.ea.com/en/articles/the-sims/the-sims-4/scandals/
  - Royalty & Legacy dev diary: https://www.ea.com/games/the-sims/the-sims-4/news/royalty-and-legacy-dev-diary
  - For Rent secrets: https://simscommunity.info/2024/01/11/the-sims-4-secrets-guide/
- **Sims systems:**
  - Sentiments: https://simscommunity.info/2021/11/06/the-sims-4-all-sentiments-and-their-effects-packs-included/
  - Sims 2 memories: https://strategywiki.org/wiki/The_Sims_2/Memories
  - Chance cards: https://sims.fandom.com/wiki/Chance_card
  - Neighbourhood Action Plans: https://simscommunity.info/2020/06/10/the-sims-4-eco-lifestyle-influence-neighbourhood-action-plans/
  - Get Famous reputation: https://simscommunity.info/2018/11/15/the-sims-4-get-famous-reputation/
- **Sims mods:**
  - Drama Mod: https://snootysims.com/wiki/sims-4/sims-4-drama-mod/
  - Lumpinou: https://lumpinoumods.com/2020/12/26/woohoo-wellness-pregnancy-overhaul-module-7/
  - Law & Disorder: https://simscommunity.info/2026/03/16/the-sims-4-law-disorder-mod-guide/
  - Life's Drama: https://gamersdecide.com/articles/sims-4-best-mods-for-drama
  - Meaningful Stories: https://roburky.itch.io/sims4-meaningful-stories
  - NRaas StoryProgression: https://www.nraas.net/community/StoryProgression
- **Stardew:**
  - Garbage Can: https://stardewvalleywiki.com/Garbage_Can
  - Breakup event: https://stardewvalleywiki.com/Breakup_event
  - Marnie: https://stardewvalleywiki.com/Marnie
  - Mayor's shorts: https://stardewvalleywiki.com/Mayor’s_shorts
  - Help Wanted: https://stardewvalleywiki.com/Help_Wanted
- **Animal Crossing:**
  - Move-outs: https://nintendoeverything.com/animal-crossing-new-horizons-datamine-provides-details-about-how-villagers-move-out/
  - Favors: https://www.nookipedia.com/wiki/Favor
- **Tomodachi Life:** https://simscommunity.info/2026/07/25/tomodachi-life-fights-living-the-dream-guide/
- **RimWorld:**
  - Social: https://rimworldwiki.com/wiki/Social
  - Mental breaks: https://rimworldwiki.com/wiki/Mental_break
  - AI storytellers: https://rimworldwiki.com/wiki/AI_Storytellers
  - Precepts: https://www.rimworldwiki.com/wiki/Precept
- **Dwarf Fortress:**
  - Memory: https://dwarffortresswiki.org/Memory_(thought)
  - Justice: https://dwarffortresswiki.org/Justice
- **CK3 and research games:**
  - CK3 secrets and hooks: https://primagames.com/gaming/crusader-kings-3-intrigue-schemes-hooks-secrets
  - Prom Week social exchanges: https://promweek.soe.ucsc.edu/2012/02/22/prom-weeks-social-exchanges/
  - Talk of the Town: https://ojs.aaai.org/index.php/AIIDE/article/view/12825
  - Felt: https://github.com/mkremins/felt
- **Variety:**
  - Watts 2002: https://pmc.ncbi.nlm.nih.gov/articles/PMC122850/
  - MusicLab: https://pdodds.w3.uvm.edu/research/papers/years/2006/salganik2006a.pdf
  - "10,000 bowls of oatmeal": https://emshort.blog/2016/09/21/bowls-of-oatmeal-and-text-generation/
  - Wildermyth repetition: https://www.rpgsite.net/review/12107-wildermyth-review
- **Repo, read only:**
  - `docs/under-glass/specs/acts-spec.md`
  - `docs/under-glass/specs/town-spec.md`
  - `docs/under-glass/design.md`
  - `sim/README.md`
  - `sim/UnderGlass.Sim/Calendar.cs`
  - `sim/UnderGlass.Sim/Harness.cs`
