# 21. Temperament: seed personality values per character

**Status: partial.** Proposed to Sid on 2026-09-30. The method, the tool and a draft table are
built; nothing in the mod reads the table yet. The motives redesign with Sid on 2026-10-01 gives
the traits their final roles (below, "How the motives use the traits") and adds a seventh,
hand-set value, `retention`; both wait on the motives work. Today
each character's personality reaches decisions
only as the three game traits in words on the NPC card ([laya.md](laya.md), "The NPC card") and a
one-line voice (`VoiceSheets`). The motives ([motives.md](motives.md)) use the same factors for
everyone, so Shane forgives as fast as Emily. This spec gives every villager six numbers, derived
from the game's own data by a repeatable method, that the motives, ladder and card can use, plus six
emotion biases after Paul Ekman's basic emotions (Sid, 2026-10-01) for how each one tends to feel.

## Player-visible behavior

None by itself. Once wired (later steps, below): characters differ in how fast they miss the
player, how easily they are hurt, how long they hold it, how much they talk, how curious they are
about the newcomer, and whether they come in person or write. Shane takes a forgotten birthday hard
and keeps it; Emily lets it go. Robin brings up the news; Linus mostly doesn't.

## The traits

All 0..1; 0.5 is the town's typical villager (the scale is relative to the vanilla cast).

| Trait | Meaning | What it will drive |
|---|---|---|
| `warmth` | how much they like company and miss the player | `MissingYou` and `Greeting` strength ([motives.md](motives.md)) |
| `sensitivity` | how easily they are hurt | the **amplitude**: every stress's magnitude, and how far the daily mood roll moves the outlook ([motives.md](motives.md)) |
| `forgiveness` | how fast hurt fades | `GrudgeDailyDecay`, the fallback for "would <npc> hold this against the player?" |
| `chattiness` | how much they talk and pass news on | `News` strength, gossip spread chance ([ledger-gossip.md](ledger-gossip.md)), [town-life.md](town-life.md) chats |
| `curiosity` | interest in the newcomer and in what others do | `Curious` strength ([newcomer-week.md](newcomer-week.md)) |
| `boldness` | in person rather than from a distance | the **expression**: the base of effective boldness, which must reach an act's cost ([motives.md](motives.md), "The act rule"); low boldness ends up at letters and queued lines, high at walking up and visits |

Six, not more: each one maps to a factor a spec already has. A trait nothing reads would only be
noise to tune.

## Emotion biases (Ekman)

Added at Sid's request on 2026-10-01. The behaviour traits say what a character *does*; the emotion
biases say how they tend to *feel* about the same event, after Ekman's six basic emotions. A
forgotten birthday makes Shane sad (sadness 0.76) and Haley cross (anger 0.77). Same 0..1 scale,
0.5 typical.

| Emotion | Signal | Will drive |
|---|---|---|
| `anger` | `$a` portraits + anger words ("angry", "annoyed", "ugh") | how `Hurt` shows: anger-leaning characters go cold or short ([motives.md](motives.md)); the line's tone bucket and portrait ([text.md](text.md)) |
| `sadness` | `$s` portraits + sad words ("lonely", "sigh") | how `Hurt` shows: sadness-leaning characters write a sad note or go quiet |
| `happiness` | `$h` portraits + happy words ("glad", "wonderful") | how `Grateful` shows (comes to say thanks), the happy line variant |
| `fear` | fear words only ("scared", "nervous", "worried") | `Worried` strength (storms, the mines) |
| `disgust` | disgust words only ("gross", "yuck") | reaction to hated gifts (line tone) |
| `surprise` | surprise words only ("wow", "whoa", "can't believe") | reaction to big news (line tone) |

The game has no fear, disgust or surprise portrait, so those three rest on a handful of words per
character (2 to 5 hits for the strongest). They move half as far (`WordsOnlyDamping` 0.5, so at most
0.15 from dialogue) and are starred in the review table as weaker numbers. Game-trait offsets:
Polite -0.05 anger, Rude +0.05; Outgoing -0.05 fear, Shy +0.05; Positive +0.05 happiness and -0.05
sadness, Negative the reverse; Child +0.05 surprise. `$l` (love) and `$u` (unique) portraits are
not Ekman emotions; love feeds warmth, unique is ignored.

