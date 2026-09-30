# Stardew Source Notes

"Verified" means read in source or docs in this project's research. "Recall" means remembered, not confirmed, so check it.

## Sources

- Decompiled 1.5.6: https://github.com/WeDias/StardewValley (NPC.cs, 5,954 lines)
- Decompiled 1.6: https://github.com/Dannode36/StardewValleyDecompiled (NPC.cs 7,154 lines, Game1.cs 16,245 lines)
- A local decompile of the installed game (1.6.15) with ILSpy or `ilspycmd` is the most exact source; how to make one is in `docs/spec/README.md`, "Verifying game facts". Done on 2026-09-30; findings below.
- Wiki: Modding:Dialogue, Modding:NPC data, Modding:Schedule data, Friendship, Marriage (stardewvalleywiki.com)
- Wiki, modder guide (read 2026-09-30): Get Started, APIs/Harmony, APIs/Multiplayer, APIs/Translation. What they settled is quoted in `docs/spec/` and marked "wiki" there.
- TypeSafe announcement: typesafe.ai/blog/introducing-system-one-models-and-jev
- Laya: github.com/NandhaKishorM/laya and huggingface.co/convaiinnovations/laya

## What NPCs store about the player

**Per NPC and player (`Farmer.friendshipData[npcName]`), verified in NPC.cs:** `Points`, `Status`, `IsDating`, `IsMarried`, `IsDivorced`, `ProposalRejected`, `GiftsToday`, `GiftsThisWeek`, `LastGiftDate`, `TalkedToToday`. Each heart is 250 points.

**Player-side fields NPC.cs reads:** `activeDialogueEvents` (topic to days left), `eventsSeen`, `mailReceived`. The game uses `mailReceived` as a per-NPC "already said" flag, stored as `<NPC>_<topic>`.

**Per-NPC fields:** age, manners, socialAnxiety, optimism, gender, datable, birthday, homeRegion, lastSeenMovieWeek, hasSaidAfternoonDialogue, hasBeenKissedToday, sleptInBed, isSleeping.

**Not found in NPC.cs:** any history of conversations. The per-item gift log is `Farmer.giftedItems` (verified; see "Checked in the 1.6.15 decompile").

**Decay (wiki):** married -20/day; after a bouquet -10/day until 10 hearts; pre-bouquet -2/day until 8 hearts; non-romanceable -2/day until 10 hearts.

## NPC methods (1.5.6, 123 public)

- **Talk and dialogue:** `checkAction(Farmer, GameLocation)`, `canTalk`, `grantConversationFriendship(who, amount=20)`, `checkForNewCurrentDialogue(heartLevel, noPreface)`, `tryToRetrieveDialogue`, `resetCurrentDialogue`, `resetSeasonalDialogue`, `setNewDialogue(text, add, clearOnMovement)` plus a sheet/key overload, `addExtraDialogues(text)`, `getHi`, `sayHiTo`, `showTextAboveHead(text, color, style, duration, preTimer)`, `clearTextAboveHead`, `hasTemporaryMessageAvailable`, `setTemporaryMessages`
- **Marriage dialogue:** `addMarriageDialogue`, `checkForMarriageDialogue`, `setRandomAfternoonMarriageDialogue`, `setSpouseRoomMarriageDialogue`
- **Gifts:** `canReceiveThisItemAsGift`, `getGiftTasteForThisItem`, `getFavoriteItem`, `receiveGift(Object, Farmer, updateGiftLimitInfo, multiplier, showResponse)`, `tryToReceiveActiveObject`
- **State:** `isMarried`, `isMarriedOrEngaged`, `isRoommate`, `isDivorcedFrom`, `getSpouse`, `isBirthday`, `getHome`
- **Lifecycle:** `dayUpdate`, `resetForNewDay`, `performTenMinuteUpdate`, `update`, `reloadData`
- **Movement:** `checkSchedule`, `getMasterScheduleEntry`, `hasMasterScheduleEntry`, `InvalidateMasterSchedule`, `clearSchedule`, `arriveAt`, `walkInSquare`, `randomSquareMovement`, `moveTowardPlayer(threshold)`, `facePlayer`, `Halt`, `PathToOnFarm(dest, callback)`, `warp`, `FindFarmActivity`, `InitializeFarmActivities`, `UpdateFarmExploration`
- **Reactions:** `behaviorOnFarmerLocationEntry`, `behaviorOnFarmerPushing`, `getHitByPlayer`, `withinPlayerThreshold`
- **Not in NPC.cs:** `doEmote` and `faceTowardFarmerForPeriod` (Character class, recall); movement code that acts on `isWalkingTowardPlayer`; `Game1.drawDialogue(npc)` opens a dialogue box.

