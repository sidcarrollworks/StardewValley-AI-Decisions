# 14. Multiplayer and compatibility

**Status: single-player guard built (2026-10-04); multiplayer itself not started.** The mod assumes one player (`Game1.player`) and vanilla NPCs, and has not
been tested with other mods.

## Multiplayer

Every SMAPI player runs the mod. Without care, each machine would keep its own memory and act on
its own, and the "Player" subject would mean a different farmer on each machine.

**Decided (Sid, 2026-09-30): single-player for now**, and research what multiplayer would need (below).
- A remote farmhand (`!Context.IsOnHostComputer`) logs `Multiplayer farmhand: NPC memory runs on
  the host only` and hooks nothing else.
- Split-screen players share one process with the host, so there the guard is per event: every
  handler returns early unless `Context.IsMainPlayer` (screen 0). `Context.IsMultiplayer` is true for
  both split-screen and network sessions.
- On the host in multiplayer, the mod keeps running but observes only the host's farmer as `Player`
  and logs a warning that other farmers are ignored. No live behavior runs in multiplayer
  (`Live` switches are ignored) until multiplayer is designed.

### What multiplayer would need (research list)

Checked against the SMAPI wiki's multiplayer page (stardewvalleywiki.com/Modding:Modder_Guide/APIs/Multiplayer,
read 2026-09-30) where marked **wiki**; everything else is recalled and needs checking.

What we know:
- **Only the host sees the whole world.** "Each farmhand will receive data for their current
  location, the farm, farmhouse, and farm buildings" (**wiki**, `GetActiveLocations`). So a farmhand
  can't observe NPCs across town: memory, gossip, the ladder and planning must run on the host.
- **Messages between copies of the mod:** `helper.Multiplayer.SendMessage(data, messageType,
  modIDs, playerIDs)` and the `ModMessageReceived` event (**wiki**). This is how the host would tell
  a farmhand's copy to show an emote, a bubble or a dialogue on that farmhand's screen.
- **Who is connected and whether they have the mod:** `GetConnectedPlayers()` returns each peer's
  `PlayerID`, `IsHost`, `IsSplitScreen`, `HasSmapi` and `Mods` (**wiki**). A farmhand without the
  mod still gets host-side effects that are synced by the game (mail, NPC movement), but not
  mod-drawn visuals.
- **Split-screen co-op** runs several players in one process (`Context.ScreenId`, **wiki**). Every
  field keyed to "the player" would need SMAPI's per-screen storage (`StardewModdingAPI.Utilities.PerScreen<T>`,
  read and written through `.Value`).

What has to change (each a design question to answer before building):
1. **Subjects per farmer.** `Player` becomes `Player:<UniqueMultiplayerID>` (`Farmer.UniqueMultiplayerID`, a `long`)
   in the ledger, diaries, beliefs and ladder; a save migration maps the old `Player` to the host.
   Hearts are per farmer already in the game (`friendshipData` lives on each `Farmer`).
2. **Perception on the host.** `CollectPresences` adds every online farmer in `Game1.getOnlineFarmers()`,
   each with its own location and tile.
3. **Motives per (NPC, farmer).** Regard, motives and frustration per farmer; mood stays per NPC
   (a villager has one day, whoever it meets), and the daily caps probably stay town-wide so a busy
   server doesn't flood ([motives.md](motives.md)).
4. **Dialogue lives in each player's own game, not in the shared world.** Checked in the decompile:
   `NPC.CurrentDialogue` reads `Game1.npcDialogues[name]`, a per-instance static that is not synced.
   So a line pushed on the host is shown only to the host, and a farmhand's line must be pushed by
   the mod copy on that farmhand's machine, told by a host message. That is workable: it makes
   delivery naturally per farmer.
5. **Mail is per farmer** (`mailForTomorrow`, `mailbox` and `mailReceived` are fields on each `Farmer`), which fits invitations.
6. **Save data:** `helper.Data.ReadSaveData` and `WriteSaveData` both throw on a farmhand connected
   to a remote host (split-screen players on the host's PC are allowed; verified, SMAPI 4.5.2
   `DataHelper.cs:181-226`). Memory only needs to live on the host anyway.
7. **NPC movement** (visits, newcomer week) is host-side: path controllers only advance on the host,
   and `warpCharacter` on a farmhand only sends a request. How smoothly farmhands see a scripted
   walk is an in-game check.
8. **Day length:** the clock is the host's. Only the host accumulates the ten-minute timer and
   advances `timeOfDay`; farmhands receive it, so the constants only need setting on the host.
9. **Laya:** one server on the host's machine; farmhands never call it.

Rough size once the research questions are answered: M for host-only memory with per-farmer
subjects in shadow; L for live delivery to farmhands.

## Compatibility with other mods

