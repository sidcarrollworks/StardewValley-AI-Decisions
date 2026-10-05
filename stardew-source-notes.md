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

### Motives verify pass (2026-10-01)

Added for the motives work ([motives.md](spec/motives.md)); every fact grepped from the local
1.6.15 decompile. NOT FOUND items are listed so they land in the in-game checklist instead.

**Dialogue answers and questions**

- Answer flow: `DialogueBox.selectedResponse` (public int) is the picked index; on confirm the box
  calls `characterDialogue.chooseResponse(responses[selectedResponse])` (DialogueBox.cs:400-403),
  else `Game1.currentLocation.answerDialogue` / `currentEvent.answerDialogue` (:387/:392).
- `Dialogue.chooseResponse` (Dialogue.cs:1582) matches by `responseKey`; `$r` answers apply
  `farmer.changeFriendship(playerResponses[i].friendshipChange, speaker)` (Dialogue.cs:1618),
  `addSeenResponse` (`Farmer.dialogueQuestionsAnswered`, Farmer.cs:9106), optional
  `friend_<NPC>_<n>` extraArgument, then parses the answer text (:1635); `$y` quick answers swap
  the NPC's stack entry (1601-1610); festivals route to `Event.answerDialogueQuestion` with no
  friendship (1611-1617). The `$r` syntax is `$r <answerId> <friendshipDelta> <responseKey> <text>`
  (Dialogue.cs:745); `$y` answers carry -1 (:800).
- A read-only postfix on `Dialogue.chooseResponse(Response)` sees: `__instance.speaker` (NPC),
  `__instance.TranslationKey` (`Characters/Dialogue/<Name>:<key>`), the response's
  responseKey/responseText, and — castable to `NPCDialogueResponse` — friendshipChange, id,
  extraArgument. The chosen INDEX is a loop-local: compute it in a prefix from
  `getResponseOptions()` (public, :1568; cleared by postfix time at :596) or read
  `DialogueBox.selectedResponse`.
- Vanilla `$q` questions (real game content, not the decompile): 17 in 9 villager files —
  Abigail 4, Alex 3, Haley 2, Maru 2, Sebastian 2, Clint/Leah/Penny/Sam 1 each; plus 5 in
  `Data/ExtraDialogue` (Morris_* keys) and 46 inside `Data/Events` scripts. MarriageDialogue and
  rainy have none.
- Portrait/emotion commands are parsed by `checkEmotions` (Dialogue.cs:588-605, 1485-1561):
  `$h/$s/$u/$l/$a` match ANYWHERE in their `#`-segment (h->s->u->l->a, then stripped); the
  numbered form is the first `$`-digits in the segment (`$0..$n`; `getPortraitIndex` :554-582 maps
  $neutral=0,$h=1,$s=2,$u=3,$l=4,$a=5). So a portrait command APPENDED at the end of the text
  works; block commands (`$e $b $k $c $t $q $r $p $d $y $action $query`) must start the segment.

**Schedules**

- 1.6 schedules are NOT `Data/Schedules`: each NPC loads `Characters/schedules/<Name>`
  (NPC.cs:5993), cached in `_masterScheduleData`, re-read only after `InvalidateMasterSchedule()`
  (:5983-6012). `NPC.Schedule` has a private setter (:534); mods go through `TryLoadSchedule`
  (key / key+raw / parsed, :5911/5932/5951) or `ClearSchedule()`.
