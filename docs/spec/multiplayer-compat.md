# 14. Multiplayer and compatibility

**Status: not started.** The mod assumes one player (`Game1.player`) and vanilla NPCs, and has not
been tested with other mods.

## Multiplayer

Every SMAPI player runs the mod. Without care, each machine would keep its own memory and act on
its own, and the "Player" subject would mean a different farmer on each machine.

**Recommended v1 (roadmap decision 7): single-player only.**
- If `Context.IsMultiplayer` (verify) and this machine is not the host (`!Context.IsMainPlayer`), the
  mod logs `Multiplayer farmhand: NPC memory runs on the host only` and hooks nothing else.
- On the host in multiplayer, the mod keeps running but observes only the host's farmer as `Player`
  and logs a warning that other farmers are ignored. No live behavior runs in multiplayer
  (`Live` switches are ignored) until multiplayer is designed.

**Later, if wanted:**
- Subjects per farmer: `Player:<UniqueMultiplayerID>` (verify the property) instead of `Player`; all
  diaries, ledger entries and beliefs are keyed that way; hearts per farmer.
- Host-authoritative: memory, ladder and planning run on the host; live actions for a farmhand are
  sent with SMAPI's multiplayer messages (`helper.Multiplayer.SendMessage`, verify) and shown on that
  farmhand's machine.
- Save data already lives with the host's save.

## Compatibility with other mods

| Concern | What happens today | Plan |
|---|---|---|
| Custom NPCs (e.g. Stardew Valley Expanded) | observed like any villager (`IsVillager`); unmapped locations count as region `Other`; place names fall back to splitting CamelCase; voice falls back to "friendly and plain-spoken" | add regions and names by data (`data/regions.json`, `PlaceNames`) for popular mods on request; `data/lines.json` tone buckets cover custom NPCs through their `Data/Characters` fields |
| Mods that edit schedules | the planned prior loader reads schedules through `GameContent.Load`, so it sees edits | nothing |
| Mods that add dialogue | our live lines are added with `setNewDialogue(add: true)`, which should queue alongside theirs (verify) | test with one popular dialogue mod before `IntentLines` ships |
| Mods that add mail | our letter ids are prefixed `squid.StardewNpcMod.` | nothing |
| Harmony patches | the only planned patch is a read-only postfix on `NPC.receiveGift` ([diary.md](diary.md)); postfixes stack safely | nothing |
| Time mods (e.g. TimeSpeed) | conflict with `DayLengthMinutes` | if another mod sets the clock constants, log a warning and don't set them (detect by checking the value at `SaveLoaded` against vanilla: verify) |
| Generic Mod Config Menu | not integrated | optional, later ([config.md](config.md)) |

## Acceptance tests

- Unit: a pure `MultiplayerMode.For(isMultiplayer, isMainPlayer)` returns Off / HostOnly / Single,
  and the mod's hook-up follows it.
- In-game: a two-player LAN session on one PC (two game instances: verify that's allowed) where the
  farmhand's log shows the disabled message and the host's shows the warning.

## Status

Not started. Today a farmhand would run a full independent copy.

## Open questions

- Is multiplayer in scope at all? (roadmap decision 7)