The emotion is a tilt on how a reaction looks, never whether it happens: which motive wins stays
with the motive strengths and the model's choice ([motives.md](motives.md)).

## How the motives use the traits

Agreed with Sid on 2026-10-01 ([motives.md](motives.md), D24). The morning draft's resting urge
(`0.5 + 0.6 x (0.4 boldness + 0.3 warmth + 0.3 chattiness - 0.5)`) is dropped: on the draft table
it put every villager between 0.35 and 0.64, all past the ladder's first threshold, and it counted
boldness twice.

- **boldness** is the base of effective boldness: `boldness + familiarity + 0.5 x intensity` must
  reach the act's cost. Shane (0.16) needs to know someone, or feel strongly; Pam (0.57) greets
  acquaintances readily.
- **sensitivity** scales every stress (`x (0.5 + sensitivity)`) and how much the daily roll moves
  the outlook.
- **warmth, chattiness, curiosity** scale the motives they name; **forgiveness** sets how fast
  negative regard heals.
- **Emotion biases** skew the daily mood roll (`happiness - (anger + sadness) / 2`) and pick how a
  feeling shows; the fear bias scales `Worried`.

### `retention`: how much a character keeps

Sid, 2026-10-01: what goes into long-term memory depends on the person; Pam, a drinker, forgets most
slights that Robin would keep, unless the slight is terrible. `retention` (0..1, 0.5 typical) scales
how much of each plastic stress reaches regard (`0.5 + retention`); severe stresses ignore it
([motives.md](motives.md), "Regard").

The dialogue gives no clear signal for it, so it is **hand-set**, not derived: 0.5 for everyone,
with values in `temperament-overrides.json` for characters whose writing makes it obvious (Pam
low, first). The extractor's overrides step must accept the new key; until it does, the mod reads
the default. This is the one temperament value the method doesn't produce, so the table marks it
as hand-set.

## Inputs (what the game gives us)

1. **`Data/Characters`** (confirmed in the 1.6.15 decompile, `CharacterData`): `Manner`
   (Neutral/Polite/Rude), `SocialAnxiety` (Outgoing/Shy/Neutral), `Optimism`
   (Positive/Negative/Neutral), `Age` (Adult/Teen/Child). Also there but not used here:
   `FriendsAndFamily`, `HomeRegion`, `CanBeRomanced`, `LoveInterest`, birthday, spouse settings.
   Committed as `fixtures/game/temperament/characters.json` (34 villagers: every character with a
   birthday).
2. **`Characters/Dialogue/<name>`** (English base files only): every line a villager says
   outside events, keyed by day, season, hearts and occasion. Each value is split into pages the
   player reads: `#` separates segments, `$b`/`$e` break pages, `$h $s $u $l $a` (or `$1..$5`) set
   the portrait, `^` separates the male and female variants (first kept), `@` is the player. The
   segment after `$r` is the player's answer and is skipped (confirmed in
   `Dialogue.parseDialogueString`). Not committed; the tool reads the unpacked files.
3. **`Data/NPCGiftTastes`**: the five gift reaction lines (love, like, dislike, hate, neutral) are
   added to the character's pages.

Not used: event scripts and `Strings/` (lines there are not keyed by speaker in one place), and
`MarriageDialogue*` (spouse-only lines would tilt the romanceable characters).

## Method (deterministic)

Code: `src/NpcTemperament` (pure, tested), CLI `tools/TemperamentExtractor`.

