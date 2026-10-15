# History evidence audit

Reviewed 2026-10-14 against `b3224f4`. This is a separate evidence audit for Sid and Claude,
not an amendment to the lives assessment or an implementation specification.

The existing pack contains useful material for lived histories. Its 101 anchors include childhood
experiences, losses, learned skills, work, recurring relationships and personal interpretations.
They should not all become remembered events: a family relationship, a repeated practice and a
particular experience are different kinds of knowledge. The next step is to preserve those
differences and connect a small selection to choices with consequences.

Keep law 1 unchanged while building that connection. Nothing in this audit establishes a formula
for need bars, association strength, decay or desirable social outcomes.

## Scope and evidence

All **101 anchors across 34 characters** were read against their referenced local source entries.
Their **140 source references resolve to 110 distinct entries**, with no missing keys in the
case-sensitive export. This confirms source coverage, not that every paraphrase or chronological
assumption is equally strong. All runtime anchor conditions are currently null.

Sources:

- [Current character pack](../../../sim/character-inspiration.json), the exact anchor texts and source keys.
- [Original admission review](character-voices-2026-10-12.md), including the source asset manifest.
- Local `out/stardew-dialogue-2026-10-12/raw-assets.json`, English game version
  `1.6.15.24356`. The earlier case-folded export was not used. Dialogue, event scripts and
  relevant `Data/Characters` family metadata were checked directly; this audit does not repeat
  extraction from the installed XNB files.
- [Character inspiration contract](../specs/character-inspiration-spec.md) and
  [current implementation](../../../sim/UnderGlass.Sim/CharacterInspiration.cs).

File SHA256 values, for reproducing this audit:

| Input | SHA256 |
| --- | --- |
| Character pack file | `b1277a098bcf945d8a485bbb5ba7df9ca893aff095219c8875c509ee9382a63e` |
| Case-sensitive raw export file | `1396930b5fe40578e1a50f9ac07221a017426645d4c79f6234f454f020e27e30` |

These are file hashes, not the runtime's normalized pack hash. Raw game dialogue stays in the
ignored local corpus. The findings below paraphrase it and identify the source entries.

## Findings that matter before seeding histories

### Existing anchors are private context rather than lived memories

`CharacterBackground` contains an ID, text, subjects, sources and an unsupported conditional
field. `Resolve` selects one anchor matching the believed subject, or one general anchor when
there is no match. The contract explicitly prevents background from becoming a witnessed act,
belief, reflection source, social prior or gossip. A character can therefore mention a biography
without having an experience that influences the rest of the decision process.

That distinction should remain visible during migration. Do not describe the existing pack as
101 implemented memories. It also has overlapping accounts: `jas-shane` and `jas-parents` share
material, as do `leah-city` and `leah-kel`. Counting anchors as independent experiences would
overstate both evidence and emotional reinforcement.

### Separate what happened from what it means to someone

Some particularly useful material already contains this distinction:

- **Marnie and the dock:** `Data/Festivals/summer28:Marnie_y2` explicitly links a childhood fall
  through a rotten dock with present trembling at creaking wood. The ordinary `Marnie` entry
  supports reluctance to go farther onto the crowded dock. This is unusually strong evidence for
  an experience, a situational cue and her own account of its continuing effect.
- **Leah and the valley:** `Data/Events/LeahHouse:50/f Leah 500/p Leah` describes a childhood
  visit and her wish to return. Keep the visit, the remembered attraction and the later move
  distinct. The farmer's art-show or internet suggestion is a later event and cannot be seeded.
- **Clint and blacksmithing:** `Characters/Dialogue/Clint:Mon4` attributes his occupation to
  pressure from his father; `Sun8` also expresses pride in doing it well. Neither cancels the
  other. They do not prove what his father intended.
- **Alex and his father:** `Data/Events/Town:2481135/f Alex 1000/t 900 1600` distinguishes his
  recollection of mistreatment from his conjecture about his father's resentment. Preserve that
  uncertainty rather than turning an inferred motive into a world fact.

