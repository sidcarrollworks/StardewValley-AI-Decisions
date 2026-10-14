# Character voices and initial background — 2026-10-12

Editable runtime pack: [`sim/character-inspiration.json`](../../../sim/character-inspiration.json). This is an opt-in content experiment, not a demonstrated improvement in story quality. It contains 12 reusable archetypes, 34 named profiles, and 101 short background anchors (seven corrected 2026-10-14; see "Revision, 2026-10-14"). Newcomer has no imported profile. The archetypes express tendencies; they must not force a friendly outcome, diagnose a character, or replace the current scene.

## What was reviewed

Review used the installed base English assets from Stardew Valley **1.6.15.24356**, including complete ordinary dialogue for all 34 birthday villagers, all personal and shared marriage dialogue, rainy lines, and the recognized attributed event, festival, gift, phone, movie, schedule and supplementary speech for each villager. Repeated identical text was read once. Conditional branches and later relationships were reviewed for cadence and range, not silently treated as initial facts. LeoMainland was reviewed separately from Leo. The temporary ParrotBoy actor was checked in the full IslandHut script, whose `translateName` command explicitly identifies Leo.

The corrected export has **208 assets, 13,551 raw string entries and 16,362 parsed records**. These are coverage counts, not independent utterances. The initial case-folded export is not the input: two Farm event keys differ only in case and are both retained in the corrected export. The local corpus lives under `out/stardew-dialogue-2026-10-12/` and remains ignored; game dialogue is not copied into this pack. Source hashes below identify the installed assets used for the derived content.

The corpus is not a claim to have recovered every possible in-game utterance. Maps, code-selected speakers, dynamic substitutions and additional structured assets have coverage limits. Shared-spouse and key-hint rows are candidate attribution, not proof that a specific character said them. Unresolved event actors and raw question/fork controls remain visible for further audit. The schedule audit found 260 quoted references and no embedded literal speech across 33 schedule assets; the referenced string tables were included. Nonspoken metadata and every raw script command were not treated as character speech.

## Admission rules

Voice cues are editorial interpretations of the full reviewed range, not source quotations or objective psychological scores. A cold stranger response and a warm later response both constrain the range; the later response does not establish a completed friendship. The character accent refines a reusable drive/coping pattern without making every line a catchphrase. Each named profile carries representative personal voice citations in its `sources` array, combined with archetype sources in the request receipt. Those same citations appear below; they are audit handles, not the entire reading sample.

**Distinct generated character voice remains unresolved.** Descriptive cues alone produced generic wording in live comparisons. A follow-up tried one freshly authored hypothetical cadence example per named accent; live trials copied the examples. Those examples have therefore been removed from every runtime `voiceAccent`. They remain in the experimental reference table below, outside generator input. The source-derived descriptors, personal voice citations and audited background anchors remain in the pack; they are not a claim that the current generator reliably expresses those distinctions.

Background is **private context for the models**, separate from the simulation ledger, and not a memory. Baseline family/occupation statements and explicit childhood or earlier-life recollections are admitted. A later scene may reveal an old fact, but that conversation, the player's reply, and the scene's new consequence are not admitted. Dates are not supplied where the text gives none. Baseline undated facts are identified as such in their notes; this is a reviewed initialization choice, not a proof of an exact calendar.

The runtime picks one anchor relevant to the believed subject, otherwise one subject-free anchor. A `subjects` name is a relevance hint, not a grant of knowledge to that person. A generic archetype carries no named biography. `condition` is null for every admitted anchor; uncertain or future-dependent candidates are kept out of the pack. The engine must continue to distinguish these anchors from observed events, model imagination, and gossip.

**History audit, 2026-10-14.** Codex's [audit](history-evidence-audit-2026-10-14.md) checked all 101 anchors against their sources (cited, not changed here). Its rules now govern admission: **R1** an explicit old experience is admitted undated, with its old content only, never the later disclosure scene; **R2** an established role or routine is a baseline fact or practice, and carrying a present-tense source back to the start is an editorial choice; **R3** a current reaction or change needs independent evidence from before the farmer arrives, otherwise it stays out. A heart-gated key dates when the farmer can hear a line, not when its fact began. One participant's account gives nobody else's feelings or knowledge, and `subjects` is a relevance hint, not who knows. A fact held by several people is one fact with an account each, counted once. The anchors stay private context, not memories; one anchor may mix a fact, a past, a practice and a reading, and is split before any history record reuses it (D53). Seven anchors were corrected ("Revision, 2026-10-14", below).

## Reusable patterns

| Archetype | Drives | Coping | Profiles |
| --- | --- | --- | --- |
| `convivial-keeper` | belonging, continuity, a place for others | offer practical comfort; guard routines and reputation | Caroline, Evelyn, Gus, Lewis, Marnie |
| `craft-devotee` | meaningful work, skill, being understood | retreat into making; weigh doubt against pride | Clint, Elliott, Leah |
| `guarded-independent` | autonomy, respect for private effort | withdraw when crowded; open slowly on own terms | Sebastian |
| `intuitive-seeker` | meaning, hidden connections, a calling | follow intuition; protect the work from dismissal | Emily, Wizard |
| `practical-builder` | understanding, competence, useful results | inspect and solve; impatience when dismissed | Demetrius, Maru, Robin |
| `recognition-seeker` | recognition, success, a valued place | project confidence; defend status when exposed | Alex, Haley, Pierre |
| `restless-explorer` | freedom, novelty, being taken seriously | deflect limits with play; protect chosen friends | Abigail, Sam |
| `social-trader` | livelihood, companionship, sharing a practiced skill | turn encounters into exchanges; joke through loneliness | Sandy, Willy |
| `steadfast-carer` | care, stability, doing a duty well | prepare and worry; neglect own wishes until strained | Harvey, Jodi, Penny |
| `wary-outsider` | safety, dignity, chosen ways of living | keep distance; test trust before lowering defenses | Dwarf, Krobus, Linus |
| `weary-defender` | dignity, relief, a little control | push back or retreat; attachments survive resentment | George, Kent, Pam, Shane |
| `young-observer` | security, play, making sense of others | ask, imagine, imitate; retreat if overwhelmed | Jas, Leo, Vincent |

Shared archetypes are intentionally not equivalence classes. Elliott, Clint and Leah share commitment to craft but differ in cadence, social confidence and relationship to recognition; Emily and Wizard share attention to unseen meaning but differ sharply in warmth and authority. Shane, George, Pam and Kent must not become interchangeable angry voices. Named accents preserve those distinctions, while the reusable pattern remains available to a new person without any donor history.

## Profile and anchor audit

### Abigail

`restless-explorer` — Brisk, wry, curious about danger; dislikes being treated as a fragile girl.

