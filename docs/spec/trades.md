# 17. Trades

**Status: not started.** Sid's idea (2026-09-30): characters offer trades, like a Minecraft villager,
and wanting to trade gives them another reason to seek the player out. The NPC doesn't use what it
gets; the trade is a small reward loop and a reason to visit.

## The game already has this

Checked in the 1.6.15 decompile: `Data/Shops` supports barter. A shop item with `TradeItemId` and
`TradeItemAmount` costs that item instead of money (`ShopBuilder` sets the price to 0 when a trade
item is set). The Desert Trader (`DesertTrade`) and Island Trader (`IslandTrade`) are vanilla shops
built this way. Shop items also have `AvailableStock` with a per-player or global limit, a
`Condition`, and `ActionsOnPurchase` (trigger actions run on purchase).
`Utility.TryOpenShopMenu(string shopId, string ownerName)` opens a shop for any NPC wherever they
stand. So trades need no new UI: each character gets its own small barter shop.

## Player-visible behavior

- Some days a character has one to three trade offers: "3 Sardines for a Wild Bait bundle" from
  Willy, "5 Copper Ore for a Geode" from Clint, "a Leek for 10 Parsnip Seeds" from Pierre. Offers suit
  the character and the season.
- **Talking to a character with offers:** after their normal dialogue, a question box asks "Want to
  see what <npc> has to trade?" Yes opens their barter shop with that day's offers. Each offer can be
  taken once (stock 1).
- **Seeking out:** a character with a good offer wants to trade (`WantsToTrade` motive,
  [motives.md](motives.md)), so it may wave, send "I've got something you might like" by letter, or
  come find the player, through the normal ladder and visit rules.
- Taking a trade makes the character a little grateful (a `Traded` diary entry), which feeds lines
  ("That trout was perfect, by the way").
- Shadow: `[shadow] Willy would offer: 3 Sardine -> 1 Wild Bait x5 (wants it 0.7)`.

## Data model

- **`data/trades.json`** (D20: a table; the only source of trade items, like the newcomer gifts):
  per NPC, a list of possible offers:
  ```json
  { "Willy": [ { "id": "sardine-bait", "want": "(O)131", "wantCount": 3, "give": "(O)774", "giveCount": 5,
                 "seasons": ["spring", "summer", "fall"], "minHearts": 0 } ] }
  ```
  (The ids in the example are illustrative.) Item ids are qualified ids, checked against `Data/Objects` by a test at build time and at load
  (unknown ids are dropped with a warning, so other mods' removed items can't break it).
- **Balance rule:** the given items' total sell price is between 80% and 130% of the wanted items'
  (a test enforces it on the table), so trades are convenient, not an exploit.
- **Today's offers** (saved under a new save-data key `trades`, [persistence.md](persistence.md)):
  per NPC, the offer ids chosen for today and whether each was taken.
- Shop ids: `squid.StardewNpcMod_Trade_<Npc>`, added to `Data/Shops` by `AssetRequested` with one
  `ShopItemData` per offer (`TradeItemId`, `TradeItemAmount`, `AvailableStock` 1,
  `AvailableStockLimit` per player), an `Owners` entry for the NPC, and
  `ActionsOnPurchase: ["squid.StardewNpcMod_Traded <npc> <offer id>"]`.
- Diary kind `Traded` (subject Player; detail `offer=...;gave=...`; news weight 2).

## Triggers and game hooks

| When | What |
|---|---|
| 6:00 tick | pick today's offers (below), invalidate `Data/Shops` |
| `AssetRequested` for `Data/Shops` | add each character's barter shop with today's offers |
| `MenuChanged` closing a `DialogueBox` from an NPC with offers not yet declined today | `Game1.currentLocation.createQuestionDialogue(question, responses, afterQuestion, npc)`; on yes, `Utility.TryOpenShopMenu(shopId, npcName)` |
| a trade | our trigger action, registered at `Entry` with `TriggerActionManager.RegisterAction("squid.StardewNpcMod_Traded", ...)`, marks the offer taken and queues a `Traded` diary line |

No Harmony needed: the purchase hook is the game's own trigger action system.

## Laya questions

| Question | Type | Fallback |
|---|---|---|
| "Which of these would <npc> most want today?" | `choice` over the NPC's eligible offers (season, hearts), at most 5 | uniform |
| "Does <npc> feel like trading today?" | `noul` | 0.5 |

Asked on a background task at the 6:00 tick; offers appear once the answers arrive (well before
the player can reach anyone).

## Deterministic rules

- Eligible offers: the season matches, hearts >= `minHearts`, not taken in the last `RepeatDays` (7).
- Up to `MaxOffersPerNpc` (3) offers a day, chosen by the model's probabilities with a seeded draw;
  at most `MaxTradersPerDay` (4) characters trade on a day, the highest yes/no first.
- `WantsToTrade` strength = the chosen offer's want weight (from the table, 0..1) x 0.8 per day the
  offer has gone untaken; offers reset each day.
- Closed characters: no offers on festival days, and none for NPCs the player has never talked to
  (they'd have no reason to trade with a stranger).
- The question box appears at most once per character per day; "no" hides it until tomorrow.

## Tuning constants

`MaxOffersPerNpc` 3, `MaxTradersPerDay` 4, `RepeatDays` 7, balance band 80-130%. Not saved.

## Acceptance tests

- Table validation: every id exists, every offer is inside the balance band, no NPC without offers
  is picked.
- Offer picking: season and hearts respected, no repeats within a week, caps hold, deterministic by
  seed.
- The generated `ShopData` has barter items with stock 1 and our trigger action; the trigger action
  marks the offer taken and writes one `Traded` line.
- In-game: talk to Willy with an offer, accept, trade sardines for bait, the stock empties, the
  diary gets `Traded`, and the question doesn't come back that day.

## Status

Not started. Needs a live switch (`Live.Trades`) and Sid's go-ahead, since it gives items (through
the vanilla shop, from our table).

## Open questions

- Who writes the offer table? Recommendation: Claude drafts 3-6 offers per villager from their
  character (Willy fish and tackle, Clint ores and geodes, Leah forage and art), Sid edits.
- Should characters also want things by letter ("Could you bring me 5 Clay?") as a quest instead of a
  shop? That is the vanilla quest path ([invitations.md](invitations.md), "Later"); trades are the
  lighter version.