| Concern | What happens today | Plan |
|---|---|---|
| Custom NPCs (e.g. Stardew Valley Expanded) | observed like any villager (`IsVillager`); unmapped locations count as region `Other`; place names fall back to splitting CamelCase; voice falls back to "friendly and plain-spoken" | add regions and names by data (`data/regions.json`, `PlaceNames`) for popular mods on request; the tone buckets in `i18n/default.json` cover custom NPCs through their `Data/Characters` fields |
| Mods that edit schedules | the planned prior loader reads schedules through `GameContent.Load`, so it sees edits | nothing |
| Mods that add dialogue | our live lines are added with `setNewDialogue(dialogue, add: true)`, which pushes on top of the NPC's stack without clearing it; but vanilla topic and location lines clear the stack when they apply ([intents.md](intents.md)), and other mods' may too | test with one popular dialogue mod before `IntentLines` ships |
| Mods that add mail | our letter ids are prefixed `squid.StardewNpcMod.` | nothing |
| Harmony patches | the only planned patch is a read-only postfix on `NPC.receiveGift` ([diary.md](diary.md)); postfixes stack safely | nothing |
| Time mods (e.g. TimeSpeed) | conflict with `DayLengthMinutes` | if another mod sets the clock constants, log a warning and don't set them (detect by checking the values at `SaveLoaded` against vanilla 700 / 7000: the game never changes them itself) |
| Generic Mod Config Menu | not integrated | optional, later ([config.md](config.md)) |

## Acceptance tests

- Unit: a pure `HostOnly.RoleOf(isMultiplayer, isMainPlayer, isOnHostComputer)` returns Solo,
  Host, SplitScreenGuest or Farmhand, and the mod's hook-up follows it (built as `HostOnly`;
  `tests/NpcMemory.Tests/HostOnlyTests.cs`, which also covers a split-screen guest leaving for the
  title).
- In-game: a split-screen co-op session on one PC (local multiplayer; `Context.IsSplitScreen`):
  the host's log shows the warning, and nothing runs twice for the second screen.

## Status

Built (2026-10-04, Sid: "Please add the guard"): `HostOnly` (`src/NpcMemory/HostOnly.cs`) names
the role from SMAPI's `Context` flags.
- **Farmhand on a remote host:** `OnSaveLoaded` logs the farmhand line once and returns before
  `LoadMemory`. Every other handler returns early, because `Context.IsMainPlayer` is false. Nothing
  is recorded, saved or shown. The gift and quest postfixes return early too.
- **Split-screen guest:** that screen's handlers return early. `ReturnedToTitle` on a guest's
  screen (`ScreenId != 0`, `HostOnly.ResetsAtTitle`) doesn't clear the host's memory.
- **Host in multiplayer:** the mod keeps running for the host's farmer. It logs one warning at load,
  or when the first farmer connects (`PeerConnected`), that other farmers are ignored. Live acts are
  already off in multiplayer (`LiveGate`).
- **Settled in the decompile (2026-10-05, `stardew-source-notes.md`, "Audit pass"):**
  `Context.IsMultiplayer` is already true at `SaveLoaded` for a hosted co-op save;
  `Context.IsMainPlayer` is false for split-screen guests and remote farmhands; the host's screen is
  `ScreenId` 0 and guests get new ids from 1.
- **Fixed in the audit:** a farmhand's own screen is screen 0, so `ReturnedToTitle` runs its reset
  there too. It threw a `NullReferenceException` on the playtest log when no save had been loaded
  earlier in that launch (`OnSaveLoaded` stops before creating it for a farmhand). It is null-safe
  now.
- **Still to check in game:** that a farmhand joining a host without the mod sees the one log line
  and no errors, and a split-screen session (the host's warning; nothing runs twice).

Multiplayer itself (the list above) is not started.

## Later: pairing with another mod for content

Sid's idea (2026-09-30), for after the system works: pair the mod with others so NPCs have more to
do and talk about. Directions to evaluate then, none designed yet:

- **Expose memory to content packs.** Content Patcher lets other mods register custom tokens
  (recalled: its mod-provided token API; verify). Tokens like "does Haley know where the player is",
  "days since Shane was stood up" or "the NPC's last diary kind" would let any content pack write
  dialogue and events that react to our memory, without us writing that content. This fits D12: the
  text comes from human-written packs, we only supply facts.
- **Ship our own content pack** alongside the mod, with dialogue for conversation topics and small
  events ([invitations.md](invitations.md), "Later"), written the same way as our `i18n/default.json` lines.
- **Support a big content mod's NPCs** (Stardew Valley Expanded is the obvious one): regions, place
  names and voice notes for its characters, so they are first-class rather than tone-bucket
  fallbacks.
- **Quest and event frameworks** from other mods could carry the "requests" idea further than
  vanilla quests. Research which are maintained for 1.6 before picking one.

## Open questions

- Which content mods Sid plays with; supporting those first is the cheapest win.
- Multiplayer: answer the research list above before estimating it properly.