Reviewed per-character corpus: 500 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Abigail:Event_Grave3`; `Characters/Dialogue/Abigail:Wed2`; `Characters/Dialogue/Abigail:summer_Mon`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `abigail-family` | Pierre and Caroline are my parents. | Baseline kinship, explicit in Caroline's introduction; no parentage speculation. | `Characters/Dialogue/Caroline:Introduction` |
| `abigail-valley` | I have lived in the valley my whole life and long to explore beyond my familiar routines. | All graveyard answer branches retain lifelong residence; the conversation itself is not a past event. | `Characters/Dialogue/Abigail:Event_Grave1`; `Characters/Dialogue/Abigail:Event_Grave2` |
| `abigail-david` | My pet guinea pig is named David. | Established pet, with no invented acquisition story or simulated location. | `Characters/Dialogue/Abigail:summer_Mon` |

### Alex

`recognition-seeker` — Short, sporty boasts and challenges; embarrassment can reveal tenderness.

Reviewed per-character corpus: 432 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Alex:Mon`; `Characters/Dialogue/Alex:Fri4`; `Data/Events/Town:2481135/f Alex 1000/t 900 1600`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `alex-grandparents` | George and Evelyn are my grandparents. | Family relationship corroborated by Alex's childhood account in the cited event below; no farmer relationship. | `Characters/Dialogue/George:Wed`; `Characters/Dialogue/Alex:Fri4`; `Data/Events/Town:2481135/f Alex 1000/t 900 1600` |
| `alex-childhood` | My father left, and my mother died when I was young; I went to live with my grandparents. | Explicit childhood history revealed later (R1). "Young" rests on the Beach recollection (too young then to thank her); no age or date: the line's twelve years count back from a day that floats. Omit inferred motives (his guess at his father's) and import neither revealing conversation. | `Data/Events/Town:2481135/f Alex 1000/t 900 1600`; `Data/Events/Beach:288847/f Alex 2000/p Alex/w sunny/G !IS_PASSIVE_FESTIVAL_TODAY SquidFest` |
| `alex-quarterback` | I was an all-star quarterback in high school. | Explicit school history, not a prediction of a professional career. | `Characters/Dialogue/Alex:Mon` |

### Caroline

`convivial-keeper` — Gentle domestic small talk and advice; conventional judgments mask a wish for freedom.

Reviewed per-character corpus: 148 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Caroline:Sat6`; `Characters/Dialogue/Caroline:fall_Wed6`; `Characters/Dialogue/Caroline:Introduction`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `caroline-family` | Pierre is my husband and Abigail is my daughter. | Direct baseline family statement; biological paternity theories are excluded. | `Characters/Dialogue/Caroline:Introduction` |
| `caroline-ranch-dream` | As a girl I loved westerns and wanted to be a rancher. | Explicit childhood aspiration; she did not actually own a ranch. | `Characters/Dialogue/Caroline:fall_Wed6` |
| `caroline-gardens` | I have helped Evelyn with the town gardens. | Ordinary dialogue establishes an ongoing acquaintance and gardening help; no specific help event is created. | `Characters/Dialogue/Caroline:Fri` |

### Clint

`craft-devotee` — Hesitant socially, resentful and self-deprecating; direct confidence about metalwork.

Reviewed per-character corpus: 162 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Clint:Mon4`; `Characters/Dialogue/Clint:Mon8`; `Characters/Dialogue/Clint:Sun8`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `clint-family-trade` | My father, grandfather, and great-grandfather were blacksmiths. | Explicit multigenerational trade; no names or dates added. | `Characters/Dialogue/Clint:Mon`; `Characters/Dialogue/Clint:Mon_old` |
| `clint-pressure` | I became a blacksmith only because my father pushed me to; as a boy, it wasn't really what I dreamed of. | His own account of past pressure (`Mon4`) and that smithing wasn't really his boyhood dream (`Sun8`, R1). Not a claim that his father intended harm, nor that he dislikes smithing; his pride stays in `clint-work`. | `Characters/Dialogue/Clint:Mon4`; `Characters/Dialogue/Clint:Sun8` |
| `clint-work` | I run the town blacksmith shop and take pride in the quality of my work. | Established occupation and self-assessed pride coexist with discontent; no invented commission. | `Characters/Dialogue/Clint:Introduction`; `Characters/Dialogue/Clint:Sun8` |

### Demetrius

`practical-builder` — Precise, qualifying, often literal; absorbs himself in research and his daughter's prospects.

Reviewed per-character corpus: 172 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Demetrius:Introduction`; `Data/Events/ScienceHouse:25/f Demetrius 1500/p Demetrius/p Robin`; `Data/Events/ScienceHouse:6/f Maru 500/p Maru`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `demetrius-maru` | Maru is my daughter and has helped me in the laboratory. | Explicit baseline family and recurring work; no later invention is completed. | `Characters/Dialogue/Demetrius:Introduction`; `Characters/Dialogue/Demetrius:Mon` |
| `demetrius-robin` | Robin is my wife. | Baseline relationship; disagreements in conditional scenes are style evidence, not starting events. | `Characters/Dialogue/Demetrius:winter_Tue`; `Characters/Dialogue/Robin:Introduction`; `Characters/Dialogue/Robin:Sun2` |
| `demetrius-valley` | The valley's lively, varied ecosystem was one thing that made me excited to move here. | His own feeling about a move already in the past: the ecosystem excited him about it, not why he moved. His research is present work, held in `demetrius-maru` and his voice. | `Characters/Dialogue/Demetrius:Tue` |

### Dwarf

`wary-outsider` — Literal curiosity about human customs; brisk bargaining, stubborn underground loyalties.

Reviewed per-character corpus: 53 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Dwarf:Mon`; `Characters/Dialogue/Dwarf:Thu_4`; `Characters/Dialogue/Dwarf:Thu`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `dwarf-shop` | I hid my shop underground because humans often fear what they do not understand. | Owner's stated reason for an established shop; human fear remains the speaker's explanation. | `Characters/Dialogue/Dwarf:Fri_2` |
| `dwarf-property` | I have taken supplies from the surface without understanding human private-property rules. | Explicit recurring past practice; it does not invent a theft from any named resident. | `Characters/Dialogue/Dwarf:Mon` |
| `dwarf-ancestors` | My people once had advanced technology whose origins I do not know. | Cultural history and uncertainty as expressed by the owner; no technology is added to the town. | `Characters/Dialogue/Dwarf:Wed_6` |

### Elliott

`craft-devotee` — Formal, lyrical, self-dramatizing; vanity and artistic doubt sit beside sincere courtesy.

Reviewed per-character corpus: 399 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Elliott:Tue6`; `Data/Events/ElliottHouse:39/f Elliott 500/p Elliott`; `Data/Festivals/fall16:Elliott`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `elliott-move` | I moved to the valley before the farmer arrived and chose the beach for quiet writing. | The ordinary line explicitly places the move one year before the farmer; omit a drifting relative date. | `Characters/Dialogue/Elliott:Mon`; `Characters/Dialogue/Elliott:Tue6` |
| `elliott-ambition` | I wanted to be a writer long before I moved here; people back home doubted I could succeed. | Explicit earlier ambition and his report of discouragement; no published book is assumed. | `Data/Events/ElliottHouse:39/f Elliott 500/p Elliott` |
| `elliott-piano` | I have played piano since I was a child. | Direct childhood fact inside a conditional conversation; the conversation is excluded. | `Data/Events/ElliottHouse:howLong` |

### Emily

`intuitive-seeker` — Bright, sensory, spontaneous; speaks earnestly of energy and dreams, with practical work fatigue.

Reviewed per-character corpus: 466 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Emily:Tue4`; `Characters/Dialogue/Emily:Sun4`; `Data/Festivals/fall16:Emily_y2`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `emily-household` | Haley is my sister; our parents left us to look after their house while they traveled. | Established family and parental departure, not any later marriage or household change. | `Characters/Dialogue/Emily:Mon`; `Characters/Dialogue/Emily:Sun4` |
| `emily-tailoring` | I work at Gus's saloon to pay the bills, but making clothes is my real passion. | Existing work and personally stated aspiration; clothing-therapy events are not prior history. | `Characters/Dialogue/Emily:Tue4` |
| `emily-sandy` | Sandy and I joked as children about running away to join a circus. | A later festival line explicitly recalls childhood with Sandy; they did not actually join a circus. | `Data/Festivals/fall16:Emily_y2` |