- **One-day replacement**: from `GameLoop.DayStarted` (after the day's schedules exist), call
  `npc.TryLoadSchedule("mymod_day", rawScript)` — `resetForNewDay` sets `ignoreScheduleToday =
  false` (NPC.cs:6194) and rebuilds from data next morning (:6216). A pure AssetRequested edit
  persists for every matching day AND is ignored until `InvalidateMasterSchedule()`.
- The day's schedule is built in `Game1._newDayAfterFade` -> `NPC.dayUpdate` -> `resetForNewDay`
  -> `TryLoadSchedule()` (Game1.cs:8442, NPC.cs:6087/6166/6216). A mid-day `TryLoadSchedule`
  replacement fires for all not-yet-reached times (checkSchedule compares against
  `lastAttemptedSchedule`, NPC.cs:4112-4121; clear `npc.queuedSchedulePaths` to drop queued
  stops). `Game1.warpCharacter` never touches `Schedule`, so replacements survive warps.
- `endOfRouteMessage` / `endOfRouteBehavior` are per-stop (parseMasterScheduleImpl, NPC.cs:5674-5690;
  SchedulePathDescription.cs:14/16). Behavior values: `change_beach`/`change_normal` (4636-4640),
  anything containing `square_`, or any `Data/AnimationDescriptions` key (105 shipped keys, e.g.
  abigail_videogames, haley_photo). The message is pushed via `setTemporaryMessages`; the literal
  `silent` suppresses dialogue (4307-4313).
- **Farm routing is excluded.** `Farm.ShouldExcludeFromNpcPathfinding()` returns true (Farm.cs:946)
  and `WarpPathfindingCache.IgnoreLocationNames = { Backwoods, Cellar, Farm }`
  (Pathfinding/WarpPathfindingCache.cs:15), so `pathfindToNextScheduleLocation` produces an empty
  path to Farm/FarmHouse and normal schedules cannot route there. Married NPCs bypass routing with
  direct warps (`Game1.warpCharacter(this, "Farm", ...)`, `arriveAtFarmHouse` NPC.cs:6965-6984) —
  that is the only farm entry. NOT FOUND: vanilla schedules that visit the farm (content files).

**Quests, special orders, mail**

- `new ItemDeliveryQuest(targetNpcName, itemId)` or the 6-arg ctor (Quests/ItemDeliveryQuest.cs:48/60;
  `target` is the NPC internal name). Add with `Game1.player.questLog.Add(q)` (public
  NetObjectList, Farmer.cs:199) or `Farmer.addQuest(id)` (Farmer.cs:7993) — the latter warns and
  adds NOTHING for ids not in `Data/Quests` (Quest.cs:275-281). `%item quest <id> %%` in mail also
  goes through `Farmer.addQuest`, so a Data/Quests entry IS required; it respects `NOQUEST_<id>`.
- Special orders: `Game1.player.team.AddSpecialOrder(id)` (FarmerTeam.cs:682) or mail
  `%item specialorder <id> true %%`; `Data/SpecialOrders` entries carry `Requester` (SpecialOrderData.cs:13).
  Automatic rewards on claim: `MoneyReward` (Amount x Multiplier), `FriendshipReward` (TargetName
  defaults to the Requester, Amount defaults to 250 -> `changeFriendship`), plus Mail/Object/Gems/
  ResetEvent rewards; claiming also bumps `specialOrderPrizeTickets` except Qi/DesertFestival
  orders (SpecialOrder.cs:876-884).

**Conversation topics** (every vanilla id, trigger, days)

- `Introduction` 6 (Farmer.cs:2076); `firstVisit_<location>` (:2280); `fishCaught_<item>` (3016);
  `houseUpgrade_<n>` (3550); `divorced_<npc>` / `divorced_once` / `divorced_twice` (3725/3748-3750);
  `married_<spouse>` / `married` / `married_twice` / `roommates_<spouse>` (Game1.cs:7969-7977);
  `achievement_<n>` (10643); `eventSeen_<id>` (GameLocation.cs:15770); `mineArea_<n>`
  (MineShaft.cs:3401); `dating_<npc>` / `dating` (NPC.cs:2157-2158); `questComplete_<id>`
  (Quest.cs:638); `emilyFiber` 2 (635); `cropMatured_<n>` (Crop.cs:907, Bush.cs:245);
  `purchasedAnimal_<n>` (AnimalHouse.cs:157); `structureBuilt_<n>` (Building.cs:1578); `wonGrange`
  (Event.cs:12049); `gotPet` (13088); `wonEggHunt` (13480); `wonIceFishing` (13541);
  `GreenRainFinished` 1 (Game1.cs:8397); `joja_Begin` 7 (JojaMart.cs:168); `movieTheater` 3
  (WorldChangeEvent.cs:322); `cc_Greenhouse` 3, `cc_Bus`/`cc_Minecart`/`cc_Bridge`/`cc_Boulder` 7;
  `pamHouseUpgrade` 4 (Event.cs:10987); `FullCrabPond` 14 (FishPond.cs:657);
  `lucky_pants_lewis` 28 (NPC.cs:1838); `dumped_Guys`/`dumped_Girls` 7,
  `secondChance_Guys`/`secondChance_Girls` 14 (Event.cs:3665-3671); `DesertMakeover` 0 marker
  (DesertFestival.cs:434). `pennyRedecorating` is only ever READ — content must add it.
- Content paths: `$t <id> [days=4]` (Dialogue.cs:854), event `addConversationTopic`,
  trigger `AddConversationTopic`, mail `%item conversationtopic <id> <days> %%`, or
  `Farmer.addEvent(id, days)` / `autoGenerateActiveDialogueEvent(id, 4)` (Farmer.cs:7117/3675).
  `Farmer.dayupdate` decrements each topic nightly (3521/3594-3609) and copies it to
  `previousActiveDialogueEvents`, which spawns `<topic>_memory_oneday/_oneweek/_twoweeks/
  _fourweeks/_eightweeks/_oneyear` at 1/7/14/28/56/104 days (3613-3638).
- An NPC with no dialogue key for a topic simply skips it — `checkForNewCurrentDialogue`
  `continue`s on a null key and the topic is NOT consumed (NPC.cs:3913-3920); the
  `FallbackDialogueForError` "..." path is only for load errors (NPC.cs:4724), not missing topics.

**Feelings sources**

- **Garbage cans**: `GameLocation.CheckGarbage` (GameLocation.cs:8349-8524) from the map action
  `Garbage <id>`; once-per-can per day via `NetWorldState.CheckedGarbage`. The FIRST villager
  within 7 tiles (Euclidean, Utility.cs:5086-5095) reacts: friendship
  `data?.DumpsterDiveFriendshipEffect ?? -25` (GameLocation.cs:8477), emote `DumpsterDiveEmote`
  else by Age (child 28, teen 8, other 12), plus a `DumpsterDiveComment` line. Linus is
  special-cased only for a chat message (8472-8475); his +5 friendship is data — NOT FOUND in
  decompile. No hat check exists anywhere in the path.
- **Heart events**: `Data/Events/<Location>` keys are `<eventId>/<precondition>...`; the NPC+hearts
  binding exists only as the `f <NpcName> <points>` precondition (Preconditions.cs:219-233, raw
  points, 250/heart). `eventsSeen` (Farmer.cs:237) gains the id in `Event.exitEvent` (Event.cs:4743-4752).
  NOT FOUND: any code-level way to tell a heart event from a cutscene, and any structured
  two-villager participant data (events are plain scripts).
- **Flower Dance**: accept iff spouse OR (`!HasPartnerForDance && friendship >= 1000` (4 hearts)
  `&& !isMarried`) with +250 friendship (Event.cs:12109-12160); who can be asked comes from
  Data/Characters `FlowerDanceCanDance`. `Farmer.dancePartner` (NetDancePartner) resets in
  `Farmer.dayupdate` (Farmer.cs:3532) — NOT readable after the festival day.
- **Luau soup**: `Event.governorTaste` (Event.cs:13396-13447) over `FarmerTeam.luauIngredients`:
  quality 4/3/2/1/edible-invalid -> +120/+60/0/-50/-100 friendship immediately to every NPC whose
  `HomeRegion` is Town (Utility.improveFriendshipWithEveryoneInRegion). No numeric result is
  stored; ingredients clear in `FarmerTeam.NewDay`. **Grange**: `Event.judgeGrange` ->
  `Event.grangeScore` (only while the event object lives); the durable bits are
  `player.festivalScore` (>=90 +1000 +achievement, >=75 +500, >=60 +250, shorts +750, else +50,
  Event.cs:12525-12556), reset in `Farmer.dayupdate` — so festivalScore is readable AT DayEnding.
  **Egg hunt**: winner compared by festivalScore thresholds (Event.cs:13449-13508); the durable
  marker is the `wonEggHunt` topic. All three festivals run AFTER SMAPI `DayEnding`, so the
  friendship deltas and `festivalScore` are DayEnding-readable.
- **Movies**: invitation via `tryToReceiveActiveObject` on the NPC (NPC.cs:1713/1920) with the
  villager/socialize/once-per-week/no-festival/before-21:00 gates; reaction love/like/dislike/
  reject = `MovieTheater.GetResponseForMovie` (MovieTheater.cs:1257-1284), deterministic from
  `Data/MoviesReactions` + the date's movie — love +200/emote 20, like +100/emote 56, dislike
  0/emote 24 (MovieTheaterScreeningEvent.cs:745-800). The reaction is NOT stored: readable traces
  are `NPC.lastSeenMovieWeek` (set at screening start) and the friendship points; a mod must
  recompute `GetResponseForMovie`. `Farmer.lastSeenMovieWeek` set on movie end (FarmerTeam.cs:937).
- **Passing out**: stamina <= -15 or time >= 2600 (Game1.cs:6453-6464) -> `Farmer.performPassoutWarp`
  (Farmer.cs:5841-5925): cost = min(location MaxPassOutCost, money/10); the letter id comes from
  `LocationContextData.PassOutMail` as `<id>_{Billed|NotBilled}_{Male|Female}` (fallback
  `passedOut2`) and goes to `mailForTomorrow`. The rescuer NAMES live in the Data/mail letters —
  NOT FOUND in decompile. `passedOut*` letters are deliberately not added to `mailReceived`
  (GameLocation.cs:10552-10555). Mine death is separate: `PlayerKilled` event with the rescuer
  chosen in code (Robin/Clint/Maru/Linus, 10% spouse; island: Willy/Leo), no mail (GameLocation.cs:15558-15597).
- **Resort**: `IslandSouth.SetupIslandSchedules` (IslandSouth.cs:839-970): 40% -> 5 random
  eligible NPCs, else one of 12 hard-coded name groups filled to 5; eligibility via
  `CanVisitIslandToday` (Data/Characters game-state query, CanSocialize, not on Farm, no hospital).
  Result stored in `Game1.netWorldState.Value.IslandVisitors` (:966; check
  `Game1.IsVisitingIslandToday`, Game1.cs:9273-9276) — readable in the morning; computed in
  `_newDayAfterFade` and on save load.
- **Friendship decay**: `Farmer.resetFriendshipsForNewDay` (Farmer.cs:4102-4137), called from
  `Farmer.dayUpdate` — i.e. AFTER SMAPI `DayEnding`. Rules: spouse/roommate not talked to -20/day
  (the spouse path is x0.66 in changeFriendship, Farmer.cs:5581); dating/engaged not talked and
  Points < 2500: -8/day; plus a -2 branch for (not talked AND (non-datable-or-dating, not married,
  < 2500) OR (datable, not dating, not married, < 2000)). So bouquet-dating decays -10/day until
  10 hearts, pre-bouquet datable -2/day until 8 hearts, ordinary friends -2/day until 10 hearts,
  spouse -20/day (no stop; cap 14 hearts). The wiki's "no decay below 2 hearts" threshold does NOT
  exist in code — the only gates are 2500/2000 points.

**Channels**

- **Phone (1.6)**: `Data/IncomingPhoneCalls` -> `IncomingPhoneCallData` with `TriggerCondition` /
  `RingCondition` (GSQs), `FromNpc`/`FromPortrait`/`FromDisplayName`, `Dialogue` (tokenizable),
  `IgnoreBaseChance`, `MaxCalls` (default 1) (IncomingPhoneCallData.cs:12-50).
  `DefaultPhoneHandler.CheckForIncomingCall` rolls 1% per ten-minute tick (DefaultPhoneHandler.cs:39-51);
  a day-specific call = an entry with a date GSQ in `TriggerCondition` + `IgnoreBaseChance=true`;
  exact timing needs an `IPhoneHandler` or the public `Phone.Ring(callId)` (Phone.cs:184-196).
- **Trigger actions** (exact names): `AddMail <Current|Host|All> <mailId> [MailType]` (default
  Tomorrow), `RemoveMail ...`, `AddConversationTopic <topicId> [daysDuration=4]`,
  `RemoveConversationTopic <topicId>`, `AddFriendshipPoints <npcName> <points>` — all public
  statics registered in `TriggerActionManager.DefaultActions` (TriggerActionManager.cs:657-671,
  bodies :112-255). Mail runs them via `%action <action string>%%`
  (LetterViewerMenu.cs:267-294; skipped from the collections tab). There is NO `%commands` in 1.6;
  the dialogue equivalent is `$action <action>` (Dialogue.cs:624-642).

**In-game checks pending** (what the decompile could not settle; Sid runs these on
`BUNKO_450391925`): (1) a garbage-can dive next to a villager — confirm radius/friendship/emote
and Linus's data-only +5; (2) one `$q` answer both ways — confirm the per-answer friendship
deltas and that the choice is not visible to the mod; (3) a movie — confirm the reaction
love/like/dislike line and emote (the reaction is never stored; a producer recomputes it from
`Data/MoviesReactions` + the date); (4) pass out once — read the rescue letter for the finder
names that live only in Data/mail; (5, optional) dump `Data/Events` keys to confirm the
`f <Npc> <points>` heart-event precondition; (6) grep `Characters/schedules/*` for Farm/FarmHouse
entries; (7) Linus's garbage special-case chat.

### Audit pass (2026-10-05)

The `VERIFY` items left by PRs #29 to #34, settled in the same decompile (game 1.6.15.24356, SMAPI
4.5.2). Data values come from the installed `Content/Data/*.xnb`, read through the game's own
content loader (see Tools).