Recommended corrections to the existing wording, before using it as mechanical evidence:

| Anchor | Finding | Proposed treatment |
| --- | --- | --- |
| `maru-clinic` | `Maru:Wed` says Harvey likes having her available in case equipment fails. It does not recount a repair. | Keep clinic employment and expected technical usefulness; do not seed a completed repair. |
| `linus-tent` | `Linus:Fri` recalls prior tent destruction and asks the listener not to destroy it. General wariness and the causal wording are our synthesis. | Keep the destruction as his account, perpetrator unknown; keep the plea separately. Mark broader visitor distrust as an interpretation to review. |
| `demetrius-valley` | `Demetrius:Tue` says the ecosystem made him excited to move. That is slightly narrower than proving why he moved. | Prefer that the ecosystem appealed to him when he moved; do not make it the established cause of relocation. |
| `alex-childhood` | The cited Town event supports departure, death and moving to grandparents, but does not itself state his age. | Add the Beach recollection cited below to support being young, or remove the age qualifier. The claim is corroborated, not a reason to discard the history. |

Alex's additional evidence is `Data/Events/Beach:288847/f Alex 2000/p Alex/w sunny/G !IS_PASSIVE_FESTIVAL_TODAY SquidFest`.
He recalls his mother's care and says he was too young to thank her adequately. It is also useful
positive history: shared food and playing catch, rather than only bereavement. Do not seed the
farmer's attendance at the anniversary, a response option, or an invented calendar date.

### A later disclosure is different from a later experience

The agreed baseline is established backstory before the farmer arrives. A friendship gate or a
year-two festival line can reveal an older experience. It does not require us to initialize the
conversation in which the farmer learned it.

Use three admission rules:

1. **Explicit old experience:** childhood, prior residence, a completed move, or an expressly
   old accident can be admitted as an undated or roughly dated personal account. Keep only the
   old content, not the later scene's interactions or changes of attitude.
2. **Established role or routine:** employment, residence, household membership and repeated
   habits can be baseline facts or practices. Many sources describe the present when spoken;
   carrying them back to the start is an editorial baseline choice, not proof of a dated episode.
3. **Current reaction or change:** a recent quarrel, a new attachment, a resolution, recovery,
   player gift or completed quest needs independent pre-arrival evidence. Otherwise leave it out.

For example, Shane's `Wed8` supports his account of a sparse childhood family. Its statement
about having grown attached to Marnie and Jas need not describe his state before the farmer
arrives. Likewise `Sat8` is useful voice evidence about chickens, but does not establish when
that attachment formed. Marnie's ordinary `Tue` does support prior chicken care.

Do not copy relative expressions such as yesterday, last week or two years into precise pre-run
dates. The sisters' absent parents are supported; their departure date at our chosen start remains
approximate. No exact start date, frequency or association weight follows from these sources.

### A source speaker is not automatically the future memory holder

Family metadata corroborates many anchors whose cited dialogue names only a spouse's occupation
or children. A sourced parent-child relationship can be baseline self-knowledge without inventing
a scene where the character learned it.

But an account from one participant does not establish another participant's private reading:

- Evelyn recounts meeting George after the snowball. George can be represented as a participant;
  the corpus does not give his remembered feelings about that meeting.
- Sam recalls Kent taking the children to the beach and taking him to the desert. Those support
  shared activities, not identical recall, enjoyment or confidence for everyone present.
- Emily recalls childhood circus jokes with Sandy. Sandy's involvement is supported, while her
  interpretation is not separately supplied by that line.
- Penny knows the household consequences of Pam's nights out. That is not evidence that Penny
  watched every drink at the saloon or knows Pam's reason for drinking.

Keep authoring provenance separate from in-world acquisition. The player's later conversation
with Sam is our source for a past beach outing, not a conversation Vincent must remember hearing.
The current `subjects` list remains a relevance hint, not a list of people who know an anchor.

## Recommended pilot and supporting evidence

### Penny Pam and Gus as the first complete thread

This three-person cast gives us overlapping but different relationships with one place, concrete
requests, family care and conflict. It can connect to the existing errands direction while leaving
the implementation assessment to Claude.