## Schedules (1.6, verified)

**Script format:** commands separated by `/`. Each is `time location x y [facing] [animation] ["message"]`. A leading `a` on the time means arrive-by instead of leave-at. `bed` sends the NPC to their sleep spot. `time 0` sets the spawn spot. Headers: `GOTO <key>`, `GOTO season`, `GOTO NO_SCHEDULE`, `NOT friendship <npc> <hearts>`, `MAIL <id>`.

All of the below was verified in this project against decompiled 1.6 (`NPC.parseMasterScheduleImpl`, `NPC.TryLoadSchedule`, `Game1.performTenMinuteClockUpdate`, `Game1.shortDayNameFromDayOfSeason`).

**Initial commands (NPC.parseMasterScheduleImpl):**
- First field `GOTO <key>`: load that key instead. `season` -> current season name (fallback `spring` if that key is missing). Missing key -> log + fall back to `spring`. Target that fails to parse -> fall back to `spring`.
- First field `NOT friendship <npc> <hearts> [more pairs]`: if ANY pair is met (player hearts >= level) the script ends and `spring` is used. If not met, the next field may be a `GOTO` (runs then). `NOT` without `friendship` is ignored.
- First field `MAIL <id>`: if the letter/world-state ID was NOT received, the NEXT field's command runs; if received, the field after that runs (example: `MAIL ccVault/GOTO spring/GOTO summer`). Not limited to GOTO in principle.
- `GOTO` after NOT/MAIL: `season` -> current season, `no_schedule` (case-insensitive) -> followSchedule=false, no schedule today. Missing target -> parse exception -> empty schedule (the game catches it; the NPC just stays home).
- Any parse failure (bad time, bad coords, duplicate times — the schedule dictionary is keyed by time, so a duplicate throws) -> the whole script fails -> empty schedule for the day.
- Cycle detection: a `visited` list of keys, case-insensitive. A loop logs an error and gives an empty schedule.

**Key picked for an unmarried NPC (NPC.TryLoadSchedule, exact order):**
1. `GreenRain` (only on green-rain days in year 1)
2. island-resort schedule name (`islandScheduleName`)
3. passive-festival keys: `<festival>_<day>`, then `<festival>`
4. `<season>_<day>`
5. `<day>_<hearts>` — tried from the current heart level DOWN to 1 (plain loop, no skip)
6. `<day>`
7. `bus` — Pam only, and only if the player has the `ccVault` mail flag
8. if raining: `rain2` (50% coin flip, `Game1.random.NextBool()` — unseeded in the game), then `rain`
9. `<season>_<dow>_<hearts>` — NOTE the loop decrements `tryHearts` twice per iteration, so it tries heartLevel, heartLevel-2, heartLevel-4, ... >= 1
10. `<season>_<dow>`
11. `<dow>_<hearts>` — same double-decrement quirk
12. `<dow>`
13. `<season>`
14. `spring_<dow>`
15. `spring`
16. none matched -> no schedule for the day (NPC stays at default position)

Married NPCs use only `marriage_*` keys (+ `marriageJob` for Harvey/Maru/Penny on work days); if none match, no schedule. `hearts` = max over all players of friendship points / 250.

**Point parsing details:**
- Omitted location (token parses as int) = previous map; for the first point, the default map.
- `bed` (unmarried): resolves to the LAST point of the `default` schedule, else `spring`; married: BusStop. Any x/y after `bed` are ignored.
- Facing: defaults to 2 (down); a non-numeric token is NOT consumed and is then read as the animation.
- Dialogue = raw substring from the opening quote on (quotes included).
- Locked locations (JojaMart/Railroad/CommunityCenter when unavailable): the game switches to the `<location>_Replacement` schedule, else `default`, else `spring`.
- Arrival time math: walk distance (only orthogonal steps count) x 64px / 2, divided by `realMilliSecondsPerGameTenMinutes/1000*60` (= 420), rounded to ten-minute blocks, subtracted from the arrival time, then clamped to >= the previous point's time. An `a` point never starts before the previous point.

