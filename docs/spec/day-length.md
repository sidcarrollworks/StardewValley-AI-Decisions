# 8. Day length

**Status: not started.** Brief design decision 7. Independent of every other feature; it gives
NPCs (and the player) more real time per day, so more happens between ticks the player can see.

## Player-visible behavior

With the setting on, a game day lasts longer in real time: 24 real minutes instead of vanilla's
14 (120 ticks x 7 s). Everything tied to the clock (shop hours, schedules, crops) is unchanged in game
time; only the real seconds per ten-minute tick change. Off by default. This is a gameplay change,
not shadow behavior, so it is a switch Sid turns on deliberately.

## Data model

Config only: `DayLengthMinutes` (int, 0 = vanilla, default 0). Nothing saved.

`src/NpcSchedules/DayLength.cs` (pure): `TenMinuteMs(minutes) = round(minutes * 60000 / 120 / 1000) * 1000`
(a multiple of 1000, because the game divides by 1000 with integers: `stardew-source-notes.md`,
"Clock"), and `MinuteMs = TenMinuteMs / 10`. 24 minutes gives 12000 and 1200; 18 gives 9000 and 900;
20 gives 10000 and 1000.

## Triggers and game hooks

- Set **both** `Game1.realMilliSecondsPerGameTenMinutes` and `Game1.realMilliSecondsPerGameMinute`
  at `SaveLoaded`. In the 1.6.15 decompile they are `public static int` (not readonly), set to 700 and
  7000 only in `Game1`'s static constructor, and never assigned anywhere else, so nothing in the game
  resets them and one set per session is enough. The ten-minute value is not derived from the
  one-minute value after start-up, so setting one alone is a bug.
- What reads which: the clock compares against the ten-minute value (plus a per-location
  `ExtraMillisecondsPerInGameMinute`, used by a few special maps); schedule arrival math uses
  `realMilliSecondsPerGameTenMinutes / 1000 * 60`; buff durations and a few map timers use the
  one-minute value, so buffs keep the same length in game minutes.
- Schedules are parsed in the new-day processing (`NPC.dayUpdate` -> `resetForNewDay` ->
  `TryLoadSchedule`), before any 6:00 event. Setting the values at `SaveLoaded` covers every day after
  the first; on the load day, schedules were parsed with the vanilla value (a small arrival-time
  error for one day, acceptable).
- Restore the vanilla values (700 and 7000) on `ReturnedToTitle` so another save without the setting
  is unaffected.
- Multiplayer: only the host sets it (`Context.IsMainPlayer`). Confirmed: only the host accumulates
  the clock timer and advances `timeOfDay`; farmhands receive the time from the host.

## Laya questions

None.

## Deterministic rules

Pure arithmetic above. Any value under 14 minutes or over 60 is clamped and logged.

## Tuning constants

`DayLengthMinutes` in config. Decided (Sid, 2026-09-30): the default stays vanilla (0); Sid tries 20
minutes first, then decides whether 24 is better. The brief says test 18 to 20 before 24.

## Acceptance tests

Unit: `DayLength.TenMinuteMs` for 0 (vanilla 7000), 18, 20, 24, and clamping; always a multiple of
1000.

In-game: time a full day with a stopwatch at 20 minutes; check that Pierre opens and closes at his
usual game times, that NPCs arrive at schedule points on time (not early), and that buffs whose
duration is in game minutes still last the right game time (the source notes warn some buffs scale
with the constant: test the ones the player uses). Check the mod's ladder pace in real time: urge
growth is per tick, so it is unchanged in game time.

## Status

Not started.

## Open questions

- None left in code; the in-game checks above (shop hours, arrival times, buffs) are what remain.
