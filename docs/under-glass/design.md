# Under Glass

*Design draft 2, 2026-10-05. Draft 1 was the brainstorm; this version folds in Sid's notes: layers of visibility, a world that goes on without the player, the name, Spinoza as the basis for how people act, and the engine choice.*

## 1. The recommendation in brief

Make a farming game in a small town that runs on about fifteen simulation rules instead of a script. Villagers see what happens around them, more clearly the closer they are and the longer they watch, and remember it. They retell the juicy parts, hold a feeling toward every other person and act on motives. There is no hearts meter, no gift cap and no heart event. The player farms and trades as in Stardew. They answer the town with the emotion wheel, the notice board, the promises they keep or break, and where they choose to be.

The first thing to build is a headless town simulator: 12 villagers, 5 locations, one season, player bots and metrics over 1000 seeds. It starts as a gossip-only harness. A text REPL comes next so Sid can play before any art exists.

## 1a. Principles

1. **The world goes on without you.** No rule checks where the player is, except perception: what the player sees and who sees the player. Every villager runs at full detail all day, every day, whether or not the player is nearby. Nothing waits for the player to arrive.
   - Things that happen leave traces the player can find later: a note on the board, changed shop hours, a broken fence, two people who now walk together, a story going around, a cold greeting.
   - The player learns about the world the way villagers do: by seeing it, being told, or reading the board.
2. **Knowledge has layers.** Seeing something from 2 tiles away for a while is not the same as glimpsing it at 8 tiles through a fence. What a villager saw decides what it knows, how sure it is, and how strongly it feels (rule 2).
3. **People act from their nature, not from a script.** The rules for how a person is moved and what they want come from Spinoza's account of the affects (section 3a).
4. **The player is one more person.** The same rules apply to the player as to any villager. The player has no meters of their own.
5. **Every consequence can cite its cause.** If the game shows that something changed, it can point to the event, and to who saw or told it.

## 2. What we learned from the mod

- **No motive, no act.** The urge clock put Linus at the top of the table without him seeing the player. Acts gated by a motive and a boldness cost fixed it (D24).
- **Feelings need two parts.** The elastic part comes from 3 days of diary and fades. The plastic part, regard, is saved per pair and scaled by retention. Pam forgets and Robin keeps.
- **Gossip by juiciness works at 34 villagers.** Haley's sunflower story reached Alex, and Marnie hit the 3-listener cap. At 10 villagers the same settings saturate: in a Monte Carlo test, scandals reached everyone in 76% of seeds.
- **Only scandals heard secondhand lead to action** (D34, Gus and Shane's fish). **Diaries forget the least important entries first** (D35, Emily's diary).
- **A deterministic score decides, and the model only steers close calls.** Laya's yes/no answers bunched between 0.47 and 0.60, so "I saw you" beat a birthday gift (D21).
- **Pacing rules that worked:** two attempt slots, asking again only when the situation changes instead of a cooldown, waves never counted as ignored, and a minimum motive strength for letters (D28).
- **Stardew's engine blocked emergence. The core ideas held up.** These all go away in our own engine:
  - fixed schedules;
  - one conversation per villager per day;
  - a dialogue stack that wipes our lines;
  - a clock that stops during events;
  - a farm that NPCs cannot walk onto.
- **Every tuning fix came from the viewer and the JSONL log,** so both exist from day one.

## 3. Assets and IP

Decided (Sid, 2026-10-05): prototypes may use Stardew's town, cast and art privately, or as a mod that needs the base game. If Under Glass becomes its own game, every asset is redone: an original town, an original cast built from archetypes, and new art, music and writing. That includes the data this repo decoded from the game (`fixtures/game/*`, the temperament seeds computed from Stardew dialogue).

Two items remain before any sale: a LICENSE for the public repo, and a lawyer's read of Warner Bros' Nemesis patent (the research says it runs to 2036; it covers NPC relationships that change with the player's actions).

## 3a. The Spinozan core: how a person is moved, and what they want

Spinoza's *Ethics* (Part III, "On the Origin and Nature of the Affects", and Part IV) treats people as natural things moved by causes, the way physics treats bodies. That suits a simulation: it gives a small set of laws from which every feeling and want is derived, with no authored story. Below, each law is stated, then what it becomes in the game. The proposition numbers are from memory and should be checked against a translation (Curley's is the standard English one).

**1. Conatus: each person strives to keep and increase their power of acting (III P6-P7).**
Each villager has a power of acting, `P` from 0 to 1. Health, money, home, work, rest, company and recent events raise or lower it. Everything a villager wants comes from this one striving. There is no separate list of needs for social life.

**2. Three primary affects: joy, sadness and desire (III P11).**
- *Joy* is a rise in `P`, and *sadness* a fall.
- *Desire* is the striving itself, aimed at something.

Every event a villager perceives produces joy or sadness of some amount. Mood is the recent trend of `P`.

**3. Love and hate are joy and sadness with the idea of a cause (III P13, scholium).**
Regard toward someone is the running sum of the joy and sadness a villager attributes to them. The key word is *attributes*: the feeling attaches to whoever the villager *thinks* caused it. If the villager couldn't tell who did it (rule 2), the feeling attaches to "someone", to a place, or to a kind of person (law 9).

**4. Desire follows: keep what brings joy, remove what brings sadness (III P12-P13, P28).**
Motives are desires about a cause. Toward someone they love, a villager wants to be near, help, give, and keep them well. Toward someone they hate, they want distance, or to see them diminished.

Harm is held back by fear of greater harm (III P39). So hostile acts need more than hatred: they also need a low expected cost. This is the boldness cost from the mod, now with a reason behind it.

**5. Feelings follow the people we love and hate (III P19-P24).**
When a villager sees or hears that someone was affected:

| Villager's regard for the person affected | That person's joy | That person's sadness |
|---|---|---|
| love (above +0.2) | the villager feels joy, and loves the cause | the villager feels sadness, and hates the cause |
| hate (below -0.2) | the villager feels sadness, and comes to hate the cause (envy) | the villager feels joy (they are glad of it) |
| neither | law 6 applies | law 6 applies |

This replaces the bystander rule of draft 1. It produces friend-of-friend warmth, taking sides, envy and spite from one table.

*As built in 0c (2026-10-06):* this table is the felt amount f0 of rule 6 below. Two readings of Spinoza await Sid (0c questions 2 and 3): someone who hates the person harmed comes to *love* whoever harmed them, not only feel glad (III P24); and a witness with no strong regard cools a little on whoever harmed someone like them, not only on nobody (III P27 cor. 1). Both are built.

**6. Imitation of the affects: we feel what we imagine someone like us feels, even without prior feeling for them (III P27).**
A villager with no strong regard for the person affected still feels a fraction of that person's joy or sadness: pity, or shared gladness. The fraction is scaled by how alike they are (household, age, work, kind of person) and by the observer's temperament (law 12). This is what makes a crowd react to a fall or a fight.

**7. Ambition and conformity: we strive to do what we imagine others regard with joy (III P29, P31).**
- Each villager keeps an estimate of how the town feels about each kind of act. The estimate is learned from the reactions it has witnessed.
- Each villager leans toward acts it expects to be welcomed, and away from acts it expects to be frowned on.
- It also wants others to love what it loves, so it praises the people and shops it likes when it talks.

Norms are not written anywhere. They come from what people have seen others react to. In a town that laughs at rudeness, rudeness spreads.

**8. Reciprocity (III P33, P40-P41, P43-P44).**
- Who imagines themselves loved, loves back. Who imagines themselves hated, hates back. Hate returned increases.
- Hate answered with love can be overcome. When it is, it turns into a love greater than if there had been no hate.

This gives feuds that escalate, and reconciliations that are stronger than plain friendship, both from the same rule.

*As built in 0c:* "who imagines themselves loved, loves back" is the direct attribution of an act aimed at them (rule 4); "hate returned increases" is that attribution plus whom acts are aimed at (rule 10). Hate conquered by love (III P44, VERIFY): each pair keeps the lowest point of a hate (at -0.2 or below) and the love received since; when love carries regard past zero, it gains back min(depth of the hate, love received since). In a test, a pair that argued (-0.3) and then made up with three kindnesses ends at 0.315, against 0.287 for a pair that never fought.

**9. Feelings spread to a kind of person (III P46).**
If a villager is affected by someone it knows only as a kind of person, it loves or hates the kind:
- "the new farmer";
- "a Joja worker";
- "someone from out of town".

This is prejudice. It fades as individual knowledge replaces the category. It ties to perception: a far witness who couldn't tell who rummaged in the bin holds it against "a farmer", or "someone".

**10. Blame depends on freedom (III P49; V P3, P6).**
Love and hate are stronger toward someone believed to have acted freely than toward someone believed to have been compelled.
- An accident, a known hardship ("he was broke that week") or a cause from outside reduces blame.
- Understanding why something happened weakens a passive feeling about it.

The player's apology, correction and explanation verbs work this way. They do not erase what happened; they supply a cause. Occurrences from the occasion catalog (weather, breakages) produce less blame than chosen acts.

**11. Presence and time (IV P9-P13), and contrary feeling (IV P7).**
- A feeling about something present, or seen first-hand, is stronger than one about something remembered or heard. Near things move more than distant ones. This is the basis of hearsay weighing less than witnessing, and close witnesses feeling more than far ones.
- A feeling can only be overcome by a stronger contrary feeling. A grudge does not dissolve on a timer; it fades slowly, and it yields to new joy from the same person.