- **`NPC.CanSocialize`** exists in 1.6: a `virtual bool` property (NPC.cs:749) that is false when
  `IsVillager` is false and otherwise evaluates `Data/Characters` `CanSocialize`, a game state query,
  with the NPC's current location (`CanSocializePerData`, NPC.cs:4873); no entry in the data means
  false, a missing query means true (CharacterData.cs:73-75). In the 1.6.15 data it is `FALSE` for the
  Bouncer, Mister Qi, Gunther, Marlon, Gil, Birdie, the Henchman, Morris, the Old Mariner, the Bear,
  the Governor, Grandpa and Welwick. **Sandy's is `PLAYER_HAS_SEEN_EVENT Any 67`**, the Oasis
  introduction ("A... customer?", `Data/Events/SandyHouse`), so she can't socialize until the player
  has met her there. Krobus, the Dwarf and the Wizard have none (true). `IsVillager` is true for every
  `NPC` and false for `Child`, `Horse`, `Junimo`, `JunimoHarvester`, `Pet`, `TrashBear` and every
  `Monster` (Character.cs:486, NPC.cs:529).
- **When `Farmer.friendshipData` gets an entry:** the first time the player interacts with a
  villager who can receive gifts (`NPC.checkAction`, NPC.cs:2512-2514), or meets them in an event
  (Event.cs:189-191). Villagers whose `SocialTab` is `AlwaysShown` (Lewis, Robin, Kent, Leo in the
  1.6.15 data) get one whenever `Game1.AddCharacterIfNecessary` runs for them (Game1.cs:7313/7349)
  and when the social page opens (SocialPage.cs:245), so for them an entry doesn't mean a meeting.
  `Farmer.hasPlayerTalkedToNPC` (Farmer.cs:4163) also adds one for a socializable name, but its
  callers only pass names already in the dictionary.
