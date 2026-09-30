# 1. Diary and diary enrichment

**Status: partial.** The diary exists and records three kinds (`Saw`, `TriedToReach`, `IgnoredBy`),
enrichment part 1 landed in PR #5 (`DiaryDetail`, `MemoryStore.Note`, the day-end notes `Talked`,
`PassedBy`, `BirthdayForgotten`, `Newsworthiness` and the planner's news filter and ranking), and
part 2 landed in PR #6: the read-only Harmony postfixes and the `GiftReceived`, `SawGift`,
`QuestHelped`, `Festival` and `MissedFestival` kinds. The visit/letter/romance/town-life kinds
remain (their features do not exist yet).
Brief goal 1; decisions D10 and D15; `docs/decisions.md`, "Open work".

Why this matters now: overnight lines can only be as interesting as the diary. Today almost every
line is "I saw Pierre at Pierre's General Store yesterday", because most `Saw` entries are
housemates at home (D15, "Known weakness"). Enrichment gives the planner real news, and the
newsworthiness score keeps the dull entries out.

## Player-visible behavior

- **Shadow:** the SMAPI log shows new diary lines as they are written, at Trace level:
  `[shadow] diary Haley: GiftReceived Player (item=Sunflower; taste=Love)`. Overnight lines start
  citing them: `[shadow] Haley would say: "Thank you again for the sunflower yesterday. I put it by
  the window."`.
- **Live:** nothing is shown directly. The diary is private; the player meets it only through lines
  ([intents.md](intents.md)), letters and the ladder.

## Data model

`DiaryEntry(AbsoluteTick, Subject, Kind, Detail)` is unchanged (`src/NpcMemory/Models.cs`). New kinds
use `Detail` as a small key-value string so no record grows:

```
Detail := pair (";" pair)*        pair := key "=" value
e.g.  "item=(O)421;name=Sunflower;taste=Love;birthday=0"
```

