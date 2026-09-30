# 10. Text generation and sanitizing

**Status: partial.** Three first-person templates, place display names and one sanitizer exist.
Template banks, tone variants, per-channel sanitizers and length limits are not started. D12, D20;
architecture, "Overnight intents" (`LineRenderer`).

**Settled:** the model never writes text (`AGENTS.md`, rule 6; D12). The brief's older option of "a
short sentence from an LLM overnight" is not planned. Every visible string is a template filled with
values the mod controls.

## Player-visible behavior

Lines that sound a little different per NPC and per occasion, never contain internal names
(`SeedShop`), never repeat day after day, and never run a game command. Four channels:

| Channel | Where the player sees it | Max length |
|---|---|---|
| Dialogue | a dialogue box (queued lines, intents, forced dialogue) | 160 chars (fits one box page: verify) |
| Bubble | `showTextAboveHead` | 40 chars |
| Mail | a letter | 400 chars |
| Log | the SMAPI log (shadow) | none |

## Data model

- **`data/lines.json`** (new, D20: tables not code, editable without rebuilding, copied next to the
  mod like `regions.json`):
  ```json
  {
    "GiftReceived.Love": {
      "any":      ["Thank you again for the {name} {when}. I love it."],
      "polite":   ["I wanted to thank you properly for the {name} {when}. It was lovely."],
      "rude":     ["The {name} {when}? Fine. It was actually pretty great."],
      "negative": ["The {name} {when} was the best part of a long week."]
    },
    "Saw.Player": { "any": ["I saw you at {place} {when}."] },
    "Bubble.Greet": { "any": ["Hey!", "Oh, hi!"], "shy": ["..."] },
    "Mail.MissedYou": { "any": ["Dear @,^I was hoping to run into you {when}...^-{npc}"] }
  }
  ```
  Key = channel/kind plus a variant (taste, subject kind). Tone buckets come from `Data/Characters`
  (Manner: polite/rude; Optimism: positive/negative; SocialAnxiety: shy/outgoing; verify the
  fields); `any` is the fallback. An NPC-specific bucket (`"Haley": [...]`) overrides tone.
- **Placeholders:** `{who}` (you / a name), `{npc}`, `{place}` (`PlaceNames`), `{when}`
  (`LineRenderer.When`), `{name}` (an item or festival display name stored in the diary detail).
  Unknown placeholders are a load error, caught by a test.
- **Choice among variants:** FNV-1a of (npc, day, kind) modulo the count. Deterministic, varies by day.
- `LineRenderer` keeps its current behavior as the built-in fallback when `lines.json` is missing.

## Triggers and game hooks

`lines.json` is loaded at `Entry`, like `regions.json`; a malformed file logs an error and uses the
built-in templates (unlike `regions.json`, a text problem should not disable the mod). Rendering runs
on the plan job or the game thread; it is pure.

## Laya questions

None. The model picks *what* to talk about; the template bank decides the words.

## Deterministic rules: sanitizing

One sanitizer per channel, in `src/NpcIntents/LineSanitizer.cs`, applied last, after placeholders
are filled. Values inserted into templates are sanitized **before** insertion as well, because an
item name from another mod could contain anything.

| Channel | Removes | Keeps | Why (verify each against the dialogue and mail docs) |
|---|---|---|---|
| Dialogue | `#` `$` `%` `{` `[` (done), plus `^` `@` `*` `\|` `}` `]` `<` `>` | letters, digits, basic punctuation | `#` breaks pages, `$` runs commands and portraits, `%` and `{` are tokens, `[` gives items, `^` splits gendered text, `@` is the player name, `*` and `\|` are special (D12 notes); `}` `]` are harmless but pointless |
| Bubble | same as dialogue | | `showTextAboveHead` probably ignores commands, but it is cheap to be safe |
| Mail | `#` `$` `{` `[` `*` `\|` and `%` **except** the template's own `%item ... %%` block, which is appended by code after sanitizing | `^` (newline in mail) and `@` (player name) only where the template put them | mail uses `%item` to attach items; only the newcomer code may add one |
| Log | control characters | everything else | readability |

Rules:
- Templates may use `@` and `^` only in the mail channel, and the sanitizer strips any that came
  from values.
- Length: cut at the channel maximum on a word boundary, adding nothing (no ellipsis needed).
- Everything is plain ASCII plus the characters the game font supports (verify for accented names
  in other languages; for now non-English play is untested).

## Tuning constants

Channel maximums above; `RecentLinesKept` 20 lives in intents.

## Acceptance tests

- Every key in `lines.json` renders for every tone with sample values; no leftover `{...}`.
- Each sanitizer removes exactly its set; a value containing `$action` or `[72]` comes out harmless
  in every channel; mail keeps template `^` and `@` but strips them from values.
- Variant choice is deterministic by (npc, day, kind) and covers all variants over many days.
- `PlaceNames` has an entry for every location in `data/regions.json` (a new test; today unknown
  names are split at capitals).
- Length cuts land on word boundaries.

## Status

Done: `src/NpcIntents/LineRenderer.cs` (3 templates), `LineSanitizer.cs` (dialogue set only),
`PlaceNames.cs`, `VoiceSheets.cs` (used only in model context). Not started: `lines.json`, tone
buckets, channels, value sanitizing, lengths.

## Open questions

- Who writes the template text? It is the most player-visible part of the mod. Recommendation: Claude
  drafts a first bank per kind and tone; Sid edits `lines.json` directly.
- Translation: out of scope until the mod works in English.