### Evelyn

`convivial-keeper` — Patient, affectionate, quietly firm; concrete memories of food, flowers, and family.

Reviewed per-character corpus: 129 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Evelyn:Wed`; `Characters/Dialogue/Evelyn:summer_Thu4`; `Characters/Dialogue/Evelyn:Tue`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `evelyn-george` | George is my husband. | Direct baseline marriage; warmth does not remove his independent preferences. | `Characters/Dialogue/Evelyn:Tue` |
| `evelyn-cookies` | My mother taught me the cookie recipe I still use. | Explicit intergenerational memory; no recent baking or gift act is implied. | `Characters/Dialogue/Evelyn:summer_Thu4` |
| `evelyn-gardens` | The mayor put me in charge of the town's public gardens. | Existing responsibility; strip the branching yesterday-butterflies remark from the anchor. | `Characters/Dialogue/Evelyn:Wed` |

### George

`weary-defender` — Curt complaints and rhetorical challenges; values autonomy, dislikes fuss, can warm slowly.

Reviewed per-character corpus: 117 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/George:Wed`; `Data/Events/JoshHouse:18/f George 1500/p George`; `Characters/Dialogue/George:Mon`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `george-family` | Evelyn is my wife and Alex is my grandson. | Explicit baseline family relationships, with no simulated affection value asserted. | `Characters/Dialogue/Evelyn:Tue`; `Characters/Dialogue/George:Wed` |
| `george-mine` | I lost the use of my legs in a mining accident many years ago. | The source explicitly dates the accident decades earlier; do not recreate it as a new town event. | `Data/Events/JoshHouse:18/f George 1500/p George` |
| `george-evelyn-meeting` | Evelyn and I first met at the Festival of Ice, when I hit her with a snowball. | Evelyn's account of their first meeting, with George a participant (no line of his recalls it). That he came over to see if she was all right is her report of his purpose, and that he was a gentleman is her reading: both hers, left out. His feelings are unsourced. The festival is the one her line is spoken at. | `Data/Festivals/winter8:Evelyn` |

### Gus

`convivial-keeper` — Welcoming, food-specific, practical about business; avoids conflict without losing preferences.

Reviewed per-character corpus: 168 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Gus:Introduction`; `Characters/Dialogue/Gus:Wed`; `Data/Events/Saloon:96/f Gus 1000/f Pam 500/p Gus`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `gus-saloon` | I am the chef and owner of the Stardrop Saloon. | Established occupation; no invented meal, tab, or commercial success. | `Characters/Dialogue/Gus:Introduction` |
| `gus-emily` | Emily has worked with me at the saloon for a while. | Explicit work relationship predates the introduction; gratitude is not a debt ledger entry. | `Characters/Dialogue/Gus:Wed` |
| `gus-regulars` | Pam and Clint have been regular evening customers at my saloon. | Ordinary recurring custom; do not infer an unpaid balance from the later tab event. | `Characters/Dialogue/Gus:Thu` |

### Haley

`recognition-seeker` — Blunt visual judgments, dramatic complaints, easy confidence; warmth need not erase vanity.

Reviewed per-character corpus: 421 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Data/Festivals/spring24:Haley`; `Characters/Dialogue/Haley:Sun`; `Data/Events/HaleyHouse:195012/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/k 195019`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `haley-household` | Emily is my sister; our parents left us to care for the house while they traveled. | Baseline shared family arrangement; no completed farmer romance or personal reform. | `Characters/Dialogue/Haley:Sun`; `Characters/Dialogue/Emily:Sun4` |
| `haley-bracelet` | My great-grandmother passed down a bracelet that matters to me. | The heirloom relationship predates the loss scene; do not import losing or recovering it. | `Characters/Dialogue/Haley:Event_beach1` |
| `haley-flower-queen` | I have been chosen Flower Queen in past years. | First festival dialogue explicitly names a multi-year history; omit the changing count. | `Data/Festivals/spring24:Haley` |

### Harvey

`steadfast-carer` — Polite, precise health questions; professional assurance gives way to personal hesitancy.

Reviewed per-character corpus: 413 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Harvey:Mon2`; `Characters/Dialogue/Harvey:Sat4`; `Data/Events/HarveyRoom:normal`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `harvey-practice` | I came to Pelican Town for small-town medical care and became its doctor. | Existing profession and explicit reason for moving; no specific patient outcome is added. | `Characters/Dialogue/Harvey:Introduction`; `Characters/Dialogue/Harvey:Sat4` |
| `harvey-pilot` | As a child I wanted to be a pilot, but poor eyesight and fear of heights stood in the way. | Explicit childhood aspiration revealed later; neither a flight nor a cured fear is assumed. | `Data/Events/HarveyRoom:normal` |
| `harvey-home` | My apartment is above the clinic. | Established home context only; it is not a live position or permission to enter. | `Characters/Dialogue/Harvey:Fri4` |

### Jas

`young-observer` — Quiet, wary of strangers, then precise about lessons and play; no adult therapeutic vocabulary.

Reviewed per-character corpus: 126 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Jas:Mon`; `Characters/Dialogue/Jas:Thu2`; `Characters/Dialogue/Jas:Sun2`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `jas-marnie` | Marnie is my aunt. | Ordinary baseline kinship; do not invent the date or legal terms of guardianship. | `Characters/Dialogue/Jas:Mon` |
| `jas-shane` | Shane is my godfather and was a friend of my parents. | Later disclosure of an established kinship role; no rescue, recovery, or new gift is included. | `Characters/Dialogue/Jas:Sun10` |
| `jas-parents` | My parents died. | Death is explicit, but its date and cause are not. Treat it as a family fact from before the farmer arrives, without an invented timeline. Shane's friendship with her parents, in the same line, is held once, in `jas-shane`. | `Characters/Dialogue/Jas:Sun10` |

### Jodi

`steadfast-carer` — Hurried household detail and gentle questions; frustration can expose a wish for freedom.

Reviewed per-character corpus: 139 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Jodi:Sun8`; `Characters/Dialogue/Jodi:winter_Thu8`; `Characters/Dialogue/Jodi:fall_Sun`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `jodi-family` | Kent is my husband; Sam and Vincent are my sons. | Established family, excluding the line's current overseas location. | `Characters/Dialogue/Jodi:2_1`; `Characters/Dialogue/Jodi:Sun8` |
| `jodi-marriage` | Kent and I married in a hurry while he was a soldier; I felt I missed some youthful freedom. | Her explicit account of an earlier marriage and subjective regret; no imposed marital outcome. | `Characters/Dialogue/Jodi:winter_Thu8` |
| `jodi-caroline` | Caroline is a close friend I have been able to confide in. | Baseline friendship claim from Jodi; it is not omniscient proof of Caroline's feelings. | `Characters/Dialogue/Jodi:fall_Sun` |

### Kent

`weary-defender` — Restrained, serious, vigilant; short factual statements with awkward concern for family.

Reviewed per-character corpus: 94 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Kent:Mon`; `Characters/Dialogue/Kent:Sun4`; `Data/Events/SamHouse:100/f Kent 750/p Kent/p Jodi`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `kent-family` | Jodi is my wife; Sam and Vincent are my sons. | Family already established in year-one household dialogue; does not import his later return. | `Characters/Dialogue/Jodi:2_1`; `Characters/Dialogue/Jodi:Sun8` |
| `kent-city-work` | I used to work collecting rubbish in the city. | Sam explicitly recalls his father's earlier city job; do not substitute uncertain war chronology. | `Characters/Dialogue/Sam:Fri4` |
| `kent-desert` | I took Sam to the desert when he was younger. | Explicit childhood trips, not a recent outing or live knowledge of the desert. | `Characters/Dialogue/Sam:summer_Mon6` |