- Add `src/NpcMemory/DiaryDetail.cs` with `Parse(string) -> IReadOnlyDictionary<string,string>`
  (case-insensitive keys, unknown or malformed pairs ignored) and `Format(params (string, string)[])`
  which strips `;` and `=` from values and runs the dialogue sanitizer on them. Old kinds keep their
  plain `Detail` (a location for `Saw`, a step name for the ladder's kinds); `Parse` of a plain string
  returns an empty map, so nothing breaks.
- Display names (item names) are stored at write time, on the game thread, in the player's language,
  because the planner and renderer run off the game thread and cannot touch the game. The qualified
  item ID is stored too, so a later language change or a template can re-resolve it.
- Add `MemoryStore.Note(npc, entry)`: append and trim to `MaxDiaryEntries`. Every new writer uses it.
  Today only `Observe` trims (architecture, "Diary").

### Kinds

| Kind | Subject | Detail keys | Written when | News weight |
|---|---|---|---|---|
| `Saw` (done) | Player or NPC | plain location | a co-located span starts | see scoring |
| `TriedToReach` (done) | Player | plain step | the ladder attempts | 0 (skipped) |
| `IgnoredBy` (done) | Player | plain step | an attempt goes unanswered | 3 |
| `Talked` | Player | `hearts` | the first conversation with the player on a calendar day | 1 |
| `GiftReceived` | Player | `item`, `name`, `taste` (Love, Like, Neutral, Dislike, Hate), `birthday` (0/1) | the NPC accepts a gift from the player | Love 5, Like 3, Neutral 1, Dislike 3, Hate 4; +2 on a birthday |
| `SawGift` | the recipient NPC | `giver=Player`, `name`, `taste` | this NPC is co-located with the player when the player gives someone else a gift | 2; 3 if the NPC has 6+ hearts with the player |
| `QuestHelped` | Player | `quest` (ItemDelivery, Fishing, SlayMonster, ResourceCollection, LostItem, Special), `name` | the player completes a quest whose target is this NPC | 4 |
| `Festival` | Player | `festival` (id), `with` (0/1: talked there) | a festival day ends and the player attended it; written for every NPC who took part | 2; 3 if talked |
| `MissedFestival` | Player | `festival` | a festival day ends and the player never attended; written for NPCs with 4+ hearts | 2 |
| `PassedBy` | Player | `ticks` | the player spent 6+ ticks co-located with the NPC today, talked to at least one other NPC, and never to this one; at most once a day; only for NPCs with 2+ hearts | 2 |
| `BirthdayForgotten` | Player | `hearts` | at `DayEnding` on the NPC's birthday, if the player has 3+ hearts with it and gave it no gift that day | 4 |
| `WentLooking` | Player | `place`, `found` (0/1) | a visit trip ends ([find.md](find.md)) | 4; 2 if found |
| `AcceptedInvite` | Player | `place`, `talked` (0/1) | the NPC sees the player at the invited place in the window ([invitations.md](invitations.md)) | 4 |
| `StoodUp` | Player | `place` | an invitation's window ends unanswered | 4 |
| `MissedVisit` | Player | - | a newcomer-week visit ends without the player talking to the visitor ([newcomer-week.md](newcomer-week.md)) | 3 |
| `Traded` | Player | `offer`, `gave` | the player takes one of the NPC's trade offers ([trades.md](trades.md)) | 2 |
| `StartedDating`, `Engaged`, `Married`, `Divorced`, `Anniversary` | Player | `status` | the daily status poll sees a change, or the wedding's anniversary ([romance.md](romance.md)) | 5 |
| `ChattedWith`, `MetUpWith`, `LookedFor` | the other NPC | `place`, `seen` (0/1: the player was there) | town-life scenes ([town-life.md](town-life.md)) | 1; 2 if the player saw it |
| `HeldAGrudge` | Player | `points` | the grudge penalty applies ([motives.md](motives.md)); never shared or cited directly | 0 (skipped) |

The weights are the base for newsworthiness, below. `IgnoredBy` stays; `PassedBy`,
`BirthdayForgotten`, `StoodUp` and `MissedVisit` are the other forms of "being ignored". This table
is the registry of kinds: a feature that adds a kind adds its row here.

## Triggers and game hooks

All writers run on the game thread and write through `MemoryStore.Note`.

| Kind | Hook | Notes |
|---|---|---|
| `Talked` | existing `ModEntry.OnMenuChanged` (a `DialogueBox` with a speaker) | one per NPC per calendar day; keep a `HashSet` of NPCs talked to today, cleared at the 6:00 tick |
| `GiftReceived` | Harmony postfix on `NPC.receiveGift(Object o, Farmer giver, bool updateGiftLimitInfo = true, float friendshipChangeMultiplier = 1f, bool showResponse = true)` (1.6.15, `NPC.cs:4899`). Vanilla gifting reaches it from `NPC.tryToReceiveActiveObject` after the game's own gift limits (1 a day, 2 a week, except spouse, birthday, Stardrop Tea). The Winter Star secret gift also calls it, with `updateGiftLimitInfo: false`; record it with `festival=WinterStar`. Stardrop Tea passes false too (`NPC.cs:2403`), so the patch records Winter Star only when `updateGiftLimitInfo` is false AND the item is not `(O)StardropTea`. Items handed over for quests and special orders never reach it (those are `QuestHelped`) | the postfix only reads: `o.QualifiedItemId`, `o.DisplayName`, `npc.getGiftTasteForThisItem(o)` (`NPC.gift_taste_*`: love 0, like 2, dislike 4, hate 6, Stardrop Tea 7, neutral 8), `npc.isBirthday()`, `npc.CanReceiveGifts()`. It must not change anything. If the patch can't be applied (see Harmony below), the fallback is to diff `Game1.player.giftedItems` (`Dictionary<npc, Dictionary<itemId, count>>`, the game's own per-item gift log, updated in `Farmer.onGiftGiven`) each tick: that keeps the item, and the taste can be looked up with `getGiftTasteForThisItem` on a created item |
| `SawGift` | the same postfix | loop over the NPC diaries' current co-located pairs from the last `Observe` (the span tracker already holds them), not over live positions; this keeps rule 2 |
| `QuestHelped` | Harmony postfix on `Quest.questComplete()` (`Quest.cs:581`, the one place every quest completes; SMAPI has no quest event). Fallback: poll `Game1.player.questLog` for `completed` turning true (a quest with a money reward stays in the log, completed, until the reward is claimed) | target NPC by type: `target` on `ItemDeliveryQuest`, `SlayMonsterQuest`, `FishingQuest`, `ResourceCollectionQuest`; `npcName` on `LostItemQuest` and `SecretLostItemQuest`; `SocializeQuest` (introductions) has no single target and is skipped. Special orders: `SpecialOrder.requester` is the NPC; completion adds the order's `questKey` to `Game1.player.team.completedSpecialOrders` (a string set) in `SpecialOrder.CheckCompletion` |
| `Festival` / `MissedFestival` | at `DayEnding`, if `Utility.isFestivalDay(int day, Season season)` was true (it covers the main festivals only; passive festivals such as the Night Market are separate, `Utility.IsPassiveFestivalDay`, and run like normal days) | "attended" = `Game1.isFestival()` was true on any update tick (it reads `currentLocation.currentEvent.isFestival`); captured on `OneSecondUpdateTicked`, because no `TimeChanged` fires during a festival (see below). Who took part: the names in `Game1.CurrentEvent.actors`, captured while it runs. The actors are **clones** created for the event (`EventActor = true`), so keep names, never the objects. The Detail's `festival` value is the stable date key (`spring13`), never the localized display name from `Data/Festivals/FestivalDates`. When it ends the time jumps straight to 22:00 (one `TimeChanged` with a large gap) |
| `PassedBy` | at `DayEnding`, from today's `Saw` spans (co-located tick counts) and `Talked` entries | pure memory; no new hook |
| `BirthdayForgotten` | at `DayEnding`: `npc.isBirthday()` and no `GiftReceived` today | `isBirthday()` compares the NPC's `Birthday_Season`/`Birthday_Day`, loaded from `Data/Characters` `BirthSeason`/`BirthDay` |

### Harmony (decided 2026-09-30: use it)

Sid approved Harmony for the gift hook, knowing the wiki warns it can cause crashes, conflicts with
other mods and breakage on game updates (stardewvalleywiki.com/Modding:Modder_Guide/APIs/Harmony,
read 2026-09-30). Rules for this mod, from that page plus our own shadow-mode rules:

- **Postfixes only**, and only to observe. No prefixes that return `false`, no transpilers, no
  changing arguments or results. A postfix that only reads is the least likely to break the game or
  another mod's patch.
- **Code API, not attributes:** `var harmony = new Harmony(ModManifest.UniqueID);` then
  `harmony.Patch(original: AccessTools.Method(typeof(NPC), nameof(NPC.receiveGift)), postfix: new
  HarmonyMethod(typeof(GiftPatch), nameof(GiftPatch.Postfix)));` (the wiki's form). `nameof` makes a
  renamed method a compile error instead of a silent no-op.
- **Enable it in the csproj:** `<EnableHarmony>true</EnableHarmony>` (wiki). That only adds the
  reference: SMAPI 4.5.2 ships Harmony 2.2.2 and needs no opt-in at runtime; it just logs that the mod
  patches game code.
- **Every postfix body is a try/catch** that logs `Failed in <method>` with the exception at Error
  level and returns. Errors in patches can show up under other mods' names (wiki), so our messages
  must name this mod and the patched method.
- **All patches live in one place:** `mod/StardewNpcMod/Patches/`, one class per patched method,
  applied in `Entry` from a single `ApplyPatches()`, so one list shows everything the mod hooks.
- **If `AccessTools.Method` returns null** (the game changed), log a warning, skip that patch and
  the mod still loads. The spec's polling fallbacks for the skipped kind (diffing
  `Game1.player.giftedItems`; polling `Game1.player.questLog`) are deferred until a game update
  actually breaks a patch — the postfixes are verified against 1.6.15 today.
- **A postfix records, the tick applies.** Gifts and quests complete inside the game's update on
  the game thread, but a postfix still only queues an observation; the next tick writes it to memory,
  so there is one writer and one order.
- **Test after every game update:** the in-game gift checklist below. SMAPI's `harmony_summary`
  console command (confirmed in SMAPI 4.5.2) lists every patch per method with each mod's id, useful
  when another mod conflicts.

## Laya questions

None at write time. Diary entries are facts; the model sees them only later, in the planner's
context ([intents.md](intents.md)).

## Deterministic rules: newsworthiness

`src/NpcIntents/Newsworthiness.cs`, a pure function
`Score(DiaryEntry entry, NewsContext ctx) -> double`, used by the planner before it asks the model.

- Base weight from the kinds table.
- `Saw` entries:
  - subject is the player: 2;
  - subject is an NPC who lives with the observer (same home location: the `homes` table of
    `data/regions.json`, else the first entry of `Data/Characters` `Home`, a list of
    `{Id, Condition, Location, Tile, Direction}` where the first matching condition wins), seen at
    that home: **0** (dropped);
  - subject is an NPC somewhere unusual for it: 2. "Unusual" means the observer's belief about that
    subject has Evidence of at least 12 in the entry's block, and the region's share there is under
    0.15. Only the observer's own belief is used (memory, not the true schedule);
  - otherwise 0.5.
- +1 if the entry is about the player and the NPC has 4+ hearts.
- -1 per time the same (kind, subject) was already cited in the last 7 days (needs the persisted
  recent-lines list; [intents.md](intents.md)).
- Entries scoring below `MinNews` (2.0, raised from 1.0 after the in-game week so "nice talking"
  chit-chat never reaches the model) are not offered. If nothing is left, the NPC is
  skipped with no model call, as today.

`NewsContext` carries only memory: the observer's beliefs, home table, hearts and recent citations,
built on the game thread into the snapshot, so the planner stays game-independent.

## Tuning constants

| Name | Default | Where | Saved |
|---|---|---|---|
| kind weights | table above | `NewsworthinessOptions` | no |
| `MinNews` | 2.0 | `NewsworthinessOptions` | no |
| `UnusualShare` / `UnusualEvidence` | 0.15 / 12 | `NewsworthinessOptions` | no |
| `PassedByMinTicks` | 6 | `DiaryOptions` | no |
| hearts floors (PassedBy 2, MissedFestival 4, BirthdayForgotten 3) | as listed | `DiaryOptions` | no |
| `MaxDiaryEntries` | 500 | `MemoryStore` | no |

## Acceptance tests

Unit (`tests/NpcMemory.Tests`, `tests/NpcIntents.Tests`):
- `DiaryDetail` round-trips; values with `;`, `=` or `#$%{[` come back stripped; a plain string
  parses to an empty map.
- `MemoryStore.Note` trims at the cap for every kind.
- `PassedBy`: 6 co-located ticks plus a `Talked` with someone else writes one line; 5 ticks, or a
  `Talked` with this NPC, writes none; never twice a day.
- `BirthdayForgotten` is written only on the birthday, only above the hearts floor, only without a
  gift that day.
- Newsworthiness: a housemate at home scores 0 and is never offered; a Love gift outranks a `Saw`;
  a repeated citation loses 1 per repeat; an "unusual" `Saw` needs the evidence floor.
- Planner: with the Fake backend and mixed diaries, the NPCs with the highest-news entries are the
  ones that speak (this replaces "the first three names alphabetically").
- Serialization: new kinds survive `MemoryStore.ToJson` / `FromJson` unchanged.

In-game (test save `BUNKO_450391925`):
- Give Haley a loved gift and a hated gift to someone else: two `GiftReceived` lines with the right
  taste, and a `SawGift` for any NPC standing nearby.
- Complete a help-wanted delivery: one `QuestHelped` for the target NPC.
- Attend a festival and skip another: `Festival` and `MissedFestival` lines at day end.
- Walk past an NPC for an hour and talk to someone else: one `PassedBy`.
- The next morning, overnight lines cite these rather than housemates.

## Status

- Done: `src/NpcMemory/Diary.cs`, `MemoryStore.Observe` (`Saw`), `InitiationLadder` (`TriedToReach`,
  `IgnoredBy`); `DiaryDetail` (Parse/Format), `MemoryStore.Note`, the day-end notes (`Talked`,
  `PassedBy`, `BirthdayForgotten` via `MemoryStore.DayEndNotes`); `Newsworthiness` and the planner's
  news filter and ranking (PR #5); the Harmony kinds `GiftReceived`, `SawGift`, `QuestHelped`,
  `Festival` and `MissedFestival` via read-only postfixes plus `src/NpcDiaryEvents` producers and
  `LineRenderer` templates (PR #6); tests in `tests/NpcMemory.Tests` (153), `tests/NpcIntents.Tests`
  (138) and `tests/NpcDiaryEvents.Tests`.
- Not started: the recent-citations wiring (`RecentCitations` is always empty until intents step
  5); the polling fallbacks if a patched method disappears after a game update; the
  visit/letter/romance/town-life kinds (their features do not exist yet).

## Open questions

- ~~The `Talked` hook is any dialogue box with a speaker, so cutscene and festival dialogue also
  counts as "talked to" (and feeds `PassedBy`).~~ Decided (PR #6): festival conversations still
  count as `Talked` — they ARE conversations — and additionally feed the Festival `with` key.
- `MissedFestival` is written for every 4+ heart villager, including ones who never appear at the
  festival in question (Krobus, the Wizard, the Dwarf...). "You missed the festival" from them
  reads oddly; filtering by the festival's actor data is a possible fix if it bothers anyone.
- Should NPCs record things the player does alone that they could see (fishing, tilling, dumpster
  diving)? The game already reacts to dumpster diving (`stardew-source-notes.md`). Deferred: every
  new kind needs its own template set and a reason to exist in a line.
- Gifts from NPCs to NPCs are not a game mechanic; nothing to record.
