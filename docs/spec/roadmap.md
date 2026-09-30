# Roadmap

What to build next, in order, and the decisions only Sid can make. Each step is one or more PRs off
`main`, with tests and doc updates (`AGENTS.md`, "Workflow"). Sizes: **S** is one small PR, **M** a
few days of agent work, **L** several PRs.

## Where things stand

Steps 1-9 of the brief are done in shadow mode (386 tests at `c97a829`). DeepSeek's PR #3 (console
heartbeat and plan-collection logging) is open. Nothing is live. Laya has never answered a real
question. The overnight lines are dull because the diary only knows who saw whom (D15).

## Order

| # | Step | Size | Spec | Depends on |
|---|---|---|---|---|
| 1 | **Diary enrichment, part 1:** `DiaryDetail`, `MemoryStore.Note`, `Talked`, `PassedBy`, `BirthdayForgotten`, newsworthiness in the planner (drops housemates at home, ranks by news) | M | [diary.md](diary.md), [intents.md](intents.md) | - |
| 2 | **Diary enrichment, part 2:** `GiftReceived` and `SawGift` (Harmony, decision 1), `QuestHelped`, `Festival`/`MissedFestival` | M | [diary.md](diary.md) | 1 |
| 3 | **Laya in practice (shadow):** `DecisionState` and the NPC card within the 512-token budget, the varied fake, health re-check and short-circuit, budget pass-through, batching, the eval set and the checkpoint A/B | M | [laya.md](laya.md) | 1 (for real news in the eval) |
| 4 | **Text bank:** `data/lines.json`, tone buckets, per-channel sanitizers, lengths, a first draft of every template | M | [text.md](text.md) | 1, 2 (the kinds to write for) |
| 5 | **Intents ready to ship:** plan persistence, recent lines, novelty and cite cooldowns, one topic per event | S | [intents.md](intents.md), [persistence.md](persistence.md) | 1 |
| 6 | **First live release:** `Live` config, `[live]` logs, circuit breaker, console kill switch, multiplayer guard, then `IntentLines` on (decision 2) | M | [rollout.md](rollout.md), [multiplayer-compat.md](multiplayer-compat.md) | 4, 5, and Sid's word |
| 7 | **Routine and gossip fixes:** `UnlockThreshold` not saved, decay, family priors, ambient gossip and `Heard` | M | [routines.md](routines.md), [ledger-gossip.md](ledger-gossip.md) | - (can run in parallel with 3-6) |
| 8 | **Ladder live, low rungs:** `Emote`, `Bubble`, `QueuedLine`, then `Mail` (asset editing, mail sanitizer), temperament floor, `LiveGate` | M | [ladder.md](ladder.md) | 6 |
| 9 | **Newcomer week** (shadow, then live on a fresh save) | L | [newcomer-week.md](newcomer-week.md) | 7 (spreading), 8 (mail) |
| 10 | **Day length** | S | [day-length.md](day-length.md) | none; can be done any time |
| 11 | **Movement:** `ApproachNear` (schedule restore experiment first), then `ForcedDialogue` | M | [ladder.md](ladder.md) | 8 |
| 12 | **Far travel:** `ApproachFar`, NPCs looking for each other | L | [find.md](find.md) | 11; may be cut (decision 5) |

### Why this order, and where it differs from the default

The starting order was Diary enrichment, then live Laya, then newcomer week and day length. Kept:
**diary first**, because every later step feeds on it. Laya can only be judged on interesting
choices (with today's diaries the right answer is nearly always "nothing worth saying"), the
templates need to know which kinds exist, and the first live feature is only worth shipping if the
lines say something.

Changed:
- **Laya stays third, but "live Laya" means running it in shadow**, not making anything live. It
  comes after diary part 1 so its eval set uses real news. Sid can install the sidecar any time
  (`sidecar/README.md`); nothing in the mod needs to wait for it.
- **A first live release (steps 4-6) comes before newcomer week.** Newcomer week needs mail, item
  gifts and placing an NPC on the farm, which are three live mechanisms at once. Shipping the
  simplest live feature first (planned lines, heard only when the player talks) proves the live
  switches, the sanitizers and the kill switch on something low-risk.
- **Day length is not tied to newcomer week.** It is independent, small and a gameplay change rather
  than NPC behavior, so it can be done whenever someone has a spare slot (a good task for DeepSeek)
  and stays off by default.
- **Routine and gossip fixes (7)** are a parallel track: they touch `src/NpcMemory` only, and newcomer
  week needs ambient gossip.

## Decisions only Sid can make

Each is a short question with a recommendation. Work proceeds on the recommendation until Sid says
otherwise.

1. **May the mod use Harmony, read-only, to see gifts (item and taste)?** Recommended: yes. Without
   it the diary knows a gift happened but not what it was, which is most of the news value.
2. **What goes live first?** Recommended: overnight lines only (`IntentLines`), then emotes a week
   later.
3. **Newcomer week on existing saves too?** Recommended: new saves only, plus a console command to
   trigger it for testing.
4. **Day length default?** Recommended: off (vanilla); Sid tries 20 minutes, then decides on 24.
5. **Should NPCs ever walk to another location to look for the player?** Recommended: keep it in
   shadow and decide after the in-location approach is live; letters already cover "I was looking
   for you".
6. **Should the mod start the Laya server itself?** Recommended: no, keep it manual for now.
7. **Is multiplayer in scope?** Recommended: single-player only; farmhands disable the mod, and the
   host ignores other farmers.
8. **Who writes the line templates?** Recommended: Claude drafts every kind and tone in
   `data/lines.json`, and Sid edits them directly.
