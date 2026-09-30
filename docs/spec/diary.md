# 1. Diary and diary enrichment

**Status: partial.** The diary exists and records three kinds (`Saw`, `TriedToReach`, `IgnoredBy`).
Enrichment (conversations, gifts, quests, festivals, being passed by, newsworthiness) is not started.
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
| `QuestHelped` | Player | `quest` (ItemDelivery, Fishing, SlayMonster, ResourceCollection, Special), `name` | the player completes a quest whose target is this NPC | 4 |
| `Festival` | Player | `festival` (id), `with` (0/1: talked there) | a festival day ends and the player attended it; written for every NPC who took part | 2; 3 if talked |
| `MissedFestival` | Player | `festival` | a festival day ends and the player never attended; written for NPCs with 4+ hearts | 2 |
| `PassedBy` | Player | `ticks` | the player spent 6+ ticks co-located with the NPC today, talked to at least one other NPC, and never to this one; at most once a day; only for NPCs with 2+ hearts | 2 |
| `BirthdayForgotten` | Player | `hearts` | at `DayEnding` on the NPC's birthday, if the player has 3+ hearts with it and gave it no gift that day | 4 |
| `WentLooking` | Player | `place`, `found` (0/1) | a visit trip ends ([find.md](find.md)) | 4; 2 if found |
| `AcceptedInvite` | Player | `place`, `talked` (0/1) | the NPC sees the player at the invited place in the window ([invitations.md](invitations.md)) | 4 |
| `StoodUp` | Player | `place` | an invitation's window ends unanswered | 4 |
| `MissedVisit` | Player | - | a newcomer-week visit ends without the player talking to the visitor ([newcomer-week.md](newcomer-week.md)) | 3 |

The weights are the base for newsworthiness, below. `IgnoredBy` stays; `PassedBy`,
`BirthdayForgotten`, `StoodUp` and `MissedVisit` are the other forms of "being ignored". This table
is the registry of kinds: a feature that adds a kind adds its row here.

## Triggers and game hooks

All writers run on the game thread and write through `MemoryStore.Note`.

| Kind | Hook | Notes |
|---|---|---|
| `Talked` | existing `ModEntry.OnMenuChanged` (a `DialogueBox` with a speaker) | one per NPC per calendar day; keep a `HashSet` of NPCs talked to today, cleared at the 6:00 tick |
| `GiftReceived` | Harmony postfix on `NPC.receiveGift(Object o, Farmer giver, bool updateGiftLimitInfo, float friendshipChangeMultiplier, bool showResponse)` (signature from 1.5.6 notes: verify in 1.6) | the postfix only reads: `o.QualifiedItemId`, `o.DisplayName`, `npc.getGiftTasteForThisItem(o)` (returns 0 love, 2 like, 4 dislike, 6 hate, 8 neutral: verify), `npc.isBirthday()` (verify). It must not change anything. If the patch can't be applied (see Harmony below), the fallback is to poll `Game1.player.friendshipData[npc].GiftsToday` on every `MenuChanged` and tick and record a gift with no item when it rises; this loses the item and the taste, which is most of the news value |
| `SawGift` | the same postfix | loop over the NPC diaries' current co-located pairs from the last `Observe` (the span tracker already holds them), not over live positions; this keeps rule 2 |
| `QuestHelped` | poll `Game1.player.questLog` at each tick and on `MenuChanged`; a quest that moved to completed since the last poll, with a target NPC, is recorded | quest types and their target field (`ItemDeliveryQuest.target`, `SlayMonsterQuest.target`, `FishingQuest.target`, `ResourceCollectionQuest.target`: verify). Special orders: `Game1.player.team.completedSpecialOrders` gains an id; the order's `requester` is the NPC (verify) |
| `Festival` / `MissedFestival` | at `DayEnding`, if the day was a festival day (`Utility.isFestivalDay(day, season)`: verify) | "attended" = the player entered the festival (set a flag when `Game1.CurrentEvent?.isFestival` is true on any update tick: verify). Who took part: the actors of that festival event, captured while it runs (`Game1.CurrentEvent.actors`: verify). `TimeChanged` probably does not fire while the festival runs (the clock is frozen: verify), so ordinary `Saw` lines are not written there |
| `PassedBy` | at `DayEnding`, from today's `Saw` spans (co-located tick counts) and `Talked` entries | pure memory; no new hook |
| `BirthdayForgotten` | at `DayEnding`: `npc.isBirthday()` and no `GiftReceived` today | birthday from `Data/Characters` (verify) |

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
- **Enable it in the csproj:** `<EnableHarmony>true</EnableHarmony>` (wiki).
- **Every postfix body is a try/catch** that logs `Failed in <method>` with the exception at Error
  level and returns. Errors in patches can show up under other mods' names (wiki), so our messages
  must name this mod and the patched method.