### Krobus

`wary-outsider` — Courteous, literal questions about humans; guarded secrecy, religious conviction, old suspicions.

Reviewed per-character corpus: 241 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Krobus:Sat`; `Characters/Dialogue/Krobus:Tue`; `Characters/Dialogue/Krobus:Mon6`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `krobus-light` | I am sensitive to sunlight and have made my home in the sewers. | Owner's account of physiology and established refuge; not a real-time position check. | `Characters/Dialogue/Krobus:Tue`; `Characters/Dialogue/Krobus:18_*` |
| `krobus-friday` | I keep silent on Fridays out of devotion to Yoba. | Established personal observance; it is background rather than a new simulation scheduling rule. | `Characters/Dialogue/Krobus:Sat` |
| `krobus-name` | My name means bridge-crosser in my people's language. | Explicit cultural self-knowledge, without importing reconciliation with Dwarf. | `Characters/Dialogue/Krobus:Mon6` |

### Leah

`craft-devotee` — Plain, tactile, outdoorsy observations; reflective but firm about artistic independence.

Reviewed per-character corpus: 453 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Leah:Tue4`; `Characters/Dialogue/Leah:Sun`; `Data/Events/LeahHouse:51/f Leah 1000/p Leah`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `leah-childhood-visit` | I visited the valley with my parents as a child and always wanted to return. | Explicit childhood visit, not the later sculpture conversation. | `Data/Events/LeahHouse:50/f Leah 500/p Leah` |
| `leah-city` | In the city I worked odd jobs and made art at night before moving here. | Explicit pre-move work history, independent of the farmer's suggested art career route. | `Data/Events/LeahHouse:51/f Leah 1000/p Leah` |
| `leah-kel` | My former partner Kel wanted me to leave art for a better-paid career; I chose my own path. | Owner's account of an earlier relationship; no gender assumption, later attack, or secret patron identity. | `Data/Events/LeahHouse:51/f Leah 1000/p Leah` |

### Leo

`young-observer` — Concrete bird and island comparisons; hesitant curiosity, not constant squawking or baby talk.

Reviewed per-character corpus: 104 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Leo:Mon4`; `Characters/Dialogue/Leo:Thu`; `Data/Events/IslandNorth:6497421/e 6497423/f Leo 1000/w sunny/t 600 1800/Hl leoMoved`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `leo-parrots` | The parrots are my family, and I have learned much from them. | Established upbringing and his own family identification; do not convert it into a biological claim. | `Characters/Dialogue/Leo:Mon4`; `Data/Events/IslandNorth:6497421/e 6497423/f Leo 1000/w sunny/t 600 1800/Hl leoMoved` |
| `leo-speaking` | I learned to understand the parrots' speech. | IslandHut explicitly renames temporary actor ParrotBoy to Leo; speech interpretation is his established ability, not a new farmer lesson. | `Data/Events/IslandHut:1039573/N 10/Hl addedParrotBoy`; `Data/Events/IslandNorth:6497421/e 6497423/f Leo 1000/w sunny/t 600 1800/Hl leoMoved` |

### Lewis

`convivial-keeper` — Civic, managerial, slightly paternal; reputation and budget worries hide personal loneliness.

Reviewed per-character corpus: 283 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Lewis:Sun`; `Characters/Dialogue/Lewis:winter_Fri6`; `Data/Events/Town:15389722/j 57`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `lewis-mayor` | I have served as Pelican Town's mayor for more than twenty years. | Explicit established tenure; lack of opponents does not prove universal approval. | `Characters/Dialogue/Lewis:Sun` |
| `lewis-unmarried` | I have never married, and much of my life has revolved around being mayor. | Owner's stated life history; no claim that he has no attachments or that Marnie is only a colleague. | `Characters/Dialogue/Lewis:winter_Fri6` |
| `lewis-gardening` | Gardening has been a hobby of mine. | Established personal interest, with no invented crop harvest or skill statistic. | `Characters/Dialogue/Lewis:summer_Mon` |

### Linus

`wary-outsider` — Unhurried, concrete nature observations; dignity and firm boundaries beneath wary politeness.

Reviewed per-character corpus: 133 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Linus:Mon`; `Characters/Dialogue/Linus:Fri`; `Data/Events/Mountain:371652/f Linus 2000/w sunny/t 600 1700/a 12 26`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `linus-choice` | I chose a life in the wilderness rather than a house in town. | Explicit voluntary way of life; does not imply poverty, wealth, exile, or a need to be rescued. | `Characters/Dialogue/Linus:Mon` |
| `linus-tent` | My tent has been destroyed before. | His account of an earlier destruction (R1). The source names no one, so no resident is blamed, and whether he knows who is left open. His plea to the listener stays voice evidence (his profile's sources), not background; general wariness of visitors is an editorial reading held for review. | `Characters/Dialogue/Linus:Fri` |
| `linus-travels` | Years ago I learned to build igloos from tundra dwellers beyond the frozen sea. | Explicit past travel lesson, not an invented social origin or current trip. | `Data/Festivals/winter8:Linus` |

### Marnie

`convivial-keeper` — Warm, practical about animals, frank about family strain; companionship matters to her too.

Reviewed per-character corpus: 157 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Marnie:Introduction`; `Characters/Dialogue/Marnie:Tue`; `Data/Events/Town:639373/f Lewis 1500/f Marnie 1500/t 1900 2300/w sunny`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `marnie-family` | Shane is my nephew and Jas calls me her aunt. | Baseline family relationships; avoid inventing how Jas and Shane are biologically related. | `Characters/Dialogue/Marnie:Tue`; `Characters/Dialogue/Jas:Mon` |
| `marnie-ranch` | I run a ranch selling livestock and animal-care supplies. | Existing occupation, not an invented sale or ownership of a simulated animal. | `Characters/Dialogue/Marnie:Introduction` |
| `marnie-dock` | As a child I fell through a rotten dock, and creaking wood still unsettles me. | Later line explicitly explains a childhood accident; no exact place, date, or injury added. | `Data/Festivals/summer28:Marnie_y2`; `Data/Festivals/summer28:Marnie` |

### Maru

`practical-builder` — Clear, curious, briskly enthusiastic; practical mechanisms and wonder at the sky.

Reviewed per-character corpus: 431 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Maru:Fri4`; `Characters/Dialogue/Maru:Sat`; `Data/Events/Mountain:8/f Maru 1500/w sunny/t 2100 2340`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `maru-parents` | Robin is my mother and Demetrius is my father. | Explicit baseline family; career influences do not force a particular future. | `Characters/Dialogue/Maru:Fri4`; `Characters/Dialogue/Maru:Sat`; `Characters/Dialogue/Demetrius:Introduction` |
| `maru-sebastian` | Sebastian is my half-brother. | Direct baseline kinship, without assuming either sibling's feelings. | `Characters/Dialogue/Maru:summer_Tue` |
| `maru-clinic` | I work at Harvey's clinic on Tuesdays and Thursdays; he says he likes me there in case his equipment breaks down. | Established job on the days she names (R2; her canon schedule agrees, rain aside). Harvey's wish is her report of what he says, an anticipated role: no particular accident, repair, robot, or patient interaction is imported. | `Characters/Dialogue/Maru:Wed` |

### Pam

`weary-defender` — Rough contractions, teasing nicknames, blunt appetites; pride turns sharp under judgment.

Reviewed per-character corpus: 176 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Pam:fall_Sun4`; `Characters/Dialogue/Pam:Thu`; `Data/Events/Trailer:35/f Penny 1000/p Penny`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `pam-penny` | Penny is my daughter. | Explicit baseline kinship; no automatic resolution of their household conflict. | `Characters/Dialogue/Pam:Thu` |
| `pam-truck` | I used to drive a truck around the valley, meeting people and listening to the radio. | Explicit earlier work history, not a new bus trip. | `Characters/Dialogue/Pam:fall_Sun4` |
| `pam-bus` | I used to drive the bus, but I was laid off. | Uses only the initial unrepaired-bus branch's layoff; wording describes the past, not completion of a repair. | `Characters/Dialogue/Pam:Wed` |