**Runtime:** `checkSchedule(timeOfDay)` runs each ten-minute tick per visible NPC; looks up the exact time in the parsed schedule dictionary. `ignoreScheduleToday` returns early. `addHour()` re-checks the previous 50 to 90 minutes. No teleporting in this path.

**Day of week:** `dayOfMonth % 7`: 0=Sun, 1=Mon, ... 6=Sat, so day 1 of every season is a Monday (verified).

**Cross-location travel:** precomputed route table (`routesFromLocationToLocation`, `getLocationRoute`) plus schedule path descriptions queued by time.

## Clock (1.6, verified)

- `Game1.realMilliSecondsPerGameMinute = 700`; ten minutes = 7000 ms. Day is 6:00 to 2:00.
- Each ten-minute tick: `timeOfDay += 10` (master only), minutes wrap (`%100 >= 60` -> next hour), capped at 2600. So the day is exactly **120 ticks**, and the minute field only ever holds 0-50: tick k (0..119) = `600 + (k/6)*100 + (k%6)*10`, last live tick 2550. Day ends when timeOfDay >= 2600 (passing out at 2800).
- Both constants are public static and read by the clock loop, lighting, and NPC arrival math. Use multiples of 1000 for the ten-minute value (integer division). Some buffs scale with the constant; test the ones you use.
- 24 minutes: 1200 and 12000.

## Dialogue system (wiki, verified)

- **Commands:** `$action <trigger action>`, `$b`, `$c`, `$d`, `$e`, `$k`, `$p`, `$q`/`$r`, `$query`, `$t <topic> [days]`, `$v <event id>`, `$y`, `[<item ids>]` (gives an item).
- **Portraits:** `$0` neutral, `$1`/`$h` happy, `$2`/`$s` sad, `$3`/`$u` unique, `$4`/`$l` love, `$5`/`$a` angry.
- **Emote IDs:** 28 sad, 8 question mark, 12 angry.
- **Conversation topics:** last 4 days by default. Auto "memory" topics appear at 1, 7, 14, 28, 56, and 104 days. A generic `Introduction` topic lasts 6 days from farmer creation.
- **Dumpster diving:** -25 friendship default, seen within 7 tiles. Only one NPC reacts.
- **Location keys:** `<location>_Entry` shows a bubble with a 50% chance when the NPC enters.
- **Data/Characters fields:** Manner (Neutral/Polite/Rude), SocialAnxiety (Neutral/Outgoing/Shy), Optimism (Neutral/Negative/Positive), Age, HomeRegion, FriendsAndFamily, gift tastes in five tiers, WinterStarGifts. Children are excluded from item-delivery quests.

## Checked in the 1.6.15 decompile (2026-09-30)

