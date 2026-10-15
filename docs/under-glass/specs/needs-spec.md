# Needs: one named thing, asked for, promised, fetched and handed over

## Status

Built as an opt-in slice, 2026-10-13: the agreed order's step 4, follow-through ("one concrete need
→ request → agreement → attempt → outcome thread, so help has an object and a next day"). Switch
`Errands` (`--catalog errands`), off by default; with it off, in watch mode, without steering, or in a
town without an errands book, nothing here runs and every run is byte-identical to before. The
shipped town has a book; grown towns (`TownGen`) don't yet, so their pinned hashes stay put.

Not built: needs other than shop goods (a fish, a crop, something made); asking the Newcomer as the
player (the [player speech spec](player-speech-spec.md)); needs through reflection; Laya on close
calls; debts when the one who asked can't pay it back; and anyone learning, from a broken errand,
not to promise what their hours won't allow (below, "For Sid").

## Where it comes from

**Stardew** (1.6.15, the local decompile; `Quests/ItemDeliveryQuest.cs`, `Data/Shops`, gift tastes):

- The billboard's item request names one villager and one exact item, and gives two days
  (`daysLeft = 2`). Kept, it gives friendship (+150) and gold (Price × 3); expired, it is removed
  silently, with no word and no friendship lost. Only the player is ever asked: villagers never ask
  each other, and hold no inventory.
