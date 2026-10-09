# Real towns as a guide to Under Glass layouts: a data brief

> Historical research input for the town generator; the [roadmap](roadmap.md) puts further
> town expansion later. See `sim/README.md` for the generator and maps already implemented.
> The measurements below remain research evidence, not mandatory layouts or a new work queue.

This brief turns real small-town data into numbers a layout generator can use. The numbers come from eight real towns measured from open map data, plus published research. Distances are given in metres and in walking minutes, so they still hold whichever tile scale is chosen (section 0). Nothing in the repository was changed.

## How the numbers are marked

- **[M]**: measured from Overture Maps open map data (release 2026-09-23.1) by the data researcher, within 1.5 km of each town's centre [Overture]. Results are in `docs/under-glass/town-data/results/*.json`.
- **[M+]**: measured for this brief from the same extracts: home spacing, and the share of homes within a walk of a shop. The script is `docs/under-glass/town-data/scripts/analyse_extra.py`, a copy of the researcher's `analyse.py` with extra metrics. Output is in `docs/under-glass/town-data/results-extra/*.json`.
- **[P]**: published work. The proxy blocked almost every research host, so these figures come from search-result text that quotes the source, not from the page itself. Check one against the original before relying on it.
- **[D]**: derived here from [M] or [P] numbers.
- **[VERIFY]**: recalled, and not seen in any source this session.

Distances are straight lines unless stated. Walking routes are longer.

### The eight measured towns

| Town | Form | Population (cited; verify) | Homes in 1.5 km [M] | Est. residents in circle [D] |
|---|---|---|---|---|
| Mevagissey, Cornwall | harbour village | parish 2,160; built-up area 1,749 [Pop-Mevagissey] | 1,098 | 2,590 |
| Hay-on-Wye, Wales | market town | community 1,675; built-up area 2,059 [Pop-Hay] | 856 | 1,600 |
| Long Melford, Suffolk | linear village | parish 3,897 (2021) [Pop-LongMelford] | 1,277 | 3,010 |
| Laxton, Notts | farming village | parish 251 (2021) [Pop-Laxton] | 136 | 320 |
| Bloomfield, Iowa | Midwest grid | 2,682 people, 1,144 households [Pop-Bloomfield] | 1,050 | 2,460 |
| Monpazier, Dordogne | bastide (planned grid core) | commune 441 [Pop-Monpazier] | 521 | 1,150 |
| Ogimachi, Shirakawa-go | Japanese farming village | about 600 people, 150 households [Pop-Ogimachi] | 325 | 850 |
| Ine (Hirata), Kyoto | strip round a bay | town 1,928 over 62 km² [Pop-Ine] | 325 | 740 |

**Caveats.**
- "Homes" are buildings, not dwellings:
  - a terrace mapped as one polygon counts once, so Hay is undercounted;
  - Laxton's machine-drawn footprints include barns, about 25% too many;
  - Bloomfield is close to its census: 1,050 buildings against 1,144 households.
- Holiday lets inflate Mevagissey, Monpazier and Ogimachi.
- Shop positions are geocoded and can be tens of metres off.
- Hay (a book town), Ogimachi and Monpazier are tourist places with more shops than their residents alone would support.
- The circle often holds more than the named place. Monpazier's circle takes in farms in neighbouring communes, so only its walled core, 226 homes within 200 m, matches the 441-person commune.