- **All patches live in one place:** `mod/StardewNpcMod/Patches/`, one class per patched method,
  applied in `Entry` from a single `ApplyPatches()`, so one list shows everything the mod hooks.
- **If `AccessTools.Method` returns null** (the game changed), log a warning, skip that patch and
  fall back to the polling approach for that kind. The mod must still load.
- **A postfix never touches memory directly** if it could run off the game thread (verify that
  `receiveGift` only runs on the game thread); it queues an observation that the next tick applies.
- **Test after every game update:** the in-game gift checklist below. SMAPI's `harmony_summary`
  console command lists every patch on a method (recalled: verify), useful when another mod
  conflicts.

## Laya questions

None at write time. Diary entries are facts; the model sees them only later, in the planner's
context ([intents.md](intents.md)).

## Deterministic rules: newsworthiness

`src/NpcIntents/Newsworthiness.cs`, a pure function
`Score(DiaryEntry entry, NewsContext ctx) -> double`, used by the planner before it asks the model.

- Base weight from the kinds table.
- `Saw` entries:
  - subject is the player: 2;
  - subject is an NPC who lives with the observer (same home location in the `homes` table of
    `data/regions.json`, or `Data/Characters` `Home`: verify), seen at that home: **0** (dropped);
  - subject is an NPC somewhere unusual for it: 2. "Unusual" means the observer's belief about that
    subject has Evidence of at least 12 in the entry's block, and the region's share there is under
    0.15. Only the observer's own belief is used (memory, not the true schedule);
  - otherwise 0.5.
- +1 if the entry is about the player and the NPC has 4+ hearts.
- -1 per time the same (kind, subject) was already cited in the last 7 days (needs the persisted
  recent-lines list; [intents.md](intents.md)).
- Entries scoring below `MinNews` (1.0) are not offered to the model. If nothing is left, the NPC is
  skipped with no model call, as today.

`NewsContext` carries only memory: the observer's beliefs, home table, hearts and recent citations,
built on the game thread into the snapshot, so the planner stays game-independent.

## Tuning constants

| Name | Default | Where | Saved |
|---|---|---|---|
| kind weights | table above | `NewsworthinessOptions` | no |
| `MinNews` | 1.0 | `NewsworthinessOptions` | no |
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
  `IgnoredBy`); tests in `tests/NpcMemory.Tests/DiaryTests.cs`.
- Not started: `DiaryDetail`, `MemoryStore.Note`, every new kind, `Newsworthiness`.

## Open questions

- Does `TimeChanged` fire during festivals? If it does, festival `Saw` lines already exist and the
  `Festival` kind should replace them for that day.
- Should NPCs record things the player does alone that they could see (fishing, tilling, dumpster
  diving)? The game already reacts to dumpster diving (`stardew-source-notes.md`). Deferred: every
  new kind needs its own template set and a reason to exist in a line.
- Gifts from NPCs to NPCs are not a game mechanic; nothing to record.