| Holder | Evidence to prepare | Supported reading and boundary |
| --- | --- | --- |
| Penny | Existing `penny-family`; `Characters/Dialogue/Penny:fall_Fri6` | Her father left during childhood; his stated reason is something she reports. Do not invent his actual motive or make every absence mean abandonment. |
| Penny | `Characters/Dialogue/Penny:Mon4`, first statement before the player question; `Fri4`; `Sun4` | She regards her mother's saloon visits as a problem and describes hangovers and household work falling to her. These support her experienced burden and account of a recurring pattern. Do not seed the farmer's advice or a particular fabricated night. |
| Penny | Existing `penny-teaching`; `Characters/Dialogue/Penny:Wed` | Teaching is a valued activity with a stated concern for the children's opportunities. It gives her something to pursue beyond managing Pam. |
| Pam | Existing `pam-truck`, `pam-bus`; `Characters/Dialogue/Pam:fall_Sun4`, unrepaired-bus branch of `Wed` | Past work and remembered enjoyment of travel, people and radio; current unemployment. Do not assume her drinking was caused by job loss. |
| Pam | `Characters/Dialogue/Pam:Thu`, daughter statement; `Sun` | Protective language toward Penny and appreciation of Gus, including a described birthday practice. Do not initialize the preceding last-night hangover as an actual dated event. |
| Gus | Existing `gus-regulars`, `gus-emily`; `Characters/Dialogue/Gus:Thu`, `Wed` | Familiar customers matter to his business; he values Emily's work. He does not thereby know Penny's private account or approve of everything Pam does. |

The candidate loop is a **new run outcome**, not reconstructed canon: a household need makes
Penny recall both care and burden; she considers helping, offering a different form of help,
declining, or returning to her own plans; Pam answers; an agreed practical task succeeds or
fails; each person learns from what they actually experienced. Gus can participate when a real
purchase or encounter involves him. Whether housework is the first task or an errand supplies it
depends on Claude's assessment of the action model.

Preserve Sid's existing decision that Penny will not fetch alcohol, recorded in
[the decisions log](../../decisions.md), under Sid's answer on Penny, 2026-10-13. That is a product
commitment informed by her characterization; the cited dialogue does not mechanically prove an
absolute prohibition. History should explain this boundary without quietly replacing it with a
random chance of compliance. A refusal of alcohol need not mean a refusal of care.

The preparation still needs an explicit account of Penny's care for Pam. Sid has supplied that
direction; kinship alone should not be used as proof of affection or an arbitrary positive score.
Likewise, no observed outcome is required to improve the household. Refusal, resentment,
misunderstanding and failed repair remain legitimate consequences.

### Other casts and transfer cases

| Cast or character | Why it is useful | Limits for the first slice |
| --- | --- | --- |
| Evelyn, George and Alex | Completed histories, ordinary care, pride and friction. Evelyn's `Fri` wishes George would go outside; George's `Wed8` wishes he could play catch with Alex; Alex's `Fri4` expresses love for his grandmother. | Good second household to test that the mechanism is not specific to alcohol or crisis. Do not invent a repeated argument or assume George rejects all assistance. |
| Robin, Demetrius, Maru and Sebastian | Maru's `Sat` enjoys lab work; `summer_Tue4` wants a relationship with Sebastian; Sebastian's `Thu4` voices jealousy and `fall_Tue6` resists Demetrius's authority. Robin's `Fri4` expresses concern. | Four distinct readings, but many useful lines describe present feelings rather than dated old episodes. Exclude the recent ashtray discovery and specific seasonal disputes as pre-run events without chronology evidence. |
| Emily and Haley | Parental absence, Emily's tailoring and work, different views of household contributions. `Emily:Mon6` and `Haley:summer_Sun` support the differing accounts of chores and cooking. | Do not seed the farmer-mediated cushion dispute or its solution from `Data/Events/HaleyHouse:11/f Haley 500/p Haley/p Emily`. The reviewed material is stronger for friction than for a specific shared warm memory; do not invent one to balance scores. |
| Marnie | Childhood dock accident and present cue, with an explicit personal causal account. | Best compact negative-association case; not by itself a complete social thread. |
| Leah | Childhood visit, later return, city work and artistic ambition. | Strong positive place association alongside pressures around work. The ex-partner's motives remain her account. |
| Willy | Childhood fishing, inherited boat and a fishing lesson from his father. | Useful positive activity and skill history for a later making/learning thread. Do not seed the farmer's boat repair or bug-meat delivery. |