From a local decompile of the installed game (`Stardew Valley.dll` 1.6.15.24356), its data models
(`StardewValley.GameData.dll`) and SMAPI 4.5.2, made with `ilspycmd` into
`%USERPROFILE%\stardew-decompiled\` (not in the repo; how: `docs/spec/README.md`, "Verifying game
facts"). File names are the decompiled classes. Everything here is **verified**.

**Clock and the night**
- `Game1.realMilliSecondsPerGameMinute` / `realMilliSecondsPerGameTenMinutes` are `public static int`,
  700 / 7000, set only in `Game1`'s static constructor and never reassigned; the second is not
  recomputed from the first. The clock compares `gameTimeInterval` with the ten-minute value (plus a
  per-location `ExtraMillisecondsPerInGameMinute`); schedule arrival math uses
  `realMilliSecondsPerGameTenMinutes / 1000 * 60`; buffs use the one-minute value.
- Only the host accumulates the timer and advances `timeOfDay`; farmhands receive it.
- `Game1.shouldTimePass()` is false during a festival (and, in single player, during events, menus
  and pauses). When a festival ends the time jumps to 22:00 (or 24:00) in one step.
- New day (`Game1._newDayAfterFade`, which SMAPI runs synchronously): `stats.DaysPlayed++`,
  `timeOfDay = 600`, dialogue stacks reset (`ResetCharacterDialogues`), mail for tomorrow moved to
  the mailbox (`ReceiveMailForTomorrow`), then every `NPC.dayUpdate` -> `resetForNewDay` ->
  `TryLoadSchedule`. So each NPC's schedule for the day exists before any 6:00 event. The observed
  SMAPI order (`TimeChanged` 600 before `Saving`, see Tools) stands.
- SMAPI's `TimeChanged` is a watcher on `Game1.timeOfDay`, polled every update: one event per change
  of value, however big the jump. It doesn't fire on the tick a save loads or while saving.

**NPCs, schedules, movement**
- Off-screen NPCs move every tick on the host: `Game1.UpdateLocations` updates every location
  (`updateEvenIfFarmerIsntHere` -> `updateCharacters`). Farmhands update only their active locations.
- `Game1.locations` holds the static maps only; building interiors are reached through
  `Utility.ForEachLocation(action, includeInteriors = true, includeGenerated = false)`.
- `NPC.Schedule` is `Dictionary<int, SchedulePathDescription>` (private setter); a
  `SchedulePathDescription` has `route` (`Stack<Point>`), `time`, `targetLocationName`,
  `targetTile`, `facingDirection`, `endOfRouteBehavior`, `endOfRouteMessage`.
- `NPC.checkSchedule(int timeOfDay)` returns early if `ignoreScheduleToday` or no schedule. While a
  path controller is busy it queues the due steps (`queuedSchedulePaths`) instead of dropping them.
- `NPC.getMasterScheduleRawData()` loads `Characters\schedules\<Name>`. `TryLoadSchedule()` has
  overloads taking a key, a key plus raw script (used mid-day by the Desert Festival), or a parsed
  schedule.
- Cross-map routes: `WarpPathfindingCache.GetLocationRoute(start, end, gender)`;
  `NPC.pathfindToNextScheduleLocation(scheduleKey, startLocation, startX, startY, endLocation, endX,
  endY, facing, endBehavior, endMessage)` returns a `SchedulePathDescription`.
- `PathFindController`: `Character.controller` (and `NPC.temporaryController`) advance on the host
  only. Only controllers with `NPCSchedule` (or `nonDestructivePathing`) follow warps between maps.
  When a `temporaryController` with `NPCSchedule` finishes, `NPC.update` calls
  `checkSchedule(Game1.timeOfDay)`: the game's own "walk somewhere, then resume" pattern
  (`NPC.prepareToDisembarkOnNewSchedulePath`). Setting `ignoreScheduleToday` abandons the schedule
  for the rest of the day.
- **`NPC.PathToOnFarm` does not exist in 1.6** (the brief named it).
- `Game1.warpCharacter(NPC, string location, Point tile)` (also `Vector2` overloads); on a farmhand it
  only sends a request.

**Dialogue**
- `NPC.setNewDialogue(string translationKey, bool add = false, bool clearOnMovement = false)` takes a
  translation key, not text. For raw text: `setNewDialogue(new Dialogue(npc, key, text), add,
  clearOnMovement)`; the `Dialogue(NPC, string translationKey, string dialogueText)` key is a label.
- `NPC.addExtraDialogue(Dialogue)` is singular.
- `clearOnMovement` sets `removeOnNextMove`: the line is popped when the NPC starts a path, is
  warped, or ends a route animation.
- Talking runs `NPC.checkForNewCurrentDialogue`, which clears the dialogue stack when a conversation
  topic or location line applies; events clear it too.
- `NPC.CurrentDialogue` reads `Game1.npcDialogues[name]`, per game instance and **not** synced in
  multiplayer.
- `DialogueBox.characterDialogue` is the shown `Dialogue` (its `speaker` is the NPC);
  `getCurrentString()` is the text on screen. Pages split by pixel height (the box is 1200 x 384,
  about 460 px of it portrait); `#` separates lines and `#$b#` breaks a page.
- `Game1.drawDialogue(NPC)` opens a box for the top of the NPC's stack.
- `Character.doEmote(int whichEmote, bool playSound, bool nextEventCommand = true)`. Emote ids:
  empty can 4, question 8, angry 12, exclamation 16, heart 20, sleep 24, sad 28, happy 32, x 36,
  pause 40, video game 52, music note 56, blush 60.