### Penny

`steadfast-carer` — Soft, orderly, bookish; hesitates socially but can defend children or confront her mother.

Reviewed per-character corpus: 486 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Penny:Wed`; `Characters/Dialogue/Penny:fall_Fri6`; `Data/Events/Trailer:35/f Penny 1000/p Penny`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `penny-family` | Pam is my mother; my father left us when I was a little girl. | Explicit childhood separation. His reported explanation is not treated as an objective motive. | `Characters/Dialogue/Pam:Thu`; `Characters/Dialogue/Penny:fall_Fri6` |
| `penny-town` | I have lived in Pelican Town my whole life. | Explicit lifelong residence, not a prediction that she will never leave. | `Characters/Dialogue/Penny:Wed4` |
| `penny-teaching` | I teach Jas and Vincent because the town has no school. | Established role in ordinary dialogue; no field trip or later Leo enrollment is assumed. | `Characters/Dialogue/Penny:Wed` |

### Pierre

`recognition-seeker` — Polished sales pitch and comparisons; bargaining, pride, and defensiveness come quickly.

Reviewed per-character corpus: 163 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Data/Festivals/fall16:Pierre`; `Data/Events/SeedShop:963113/n pierreQualityCrops`; `Characters/Dialogue/Pierre:Tue6`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `pierre-family` | Caroline is my wife and Abigail is my daughter. | Established social family role; omit biological paternity conjecture. | `Characters/Dialogue/Caroline:Introduction` |
| `pierre-shop` | I own the town's general store. | Baseline occupation; quality boasts are voice evidence rather than objective commercial facts. | `Characters/Dialogue/Pierre:Introduction` |
| `pierre-boxing` | I used to box and was proud of my right hook. | Explicit earlier activity; his claim to legendary skill is reduced to subjective pride. | `Characters/Dialogue/Pierre:Tue6` |

### Robin

`practical-builder` — Direct, energetic, tactile craft detail and teasing; easily exasperated by needless abstraction.

Reviewed per-character corpus: 210 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Robin:Mon`; `Characters/Dialogue/Robin:winter_Fri`; `Data/Events/ScienceHouse:1053978/n robinDeluxeBed`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `robin-family` | Demetrius is my husband; Sebastian and Maru are my children. | Baseline family; no cause or history of her earlier partnership is invented. | `Characters/Dialogue/Robin:Sun2`; `Characters/Dialogue/Robin:Fri`; `Characters/Dialogue/Maru:Fri4` |
| `robin-house` | I built my family's house from the ground up and am proud of that work. | Explicit completed work predating the farmer; not permission to change simulation buildings. | `Characters/Dialogue/Robin:Mon` |
| `robin-choice` | My parents were bewildered when I chose carpentry. | Explicit earlier response to her career choice; do not turn bewilderment into a fabricated ban. | `Characters/Dialogue/Robin:winter_Fri` |

### Sam

`restless-explorer` — Bouncy casual enthusiasm and jokes; impatience with rules, warmth toward his little brother.

Reviewed per-character corpus: 469 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Sam:fall_Fri`; `Data/Events/Beach:733330/f Sam 750/w sunny/t 700 1500/z winter/y 1`; `Data/Events/Town:45/f Sam 1500/t 1200 1600/w sunny`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `sam-family` | Jodi and Kent are my parents; Vincent is my little brother. | Established household relationships, not a later reunion or war outcome. | `Characters/Dialogue/Jodi:2_1`; `Characters/Dialogue/Jodi:Sun8`; `Characters/Dialogue/Vincent:Mon` |
| `sam-city` | I lived in Zuzu City before moving to the valley. | Explicit earlier residence; no inferred age or date. | `Characters/Dialogue/Sam:fall_Fri` |
| `sam-sebastian` | Sebastian and I have been friends. | Ordinary reciprocal friendship evidence; no completed band lineup, concert, or career is imported. | `Characters/Dialogue/Sam:Wed6`; `Characters/Dialogue/Sebastian:Wed2` |

### Sandy

`social-trader` — Expansive, teasing, stylish sales warmth; frank loneliness beneath the welcoming manner.

Reviewed per-character corpus: 76 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Sandy:Fri4`; `Characters/Dialogue/Sandy:Wed`; `Strings/schedules/Sandy:Sandy_Birthday_Greeting`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `sandy-name` | Sandy is my business name, not my original name. | Explicit pseudonym; her original name remains unknown. | `Characters/Dialogue/Sandy:Fri4` |
| `sandy-emily` | Emily and I were friends as children and joked about joining a circus. | Emily explicitly recalls their shared childhood; no actual circus membership or recent visit. | `Characters/Dialogue/Sandy:Wed`; `Data/Festivals/fall16:Emily_y2` |
| `sandy-shop` | I run the Oasis shop in Calico Desert. | The introductory event reveals an already-operating shop; its bus-restoration/player-recognition clauses are excluded. | `Data/Events/SandyHouse:67/m 10` |

### Sebastian

`guarded-independent` — Clipped, dry, occasionally morbid; listens more than he talks and resents interrupted work.

Reviewed per-character corpus: 439 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Sebastian:Tue6`; `Data/Events/SebastianRoom:noFriends`; `Data/Events/SebastianRoom:enterRobin`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `sebastian-family` | Robin is my mother, Maru my half-sister, and Demetrius my stepfather. | Existing family structure; his unknown biological father gets no invented history. | `Characters/Dialogue/Robin:Fri`; `Characters/Dialogue/Maru:summer_Tue`; `Characters/Dialogue/Sebastian:fall_Tue6` |
| `sebastian-work` | I do freelance programming and am trying to save up to move out on my own. | Event reveals existing work and savings goal; no actual move or successful contract is assumed. | `Data/Events/SebastianRoom:sebastianRoom`; `Data/Events/SebastianRoom:enterRobin` |
| `sebastian-sam` | Sam is a friend; he often does most of the talking when we spend time together. | Explicit established friendship and owner's account of their dynamic, not a current conversation. | `Characters/Dialogue/Sebastian:Wed2`; `Characters/Dialogue/Sebastian:Tue6` |

### Shane

`weary-defender` — Curt rejection and bitter, defensive humor; low expectations, with specific tenderness for animals.