These extra dialogue entries are candidates for a future history pack, not additions made by this
audit. Present-state material still needs an explicit baseline decision under the rules above.

## Preparing histories that can scale

Prepare a small set of reusable experience forms: learning from someone, losing someone,
receiving care, experiencing pressure, sharing an activity, moving somewhere, and recurring
household burdens. Named Stardew biographies supply examples and source receipts. They do not
belong in inherited archetypes, and a form must not invent a biography merely to fill every slot.

Each prepared history should distinguish:

- The experience or ongoing circumstance, including whether its time and repetition are known.
- Who participated and whose account is available. An account may be incomplete or mistaken.
- Any explicitly sourced feeling or interpretation, separate from an editor's proposed inference.
- Relevant people, places, activities or cues, without making those associations universal.
- What the character could plausibly recall, expect or attempt now; proposed behavioral effects
  should remain labeled as proposals until the simulation produces them.

Ended histories do not need to be replayable by today's action catalog. George's accident and
Penny's father's departure can matter even if the engine cannot stage mining accidents or family
departures yet. What the first slice does need is at least one meaningful **present** response and
its consequences.

For population growth, keep provenance available for inspection while selecting bounded relevant
accounts for a decision. Do not require a model to reread every resident's biography at every
choice. The same forms should accept future characters' in-run experiences without special
branches for Penny, Pam or any other name. Firm individual commitments can be data; they need
not be hardcoded into the general history mechanism.

Useful acceptance cases for Claude's later implementation work:

1. One prior experience recalls differently for different participants, without sharing private
   interpretations between them.
2. A person retains both welcome and apprehension toward the same person, place or activity.
3. A completed past experience affects a present choice without being replayed as a new act.
4. A new outcome changes an expectation; it is not automatically erased by an immutable starting
   relationship value.
5. A failed search, a busy answer and a refusal remain distinguishable experiences.
6. A new character can use the same history forms; donor biography is not inherited with an
   archetype, and unobserved characters continue living.

## Anchor by anchor review

Kinds below are editorial distinctions, not a proposed runtime enum:
**Fact** is identity, kinship, possession or established role; **past** is a completed experience;
**practice** is recurring activity; **reading** is an interpretation, desire or preference.
Multiple kinds mean the anchor should be separated before mechanical use. Retain means the
content is usable within its stated scope, not that it is already implemented as a memory.

Each row identifies the exact anchor in the linked pack and original admission review, where its
full asset and key references are preserved. Additional corroboration and cautions appear here.
General baseline and knowledge-holder rules above apply to all rows.

### Abigail

| Anchor | Kind | Finding |
| --- | --- | --- |
| `abigail-family` | Fact | Retain. Caroline's introduction identifies Abigail and her shopkeeper husband; `Data/Characters:Abigail` explicitly identifies both parents. |
| `abigail-valley` | Fact, reading | Retain lifelong residence separately from longing for adventure. `Event_Grave1/2` disclose it through alternate answers; do not seed the graveyard encounter. |
| `abigail-david` | Fact | Retain pet and name. The summer heat complaint is a present situation, not a pre-run incident. |

### Alex

| Anchor | Kind | Finding |
| --- | --- | --- |
| `alex-grandparents` | Fact | Retain; family metadata explicitly corroborates both names. Do not turn the kinship itself into a positive event. |
| `alex-childhood` | Past | Split father's departure, mother's death and move to grandparents. Add the Beach source above for the age qualifier. Do not assign an exact date. |
| `alex-quarterback` | Past, reading | Retain as Alex's account of school achievement. Prestige and confidence are his reading, not a fixed confidence score. |