**12. Each person is moved in their own way (III P51), and some act more from understanding (Part IV, the free person).**
- The temperament traits from the mod (warmth, sensitivity, boldness, forgiveness, chattiness) set how strongly each law acts on a given villager.
- Add one trait, *understanding*: how much a villager acts from adequate ideas rather than passions. A villager high in understanding imitates feelings less, generalises less to kinds of people, blames less when a cause is known, and forgives more.

**13. Wonder at what is new (III P52).**
Something unusual holds attention and is retold more: a newcomer, a strange act, an unexpected visitor. Novelty adds to juiciness and fades as it becomes familiar.

What this gives the design: rules 4 to 10 below are these laws with numbers attached. Each constant is a first guess for the simulator to test.

## 4. The fundamental rules

Every constant here is a first guess, to be tested in phase 0.

1. **Time, space and bodies** (decided 2026-10-05; see 11b).
   - **The clock runs all 24 hours.** The date changes at midnight. There is no end of the day: nobody is sent home at 2:00, and nothing resets at 6:00. A week is 7 days, a season 28, a year 4 seasons. All of these are parameters.
   - **Pace.** A game day takes about 20 real minutes, so a game minute is 0.83 real seconds. We set it from play later, once there are mechanics to judge it by.
   - **Two time steps.**
     - Routines, movement decisions and chats run on 5-minute ticks.
     - Acts and perception run minute by minute: an act lasts minutes, and a witness must be looking during those minutes.
     - Every act and diary entry carries its game minute.
   - **Places.** Locations have named spots with tile coordinates (counter, bar stool, bench, field row). Roads join them. People walk about 2 tiles a game minute, so they are on the roads between places, where they see and are seen.
   - **Energy and sleep.** Everyone, the player included, has an energy bar. Each person's maximum is different.
     - Being awake uses energy; work uses more, depending on the job.
     - Anyone can go to sleep whenever they like. Villagers go to bed when they feel tired enough, not at a set hour. Feeling tired is the share of the bar used plus a body clock (sleepy in the small hours, alert in the afternoon), against each person's own threshold (night owls stay up later). The body clock keeps everyone's day near 24 hours.
     - Rest drains a share of each person's own bar; work and walking cost a fixed amount on top, so a bigger bar takes hard work in its stride.
     - Sleep refills energy and stops when the bar is full: nobody can sleep longer than that.
     - Anyone can set an alarm. An alarm may not wake a sleeper: the chance of waking rises with how full the bar already is. A villager who sleeps through it is late for work, and people notice.
     - At zero energy a person collapses where they stand. The collapse is an act that others can see, and the person is taken home.
   - **Jobs make the routines.** A villager's day comes from their job (place, hours, days off, how tiring it is), their haunts for free time (place, hours, weight), and their energy. The player's day comes from the same parts, chosen by the player.
   - **Acting normal.** Because bedtimes and routines come from each person, the town has a sense of what is usual for whom. Being up at odd hours, or somewhere unusual, is something a villager can notice and talk about. For the alien, keeping human hours is part of fitting in (11a).
     - *As built in 0a (2026-10-06):* everyone learns, from what they see, the hours each person keeps out of doors. Seeing someone out at night (22:00-6:00), at an hour when you have never seen them out, nor the hour either side, once you have seen them out for 6 hours or more, is a news story about them (`OutLate`, juiciness 2). It happens about twice a season, mostly to the town's night people (Shane, Sam, Pam, Sebastian, Abigail). Judging "quiet" hours from what each observer saw was tried first and dropped: it depends too much on where the observer happens to be (43 odd outings a season, mostly by day).