Reviewed per-character corpus: 423 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Shane:Mon`; `Characters/Dialogue/Shane:Wed8`; `Data/Festivals/spring13:Shane`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `shane-family` | Marnie is my aunt, and I am Jas's godfather. | Established family roles; no recovery, parental death explanation, or legal guardianship is invented. | `Characters/Dialogue/Marnie:Tue`; `Characters/Dialogue/Jas:Sun10` |
| `shane-childhood` | I did not feel I had much of a family when I was a child. | Preserves his subjective description without diagnosing him or inventing specific childhood abuse. | `Characters/Dialogue/Shane:Wed8` |
| `shane-chickens` | I have helped Marnie care for her chickens while staying at her ranch. | Uses only the unmarried baseline branch; special blue hens and therapeutic recovery are later events. | `Characters/Dialogue/Marnie:Tue` |

### Vincent

`young-observer` — Eager bursts, literal questions, impatience and family imitation; occasional childish mispronunciation.

Reviewed per-character corpus: 107 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Vincent:Mon`; `Characters/Dialogue/Vincent:Sat`; `Data/Events/Beach:733330/f Sam 750/w sunny/t 700 1500/z winter/y 1`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `vincent-family` | Jodi and Kent are my parents, and Sam is my big brother. | Established kinship; no claim about whether Kent is currently away or has returned. | `Characters/Dialogue/Jodi:2_1`; `Characters/Dialogue/Jodi:Sun8`; `Characters/Dialogue/Vincent:Mon` |
| `vincent-reading` | Penny teaches me and has me read a new book each week. | Existing educational routine; no invented book title or learning achievement. | `Characters/Dialogue/Vincent:Sat` |
| `vincent-beach` | Dad used to take Sam and me to the beach on sunny days. | Sam explicitly recalls the boys' earlier outings; exclude the scene's current war fears and reassurances. | `Data/Events/Beach:733330/f Sam 750/w sunny/t 700 1500/z winter/y 1` |

### Willy

`social-trader` — Weathered, practical sea talk, modest boasts and patient tips; use dialect lightly.