- **A spouse's schedule:** `NPC.TryLoadSchedule()` (NPC.cs:5755-5905) for a married NPC tries only
  `marriage_<season>_<day>`, then `marriageJob` (Penny Tue/Wed/Fri, Maru and Harvey Tue/Thu), then
  `marriage_<Mon..Sun>` when it isn't raining, else no schedule. The unmarried key order never runs,
  so routine priors from it would be wrong for the spouse (the mod skips the spouse).
- **`Context.IsMultiplayer` at `SaveLoaded`:** `IsMultiplayer` is `IsSplitScreen ||
  (IsWorldReady && Game1.multiplayerMode != 0)` (Context.cs:125-140). Hosting from the co-op menu sets
  `Game1.multiplayerMode = 2` before the load (`CoopMenu.HostFileSlot.Activate`, CoopMenu.cs:129),
  and SMAPI raises `SaveLoaded` only once `IsWorldReady` is true (SCore.cs:900-915), so it is true at
  `SaveLoaded` for a hosted co-op save. Split-screen started mid-session turns it on later
  (`Game1.StartLocalMultiplayerIfNecessary`, Game1.cs:3459). At the title,
  `Game1.ResetGameStateOnTitleScreen` sets `multiplayerMode = 0` (Game1.cs:16368).
- **`Context.IsMainPlayer`** is `Game1.IsMasterGame && ScreenId == 0`, and not while the title
  screen shows the farmhand menu (Context.cs:171-181). `IsMasterGame` is `multiplayerMode` 0 or 2
  (Game1.cs:1808); every network client, a split-screen guest included, sets it to 1 when it
  connects (Network/Client.cs:205). So it is false for remote farmhands and split-screen guests.
  `IsOnHostComputer` is `IsMainPlayer || IsSplitScreen` (Context.cs:142-152).