- `NPC.showTextAboveHead(string text, Color? spriteTextColor = null, int style = 2, int duration =
  3000, int preTimer = 0)`.
- Conversation topics: `Farmer.activeDialogueEvents` (topic -> days left);
  `Farmer.autoGenerateActiveDialogueEvent(id, duration = 4)`. An NPC says the dialogue whose key is
  the topic id, once (mail flag `<Name>_<topic>`).

**Gifts, quests, festivals**
- `NPC.receiveGift(Object o, Farmer giver, bool updateGiftLimitInfo = true, float
  friendshipChangeMultiplier = 1f, bool showResponse = true)`, reached from
  `tryToReceiveActiveObject` after the limits (1 a day, 2 a week, except spouse, child, birthday,
  Stardrop Tea). Birthday multiplies friendship by 8. The Winter Star gift calls it with
  `updateGiftLimitInfo: false`. Quest and special-order handovers take other paths.
- **The per-item gift log** (open question in the brief): `Farmer.giftedItems`, NPC name -> item id ->
  count, updated in `Farmer.onGiftGiven`.
- `NPC.getGiftTasteForThisItem(Item)`: love 0, like 2, dislike 4, hate 6, Stardrop Tea 7, neutral 8
  (`NPC.gift_taste_*`). `NPC.isBirthday()` compares `Data/Characters` `BirthSeason`/`BirthDay`.
- `GiftsToday`/`GiftsThisWeek`/`LastGiftDate` (on `Friendship`) change only in `receiveGift`.
- Quest completion always goes through `Quest.questComplete()`; `Quest.completed` is a `NetBool`. A
  quest with a money reward stays in `questLog`, completed, until the reward is claimed. Target NPC:
  `target` on item delivery, slay monster, fishing and resource collection quests; `npcName` on
  lost-item quests; `SocializeQuest` (introductions) has a `whoToGreet` list. Item delivery grants the
  target friendship itself (150 points for a daily quest, 255 otherwise). No SMAPI quest event exists.
- Special orders: `SpecialOrder.requester`; completed keys go into `team.completedSpecialOrders`, a
  string set.
- `Data/Quests`: key -> `type/title/description/objective/conditions/nextQuests/money/rewardDescription/canBeCancelled`.
- `Utility.isFestivalDay(int day, Season season)` (and overloads) covers the main festivals only;
  passive festivals are `Utility.IsPassiveFestivalDay` and have no event. `Game1.isFestival()` reads
  `currentLocation.currentEvent.isFestival`. Festival NPCs are `Event.actors`, **clones** of the
  villagers (`EventActor = true`).
- Introductions: a `SocializeQuest` whose list is every NPC with `IntroductionsQuest` set, or by
  default `HomeRegion == "Town"`. It progresses on `OnNpcSocialized` (talking, in `NPC.checkAction`),
  where the `Friendship` record is also created on first talk.
- Bedroom doors (`Door` action) open at 2 hearts and then stay open (`doorUnlock<Name>` mail flag).

**Mail**
- `Farmer.mailForTomorrow`, `mailbox` and `mailReceived` are per farmer.
  `Game1.addMailForTomorrow(id, noLetter = false, sendToEveryone = false)` skips ids the player has or
  will get. Opening a letter from the mailbox adds its id to `mailReceived`; the open
  `LetterViewerMenu` has `mailTitle`.
- The letter parser (`LetterViewerMenu`): `[#]` cuts the rest; `@` is the player's name; gender
  switch blocks; `[letterbg ...]`; `[textcolor ...]`; `%action <trigger action> %%`; `%item <type>
  ... %%` with types `id`, `object <id> <count>`, `tools`, `bigobject`, `furniture`, `money`,
  `conversationtopic <id> <days>`, `cookingrecipe`, `craftingrecipe`, `itemrecovery`, `quest <id>
  [autoAdd]`, `specialorder`; `%secretsanta`; `^` is a line break.

