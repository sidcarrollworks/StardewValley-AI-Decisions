# 18. Debug and playtest tools

**Status: not started.** Sid, 2026-09-30: "we need a tab in the in-game menu that shows some of
this data. Console commands work too." Without a way to look inside an NPC, "does it feel right?"
can't be judged, so these come early on the roadmap.

## Player-visible behavior

- **An "NPC Minds" tab in the game menu** (the menu opened with E / Esc), next to Social. It lists the
  villagers the player has met; picking one shows:
  - what they know about the player: last seen (with how, e.g. "Robin told her, an hour ago"), their
    habit guess for this hour;
  - their motives with strengths and the diary entries behind each ([motives.md](motives.md));
  - urge, current ladder rung, attempts today, grudge;
  - the last 10 diary entries in plain words;
  - today's planned line, open invitation, trade offers.
  It's read-only. It can be turned off (`ShowMindsTab`, default on while in development, off in a
  release unless Sid decides otherwise).
- **Console commands** in the SMAPI window, for the same data and more:

  | Command | Shows or does |
  |---|---|
  | `npcmod_npc <name>` | everything the tab shows, as text |
  | `npcmod_diary <name> [n]` | the last n diary entries (default 20) |
  | `npcmod_ledger <name>` | every ledger entry the NPC holds, with detail and hops |
  | `npcmod_motives [name]` | motive table for one NPC, or the top motive of each |
  | `npcmod_plan` | tonight's or today's planned lines and why |
  | `npcmod_summary [days]` | a playtest digest of the last n days: attempts by rung, responses, lines, visits, trades, grudges, model fallbacks |
  | `npcmod_newcomer ...`, `npcmod_live off` | already specced ([newcomer-week.md](newcomer-week.md), [rollout.md](rollout.md)) |
  | `npcmod_simulate <event>` | test hooks (shadow only), e.g. `npcmod_simulate gift Haley (O)421` writes a diary entry as if it happened |

## Data model

No new saved data. Everything is read from memory, the ladder snapshot and the plan. The summary
command needs a small rolling log of counters per day (`DailyStats`, last 28 days), saved under a
new save-data key `stats` so a digest survives reloads.

## Triggers and game hooks

- **Console:** `helper.ConsoleCommands.Add(name, doc, callback)` at `Entry`. Callbacks run on the game
  thread; they read `LatestJson`-style snapshots from the ladder, never wait on the worker.
- **The tab** (checked in the 1.6.15 decompile, `Menus/GameMenu.cs`): `GameMenu` keeps public `pages`
  and `tabs` lists, but the tab code maps tab names to pages with a fixed `switch`
  (`getTabNumberFromName`) and draws icons by name, so an unknown tab can't be clicked or drawn by the
  game itself. Without Harmony:
  1. on `Display.MenuChanged` to a `GameMenu`, append our page (`MindsPage : IClickableMenu`) to
     `pages` and a `ClickableComponent` named `squid.minds` to `tabs`, placed after the last tab;
  2. draw its icon in `Display.RenderedActiveMenu`;
  3. in `Input.ButtonPressed`, when the click lands on our tab, suppress it
     (`helper.Input.Suppress`) and switch pages by setting `GameMenu.currentTab` to our page's index
     (the same fields `changeTab` sets; check what else `changeTab` does before relying on it).
  If another mod also adds tabs and the layout breaks, fall back to opening the page as its own menu
  from a hotkey (`MindsKey`, default F8). Spike this first; the fallback is always available.
- Text in the tab comes from `i18n/` ([text.md](text.md)), like everything player-visible.

## Laya questions

None.

## Deterministic rules

- The tab and commands only read; `npcmod_simulate` is the one writer and only works in shadow
  mode (refused when any live switch is on).
- The tab shows only NPCs the player has a friendship record with (met), so it spoils nothing about
  characters not yet introduced.

## Tuning constants

`ShowMindsTab` (config), `MindsKey` (config), `StatsDays` 28.

## Acceptance tests

- Unit: the formatter for each command (pure functions over memory snapshots), the `DailyStats`
  rollover, `npcmod_simulate` refused when live.
- In-game: the tab appears, opens, scrolls, closes; switching between vanilla tabs still works; with
  the tab off nothing changes; every command prints for a known NPC and fails politely for an
  unknown name.

## Status

Not started.

## Open questions

- Should a release keep the tab (players might enjoy seeing what NPCs think) or hide it? Decide once
  it exists.