- **`Context.ScreenId`** is `Game1.game1.instanceId` (Context.cs:122). Each `Game1` takes
  `GameRunner.GetNewInstanceID()` in its constructor (Game1.cs:2343, GameRunner.cs:130-133), a
  counter from 0, so the host's screen is 0 and split-screen guests get 1, 2... (never reused).
- **`Constants.SaveFolderName`** is computed on each read from `Game1.GetSaveGameName()` and
  `Game1.uniqueIDForThisGame` whenever the load stage isn't `None` (Constants.cs:262-305). SMAPI sets
  the stage to `Ready` just before raising `SaveLoaded` (SCore.cs:914-915), so it is set there.
- **Save data on a farmhand:** both `Helper.Data.ReadSaveData` and `WriteSaveData` throw
  `InvalidOperationException` when `!Context.IsOnHostComputer`, and when no save is loaded
  (DataHelper.cs:181-226). Split-screen guests are on the host computer, so they may read and write.
- **`OneSecondUpdateTicked`** is raised from SMAPI's per-screen update (`SCore.OnPlayerInstanceUpdating`,
  SCore.cs:757 and 1155), on the game thread, once per screen in split-screen.
- **Emotes:** `Character` constants (Character.cs:29-53): empty can 4, question 8, angry 12,
  exclamation 16, heart 20, sleep 24, sad 28, happy 32, x 36, pause 40, video game 52, music note
  56, blush 60. `isEmoting` is a public field on `Character` (Character.cs:145).