### Caroline

| Anchor | Kind | Finding |
| --- | --- | --- |
| `caroline-family` | Fact | Retain; family metadata names Pierre. |
| `caroline-ranch-dream` | Past, reading | Retain childhood westerns and ranching aspiration; do not infer actual ranching experience. |
| `caroline-gardens` | Practice | Retain a described habit of helping Evelyn on fine days. No particular visit, frequency or reciprocal feeling is established. |

### Clint

| Anchor | Kind | Finding |
| --- | --- | --- |
| `clint-family-trade` | Fact | Retain multigenerational occupation. `Mon_old` supplies the great-grandfather detail; joke responses are not rival biographies. |
| `clint-pressure` | Past, reading | Retain his account of paternal pressure. Do not initialize the father's intent; do not infer he despises all blacksmithing. |
| `clint-work` | Fact, reading | Separate occupation from expressed pride in quality. His late disclosure of pride does not date its onset. Preserve it alongside pressure. |

### Demetrius

| Anchor | Kind | Finding |
| --- | --- | --- |
| `demetrius-maru` | Fact, practice | Retain daughter and recurring assistance. No specific experiment or competence gain is established. |
| `demetrius-robin` | Fact | Retain; `Data/Characters:Demetrius` names Robin. `Robin:Introduction` adds no marriage evidence; `Sun2` must not initialize the last-night explosion. |
| `demetrius-valley` | Past, reading | Narrow from reason for moving to an aspect of the move he found exciting; see correction table. |

### Dwarf

| Anchor | Kind | Finding |
| --- | --- | --- |
| `dwarf-shop` | Past, reading | Retain concealment and Dwarf's explanation about human fear separately. Do not treat that generalization as knowledge of each human. |
| `dwarf-property` | Practice, reading | Retain described taking of supplies and unfamiliarity with ownership rules. Do not invent victims or theft events. |
| `dwarf-ancestors` | Fact, reading | Retain cultural knowledge and uncertainty about origins. This is not personally witnessed ancient history. |

### Elliott

| Anchor | Kind | Finding |
| --- | --- | --- |
| `elliott-move` | Past, reading | Retain move before arrival and stated desire for quiet writing. A positive expectation of the beach is more defensible than automatic happiness there. |
| `elliott-ambition` | Past, reading | Retain longstanding aspiration and his report of discouragement back home. Exclude the farmer's encouragement and genre choice in the disclosure scene. |
| `elliott-piano` | Practice | Retain childhood piano playing from `howLong`. It answers a question in a later scene; do not seed that scene or infer expert skill. |

### Emily

| Anchor | Kind | Finding |
| --- | --- | --- |
| `emily-household` | Fact, past | Retain sister and parents' departure with household entrusted to them. Treat elapsed travel time as approximate at the chosen baseline. |
| `emily-tailoring` | Fact, practice, reading | Separate paid saloon work, making clothes and preference for tailoring. Clothing she says she made supports practical experience, not a particular delivery event. |
| `emily-sandy` | Past | Retain childhood jokes; year-two festival is disclosure time. Do not invent a serious escape plan or actual circus employment. |

### Evelyn

| Anchor | Kind | Finding |
| --- | --- | --- |
| `evelyn-george` | Fact | Retain marriage. Advice about his warming to strangers is Evelyn's expectation, not a guaranteed behavior. |
| `evelyn-cookies` | Past, practice | Retain learning the recipe from her mother and continuing to use it. No exact lesson or date supplied. |
| `evelyn-gardens` | Fact, past | Retain responsibility for public gardens as baseline role. Appointment date is unknown; exclude the previous day's butterfly sighting. |

### George

| Anchor | Kind | Finding |
| --- | --- | --- |
| `george-family` | Fact | Retain wife and grandson; explicit family metadata corroborates both. |
| `george-mine` | Past | Retain accident and loss of ability to walk as his historical account. Keep the accident distinct from present feelings about help; no precise simulation date. |
| `george-evelyn-meeting` | Past | Retain shared meeting from Evelyn's account. George's involvement is supported; his remembered feelings are not independently supplied. |

