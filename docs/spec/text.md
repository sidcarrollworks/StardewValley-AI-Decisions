# 10. Text generation and sanitizing

**Status: partial.** Three first-person templates, place display names and one sanitizer exist.
Template banks, tone variants, per-channel sanitizers and length limits are not started. D12, D20;
architecture, "Overnight intents" (`LineRenderer`).

Where the lines live follows SMAPI's translation system (stardewvalleywiki.com/Modding:Modder_Guide/APIs/Translation,
read 2026-09-30; marked **wiki** below), which Sid's Get Started link points to. That makes the
lines ordinary SMAPI translation files: familiar to Stardew modders, editable without rebuilding
(D20), and translatable later without a format change.

**Settled:** the model never writes text (`AGENTS.md`, rule 6; D12). The brief's older option of "a
short sentence from an LLM overnight" is not planned. Every visible string is a template filled with
values the mod controls.

## Player-visible behavior

Stardew has no voice acting: every "line" in this spec is on-screen text, in a dialogue box, a
bubble over a head, or a letter. Where a spec says an NPC "says" something, it means text the player
reads.

Lines that sound a little different per NPC and per occasion, never contain internal names
(`SeedShop`), never repeat day after day, and never run a game command. Four channels:

| Channel | Where the player sees it | Max length |
|---|---|---|
| Dialogue | a dialogue box (queued lines, intents, forced dialogue) | 160 chars, a style target: the box splits text into pages by pixel height, so a longer line becomes a second page rather than being cut |
| Bubble | `showTextAboveHead` | 40 chars |
| Mail | a letter | 400 chars |
| Log | the SMAPI log (shadow) | none |

## Data model

- **`i18n/default.json`** in the mod folder (new). The format is fixed by SMAPI (**wiki**): a flat
  key-to-string map, keys case-insensitive using letters, digits, `_`, `-` and `.`, JavaScript
  comments allowed, tokens written `{{name}}`. Other languages go in `i18n/<locale>.json` (`fr.json`,
  `de.json`, ...), and a key missing there falls back to `default.json` automatically (**wiki**).
  Because the values are flat strings, variants and buckets are spelled out in the key:
  ```js
  {
    // <channel>.<kind>[.<variant>].<bucket>.<n>; bucket = an NPC (optionally .low/.high hearts),
    // a tone, or "any". n counts from 1 with no gaps.
    "line.GiftReceived.Love.Haley.high.1": "You remembered I love {{name}}! It's on my vanity now.",
    "line.GiftReceived.Love.polite.1": "I wanted to thank you properly for the {{name}} {{when}}.",
    "line.GiftReceived.Love.any.1": "Thank you again for the {{name}} {{when}}. I love it.",
    "line.Saw.Player.any.1": "I saw you at {{place}} {{when}}.",
    "bubble.Greet.any.1": "Hey!",
    "bubble.Greet.any.2": "Oh, hi!",
    "mail.MissedYou.any.1": "Dear @,^I was hoping to run into you {{when}}...^-{{npc}}"
  }
  ```
  Lookup order for a speaker: its own hearts bucket (`Haley.high`), its own bucket (`Haley`), its
  tone bucket, then `any`; within the first bucket that has `.1`, the variants are `.1`, `.2`, ...
  up to the first gap. Tone buckets come from `Data/Characters` (`Manner`: Neutral, Polite, Rude;
  `Optimism`: Positive, Negative, Neutral; `SocialAnxiety`: Outgoing, Shy, Neutral; read with
  `npc.GetData()`) and exist for custom NPCs from other mods.
- **Keeping `src/` game-independent.** The renderer in `src/NpcIntents` can't call SMAPI. It takes an
  `ILineBank` (a read-only key-to-template map). The mod builds one on the game thread at load, from
  `helper.Translation` for the current language (`GetTranslations()` returns them all; `GetKeys()`
  and `ContainsKey()` also exist in SMAPI 4.5.2), and hands
  the immutable copy to the planner and ladder threads. Tests build one by reading
  `i18n/default.json` straight from the repo, so the coverage tests run against the real file.
- **Tokens are filled by our renderer**, not by `helper.Translation.Get`, because rendering happens
  off the game thread and every value must be sanitized before insertion. The syntax matches SMAPI's
  so translators see what they expect. SMAPI's gender switch blocks (**wiki**) aren't supported: the
  sanitizer removes `^`, which they use, from dialogue anyway.
- **Motive variants:** where a kind can be said in different moods, the key carries the motive
  after the kind (`line.Saw.Player.Hurt.Shane.1` vs `line.Saw.Player.Grateful.Shane.1`), chosen by
  the character's leading motive ([motives.md](motives.md)); lookup falls back to the key without a
  motive. Under motives (D24) a line's tone also follows the **net feeling** toward the subject:
  a hostile act (a sharp bubble, a cold letter) uses the `Hurt` or `Jealous` variant even when the
  act carries `News`, and the character's strongest emotion bias picks between an angry and a sad
  wording where a bucket has both ([temperament.md](temperament.md)).