1. **Features** (`DialogueFeatures`): for each character, the share of pages with a happy, sad,
   angry or love portrait; with `?`, `!` or `...`; with thanks words, sorry words, welcome words
   ("good to see you", "stop by"), dismissive words ("leave me alone", "why are you talking");
   gossip (rumor words, or another villager's name, matched case-sensitively); and mean words per
   page.
2. **Game-trait offsets** (`TemperamentScorer.TraitOffsets`): added to 0.5.

   | Trait | Offsets |
   |---|---|
   | warmth | Polite +0.05, Rude -0.05; Positive +0.05, Negative -0.05 |
   | sensitivity | Outgoing -0.05, Shy +0.05; Positive -0.05, Negative +0.05 |
   | forgiveness | Polite +0.08, Rude -0.08; Positive +0.04, Negative -0.04 |
   | chattiness | Outgoing +0.10, Shy -0.10 |
   | curiosity | Outgoing +0.05, Shy -0.05; Child +0.05 |
   | boldness | Outgoing +0.15, Shy -0.15 |
   | emotions | see "Emotion biases" above |

3. **Dialogue part** (`TemperamentScorer.Recipes`): each feature is turned into a z-score against
   all characters in the run (clamped to +-2), signed, averaged per trait, times `Spread` (0.15). So
   dialogue moves a trait at most 0.3 either way.

   | Trait | Raised by | Lowered by |
   |---|---|---|
   | warmth | happy, love, thanks, welcome | dismiss, angry |
   | sensitivity | sad, sorry, `...` | happy |
   | forgiveness | thanks, happy | angry, dismiss |
   | chattiness | words per page, gossip, `!` | `...` |
   | curiosity | `?`, gossip | - |
   | boldness | `!` | `...`, sorry |
   | anger / sadness / happiness | their portrait + their words | - |
   | fear / disgust / surprise | their words (half spread) | - |

4. Seed = clamp(0.5 + offsets + dialogue part, 0, 1), rounded to 2 places. Characters with fewer
   than `MinPages` (20) pages get the offsets only. Characters are processed in name order, files
   in key order; the same inputs always give the same bytes.
5. **Overrides** last: `fixtures/game/temperament/temperament-overrides.json`, shape
   `{ "Shane": { "forgiveness": 0.3 } }`, any subset of traits, clamped. Hand edits live there so a
   regeneration never loses them. Unknown trait names fail the run.

Outputs: `temperament.json` (the table the mod will read) and `temperament.md` (the table with each
cell's game-trait-only value beside it, plus every feature, for review).

**Why counting, not a model.** The numbers must be reproducible and explainable in a PR, and the
model never writes or reads free text here ([decisions.md](../decisions.md), D22). A reviewer can
see that Shane's low forgiveness comes from "Why are you talking to me?"-type lines (8% dismissive,
the highest in town), not from a black box.

## Reproducing the table

Needs the game, Node and [xnbcli](https://github.com/LeonBlade/xnbcli) (as in `README.md`, "Getting
the real schedule files").

```bash
# 1. dialogue and gift tastes (English base files only, not *.de-DE.xnb etc.)
node xnbcli.js unpack "<game>/Content/Characters/Dialogue" dialogue
node xnbcli.js unpack "<game>/Content/Data/NPCGiftTastes.xnb" dialogue
# 2. Data/Characters: xnbcli can't read the typed asset, so dump its decompressed bytes. In
#    xnbcli's app/Xnb/index.js, just before the "Reading from byte position" log line, add:
#    if (process.env.XNB_DUMP) require("fs").writeFileSync(process.env.XNB_DUMP, this.buffer.buffer);
XNB_DUMP=Characters.bin node xnbcli.js unpack "<game>/Content/Data/Characters.xnb" scratch
python tools/TemperamentExtractor/character_traits.py Characters.bin fixtures/game/temperament/characters.json
# 3. the table
dotnet run --project tools/TemperamentExtractor -- --dialogue dialogue \
  --characters fixtures/game/temperament/characters.json --gift-tastes dialogue/NPCGiftTastes.json \
  --overrides fixtures/game/temperament/temperament-overrides.json --out fixtures/game/temperament
```

Step 2 fails loudly if a game update changes the `CharacterData` field order (it checks each
display name it decodes). Redo all three after a game update.

## Data model and where it will live

`Temperament` record (six behaviour traits then six emotions, positional, new fields only at the end; `Temperament.Neutral` = all 0.5 for any character not in the
table, such as a modded NPC), `TemperamentTable` (load, overrides, JSON). Not saved per save: it is
data, like `regions.json`, so edits apply to old saves (`AGENTS.md`, "Tuning is not saved").

When wired: ship `temperament.json` as `data/temperament.json` beside `regions.json`, and expose it
as a custom asset `Mods/squid.StardewNpcMod/Temperament` loaded through `AssetRequested`, so Content
Patcher packs can add or edit characters (the content-packs note in [references.md](references.md)).
Custom NPCs without a row could get a value from their `Data/Characters` fields alone
(`TemperamentScorer.TraitOffsets` + 0.5), computed in the mod at load.

## Laya questions

None new. When wired, the NPC card's `temperament:` line gains plain words for the strongest traits
("quick to forgive, talkative"), still within the card's ~60-token budget ([laya.md](laya.md)).
Numbers never go into the state. The strongest emotion bias can add one word ("quick to anger").

## Deterministic rules (when wired)

Each trait scales one factor by `0.5 + trait` (so 0.5 leaves it unchanged, 0 halves it, 1 makes it
1.5x): `MissingYou` growth by warmth; each `Hurt` entry's weight by sensitivity; the grudge's daily
decay by forgiveness (more forgiving = faster decay), and the "hold it against" fallback becomes
`1 - forgiveness` instead of 0.5; `News` weight by chattiness; `Curious` by curiosity. The ladder's
step choice adds `(boldness - 0.5) x BoldnessStepBias` to in-person steps and subtracts it from
distant ones. The temperament floor in [ladder.md](ladder.md) (shy never uses `Bubble` or
`ForcedDialogue`) stays as written: it is a hard rule, these are tilts.

## Tuning constants

`TemperamentScorer.Spread` 0.15, `WordsOnlyDamping` 0.5, `MinPages` 20, the z clamp 2, the offsets above, and the word
lists in `DialogueFeatures`. Changing any of them means regenerating the table in the same PR.
Planned: `MotiveOptions.TemperamentWeight` (1.0; 0 turns temperament off), `BoldnessStepBias`.

## Acceptance tests

Done (`tests/NpcTemperament.Tests`, 23): page splitting, portraits, gender variants, tokens, skipped
`$q`/`$r` segments; feature rates and case-sensitive names; game traits alone when features are
equal or pages are few; angrier dialogue lowers forgiveness; output independent of input order and
in range; overrides apply last, clamp, reject unknown traits; JSON round trip; the committed table
covers all 34 characters and keeps known orderings (Shane forgives less than Emily, Penny is less
bold than Robin); emotion words counted; portrait and words both raise an emotion; words-only
emotions move half as far; the committed table's emotion orderings (Shane sadder than Robin, Robin
happier than Shane, Haley and Sebastian angrier than Emily).

When wired: motive tests with two fixture characters at opposite temperaments; in-game, stand up
Shane and Emily on the same day and see Shane's grudge outlast Emily's in the shadow log.

## Status

Partial. Built: `src/NpcTemperament`, `tools/TemperamentExtractor` (+ `character_traits.py`), the
draft table in `fixtures/game/temperament/`. Shipped and read for display only: the mod build copies
`temperament.json` and `temperament-overrides.json` from `fixtures/game/temperament/` into the mod
folder, loads them at `Entry` (overrides last), and the NPC Minds viewer shows each character's
values ([debug-tools.md](debug-tools.md)); no decision reads them. Not started: moving the file to
`data/temperament.json` and the custom asset, the card words, and the motive and ladder factors
(with [motives.md](motives.md), roadmap step 14).

## Open questions

- Do the draft numbers match how Sid reads these characters? The review table shows where the
  dialogue moved each one; disagreements go in the overrides file. Known odd spots: Haley's
  forgiveness (0.30) is lower than her later-hearts arc suggests, because early lines are curt;
  Dwarf and Wizard have no portrait codes, so their mood features are all zero.
- Should hearts change temperament over time (Shane softening at 8 hearts)? Recommendation: no;
  hearts already enter every motive. Keep temperament fixed.
- Weighting lines by hearts level (keys like `Mon6`) or season. Recommendation: not in v1.
