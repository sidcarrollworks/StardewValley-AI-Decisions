> Draft for Sid's review, 2026-10-08. Nothing here is built. It merges two designs ("Pelican Town, grown" and "Under Glass's own town"), the scaling experiment (the cast copied 2, 4 and 8 times), the town and act readers, and the real-town data brief (task 24). Line numbers are for `origin/main` (03c5745). Numbers marked *measured* come from those runs. Every other number is a first guess for the sweeps to settle.

# Under Glass: a bigger, better-laid-out town (spec draft)

## 0. Summary

Sid (2026-10-08): "add a population tab to the webpage... improve the layout of the town. Maybe experiment with larger population sizes... expand the amount of actions a person can do."
- The Population tab already exists on `claude/affectionate-pasteur-qzdpvi` (48388b6). Section 7 says what it needs at 120 people.
- More acts has its own spec. Section 9 says where the two specs meet.
- This spec covers the town: its layout, and how it grows.

**What the scaling experiment found** (*measured*, with copies of the cast sharing the same public places):
- Bigger towns run. At 208 people the sim uses 0.3% of a 20-minute game day.
- But a copied town is a bigger crowd, not a bigger town:
  - act rates are set for the whole town, so each person does less: 0.30 acts per person per day at 26 people, 0.14 at 104;
  - everyone shares the same four hubs and six public places, so the crowd round each person grows: 2.8 others within 8 tiles at 26, 11.0 at 104;
  - no neighbourhoods form: tellings between copies run at about the rate random mixing would give;
  - about 80% of all pairs know each other well within 28 days, at every size;
  - the 0a band fails: 61% of witnessed scandals land in band at 26, 39% at 52, 38% at 104;
  - the recorder throws at 132 places (k = 10).
- Cost grows about as n²: 0.077 s per game day at 26, 0.21 at 52 and 0.93 at 104 (112-day runs); 3.28 at 208 (28 days).

**Recommendation.**
1. **The shipped town stays exactly as it is**, with every pin. Everything new is opt-in.
2. **Grow Pelican Town first.** The 26 people, their places and their doors stay byte for byte at the centre, and generated neighbourhoods are added round them. The core then works as a calibration inside the bigger town: we can check whether it still behaves like itself.
3. **A neighbourhood is one outdoor place**: a lane or court of 3-12 homes, with house footprints, hedges, front steps and a green drawn inside it. Homes stay interiors behind their front steps, as now. This matches the real-town data: clusters of 8-12 homes, with next-door doors 4-9 tiles apart.
4. **Neighbourhoods differ in density** (terrace, lane, green, court, scattered cottages). Density gives each neighbourhood its character, and it is a story lever we can measure.
5. **The town is a pure function** `TownGen.Build(TownSpec) → TownData`, with labelled random draws. The same spec gives the same town. Adding a neighbourhood leaves the others unchanged. A town can be described, dumped to JSON, checked and edited by hand.
6. **Generated people start from archetype cards.** Each is a core villager's card with its values varied, and 10% are wildcards so fringe people can exist.
7. **Hubs gain admission and a crowd limit, familiarity is seeded by circle, and act rates scale per person.** Together these turn a crowd into neighbourhoods.
8. **There are few engine changes.** They are opt-in and mostly in new files. Performance work must leave every hash byte-identical.
9. **The gates are counted per person and per neighbourhood**, with new locality gates and a check that the core stays itself.
10. **The recorder and viewer get a town plan**: every place's position in the world, so the whole-town map no longer has to guess.

**The alternative, kept for Sid** (question 1): Under Glass's own town, profile `own`. The same generator also generates the centre, the anchors and the shops. It needs two more engine changes (roles for the newcomer and for the shops) and its own calibration (section 4.7).

---

## 1. What changes and what stays

**Stays:**
- `DefaultTown`: its cast, places, doors, hubs, acts, economy and authority. `DefaultTown.cs` is not edited.
- Every pinned hash (9 on main: `PinnedTests.cs:36-37, 101-105, 138, 151`; 0d.6 adds more when it merges).
- Every rule: perception and the 8-tile rule, 5-minute ticks, walking at 2 tiles a minute, gossip, feelings, the desire gate, money and authority. A generated town runs the same rules. Only its data and its opt-in options differ.
- Model records change only by fields appended at the end, with defaults that keep today's behaviour.
- Replay version 1 files stay readable.

**Changes:**
- New files: the town bundle (`Town.cs`), its constructor (`Simulation.Town.cs`), the generator (`Generation/`), the opt-in engine options (`Simulation.Growth.cs`) and the town metrics (`TownMetrics.cs`).
- One-line hooks in existing files for each opt-in option (section 3).
- Replay version 2, and the viewer's use of it (section 7).
- The runner: `--town`, `--describe` and `--dump-town`, and memory that no longer grows with the number of runs.

**Pins.** No step re-pins the shipped town. Two of Sid's choices would (questions 4 and 5), and each would be a change of its own. New pins are added for generated towns (section 8).

**Why opt-in costs nothing.** Every random draw is keyed by name (`Rng.Unit(seed, parts…)` in `Rng.cs`), not taken from a stream. A feature that is off draws nothing, so it shifts no other draw.

---

## 2. The layout model

### 2.1 What a town is made of

| Part | What it is | Pelican profile | Own profile |
|---|---|---|---|
| **Centre** | The square, the shops, the saloon, the clinic | The shipped 24 places, unchanged | A generated centre district of about 46×28: a square and a high street with shop fronts |
| **Neighbourhoods** | One outdoor place each, at most 48×44 tiles: a street or court with house footprints `#`, hedges `+`, front steps and a linger spot. Each front step is a door to a home interior | Added round the core in fixed slots | Added round the centre in fixed slots |
| **Connectors** | Short roads, 3 tiles tall like the core roads, so walkers are seen | One for each neighbourhood that joins the core | The same |
| **Edges** | Shore, forest, farms, hill: quiet, sparse places | The shipped Beach, ForestPath, Farm and MountainPath | Generated edges |
| **Interiors** | Homes, shops and workplaces | Homes sized by household (2.2) | The same |

Why each neighbourhood is one place:
- Neighbours' front steps can fall inside the 8-tile rule. Today homes are rooms off 3-tile roads, so neighbours meet only while walking past each other.
- Footprints block sight and hedges halve it, so a corner can hide an act.
- A place is a natural neighbourhood ID for the rules, the metrics and the viewer.
- Distance fields stay small (about 2,000 tiles each), and coordinates stay under the recorder's limit of 255.
- It mirrors Stardew's separate maps (VERIFY: the game's Town, Beach and Forest are separate maps).

The cost: people on either side of a neighbourhood's edge can't see each other. Stardew's map edges work the same way.

### 2.2 Neighbourhood templates

Templates are drawn by hand, as rows of characters in a data file. The generator only fills their plots with households.

| Template | Homes | Shape | Next door / across | Other doors within 8 tiles | Linger spot | Character |
|---|---|---|---|---|---|---|
| **Terrace** | 10-12 | two facing terraces, street 3 wide | 4 / 4 | 7-9 | a bench at each end | dense and busy: many weak ties |
| **Lane** | 8-12 | facing rows of detached houses with hedged gardens, street 4 wide | 7 / 5 | about 5 | a small green at one end | the ordinary street |
| **Green** | 8-10 | two rows facing a green of about 12×8 | 7 / 10 or more | about 2, plus the green | the green, with benches | ties form on the green |
| **Court** | 6-8 | homes round a court of about 8×8 with one way in | 4-6 | all or nearly all | the court itself | the strongest local ties (the cul-de-sac effect) |
| **Scattered** | 3-6 | cottages along a track on an edge | 14-20 | 0-1 | none | quiet: where the shy, the hermits and the vices go |