### Gus

| Anchor | Kind | Finding |
| --- | --- | --- |
| `gus-saloon` | Fact | Retain occupation and ownership. No remembered founding or purchase episode. |
| `gus-emily` | Fact, practice | Retain established working relationship. His appreciation in the source can be a separately attributed reading. |
| `gus-regulars` | Practice | Retain repeated patronage by Pam and Clint; no fabricated dated visits. His business dependence is his stated assessment. |

### Haley

| Anchor | Kind | Finding |
| --- | --- | --- |
| `haley-household` | Fact, past | Retain sister and shared household responsibility from Emily's account. Do not transfer Emily's enjoyment of living there to Haley. |
| `haley-bracelet` | Past, reading | Retain inherited bracelet and its significance. Exclude losing it and the farmer finding it during the later scene. |
| `haley-flower-queen` | Past, reading | Retain past selections; no exact pre-run contest dates or automatic future victory. Her desire to retain the title is separate. |

### Harvey

| Anchor | Kind | Finding |
| --- | --- | --- |
| `harvey-practice` | Past, fact, reading | Retain move, occupation and his stated reasons. Do not treat current satisfaction as guaranteed by location. |
| `harvey-pilot` | Past, reading | Retain childhood aspiration and reported impediments. Exclude the plane-watching scene, its newly completed model and any later resolution of fear. |
| `harvey-home` | Fact | Retain residence above clinic. The farmer's invitation is not a starting relationship. |

### Jas

| Anchor | Kind | Finding |
| --- | --- | --- |
| `jas-marnie` | Fact | Retain; aunt address is explicit and `Data/Characters:Marnie` also identifies Jas as niece. |
| `jas-shane` | Fact, past | Retain godfather and prior friendship with her parents; disclosure threshold is not the origin of the relationship. |
| `jas-parents` | Past | Retain bereavement without cause, date or invented witnessed death. Share provenance with `jas-shane` rather than counting the overlap as reinforcement. |

### Jodi

| Anchor | Kind | Finding |
| --- | --- | --- |
| `jodi-family` | Fact | Retain; `Data/Characters:Jodi` explicitly names husband and both sons. Exclude the dream described in `Sun8` as a specific pre-run occurrence. |
| `jodi-marriage` | Past, reading | Retain hurried marriage and her sense of lost freedom separately. The source does not say she regrets having her sons. |
| `jodi-caroline` | Practice, reading | Retain self-described close friendship and confiding. No specific secret or universal access to Jodi's thoughts for Caroline. |

### Kent

| Anchor | Kind | Finding |
| --- | --- | --- |
| `kent-family` | Fact | Retain; family metadata corroborates. His prototype presence does not authorize seeding his return from war before the farmer arrives. |
| `kent-city-work` | Past | Retain earlier refuse-collection work reported by Sam. Kent's own experience of the work is not provided. |
| `kent-desert` | Past, practice | Retain earlier trips with Sam. No exact itinerary, purpose or feelings from Kent are established. |

### Krobus

| Anchor | Kind | Finding |
| --- | --- | --- |
| `krobus-light` | Fact, reading | Separate sunlight sensitivity, sewer residence and his assessment of its suitability. No fabricated sunlight injury. |
| `krobus-friday` | Practice, reading | Retain religious observance with its stated meaning. Not an event on every seeded Friday. |
| `krobus-name` | Fact | Retain linguistic self-knowledge. No inferred event of crossing a bridge. |

### Leah

| Anchor | Kind | Finding |
| --- | --- | --- |
| `leah-childhood-visit` | Past, reading | Retain childhood visit and longstanding wish to return. Exclude the farmer's later art-business suggestion. |
| `leah-city` | Past, practice | Retain city life, odd jobs and night-time art. Do not fabricate work shifts or specific finished objects. |
| `leah-kel` | Past, reading | Keep past pressure and Leah's decision in her account. Do not infer Kel's unspoken motives, choose a player-dependent gender, or seed the later phone call. |