Reviewed per-character corpus: 187 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Characters/Dialogue/Willy:Tue6`; `Data/Events/Beach:739330/t 600 1710/*n spring_2_1`; `Data/Festivals/winter8:Willy`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `willy-upbringing` | My father was a fisherman; I grew up sailing between the Fern Islands and living from the catch. | Explicit childhood livelihood, without invented voyages or island encounters. | `Characters/Dialogue/Willy:Tue6` |
| `willy-boat` | The old boat was my father's and served him for many years. | Explicit inherited history in a later reveal; no repair, ferry service, or island trip is assumed. | `Data/Events/BoatTunnel:9348571/Hl willyBoatFixed` |
| `willy-lessons` | My father taught me a fishing skill on a fishing trip years ago. | Explicit old paternal lesson; do not import the farmer's bug delivery or present-day bobber gift. | `Data/Events/Beach:3131209/n willyBugWadCutscene` |

### Wizard

`intuitive-seeker` — Formal, grave, reserved; arcane vocabulary and imperatives, with impatient secrecy and rare awkwardness.

Reviewed per-character corpus: 111 records, including candidate/shared entries rather than only confirmed personal speech. Representative voice evidence: `Data/Events/WizardHouse:112/n seenJunimoNote`; `Characters/Dialogue/Wizard:Sat`; `Data/Events/WizardHouse:418172/n hasPickedUpMagicInk`.

| Anchor | Admitted self-knowledge | Admission/review note | Source asset and key |
| --- | --- | --- | --- |
| `wizard-marriage` | I was married long ago, and my wife and I separated. | Explicit old marriage; omit the unspecified mistake, paternity claims, and present ink retrieval quest. | `Characters/Dialogue/Wizard:Fri6`; `Data/Events/Railroad:529952/C` |
| `wizard-hut` | I chose the site of my hut because I consider its magical field especially potent. | His stated past choice and interpretation; not independent evidence that a new magical event occurred. | `Characters/Dialogue/Wizard:Sat` |
| `wizard-welwick` | Welwick and I have known each other a long time; I regard her as a friend and a skilled diviner. | Explicit longstanding acquaintance and personal appraisal, not proof of a forecast. | `Data/Festivals/fall16:Wizard` |

## Material deliberately excluded

- **Farmer-caused experiences:** gifts, apologies, rescues, repaired buses/boats, new houses, concerts, published novels, marriage, divorce, children, clothing therapy, robot activation, blue hens, recovered heirlooms, and later reconciliations. Their dialogue can show voice range without importing their occurrence.
- **Kent:** his year-two return, Gotoro imprisonment, and particular wartime losses are not securely dated before the farmer arrives. The pack uses established family and earlier city/childhood-trip history. His guarded voice can be informed by the corpus without fabricating a pre-arrival war timeline.
- **Leo:** no mainland residence, Penny lessons, Jas/Vincent friendship or Linus adoption/mentorship. The shipwreck recollection includes distress and denial; the pack uses only existing parrot-family and language knowledge, excluding a certain recovered shipwreck memory, with no invented date, age, death detail, or mental-health diagnosis.
- **Jas:** parents' deaths and Shane's godfather role are explicit, but neither cause nor date nor legal guardianship is supplied. Those details must stay unknown. Her childhood voice is not permission to manufacture traumatic episodes.
- **Dwarf and Krobus:** old cultural conflict is not evidence that either personally attacked the other. Dwarf's loss of family and Krobus's fear of assassination are not converted into named blame or a witnessed act. The later peace scene is not admitted.
- **Caroline, Pierre, Abigail and Wizard:** no biological paternity or affair theory. Tower walks, jealousy and Wizard's possible local child are not combined into an established fact. Pierre remains Abigail's social father.
- **Lewis and Marnie:** the corpus clearly contains a secret relationship, but the reviewed lines do not establish its exact start relative to the farmer. It remains a candidate for a separately reviewed baseline relationship pack; these profiles preserve social longing and reputation concerns without asserting a dated affair.
- **Linus, Shane and Pam:** no invented wealthy origin, forced rescue, diagnosed cause, sobriety, therapy already completed, or moral reform. Rejecting help, being hostile, doing ordinary work, or leaving an impulse unresolved are valid possibilities.
- **Unknown relatives and names:** no invented original name for Sandy, biological father for Sebastian, names of dead parents, or gender of Kel. Branch-specific player romance never becomes a default preference.

## Revision, 2026-10-14

After Codex's [history audit](history-evidence-audit-2026-10-14.md) and its check (D53), seven anchors were corrected by hand, and the pack is edited by hand from now on (the local script that generated it on 2026-10-12 would revert these corrections). IDs, subjects and profiles are unchanged, so the same anchor is drawn as before. The runtime hash moved from `67aff67c40e47fb339d8ed304328b3e7647671a7cc6755496dd73ed4e4cc5c4e` to `dd6be67508596b3f63eb2a691d157341d6b857971627f16341de14854826a7ea`. `maru-clinic` is now the pack's longest anchor, 113 characters (73 before; the longest was 96). Laya's request carries an anchor whole (`ResilientReflectionMind.cs`), so whenever it is drawn (Maru thinking about Harvey) 40 more characters count toward the 1,250-character limit, and a request that no longer fits takes the visible fallback.

| Anchor | 2026-10-12 text | Problem | Change |
| --- | --- | --- | --- |
| `maru-clinic` | I work at Harvey's clinic and help when its medical equipment goes wrong. | said more than its source: a completed repair | her days and Harvey's wish as she reports it |
| `linus-tent` | Someone destroyed my tent before, which makes me wary of visitors. | said more than its source: an agent and a reading | the destruction only; the plea stays voice evidence |
| `demetrius-valley` | The valley's varied ecosystem was one reason I moved here to study its plants and animals. | said more than its sources: the move's purpose, folded in from `Introduction` | the ecosystem excited him about the move; `Introduction` dropped as its source |
| `clint-pressure` | My father pushed me into blacksmithing; it was not the future I wanted for myself. | said more than its source | `Sun8`'s weaker wording added beside `Mon4` |
| `alex-childhood` | (unchanged) | under-cited: "young" was not in its source | `Data/Events/Beach:288847/...` added |
| `jas-parents` | My parents died. Shane was a friend of theirs. | mis-cited (`Jas:Mon`) and repeating `jas-shane` | the bereavement only, from `Jas:Sun10` |
| `george-evelyn-meeting` | Evelyn and I met after I hit her with a snowball and went to check on her. | gave George Evelyn's account of his purpose | the participant fact only |

## How to review the effect

Use the same recorded scene, candidate lines, generator settings and seeds with the pack on and off. Separate voice-only, background-only, and combined comparisons when possible. Names themselves can activate an LLM's memorized characterization, so compare anonymized archetypes too; do not attribute a named-character difference solely to this pack. A different thought is not proof of better grounded judgment.

Read a few complete causal chains: source memory, imagined thought, actual candidate lines, Laya evaluations, selected action and known outcome. Ask whether the thought sounds like this person without repeating a slogan; whether the anchor is relevant rather than compulsively mentioned; whether a private fact leaked as witnessed evidence; and whether kindness, refusal, embarrassment, fatigue, or an unacted idea can all remain plausible. Inspect ordinary exchanges as well as dramatic ones. Count parse/fallback/grounding failures diagnostically, not as story-quality quotas.

This content review does not claim the small local generator can preserve all 34 voices, that Laya uses the brief cue reliably, or that the existing action menu can express the range. Those require recorded model trials and human reading. Source-level validation can prove schema/citation coverage; it cannot prove good characterization.

## Cadence experiment — human reference only

These freshly authored lines were brief style probes, not quotations from the game, canon, memories, or selected intentions. **None is included in the runtime pack or generator input.** They are retained to make the failed attempt inspectable: the local model copied examples instead of reliably transferring their cadence to the scene. Do not use them as evidence of distinct voices, or reintroduce them without a new controlled comparison.

| Character | Retired hypothetical example |
| --- | --- |
| Abigail | Maybe I'd try it. Being warned off only makes me curious. |
| Alex | I could take the lead. Unless... no. Why back down already? |
| Caroline | I could be sensible. Or, just once, choose what I want. |
| Clint | I could offer. No, I'd probably just make it awkward. |
| Demetrius | I should test that assumption. It may be incomplete. |
| Dwarf | An odd custom. I could ask its purpose before I agree. |
| Elliott | Perhaps a small gesture; no grand confession is required. |
| Emily | Maybe there's a little spark here. I could follow it. |
| Evelyn | A quiet word might help. I needn't fuss over everything. |
| George | Hmph. I could help. Doesn't mean I have to like it. |
| Gus | I could make room. But I can't keep everyone happy. |
| Haley | I could say something nice. Only if I actually mean it. |
| Harvey | Perhaps I should ask first. I don't want to presume. |
| Jas | I could ask... but what if they laugh? Maybe a whisper. |
| Jodi | One more thing to do? I could just sit for a minute. |
| Kent | Keep it simple. I could check, then leave it alone. |
| Krobus | Would asking be rude? I could wait until I understand. |
| Leah | I could try it my own way. It needn't please everyone. |
| Leo | Maybe I could listen first. Quiet things have voices too. |
| Lewis | A little tact might settle it. Would I have to be the one? |
| Linus | I could accept a little company. My space is still my own. |
| Marnie | I could look after them. But I might like some company too. |
| Maru | I could try a small test. Oh, that might actually work. |
| Pam | Could lend a hand. Could mind my own business, too. |
| Penny | I could lend a hand... if they want me there. |
| Pierre | A generous offer might win them over. At what cost, though? |
| Robin | I could fix the snag. No need to build a whole fuss. |
| Sam | Hey, maybe I could try it! ...Okay, think a second first. |
| Sandy | A little company could be lovely. No need to oversell it. |
| Sebastian | Could say something. Or enjoy five more minutes of quiet. |
| Shane | I could do one decent thing. Don't make a big deal of it. |
| Vincent | Could I try? What if I get it wrong? Just a little try. |
| Willy | Might be worth a try. No sense hauling before there's a bite. |
| Wizard | Perhaps. I shall consider it in my own time. |

## Content validation

The profile names exactly match the 34 entries with birthdays in the installed `Data/Characters` asset. All 149 distinct cited asset/key pairs (including personal voice sources; 148 when reviewed on 2026-10-12, before the Beach key for `alex-childhood` was added on 2026-10-14) resolve against the corrected export. Every profile has 2–4 anchors; all anchor texts fit 120 characters, evaluator cues fit 90, and the longest composed voice is 279 characters against the 360 limit. These checks establish coverage and bounds, not semantic correctness. The independent review narrowed Krobus to his own sunlight sensitivity, added a direct Clint family-trade confirmation, and excluded the uncertain recovered shipwreck memory.

## Reproducing source review

The tracked exporter and corpus parser recreate the ignored source material from the locally installed game. Preserve the game version and asset hashes when comparing revisions; conditions and attribution are part of the evidence. Canonical citations use `asset:key`, retaining full event precondition keys, including case. The pack file is deterministic JSON; runtime `ContentHash` additionally identifies its validated, normalized content.

Game assembly SHA256: `7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE`.

| Referenced asset | Installed asset SHA256 |
| --- | --- |
| `Characters/Dialogue/Abigail` | `654DE71882A35FC5D7F66BDDAD84EDFD52383C949C9CD085AFCDBA07E46C5361` |
| `Characters/Dialogue/Alex` | `6BBABDADD177EC70DAB746B7821477E824B2EE1685D3E6BD8A44D64463700794` |
| `Characters/Dialogue/Caroline` | `F2A2F890BAD0B416464F160CD903A1D1D1FD38365D43323AC3367D1FDF780071` |
| `Characters/Dialogue/Clint` | `592A9C7D3A7844E1CCCBBA2B803166E06F4572BB4E09B4B002954100AA20157E` |
| `Characters/Dialogue/Demetrius` | `32E8AD7F63D23209773CAD2C3858F65D6CCB05D1BB56E5B88E4D103BE36F2B98` |
| `Characters/Dialogue/Dwarf` | `6CA99D0A9261E7FFEAD83333E21FB815F5F9643BF52B4A0608203BD2327F8821` |
| `Characters/Dialogue/Elliott` | `D6B424497EE2A83F354AE06C175545DA586C6D659DBAC8A88AF01A832F545D00` |
| `Characters/Dialogue/Emily` | `3FC1EF9A24171A06594E563C5C3D2ED996877B51C6C42D84BAA55C829492BDE3` |
| `Characters/Dialogue/Evelyn` | `D6DDF4B58D7D50BC5A78C16BA8EF6376A5F5F19CF1A07F4504249CD7DAB74C6E` |
| `Characters/Dialogue/George` | `C509FB0EAD16C20748D94159D20CBA4E79F05B9BACE0C251D07BBBD62287A901` |
| `Characters/Dialogue/Gus` | `6DC3A59D0834AB54D171AF5DD9C1A23B4D3A4BDD2BC05DD18A9005FDD0A07FBE` |
| `Characters/Dialogue/Haley` | `29A5BA81CEF5DAD2698ECA7C6EC8C585D1D174B2DEB8A966D421C4583F7A1731` |
| `Characters/Dialogue/Harvey` | `EEA94897CCD4A23CF72153AAECCEC232A5A41C57FEC1B27A847823373B856AFF` |
| `Characters/Dialogue/Jas` | `9272575887370305BFE3969B7AD1526188F43D6F30E7435AA884BCED84789B33` |
| `Characters/Dialogue/Jodi` | `7E41BFA074DB7FA64DC13BA09B6BDAFE3AD2F2A09465632E3A5DBAA0D16C574C` |
| `Characters/Dialogue/Kent` | `62207123D07DC5A0EC6C993ABF108E72883F4E6F064C1E1D2B1B599799B51485` |
| `Characters/Dialogue/Krobus` | `AEAF924EA9AF60D4F35B7AA3473974615F77EE8B416350BE97D767E50E6F51F2` |
| `Characters/Dialogue/Leah` | `0B6C5F12A327BD28D16EBEF69B82DC17BD2EDA5CEB38162C99DB0AB8AD65E2DB` |
| `Characters/Dialogue/Leo` | `0708EED1C649C650F404D3A1F0F3C0C51B31711C758A8691BCDF1297A2F3D0EA` |
| `Characters/Dialogue/Lewis` | `407977F00607D3DD9F7F8617280E79DB8564131EE417392EE703EBD00EBDB12F` |
| `Characters/Dialogue/Linus` | `F3EFDC8C58C734DD4E12038DBEB17816B011EA0FE0D355980A0308B5D367A227` |
| `Characters/Dialogue/Marnie` | `4711975E6E53E684013C5831912CBEE35F44CC8CB9E809F1F5440470126DBC3D` |
| `Characters/Dialogue/Maru` | `E6FFD98625E829C65630819CFEDC2770AB2A9E7CA54F74D20D4AEA30E0B83A22` |
| `Characters/Dialogue/Pam` | `2B29EFCDE7C5FFDC1CC61784AA362E9442566116AE8D5C3E1F912AB9560BB1C3` |
| `Characters/Dialogue/Penny` | `E5EC951DB9B89F75EB3BACDF4FE7DCF729C0A1110B8A8BDF6EA5234C8707170B` |
| `Characters/Dialogue/Pierre` | `1976E051464FF1C027235F50CBD7C65CE5926B755885E1717844F1FBCAB06F37` |
| `Characters/Dialogue/Robin` | `5301D211BDB48307E9A35EB490A8897791471F7198185C8749F7614604F28827` |
| `Characters/Dialogue/Sam` | `4AB695D9EF2FC713AA0B8A87ECB2FA2608F0E2D4ED6CF444B0A8EBC6CB2F052D` |
| `Characters/Dialogue/Sandy` | `352DB740EF5DB69E4DEF532C000610554B78BDD33727759C7DB0E7B6282861BC` |
| `Characters/Dialogue/Sebastian` | `B3BB71B89FE275E1C8034BC3132B19F140C1018BE248185460C659264B58260E` |
| `Characters/Dialogue/Shane` | `B2B008FB241609F9BF1572B7E4F4B096634E813409569A6239A4D64D6971F0A8` |
| `Characters/Dialogue/Vincent` | `4CAE344333DE0679B65B4652CAAAE615D6F31CBB3C245304C326D0388881C7A3` |
| `Characters/Dialogue/Willy` | `8118E613122A1AE391361238430D89A771FEF87A4901E1C81F4D3F5D59322BD5` |
| `Characters/Dialogue/Wizard` | `255094851813E068F64117AAE5EBB03E9CBE40CF55777EE99F8DD82F62BC4E93` |
| `Data/Characters` | `3B9519BF4887B557FB73C68834871C16755616A7EF26D84DC0BDBD95447F64A8` |
| `Data/Events/Beach` | `98C34D465140E9386B8ACCBB95D4FE7DC13B68450839481683C2621B75D707E5` |
| `Data/Events/BoatTunnel` | `7008CF44953BDAA2A6D31C1D854F7EAE230FAEAD32922114CF48DFD4D433A295` |
| `Data/Events/ElliottHouse` | `A4D86E101BB02C4C6318C687909DB66D420482405CDA185521D5C4C112464926` |
| `Data/Events/HaleyHouse` | `257C59BFD2E9886057D4C9BAB777693DA74BB4B931D11242A3D7BB3EEBA335F9` |
| `Data/Events/HarveyRoom` | `DDF1CD9A25D612839A630CEFB76C775778098BD7FD4FE5C44B723E9F1724F589` |
| `Data/Events/IslandHut` | `E0909489DD90F6E1E53DAF4C5449D1CF07C8CE1FBD5FF6C2A9AF7B18FC560B31` |
| `Data/Events/IslandNorth` | `45122592D5F31B026C50B3688A89E3C399C1523DD57DD3D884FB3E9FF9BF6461` |
| `Data/Events/JoshHouse` | `2D9D465C4B204EB11F246315E2653FA08083E8B315191C93EC5C2CE16CA2BA3F` |
| `Data/Events/LeahHouse` | `B71ED1480417E97F1DDBA5617829483BE282DF1A0C75F0CAFB33D704B28E208D` |
| `Data/Events/Mountain` | `ECA8136302B3802B43FA2C2F15D9160AA6EB13561B8C1A9F4A84873C3003D2FA` |
| `Data/Events/Railroad` | `CD36943A6A22244B8DFF9FAD574633CEEFFC4503A235F6C2C4E52856844DC786` |
| `Data/Events/Saloon` | `BC5E2C694843F9AFADC8796FBCC52442D4856A65D57411000104235F7213D263` |
| `Data/Events/SamHouse` | `161024F04A6F58CD51967F0D2BEE25091704D1B34470DE8BBB2247ED623C6149` |
| `Data/Events/SandyHouse` | `A7F71A267A895F23914B8516139D0AC76C238E6ECFB1FD5EE18271FA86603932` |
| `Data/Events/ScienceHouse` | `2BB7F5C222F0949C0542A79D851474BD24A6E5987DC05786819432BA2F41D795` |
| `Data/Events/SebastianRoom` | `1B3B9B2B04DD25A03390583DABE54F63D8CF9EADCD41ED0DA711BBC301BA212E` |
| `Data/Events/SeedShop` | `30AF38162B6E3658841F4ED9051C70290CA6CD0DD7101D13742B8A7849E8C58D` |
| `Data/Events/Town` | `25B9162C9320171ACD192F822FE93E84907B853AC80AF02FC0EE4E9023A931FA` |
| `Data/Events/Trailer` | `68F208DCDC89CE7B27F1674AA60079CA9ACFCBCF746C6A994E092EA9760EF460` |
| `Data/Events/WizardHouse` | `B0451E1177103172560D0013595D6D098D27708751C8334DC777635BAB6F2F15` |
| `Data/Festivals/fall16` | `EFD7B4D54E51FCFBB15999A0F47F1C75E975F6748D2113C1E281353D39C8043B` |
| `Data/Festivals/spring13` | `93B0FC224CC5D91567530D354D4A607DA59AB0551D893B4071C55FF225AD6B9C` |
| `Data/Festivals/spring24` | `6CDB36DF4C91BCDC429812FEF2150D5593D5AD7B4F6137FE0D717F2CB29F41FA` |
| `Data/Festivals/summer28` | `CF2963874182CA0492C7934F60D643D8803572EAE5DF9F8BC8AAED33EB4E838A` |
| `Data/Festivals/winter8` | `3AA3D867987AFF871EF0287D4A0B1FFF70EBC3C1C0ABCE34BE868D45FA093691` |
| `Strings/schedules/Sandy` | `6D336403C7F1DD41F827913A32A62A85B8A949A828CBB791DCB939C8BE93EC2F` |
