# 14. Multiplayer and compatibility

**Status: not started.** The mod assumes one player (`Game1.player`) and vanilla NPCs, and has not
been tested with other mods.

## Multiplayer

Every SMAPI player runs the mod. Without care, each machine would keep its own memory and act on
its own, and the "Player" subject would mean a different farmer on each machine.

**Decided (Sid, 2026-09-30): single-player for now**, and research what multiplayer would need (below).
- If `Context.IsMultiplayer` (verify) and this machine is not the host (`!Context.IsMainPlayer`), the
  mod logs `Multiplayer farmhand: NPC memory runs on the host only` and hooks nothing else.
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
  field keyed to "the player" would need SMAPI's per-screen storage (`PerScreen<T>`: verify).

What has to change (each a design question to answer before building):
1. **Subjects per farmer.** `Player` becomes `Player:<UniqueMultiplayerID>` (verify the property)
   in the ledger, diaries, beliefs and ladder; a save migration maps the old `Player` to the host.
   Hearts are per farmer already in the game (`friendshipData` lives on each `Farmer`).
2. **Perception on the host.** `CollectPresences` adds every online farmer in `Game1.getOnlineFarmers()`
   (verify), each with its own location and tile.
3. **One ladder per (NPC, farmer).** Urge, rungs and caps per farmer; the daily caps probably stay
   town-wide so a busy server doesn't flood.
4. **Dialogue is per NPC, not per farmer.** A line added with `setNewDialogue` is heard by whichever
   farmer talks first (verify). Delivery would need a check of who is talking (the speaker's
   `DialogueBox` on that farmer's screen) or per-farmer dialogue through a message and a local
   push on the farmhand's copy. This is the hardest part.
5. **Mail is per farmer** (`mailForTomorrow` on each `Farmer`: verify), which fits invitations.
6. **Save data:** does `helper.Data.WriteSaveData` work on farmhands? Recalled: save data belongs to
   the host (verify on the SMAPI data API page). Memory only needs to live on the host anyway.
7. **NPC movement** (visits, newcomer week) is host-side game state; the game syncs NPC positions to
   farmhands (verify for scripted paths).
8. **Day length:** the clock is the host's; setting the constants on the host only (verify that
   farmhands don't also run their own ten-minute timer).
9. **Laya:** one server on the host's machine; farmhands never call it.

Rough size once the research questions are answered: M for host-only memory with per-farmer
subjects in shadow; L for live delivery to farmhands.

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

## Later: pairing with another mod for content

Sid's idea (2026-09-30), for after the system works: pair the mod with others so NPCs have more to
do and talk about. Directions to evaluate then, none designed yet:

- **Expose memory to content packs.** Content Patcher lets other mods register custom tokens
  (recalled: its mod-provided token API; verify). Tokens like "does Haley know where the player is",
  "days since Shane was stood up" or "the NPC's last diary kind" would let any content pack write
  dialogue and events that react to our memory, without us writing that content. This fits D12: the
  text comes from human-written packs, we only supply facts.
- **Ship our own content pack** alongside the mod, with dialogue for conversation topics and small
  events ([invitations.md](invitations.md), "Later"), written the same way as `data/lines.json`.
- **Support a big content mod's NPCs** (Stardew Valley Expanded is the obvious one): regions, place
  names and voice notes for its characters, so they are first-class rather than tone-bucket
  fallbacks.
- **Quest and event frameworks** from other mods could carry the "requests" idea further than
  vanilla quests. Research which are maintained for 1.6 before picking one.

## Open questions

- Which content mods Sid plays with; supporting those first is the cheapest win.
- Multiplayer: answer the research list above before estimating it properly.