- **Every vanilla villager gets its own bucket for every kind** (Sid, 2026-09-30: "I really want the
  language to feel like it matches the character"). Tone buckets are only the fallback for NPCs we
  haven't written for.
- **Tokens:** `{{who}}` (you / a name), `{{npc}}`, `{{place}}` (`PlaceNames`), `{{when}}`
  (`LineRenderer.When`), `{{name}}` (an item or festival display name stored in the diary detail),
  `{{time}}` (invitations). An unknown token is a load error, caught by a test.
- **Choice among variants:** FNV-1a of (npc, day, kind) modulo the count. Deterministic, varies by day.
- `LineRenderer` keeps its current three templates as the built-in fallback when a key is missing.

## Triggers and game hooks

SMAPI loads `i18n/` itself. The mod snapshots the bank at `SaveLoaded` and again when the game
language changes (SMAPI's `Content.LocaleChanged` event). A key missing from a language falls back
to `default.json`; a key missing everywhere reads as `(no translation:<key>)` unless placeholders are
turned off, so the bank must check `HasValue()` rather than the text. A missing or broken key falls back
to the built-in templates and logs once (a text problem must never disable the mod). Rendering runs
on the plan job or the game thread; it is pure.

`i18n/` must be copied with the build: add it to the deploy command in `AGENTS.md` next to
`regions.json` when the first key lands. Voice notes (`data/voices.json`) are model context, not
player-facing text, so they stay in `data/` and are not translated.

## Laya questions

None. The model picks *what* to talk about; the template bank decides the words.

## Deterministic rules: sanitizing

One sanitizer per channel, in `src/NpcIntents/LineSanitizer.cs`, applied last, after placeholders
are filled. Values inserted into templates are sanitized **before** insertion as well, because an
item name from another mod could contain anything.

| Channel | Removes | Keeps | Why (dialogue: the wiki's dialogue page; mail: `LetterViewerMenu` in the 1.6.15 decompile) |
|---|---|---|---|
| Dialogue | `#` `$` `%` `{` `[` (done), plus `^` `@` `*` `\|` `}` `]` `<` `>` | letters, digits, basic punctuation | `#` breaks pages, `$` runs commands and portraits, `%` and `{` are tokens, `[` gives items, `^` splits gendered text, `@` is the player name, `*` and `\|` are special (D12 notes); `}` `]` are harmless but pointless |
| Bubble | same as dialogue | | `showTextAboveHead` probably ignores commands, but it is cheap to be safe |
| Mail | `#` `$` `{` `[` `*` `\|` and `%` **except** the template's own `%item ... %%` block, which is appended by code after sanitizing | `^` (newline in mail) and `@` (player name) only where the template put them | the letter parser acts on `[#]` (cuts the rest), `[letterbg ...]`, `[textcolor ...]`, `%action ... %%` (runs a trigger action), `%item ... %%` (items, money, recipes, quests, special orders, conversation topics) and `%secretsanta`; only the newcomer code may append an item block |
| Log | control characters | everything else | readability |

Rules:
- Templates may use `@` and `^` only in the mail channel, and the sanitizer strips any that came
  from values.
- Length: cut at the channel maximum on a word boundary, adding nothing (no ellipsis needed).
- Everything is plain ASCII plus the characters the game font supports. Accented names in other
  languages are untested (in-game check when translation starts).

## Tuning constants

Channel maximums above; `RecentLinesKept` 20 lives in intents.

## Acceptance tests

- Every key in `i18n/default.json` renders with sample values; no leftover `{{...}}`; variant
  numbers have no gaps; every key matches the naming pattern.
- Every vanilla villager in `data/voices.json` has a bucket for every kind the planner and ladder can
  produce (a coverage test, so a new kind can't ship without lines).
- Each sanitizer removes exactly its set; a value containing `$action` or `[72]` comes out harmless
  in every channel; mail keeps template `^` and `@` but strips them from values.
- Variant choice is deterministic by (npc, day, kind) and covers all variants over many days.
- `PlaceNames` has an entry for every location in `data/regions.json` (a new test; today unknown
  names are split at capitals).
- Length cuts land on word boundaries.

## Status

Done: `src/NpcIntents/LineRenderer.cs` (3 templates), `LineSanitizer.cs` (dialogue set only),
`PlaceNames.cs`, `VoiceSheets.cs` (used only in model context). Not started: `i18n/default.json`,
`ILineBank`, tone buckets, channels, value sanitizing, lengths.

## Writing the lines (decided 2026-09-30: Claude writes, Sid edits)

The template text is the most player-visible part of the mod, so it gets its own process:

1. **Voice notes first.** For each vanilla villager, a few lines in `VoiceSheets` style describing
   how they talk: sentence length, formality, pet phrases, what they care about, how they react to
   gifts and to being ignored (Shane's gruffness, Haley's early vanity and later warmth, Linus's
   gentleness). Written from the character's in-game dialogue and events, kept in
   `data/voices.json` (and used as the model's `voice:` line too, replacing `VoiceSheets`' one-liners).
2. **Original lines only.** Write new lines in the character's voice. Don't copy vanilla dialogue:
   it would read as a repeat, and it isn't ours to redistribute.
3. **Hearts matter.** Kinds that are personal (`GiftReceived`, `StoodUp`, invitations) get a low-
   and a high-hearts variant per NPC where the character's attitude changes (buckets `Haley.low`
   and `Haley.high`; the split is at 5 hearts). Lookup tries the hearts bucket first.
4. **Two to three variants** per NPC per kind, so repeats are rare even without novelty checks.
5. **Review loop.** Lines land in a PR that changes only `i18n/default.json` and `data/voices.json`.
   Sid edits the file directly on the branch or comments per NPC; the tests below catch broken
   placeholders and sanitizer problems, so Sid only has to judge the voice.
6. **Order:** start with the NPCs the shadow logs show speaking most, then everyone else.

## Open questions

- Translation: the file layout supports it from day one, but writing other languages is out of scope
  until the mod works in English. `PlaceNames` and voice notes are English-only today.