The real-town data (brief, sections 1.6 and 4) gives these bands:
- next-door spacing is 12-26 m, which is 4-9 tiles at 3 m a tile;
- a home has 3-11 other homes within 50 m (about 17 tiles);
- 0.2-7% of homes are isolated, and 10-35% are outlying (Pelican Town has 4 of 12, or 33%).

The templates cover those bands. Section 2.4 discusses the tile scale.

Every template also names spots: each home's front step, the benches, the centre of the linger spot and a bin. The act catalogue can use them later (section 9).

**Home interiors** are sized by household: 8×6 for one person, 9×7 for two, 10×8 (as now) for three or four, and 12×9 for five or more.
- Every shape keeps `DefaultTown.Bed` (2,2) and `Sofa` (4,4) walkable, so the engine needs no per-home spots. `TownCheck` enforces this.
- The door tile can differ per shape, since only `Links` use it.

**Example: the Lane with 12 homes** (a schematic, not the exact rows):
```
++++++++++++++++++++++++++++++++++++++++++   back gardens behind a hedge
+#####.+#####.+#####.+#####.+#####.+#####.   footprints '#' (5x3 in the real rows), plots 7 wide
...d......d......d......d......d......d...   d = front step = the door to the home
..........................................   the street, 4 rows: its ends are the
..........................................   crossings to the connector or to the
..........................................   next neighbourhood
..........................................
...d......d......d......d.....[  green  ]   front steps across the street
+#####.+#####.+#####.+#####.+.[ b     b ]   b = bench
```
Next-door steps are 7 tiles apart and the step across the street is 5 tiles away, so each step has about 5 others within 8 tiles.

### 2.3 The world plan

Neighbourhoods fill slots in a fixed order. In the pelican profile each slot has a fixed template, so the towns nest and stay comparable.

| Slot | Joins the core at (a free edge tile) | Template | Homes | People (about) | In towns of |
|---|---|---|---|---|---|
| E | Square (29,10) | Green | 8 | 17 | 60, 120, 240 |
| N | MountainPath (12,0) | Lane | 8 | 17 | 60, 120, 240 |
| W | ForestPath (8,2) | Terrace | 10 | 22 | 120, 240 |
| SE | Beach (29,6), and to E by a path: **the first loop** | Court | 7 | 15 | 120, 240 |
| NE | joined to both N and E: **the second loop** | Lane | 8 | 17 | 120, 240 |
| SW | a free FarmRoad edge tile | Scattered | 5 | 10 | 120, 240 |
| Ring 2 | outward from ring 1 | drawn from the seed | about 55 | about 120 | 240 |

- Each slot joins the core through a short connector road.
- `DefaultTown.Links()` does not use any of these attachment tiles today. This was checked, and `TownCheck` checks it again.
- About 10% of homes stay empty (4.3, step 3). The generator sizes households to hit the target exactly; the people counts above are approximate.
- Neighbouring districts join where their streets meet, with one link per street tile at the crossing.

```
                 [NE Lane] ---- [N Lane]
                     |              |  MountainPath
   [W Terrace]---ForestPath---- SQUARE ---- [E Green] -- cafe
                     |          |    \           |
            [SW Scattered]  BeachPath  Saloon,   |
                 FarmRoad       |    Clinic    path
                   Farm       BEACH ---------- [SE Court]
```
Positions are illustrative; what matters is which place joins which.

Today every route crosses the Square: the door graph is a star (a tree of 24 places and 23 links). The grown town keeps the Square as the hub but adds two loops. This fits the real-town brief, which finds that a town of 300 people or fewer has one centre.

**The plan record** is for presentation only; the rules never read it:
```csharp
public sealed record TownPlan(IReadOnlyList<District> Districts, IReadOnlyList<Lot> Lots);
public sealed record District(string Name, string Label, string Template, IReadOnlyList<string> Places);
/// <summary>Where a place sits, in world tiles, turned by quarter turns. InsideOf: a home drawn
/// inside its footprint in that place (the viewer shows it there when zoomed in).</summary>
public sealed record Lot(string Place, int X, int Y, int Rotation, string? InsideOf = null);
```
The default town gets a hand-placed plan in `TownData.Default()`. This can't change its hash, because no rule reads the plan.

### 2.4 Distances and walking

People walk 2 tiles a minute and leave for work 30 minutes early (`CommuteMinutes`).

**Rules for the generator:**
- Every home is within 65 route tiles (33 minutes) of the Square's centre.
- Every home is within 25 route tiles of its own linger spot.
- So lanes stay short. A lane with 4 plots a side (8 homes) is about 28 tiles long, and one with 6 a side (12 homes) about 42. The 12-home lane suits only slots next to the Square.
- A generated job sets `Job.Commute` from its route: half the route tiles, plus 10 minutes (section 3, E5).