**What the sample covers.** It has a 300 tier (Laxton, plus Monpazier's walled core) and a 1,600-3,900 tier (the other six). No real place below 250 people was measured. Every number for 30-120 people is derived or published.

## 0. Settle the tile scale first

**What the sim does now:**
- People walk 2 tiles a game minute (`WalkTilesPerMinute = 2`, `sim/UnderGlass.Sim/Simulation.cs:66`).
- Sight reaches 8 tiles, in bands of 0-2, 3-5 and 6-8 tiles (`docs/under-glass/design.md` rule 2; `Perception.cs`).
- A chat needs 15 minutes co-located (design rule 8).

**Real walking pace** is 1.37 m/s, about 82 m a minute (1,177 pedestrians in Istanbul; field studies range 1.0-1.5 m/s) [P, WalkSpeed].

**The mismatch:**
- By walking time, a road tile is about 41 m.
- Sight of 8 tiles should cover about 8-25 m. Gehl gives about 7 m for conversation and about 25 m for reading faces [P, Gehl].
- On a road today, 8 tiles is about 330 m. In a town of 300 on a 1 km street, one walker would "see" a third of the street, and road meetings would grow with every new resident.
- Real neighbours are close: in all eight towns the median home's nearest neighbour is 12-26 m away [M+]. At 41 m a tile, next-door doors would share a tile.

| Option | What it means | Gain | Cost |
|---|---|---|---|
| A. A scale per place | Each Location has its own metres per tile: about 3 m for lanes, squares and yards; connecting roads without doors sized by walking time. Walking pace and sight are set in metres. | Long roads stay cheap; sight is right near doors | Two scales to keep straight |
| B. One human scale | About 3 m a tile everywhere, walking about 27 tiles a minute | One rule | About 13× more steps. Co-location must be checked at each step, or walkers pass unseen. Run time not measured |
| C. Keep today's | 2 tiles a minute, lanes squeezed | No work | Doors 4-6 tiles apart are 160-240 m of walking; sight on roads 330 m; gets worse as the town grows |

**Recommendation [D].** Use about 3 m a tile wherever there are doors, haunts or gatherings:
- 8 tiles is then 24 m, Gehl's "social field";
- the close band (0-2 tiles, 0-6 m) is conversation distance;
- the real neighbour spacing of 12-26 m puts next-door doors 4-9 tiles apart.

Use option A for long connecting roads if B is too slow at 300 people, and measure run time first. This is Sid's design choice, not a research finding.

| Real distance | Walk at 80 m/min | Tiles at 3 m | Today's tiles (by walking time) | Guidance |
|---|---|---|---|---|
| 25 m | 0.3 min | 8 | 0.6 | Gehl's social field [P, Gehl] |
| 100 m | 1.25 min | 33 | 2.5 | |
| 200 m | 2.5 min | 67 | 5 | "Desirable" walk to a town centre [P, CIHT2000] |
| 400 m | 5 min | 133 | 10 | "Acceptable" [P, CIHT2000]; Perry's school radius [P, Perry] |
| 800 m | 10 min | 267 | 20 | "Preferred maximum" [P, CIHT2000]; median US walk [P, Yang2012] |

## 1. The numbers that should drive a generator

### 1.1 Household size

| Setting | People per household | Source |
|---|---|---|
| England and Wales 2021 | 2.36 (58,555,851 people in 24,783,199 households; published as 2.4) | [P, ONS2021] |
| France 2020 | 2.2 | [P, INSEE] |
| United States | about 2.5; 27.6% of households have one person (2020) | [P, USCensus2023] |
| Hay ward 2021 | 1.87 (1,680 people in 897 households) | [P, Pop-Hay] |
| Bloomfield 2020 | 2.34 | [P, Pop-Bloomfield] |
| Shirakawa village 2020 | 2.61 (1,511 people in 580 households) | [P, Pop-Ogimachi] |
| Pre-industrial England, 1574-1821 | about 4.75, with servants 11-14% of people | [P, Laslett] |
| Pelican Town (`DefaultTown`) | 2.17 (26 people in 12 households) | repository |

- Shares by size, England and Wales 2021: 1 person 30%, 2 people 34%, 3 16%, 4 13%, 5 4%, 6 2%, 7 or more 1% [P, ONS2021; Dorset2021].
- **Rule:** use 2.2-2.5 for a modern town and 4-5 for a historic or farming setting. The second halves the number of homes.

### 1.2 Residential density by distance from the centre

Homes per hectare of land, with water taken out [M]. The b and r0 columns are my fit of D(x) = D0·e^(−bx) to the first three rings [D].

| Town | 0-200 m | 200-500 m | 500-1,000 m | 1,000-1,500 m | b (per km) | r0 = 1/b |
|---|---|---|---|---|---|---|
| Mevagissey | 23.0 | 8.2 | 2.2 | 0.28 | 3.6 | 280 m |
| Hay-on-Wye | 13.9 | 4.0 | 1.5 | 0.28 | 3.3 | 300 m |
| Long Melford | 9.2 | 6.1 | 2.4 | 0.52 | 2.1 | 480 m |
| Laxton | 2.3 | 1.3 | 0.06 | 0.02 | 5.8 | 170 m |
| Bloomfield | 1.4 | 4.3 | 2.5 | 0.42 | (crater) | n/a |
| Monpazier | 18.0 | 1.8 | 0.47 | 0.17 | 5.4 | 185 m |
| Ogimachi | 6.2 | 1.4 | 0.39 | 0.18 | 4.2 | 240 m |
| Ine | 6.2 | 1.1 | 0.57 | 0.37 | 3.5 | 290 m |

- **Peak.** Nucleated and linear towns have 9-23 homes/ha within 200 m, about 20-55 people/ha [M, D].
- **Fall.** Density drops 1.5-10× from the first ring to the second: 1.5× in linear Long Melford, 10× in walled Monpazier. Beyond 500 m it is at most 2.5 homes/ha everywhere [M].
- **Grid crater.** Bloomfield's square is commercial: 1.4 homes/ha in the core and 4.3 in the next ring [M]. Large metros show the same crater [P, SpatialGrowth].
- **Published gradients are for cities, and much flatter:**
  - mean b of 0.085/km across 192 cities [P, Liotta2022];
  - Clark's cities averaged about 0.31/km [P, SpatialGrowth];
  - larger and later cities are flatter [P, Liotta2022; BertaudMalpezzi];
  - Polish towns of 30,000 or more still fit an exponential or power curve 88% of the time [P, Sleszynski2014].
- **A compact village of 300** would extrapolate to b of about 8-14/km [D]. Laxton, a real farming village, measures 5.8/km because its farmsteads string out along the street [M, D].
- **Net plot density** in six South Cambridgeshire villages: 38.4 dwellings/ha before 1914, 21.2 for 1914-2000 and 41.8 after 2000 [P, SouthCambs].
- **Footprints.** The median home covers 79-176 m² (79-104 in the UK; 176 in Bloomfield). Home footprints cover 3-20% of the land in the core ring [M].

### 1.3 The commercial core

| Town | Shops and services: 50% / 90% within | Share within 200 m | Homes: 50% within | Home median ÷ shop median |
|---|---|---|---|---|
| Mevagissey | 73 / 553 m | 75% | 399 m | 5.5 |
| Hay-on-Wye | 137 / 447 m | 75% | 517 m | 3.8 |
| Long Melford | 279 / 793 m | 35% | 583 m | 2.1 |
| Laxton | 159 / 726 m | 3 of 6 | 349 m | 2.2 |
| Bloomfield | 251 / 1,099 m | 45% | 706 m | 2.8 |
| Monpazier | 153 / 361 m | 68% | 242 m | 1.6 |
| Ogimachi | 298 / 897 m | 42% | 483 m | 1.6 |
| Ine | 353 / 1,204 m | 38% | 870 m | 2.5 |

All values are [M].

- **Commerce is more central than homes**, by a factor of 1.6-5.5 in median distance from the centre [M].
- **Nucleated towns:** half of all shops and services are within 70-155 m of the centre, and 68-75% within 200 m [M].
- **Linear and strip towns:** only 35-42% are within 200 m; the rest run along the street [M].
- **Civic services often sit on the edge.** In Bloomfield the schools, pool, clinic and sports field are 700-1,100 m out. Mevagissey's primary school is 724 m out [M].
- **Core length.** Published examples run 1 to 5 blocks, about 100-800 m [D from P]:
  - a Michigan main-street code caps the main retail stretch at 1,360 ft (415 m) [P, CNU-Michigan];
  - Centreville's district reaches at most 400 m from its central crossroads [P, Centreville].
- **Shops follow movement.**
  - The street grid's "integration" is the strongest predictor of foot traffic, and shops locate to catch it [P, Hillier1993].
  - In Bologna, retail density correlates best with street betweenness [P, Porta2009].
- **Frontage.**
  - Medieval burgage plots were about 8.5 m wide in Alnwick, and usually 2-3 perches (10-15 m) [P, Burgage].
  - Modern codes allow 25 ft (7.6 m) at minimum [P, Petaluma].
  - A 400 m street built on both sides holds 50-100 plots [D].

### 1.4 How many places to meet

Counts per 100 estimated residents [M, D].

| Town | Shops and services | Everyday places + greens |
|---|---|---|
| Mevagissey | 3.9 | 3.6 |
| Hay-on-Wye (book town) | 10.6 | 8.6 |
| Long Melford | 4.1 | 3.3 |
| Laxton (farming) | 1.9 | 2.5 |
| Bloomfield | 6.8 | 4.3 |
| Monpazier | 4.3 | 4.0 |
| Ogimachi (tourist) | 11.2 | 8.4 |
| Ine | 7.2 | 4.9 |

**From the eight towns:**
- An ordinary small town has about 4-7 shops and services and 3-5 everyday places per 100 residents. A farming village has about 2 and 2.5. Tourist places have 10-11 and 8-9 [M].
- Shopping plus food and drink make up 52-64% of establishments in Mevagissey, Hay, Long Melford, Monpazier and Ogimachi, and 33-38% in Laxton, Bloomfield and Ine [M].
- Food and drink (pub, café, restaurant) is the largest single category in Mevagissey, Monpazier, Ogimachi and Ine [M].

**Towns serve outsiders.** In Christaller's scheme a market town (Marktort) of 1,000 serves 3,500 people, an Amtsort of 2,000 serves 11,000 and a district town (Kreisstadt) of 4,000 serves 35,000 [P, Christaller]. So a small town's shops serve 3.5-9 times its own population [D].

**Thresholds:**
- A 1997 survey of 9,677 English parishes found no shop in 92% of parishes under 100 people. The share without one was 22% above 500 people, 4% above 1,000 and 1% above 3,000 [P, GhostTownBritain].
- A post office becomes likely at about 300-500 residents [P, GhostTownBritain].
- 66% of Suffolk parishes have a pub and 32% a general shop [P, Suffolk].
- Planning rules of thumb: a local shop needs about 1,500 people and a GP surgery about 4,000 [P, Basingstoke].
- Berry and Garrison ranked 52 business types by threshold population [BerryGarrison1958]. The values I recall are [VERIFY]: filling station about 196, food store 254, church 265, tavern 282.
- In US metros there is 1 business per 21.6 people, with exponent 0.98 [P, Youn2016]. That is far above village size.
- By definition a hamlet has no church and no central meeting point [P, DesigningBuildings].

### 1.5 Walking access from home

Share of homes within a straight-line distance of the nearest shop (shopping or food and drink) and of the nearest everyday place (also worship, school, hall, post office, sports, parks and greens) [M+].

| Town | Shop: 100 / 200 / 400 / 800 m | Everyday place: 100 / 200 / 400 / 800 m |
|---|---|---|
| Mevagissey | 32 / 57 / 89 / 100% | 50 / 81 / 99 / 100% |
| Hay-on-Wye | 42 / 63 / 85 / 98% | 58 / 80 / 96 / 100% |
| Long Melford | 40 / 69 / 96 / 98% | 62 / 93 / 98 / 100% |
| Laxton | 15 / 37 / 71 / 93% | 35 / 54 / 84 / 99% |
| Bloomfield | 13 / 41 / 75 / 98% | 25 / 58 / 91 / 100% |
| Monpazier | 48 / 61 / 74 / 92% | 60 / 70 / 83 / 94% |
| Ogimachi | 64 / 84 / 97 / 100% | 68 / 86 / 97 / 100% |
| Ine | 33 / 52 / 74 / 89% | 44 / 60 / 75 / 92% |

- **Everywhere,** at least 71% of homes are within 400 m (5 minutes) of a shop, and at least 75% within 400 m of an everyday place [M+].
- **The far tail** is 1-25% of homes beyond 400 m of any everyday place, and 0-8% beyond 800 m [M+]. These are edge farms, or in Ine, homes round the bay.
- **Medians:** home to nearest everyday place 60-173 m; home to nearest shop 64-255 m [M].
- **Guidance:**
  - walking to a town centre: desirable 200 m, acceptable 400 m, preferred maximum 800 m [P, CIHT2000];
  - a walkable neighbourhood has facilities within about 10 minutes (800 m) [P, MfS2007];
  - WHO: green space of at least 0.5 ha within 300 m of every home [P, WHO-green].
- **Real walks:**
  - US median walk 0.5 mi (800 m), mean 1.1 km [P, Yang2012];
  - Halifax GPS walks: 25th, 50th and 75th percentiles of 230, 480 and 860 m [P, Christie2015];
  - England's average walk was 0.8 mi in 2025 [P, NTS2025].

### 1.6 How close neighbours are

Nearest home is measured between building centres, so a terrace mapped as one building counts once [M+].

| Town | Nearest home: p10 / p50 / p90 | Other homes within 50 m (median) | Within 100 m (median) | Homes with none within 100 m |
|---|---|---|---|---|
| Mevagissey | 6 / 15 / 26 m | 10 | 42 | 0.3% |
| Hay-on-Wye | 9 / 16 / 30 m | 8 | 32 | 1.2% |
| Long Melford | 6 / 12 / 25 m | 11 | 44 | 0.2% |
| Laxton | 12 / 20 / 45 m | 5 | 16 | 2.9% |
| Bloomfield | 20 / 26 / 52 m | 3 | 15 | 2.3% |
| Monpazier | 7 / 17 / 74 m | 7 | 39 | 6.7% |
| Ogimachi | 14 / 23 / 50 m | 3 | 13 | 4.3% |
| Ine | 6 / 13 / 29 m | 7 | 17 | 0.9% |

- In UK villages and the bay strip a home has 7-11 other homes within 50 m. On grid lots and among farmsteads it has 3-5 [M+].
- Truly isolated homes are 0.2-7% [M+].
- This fits Alexander's "house cluster" of 8-12 households around common land [P, Alexander] and the Westgate courts of 7-13 houses [P, Festinger1950].

### 1.7 Streets

Public street per resident in the settled area [M]: Long Melford 5.1 m, Mevagissey 5.9, Laxton 8.9, Ine 9.6, Hay 9.9, Ogimachi 12.5, Bloomfield 17.5, Monpazier 17.7.

- UK nucleated and linear towns have 5-10 m per resident; grid and dispersed towns 12-18 m. Bloomfield has about 3 times the UK figure [M].
- UK footpaths add a lot: Long Melford has 15.1 km of footpath against 15.3 km of public street in its settled area [M].
- Grid score (Boeing's orientation order): Bloomfield 0.95; every other town 0.04-0.27 town-wide [M; method Boeing2019].
- **Share of land in streets:**
  - 18% of US county land on average, 14-30% [P, MillardBall];
  - Albert Lea 23.3%, Quincy 26.0% [P, PAS14];
  - about 21% in areas built 1990-2015 [P, LincolnStreets];
  - UN-Habitat recommends at least 30% [P, UNHabitat2014].
- **Junctions** in US urbanised areas: 18% four-way, 59% three-way, 21% dead ends [P, Boeing2017].
- **Blocks:** Portland 61 m, Melbourne 200 × 100 m, Adelaide 201 m [P, CityBlock].
- **US house lots:** the median for homes built in the 1960s or earlier is about 0.25 acre (1,000 m²) [P, CensusLots2011].

### 1.8 Land-use shares (published only; not measured)

| Use | Share of developed land [D from P] |
|---|---|
| Residential | 35-50% (Albert Lea 36.7%, Quincy 42.0% [PAS14]) |
| Streets | 20-33% (1.7 above) |
| Commercial | 2-6% (Jeffersonville, Indiana 5.4% [Jeffersonville]) |
| Parks | 5-10% (Perry: at least 10% [Perry]) |
| Civic and industrial | the rest (unconfirmed) |

A measured proxy: settled land (within 50 m of a building) is 520-1,490 m² per resident, or 7-19 residents per settled hectare [M].

### 1.9 From 30 to 300 to 3,000 people

| | 30 | 300 | 3,000 |
|---|---|---|---|
| Real examples | none measured; a hamlet | Laxton [M]; Monpazier's walled core | Mevagissey, Hay, Long Melford, Bloomfield [M] |
| Homes at 2.36 | 13 [D] | 127 [D]; Laxton 136 buildings [M] | 1,270 [D]; 856-1,277 measured [M] |
| Shops and services | 0-1 without outside customers [P, GhostTownBritain] | 6 in Laxton [M]; 12-20 at ordinary rates [D] | 100-170 [M] |
| Everyday places + greens | a green, or none [D] | 8 in Laxton [M] | 93-137 [M] |
| Commercial core | none | 90% of Laxton's civic and commercial buildings within 221 m [M] | half the shops within 70-280 m; 68-75% within 200 m if nucleated [M] |
| Half the homes within | 40-65 m of the centre [D: 13 homes at 10-25 homes/ha] | 349 m in Laxton [M] | 400-710 m [M] |
| Peak density | 21-42 dwellings/ha net [P, SouthCambs] | 2.3 homes/ha (farm street) to 18 (walled core) [M] | 9-23 homes/ha nucleated; 1.4 in the grid's square [M] |
| Gradient b | n/a (one cluster) | 5.4-5.8/km [M, D] | 2.1-3.6/km [M, D] |
| Public street | 150-300 m at 5-10 m per resident [D] | 8.9 m per resident (2.9 km) [M] | 5-18 m per resident [M] |
| Homes within 400 m of a shop | all, if a shop exists [D] | 71% (Laxton) [M+] | 75-96% [M+] |
| Second centre | no | no | sometimes: Long Melford's church, green and pubs sit about 1 km from its shops [M] |
| Christaller level | none | below the lowest | between Amtsort (2,000) and Kreisstadt (4,000) [P, Christaller] |

- **Area grows more slowly than population.** Area ∝ N^α, with α between 2/3 and 5/6, from hamlets to cities [P, Ortman2014; Cesaretti2016]. Ten times the people gives 4.6-6.8 times the area [D].
- **From 300 to 3,000, form matters more than size.** The median home distance from the centre runs 240-870 m across the measured towns, with no clear order by population [M].

## 2. Four town forms worth offering

**Shared modifier, outlying farmsteads.** Any form can have them. In real towns:
- 17% of Laxton's homes are beyond 500 m, and 50% of Ogimachi's (including the Hatogaya hamlet) [M];
- US quarter-section farms sit about 800 m apart [P, TownshipAmerica; D].

Pelican Town has 4 of its 12 homes off the lane and square (Farm, Ranch, Cottage, ScienceHouse) [repository].

### Form 1: Nucleated, with a square or green (Mevagissey, Hay; bastide variant Monpazier)

| Parameter | Value |
|---|---|
| Centre | a square where the roads in meet [P, Watabou; Christaller] |
| Shops within 200 m | 68-75%; half within 70-155 m [M] |
| Ring density | 14-23 → 4-8 → ≤2.2 homes/ha; b 3.3-3.6/km [M, D] |
| Half the homes within | 400-520 m at 1,600-2,600 people [M] |
| Streets | irregular (grid score 0.04-0.06); 6-10 m per resident [M] |
| Neighbours | nearest home 15-16 m; 8-10 homes within 50 m [M+] |

- **Green variant**, for 30-120 people: two rows of homes face a green, with crofts behind (Northumberland plan) [P, Sitelines]. Wallsend's green is 1.2 ha [P, Sitelines].
- **Bastide variant** (Monpazier):
  - a planned grid core, with 67% of core street on two axes [M];
  - 43% of homes within 200 m, at 18 homes/ha, living above or beside the shops [M];
  - almost nothing between the walls and the farms: 1.8 homes/ha at 200-500 m, and 6.7% of homes isolated [M, M+].
- **Social reading:** one strong hub. The homes on the edge are the "far" group.

### Form 2: Linear, along one road (Long Melford; Laxton as a short version)

| Parameter | Value |
|---|---|
| Test | home cloud elongation ≥ 2 (Long Melford 2.01), or ≥ 35% of homes within 60 m of one street (Laxton 38%) [M] |
| Shops by ring | 35% / 37% / 22% / 6% [M] |
| Two centres | shops on Hall Street; church, green and two pubs 0.9-1 km north [M] |
| Ring density | 9.2 → 6.1 → 2.4 homes/ha; b 2.1/km (the flattest) [M, D] |
| Access | 96% of homes within 400 m of a shop (the best of all eight); nearest home 12 m [M+] |
| Frontage | 8.5-15 m [P, Burgage] |

- **Published:**
  - row plans made up 52% of villages on the Isle of Wight's 1793 map, and the irregular two-row street was the commonest single type (22%) [P, IoW];
  - Sharp found the roadside village the commonest English type [P, Sharp1946];
  - row villages have no single focus, and their amenities are spread along the street [P, Sporle];
  - linear growth turns concentric as towns grow [P, Bimantara2022].
- **Social reading:** neighbours on two sides only. A shop end and a pub-and-church end can form two social halves.

### Form 3: Coastal strip (Ine; Mevagissey as the harbour-valley version)

| Parameter | Value |
|---|---|
| Land | water cuts the rings: Ine's hold 11 / 50 / 181 / 278 ha against 12.6 / 66 / 236 / 393 ha for full rings [M] |
| Spread | half the homes beyond 870 m; only 33% within 400 m of the centre [M, M+] |
| Neighbours | close along the shore (13 m; 7 within 50 m) but only 17 within 100 m, a line rather than a cluster [M+] |
| Shops by ring | 38% / 19% / 23% / 21% [M] |
| Access | 74% of homes within 400 m of a shop; 25% beyond 400 m of any everyday place [M+] |

- **Harbour-valley version** (Mevagissey): 75% of shops within 200 m of the harbour, and 23 homes/ha in the core [M].
- **Social reading:** one shore road carries everyone, so passing contact is high. Lingering spots spaced along it (harbour, temple) decide who forms ties. The two ends are far apart.

### Form 4: Grid (Bloomfield)

| Parameter | Value |
|---|---|
| Grid score | 0.95 town-wide, 1.00 in the core [M] |
| Square | commercial: 75 of 167 establishments within 200 m; 1.4 homes/ha [M] |
| Homes | peak at 200-500 m (4.3/ha); only 19% within 400 m of the centre [M, M+] |
| Edge | schools, pool, clinic, sports field and highway retail at 700-1,100 m [M] |
| Lots | nearest home 26 m; 3 within 50 m [M+] |
| Street | 17.5 m per resident [M] |
| Blocks | 61-201 m a side in published grids [P, CityBlock]; Bloomfield's own blocks were not measured |

- **Social reading:** the square is a destination, not anyone's home ground. Neighbours are farther apart. Civic hubs on the edge, like the school gate, pull people outward.

## 3. What layout does to contact

**Evidence strength:**
- **Strong:** randomised or natural experiment, or replicated.
- **Moderate:** one good study, or several weaker ones.
- **Weak:** small sample, conceptual, or secondhand only.

A caveat on the current sim: by the contact researcher's reading of `DefaultTown.cs`, homes are indoor rooms with no front haunts. Neighbours can share the 8-tile range only while walking past each other, so the door effects below cannot appear yet.

| # | Rule | Numbers | Strength | How the sim uses it |
|---|---|---|---|---|
| 1 | Friendship falls with door distance | Share of possible friendships made: 41% next door, 22.5% two doors away, 10% four doors away (Westgate West) [P, Festinger1950] | Strong for the direction; the figures are secondhand | Non-kin tie chance ≈ 0.4/d for d = 1-4 doors, then flat |
| 2 | Adjacency multiplies tie odds | Random seating raised mutual friendship from 15% to 22% (×1.5) in 182 classrooms (n = 2,966) [P, Rohrer2021]. Random seats predicted friendship a year later (n = 54) [P, Back2008]. Random roommates exchanged 45× more email than random classmates, still 10× as seniors [P, Marmaros2006] | Strong (randomised) | About ×1.5 on top of similarity; ties outlast the proximity |
| 3 | Clusters hold most ties | In courts of 7-13 houses, 55.5% of choices went to the chooser's own court, and 80% of residents named a friend there [P, Festinger1950] | Moderate | Lanes or courts of 8-12 homes, each one Location |
| 4 | Route overlap beats distance | Homes at the foot of stairs had more friends upstairs [P, Festinger1950]. Overlapping walking routes predicted research collaboration [P, Kabo2014] | Moderate | "Door exposure": other households' minutes walked within sight of a door |
| 5 | Free time beats duty | Co-location outside work hours says more about friendship [P, Sapiezynski]. Raw phone co-location detects friendship only about 30% better than chance [P, Malik2020] | Moderate | Weight tie-building: free time > errands > on duty |
| 6 | Fixed-time hubs build networks | Childcare centres with strict drop-off times built stronger parent networks; mothers there had at least one more good friend [P, Small2009]. Gatherings form stable cores that recur for months [P, Sekara2016] | Moderate | Hubs with fixed start times (market, service, school gate) |
| 7 | Third places | 56% had a regular local spot in 2021 (67% before the pandemic). Trust in neighbours was 76-77% in amenity-rich areas against 60-62% in amenity-poor ones [P, SurveyCenter2021]. 75% against 23% have a local hangout, by amenity level [P, WashMonthly2026]. 22% have a "local" pub, and they have more close friends [P, CAMRA2016] | Weak to moderate (surveys) | One small third place within a short walk of each cluster; small venues for strong ties |
| 8 | Lingering, not passing | Light-traffic street: 3.0 friends and 6.3 acquaintances per resident; heavy street: 0.9 friends [P, Appleyard]. Bristol: under a quarter of the local friends on busy streets [P, HartParkhurst] | Moderate (few streets) | Homes on a through road with nowhere to stop get a quarter to a third of the neighbour ties |
| 9 | Barriers | Fewer highway-crossing ties than distance predicts in all 50 US metros, strongest under 5 km; Detroit 94 against 152 expected [P, Aiello2025] | Moderate (large n, online ties) | A river or rail line with 1-2 crossings cuts short-range ties across it by 10-40% |
| 10 | Cul-de-sacs | Strong agreement that friendships with immediate neighbours run deep: just over 25% on bulb cul-de-sacs, 0% on through streets (110 homes) [P, Hochschild] | Weak to moderate | Courts give strong local ties; join them to hubs by footpath |
| 11 | Centrality brings footfall | Integration is the strongest predictor of movement; about 75% of variance in one example [P, Hillier1993] | Moderate to strong (replicated) | Put the square and shops at the most integrated node |
| 12 | Tenure | Length of residence predicts local ties more than town size or density [P, Sampson1988] | Strong (replicated) | Newcomers are the predictable left-out group |
| 13 | Social distance trades with physical | A 10% gap in home value acts like 5.6% more distance (about 150 homes) [P, HippPerrin2009] | Weak to moderate (one study) | Mix house values within a lane, so cross-class ties appear only next door |
| 14 | Rural life | Know all or most neighbours: rural 40%, urban 24%. Talk face to face weekly: 47% against 53% [P, Pew2018]. Small-town ties lean on kin [P, Fischer1982] | Moderate (survey) | Hamlets give more recognition, not more chats, and kin-heavy ties |
| 15 | Crowds of strangers | Moving to open-plan offices cut face-to-face time by 67-70% [P, BernsteinTurban2018]. Crowding went with about 39% more momentary loneliness [P, Hammoud2021] | Moderate | Keep the listener cap; lower chat odds among strangers in packed spots |
| 16 | Density alone does little | Density was unrelated to neighbour ties, but car dependence was strongly negative [P, Freeman2001]. More walkable destinations, more neighbours known [P, Leyden2003] | Moderate | Aim for places to walk to, not for density |
| 17 | The whole town is in range | Face-to-face contact tracks distance within about 5 miles [P, Mok2010] | Moderate | Distance acts through doors and minutes to hubs, not one decay curve |
| 18 | Proximity also makes enemies | [VERIFY, Ebbesen1976] | Weak | Close neighbours are also the most likely feuds |

## 4. Recommended generator

**Principle: measure, then a seeded procedure, then a check.**
- No small-town generator found learns from real data. Data-driven ones work at city scale and need heavy machinery [P, Raimbault2018; Vanegas2012].
- Vanegas et al. extract parcel attributes from real cities and compare the output statistically [P, Vanegas2012].
- Raimbault calibrates a two-parameter growth model against four summary statistics [P, Raimbault2018].
- Watabou's town generator gets a believable centre-to-edge gradient from an ordered land-use list plus a location score per use [P, Watabou].
- Here the list and the scores come from the eight towns, kept in a **FormProfile** data file with tests that pin its entries. The fitting script stays outside `sim/`.

**Inputs:**
- `seed`;
- population `N`;
- `Form`;
- `Catchment`: outside customers, 0 for a real hamlet;
- `Edges`: beach, forest, mountain, farm road;
- the `FormProfile`.

**Steps:**

1. **Scale.** Fix metres per tile (section 0). Generate in metres and convert at the end.
2. **Budget.**
   - Households: N ÷ 2.2-2.5, with sizes drawn from the ONS shares; use 4-5 per household for a historic setting.
   - Clusters: households ÷ 8-12.
   - Everyday places: 0.025-0.05 × (N + Catchment). Shops and services: 0.02-0.07 × (N + Catchment) [D from M].
   - Check: Pelican Town's 26 residents plus a catchment of about 150-250 give the roughly 10 businesses Stardew has [D; the Stardew list is VERIFY].
   - Kinds, in threshold order: green or linger spot, pub or café, church or hall, shop, then post office or school (300-500 people), then clinic [P, Suffolk; GhostTownBritain; Basingstoke].
   - Hubs: one centre until the edge is more than 10 minutes (800 m) away [P, CIHT2000]. At 300 people or fewer, always one.
3. **Skeleton, by form.**
   - Put the centre where the edge roads meet [P, Watabou].
   - Lay the spine or spines: one for Row or Linear, a shore line for Coastal, blocks for Grid.
   - Agglomerated growth joins each new cluster to the nearest point of the network by the shortest new segment [P, BarthelemyFlammini2008]. New roads attract houses, and new houses extend roads [P, Emilien2012].
4. **Lots and clusters.**
   - Frontage 8.5-15 m [P, Burgage].
   - Doors stay in street order. Nearest-door spacing is 12-26 m, or 4-9 tiles at 3 m [M+].
   - Each cluster of 8-12 homes is one outdoor lane or court Location [P, Alexander; Festinger1950].
5. **Land use from movement.**
   - Route every household's expected daily trips by shortest path, and compute through-traffic and integration [P, Hillier1993].
   - Place shops on the busiest frontage inside the core radius, in threshold order, aiming at the form's share within 200 m (section 2).
   - Fill homes outward at the form's ring densities.
   - Civic buildings go at the centre (UK forms) or the edge (Grid).
   - Outlying farmsteads go on edge roads at 300-900 m. Laxton's 90th-percentile home is 745 m out [M].
6. **Households into homes.**
   - Shopkeepers live at or above their shop (Monpazier) [M].
   - Grown children live within 1-2 clusters of their parents.
   - Mix house values within lanes, and give households a spread of how long they have lived there.
7. **Compile to sim types.**
   - A `Location` per lane, court, square and yard; a room per home; a `Link` per door.
   - `Road` Locations for connectors, with length from walking minutes.
   - A stoop `Haunt` at each door and a linger spot (bench, well, green) per cluster, weighted for free time.
   - `Gathering`s with fixed hours at the square and pub; `Job`s from the businesses.
   - Every draw uses `Rng.Hash` / `Rng.Unit` / `Rng.Range` with named parts ("layout", step, item), iterating in ordinal order. `Rng` is in `sim/UnderGlass.Sim/Rng.cs`.
8. **Check the layout** against the bands in section 5 before simulating. If a seed fails, retry with an attempt number in the hash, which keeps it deterministic.
9. **Simulate** and check the behaviour targets.

### By size

The 300 column's frontage needs 2-4 streets, as one street would be too long [D].

| | 30 | 60 | 120 | 300 |
|---|---|---|---|---|
| Households at 2.36 | 13 | 25 | 51 | 127 |
| Clusters of 8-12 | 1-2 | 2-3 | 4-6 | 11-16 |
| Forms that fit | Row, Green, nucleus plus farmsteads (Pelican) | Row, Green, Coastal | Linear, crossroads, Coastal, Nucleated | Nucleated, Linear, Coastal, Grid, bastide core |
| Nucleus radius at 10-25 homes/ha [D] | 40-65 m (14-21 tiles) | 55-90 m (19-30) | 80-125 m (27-42) | 125-200 m (42-67) |
| Two-sided frontage at 8.5-15 m | 55-100 m | 105-190 m | 215-380 m | 540-950 m |
| Public street at 5-10 m per resident | 150-300 m | 300-600 m | 0.6-1.2 km | 1.5-3 km (Laxton 2.9 km) |
| Everyday places, no catchment | 1 | 2-3 | 3-6 | 8-15 |
| Centre | a green or yard | green + pub | square + pub + hall | square, pub, church or hall, shop, school |
| Outlying homes, 300-900 m | 10-35% of homes (Pelican 33%; Laxton 17% beyond 500 m) | same | same | same |
| Walk, nucleus edge to centre | under 1 min | about 1 min | 1-1.5 min | 1.5-2.5 min; farms 4-11 min |

**The key point at 30-120 people [D].** Every home in the nucleus is within about 2 minutes of the centre, so access to shops cannot be what sets people apart. Who meets whom will come from door spacing, linger spots, fixed-time hubs, outliers and tenure.

## 5. What to measure

### Layout checks: cheap, before simulating (a `LayoutMetrics` class with tests)

| Metric | Real band |
|---|---|
| Everyday places within 200 m (2.5 min) of the centre | nucleated 68-75%; linear 35%; grid 45%; strip 38% [M] |
| Median home distance ÷ median shop distance from the centre | 1.6-5.5 [M] |
| Homes within 400 m (5 min) of a shop / of an everyday place | 71-96% / 75-99% [M+] |
| Homes beyond 400 m of any everyday place (the "far" group) | 1-25% [M+] |
| Nearest home, median / other homes within 50 m | 12-26 m / 3-11 [M+] |
| Isolated homes (none within 100 m) | 0.2-7% [M+] |
| Ring densities and gradient b, by form | section 1.2 [M] |
| Public street per resident | 5-10 m (UK nucleated and linear), 12-18 m (grid, dispersed) [M] |
| Shops on the most integrated nodes | positive rank correlation between integration and shop count [P, Hillier1993] |

### Behaviour checks: after simulating, across seeds

1. **Meetings in the centre.** The share of out-of-home co-location minutes and chats that happen in the core should be close to the form's share of everyday places in the core, within about 10 points. No published figure exists [D].
2. **Door gradient.** The non-kin tie rate at d = 1, 2 and 4 doors should be about 0.41, 0.22 and 0.10, or at least halve from one door to two [P, Festinger1950].
3. **Clusters.** At least 50% of non-kin ties fall inside the household's own cluster, and at least 80% of households have one [P, Festinger1950].
4. **Distance between friends.** Compare the walking-minute distribution between friend households with all household pairs. Non-kin friends should sit much closer; kin follow where kin were housed. Report the ratio of the medians. No village figure exists, so set the band after the first runs [D].
5. **Barriers.** At equal path distance, ties across a barrier run at 60-90% of the same-side rate [P, Aiello2025].
6. **Thoroughfares.** Homes on a through road have a quarter to a third of the neighbour ties of quiet-lane homes [P, Appleyard; HartParkhurst].
7. **Regular haunts.** 55-75% of adults near hubs have a regular out-of-home spot, against about 25% far from them [P, SurveyCenter2021; WashMonthly2026]. High isolation is about 1.7× as common far from hubs [P, SurveyCenter2019].
8. **Hub peaks.** Most of a hub's use falls in its peak window; Whyte found about 80% of plaza use between 12:00 and 14:00 [P, Whyte1980].
9. **Left out.** The bottom decile of meeting potential should be mostly far-tail homes and newcomers. Ties should rise with months in town [P, Sampson1988].
10. **Context.** Free-time co-location should predict ties better than on-duty co-location [P, Sapiezynski].
11. **Hamlets.** Hamlet households recognise more of their neighbours but chat no more, and their ties are mostly kin [P, Pew2018; Fischer1982].
12. **Growth, 60 → 120 → 300.** Contacts per person should rise modestly while clustering stays roughly flat. City data show contacts ∝ N^1.12 with clustering about 0.25 [P, Schlapfer2014]. Use the direction only.

## Gaps and things to verify

- No real place under 250 people was measured. Every number for 30-120 people is derived.
- Town populations come from search summaries. Laxton's 251 (2021) conflicts with a blog's 489 (2011).
- Published figures were seen only in search-result text.
- **Not yet measured:**
  - walking-route distances and circuity;
  - block sizes in Bloomfield and Monpazier;
  - junction mix;
  - street betweenness at shop sites;
  - land-use shares.

  All can be measured from the same Overture extracts.
- **[VERIFY]:**
  - Berry and Garrison's threshold values;
  - the citation for Clark's law;
  - Bartholomew's land-use shares;
  - Ebbesen et al.'s finding that proximity also breeds enemies;
  - Stardew facts: a tile is about 1 m, the Town map size, and the business list;
  - the title of Louf and Barthelemy's arXiv 1309.3961.
- The tile scale (section 0) is Sid's decision.

## Sources

- [Overture] Overture Maps Foundation, release 2026-09-23.1. OSM-derived layers are under the ODbL licence. https://overturemaps-us-west-2.s3.us-west-2.amazonaws.com/release/2026-09-23.1/
- [Pop-Mevagissey] "Mevagissey", Wikipedia. https://en.wikipedia.org/wiki/Mevagissey
- [Pop-Hay] City Population, "Hay". https://citypopulation.de/en/uk/wales/admin/powys/W04000281__hay ; City Population, "Hay-on-Wye". https://www.citypopulation.de/en/uk/wales/powys/K08000009__hay_on_wye ; censusdata.uk, Hay ward TS063. https://www.censusdata.uk/w05001135-hay/ts063-occupation
- [Pop-LongMelford] Wikidata Q1016818. https://www.wikidata.org/wiki/Q1016818 ; Babergh, Long Melford neighbourhood plan appendices. https://babergh.gov.uk/documents/d/babergh/long-melford-np-submission-draft-appendices
- [Pop-Laxton] "Laxton and Moorhouse", Wikipedia. https://en.wikipedia.org/wiki/Laxton_and_Moorhouse
- [Pop-Bloomfield] "Bloomfield, Iowa", Wikipedia. https://en.wikipedia.org/wiki/Bloomfield,_Iowa
- [Pop-Monpazier] "Monpazier", Wikipedia. https://en.wikipedia.org/wiki/Monpazier ; INSEE, commune 24280. https://www.insee.fr/fr/statistiques/8643952?geo=COM-24280
- [Pop-Ogimachi] Gifu Prefecture. https://www.pref.gifu.lg.jp/uploaded/attachment/7406.pdf ; seijiyama, Shirakawa. https://seijiyama.jp/lgov/21/216046/
- [Pop-Ine] seijiyama, Ine. https://seijiyama.jp/lgov/26/264636/
- [ONS2021] ONS, "Household and resident characteristics, England and Wales: Census 2021". https://www.ons.gov.uk/peoplepopulationandcommunity/householdcharacteristics/homeinternetandsocialmediausage/bulletins/householdandresidentcharacteristicsenglandandwales/census2021
- [Dorset2021] Dorset Council, "Census 2021: households". https://www.dorsetcouncil.gov.uk/census-2021/census-2021-households
- [INSEE] INSEE, household size. https://www.insee.fr/fr/statistiques/7666831
- [USCensus2023] US Census Bureau, "Family households still the majority". https://census.gov/library/stories/2023/05/family-households-still-the-majority.html
- [Laslett] Pre-industrial English household size. https://ugp.rug.nl/ha/article/download/2089/2081/2116
- [SpatialGrowth] "Modeling the spatial growth of cities", arXiv 2510.03045. https://arxiv.org/pdf/2510.03045
- [Liotta2022] Liotta, Viguié, Lepetit, "Testing the monocentric standard urban model in a global sample of cities". https://arxiv.org/pdf/2111.02112
- [BertaudMalpezzi] Bertaud and Malpezzi, "The Spatial Distribution of Population in 48 World Cities". https://www2.lawrence.edu/fast/finklerm/Complete%20Spatial%20Distribution%20of%20Population%20in%2050%20World%20Ci.pdf
- [Sleszynski2014] Śleszyński, "Distribution of population density in Polish towns and cities". https://geographiapolonica.pl/article/item/9410.html
- [Ortman2014] Ortman et al., "The Pre-History of Urban Scaling". https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3922752/
- [Cesaretti2016] Cesaretti et al., "Population-Area Relationship for Medieval European Cities". https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0162678
- [SouthCambs] South Cambridgeshire, "High quality homes audit trail". https://Scambs.gov.uk/media/7741/chapter-7-high-quality-homes-audit-trail.pdf
- [Christaller] P. Hall seminar. https://globalurban.org/hall_seminar.htm ; Masaryk University notes. https://is.muni.cz/el/1431/podzim2004/Z3090/um/220638/HGpr5www.pdf
- [GhostTownBritain] "Ghost Town Britain" chapter. https://enterprise.gov.ie/en/publications/publication-files/chapter-ten-ghost-town-britain.pdf
- [Suffolk] Suffolk ACRE, Suffolk services review. https://www.rsnonline.org.uk/executive-summary-of-the-suffolk-services-review-2102-suffolk-acre-ltd
- [Basingstoke] Basingstoke threshold guide. https://basp.basingstoke.gov.uk/content/doclib/1249.pdf
- [BerryGarrison1958] "The Functional Bases of the Central Place Hierarchy". https://www.tandfonline.com/doi/abs/10.2307/142299
- [Youn2016] Youn et al., "Scaling and universality in urban economic diversification". https://pmc.ncbi.nlm.nih.gov/articles/PMC4759798/
- [DesigningBuildings] "Hamlet". https://designingbuildings.co.uk/wiki/Hamlet
- [Hillier1993] Hillier et al., "Natural movement". https://discovery.ucl.ac.uk/id/eprint/1398/ ; UCL chapter. https://discovery-pp.ucl.ac.uk/id/eprint/4384/8/4384.ch6.pdf
- [Porta2009] Porta et al., "Street centrality and densities of retail and services in Bologna". https://webspace.maths.qmul.ac.uk/v.latora/porta_bologna_450_epb09.pdf
- [CNU-Michigan] CNU, Michigan Main Street Corridor District. https://www.cnu.org/sites/default/files/ProjectForCodeReform.Michigan.MainStreetCorridorDistrict.pdf
- [Centreville] Town of Centreville retail study. https://www.townofcentreville.org/node/2497
- [Burgage] Alnwick Civic Society, "Rods, poles and perches". https://alnwickcivicsociety.org.uk/2020/09/11/rods-poles-and-perches/ ; Wiltshire Community History. https://apps.wiltshire.gov.uk/communityhistory/Question/Details/216
- [Petaluma] Petaluma SmartCode 4.80.140. https://petaluma.municipal.codes/SmartCode/4.80.140
- [IoW] Isle of Wight rural settlement assessment. https://www.iow.gov.uk/documentlibrary/download/rural-20settlementheap-20131
- [Sharp1946] Sharp, *The Anatomy of the Village*. https://researcharchive.ncl.ac.uk/sharp/galrev/anatomy.html
- [Sporle] Sporle with Palgrave views assessment. https://sporle-pc.gov.uk/wp-content/uploads/2026/07/Sporle-with-Palgrave-Views-Assessment-for-Reg-14.pdf
- [Bimantara2022] Bimantara, Roychansyah, Ogawa. https://journal.ugm.ac.id/v3/BEST/article/view/3541
- [Sitelines] Newcastle Sitelines, Wallsend green. https://sitelines.newcastle.gov.uk/SMR/12124
- [MillardBall] Millard-Ball, "The width and value of residential streets". https://www.its.ucla.edu/publication/width-and-value-of-residential-streets/
- [PAS14] Planning Advisory Service, "Urban Land Use". https://www.planning.org/pas/reports/report14/
- [LincolnStreets] Lincoln Institute, "The State of Streets". https://www.lincolninst.edu/publications/working-papers/state-streets/
- [UNHabitat2014] UN-Habitat, "A New Strategy of Sustainable Neighbourhood Planning: Five Principles". https://unhabitat.org/sites/default/files/download-manager-files/A%20New%20Strategy%20of%20Sustainable%20Neighbourhood%20Planning%20Five%20principles.pdf
- [Boeing2017] Boeing, "A Multi-Scale Analysis of 27,000 Urban Street Networks". https://arxiv.org/pdf/1705.02198
- [Boeing2019] Boeing, "Urban spatial order", Applied Network Science 4:67. https://doi.org/10.1007/s41109-019-0189-1
- [CityBlock] "City block", Wikipedia (seen only in search text). https://en.wikipedia.org/wiki/City_block
- [CensusLots2011] US Census working paper SEHSD-WP2011-18. https://www.census.gov/content/dam/Census/library/working-papers/2011/demo/SEHSD-WP2011-18.pdf
- [Jeffersonville] Indiana Academy of Science. https://journals.indianapolis.iu.edu/index.php/ias/article/download/6214/6177/12282
- [Perry] designboom, "Clarence Perry's neighborhood unit". https://www.designboom.com/architecture/clarence-perry-neighborhood-unit-15-minute-city/
- [CIHT2000] CIHT, "Guidelines for Providing for Journeys on Foot", via WYG, "How far do people walk?". https://rapleys.com/wp-content/uploads/2020/10/CD3.38-WYG_how-far-do-people-walk.pdf
- [MfS2007] Manual for Streets, via a Coventry technical note. https://www.coventry.gov.uk/downloads/file/45269/t23570-hub-highways-tn1-redacted
- [WHO-green] ISGlobal ranking (WHO 300 m guideline). https://www.isglobal.org/en/-/isglobal-presenta-el-ranking-de-las-ciudades-europeas-con-mayor-mortalidad-atribuible-a-la-falta-de-espacios-verdes
- [Yang2012] Yang and Diez-Roux, AJPM 43(1). https://pmc.ncbi.nlm.nih.gov/articles/PMC3377942
- [Christie2015] Christie et al., STRC 2015. https://strc.ch/2015/Christie_EtAl.pdf
- [NTS2025] DfT, National Travel Survey 2025: active travel. https://www.gov.uk/government/statistics/national-travel-survey-2025/nts-2025-active-travel
- [WalkSpeed] Istanbul walking-speed study, IJBES. https://ijbes.utm.my/index.php/ijbes/article/view/1343
- [Gehl] Happy Cities. https://happycities.com/blog/how-to-create-vibrant-streets-focus-on-what-people-can-see ; Planetizen. https://planetizen.com/node/125446
- [Alexander] *A Pattern Language*, pattern 37. https://www.patternlanguage.com/apl/apl37/apl37.htm ; pattern 14. https://urbigenous.net/a-pattern-language/pattern-014.html
- [TownshipAmerica] "Quarter section". https://townshipamerica.com/glossary/quarter-section
- [Festinger1950] Festinger, Schachter, Back, *Social Pressures in Informal Groups*. https://archive.org/details/socialpressuresi00fest ; figures via Merrill. https://livingwholly.substack.com/p/on-waving-from-the-front-porch ; and "Propinquity". https://en.wikipedia.org/wiki/Propinquity
- [Rohrer2021] Rohrer, Keller, Elwert, "Proximity can induce diverse friendships". https://pmc.ncbi.nlm.nih.gov/articles/PMC8357142
- [Back2008] "Becoming friends by chance", ScienceDaily. https://www.sciencedaily.com/releases/2008/06/080602163842.htm
- [Marmaros2006] Marmaros and Sacerdote, "How Do Friendships Form?". https://www.nber.org/papers/w11530
- [HippPerrin2009] Hipp and Perrin. https://escholarship.org/uc/item/4tp2801z
- [Kabo2014] Kabo et al., Research Policy 43. https://ideas.repec.org/a/eee/respol/v43y2014i9p1469-1485.html
- [BernsteinTurban2018] Harvard DASH. https://dash.harvard.edu/entities/publication/890a4d8f-e65b-4c5e-a9d9-884fb1526fab
- [Malik2020] "Can Smartphone Co-locations Detect Friendship?". https://arxiv.org/abs/2008.02919v2
- [Sapiezynski] "Offline Behaviors of Online Friends". https://arxiv.org/pdf/1811.03153
- [Sekara2016] "Fundamental structures of dynamic social networks". https://pmc.ncbi.nlm.nih.gov/articles/PMC5018769
- [SurveyCenter2021] "Public Places and Commercial Spaces". https://www.americansurveycenter.org/research/public-places-and-commercial-spaces-how-neighborhood-amenities-foster-trust-and-connection-in-american-communities/
- [SurveyCenter2019] "The importance of place". https://www.americansurveycenter.org/research/the-importance-of-place-neighborhood-amenities-as-a-source-of-social-connection-and-trust/
- [WashMonthly2026] "The shrinking space between home and work". https://washingtonmonthly.com/2026/01/30/the-shrinking-space-between-home-and-work/
- [CAMRA2016] CAMRA, "Pubs and wellbeing". https://camra.org.uk/index.php/about/pubs-and-wellbeing
- [Small2009] UChicago News, "Child care pays unexpected dividends". https://news.uchicago.edu/story/child-care-pays-unexpected-dividends-parents
- [Whyte1980] "The Social Life of Small Urban Spaces". https://en.wikipedia.org/wiki/The_Social_Life_of_Small_Urban_Spaces
- [Appleyard] PPS, "Donald Appleyard". https://www.pps.org/article/dappleyard
- [HartParkhurst] Hart and Parkhurst, "Driven to excess". https://uwe-repository.worktribe.com/output/968892
- [Aiello2025] Aiello et al., "Urban highways are barriers to social ties". https://arxiv.org/abs/2404.11596v3
- [Hochschild] Hochschild, "The Cul-de-sac Effect". https://ascelibrary.com/doi/10.1061/%28ASCE%29UP.1943-5444.0000192
- [Pew2018] Pew, "How urban, suburban and rural residents interact with their neighbors". https://www.pewresearch.org/social-trends/2018/05/22/how-urban-suburban-and-rural-residents-interact-with-their-neighbors/
- [Fischer1982] ICPSR 7744. https://www.icpsr.umich.edu/web/ICPSR/studies/7744
- [Hammoud2021] "Lonely in a crowd". https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8688521/
- [Freeman2001] Freeman, "The Effects of Sprawl on Neighborhood Social Ties". https://www.tandfonline.com/doi/abs/10.1080/01944360108976356
- [Leyden2003] Leyden, "Social Capital and the Built Environment". https://pmc.ncbi.nlm.nih.gov/articles/PMC1448008/
- [Sampson1988] Sampson (doi:10.2307/2095822). https://sampson.scholars.harvard.edu/filter_by/social-disorganization
- [Mok2010] Mok, Wellman, Carrasco, "Does Distance Matter in the Age of the Internet?". https://ideas.repec.org/a/sae/urbstu/v47y2010i13p2747-2783.html
- [Schlapfer2014] Schläpfer et al., "The scaling of human interactions with city size". https://pmc.ncbi.nlm.nih.gov/articles/PMC4233681
- [Ebbesen1976] Southampton lecture 4. https://www.southampton.ac.uk/~crsi/lecture4.htm
- [Watabou] TownGeneratorOS source. https://raw.githubusercontent.com/watabou/TownGeneratorOS/master/Source/com/watabou/towngenerator/building/Model.hx ; Village Generator devlogs. https://watabou.itch.io/village-generator
- [Emilien2012] Emilien et al., "Procedural generation of villages on arbitrary terrains". https://hal.archives-ouvertes.fr/hal-00694525
- [BarthelemyFlammini2008] "Modeling urban street patterns". https://arxiv.org/abs/0708.4360
- [Vanegas2012] "Procedural generation of parcels in urban modeling". https://diglib.eg.org/handle/10.1111/v31i2pp681-690
- [Raimbault2018] "Calibration of a density-based model of urban morphogenesis". https://pmc.ncbi.nlm.nih.gov/articles/PMC6126859/