**Shops**
- `Data/Shops` `Owners` (`ShopOwnerData`: `Name`, `Condition`, `ClosedMessage`, ...).
  `Utility.TryOpenShopMenu(shopId, location, ownerArea, maxOwnerY, forceOpen, ...)` opens only if a
  listed owner stands in the area, else shows `ClosedMessage` or doesn't open. Pierre must be at tile
  (4, 17) (his honor box appears only when he's on Ginger Island); Robin within 3 tiles of her
  counter; Marnie at hers (unless the Animal Catalogue was read); Gus behind the bar; Harvey at the
  counter; Willy anywhere in his shop; Clint anywhere in the blacksmith.

**Characters data** (`Data/Characters`, `CharacterData`)
- `Home` is a list of `{Id, Condition, Location, Tile, Direction}` (first matching condition wins).
- `SocialAnxiety` Outgoing/Shy/Neutral; `Manner` Neutral/Polite/Rude; `Optimism`
  Positive/Negative/Neutral; `Age` Adult/Teen/Child; `CanBeRomanced`; `BirthSeason`, `BirthDay`;
  `HomeRegion`; `IntroductionsQuest`.
- `FriendsAndFamily` is `Dictionary<string, string>`: other NPC -> optional word for dialogue
  ("mom"). Not comprehensive, and it doesn't say family or friend.
- Read with `npc.GetData()`, `NPC.TryGetData(name, out data)` or `Game1.characterData`.

**Other**
- `Game1.uniqueIDForThisGame` is a `ulong` per save (part of the save folder name).
- `Game1.stats.DaysPlayed` is a `uint`, raised by one in each new-day processing.
- `Farmer.getFriendshipHeartLevelForNPC(name)` = friendship points / 250.
- `Farmer.addItemByMenuIfNecessary(Item, callback = null, forceQueue = false)`.
- `Farmer.UniqueMultiplayerID` (`long`), `Game1.getOnlineFarmers()`, `Game1.getAllFarmers()`.

**SMAPI 4.5.2**
- `Context`: `CanPlayerMove` = `IsPlayerFree && Game1.player.CanMove`; `IsPlayerFree` needs no menu
  and no dialogue, and during an event is true only at a festival. `IsMultiplayer` covers
  split-screen and network play; `IsMainPlayer` is the host's first screen; `IsOnHostComputer` also
  covers split-screen guests.
- `PerScreen<T>` (`StardewModdingAPI.Utilities`) holds per-screen state.
- Save data (`helper.Data.WriteSaveData`) is JSON inside the save file (`Game1.CustomData`, key
  `smapi/mod-data/<mod id>/<key>`, lower-cased); it throws for a farmhand on a remote host.
- Translations: `helper.Translation.Get(key, tokens)`, `GetTranslations()`, `GetKeys()`,
  `ContainsKey()`; tokens `{{name}}`; a missing key reads `(no translation:<key>)`, so test
  `HasValue()`. `Content.LocaleChanged` exists.
- `Content.AssetRequested` -> `e.Edit(Action<IAssetData>, priority, onBehalfOf)`;
  `asset.AsDictionary<TKey, TValue>().Data`; `GameContent.InvalidateCache(string)`.
- Multiplayer: `SendMessage<T>(message, messageType, modIDs, playerIDs)`,
  `ModMessageReceived`, `GetConnectedPlayers()`, `GetActiveLocations()`.
- Harmony: SMAPI ships Harmony 2.2.2 and needs no opt-in (it logs "patched game code"); the
  `harmony_summary` console command lists patches per method.

## Tools

- **SMAPI:** `GameLoop.Saving`, `DayStarted`, `DayEnding`, `helper.Data.WriteSaveData` (verified in-game, SMAPI 4.5.2 / 1.6.15). Night order seen in the SMAPI log: `DayEnding` -> the "NewDay" task -> `TimeChanged` with NewTime 600 (the date is already the new day) -> `Saving` -> `DayStarted`. So the 6:00 tick runs before the save and before `DayStarted`.
- **Laya checkpoints:** `laya` (English, 512 ctx), `laya-multilingual` (1,024), `laya-typed-decisions` (1,024).
- **xnbcli** (LeonBlade/xnbcli, Node): unpacks plain-XNA-type XNB files directly (dictionary data works, e.g. `Characters/schedules/*.xnb`). The native `lz4` module needs a stub (`const LZ4 = null`) when no C++ toolchain is present; LZX files (all schedule files) don't need it. Structured 1.6 data (Data/Locations etc.) uses game-specific type readers xnbcli can't resolve — StardewXnbHack handles those but runs through a real game instance (needs to run from the game folder, briefly opens a game window, writes `Content (unpacked)`).