**The tile-scale mismatch** (brief, section 0):
- By walking time, a tile today is about 41 m.
- By sight, 8 tiles should be about 24 m (Gehl's social field), which makes a tile about 3 m.
- So today's town is squeezed by walking time:
  - a 120-person town takes 30 minutes to cross, where a real one takes 2-3;
  - people spend more of the day walking, and walkers are seen, so passing contact is inflated.

This spec keeps today's scale (question 4), because every rule is calibrated on it. It measures walking minutes per person per day against the shipped town (6.2). If that measure fails, the fallback is a separate scale for each place.

### 2.5 Hubs

`Gathering` gains five fields at the end (section 3, E4):
- `MinAge` and `MaxAge`;
- `Capacity`;
- `Local`: the households the hub belongs to;
- `Visitors`: a weight factor for anyone not local.

| Hub | Place | Radius | When | Who | Capacity |
|---|---|---|---|---|---|
| Meeting | Square | 5 | day 0 | everyone | none |
| Market | Square | 5 (9 as an option) | Saturday 9:00-14:00 | everyone | none |
| Noon | Square | 4 | daily 11:30-13:30 | core households; visitors ×0.25 | 25 |
| Evening | Saloon | 4 | 18:00-23:00 | everyone | 30 |
| Evening on the green | each neighbourhood's linger spot | 3-4 | 17:00-20:00 | its own households; visitors ×0.1 | 15 |
| Pub evening (120 and up) | a second bar | 4 | 18:00-23:00, 18 and over | nearby neighbourhoods; others ×0.3 | 30 |
| Lessons | Square | (a job) | weekdays 10:00-14:00 | every child | (one teacher per 15 children) |

- **Core hubs keep their place, time, radius and weight.** Grown towns add only `Capacity`, `Local` and `Visitors` to them. Children can still pick the Saloon's evening, as in the shipped town; an age filter there is measured separately.
- **How the crowd limit works.** A person who arrives and sees `Capacity` or more awake people within the radius turns back. The hub is struck off for them for the rest of the day, and they decide again. This uses only what they see, and it is deterministic.
- **The fallback for the crowd limit** is to scale the hub's weight by (1 − present/capacity), counted at the start of each tick. It uses true positions, so it is kept only in case too many people turn back.
- **Fixed start times** (the market, lessons, the evening on the green) are what build lasting networks in the research (brief, section 3, rule 6).

### 2.6 How the layout decides who meets whom

Everything social needs two people in the same place and within 8 tiles. A chat also needs 15 minutes together, and an act needs 5 tiles and sight. So the layout works only through who ends up near whom, and for how long.

| Lever | How | What it makes | Dial |
|---|---|---|---|
| Front steps | doors within 8 tiles; a front-step haunt for 30-50% of adults and most elders, 16:00-19:00 | weak ties with neighbours; habits ("I see her every evening") | template density; the share with a step haunt |
| Linger spots | each neighbourhood's local hub | neighbourhood cliques; stories stay local first | local weight, `Visitors`, `Capacity` |
| Work | coworkers within 8 tiles for 6-8 hours (every workplace's staff spots fit within 16 tiles) | the strongest tie after the household, and bridges between neighbourhoods | who is hired from where |
| School | children from every neighbourhood at lessons | bridges, and later parents meeting through children | catchment |
| Central hubs | Noon, Market, Meeting, the Saloon's evening | stories cross the town | weights and capacities |
| Routes | walkers pass doors on the way to the centre | people known by sight: habits and suspect lists, not friendships | connectors and loops |
| Edges | the Beach's far end, the forest, the farms, scattered cottages | quiet places for the shy and for vices | edge haunts for shy archetypes |
| Sight | footprints block, hedges halve, bins sit in screened corners | corners hide acts; traces matter | the template drawings |
| Familiarity seeds | by circle (4.3, step 11) | who can name whom; how many must hold a scandal before a confrontation | the stranger value; fading |

---

## 3. Engine changes (all opt-in)

| # | Change | Where | Default | Pins | Step |
|---|---|---|---|---|---|
| E1 | `TownData` and `Simulation(long seed, TownData town, …)`, chaining to today's constructor | new `Town.cs`, `Simulation.Town.cs` | n/a | unchanged (tested) | T0 |
| E2 | `ReplayOptions.Town` | `Replay.cs:45-52` | null: the default town | default JSON identical | T0 |
| E3 | Routing by walking tiles, not fewest doors | `Hop`, `Simulation.cs:775-799` | the only rule, if every pin holds | unchanged (tested) | T1 |
| E4 | `Gathering` + `MinAge = 0, MaxAge = 200, Capacity = 0, Local = null, Visitors = 1` | `Model.cs:134-140`; `Decide` `:629-640`; an arrival check in `Walk` | today's behaviour | unchanged | T1 |
| E5 | `Job` + `int? Commute = null` | `Model.cs:66`; the work rule `:618` | `BodyOptions.CommuteMinutes` | unchanged | T1 |
| E6 | `TownData.Familiarity` (sparse seed pairs) and forgetting, built as `GossipOptions.Forgetting` (`ForgettingOptions`, Sid's model, question 5) | `SeedFamiliarity`; `Meeting` in `Socialise`; `Forget` at the night's close | none; `FadePerDay = 0` | unchanged | T1 |
| E7 | Performance work (section 5) | several | n/a | **byte-identical** | T5 |
| E8 | `Economy.Shops` (shop roles: grocer, chain, bar) | `Simulation.Money.cs` (94-372), `Simulation.Feelings.cs:766-782`, `Simulation.Authority.cs:309`, `Metrics.cs:304-307` | null: today's Store, Mart and Saloon, in today's order of arithmetic | unchanged | T6 |
| E9 | `Location.Offstage` (only if question 7 says yes) | `Socialise` `:1048`, `See` (Suspicion.cs:124), `Watch` `:929`, `InReach` | false | unchanged | T6 |
| E10 | Own profile only: a newcomer role in place of the name "Newcomer" | `Simulation.cs:312`, `Simulation.Feelings.cs:111`, `Simulation.Authority.cs:65, 73`, `Metrics.cs:312` | "Newcomer" | unchanged | own |

Notes:
- **E1 removes a trap.**
  - Today, passing `places:` quietly switches off feelings, money, authority, hubs and doors unless each is passed too (`Simulation.cs:258-266, 291`). The scaling probe fell into it.
  - `TownData` carries every part, so nothing defaults silently.
  - It is called `TownData` because `Simulation.Town` is already the town purse (`Simulation.Money.cs:67`).
  - Treat it as immutable. One `TownData` will be shared by parallel runs, so verify that the constructor writes nothing back into `AuthorityOptions` or `FeelingOptions`.
- **E3 can't change the shipped town.** Its door graph is a tree, so every route is unique. The algorithm:
  - each door end is a node;
  - the cost between two doors in one place is the walking distance between them, taken from the cached distance fields;
  - crossing a door costs 1;
  - Dijkstra runs once per target place, and the result is cached;
  - at each step, the walker takes the exit that gives the smallest sum of the distance to that exit and the exit's cost to the target.

  The pins prove that it changes nothing in the shipped town. If one moves, E3 becomes an option instead.
- **E6 values** are set by the generator (4.3, step 11), and the engine only reads them. Pairs not listed keep today's seeds: household 0.8, friends 0.5, everyone else 0.25, and 0 for the newcomer.
- **Rule 5 says familiarity "falls 1% a day", but that was never built.** E6 builds it as an option. Whether it goes on everywhere is question 5.
- **As built in T1 (2026-10-08)**, where it differs from or adds to the above:
  - **E3** replaces the fewest-doors `Hop` outright with `Route`: Dijkstra over door tiles once per target place, then at each step the exit with the fewest tiles from where the walker stands. On a tie the nearer door wins, then the doors' order. A walker on a tile that can't reach a door steps over to it, as before. Every pin held, so it is the only rule.
  - **E4** appends `MinAge`, `MaxAge`, `Capacity`, `Local` and `Visitors` to `Gathering`, and `Hub` to `Haunt` (the gathering a crowd spot stands for). The crowd is counted once, on entering the hub's place: awake people within the radius whom the arriving person can see (line of sight, any distance). Turning back is logged (`turned-back`). The weight fallback is not built.
  - **E5** also moves the alarm: it rings `MorningMinutes` before work, plus the job's commute less the town's.
  - **E6, forgetting** (Sid's answer 5). Each night, every tie but kin's and housemates' falls by `FadePerDay` x (1 - strength)^`StrengthPower` (2). Strength is 1 - (1 - familiarity)(1 - |regard|), each averaged both ways. A new face (familiarity under `NewFaceBelow`, 0.2) gains familiarity 1 + `WarmMeetingBoost` (2) x warmth times as fast, warmth being effective regard toward them (their kind included). New faces met in the last seven days over `NewFacesPerWeek` (10) multiply a tie's fade by 1 + `InterferenceWeight` (1) x (count / 10 - 1) x (1 - strength). Familiarity falls toward 0, with no floor. After review (2026-10-08): each face counts once in any seven days, however often it is met; regard counts toward strength and warmth only while feelings steer (T12 holds with forgetting on); and each night's fade is worked out from the night's starting values, so a pair's two halves fade alike.
  - Also added: `SimResult.Familiarity` (every pair at the end), the runner's `--forget <Name>=<value>` and a "familiarity at the end" line, and the replay's `ForgettingOptions.*` settings.
- **TellsPerDay** needs no engine change. Generated towns set it to 2 explicitly and sweep 2-4. Left at its default it jumps to 3 at 30 people (`Simulation.cs:330`), which would quietly change gossip.
- **Teleports stay.** With no route, people arrive instantly (`:687-693, 721-724`). Tests build small worlds that rely on this. `TownCheck` prevents it in generated towns.

---

## 4. How villagers are made: the generator

### 4.1 Input and output

```csharp
public sealed record TownSpec(long Seed, int People, string Profile = "pelican" /* or "own" */,
    double PerCapita = 0.8, double StrangerFamiliarity = 0.08, bool Away = false);
```
- `TownGen.Build(spec)` returns a `TownData` holding:
  - places, links, gatherings, the cast and the acts;
  - authority, economy, feelings, gossip and body settings;
  - the familiarity seeds and the plan;
  - place roles (Grocer, Chain, Bar, Clinic, School, Workshop, Green, Home…), used by the generator, the metrics and the viewer.
- **The town seed is not the run seed.** One town can be run under many run seeds, and many towns under one set of rules. The runner writes it as `pelican:120@7` (profile, size, town seed).
- `GeneratorVersion` goes into the town hash and the replay header.

### 4.2 Deterministic and nested

- **Labelled draws.** Every draw is `Rng.Unit(spec.Seed, "town", step, slot, plot, member, …)`. It is keyed by structure, never by how many draws came before, so a later step never shifts an earlier one.
- **Nested growth.** A neighbourhood's content depends only on the seed and its slot. The 60 town is found unchanged inside the 120 town, and the 120 inside the 240.
  - One documented exception: a kin tie from a later neighbourhood adds a `Family` entry to the earlier person.
  - The nesting test ignores entries that point at later slots.
- **Order.** Lists are sorted everywhere; nothing depends on a dictionary's order.
- **The core is never edited.**
  - Lewis stays mayor, "Newcomer" stays the only newcomer, and no core card changes.
  - A generated person's friendship toward a core person lives on the new card only. That is enough, since friendship counts from either card (`Simulation.cs:313`).
  - No kin ties point into the core.

### 4.3 Steps

1. **The core** (pelican): `DefaultTown`'s data, unchanged.
2. **Layout.** Stamp each slot's template and connector, add crossings and loops, place lots in the plan, and add interiors and doors.
3. **Households onto plots**, in slot order, then plot order.
   - Each template lists household shapes: single, couple, couple with children, single parent, elders, elders with a grandchild, housemates.
   - Shapes are drawn so the town follows the census shares (1 person 30%, 2 people 34%, 3 16%, 4 13%, 5 or more 7%; brief 1.1), with a mean of 2.2-2.5. Pelican Town's mean is 2.17.
   - About 10% of homes stay empty, for households that form later (0e).
4. **Members.**
   - Couples are 21-55, with partners within N(2, 3) years of each other. A parent is 22-40 at a child's birth.
   - Children are 5-12, teens 13-19 (the code's `Stage.Teen`), and elders 65-85.
   - About 15% of families with children are blended (one has a stepparent). A small share of couples are same-sex; romance uses the same rules for everyone (11a).
   - No child is under 5 until there is a rule for being looked after.
   - The core has no teens. Generated towns will, which exercises the 13+ act limits and the curfews.
5. **Names.**
   - Each name is a single ASCII token, unique in the town, drawn without replacement from curated lists (licence VERIFY).
   - A name is never a Stardew name; a place, district or act name; `*`; `someone`; `Town`; or `Newcomer`.
   - Each household has a unique surname, and its home is `Home:<Surname>`.
   - `Kind` uses the core's words, chosen by age and a drawn sex: boy, girl, young man/woman, man/woman, older man/woman, old man/woman.
6. **Kin across households.**
   - About 30% of elder households have an adult child's household in town.
   - About 20% of adults have a sibling in another household.
   - These ties point only at earlier slots.
   - Families cover for each other, so these ties are what spread cover and alibis across the town (rule 17).
7. **Jobs.**
   - New workplaces come first, by size:
     - at 60: extra slots at the Store, Mart, Saloon and Clinic; a café where the E connector meets the Square; a workshop at the N lane's end;
     - at 120: a second bar, a dock by the Beach for SE, and a school with a generated teacher once there are more than 15 children.
   - Slots are filled with the nearest suitable adults, in keyed order.
   - Hours by type: shop 9:00-17:00 with a day off, café 6:00-14:00, bar 16:00-24:00, clinic 9:00-15:00 on weekdays, school 9:00-14:00, dock 5:00-12:00. Effort is as in the core (0.8-1.3).
   - About 35% of adults have no job, as in the core (10 of 24), and most elders are retired. Children and teens go to lessons in the Square.
   - If question 7 says yes, 10-20% of the employed work Away.
   - New keepers join `Keepers`. The mayor keeps the greens, as he keeps the Square.
8. **Temperament and body.**
   - Draw an archetype: a core card of the same life stage (teens draw from the young adults). A card is not reused inside a neighbourhood until all have been used.
   - Each trait is the card's value plus N(0, 0.5 × the core's spread for that trait), clipped to [0.02, 0.98]. This keeps the way traits go together inside a person, and keeps the town's spread.
   - 10% are **wildcards**, drawn from the cast's means and covariance, so fringe people can exist (11e).
   - Children: each trait is z = 0.3 × the parents' mean z + N(0, 0.977), about 0.15 correlation with each parent. This is a placeholder until 0e builds inheritance (`inheritance-research.md`).
   - Body: the card's energy ± 10, and its bed-at ± 0.05.
   - Once 0d.6 merges:
     - Expression is drawn like the other traits;
     - Birthday is drawn uniformly over the 112-day year, for each person;
     - never copy a card with `with`, which would also copy its birthday.
   - IP: archetypes taken from Stardew's people are private-prototype data. They are replaced before any release (design section 3).
9. **Haunts.** One to four per adult and one or two per child, from zones in the templates and the core.
   - P(zone) ∝ its weight × exp(−route tiles from home / 40), times 2 if the archetype haunts that kind of place. Distance decay is applied here once, not every tick.
   - The shy (chattiness under 0.4) weight quiet zones ×1.5 and busy ones ×0.5.
   - Spots are walkable and at least 2 tiles from anyone else's spot, so people no longer stack 3-6 on one tile as they did in the probe.
   - Some people also get the front-step haunt from 2.6.
10. **Friends and tensions.**
    - Each person has 0-3 friends, chosen by likeness (age band, neighbourhood, workplace, chattiness). 70% of friendships are returned.
    - About 20% of adults have a friend in another neighbourhood or in the core. These friendships are the bridges.
    - Tensions come from the core's structural sparks:
      - rival keepers, both ways;
      - a stepchild toward a stepparent (P 0.5);
      - a young adult living with a strict parent (P 0.25).
    - Aim for about 4 tensions per 26 people, as in `TensionPairs`.
11. **Familiarity seeds** (E6). Where several rows apply, the larger value wins. Only values that differ from today's defaults are stored.

    | Pair | Seed |
    |---|---|
    | same household | 0.8 (as now) |
    | kin in another household | 0.6 |
    | friends | 0.5 (as now) |
    | coworkers, classmates | 0.4 |
    | same neighbourhood | 0.25 (so a neighbourhood starts as the whole shipped town does today) |
    | a public figure (the mayor, the keepers, the doctor, the teacher) with anyone | 0.2 |
    | anyone else | `StrangerFamiliarity` (0.08) |

    - Core pairs keep their seeds exactly.
    - 0.2 is `KnowsActorAt`, the level for knowing someone at all. With these seeds, the number of people who know an actor scales with the actor's own world, not with the whole town.
    - That number sets how many must hold a scandal before someone confronts (`Simulation.cs:1178-1179`). At 208 people, today's seeds require 52 named holders.
    - A witness can name a stranger only after a close look (clarity 0.9 − 0.7 × familiarity, design rule 2). So acts by strangers are more often put down to "someone", which only happens in a bigger town.
12. **Acts.**
    - Each person's act weights are the archetype's × U(0.7, 1.3).
    - Vices are drawn separately among adults, at the core's rates: Stole about 0.2, RummagedInBin 0.12, DrunkScene 0.08. (Temptation ignores the weight, `Simulation.Money.cs:290`.) Squabbled goes to children who have a child sibling.
    - The act list is `DefaultTown.Acts()`, with each rate set to `PerDay × PerCapita × n/26`. `PerCapita` starts at 0.8, because full per-person rates overshot in the probe (0.37 acts per person per day, against 0.30).
    - `Allowed` lists are remapped by role:
      - RummagedInBin is also allowed in each neighbourhood, with the mayor as keeper, so placed scandals happen outside the core too;
      - Stole stays at the Store and the Mart until E8.
13. **Economy.**
    - Start purse: 200 + 200 per member.
    - Wages come from the job slots, on the core's scale: teacher 300, staff 350, doctor 900, workshop 700, dock 450-700.
    - Pensions are 400-600. Allowances are 10 for children and 30-50 for teens.
    - Groceries come from the Store, or from the Mart for a keyed 30% of households.
    - Wants are set by life stage.
    - The stipend and the town's starting purse scale by n/26.
14. **Authority.** Patrol stops include each neighbourhood's street. There is still one constable (section 10).
15. **Check, then hash** (4.4).

### 4.4 TownCheck and LayoutMetrics

`TownCheck` throws on any failure. Tests run it for seeds 1-200 at every size. It checks that:
- every place can be reached from the Square (breadth-first over the doors);
- every door tile is walkable and used by one link only, and linked doors sit next to each other in the plan. (A door moves someone only when their route goes through it, so a front step can also be a haunt spot.)
- every job, haunt, hub and named spot is walkable, and Bed and Sofa are walkable in every home;
- names are unique and not reserved, ties go both ways, and ages agree with ties (a parent is at least 18 years older);
- every household has an adult or a guardian, and every keeper exists and works at their place;
- no place is wider or taller than 255, and no lots overlap;
- the route limits from 2.4 hold;
- before replay version 2 exists, there are at most 127 places.

*As built (`TownCheck.cs`, 2026-10-08; it returns a list of problems, and the generator and `--town file:` refuse a town with any):* every place reachable from the largest outdoor place; doors walkable and one link to a tile; every job, haunt, hub, patrol and named spot walkable and reachable inside its place; bed and sofa in every home, and every villager's home exists; names unique; kin in the town, both ways, and a parent at least 18 years older; the mayor, constable and keepers in the town; purses, incomes, groceries (only the Store or the Mart sell them), allowances and wants; starting regards and hubs' own households naming people and households in the town; places up to 255 tiles a side, and at most 127 places. Not checked yet: reserved names, households with no adult, keepers working at their place, plan lots (the plan isn't built), and the route limits of 2.4 (`TownGenTests` checks the generator's 25-tile limit to the green).

`LayoutMetrics` measures a town before it is run, against the real-town bands (brief, section 5). It measures:
- doors within 8 tiles of a door;
- homes per neighbourhood;
- isolated and outlying homes;
- route minutes from home to the centre and to the nearest linger spot.

These are reported, not gated, until the first runs set the bands.

### 4.5 Reading a town

- `UnderGlass.Run --town pelican:120@7 --describe` prints a census card.
  - For each neighbourhood, it lists every household with its address, members, ages, jobs and archetypes. Then it lists the shops, keepers and hubs.
  - It is built from templates, with no model text.
- `--dump-town town.json` writes the town, and `--town file:town.json` reads it back.
  - Sid can edit a town by hand.
  - This also delivers 0d's "the town as JSON".
- `TownHash` is FNV-1a over the town's canonical JSON.

**As built (2026-10-08, `TownJson`, `TownJsonTests`)**: `--dump-town town.json` writes the town named by `--town` (the shipped town without it) and exits; `--town file:town.json` runs it, in the runner and the replay tool. The file is `TownData` itself, indented: the cast's cards, the places' rows, the doors, the hubs, the act kinds and every option. Tuples are arrays (`[from, to]` for a patrol hour, `[a, b, value]` for a familiarity seed, `[from, to, regard]` for a starting tension), enums are names, and numbers that are not finite are written `"Infinity"`. What the code works out (a place's width and height, a villager's home, a life stage, an act kind's tier) and the one setting that is code (`FeelingOptions.CloseCall`) are left out. A town read back writes the same file, has the same census hash and runs the same log. The hash stays `Census.Hash`, over the census card rather than the JSON, so a file's formatting can't change it.

### 4.6 The 31 town (optional, question 6)

The core plus Stardew's other five:
- Clint, at the Blacksmith on TownLane;
- Willy, at a fish shop on the Beach;
- Elliott, in a cabin on the Beach;
- Linus, in a tent off MountainPath;
- the Wizard, in a tower off ForestPath.

Their cards are written by hand. Sensitivity comes from `fixtures/game/temperament/temperament.json`, which holds all five. The other traits are mapped onto the core's scale. Every lore detail (Linus and the bins, the friendships) is recalled, not checked: VERIFY each.

It is a cheap step: the core people plus five newcomers, with five new places and doors. If the gates move there, the cause is the new people and places, not the generator.

### 4.7 The own profile (the alternative)

- The centre is generated too: a square and high street, with institutions by size:
  - a grocer always, and a clinic;
  - a chain store and a café at 60 people or more;
  - one bar per about 60 adults;
  - a school once there are more than 15 children;
  - a town hall with the lockup.

  Shop-owning households live behind their shop.
- The anchors are generated: a mayor aged 50-70 with high understanding, the keepers, the doctor and the teacher.
- It needs E8 and E10.
- **Its calibration is the hamlet test:**
  - 100 own towns of 26 people with Pelican's household mix: 5 singles, 2 pairs, 3 households of three and 2 of four, with 2 children, 2 elders and no teens;
  - each run for 3 seeds × 112 days, against 20 seeds of the shipped town;
  - it passes when the generated median sits inside the shipped town's 10-90% seed band on at least 80% of the measures in 6.2.

---

## 5. Sizes and what they cost

| Size | Contents | Households | Places | s per game day | Full gate set, 4 cores | Replay, 112 days | Needs |
|---|---|---|---|---|---|---|---|
| 26 | shipped | 12 | 24 | 0.077 *measured* | about 15 min | about 4.6 MB in v1 | nothing |
| 31 | + the Stardew five (optional) | 17 | about 31 | about 0.1 | about 20 min | about 6-7 MB in v1 | T0-T2, the cards |
| 60 | core + E and N | about 27 | about 46 | about 0.28 (0.21 *measured* at 52) | about 50 min | about 14 MB in v1; less in v2 | T0-T4 |
| 120 | core + ring 1 (six neighbourhoods), second bar, school | about 53 | about 88 | about 1.2 projected (0.93 *measured* at 104); target 0.4 or less after T5 | about 3.6 h; about 1.2 h after T5 | 36.6 MB *measured* at 104 in v1; target 15 MB in v2 | T5, T6, replay v2 |
| 240 | + ring 2 | about 105 | about 160 | about 4.4 projected (3.28 *measured* at 208) | about 14 h (report only) | | replay v2 (more than 127 places) |
| 400 | stress only | about 170 | about 250 | 10.2 *measured* at 416 (3-day run) | report only | | everything above |

The full gate set is 44,800 game days: the band (400 seeds × 14 days), one year (200 × 112) and three years (50 × 336).

**What drives the cost.** These shares come from profiling the copy town (*measured*):

| Phase | At 208 people | At 26 people |
|---|---|---|
| See | 33% | 12% |
| Pursue | 33% | 27% |
| Socialise | 24% | 15% |
| Live | n/a | 24% |

- The desire gate is about 55% of the cost at 104 people.
- Cost follows crowding, not head count. In the copy town, everyone crowds into the same places.
- A town with neighbourhoods should therefore cost less than the probe did. That still has to be measured.

**Memory** (*measured*): at 104 people over 112 days, the peak was 350 MB, 246 MB was still held after the run, and the log was 664k lines (22.8 MB).
- The runner keeps every `SimResult` until it summarises (`UnderGlass.Run/Program.cs:147-148`).
- At 104 people, 200 seed-years would need about 50 GB.
- This must be fixed before the first sweep of a grown town.

**Performance work, in order** (T5). Each item must leave every hash byte-identical.
1. **Pair loops by place.**
   - Each tick, build the list of awake people in each place.
   - `Socialise`, `See`, `Watch`, `FondMotives`, `InReach`, `Candidates` and the thief's count then pair only people in the same place.
   - *Measured* on the copy town, the hashes did not change: `9d3f01b56483816c` for the default town (seed 1, 14 days) and `6ecada6712c7c474` for the k=4 copy town (7 days). At 208 people it went from 2.63 to 2.00 s per day.
2. **Integer pair keys** instead of string tuples, in `_sightings`, `_habits` and `_spans`.
3. **Indexes:**
   - `_avoid` by holder (`AvoidCount`, Desire.cs:112);
   - hits by target (`Fear`);
   - sentiments by holder (Feelings.cs:451, 467, 753);
   - household sizes cached for `WeekCost` (Money.cs:112-117).
4. **Incremental work:**
   - `CheckScandals` checks only scandals that gained a holder that tick;
   - `CloseDay`'s holder counts are kept up to date as they change;
   - `_chatted`, `_told` and `_tellsToday` are pruned each night.
5. **An LRU cap on `_fields`.** A field is a pure function of its key, so evicting one never changes a result.
6. **The runner:**
   - each run is reduced to a stats row as it finishes, through `Accumulate` and `Finish` on `Metrics`, `FeelingMetrics`, `DesireMetrics` and `TownMetrics`;
   - an option hashes the log as it is written, instead of keeping it.

---

## 6. Measurements: is the bigger town better?

A bigger town is better only if it meets two conditions:
- it is still as good a story machine, measured per person;
- it gives what only a bigger town can: neighbourhoods, stories that travel, strangers, and an authority that matters.

Each size is run at 31 (if built), 60 and 120.

### 6.1 Layout checks (before running)

`TownCheck` must pass for seeds 1-200, and `LayoutMetrics` are reported (4.4).

### 6.2 Gates that must still hold, per person and per neighbourhood

| Gate | Today (26) | Grown town |
|---|---|---|
| **E1** | a new feud and a new friendship in 60%+ of seed-years | (a) By ward. The core is one ward. Each neighbourhood is pooled with its nearest until a ward holds 25+ people. 60%+ of ward-years must pass. (b) Per 100 people a year, feuds and friendships within 0.5-2× the 26 town's own rate, over the same seeds |
| **Dead and war towns** | shares of all pairs | The same thresholds, counted over **acquainted pairs** (familiarity 0.4+): moved 0.1+ at 5-25%, below −0.2 at 1-5%, and a war town above 10%. Counted over all pairs, these shares fall as 1/n: the probe's 52-person town already read 4.9%, which counts as "dead" |
| **0a band** | 52%+ of witnessed placed scandals reach 40-70% of the town | Two readings: the share of the town, and the share of the **actor's circle** (people at familiarity 0.2+ toward the actor when the act began). The gate uses the circle until Sid decides (question 2). A harness option places the scandal in each neighbourhood in turn, by seed, so every neighbourhood is measured |
| **Three years** | year 3 below −0.2 at 1-5%; moved at day 335 at most 1.5× day 111 | the same, over acquainted pairs |
| **Money** | town cash drops less than 30% a season; nothing lost to rounding | The same, per head. Households in debt no more than the 26 town's share, by neighbourhood. (In the probe at 104 people, 8.3 of 48 households were in debt, against 0.3 of 12 at 26) |
| **Hermits and brawlers** (0d.6) | 0.2-1.0 sustained hermits a seed-year | per 100 people, by neighbourhood. Does the scattered edge make more? |
| **Activity** | 0.30 acts per person per day; 3.8 tellings per person per day (28 days) | acts per person per day 0.24-0.36; tellings per person within ±30% of the 26 town over the same run length (the probe had 24.2 at 104) |
| **Witnesses and walking** | 4.1 witnesses per act | witnesses per act at most 1.5× the 26 town (the probe had 21.2 at 104); walking minutes per person per day within ±30% of the 26 town (the tile-scale check, 2.4) |

### 6.3 Locality: a bigger town, not a bigger crowd

*Built 2026-10-08 as `TownMetrics` (the runner prints it for any `--town`): the locality ratio from tellings (not yet chat minutes), tellings across districts, bridges within two days, and the median person's count of people known well. Crowding and the door gradient need positions over time and are not built yet.*

| Measure | Target (first guess) | Probe |
|---|---|---|
| **Locality ratio**: the share of chat minutes and tellings inside a neighbourhood, divided by the share random mixing would give | 2 or more at 120 | about 1 |
| Tellings across neighbourhoods | 15% or more of all tellings, so districts still talk | n/a |
| Bridges | in most runs, a story started in one neighbourhood reaches another within 2 days | n/a |
| People each person knows well (familiarity 0.4+), median after a year | 15-45 at every size (about 25 at 26) | 80-97% of the whole town |
| Crowding | mean others within 8 tiles of an awake person: 3-5; 95th percentile: 25 or fewer; each hub's peak at or under its limit | mean 11.0 and max 81 at 104 |
| Door gradient (brief, section 5) | ties outside kin at least halve from 1 door away to 2 doors away; 50%+ of them fall inside the household's own neighbourhood | n/a |

### 6.4 Calibration

- **The core stays itself** (pelican profile). Core-only measures inside the grown town stay within ±25% of the shipped town:
  - feuds and friendships among core pairs;
  - acts per core person;
  - Penny's and Pam's stance and hours out;
  - the core's own band.
- **The recast** (T3). 100 generated casts of 26, on the shipped map, pass the hamlet test of 4.7. This checks the people generator alone, before any layout is generated.

### 6.5 Better stories

- **Sid's read.** Sid watches a season of the 60 town in the viewer beside a season of the shipped town (later, the season journals of E5). He says which he would rather follow, and why.
- **Texture counts** (reported, not gated):
  - acts whose witnesses could only say "someone", because the actor was a stranger;
  - stories that reached a neighbourhood late;
  - feuds across neighbourhoods, against feuds within them;
  - the constable's share of reports, against the mayor's;
  - differences between dense and sparse neighbourhoods in feuds, hermits and gossip.

### 6.6 Decision rule

A size is worth keeping when:
1. every layout check passes for seeds 1-200;
2. every gate in 6.2 holds;
3. at least four of the six locality measures in 6.3 hit their targets;
4. the calibration in 6.4 holds;
5. Sid prefers its season, or finds the two level.

If item 5 fails, the size stays an experiment.

All new measures live in a new `TownMetrics.cs`, which reads the town's plan and households. `FeelingMetrics` gets an overload that takes a pair filter.

---

## 7. The replay recorder and the viewer

### 7.1 Recorder: version 2

- **Any town.**
  - `ReplayOptions.Town` (E2) lets the recorder take any town.
  - Today `Replay.Record` builds the default town itself (`Replay.cs:45-52`). With `Town` null it still does, and the file is identical.
- **More than 127 places.**
  - Today `Pack` stores the place index and "asleep" together in one byte (`Replay.cs:252-266`), and homes count toward the 128 places. The probe threw at 132 places.
  - Version 2 writes the place as a varint of (index × 2 + asleep).
  - x and y stay one byte each, and `TownCheck` keeps places under 256 tiles.
- **Regard written sparsely.**
  - Today every day writes n² regard values (`:91-98`). This dominates the file: 36.6 MB at 104 people over 112 days.
  - Version 2 writes a full keyframe every 7 days. On the other days it writes only the pairs whose rounded value changed.
  - Target: 120 people over 112 days in 15 MB or less.
- **New fields:**
  - `version: 2`;
  - `town`: the spec, town hash and generator version;
  - `plan`: districts and lots;
  - `people[].household` and `people[].district`;
  - `places[].district` and `places[].role`.
- The viewer reads both versions.

### 7.2 Runner

- New options: `--town default|copy:k|pelican:N@seed|own:N@seed|file:path`, `--describe` and `--dump-town`.
- `--trait` and the metrics read the town's cast and economy, not `DefaultTown`'s (`UnderGlass.Run/Program.cs:81, 266, 283`).
- `copy:k` brings the scaling probe's copy town into the repo. It is the worst case for crowding, and serves as the performance benchmark.

### 7.3 Viewer

What exists on `claude/affectionate-pasteur-qzdpvi`:
- a Population tab (48388b6);
- a whole-town map at the top that plays live (8d3da7d), with zoom and pan up to 12× (d720e48).

The map guesses each place's position from its doors (`layoutTownPlan`). Changes:

1. **Positions from the plan.** When the file has a `plan`, place every lot at its X, Y and rotation, and draw homes inside their footprints when zoomed in. Otherwise keep today's guess.
2. **Detail by zoom:**

   | Zoom | Shows |
   |---|---|
   | 6+ px a tile | tiles, people, and interiors inside their footprints |
   | 2-6 px a tile | places as blocks; people as dots outdoors; a head count on each indoor place; a home lit while anyone in it is awake |
   | under 2 px a tile | neighbourhoods as shapes, with head counts and act rings |

   - Draw people from per-place lists and skip anything outside the view, to keep 60 frames a second at 120 people.
   - Add a mini-map inset when zoomed in, and a "follow" mode for the selected person.
3. **Neighbourhood chips** (All, Centre, and one per neighbourhood). One choice filters the Town tab, the Population rows, the matrix order, the stories and the log.
   - *Built 2026-10-08* as a picker beside the tabs. It filters rather than reorders the matrix, and also narrows the ring and the ties and fades the rest of the town on the map. Places take their neighbourhood from `TownMetrics.PlaceDistricts`, and the replay records `people[].district` and `places[].district` in version 1, since old viewers ignore extra fields.
4. **Town tab.** One map per place does not work for about 88 places. Instead the tab shows:
   - the chosen neighbourhood's public places, as maps;
   - its homes as a street strip, with a small box per home and occupant dots;
   - a "busiest now" row: the 6 places with the most people or acts in the last hour.
5. **Population at 120.**
   - Fixed-height rows, drawing only those on screen plus 10.
   - Neighbourhood and household columns, a filter, and grouping by household.
   - "Liked by" and "likes" computed once per day (regard changes daily), not every frame. Today they are O(n) per row, so O(n²) a frame.
6. **Relationships.**
   - Order by neighbourhood, then household, then name, with lines between neighbourhoods.
   - Draw cells on a canvas at max(2, floor(width / n)) pixels, not a fixed 15 px.
   - Put a neighbourhood-by-neighbourhood summary on top (mean regard, feuds, flows of tellings). Clicking a block zooms to it.
   - Add an ego panel: the 10 people the selected person likes most and the 10 they like least.
7. **Stories.**
   - Show spread by neighbourhood ("reached the Court on day 3"), as bars and as a tint on the map over time.
   - Index tellings by listener. The who-told-whom tree today loops over every telling for each holder.
8. **Inspector.** Add neighbourhood, address, workplace and commute minutes.

---

## 8. Steps in order, with tests

Every step runs `dotnet test sim/UnderGlass.sln` with every pin unchanged.

| Step | What | Tests and measurements |
|---|---|---|
| **T0 Plumbing** (built 2026-10-08: `Town.cs`, `Simulation.Town.cs`, `ReplayOptions.Town`, `TownDataTests`) | E1 and E2. `TownData.Default()` built from `DefaultTown`'s public methods | Every pinned hash is reproduced through `new Simulation(seed, TownData.Default() with { … })`; the default replay JSON is byte-identical |
| **T1 Engine options** (built 2026-10-08: `Simulation.Growth.cs`, `TownGrowthTests`; section 3's "As built in T1") | E3-E6, each a one-line hook into `Simulation.Growth.cs`; appended fields on `Gathering` and `Job` | A small scene per option: a hub turns people away at its limit; a local hub draws its own households; a commute starts on time; tile routing takes a loop; familiarity is seeded from the list and fades. All pins and every existing scene test hold, which also proves E3 on the shipped tree |
| **T2 Plan and pipeline** (`--town`, `--describe`, `--dump-town` and `--town file:` built 2026-10-08 with T2b-T4; the rest not yet) | `TownPlan`; the default's hand-placed plan; replay version 2; the runner's `--town`, `copy:k`, `--describe` and `--dump-town`; per-run stats and the streamed log hash (section 5, item 6); the viewer reads version 2 and the plan | Every place has a lot, and no lots overlap; `Pack` and `Unpack` round-trip in both versions, including place 300; a version 2 file of the default town decodes to the same moves, regard and acts as version 1; runner memory stays flat across 50 runs |
| **T2b The 31 town** (Sid said yes; built 2026-10-08: `Towns.Pelican31()`, `TownCheck`, `Town31Tests`, `--town pelican31`) | the five cards and places | `TownCheck`; a new pin; the full gate set at 31 |
| **T3 People generator: the recast** (the people generator built 2026-10-08 with T4, in `Generation/`; the recast of the shipped map and the hamlet test are not built yet) | `Generation/` (Names, Households, Archetypes, Livelihoods, Haunts, Economy), filling the shipped map's 12 homes, jobs and haunt zones with generated people | The same spec gives the same town; `TownCheck` for seeds 1-200; trait means, spreads and the three main correlations (boldness-understanding −0.70, chattiness-self-regard +0.44, understanding-self-regard +0.46) within bands of the cast's; the hamlet test of 4.7 |
| **T4 Layout generator, 60** (built 2026-10-08: `TownGen`, `TownGenTests`, `--town pelican:60@<seed>`, `--describe`; see "As built in T3 and T4" below) | templates, slots, connectors, loops, local hubs, familiarity seeds, per-person rates, `LayoutMetrics`, `TownMetrics` | `TownCheck` for seeds 1-200 at 60 and 120; `TownHash` pinned for three specs; nesting (the 60 town's people are found unchanged in the 120 town); a 7-day log hash pinned for `pelican:60@1`; the full gate set and 6.3-6.5 at 60 |
| **T5 Performance** | the list in section 5 | **Every pin byte-identical**, and the `copy:4` and `pelican:60` hashes unchanged; s per day and memory at 26, 60, 120 and `copy:8`; target 0.4 s per day or less at 120 |
| **T6 The 120 town** | ring 1 complete; the second bar, with E8; the school; E9 if question 7 says yes | `Shops = null` gives every old hash; new pins; the full gate set at 120, with and without the second loop |
| **T7 Viewer at scale** | section 7.3, items 2-8 | a 120-person replay loads and plays; frame time measured with the town in view and in each tab |
| **T8 Stress and the own profile** | ring 2 (240); 400; the own profile if question 1 chooses it | report only at 240 and 400; the hamlet test for the own profile |

T3 and T4 are new files only. T5 can run alongside them once 0d.6 has merged.

**As built in T3 and T4 (2026-10-08)**, where it differs from sections 2-4 (generator version 2 after the review's fixes: `sim/README.md`, "Fixed after review"; `TownHash` is now FNV-1a over the town's JSON, as 4.5 says):
- **The 60 town is the 31 town plus slots E and N**, since Sid chose both the 31 step and 60. Each slot has a fixed number of people (E 15, N 14), so the pelican profile grows to 31, 46 and 60, and a smaller town is found unchanged in a bigger one; the generator refuses other sizes.
- **Templates are built from a few numbers** (`Templates.Green`, `Templates.Lane`) rather than drawn in a data file; the rows are the same kind of thing, and `--describe` prints the census.
- **Each slot brings its own jobs** (the café, the workshop, staff for the core's places), filled by its own adults, so a later slot never takes an earlier slot's jobs; new staff at the clinic and the blacksmith's are paid from outside, so the core's purses aren't drained.
- **A household with no wage and no pension gets an outside earner**, enough for its size; without it, a third of the generated households ran into debt and one young man rummaged from need four times a year.
- **Names**: each slot draws from its own keyed share of the lists (a rare town with many of one sex borrows from the whole list).
- **Not built yet:** the recast of the shipped map and the hamlet test (4.7), `LayoutMetrics` (4.4, 6.1; `TownMetrics` was built in #54), the per-person rates' own column, the plan (T2), and the other ring-1 slots (T6).

---

## 9. Coordination

**0d.6** (`claude/under-glass-0d6`):
- It edits `Model.cs` (the ends of `Temperament` and `Villager`), `DefaultTown.cs`, `Simulation.cs`, `Simulation.Desire.cs`, `Simulation.Feelings.cs` and the runner.
- It has no `Replay.cs`, because it branched before the recorder.
- T0 touches none of those files, so it can land now.
- T1, T5 and T6 land after 0d.6 merges, or rebase onto it.
- Any field appended to `Villager` or `Temperament` goes after 0d.6's `Expression` and `Birthday`. This spec appends none.
- The generator (T3, T4) is new files. It draws Expression and Birthday once they exist.

**The viewer** has uncommitted edits in the main working tree from another session. Viewer work (T2, T7) starts after they land.

**The act catalogue** (Sid's third ask, which has its own spec):
- Generated people get a new act through the archetype cards or the vice table, so a new act needs only a new column in the generator.
- Place roles (Bar, Grocer, Chain, Green, Yard, School, Clinic, Workshop) let `Allowed` lists name roles. The generator turns them into places.
- The templates' named spots (benches, a counter, a bar, a stall, a bin) are where acts such as serving or sitting out can happen.
  - `Location.Spots`, appended, is the likely hook.
  - It can take over the shared Bed and Sofa tiles later.
- Per-person rates apply to new rate-drawn kinds too.
- Both pieces of work change only opt-in towns, with one exception: a new scandal kind changes which scandal `Harness.ScandalFor` places. So it re-pins `388d128d7fd4cdf2`, even at a rate of 0.

---

## 10. Risks

| Risk | Mitigation |
|---|---|
| Growth dilutes the core: more contacts change its feuds and stances | the "core stays itself" check (6.4); the 31 step before the generator |
| Locality too strong (separate villages, no story crosses) or too weak (one crowd, as in the probe) | the locality ratio, cross-neighbourhood tellings and bridges (6.3). Dials: `Visitors`, capacities, the Market's radius, fading, the loops |
| Generated people are bland or clone-like (four Pierres in one store, as in the probe) | archetypes not reused within a neighbourhood; mutation; wildcards; vices drawn on their own; trait spread compared with the core's |
| Crowd limits waste trips, or move the crowd to the next hub | checked by sight on arrival only; turn-backs and the 95th percentile of crowding measured |
| Opt-in code leaks into the pins | branches off by default; keyed draws; `PinnedTests` in every PR; T5 must match byte for byte |
| With one constable for many neighbourhoods, far scandals are rarely reported, since a report means meeting the mayor or constable within 8 tiles | reports measured by neighbourhood. After measuring, decide whether a lawless edge is a story or a bug (later, perhaps one constable per about 50 people) |
| Familiarity never falls today, so within a year the weekly Market makes everyone "know" everyone | E6's fading, measured |
| Per-person rates overshoot (0.37 against 0.30 in the probe) | `PerCapita` starts at 0.8, tuned against the activity gate |
| Time and memory block sweeps at 120 | T5 and the runner's memory fix before any 120 sweep |
| Walking dominates the day at today's tile scale | walking minutes measured (6.2); a per-place scale as the fallback (question 4) |
| A missing door teleports people without an error | `TownCheck` |
| Stardew facts and lore are recalled, not checked | VERIFY markers; geography from the real-town brief's research, not memory; the five and the archetypes kept as private-prototype data |
| Merge conflicts with 0d.6 or the viewer edits | new files; one-line hooks; the order in section 9 |

---

## 11. Questions for Sid

**Sid's answers (2026-10-08)**, which replace the recommendations where they differ:

1. **Grow Pelican Town** for now (a).
2. **A scandal's reach depends on its context**, not on a fixed band: "it could reach 100% of the town if it was really
   public or very limited if it was resolved or covered up quickly." He also noted the town may not have enough
   possible scandals yet. It does not: only rummaging in a bin and stealing from a shop are scandals, there is no
   upheaval at all, and neither happened in a 56-day run of seed 1 (the band is measured on a scandal the harness
   places). So the 40-70% band is replaced by scenario checks on reach (public and witnessed reaches nearly everyone;
   covered up, kept in the family or settled quickly stays small), written with the new scandal kinds that the
   life-sim research proposes (in progress, 2026-10-08).
3. **Start with 60.**
4. **Keep today's tile scale** (a), and measure walking minutes per person per day against the shipped town.
5. **Forgetting, as Sid describes it:** people forget each other, but how fast depends on the relationship: the more a
   relationship has built, the harder it is to forget; a new person is remembered more easily when there was some
   attraction; and a person can be forgotten because too many new faces pass by. So familiarity falls by a rate that
   shrinks with the tie's strength (familiarity and regard both ways, and kin), rises faster at a first meeting when
   regard starts warm, and falls faster for weak ties when many new faces are met in a short time (interference, a
   soft cap on how many people one keeps up with). Opt-in, measured at 26, then 31, then 60 (as recommended in (a)).
6. **Add the other five** (a): Clint, Willy, Elliott, Linus and the Wizard, as the 31-person step.
7. **As recommended:** no work out of town at 60; try it at 120 as a measured option.

The original questions, with the recommendations, follow.

1. **Which town to grow first?**
   - (a) Pelican Town, grown: the shipped 26 at the centre, untouched, with generated neighbourhoods round them.
   - (b) Under Glass's own town: everything generated, including the centre and its anchors.
   - (c) Both from the start.
   - *Recommended: (a).* It keeps the calibrated people where Sid can read them, and it measures whether growth changes them. It also needs no role changes in the engine files 0d.6 is editing. The generator is built so that (b) is a profile switch later (T8).
2. **The scandal band in a big town.** Should a witnessed scandal reach 40-70% of the whole town, or 40-70% of the actor's circle (the people who know the actor)?
   - (a) The whole town.
   - (b) The circle.
   - (c) Both, each a gate.
   - *Recommended: (b) as the gate, with (a) reported.* At 26 people the two agree. At 120, holding gossip to the whole-town reading would push it toward saturation.
3. **What size is the game for?**
   - (a) 26-31: the whole town can be followed.
   - (b) About 60: two or three neighbourhoods.
   - (c) About 120: watch one neighbourhood at a time; the constable matters more (11c).
   - (d) 240 or more.
   - *Recommended:* build and measure up to 120, play-test 60 first, and decide after watching a season of each. Use 240 and up as stress tests only.
4. **Tile scale and walking.**
   - (a) Keep 2 tiles a minute and compact neighbourhoods. A tile is a "time tile": walks are slow, but every rule is calibrated on it.
   - (b) A scale per place. Lanes, squares and yards use about 3 m a tile, with faster walking inside them, and connectors are sized by walking time. This is an appended field, so the shipped town is unchanged.
   - (c) One human scale everywhere, at about 27 tiles a minute. That means about 13× more steps, sight checked at each step, and every pin re-pinned.
   - *Recommended: (a) now*, with walking minutes measured (6.2). Use (b) only if that measure fails.
5. **Familiarity falling.** Rule 5 says familiarity "falls 1% a day", but it was never built.
   - (a) In grown towns only, as an option.
   - (b) Everywhere, as rule 5 is written. This re-pins the shipped town and moves the 0a band.
   - (c) Not yet.
   - *Recommended: (a)*, then (b) as a change of its own once it has been measured at 26.
6. **Stardew's other five** (Clint, Willy, Elliott, Linus and the Wizard) as a 31-person step.
   - (a) All five.
   - (b) All but the Wizard.
   - (c) Skip it and go straight to generated neighbourhoods.
   - *Recommended: (a).* It is a cheap calibration step, and the Wizard is a natural test case for the hermit work. It stays a private prototype, and all lore is marked VERIFY.
7. **People who work out of town** (an offstage "Away" place where nobody sees them).
   - (a) No: every job is in town.
   - (b) Yes: 10-20% of the employed leave for the day.
   - *Recommended: (a) at 60, with (b) tried at 120 as a measured option.*

## 12. Docs in the same PRs

- `sim/README.md`: what each step built, its measurements and the test counts.
- `docs/under-glass/design.md`:
  - an "As built" note under rule 5 (familiarity fading, if built) and under rule 11 (hubs);
  - the new gates in section 10's risk table;
  - Sid's answers in section 12.
- This file: its status, and anywhere the code differs from it.
- `sim/viewer/README.md`: version 2 and the new views.