- The goods here are Stardew's own, sold where Stardew sells them: Pierre's flour, sugar, rice and
  vinegar; the Saloon's beer, coffee and bread; the chain's cola. Pierre is shut on Wednesdays, as
  his day off already is in the town (VERIFY the shop's own rule in the decompile).
- Pam loves beer; Penny hates it. The game already knows Penny won't name her mother's drinks: when
  Penny mentions what Pam loves, it redraws until it skips them (`NPC.cs`, the gossip code).

**The Sims** (recalled from published talks and play, no source; VERIFY before leaning on any one
of these): a want names a real target and comes from the Sim's own situation; chores fall within
the family; an errand is a trip with a reason; handing something over needs the two together; a
feeling toward someone carries its cause (Sims 4's sentiments); and a want that lapses is not the
same as a promise broken. Under Glass already had the explicit promise record (`Promise`), which
The Sims doesn't.

What this slice takes from each: Stardew's one item and two days, and its silent lapse for a need
nobody took on; The Sims' needs from one's own life, chores in the family, the trip, the hand-over
in person, and a broken promise that is felt.

## The rule

### A need

Each midnight, everyone with things they use (the errands book's `Uses`) and no need already may
come to need one of them (`NeedChancePerDay`, 0.1), drawn on keys of their own. They notice it at a
keyed minute between 8:00 and 20:00, so who is around to ask varies. A need waits `NeedDays` (4) for
someone to promise it, then lapses quietly, as Stardew's request does.

Who uses what (the shipped book; Stardew's gift tastes and each life): Pam and Shane beer; Harvey,
Kent and Maru coffee; Sebastian coffee and cola; George bread and coffee; Lewis bread; Evelyn and
Marnie flour and sugar; Gus flour and vinegar (he cooks); Jodi flour, rice and vinegar; Leah
vinegar; Haley sugar; Penny and Robin flour; Demetrius rice and sugar. Pierre's family (whose shop
sells most of it), the children and the Newcomer need nothing here.

### Asking

While the need is open, its holder, free and not asked in the last `ErrandAskMinutes` (60), asks
someone in reach (free, the same place, within 5 tiles and in sight, as every act toward someone
in reach needs): never someone they dislike, someone too
young to buy it (beer: 18), someone who works where it is sold, or someone who said no to them in the
last `ErrandAskAgainDays` (3). Among those, one is drawn on the bond (0.1 + `AskMath.Bond`), so
family and friends are asked first but not only.

The answer is the general `Ask` with `AskKind.Errand`:

- obliged = 0.5 × regard for the asker (if positive) + 0.3 × familiarity + 0.2 × understanding,
  + 0.2 if fond of the asker, + `ErrandKinBond` (0.2) for family or a shared home;
- cost = 0.2 + dislike × (0.5 + retention) + 0.3 × withdrawal + the asked person's own state as for
  an invitation (tired, due at work, low; shrunk by eagerness to please times the bond:
  `AskMath.StateCost`, Sid's answer of 2026-10-13) + 1 if they are already on an errand + 1 if their
  household can't pay for it.

Everything in the cost is the asked person's own knowledge: their body, their hours, what they
promised, what their household has. A margin beyond the close-call band decides; inside it, a draw
keyed by `ask`, `Errand`, the act and the asked person.

**What someone won't fetch** (the book's `WontFetch`: Penny, beer) is a no whatever the margin. Sid
(2026-10-13): "she is ashamed of her mom's alcoholism so she despises it. I think Penny is a case
where she would do anything for her mom besides give her alcohol. She tries to minimize her moms
drinking." So it is not a cost that love for the one asking can outweigh: the obligation is still
worked out and recorded (the viewer can say she'd have done anything else), and the answer is no.

- **No:** the one asked begins `Refused` toward the asker, as an invitation's no does (they take
  part, know who said no and feel it), and the asker waits before asking them again.
- **Yes:** the asker begins `AskedFavour` toward the one asked ("Pam asked Sam to fetch beer"), and
  a `Promise` is made: kind `Errand`, by the one asked, to the asker, for the thing, at its counter,
  due at the end of the day `ErrandDays` (2, Stardew's two days) after the promise. The need is
  promised.

### The attempt

Someone with a promise goes to the counter while its sign says open (`Counter`: the Store 9 to 5,
shut Wednesdays; the Saloon noon to midnight; the chain 9 to 5, shut at weekends), unless it turned
them away today, in `Decide` after work and patrol. They set out only with half an hour to spare
before it shuts; once on the way, they go on while it is open. A sign is public, so deciding to go
reads no one's true position. Work, bed and patrol come first, so a promise can wait on a working
day.

At the counter they buy it from whoever is serving (someone at work there): `Fetched`, toward the
one serving. The money moves as groceries' does: the chain's takings leave town; the store's and
the saloon's go to the keeper's household, less the restocking share that leaves town. A keeper's
own household takes it off the shelf. If nobody is serving, the sign says shut, or the household
can't pay, they try again tomorrow (`errand-shut` in the log).

### The hand-over

Carrying it, they hand it over when the two are together and free (the same place, within 8
tiles, in sight of each other): `HandedOver`, a kindness aimed at the one who asked (+0.2, plastic 0.25), so it can stir a
wish to return it; it answers the asking. The asker pays it back unless they share a home. Kept; late
if after it was due. The need is met.

Housemates meet at home. Otherwise the one carrying it goes looking, with what they know: where the
person works, during working hours, even when the workplace is a home (anyone knows the doctor is at
the clinic, Marnie at the ranch, Robin at the carpenter's), then the places they last saw them out of
doors (their own sightings, three days' worth), each place once a day for an hour. They never go
into a home to look for someone except where that person works. Each place they try is logged
(`errand-seek`). This is the roadmap's "seeking someone must use believed whereabouts".

### Broken

Not handed over by `ErrandGraceDays` (1) after it was due, the promise is broken: `LetDown`, begun
unseen (nobody watches a thing not happen), by the one who promised toward the one let down, who
knows who let them down and feels it (-0.2, plastic 0.25). As a given deed it stirs no grudge. The
need lapses. A bought thing never handed over is the runner's loss.

## Measurements

Six seeds of 28 days, the shipped town with returns, company, welcome, repair and sides on, plus
errands:

| | |
|---|---|
| needs | 281, every one promised in the end (none lapsed unasked) |
| askings | 462; yes 281 (61%) |
| within a home | 165 asked, 151 yes (92%) |
| across homes | 297 asked, 130 yes (44%) |
| promises | 261 kept in time, 3 late, 12 broken (4.3%), 5 still open at the end |
| time to keep | median 2.7 hours, 90% within 22 hours |

What the noes record (several can hold at once): barely knows the asker 82, no special reason
(regard alone) 55, tired 39, already on an errand 21, won't fetch it 8, can't pay 6, due at work 2.
Penny's eight noes to beer were all to her mother, six of them at margins of 0.80 to 0.91 (a clear
yes for anything else). Pam then asks someone else.

All 12 broken promises have the same cause: the one who promised works through the counter's hours
(Robin 7, Lewis 3 and Demetrius 2: Robin and Demetrius work 9 to 5 at home, Lewis by day in the
square, while Pierre's and the chain's signs are open), so never reaches it; none was bought and then
not handed over. The due window decides how often that happens: across three seeds of 56 days, one day
gives 8% broken and 4% late; two days (the default) 5% and 2%; three days 2.5% and 1.4%.

## For Sid

- **Penny and beer: answered** (2026-10-13, above). Before Sid's answer, beer was a cost of 0.5
  against saying yes, and Penny fetched it for her mother five times of six. Now it is a no.
- **The rest of Penny's concern: proposed, not decided.** Sid also said she tries to minimize her
  mother's drinking, and was unsure how to model it. What the town already does: Penny never spreads
  a story that hurts Pam (families cover), but she feels Pam's drunk scenes as any onlooker does,
  so her regard for her mother falls, which is the wrong shape; and kin shame only follows a
  scandal (juiciness 4 or more), and a drunk scene is news (3), so she feels no shame from it. The
  proposal is a concern, a small piece of character data (`Penny` about `Pam`: drinking) with three
  effects, each on machinery that exists: she won't enable it (no beer errands, built; never a drink
  with her or for her); seeing or hearing of it lands as shame on Penny, not coldness toward Pam; and
  she tries to keep Pam from it, either by asking her to something at home on evenings she believes
  Pam would go to the saloon (her own memory of Pam's habit, through an invitation), or, more
  cheaply, by Pam drinking less with her daughter there. *Since 2026-10-14 (D53) this no longer
  waits for needs bars, which are unresolved: the Lives pilot takes up Penny's care for her mother
  outside bars, from sourced history and lived outcomes (proposed; [Lives
  assessment](../lives-gap-assessment.md)). The beer refusal stays as built; Sid has not extended it
  to accepting or drinking alcohol, so "never a drink with her" above is part of the undecided
  proposal.*
- **Promising what your hours won't allow.** The broken promises all come from working people
  saying yes to a shop that is only open while they work. Should someone who knows their own
  hours and the shop's sign count that as "busy" when asked (a cost, as being due at work is for an
  invitation), or is saying yes and failing the more interesting outcome?
- **Debts.** The one who asked always pays it back, even when their purse is short (Pam's is often).
  An unpaid errand could become a `Tab` promise, batch 2's next kind.

## Speech

With `--speech`, both sides speak on each errand act:

- the asking (`AskedFavour` on a yes, `Refused` on a no): the asker's request (`AskedFavour`), then
  the answer (`AskedFavour.Accepted` or `.Refused`), marked `Reply`;
- the hand-over: the bringer's line (`HandedOver`, or `HandedOver.Late`), then the thanks
  (`HandedOver.Thanks`), marked `Reply`.

Lines name the thing with `{item}`, said as the book says it ("some flour", "a bottle of beer", "a
coffee"), so a line never puts its own article in front of it, and never starts a sentence with it
(it is lower case). `SpeechCatalog.Read` and the curation page refuse `{item}` in any other kind, or
at the start of a sentence. `Fetched` and `LetDown` say nothing.

The 128 errand lines were drafted the speech library's way: one author per archetype and a plain
author, 174 lines, copy-checked against the dialogue corpus, then judged blind by three readers per
line ([speech spec](speech-spec.md), "Curation rules"). 84 kept their voice and 28 plain lines were
kept as written; 16 voiced lines were demoted to plain; 46 were dropped: 38 heard as another voice,
5 for a problem two readers saw, and 3 because one reader saw they read wrongly with some goods ("the
next one's on me" said of flour), since `{item}` can be any good. They await Sid's curation like the
rest.

## Recording and the viewer

- `needs`, `favours` and `promises` in the recording, only with the switch on. A favour records the
  asker, the asked, the thing, the answer and its margin, the act that followed, and why: tired,
  busy, low, bond, eagerness, and whether it was something they won't fetch (`wontFetch`), they were
  already on an errand or couldn't pay. A promise records who, for whom, the thing, the counter, when it was due, the act that
  bought it and the act that settled it, kept, late or broken.
- The viewer words the acts with the thing ("asked Sam to fetch beer", "bought beer from Gus",
  "brought Pam the beer they asked for", "never brought Pam the beer they asked for", and a refusal
  as "turned down Pam's request for beer"), and the act detail's "Errand" line tells the story so
  far under the clock ("Sam promised Pam beer, due by Wednesday: brought late").
- Reflection's memory verbs: "asked a favour of", "bought something from", "brought something to",
  "let down".

## Tests

`ErrandTests`, in a small shop world unless a bullet says the shipped town. Ann needs tea; Bob, who
likes her well, stands near her and is the only one to ask; Cal keeps the shop, open and at work all
day. Nobody sleeps, chats or wanders, and without a road whoever sets out arrives at once. Each case
changes one thing: a wall, a road, a young Bob, a housemate, a short purse, Cal's hours, Ann leaving
for a park Bob never sees her in, Ann working at the clinic, or a fourth person, Dee.

- the switch adds four rows and the refusal's, none with a rate, and needs its rows;
- off, in watch mode or without a book, nothing runs (the shipped town for seven days, and the
  shop world);
- a need is noticed in waking hours, asked, promised, bought from Cal at the shop and handed over
  in time, with the acts in order and pointing at each other; against the same day with no need,
  Bob ends even, Ann is down the price and Cal up by it less the restocking share, and the town's
  money changes only by what left it; Ann feels the kindness;
- at the chain, the keeper's household gets nothing (the takings leave town); from the keeper's own
  shelf, nothing is paid at the counter and the asker's paying back is the shop's gain;
- with the shop shut the day it was due, it is bought the next day and handed over late;
- with the shop never open, the promise breaks a day after it was due: `LetDown`, unseen, which Ann
  knows and feels; the need lapses and no money moves;
- with Cal serving only from midnight to one, Bob is turned away once (nobody serving), doesn't go
  back that day, and buys it from Cal the next night;
- with a road, a runner who set out with the half hour to spare goes on and buys it before the sign
  shuts, though it shuts within the half hour;
- with Bob unwilling to fetch tea, the answer is no although the same asking is a clear yes when he
  is willing, with the same margin recorded, a refusal and no promise;
- the Ask weighs what the record says: a household that can't pay costs exactly 1 (and is recorded
  short); a shared home adds exactly `ErrandKinBond`; when Ann and Dee both ask Bob and the first
  promise stays open, the second asking costs exactly 1 more than the same asking alone, and is
  recorded as running;
- nobody is asked behind a wall (within five tiles), too young to buy it, while they work where it is
  sold, or while disliked;
- after a no, Ann waits `ErrandAskAgainDays` before asking Bob again, and every no is a refusal;
- a need nobody takes on lapses at its time, is never asked for after, and makes room for the next;
- looking reads what the runner knows: with Ann gone to the park just after asking, Bob looks where
  he last saw her and never sets foot in the park, and hands it over in the room when she comes back;
  housemates never go looking; with Ann working at the clinic, Bob looks in the room first, then at
  the clinic once her hours start, and hands it over there;
- in the shipped town for a month (seed 7), with where everyone stood at each tick: every favour's
  act matches its answer and its asked person was awake, in the asker's place, within five tiles and
  in sight; every hand-over was face to face (eight tiles, in sight); nobody is asked for something
  they are too young to buy; every promise's acts and need agree with its state; a bought thing was
  bought while the sign said open; nobody looks inside a home but where its person works; the counts
  of promises, hand-overs and let-downs agree; money is conserved; and no gate begins an errand act;
- across six seeds, Penny's favours for beer, and only those, are recorded as things she won't
  fetch; her mother asks, every one is a no, at least one would otherwise have been a clear yes, and
  she says yes to other favours;
- with speech on, each favour carries the request and the answer, each hand-over the bringer's line
  and the thanks, the thing is named as the book says it, and no brace is left in any line; only
  errand lines may name `{item}`, never to start a sentence, and each errand kind has plain lines;
- the same seed gives the same needs, favours and lines twice, and speech on or off the same log;
- a recording carries `needs`, `favours` and `promises` only with the switch on, with the fields the
  viewer reads;
- a town file whose book names a thing nobody sells, a good with no counter, a counter on a wall or
  hours that don't fit a day is refused, and the shipped book has no problems.

Each of these was checked by planting the defect it guards against (a runner turning back in the
last half hour, looking where someone truly is, a hand-over across town or out of sight, housemates
looking, asking out of sight or across town, the age, seller and dislike rules dropped, needs never
lapsing, the money routed wrongly, the turned-away path, the running and short costs, the kin bond,
reluctance as a cost, `{item}` opening a sentence, the book unchecked): a test fails for each.

`test-viewer-errands.cjs` holds the viewer's wording and the errand note under the clock;
`test-speech-page.cjs` that the curation page allows `{item}` only in errand lines, never to start
a sentence.

## Next

Needs beyond shop goods (a fish that needs rain, a crop in season: Stardew's own causes for "late");
asking through reflection; the player as the one asked; and remembering a let-down when deciding
whom to ask next time (today regard does that, roughly).