- **`doEmote`** (Character.cs:1066-1088): `doEmote(int whichEmote, bool playSound, bool
  nextEventCommand = true)` (virtual), `doEmote(int whichEmote, bool nextEventCommand = true)` and
  `doEmote(int whichEmote, int emoteYOffset)`. It does nothing while the character is already
  emoting, or during an event unless the character is one of its actors. `npc.doEmote(id)` binds to
  the second.
- **`NPC.showTextAboveHead(string text, Color? spriteTextColor = null, int style = 2, int duration =
  3000, int preTimer = 0)`** (NPC.cs:1373): does nothing while the NPC is invisible, applies
  gender-switch blocks to the text, and sets the protected `textAboveHeadTimer` (NPC.cs:160).

## Tools

- **Reading structured game data** (`Data/Characters` and the like) without unpacking: a throwaway
  net6.0 console project that references `MonoGame.Framework.dll` and `StardewValley.GameData.dll`
  from the game folder can call `new ContentManager(new GameServiceContainer(), "<game>/Content")
  .Load<Dictionary<string, CharacterData>>("Data/Characters")`; no graphics device is needed for data
  assets (used for the audit pass above, 2026-10-05). Build it outside the repo.
- **SMAPI:** `GameLoop.Saving`, `DayStarted`, `DayEnding`, `helper.Data.WriteSaveData` (verified in-game, SMAPI 4.5.2 / 1.6.15). Night order seen in the SMAPI log: `DayEnding` -> the "NewDay" task -> `TimeChanged` with NewTime 600 (the date is already the new day) -> `Saving` -> `DayStarted`. So the 6:00 tick runs before the save and before `DayStarted`. The day-end notes run at `DayEnding`, which SMAPI raises before the game's `newDayAfterFade` (`SCore.cs:1357`); `Friendship.GiftsToday` is reset later, in `Farmer.updateFriendshipGifts` (`Farmer.cs:4150`), so reading it at `DayEnding` sees the day that just ended.
- **Laya checkpoints:** `laya` (English, 512 ctx), `laya-multilingual` (1,024), `laya-typed-decisions` (1,024).
- **xnbcli** (LeonBlade/xnbcli, Node): unpacks plain-XNA-type XNB files directly (dictionary data works, e.g. `Characters/schedules/*.xnb`). The native `lz4` module needs a stub (`const LZ4 = null`) when no C++ toolchain is present; LZX files (all schedule files) don't need it. Structured 1.6 data (Data/Locations etc.) uses game-specific type readers xnbcli can't resolve — StardewXnbHack handles those but runs through a real game instance (needs to run from the game folder, briefly opens a game window, writes `Content (unpacked)`).
