# Stardew Source Notes

"Verified" means read in source or docs in this project's research. "Recall" means remembered, not confirmed, so check it.

## Sources

- Decompiled 1.5.6: https://github.com/WeDias/StardewValley (NPC.cs, 5,954 lines)
- Decompiled 1.6: https://github.com/Dannode36/StardewValleyDecompiled (NPC.cs 7,154 lines, Game1.cs 16,245 lines)
- Wiki: Modding:Dialogue, Modding:NPC data, Modding:Schedule data, Friendship, Marriage (stardewvalleywiki.com)
- TypeSafe announcement: typesafe.ai/blog/introducing-system-one-models-and-jev
- Laya: github.com/NandhaKishorM/laya and huggingface.co/convaiinnovations/laya

## What NPCs store about the player

**Per NPC and player (`Farmer.friendshipData[npcName]`), verified in NPC.cs:** `Points`, `Status`, `IsDating`, `IsMarried`, `IsDivorced`, `ProposalRejected`, `GiftsToday`, `GiftsThisWeek`, `LastGiftDate`, `TalkedToToday`. Each heart is 250 points.

**Player-side fields NPC.cs reads:** `activeDialogueEvents` (topic to days left), `eventsSeen`, `mailReceived`. The game uses `mailReceived` as a per-NPC "already said" flag, stored as `<NPC>_<topic>`.

**Per-NPC fields:** age, manners, socialAnxiety, optimism, gender, datable, birthday, homeRegion, lastSeenMovieWeek, hasSaidAfternoonDialogue, hasBeenKissedToday, sleptInBed, isSleeping.

**Not found in NPC.cs:** any history of conversations, and the per-item gift log (recall: it lives on the Farmer class).

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

## Tools

- **SMAPI:** `GameLoop.Saving`, `DayStarted`, `DayEnding`, `helper.Data.WriteSaveData` (verified in-game, SMAPI 4.5.2 / 1.6.15). Night order seen in the SMAPI log: `DayEnding` -> the "NewDay" task -> `TimeChanged` with NewTime 600 (the date is already the new day) -> `Saving` -> `DayStarted`. So the 6:00 tick runs before the save and before `DayStarted`.
- **Laya checkpoints:** `laya` (English, 512 ctx), `laya-multilingual` (1,024), `laya-typed-decisions` (1,024).
- **xnbcli** (LeonBlade/xnbcli, Node): unpacks plain-XNA-type XNB files directly (dictionary data works, e.g. `Characters/schedules/*.xnb`). The native `lz4` module needs a stub (`const LZ4 = null`) when no C++ toolchain is present; LZX files (all schedule files) don't need it. Structured 1.6 data (Data/Locations etc.) uses game-specific type readers xnbcli can't resolve — StardewXnbHack handles those but runs through a real game instance (needs to run from the game folder, briefly opens a game window, writes `Content (unpacked)`).