2. **Perception, in layers.** Every act is an event {eventId, time, actor, kind, target, place, duration, causeIds}. Nobody learns anything except by witnessing it, being told it or reading the board. Decision code sees memory only.
   - **Clarity.** Each observer gets a clarity for each event from 0 to 1. Clarity builds with time watched and depends on:
     - *distance:* close (0-2 tiles) 1.0, near (3-5) 0.6, far (6-8) 0.3, nothing beyond 8;
     - *line of sight:* a tile raycast from observer to actor; walls block, fences and bushes halve, darkness at night halves again;
     - *duration:* clarity is the sum over the ticks or seconds the observer had sight, against the act's "read time" (a glance at a wave is enough; a theft needs a few seconds to understand);
     - *loud acts* (a shout, a crash, a fight) are also heard through walls and up to 12 tiles, at low clarity and without identity.
   - **What the observer knows, by clarity:**
     - *what happened* at clarity 0.3 or more (an act's read time sets this);
     - *who did it* when clarity reaches 0.9 - 0.7 x familiarity with the actor (as built in 0a). A stranger needs a close look; a friend can tell at about 4 tiles; family who know them well can tell at 8;
     - otherwise the entry records "someone", with what was visible: a kind of person ("a farmer", "a child"), clothing colour, the direction they went.
   - **How strongly they feel it:** the affect is multiplied by clarity (law 11). The close witness to the bin rummaging knows who it was and is disgusted; the far one saw someone do it and is mildly put off.
   - **Unidentified acts can be solved later.** A story about "someone" can be joined with another witness's account, a clothing detail, or the player's known whereabouts. Two partial witnesses can add up to an identification, which gives mysteries without authoring.
   - **Misidentification** (a confident wrong guess) is an option for a later experiment, off by default: it makes drama, but it can feel unfair.
   - The player perceives through the same rule, which is why the player sometimes sees only "someone".
3. **Forgetting.** A diary over 300 entries drops its lowest-weight tenth.
   - Weight = base (sighting 0.1, act 1, secret 2) x 0.5^(days/21) x (0.5 + |regard for the subject|).
   - Sightings coarsen from spot to location to region to "earlier today", and are gone after the owner's next sleep.
   - Routine trades go to a purchase ledger. Only changes from routine (a new seller, a price change over 10%, a refusal) enter the diary.
4. **Regard.** Each act kind has a row: valence, magnitude, fade, plastic share, base juiciness, importance.
   - The target feels magnitude x (0.5 + sensitivity). The elastic feeling sums the last 3 days of entries.
   - Regard(A→B) is in -1..1 and saved per ordered pair. It moves by valence x felt x plastic share x (0.5 + retention) x (1 - |regard| in that direction).
   - A felt value of 0.7 or more ignores retention.
   - A third slight of the same kind from one actor within 5 days adds a 0.3 mark.
   - A repeated good act counts x 0.5^(repeats in 7 days).
   - *As built in 0c (2026-10-06; `sim/README.md`):* every act kind has a feeling row: who is pleased or hurt (the target, the actor, or the onlookers), how much (joy, separate from valence), the share that becomes regard for its cause (plastic), how freely its cause is believed to act (law 10), whom it is aimed at, and whether the glad or the sad do it more (law 1). The feeling is read from the holder's belief, never the truth, and goes to whoever they believe caused it; if they saw only "someone", to the kind of person they saw (law 9). A witness feels it by clarity; hearsay moves mood only. Mood is the last 3 days of joy and sadness, linearly fading; with need, an unmet want and being held, it gives the power of acting P. A feeling of 0.7 or more ignores retention; repeats count half within 7 days; one act moves one person's regard for one subject by at most 0.5. Named sentiments (Sims 4: Grateful, Hurt, Approving, Indignant, Pleased, Envious, Wary, Wronged, Ashamed, Reconciled) record the strongest causes and fade over about a season; no rule reads them. The third-slight mark is not built (0c question 6). The town runs every regard change at twice the rows' first guesses (PlasticScale 2).
5. **Healing and familiarity.** Regard drifts 0.005 a day toward a baseline set on the villager's card. It heals faster on contact: a day spent together with no new slight, an accepted apology, or a good act. Betrayed keeps regard at -0.2 or below. Familiarity is kept separate from liking. It rises by 0.02 x (1 - familiarity) per exchange (chat, trade, reaction) and falls 1% a day.
   - *As built in 0c:* regard drifts 0.005 a day toward where the pair started (housemates 0.6, friends 0.4, everyone else 0; nobody knows the newcomer). A grudge heals 0.01 x (0.5 + understanding) a day faster on a day of an hour or more together with no new slight. A friendship above where it started holds on days together and fades apart. Familiarity is unchanged from 0a (it rises with time together and does not fall yet).
6. **Bystanders** (laws 5 and 6). A witness who loves the target shares its feeling and moves its regard for the actor by 0.5 x the target's change x its regard for the target, times clarity. One who hates the target feels the opposite, at most 0.3 of that amount. One with no strong regard feels a fraction by likeness (imitation, about 0.2 x likeness), toward the target only. Changes made by this rule never trigger it again.
   - *As built in 0c:* someone who loves the person affected (regard 0.2 or more) feels 0.5 x their regard x that person's joy or sadness; someone who hates them (-0.2 or less) the opposite, at most 0.3 of it; anyone else 0.2 x (0.5 + 0.5 x likeness) x (1 - 0.5 x understanding) of it, and this moves their regard for the cause too (0c question 3). Likeness is a quarter each for household, life stage, kind of person and workplace. A told act whose patients are the onlookers (a drunk scene) is felt through the witness the story came from.
7. **Reactions.** Anyone who witnesses an event of importance 1 or more can react once, within an hour, with one of 8 emotions.
   - The reaction is an act linked to the eventId. Its magnitude = the emotion's row x the event's importance.
   - Witnesses read it against their own view of the event. Siding with the actor of a harm slights the victim; siding with the victim slights the actor.
   - Villagers pick an emotion from their own feeling about the event (laws 5 to 7) and their temperament; the player uses the wheel.
   - Witnessed reactions feed each villager's estimate of how the town regards that kind of act (law 7), which is how norms form.
   - Nobody reacts to a reaction.
8. **Gossip.** Awake villagers co-located for 15 minutes or more chat with a seeded chance of 0.3 x (0.5 + chattiness) per 10 minutes.
   - The teller offers its juiciest story at 2 or more that, by its own record, it has not told this listener and did not hear from them.
   - Add 0.5 if the listener has a close tie to someone in the story (regard 0.4 or more, or the same household).
   - The listener stores the story at 0.35 x the teller's juiciness, with the chain of tellers (tuned in 0a, 2026-10-06, for 26 villagers; 0.4 for 13; the mod uses 0.7). A scandal heard second-hand is then passed on only to people who know the culprit well, and news heard second-hand goes no further.
   - Juiciness fades 0.5 a day (0.8 at base 4 or more, as in the mod, D33, so a witness keeps telling a scandal for about three days), counted in whole 24-hour periods from when each person got the story.
   - A pair chats at most once per span, and again every 2 hours they stay together (as built in 0a).
   - A teller tells a story to at most 1 listener a day in a town under 20 villagers, 2 under 30, and 3 above that.
   - Retelling never adds detail.
   - *As built in 0c:* the close tie (+0.5) counts a listener who knows the person in the story well (familiarity 0.4 or more) or holds them at regard 0.4 or more. At the seed, every pair at 0.4 already knows each other that well, so this changes nothing yet.
9. **Hearsay and scandal.** Hearsay moves only mood, by 0.5 x magnitude x confidence. Confidence = 0.5 + 0.5 x regard for the teller, multiplied down the chain.
   - Two kinds of hearsay give a motive: a scandal (a bad act at base 4 or more), and news that touches the listener personally (household, partner, employer, its own trade).
   - Hearsay becomes regard at 0.5 strength when confirmed first-hand, and at 0.25 when two independent tellers agree.
   - When the people holding a scandal reach a quarter of those who know the actor (minimum 3), one of them confronts the actor. A seeded draw weighted by intensity x boldness picks who, preferring the person harmed.
   - Each scandal gets one confrontation, and a confrontation has base juiciness 3.
   - *As built in 0c:* hearsay moves mood by 0.5 x credence in the first teller, where credence is today's 0.5 + 0.5 x familiarity, leaning by at most 0.25 x (1 - understanding) toward tellers liked or disliked. Hearsay becomes regard at 0.25 x confidence when a second teller with no shared chain agrees, and at 0.5 when confirmed first-hand: seeing the culprit warned, taken in, set to service or rowed with at home, or, for the keeper, getting the goods paid back. Someone who saw or found it and then hears the name counts at 0.5 x clarity x confidence. Nobody confronts someone they love (0.4 or more); among the rest, hate for the culprit makes it up to three times likelier, so the person harmed is preferred without a special case.
   - **Tiers** (decided 2026-10-05). Every act kind belongs to one tier. The tier decides what the story can do, and each tier has a target for how often it happens in a town of 12:

     | Tier | Juiciness | Examples | Retold | Effect on listeners | How often, town-wide |
     |---|---|---|---|---|---|
     | Trivia | under 2 | a gift, a stumble | only to someone who knows a person in it | mood | many a day |
     | News | 2 to under 4 | an argument, a drunk night, a favour, a collapse | yes | mood only, unless it touches them personally | a few a week |
     | Scandal | 4 and up, bad | theft, the bin | yes, and fades slowly | a motive; a confrontation once enough people know | 1-2 a year from villagers in a settled town; more under strain or from the player |
     | Upheaval | set per kind | a shop closes, someone moves out, a couple splits | to everyone, and posted on the board | a motive for everyone it touches; can become a town-meeting item | once a year or less |

   - **Scandals and upheavals come from pressure, not dice.** A villager commits a scandal only when a vice is triggered (rule 15): a low purse, a run of bad days. An upheaval comes only from long pressure: weeks of losses, a feud, a failed courtship. A settled town is quiet; a strained one is not. How often they happen is a result we check against the table, not a rate we set.
10. **Motives and acts.** A villager acts toward a subject, villager or player, only on a motive about that subject.
    - It acts when boldness + 0.5 x familiarity + 0.5 x intensity reaches the act's cost (wave 0.2, note 0.25, chat 0.3, gift 0.4, walk up 0.5, visit 0.7, confront 0.8; hostile acts +0.3), and intensity reaches the act's minimum.
    - Code decides when the margin is more than 0.15 from the cost. Inside that band, a seeded draw with P = logistic(8 x margin), tilted by mood, decides.
    - Each subject gets two attempt slots per day. A question is asked again only when the motive moves by 0.1.
    - Light acts are capped at 2 a day and never counted as ignored. Hostile acts have a 3-day cooldown per pair.
    - Life choices (courting, splitting up, shop hours, hiring) use the same gate. They hold for a minimum time and need a 0.3 margin to reverse.
    - *As built in 0c (2026-10-06):* the gate itself is not built; acts still come at town-wide rates. Feelings steer two things: whom an act is aimed at, among people awake, free, within 5 tiles and in sight (love gives and helps, hate argues, and people argue less with those they love), and who acts (the glad give and help more, the sad drink more, by the power of acting). An act aimed at someone needs someone in reach, and the other party takes part. Life choices: each household chooses Pierre's store or the chain by the adults' regard for Pierre against the chain's prices (more when money is short), holding a choice for a season and changing only by a 0.3 margin.
    - *What 0c found:* aimed acts happen mostly at home (55% of arguments are inside a household), and only about a dozen arguments a year fall between people of different households, spread over many pairs. Nobody hurt can answer: someone without the act on their list, or not standing next to the arguer when an argument is drawn, never argues back. So no feud forms between households at any setting tried (50 seed-years each: plastic 1-3, target base 0.05-0.2, joy doubled, drift 0.002), and new friendships stay rare (at most 1.2 a year). E1's target (60% of seed-years with a new feud and a new friendship) needs this gate: hate as a motive toward a person, and the 3-day hostile cooldown per pair (0c question 6).
11. **Livelihoods.** Every adult has one livelihood (owner, employee, producer, out of work), and each household has one purse. Job routines gather people at 3-4 hubs at set hours (the square at noon, the saloon in the evening, market day), with meals in the routine. As built in 0a, a hub is a gathering that anyone free can pick like a haunt, with a weight: noon in the square (3), evenings at the saloon (2), Saturday market in the square (12). Motives replace blocks of the plan. A weekly want of 1-2 goods still unmet on day 4 becomes a NeedsHelp motive and a board post.
12. **Market and money.** Every sale has a named buyer with cash and weekly demand.
    - Town cash changes only through named outside accounts: the trader, outside wages, a county stipend.
    - A shop buys at 0.6 of its shelf price or less. Shelf price x (1 - 0.1 x regard for the customer); buy price x (1 + 0.1 x regard).
    - Each night the price moves by 0.2 x (unmet - max(0, stock - target)) / (target + sold + 1), with a ±3% dead band and a cap of 5% per night.
    - A shop trades only while its keeper is at the counter.
    - Monopolies charge up to 25% extra and never refuse service.
    - Buying from a rival in front of the keeper is BoughtFromRival, a small slight.
13. **Promises.** Any deal with a future part (an order, tab, loan, hire or invitation) is a Promise with a due time.
    - Keeping it writes Kept.
    - A missed debt or order writes BrokePromise (base 4). A missed invitation writes StoodUp (base 3). The creditor tells at least one person.
    - Repaid and Forgave scale with the amount divided by the debtor's weekly costs. Forgave counts only on an overdue debt.
14. **Secrets and trust.** Villagers start with 1-3 authored secrets (base 3-5) and gain new ones from private acts. Each secret has a trust tier: 0.3, 0.5 or 0.7.
    - Trust starts at the seed regard (0.2 for the newcomer). It rises 0.05 per help, kept appointment or repaid loan, and 0.1 per confidence kept for 7 days. It drops to 0 on a betrayal.
    - Retelling a secret is an act at base 4, and the secret carries its full chain of tellers.
    - An owner who hears its secret from someone it did not tell writes Betrayed (0.8, severe), split among everyone it told. It moves the whole entry onto one of them when evidence of that person's retelling arrives.
15. **Fuel.** Each villager has 1-2 vices with seeded triggers: a low purse leads to rummaging or theft, three low-mood days to drinking alone, and overload to a missed delivery. An occasion catalog (weather, prices, visitors, lost items, breakages, illness, mishaps by job) fires at base rates with cooldowns. Occasions create only world facts. A type-level test proves an occasion cannot write to any villager's mind.
    - **As built in 0b** (2026-10-06; `sim/README.md`): money and temptation.
      - Each household has a purse and each person a pocket. Money enters and leaves only through outside accounts (pensions, wages from away, sales out of town, the county's stipend, the chain store's head office, wholesalers), and a test holds the town's cash to exactly what crosses its edge.
      - Paydays are weekly: wages and pensions, 15% kept by the earner, allowances for those who live with family, groceries at Pierre's (who restocks at 60%) or the chain (10% cheaper, its takings leave town). Adults at the saloon in the evening buy a drink. Each week a household spends half of what it holds beyond four weeks of costs, half at Pierre's and half out of town; pockets the same beyond 300g, keeping the price of an open want.
      - Wants come at random (one every two weeks or so), priced by person: Abigail 300-900g, a child 20-80g. A want is bought when the pocket covers it.
      - Thefts and rummaging no longer have rates. A villager with the vice, free and somewhere it can happen, weighs a motive (an unmet want, growing over two weeks; the household short of a week's groceries; a dare, for the bold) against the risk they believe they run: people in sight now x the next step on the ladder for them x (1.2 - boldness). The excess gives the chance. Nobody robs the shop they work in. Every tempted scandal records its motive.
      - In a year: about 2.8 tempted scandals, 1.9 from Pam (her trailer runs short: no income, drinks most evenings) rummaging bins out of need and 0.8 from Abigail stealing for a want. The town's cash rises while purses fill their buffers, then holds near 20,000g (about 1.5% down a season).

16. **Authority and consequences** (decided 2026-10-06; see 11c). Social pressure is not the only cost of a crime.
    - **The mayor is the town's authority.** Lewis is mayor at the start of every run. Victims and witnesses report to him. He looks into it using only what people tell him and what he has seen himself (rule 2), then decides. So he can be lied to, and he can blame the wrong person when the accounts point that way.
    - **The constable.** Every run opens with the town voting a constable in, so each run can start with a different one. For now this is mostly story and show: the constable takes reports and walks the town. It matters more once the town is bigger.
    - **Elections.** The mayor's office comes up for election later in the game. Lewis can be voted out, and he can grow old and die (11c), which opens the office.
    - **Fairness.** Lewis is the example of a just and fair authority. Anyone in authority can be swayed (going easier on someone they love, or someone who holds power over them), but for Lewis the chance is very small. A new mayor or constable may be less fair.
    - **The ladder of consequences:**

      | Step | When | Consequence |
      |---|---|---|
      | Warning | first time, small value | a word from the mayor; it becomes a story |
      | Restitution and fine | the theft is proven | return the item or pay it back, plus a fine to the town purse |
      | Ban | the keeper's choice | barred from that shop for a while |
      | Service | a repeat, or can't pay | unpaid work in public hours, where everyone sees it |
      | Watched | a repeat | people pay closer attention to them (they are noticed from farther away) |
      | Detained | a pattern | held for a set time; they miss work and are seen being taken in |

      Durations and amounts are set after testing. Detention replaces "asked to leave" (Sid, 2026-10-06).
    - **Deterrence.** Before acting on a vice, a villager weighs need (an empty purse, a run of bad days) against the risk *they believe* they run: how busy the place usually is (from memory, never the true count) x how bad the consequence would be x how much they fear it. Bold or desperate people still steal; careful ones wait for a quiet moment, which is why traces matter (an unseen theft found later by missing stock). As built in 0b, the risk counts the people in sight at that moment; because thieves wait until nobody is watching, the culprit is among anyone's suspects in only 9% of natural scandals.
    - **Restitution, fines and service** (as built in 0b): the second verdict pays back the goods' worth to the keeper and a 100g fine to the town, from pocket then purse; whatever can't be paid becomes three hours of community service in the square, in public view.
    - **The player is under the same ladder.**
    - **As built in 0a** (2026-10-06; `sim/README.md`):
      - Reports happen when the reporter and the mayor or constable are together. Victims always report, even hearsay; the constable always reports; other witnesses and finders with a chance of 0.2 + 0.6 x boldness.
      - The mayor weighs accounts naming someone: confidence x (1 first-hand, 0.5 hearsay) x trust in the teller (0.5 + 0.5 x familiarity). He decides at 0.6 with a 2-to-1 lead; "someone" names nobody, so cases can stay open.
      - Lewis lets off someone close (familiarity 0.5 or more, or his household) with a 2% chance.
      - The ladder runs warning, restitution and fine, service, then detention from the fourth verdict (24 hours at the manor). Warnings and detentions are delivered in person and can be seen; fines and service wait for money (0b).
      - The opening vote: the bold stand, everyone but the newcomer votes. Across 400 runs, eight different villagers were voted in; Pierre most often (37%).
    - **Piecing together "someone" and interviews** (Sid, 2026-10-06: "they should be able to piece together from people around. Then the constable could interview the character and see if any more info comes up."):
      - Everyone remembers who they saw, where and when (same place, within 8 tiles, in line of sight; kept 3 days).
      - Someone who saw or found a scandal without seeing who did it suspects who was around the place at the time: 30 minutes either side for a witness, the 8 hours before for a finder. Strangers are suspected before friends (law 9); the keeper of the place is not suspected. Up to 3 names.
      - Suspicion travels with the story, and reaches the mayor as "nearby" names. Nearby counts for 0.25, split over the names, and never decides a case alone: a verdict needs someone who saw it, hearsay of a sighting, or a confession.
      - The constable (or the mayor, if there is none) questions the people named, most-named first, once each per case, when they meet. Being questioned is seen. The culprit may confess (0.25 + 0.5 x timidity); anyone questioned says who they saw around the place at the time, which can name the culprit or point at someone else.
      - Most people questioned are innocent (61% on placed scandals). Being suspected and questioned should cost the suspect's regard for whoever named them once feelings exist (0c).
    - **As built in 0c** (2026-10-06): the constable tells an innocent suspect who named them (0c question 4), so they resent the namers, split between them; kin questioned only for an alibi were named by nobody. The guilty feel shame instead and resent nobody. Whoever is warned, taken in, questioned or set to service blames the official at 0.4 of the freedom of a chosen act (they are doing their job), less if they know they gave cause. With feelings steering: the mayor's trust in a teller leans by at most 0.1 toward people he likes (Lewis, understanding 0.6; 0c question 12), he counts as close anyone in his household, anyone he knows well and doesn't dislike, or anyone he loves; a witness is less willing to report a culprit they love and more willing to report one they hate.

17. **Families and age** (decided 2026-10-06; see 11d).
    - **Kin.** Villagers have family ties with roles: parent, child, spouse, sibling, grandparent, grandchild, guardian, stepparent. Parents set rules (allowance, chores, curfew) and children can break them. A household is who lives together; kin is who is family (Shane rents at the ranch and is not Marnie's kin).
    - **Age decides what someone would do.** Life stages: child (under 13), teen (13-19), adult (20-64), elder (65+). Each act kind has an age range. A child doesn't know how to steal from a till, but squabbles with a sibling (Sid: "Vincent probably wouldn't even know how to steal from the till but he would fight with his brother"). Aging is real time (11c).
    - **Every scandal has a motive, recorded with it.** A scandal happens only when a motive outweighs the believed risk (rule 16):
      - *want:* something they want and can't afford (wants and fears, 11c);
      - *grievance:* low regard for the victim after a slight (a fight with a parent, a rival shop);
      - *thrill:* temperament (bold, restless, bored);
      - *imitation:* friends who do it and seem to get away with it (law 7);
      - *need:* a low purse or debt (rule 15, 0b).
      The log can always answer "why would she": "Abigail took 50g from the till the day after Pierre refused her allowance." Until regard (0c) and money (0b) exist, the simulator keeps fixed rates, now limited by age.
    - **Trouble inside the family stays inside.** A keeper who learns that their own kin took from them doesn't report it; it becomes a family row, which others can overhear and gossip about. A family matter becomes a town matter only through someone outside.
    - **Families cover for each other** (Sid, 2026-10-06). Family members never report kin, don't spread stories that hurt kin, don't suspect kin, and leave kin out when questioned. Asked about a suspect in their family, they give an alibi ("she was with me"). The constable knows families cover, so a family alibi counts for less, and it never outweighs a sighting.
    - **Shame by association** (laws 5 and 6, from 0c): a scandal costs the culprit's kin some standing too, so a parent is angrier at a public scandal than a private one.
      - *As built in 0c (2026-10-06):* a witness who blames the culprit for a scandal cools on the culprit's kin by 0.2 x (1 - familiarity) x (1 - 0.5 x understanding) of it. Kin feel a step of shame (sadness, and a little at the culprit) for each new person they learn knows, up to 6: each teller in a chain that names the culprit, and three at once for seeing the culprit warned, taken in, questioned or set to service in public. Families still cover: they never report, retell, suspect or confront kin.
      - *Grievance* (as built in 0c): dislike of the keeper beyond -0.2 adds to the motive to steal from them, kin included; it is named only when it is the largest motive, and the log says why ("why Abi Stole Kim Hurt since d0 act 0"). None happened in a year of the town: nobody dislikes a keeper that much.
    - **Curfews** (as built in 0a, 2026-10-06): anyone who lives with a parent or guardian has a curfew: children 20:00, teens 22:00, grown children 1:00. A parent who learns their child was seen out past it has it out with them at home (a family row).
    - **As built in 0a** (2026-10-06): every Stardew family is in the simulator (26 villagers with the newcomer), with ages (guesses where Stardew gives none) and kin. Stealing and rummaging need age 13, drunk scenes 18, arguments 13; a child squabbles with a sibling who is there instead. Kin never report, retell, suspect or confront each other, leave each other out when questioned, and vouch for each other. A family alibi takes back half of one "nearby" (0.125) and never offsets a sighting or a confession. The constable questions the most-suspected person's housemates for alibis. A keeper who learns their own kin took from them has a family row (a news act others can see) instead of reporting. Only adults stand for constable; everyone 16 and over votes.

18. **Character over time** (Sid, 2026-10-07; see 11e). A proposal, to settle when it is built.
    - **Traits are plastic.** The temperament weights of law 12 (chattiness, boldness, understanding, self-regard, sensitivity, retention) are each person's current character, not fixed. What happens to someone pushes them: a severe event (felt at 0.7 or more, rule 4's threshold) a lot, small events only by repetition. Examples to tune: being rebuffed, shamed or punished lowers boldness and self-regard; being thanked or helped raises self-regard; good company raises chattiness, and isolation lowers it; a reconciliation, or learning why someone did something, raises understanding; repeated hurt raises sensitivity or retention.
    - **Hardening.** How far an event moves a trait falls with age: children change easily, adults less, elders hardly, never to zero.
    - **Fringe people emerge from feedback.** No rule pulls anyone toward the middle. Someone shy who is rebuffed acts less, meets fewer people and grows shyer, and can end up a hermit; someone bold who wins their confrontations grows bolder. The usual case stays near the archetypes because most events are small and mixed.
    - **Changes stick while they keep being triggered** (Sid, 2026-10-07). All six traits are plastic.
    - **People drift toward who they surround themselves with** (Sid, 2026-10-07): slowly, each trait moves toward those of the people someone spends time with, weighted by time together and perhaps by regard. A subtle effect, noticed over several runs; to be found by experiment.
    - **Inheritance.** A child's traits start as a combination of its two parents', with a chance of mutation at birth. Research (2026-10-07; claims from abstracts and reviews, VERIFY before relying on exact figures):
      - **Inheritance is weak.** Personality is about 40% heritable (Vukasović & Bratko 2015), but a child correlates with a parent only about 0.10-0.20, and siblings about 0.15-0.20; the family home barely shapes traits, and almost all environmental effect is unique to each child (Plomin et al. 2016).
      - **Proposed model:** each person has a hidden genetic value per trait, separate from their current character. A child's is the mean of its parents' plus its own share of each (so siblings differ), plus a fresh non-inherited part; the plasticity rule changes character, never the genetic value, so a parent who became a hermit passes on their nature, not their withdrawal (what they pass on is a lonely household). Traits sit on a hidden scale shown through a sigmoid, so extremes need no clamp.
      - **Mutation:** a child carries about 60 new mutations, about 2 more for each year of the father's age (Kong et al. 2012), nearly all without effect. Game proposal: about 1 child in 11 gets one trait's genetic value shifted noticeably, likelier with an older father.
      - **Hardening has a measured curve** (Roberts & DelVecchio 2000): yearly change about 1.0 for a child, 0.55 at 20, 0.40 at 30, 0.28 from 50, so a floor near 0.25.
      - **Events:** life events have real but small effects; a severe event should move a trait moderately and fade unless daily life keeps reinforcing it (Bühler et al. 2024). Traits pick experiences that deepen them, which is how fringe people emerge (Roberts, Caspi & Moffitt 2003).
      - **Convergence toward others is small** in adults (couples barely converge; Humbad et al. 2010), likely larger in teens: keep it subtle, faster for the young.
      - **Not worth modelling:** birth order, sibling contrast. Worth it: sensitive children changed more by both good and bad surroundings.
      The full report: `docs/under-glass/inheritance-research.md`.
    - **Kin ties pass on as a head start, not a rule** (Sid, 2026-10-07): a child forms its parents' grudges and friendships more easily, but not certainly. Much of this may come on its own from living close to them.
    - **Measured over 5-10 simulated years:** how far traits spread, how many people reach the fringe, and that the town neither converges on one character nor flies apart.

The player is one more agent under these rules and has no meters of their own.

## 5. The villager model

- **Identity (authored):** household, job, hours, wage, home, gift tastes, routine template, temperament (six traits, six emotion biases, retention), vices, secrets, seed regard (household 0.6, friends 0.4, tensions -0.3), voice sheet.
- **State (saved, versioned):** diary, ledgers, heard stories with their chains, regard, familiarity and trust per pair, promises, cash and stock, statuses, today's plan, two attempt slots.
- **Mind (derived from memory, never saved):** elastic feelings, mood (earned mood plus the seeded MoodRoll), and motives, each with a subject and a source entry. The mod's Motive values carry over, with Owed, Owes, Courting, Confront and Avoid appended.

**How a villager decides.**
- While it sleeps, it plans its next waking day from its job, its haunts and its strongest motives.
- Each tick, perception writes diary entries. Motives are recomputed only if the diary changed, and each motive goes through rule 10.
- Villagers find each other by asking around and by habit.

**The model.** It is optional. It answers three typed questions:
- a close call (yes/no);
- which of the top motives goes first (a choice of up to 5);
- how a reader reacts to a board note or a stolen journal page (a 6-way choice).

It never writes text. Headless runs use a Fake client calibrated to Laya's measured yes-rates. Answers are recorded and arrive at a fixed delay (asked at tick t, used at t+1), so machine speed never changes the story. The game is complete with the model off.

## 5a. Laya: meaning in, meaning out

Decided (Sid, 2026-10-05): the player writes free text, villagers can misread the player, and the readings test uses Stardew's cast. Sid: "The generated text is mostly candy. The core of the interactions is the emotion behind what's being said, not really the content."

**The message is the feeling, not the words.** Every social act carries a typed payload: {emotion, intensity, target, purpose, cause eventId}. That payload is what the simulation reads. Words are only a rendering of it.

**Rules decide; Laya interprets.** The simulation never asks Laya what to do. It asks what something means to a particular person. Laya answers typed questions (choice, score, yes/no) and never writes text.

**Laya's roles, in order of value:**
1. **Reading what the player writes** (board notes, letters, the journal, and later talking). Laya turns the player's words into the typed payload: purpose (request, offer, thanks, apology, accusation...), who it's about, and the emotion behind it. Villagers react to the payload. With Laya off, the player builds notes from parts as before.
2. **How each villager reads a moment.** Given the villager's card and what it actually perceived (limited by clarity), Laya picks a reading: your Amused face at Ivo's spill read as laughing with him or at him; a gift read as kindness or as a bribe. The reading sets the feeling's sign and how much blame there is (law 10).
   - **Misreading is a feature** (Sid, 2026-10-05). It comes from three places: low clarity (a far glimpse), the villager's temperament and current feeling toward you (someone who already dislikes you reads you worse, law 5), and the *understanding* trait (a high-understanding villager reads close to the rule-based truth; a low one follows its imagination).
   - Misreadings are visible and repairable: lines can say how a villager took something ("Hana thought you were laughing at Ivo"), and the player can correct it, which supplies a cause (law 10).
3. **Close calls:** a seeded draw breaks ties near an act's threshold, and Laya may tilt it. It is the weakest use (the mod's yes/no answers bunched between 0.47 and 0.60) and stays only if a test shows it matters.

**Where Laya never goes:** perception and clarity, who knows what, money, the conatus arithmetic, the gossip rules, and the headless 1000-seed sweeps. Those stay deterministic, fast and tested.

**Determinism.** Every Laya answer is written to the event log as an event; replays read the log instead of calling the model. Answers asked at tick *t* are used at *t+1*, so machine speed never changes the story. Headless sweeps use a fake client calibrated to Laya's measured answers; smaller runs use the real model. The game is complete with Laya off.

**Running it inside the game** (checked at github.com/NandhaKishorM/laya, v0.3.28, 2026-10-05; Hugging Face was not reachable from the cloud session):
- Licence: Apache 2.0 (the repo). The weights' licence on the model card still needs a look.
- `laya-dotnet`: a C# port of the inference path on ONNX Runtime, with no Python at runtime, answers checked against golden outputs from the Python code. It supports choice, score and yes/no, batched in one forward pass. It targets .NET 10 and uses `Microsoft.ML.OnnxRuntime` 1.30 (CUDA and DirectML builds exist for the GPU). Checkpoints are exported with the repo's ONNX script; quantized models are not supported in the .NET port's v1.
- So Under Glass can call Laya in-process from C#, with no sidecar. The simulator and the Godot project should target .NET 10 to match (VERIFY that Godot's .NET build accepts a net10.0 project).
- Fine-tuning: `laya-train --data file.csv --out ./ft` trains on a plain CSV, discovering the labels. That means Laya can be trained on our own question types (how a villager reads a moment, what a note means) from labels Sid writes.

**Later: villagers' own words (candy).** If a local LLM becomes cheap enough, it can write a villager's line *from* the typed payload: "Haley, annoyed, about the egg you gave her, low intensity". The text is decoration. It never feeds back into the simulation, it passes the line sanitizer, it can be turned off, and templated lines (the voice sheets) remain the default. The emotion carries the interaction either way.

**Experiments that decide each role:**
- **E4a, notes:** Sid writes 50 notes the way a player would and labels purpose, subject and emotion. Laya passes at about 80% on purpose and subject. Then try a fine-tune on half and test on the other half.
- **E4b, readings:** 30 ambiguous scenes from Stardew's cast (for example Shane seeing you laugh when Pam trips; Haley getting an egg in front of Emily). Sid says how 3-4 villagers should read each one. Compare Laya, the rules alone and a coin flip, before and after a fine-tune on Sid's labels.
- **E4c, the blind read:** Sid reads season journals made with Laya and with the calibrated fake, without knowing which is which.

## 6. The player

**Verbs:**
- React on the emotion wheel.
- Talk with no daily cap. Ask where someone is or what they have heard, tell a story and join its chain of tellers, keep or break a confidence.
- Give a gift. The people who see it decide what it means. Diminishing returns on repeats replace the gift cap.
- Apologise for a specific event, or correct a misreading by pointing at the real cause.
- Post on the board from parts: a purpose (request, offer, thanks, apology, or an accusation that cites an event), a subject, and a tone picked on the wheel. The player's own text is shown as flavour, so tone still works with the model off.
- Fill requests, fund town projects, choose buyers and shops, lend money, forgive debts, hire a farm hand, invite people and keep appointments.
- Be somewhere. Where the player is decides what they see and who sees them.
- Write the bedtime journal. A page can be stolen when the player passes out, and it gets pinned on the board.

**The wheel.**
- Holding a key opens 8 wedges: Grateful, Pleased, Amused, Sympathetic, Surprised, Disappointed, Angry, Disgusted.
- The centre names the event the reaction attaches to ("Ivo spilled the soup"). The player can cycle among the last 4 events.
- Portraits show everyone within 8 tiles; anyone who missed the event is greyed out.
- Witnesses within 3 tiles show how they read the reaction on the same frame.
- A test merges any two wedges whose outcomes differ by less than 0.1.

**Reading the town.** There is no hearts bar and there are no numbers. The player reads the town through:
- villagers' visible reactions, on the same frame as the act;
- lines that name the cause and the source ("Kit says you went through the doctor's bin"). No consequence is shown unless it can cite an eventId;
- overheard chats that quote the story and its teller;
- the board, with a weekly "talk of the town" sheet;
- the bedtime journal's "Today you noticed": up to 3 chains of events picked by a story sifter, each with cause and source, next to the player's own two sentences;
- a "Who's who" page built only from what the player knows;
- an account book;
- each villager's first greeting of the day, which is warm, neutral, curt or turned away.

## 6a. Power: a proposal (for discussion)

Spinoza separates *potentia*, a person's own power of acting, from power over others. In the *Political Treatise*, one person is under another's power when that person holds them by fear, or by hope of a benefit, or because they have bound themselves by love. That matches Sid's definition: power is the ability to move a villager against their own inertia.

**Influence of A over B** (per pair, from 0 to 1) is built from B's affects toward A:
- **Love and the wish to please** (laws 7 and 8): B's regard for A.
- **Hope:** what B expects to gain from A (A's help, trade, credit, a kept promise).
- **Fear:** what B expects to lose (a debt B owes A, a secret A holds, A's ability to turn the town against B). Fear works, but it adds to hate every time it is used.
- **Standing:** how many people B respects regard A well (B leans toward what others love, law 7).
- **Familiarity:** a stranger moves no one.

**Inertia of B** against a request: the strength of B's own desire for the alternative, plus habit (the routine), plus temperament (bold and low-understanding villagers resist more; agreeable ones less).

**An influence attempt** (ask a favour, persuade, propose, canvass) succeeds when influence reaches inertia. It is a close call near the line, decided like rule 10.

**Elastic and plastic, as Sid put it:**
- *Elastic power* is the current balance of goodwill: favours owed, recent help, the glow after a public success. Spending it to move someone uses it up for a while, and it recovers.
- *Plastic power* is lasting standing: reputation, roles (shopkeeper, mayor), debts, held secrets, kept promises. It grows slowly and is hard to lose, except through a scandal.

**What power buys:**
- **Convincing:** changing someone's belief (they accept your account of an event, a correction takes), their plan (they come to the festival, sell you hay, hire you), or their feeling toward a third person (reconciling two villagers).
- **Changing the rules of the town:** at the season town meeting, anyone can propose a rule (shop hours, a fence round the bins, a ban on the chain store, a curfew). Each villager votes from their own desire plus the influence of the proposer and anyone who canvassed them.
- **Prices, credit and help** (rule 12), and **information:** people tell secrets to those they trust.

**Villagers hold power too.** Lewis, Pierre, the chain store's manager and the saloon keeper all build and spend it. Town politics comes out of the same rules, with or without the player. The alien's situation makes power double-edged: standing protects you from suspicion, but it also draws attention.

This is a first sketch. The constants and the meeting rules are for us to work out together.

**Sid's view (2026-10-06).** Changing the rules of the town is a big part of the game, but the meeting is not settled as *the* main stake. What counts:
- What is put to a vote has to be worth the effort of winning it: a change the player and the villagers feel in their days, not a token.
- Not every player will enjoy politics. The meeting must be one way to play, never a chore every player has to do.
- Its best quality is that it can change where a run goes: the same town can end up in different places.

## 7. Farming and the economy

**What stays:** seasons, crops, watering, rain, tools, animals, machines, fishing, foraging, and later the mine. Version 1 is small: plots, 4 crops, chickens and one machine.

**What becomes systemic:**
- **No shipping bin.** Goods go to named buyers: the general store, the chain store, the saloon kitchen, the carpenter, and a trader who visits twice a week. The trader pays the floor price from an outside account and has a weekly cap.
- **Villagers produce too.** They sell eggs, fish and vegetables, so the player supplies some neighbours and competes with others.
- **Shops run on real revenue.** Two weeks below costs leads to cut hours, a sale note or a request for help. The general store against the chain store comes out of the rules.
- **The board replaces help-wanted and special orders.** Requests come from real shortages, and villagers fill them too.
- **Town projects replace bundles.** Villagers propose them. At the end of each season, proposals with 3 or more backers go to a public vote, decided by each villager's interest and regard.
- **The farm is open to villagers,** and festivals have real attendance.

**How it feeds the social sim.**
- Every trade is witnessed.
- Regard sets prices, credit and hiring.
- Promises tie the two layers together.
- Year-1 output is sized to 30-60% of town demand.
- Regard pays in credit, first pick from the trader, hires accepted, help after a storm, secrets and votes. The target is 15-25% of the player's income.

## 8. Example stories

Each story becomes a golden scenario test before its rules are coded.

1. **Who saw the bin** (rules 2, 8, 9). The player rummages in the doctor's bin at 21:00, an act at base juiciness 4.
   - Layers: Tess, 2 tiles away for several seconds, knows it was the player and is disgusted at full strength. Hal, 7 tiles away behind the fence at dusk, saw *someone* rummaging, in a straw hat, and is mildly put off; his feeling attaches to "someone" and, a little, to "the farmer" as a kind of person. When Hal and Tess compare stories in the saloon, Hal's "someone in a straw hat" becomes the player, and his feeling moves onto the player.
   - On seed A, Tess the saloon keeper sees it. She tells one listener a day. Her first listener still holds the story at 2.0 the next day and passes it on.
   - By the next evening four villagers hold it. That passes the threshold of 3, a quarter of the 12 who know the player. The draw picks Bram, one of the four, to confront the player in the square.
   - On seed B, only Joel, a solitary fisher, sees it. His copy falls 4.0, 3.2, 2.4, 1.6. If he chats with nobody for three days, the story dies with him.
2. **The egg glut** (rules 12, 9, 10). The player sells 40 eggs a week to Dov's store.
   - His stock sits far above target, so his price drops 5% a night, about 30% in a week.
   - Dov tells Lou the rancher why his eggs fetch less. The news touches Lou's own trade, so it gives him a Hurt motive.
   - Walking up to the player is a close call, and the draw declines it, so Lou pins a note: "Ranchers need fair egg prices."
   - The player can sell to the saloon instead, cut back, or buy Lou's hay. Each choice happens in public.
3. **The wrong friend blamed** (rules 14, 2, 8). Rue owes the chain store 400g, a secret at trust tier 0.5.
   - She tells Tess. She tells the player after six filled requests and kept appointments.
   - The player retells it to Mina in the saloon while Pia sits 5 tiles away.
   - Rue hears it from Hal and splits Betrayed between Tess and the player.
   - If Pia's story reaches Rue within three days, all the blame moves to the player. Two seeds end differently. A confession moves the blame to the player at once.
4. **A tab at the saloon, with no player** (rules 13, 9, 6, 5).
   - Joel's winter catch falls and his tab with Tess goes unpaid.
   - Tess raises it at the bar in front of four villagers, and Joel starts avoiding the saloon.
   - Mina, who likes Joel, cools toward Tess and pins "Buying fish, 40g each." Joel fills the request and pays the tab, and Tess writes Repaid.
   - Joel's Avoid motive expires after 7 days.
   - The player sees all this only on the board or by being in the saloon.
5. **The laugh at the festival** (rules 15, 7, 6, 8).
   - Ivo spills the soup in front of ten people. The player picks Amused, which the witnesses read as siding against Ivo.
   - Hana, who likes Ivo, frowns and cools toward the player. Gil, who dislikes Ivo, warms slightly.
   - Enid retells the story for two days.
   - Hana passes over the player's turnip request, and Gil fills it. The journal links both to the laugh.

## 9. Ideas grafted, and ideas dropped

The spine is the minimal-lab concept (Under Glass). From it we keep:
- witnessing as the only way to learn anything;
- the act table as the only social content;
- kill criteria, bots and the REPL.

It was the only concept built around "set up rules and run a simulation". The grafts fill its gaps: nothing fuels the town, good news travels less far than bad news, and the economy is thin.

**Grafted:**
- *Rules-first:* secrets with blame that moves, obligations, BoughtFromRival, the drama-density metric, regard that pays, life choices held as commitments.
- *Living-economy:* conserved money with outside accounts, the Promise record, repricing on deviation from target stock, the purchase ledger, one purse per household, monopolies that charge extra instead of refusing.
- *Player-voice:* the wheel showing its event and its witnesses, reactions shown on the same frame, lines that quote the event, a meaning row per wedge, the Corrected act.
- *Storyteller:* the occasion catalog, and the test that occasions cannot write to a villager's mind.
- *Social-physics:* the season town meeting, and its Monte Carlo finding, which is why the gossip harness comes first.

**Dropped:**
- A standalone game on Stardew's assets: it cannot ship.
- Laya on any critical path: its measured yes/no answers were flat.
- A director that forecasts by simulating ahead: one sample per candidate is mostly noise.
- Misreads caused by same-tick timing: they feel like bad luck.
- A shared hunger clock: everyone eats at once, and shops close at random.
- Random tiles each tick: tuning done on them will not carry over to a rendered town.
- A full Stardew farm in version 1: it would take the time the social layer needs.
- Telling "the story the listener lacks": the teller would have to read another villager's mind.
- Weekly averaging of norms, and groups built from connected components: the norms freeze and the groups merge into one.
- Blight on the player's farm: it reads as punishment.

## 10. Risks and early experiments

| Risk | Test | Pass |
|---|---|---|
| The town is dead or at war | E1: no player, 1000 seeds, plus a parameter sweep | A feud and a friendship in 60%+ of seeds; dead and war towns each under 5%; a lively town in 15%+ of parameter space |
| Gossip saturates the town | Gossip-only harness | A scandal reaches 40-70% of the town over 3 or more days |
| Money leaks, or the farm swamps the town | Stock-and-flow model, 4 seasons per bot | Town cash drops less than 30% a season; the player holds under 5x town cash after season 1 |
| Regard can be pumped | MaxRegard, Sneak, Iago, CrashThenRescue and GiftSpam bots | None beats the Saint bot by more than 20% |
| The town only mirrors the player | E3, player policies | 40%+ of stories have no player in their cause chain |
| The player cannot follow it | Drama-density metric; the REPL | 3 or more acts seen a day; Sid wants a second text season |
| The stories contradict the numbers | Golden scenarios | All pass, or the rule changes |
| A rule adds nothing | E2, ablations | A rule stays only if removing it moves a story metric by 20% or more |
| Logs differ between machines | Windows and Linux CI | Same hash; values stored as fixed-point numbers |
| The model does not earn its place | E4, blind read against the calibrated Fake client | Sid prefers Laya's seasons |

E0 runs first and checks four things:
- the same seed gives the same log;
- regard stays in range;
- the caps hold;
- every heard story traces back to a witness, or to someone who found its trace.

E5 is Sid's blind read of 10 season journals.

## 11. The plan

**Phase 0: headless (about 2-3 months with agents).** It starts in this repo (`sim/UnderGlass.Sim`, `sim/UnderGlass.Run`), next to the libraries it reuses, and moves to its own repository once the rules hold.
- **0a.** A gossip-only harness: rules 2, 8 and 9 on 12 villagers.
- **0b.** A stock-and-flow money model, about 200 lines. Built 2026-10-06, with wants, needs, temptation and the ladder's fines.
- **0c.** Feelings: the Spinozan laws of section 3a on regard, mood and the believed cause. (The first plan was to port the mod's core here. The simulator was instead built natively in 0a and 0b, so 0c builds feelings on it directly; the mod's lessons, such as D33-D35, carry over as rules, not code.) Built 2026-10-06 (`sim/README.md`): laws 1-3, 5, 6 and 8-12 with sentiments, steering ten decisions. The 0a and 0b gates still hold. E1 does not: feuds between households need rule 10's desire gate, the next step (0c question 13).
- **0d.** The simulator itself (`UnderGlass.Sim`):
  - the town, the cast (12 of Stardew's villagers, privately, until our own exist), the acts and the occasions as JSON;
  - the bots;
  - the event log, snapshots, metrics and the story sifter;
  - the viewer replays runs.
- **0e (proposed, 2026-10-07).** Character over time and generations (rule 18, 11e): traits that change with events and harden with age, inheritance with mutation, the life course (couples, births, children leaving home, deaths), and time skips of 5-10 years between runs.
- **Exit:** E0-E3 and the golden scenarios pass.

**Phase 1: text REPL (about 2 weeks).** Sid plays a season through commands and sees only what the player perceives. If it is not worth playing in text, art will not fix it.

**Phase 2: the first visual build.** A Godot 4 C# project over `UnderGlass.Sim`, with Stardew's art privately as placeholders, or simple shapes. Separately, Stardew stays useful as a test bed for single ideas:
- First, a wheel spike in the current mod. It sits behind a switch that is off by default and logs each villager's reading as a `[shadow]` line. Villagers' visible reactions go live only with Sid's go-ahead (AGENTS.md rule 1).
- Then an overhaul mod: named buyers, the board, heart events off, and next-day schedules built from the overnight plan (D26).
- Verify the mod policy first.

**Engine (decided 2026-10-05).**
- **Experiments: plain C# on .NET 8** (as built, 2026-10-05: the simulator core, `sim/UnderGlass.Sim`, is .NET 8, which the cloud agents have and Godot 4 C# reads; the Laya adapter will be a separate .NET 10 project referencing it, since the Laya C# port targets .NET 10). The simulator is a class library (`UnderGlass.Sim`) with a console runner, the text REPL and xUnit tests.
  - It is the best documented and fastest option for the experiments.
  - The cloud agents can build, test and run it with no game or editor installed.
  - It reuses this repo's libraries directly, and it is deterministic and easy to run 1000 seeds of.
- **Visual prototype and final game: Godot 4 with C#.**
  - The renderer is a Godot project that references the same `UnderGlass.Sim` library and only reads its snapshots and events.
  - Sid can open the scenes, place art, paint maps and change the UI himself.
  - We skip MonoGame: using Godot from the first visual build means no engine change later.
- **What it needs:**
  - the .NET 8 SDK (and the .NET 10 SDK once the Laya adapter exists) beside the current 6.0 SDK on Sid's PC;
  - Godot's .NET build.
  - C# Godot projects don't export to the web yet, which doesn't matter for a desktop game.
- **The simulation never depends on Godot.** It runs headless in tests and in the cloud, and Godot is only a view of it.

**Phase 3: the original game.** Start it only after the visual prototype shows the loop is fun: original town, cast, art, music and writing, in the same Godot project.

**Meanwhile, in the mod:**
- keep playtesting on BUNKO_450391925;
- build the 7-day spreading test from ledger-gossip.md;
- log per-pair co-location rates in the real town to calibrate UnderGlass.Sim's spots;
- fix the architecture.md table that still lists NpcMotives and NpcLive as not wired.

## 11a. Decisions of 2026-10-05 (second round)

**Tone and limits.** The town can get fairly dark: debt, addiction, theft, shops closing, people moving out. Murder and sexual violence or abuse are out of scope. Keep in mind it is a pixel-art game: hard things are shown plainly and briefly, never dwelt on.

**Romance is the same mechanics for everyone, from the start.** Villagers court each other, and the player courts villagers, under the same rules. Courting is love plus a wish to be loved back in particular (III P33), with jealousy when the beloved favours someone else (III P35). Couples form, split and re-form without a script. Life choices hold for a minimum time and need a margin to reverse (rule 10).

**Misidentification depends on the person.** Each identification has a confidence, from clarity and familiarity. Temperament decides what a villager does with a low confidence:
- A cautious or secure villager says "someone".
- A bold villager with low self-regard names a guess as fact: *confidently wrong*.
- This needs one more trait, *self-regard* (Spinoza's self-esteem and humility, III P30 and the definitions of the affects).

**Under Glass is about an alien living secretly among the villagers.** The player crash-landed and has to fit in. This idea comes from the final game, and it shapes the design now:
- **The screen shows what the character perceives.** Rendering follows the clarity rule:
  - at 8 tiles, two figures talking;
  - at 4, their emote bubbles (they're arguing);
  - at 2, their speech bubbles (what they're saying).
  
  Behind walls, the player sees nothing. The player and the character always know the same things.
- **Enhanced senses are the progression.** The alien's senses can grow:
  - wider clarity bands;
  - hearing through walls;
  - reading the emotion of someone you can't hear;
  - telling who it is at distance.
- **Fitting in is the game's social core.** The alien has to learn the town's norms by watching reactions, the same way villagers do (law 7). Odd behaviour draws wonder (III P52) and talk about "the newcomer" as a kind of person (law 9). Suspicion is a real pressure that comes out of the ordinary rules.

**Power** (to discuss; see the proposal in section 6a). Sid: "regard plays a part in influence, changes the rules of the town, convince people of things easier. Regard plays a part in power. Power is the ability to direct 'consciousness' (in our case villagers) against its own inertia. Power is plastic and elastic."

## 11b. Decisions of 2026-10-05 (third round): time, sleep and tiers

**No fixed day.** Sid: "I don't like the Stardew Valley 6am - 2am rule. All characters should go to bed when they feel like it. This would play into the acting normal part of the game." The clock runs 24 hours; the date changes at midnight (rule 1).

**Sleep and energy, the same for everyone.** Sid: "You should be able to sleep whenever and you should be able to set an alarm for yourself. You might not always wake up to your alarm. There is a max sleeping time though, you can only sleep until your energy bar is filled. Characters have different max energies and have different jobs which change the routines."

**Pace.** About 20 real minutes per game day to start with. It is hard to set the real length of a day before the mechanics exist, so this is a parameter to revisit. Routines and chats run on 5-minute ticks, acts minute by minute.

**Scandals are rare.** Three scandals a season, as in the first 0a runs, is too many for 12 people. Scandals and the new upheaval tier come from pressure, with the targets in rule 9. Sid liked the upheaval tier.

**Consequences for the harness (0a, second pass):**
- How a scandal spreads is measured on scandals injected at a seeded time and place, because natural ones are too rare to measure in a season.
- How often scandals happen is measured separately, over years, once vices and money exist (0b).
- Until then, villagers' scandal rates are set low (about 1.5 a year town-wide) as a placeholder.

## 11c. Decisions of 2026-10-06: authority, age, runs and other life sims

**Authority** (rule 16). Lewis is mayor and the authority at the start. Every run opens with the town voting in a constable, mostly as story and show for now, so the constable can differ from run to run; it gets more weight with a bigger town. The mayor's office comes up for election later in the game. Lewis is the example of a just and fair authority: he can be swayed, but only with a very small chance. The harshest step is detention for a set time, not being asked to leave; the details come after testing.

**Age and death.** Villagers grow old, and an elder can die of old age (Lewis "may be voted out or get old in life and pass away"). This is within the tone limits of 11a: a natural death, shown plainly and briefly, never murder.

**Sims 4 and other life sims.** Sid: "I think we should take some of the ideas from Sims 4", and draw on other life sims too. Taken (2026-10-06):
- **Aspirations:** life goals the player picks, with milestones. A candidate answer to the main-goal question: there is no single goal, and politics is only needed by the aspirations that want it.
- **Sentiments:** lasting named feelings toward someone, with the event that caused them, fading over time. They put a name and a cause on regard.
- **Lifestyles:** labels earned from repeated behaviour (a night owl, a regular at the saloon). The town learns habits and talks about them, which is acting normal (rule 1).
- **Wants and fears:** short-term wishes and worries that drive motives. Fear of being caught is the deterrence of rule 16.
- **Clubs, or something like them:** groups with members, a meeting place and time, and shared norms. They give more reasons to meet and talk, and a group can back a proposal at the town meeting.

Not taken: needs bars (hunger, bladder, hygiene); controlling anyone but your own character.

**Length of a run.** At 20 real minutes per game day, a 4-season year is about 37 hours of play if every hour is played, or about 24 hours if sleep is skipped. Five years is about 120-190 hours.

**Aging is real time for now** (Sid, 2026-10-06): one game year is one year of age. Within a run, children grow and elders get older slowly; Lewis dying of old age is rare inside one run.

**Runs can rejoin the same world.** Sid: "it might be interesting to rejoin the world you were just in. Maybe 5-10 years pass every run you do." A new run can start in the town the last run left, 5-10 years later. The simulation runs the skipped years headless under the same rules, so the town the player returns to (who married, who left, who is mayor, who died) comes out of what happened, not a script. Whether this happens depends on how play turns out.

**How a run ends** (the player's death, or losing) is saved for later.

## 11d. Decisions of 2026-10-06: families and age

Sid: "We also need to include the family dynamics, Abigail is the daughter of Pierre and Caroline. She could thief but why would she." And: "Age also plays into what a character would do." Agreed: scandals need motives (rule 17), age limits what anyone would do, trouble inside a family stays inside, families cover for each other (including alibis), and shame spreads to kin. All of Stardew's families come into the simulator now: "it should make for much more interesting stories." More family dynamics may follow.

## 11e. Decisions of 2026-10-07: fringe people and character over time

Sid: "I still want to allow for 'fringe' people. People who may be overly shy and become a hermit for example. We still need enough room for emergent personalities. We start out with the Stardew Valley archetypes and the generations that follow should change over time." And: "We need a system for how actions and events change the character over time... The character at the beginning of the run will have different weights than it does 5 years later. Severity of the events contributes to this. Across generations, 2 parents have a kid, the kid will initially be a combination of the parents' weights with some chance for mutation when born. Over time the weights harden as they grow, but never fully. There is chance for change but the older they are the harder it is. Either lots of repetitions or high severity events." The aim is to simulate the 5-10 years between runs (11c).

Agreed:
- **Room for fringe people.** Extremes are allowed and can emerge; no rule pulls behaviour toward the middle. The shy can answer by withdrawing, and withdrawal can deepen.
- **Character changes with events**, by severity and repetition, hardening with age but never fully (rule 18, a proposal).
- **Inheritance:** a child starts as a combination of its parents, with a chance of mutation at birth.
- **Now:** the desire gate (0d) reads every trait from each person's current character, a state that can change, and records the events (acts done and undergone, their severity and how they turned out) that the plasticity rule will feed on. Its constants must behave sensibly at trait extremes.
- **Later:** the plasticity rule, then the life course that generations need (couples, births, children growing up and moving out, deaths) and the time skips. At today's speed a simulated year takes about 8 seconds of one core, so 5-10 years can be simulated in full.

## 12. Questions for Sid

Answered on 2026-10-05: free text from the player, villagers misreading the player, Laya's role (section 5a), Stardew's cast for the experiments, the name (Under Glass), assets (redone if it becomes its own game), the engine (C# for experiments, Godot for the visual build), the world going on without the player, layered perception, Spinoza as the basis, the clock and sleep, and the tiers of scandal (11b).

Still open:
1. **The player's main goal, and the alien premise** (11a). Sid isn't sold on either yet (2026-10-06). The simulation doesn't depend on them, so phase 0 goes ahead; both are for later.
2. **Power** (section 6a): rule changes matter, the meeting is not settled as the main stake, and votes must be worth the effort and optional for players who don't enjoy politics (Sid, 2026-10-06). Still to settle: what kinds of rule are worth voting on.
3. **Life stages:** real-time aging for now (11c). Later: the skips between runs, and what carries over.
4. **How a run ends:** how the player dies or loses. Saved for later.
5. How long to give the headless phase. Suggestion: no fixed date. Gates instead:
   - 0a, the gossip harness, about 2 weeks; it shows whether the rules make stories at all;
   - each next step only once the previous gate passes;
   - a review at 8 weeks either way.

   Sid (2026-10-06) left the 0a gate to the plan above. The gate: **a scandal that someone witnessed reaches 40-70% of the town over 3 or more days in most runs.** It counts witnessed scandals only, because a scandal nobody saw can't spread by gossip; whether it should spread some other way (traces) is a separate question. Met on 2026-10-06: 72% of witnessed placed scandals land in the band. With traces (rule 16 work, the same day), 73% by sight and gossip, or 66% counting people who only found a trace. With suspicion and interviews, 71% and 64%. With every family in (26 villagers, two listeners a teller a day), after retuning to fade 0.8 and retell 0.35: 57% and 51%. The limit is the number of witnesses: a scandal seen by one person who keeps to themselves stays small, and one seen by a crowd at a hub travels far.
6. **Phase 0c's questions** (feelings, 2026-10-06). Each is built one way for now; the answer may change it.
   1. **Starting tensions.** The town starts with none, so every dislike comes from the run. Candidates: Pierre and Shane both ways (the chain store), Sebastian toward Demetrius, Abigail toward Pierre.
   2. **III P24.** Someone who hates the person harmed comes to love whoever harmed them, not only feel glad. Built: yes.
   3. **III P27 cor. 1.** A witness with no strong regard cools a little on whoever harmed someone like them (rule 6 said "toward the target only"). Built: yes.
   4. **Should the constable tell an innocent suspect who named them?** Built: yes, so they resent the namer.
   5. **Retention.** Pam 0.2 (the mod's value), Robin 0.8 (a guess), everyone else 0.5.
   6. **The third slight.** Rule 10's 3-day hostile cooldown per pair makes rule 4's "third slight within 5 days" impossible. A 1-day cooldown, or a wider window? Not built until decided.
   7. **Love after conquered hate.** It gains back up to the depth of the old hate, bounded by the love given since: 0.315 against 0.287 for a pair that never fought. Too strong, too weak?
   8. **E1's definitions.** A feud is -0.3 or below both ways, a friendship 0.4 or above both ways; feuds inside a family are counted apart; the target is 60% of seed-years with both.
   9. **Should love cover the way kinship does?** Built: someone who loves the culprit (0.4 or more) never confronts them, and is less likely to report them.
   10. **Shop choice** per household (one purse), or per adult? Built: per household.
   11. **Sensitivity** from the mod's temperaments, derived from Stardew's dialogue, for the prototype only.
   12. **The mayor's trust leaning on regard** (at most 0.1 for Lewis), or familiarity only? Built: leaning.
   13. **Rule 10's desire gate next?** Feuds between households can't form while acts come at town-wide rates: the hurt can't answer back (rule 10, "what 0c found"). The gate (hate and love as motives toward a person, costs, cooldowns) replaces the rates, so it moves news and trivia volume and has to be retuned against the 0a band.
7. **Character over time** (rule 18, 11e). Answered 2026-10-07: all six traits are plastic; changes stick while they keep being triggered, and people drift slowly toward those they spend time with; children form their parents' grudges and friendships more easily, not certainly. Still open: how inheritance and mutation work, to be researched first.