### Leo

| Anchor | Kind | Finding |
| --- | --- | --- |
| `leo-parrots` | Fact, practice, reading | Retain upbringing with parrots and family identification. The later speech does not prove the same fluent articulation before meeting the farmer. |
| `leo-speaking` | Practice | Retain ability to understand parrots. Do not seed recovered name, settled understanding of parental loss, or mainland relocation from the surrounding scenes. |

### Lewis

| Anchor | Kind | Finding |
| --- | --- | --- |
| `lewis-mayor` | Fact, past | Retain long tenure; no precise election chronology. Lack of opposition does not prove residents approve of him. |
| `lewis-unmarried` | Fact, reading | Retain never-married status and his description of a life centered on office. Do not infer absence of intimacy or initialize that evening's regret. |
| `lewis-gardening` | Practice | Retain hobby; no invented garden visit or shared activity. |

### Linus

| Anchor | Kind | Finding |
| --- | --- | --- |
| `linus-choice` | Fact, reading | Retain chosen wilderness residence. It does not imply every hardship is wanted or all company rejected. |
| `linus-tent` | Past, reading | Separate reported destruction, present plea and inferred general wariness; see correction table. No known perpetrator. |
| `linus-travels` | Past | Retain learning igloo-building from tundra dwellers long ago. No exact place, route or date beyond the source. |

### Marnie

| Anchor | Kind | Finding |
| --- | --- | --- |
| `marnie-family` | Fact | Retain nephew and niece; metadata corroborates both. Use the unmarried-Shane branch for residence and chicken-care context. |
| `marnie-ranch` | Fact | Retain business and goods sold. No implied founding episode. |
| `marnie-dock` | Past, reading | Retain childhood accident and explicitly connected fear of creaking wood. Year-two disclosure does not make the childhood event new. |

### Maru

| Anchor | Kind | Finding |
| --- | --- | --- |
| `maru-parents` | Fact | Retain; `Data/Characters:Maru` explicitly names Robin and Demetrius. |
| `maru-sebastian` | Fact | Retain half-brother. Her wish for closeness needs its own source and account, not automatic warmth from kinship. |
| `maru-clinic` | Fact, anticipated role | Narrow equipment claim to availability if something fails. A successful prior repair is not established. |

### Pam

| Anchor | Kind | Finding |
| --- | --- | --- |
| `pam-penny` | Fact | Retain daughter; protective wording can be a separate reading. Exclude the surrounding last-night drinking as a dated seed. |
| `pam-truck` | Past, practice, reading | Retain former work and remembered enjoyable aspects. Radio and travel can be candidate cues without invented episodes. |
| `pam-bus` | Past, fact | Retain layoff using the unrepaired-bus branch. Do not seed return to work or the restored-bus branch's renewed vitality. |

### Penny

| Anchor | Kind | Finding |
| --- | --- | --- |
| `penny-family` | Fact, past | Separate mother from father's departure. Keep father's explanation as reported speech, not access to his motives. |
| `penny-town` | Fact, reading | Retain lifelong residence. The source's feeling of missed experiences is a separately attributed reading. |
| `penny-teaching` | Practice, reading | Retain teaching and her stated concern for opportunity. Do not assign the children identical feelings or learning outcomes. |

### Pierre

| Anchor | Kind | Finding |
| --- | --- | --- |
| `pierre-family` | Fact | Retain socially established wife and daughter; metadata explicit. Do not turn unrelated paternity speculation into a hidden truth. |
| `pierre-shop` | Fact | Retain ownership. No history of trade with the new farmer. |
| `pierre-boxing` | Past, reading | Retain self-reported boxing history and pride. His boast is not an objective combat statistic. |

### Robin

| Anchor | Kind | Finding |
| --- | --- | --- |
| `robin-family` | Fact | Retain; family metadata explicit. Do not import the previous-night experiment from `Sun2`. |
| `robin-house` | Past, reading | Retain building the house and expressed pride. No invented construction dates, helpers or household debts. |
| `robin-choice` | Past, reading | Retain her report of parents' bewilderment. Do not upgrade it to coercion, rejection or estrangement. |

### Sam

| Anchor | Kind | Finding |
| --- | --- | --- |
| `sam-family` | Fact | Retain; family metadata identifies parents and younger brother. |
| `sam-city` | Past | Retain former Zuzu City residence. Present comparison of noise can inform his account without deciding which place he must prefer. |
| `sam-sebastian` | Practice, reading | Retain friendship, supported in both characters' dialogue. Do not seed the farmer as another old friend. |

### Sandy

| Anchor | Kind | Finding |
| --- | --- | --- |
| `sandy-name` | Fact, past | Retain business name and adoption of it. Original name and precise renaming circumstances remain unknown. |
| `sandy-emily` | Past, practice | Retain friendship and participation in childhood jokes, with Emily as source for the childhood account. Do not invent Sandy's feelings then. |
| `sandy-shop` | Fact | Retain established shop role. The introductory scene takes place after bus restoration; do not seed it or the letter about the farmer. |

### Sebastian

| Anchor | Kind | Finding |
| --- | --- | --- |
| `sebastian-family` | Fact | Retain mother, half-sister and stepfather through combined family evidence. His resentment of authority is a separate reading. |
| `sebastian-work` | Fact, reading | Separate occupation from aspiration to move out. That aspiration is voiced in a later scene; admit as a baseline choice, not a dated old resolution. Exclude the instant message and interruption. |
| `sebastian-sam` | Practice, reading | Retain friendship and his description of their conversational habit. Not every meeting must follow that pattern. |

### Shane

| Anchor | Kind | Finding |
| --- | --- | --- |
| `shane-family` | Fact | Retain aunt and goddaughter; Marnie and Shane metadata corroborate kinship. Do not infer a formal guardianship transfer or its circumstances. |
| `shane-childhood` | Past, reading | Retain his account of lacking much childhood family. Exclude `Wed8`'s potentially later growth of attachment from the baseline. |
| `shane-chickens` | Practice | Retain recurring help supported by Marnie's unmarried-Shane branch. No automatic recovery, new breed or invented lesson with Jas. |

### Vincent

| Anchor | Kind | Finding |
| --- | --- | --- |
| `vincent-family` | Fact | Retain through Jodi, Kent and Sam metadata plus Vincent's brother reference. His admiration is separate from kinship. |
| `vincent-reading` | Practice, reading | Retain weekly assigned reading and some choice of books. Do not initialize book titles or completed reading outcomes. |
| `vincent-beach` | Past, practice | Retain father's earlier outings as reported by Sam. Exclude the later overheard war report, reassurance and band discussion. |

### Willy

| Anchor | Kind | Finding |
| --- | --- | --- |
| `willy-upbringing` | Past, practice | Retain seafaring childhood and father's occupation. No exact voyages or catches. |
| `willy-boat` | Fact, past | Retain father's ownership and long service. The farmer's later access, repair request and material delivery are not baseline memories. |
| `willy-lessons` | Past | Retain a fishing lesson from his father. Exclude the present bug-meat delivery, thanks and reward to the farmer. |

### Wizard

| Anchor | Kind | Finding |
| --- | --- | --- |
| `wizard-marriage` | Past | Retain marriage and separation. Any account of blame, anger or curses belongs to the speaker; do not seed the farmer's ink quest. |
| `wizard-hut` | Past, reading | Retain deliberate site choice and his assessment of its magical field. That assessment need not be an independently measured world property. |
| `wizard-welwick` | Practice, reading | Retain longstanding acquaintance, friendship and his appraisal of her divination. Do not infer reciprocal feelings or predictive accuracy. |

## Delivery boundary

This audit changes no character data, simulation behavior, roadmap, needs-bars document or PR.
Claude's `lives-gap-assessment.md` remains separate. The next useful handoff is to select the
pilot's small set of accounts and the present action that can complete their causal thread;
the source distinctions and corrections above should accompany that work.
