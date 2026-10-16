// Builds ../lives-observer-example-v0.2.json: an ILLUSTRATIVE fixture for the proposed Lives observer recording
// contract v0.2 (../lives-observer-recording-v0.2.md). Evolved from the v0.1 generator; nothing is implemented.
//
// What is computed, not typed:
// - fallback weights and probabilities, from the proposed rules 5 and 7 (the assessment's first guesses);
// - every keyed draw: unit = Rng.Unit(seed, "lives-choice", decision id), as rng.cjs computes it (checked against
//   a compiled copy of sim/UnderGlass.Sim/Rng.cs). SEED is the smallest seed whose draws give this story
//   (--find-seed searches again; the build stops if the story no longer holds). A real run never chooses its seed;
// - expectation states, the disappointment, the option-set and request hashes, the archetype snapshot's hash,
//   Balanced evaluation means, the comparisons' arms;
// - element order (seq): creation order, then causes before effects within a minute.
// What is invented and says so: Laya probabilities, prompts and responses (placeholders), thoughts, receipts,
// act and need ids, coordinates, states such as tiredness, walking times, Pam's arrival at the Saloon, Penny's
// afternoon place (a sim guess).
// The days: Wednesday summer 3 (day 30) and Thursday summer 4 (day 31). On Penny's sourced routine, in a dry year, her
// evenings at home and awake through the 18-21 band are summer Mondays, Thursdays and Sundays (fixtures/game/Penny.json,
// key "summer": home from 18:00, leaving for bed at 21:00; besides winter 4 from 19:00, and the 9th and 23rd until
// 18:40); her bedtime is a habit, not a rule (Sid, 2026-10-14).
//
// Usage: node build-example.cjs ../lives-observer-example-v0.2.json   |   node build-example.cjs --find-seed
"use strict";
const fs = require("fs");
const crypto = require("crypto");
const { hash, unit } = require("./rng.cjs");

const argv = process.argv.slice(2);
const OUT = argv.find((a) => !a.startsWith("--"));
const FIND = argv.includes("--find-seed");
if (!OUT && !FIND) { console.error("usage: node build-example.cjs <out.json> | --find-seed"); process.exit(2); }

const SEED = 7315; // found by --find-seed: the smallest seed whose keyed draws give this story
const r4 = (x) => Math.round(x * 10000) / 10000;
const K = 5;
const PRE = "pre-run";
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const fnv = (v) => "fnv1a64:" + hash([canon(v)]).toString(16).padStart(16, "0");
const sha256 = (s) => crypto.createHash("sha256").update(s, "utf8").digest("hex");
const slug = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

// ---- clock: day 0 is spring 1, a Monday; minutes from midnight of day 0 ----
const DAY = 1440;
const at = (d, h, m) => d * DAY + h * 60 + m;
const FROM = at(30, 10, 0), TO = at(31, 21, 0); // Wednesday summer 3, 10:00 to Thursday summer 4, 21:00
const DAY_NAMES = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], SEASONS = ["spring", "summer", "fall", "winter"];
const dayOf = (d) => ({ day: d, season: SEASONS[Math.floor(d / 28) % 4], date: (d % 28) + 1, weekday: DAY_NAMES[d % 7] });
const RUN_DAYS = 35; // a pilot run lasts at least 35 days (the assessment's corrections note: sim days 28, 31 and 34 in reach)

// ---- the base excerpt's people and places ----
const names = ["Gus", "Pam", "Penny"];
const GUS = 0, PAM = 1, PENNY = 2;
const places = ["Home:Trailer", "Saloon", "Square", "Store"];
const P = Object.fromEntries(places.map((p, i) => [p, i]));
const kinds = ["Refused"];

// ---- settings (proposed LivesOptions; the assessment's first guesses) ----
const settings = { k: K, intended: 0.2, seedStrength: 0.05, disappointment: 0.05, disappointmentScale: 0.25, tornAt: 0.04,
  planBase: 0.1, displacedHabit: 0.4, clearBand: 0.15, waitMinutes: 60, livedWindow: 6, associationsPerObject: 3,
  derivedReadingAfter: 3, sourcedCue: 2, recall: { top: 3, reservePreRun: 1, lowMoodExtra: 1, max: 4, runnersUpShown: 2 },
  modelQuestionsPerPersonDay: 2 };

// ---- the proposed rules the fallback computes (first guesses) ----
// rule 5: weight = base x chance x e^(k (warm - cold (1 - boldness) + intended - cost))
function rule5(c) {
  const chance = c.chance ? c.chance.whereabouts * c.chance.answers * c.chance.followThrough : 1;
  return c.base * chance * Math.exp(K * (c.warm - c.cold * (1 - c.boldness) + c.intended - c.cost));
}
// rule 7: yes e^(k margin), no e^(-k margin), not now e^(k reason) while a reason holds; the reason sized as the
// Ask's own state cost (our reading, E)
function rule7(c) {
  if (c.answer === "yes" || c.answer === "no") return Math.exp((c.answer === "yes" ? K : -K) * c.margin);
  if (c.answer === "not-now") return Math.exp(K * c.reason.value);
  throw new Error("rule7: " + c.answer);
}
const weightOf = (c) => (c.formula === "rule5" ? rule5(c) : rule7(c));
// The Ask's state cost (Simulation.Ask.cs:30-31): 0.3 tired + 0.3 busy + 0.3 low, times (1 - eagerness x bond)
const stateCost = (s, eager, bond) => r4(0.3 * (s.tired + s.busy + s.low) * (1 - eager * bond));
const eagerness = (t) => ((1 - t.selfRegard) + (1 - t.boldness)) / 2; // Simulation.Ask.cs:20
const bond = (family, liked, fam) => (family ? 1 : Math.min(1, Math.max(0, 0.5 * liked + 0.5 * fam))); // :24-25
// Cards (DefaultTown.cs:430, :458, :461; Temperament order Model.cs:55): chattiness, boldness, understanding, self-regard
const TRAITS = { Gus: { chattiness: 0.8, boldness: 0.5, understanding: 0.6, selfRegard: 0.6 },
  Pam: { chattiness: 0.8, boldness: 0.7, understanding: 0.3, selfRegard: 0.3 },
  Penny: { chattiness: 0.4, boldness: 0.2, understanding: 0.7, selfRegard: 0.3 } };
const EAGER = Object.fromEntries(Object.entries(TRAITS).map(([n, t]) => [n, eagerness(t)]));
function normalise(ws) {
  const s = Object.values(ws).reduce((a, b) => a + b, 0);
  return Object.fromEntries(Object.entries(ws).map(([k, v]) => [k, r4(v / s)]));
}
function pick(order, probs, u) {
  let x = u;
  for (const id of order) { x -= probs[id]; if (x < 0) return id; }
  return order[order.length - 1];
}
const altId = (dec, s) => `alt:${dec.slice(4)}/${s}`;
// The three rule-7 answers to one ask, from the Ask's parts.
// costs: base, state and errand are the Ask's own parts (Simulation.Ask.cs); displacedHabit (rule 11) and working (rule
// 16's open point) are Lives-only. Not now's reason is sized as the state the asked person is in: the Ask's state cost
// plus any Lives-only working term.
const ASK_PARTS = ["base", "state", "errand"];
const askMargin = (c) => r4(c.obliged - ASK_PARTS.reduce((s, k) => s + (c.costs[k] ?? 0), 0)); // what the base row records
function answerComponents(a) {
  const margin = r4(a.obliged - Object.values(a.costs).reduce((s, x) => s + x, 0));
  return {
    yes: { formula: "rule7", answer: "yes", obliged: a.obliged, costs: a.costs, margin },
    "not-now": { formula: "rule7", answer: "not-now", reason: { code: a.reason, value: r4(a.costs.state + (a.costs.working ?? 0)) } },
    no: { formula: "rule7", answer: "no", margin },
  };
}

// ---- the decisions' fallback numbers, needed before the seed is known ----
const DF = "dec:44295:Pam:0", D1 = "dec:45190:Penny:0", D2 = "dec:45191:Pam:0", D3 = "dec:45688:Pam:0",
  D4 = "dec:45708:Penny:0", D6 = "dec:45740:Pam:0", D7 = "dec:45740:Gus:0";
// Pam's beer ask to Penny at 09:10 on a Thursday: the Ask(Errand) parts (Simulation.Ask.cs:88-92 [#90]). Regard 0.6,
// familiarity 0.8 (the household seeds), understanding 0.7 (her card), ErrandKinBond 0.2: obliged 0.88, against the base
// 0.2 and her state. In the sim her weekday lessons start at 10:00 and she leaves at about 09:30, within
// InviteBusyMinutes (60), so AskStateOf's busy is 1 (Simulation.Invitations.cs:55-63): state 0.3 x 1 x (1 - 0.75 x 1).
// Canon has no Thursday lessons (busy 0, margin 0.68): an implementation ambiguity, named in the fixture.
const pennyState0910 = { tired: 0, busy: 1, low: 0 };
const BEER = { obliged: r4(0.5 * 0.6 + 0.3 * 0.8 + 0.2 * 0.7 + 0.2), base: 0.2, state: stateCost(pennyState0910, EAGER.Penny, 1), errand: 0 };
const REFUSED_MARGIN = r4(BEER.obliged - BEER.base - BEER.state - BEER.errand);
const pamState0911 = { tired: 0, busy: 0, low: 0.1333 }; // illustrative: low after the refusal
const pamState1815 = { tired: 0.1, busy: 0, low: 0.02 }; // illustrative, Wednesday evening
// Expectations: chance = (successes + 1) / (entries + 1) (rule 9).
const OK = ["found", "yes", "together", "free"];
const chanceOf = (entries) => r4((entries.filter((e) => OK.includes(e.outcome)).length + 1) / (entries.length + 1));
// Penny's key for Pam at home in the evening band on weekdays: p4's seeded 'away' and one lived 'away' from summer 1, a
// Monday (day 28), the first evening her sourced routine had her at home and awake through the band (rule 14, seen
// absent: Pam was at the Saloon). The key then stands below 0.5, so rule 14 stops counting her absence. Whether day 28
// was such an evening in the pilot depends on actual sleep (the energy rule), not on the habit: ours (E).
const PENNY_WHERE_LIVED = [{ at: at(28, 21, 0), outcome: "away", source: "Seen" }];
const PENNY_WHERE_CHANCE = chanceOf([{ outcome: "away", source: "Reading" }, ...PENNY_WHERE_LIVED]);
const gusState1820 = { tired: 0, busy: 0, low: 0 }; // AskStateOf (Simulation.Invitations.cs:59-61): busy only when leaving for work is due within 60 minutes; mid-shift is 0
// Lives-only (proposed, contingent on rule 16's open point): mid-shift read as a duty in hand, sized like the Ask's busy term.
const GUS_WORKING = 1;
const pamGusBond = bond(false, 0.4, 0.5), gusPamBond = bond(false, 0.4, 0.5);
const C = {
  [DF]: answerComponents({ obliged: r4(0.5 * 0.4 + 0.3 * 0.5 + 0.2 * 0.3), costs: { base: 0.2, state: stateCost(pamState1815, EAGER.Pam, pamGusBond), errand: 0 }, reason: "tired" }),
  [D1]: {
    offer: { formula: "rule5", base: REFUSED_MARGIN, chance: { whereabouts: PENNY_WHERE_CHANCE, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0.05, boldness: 0.2, intended: 0, cost: 0 },
    "keep-evening": { formula: "rule5", base: 1, warm: 0, cold: 0, boldness: 0.2, intended: 0, cost: 0 } },
  [D2]: answerComponents({ obliged: 0.9, costs: { base: 0.15, state: stateCost(pamState0911, EAGER.Pam, 1), displacedHabit: 0.4 }, reason: "low-mood" }),
  [D3]: {
    keep: { formula: "rule5", base: 1, chance: { whereabouts: 1, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0, boldness: 0.7, intended: 0.2, cost: 0 },
    stay: { formula: "rule5", base: 4, warm: 0.05, cold: 0.05, boldness: 0.7, intended: 0, cost: 0 } },
  [D4]: {
    keep: { formula: "rule5", base: 1, chance: { whereabouts: PENNY_WHERE_CHANCE, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0.05, boldness: 0.2, intended: 0.2, cost: 0 },
    stay: { formula: "rule5", base: 1, warm: 0, cold: 0, boldness: 0.2, intended: 0, cost: 0 } },
  [D6]: {
    offer: { formula: "rule5", base: 0.1, chance: { whereabouts: 1, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0, boldness: 0.7, intended: 0, cost: 0 },
    "carry-on": { formula: "rule5", base: 1, warm: 0.05, cold: 0.05, boldness: 0.7, intended: 0, cost: 0 } },
  [D7]: answerComponents({ obliged: r4(0.5 * 0.4 + 0.3 * 0.5 + 0.2 * 0.8), costs: { base: 0.15, state: stateCost(gusState1820, EAGER.Gus, gusPamBond),
    working: stateCost({ tired: 0, busy: GUS_WORKING, low: 0 }, EAGER.Gus, gusPamBond), displacedHabit: 0 }, reason: "working" }),
};
const ORDER = Object.fromEntries(Object.entries(C).map(([d, cs]) => [d, Object.keys(cs).map((s) => altId(d, s))]));
const FB = Object.fromEntries(Object.entries(C).map(([d, cs]) => {
  const ws = Object.fromEntries(Object.entries(cs).map(([s, c]) => [altId(d, s), weightOf(c)]));
  return [d, { weights: Object.fromEntries(Object.entries(ws).map(([k, v]) => [k, r4(v)])), probabilities: normalise(ws), raw: ws }];
}));
// Laya probabilities: invented for this fixture, in each decision's alternative order.
const LAYA = { [D1]: [0.46, 0.54], [D2]: [0.71, 0.21, 0.08], [D3]: [0.38, 0.62], [D4]: [0.81, 0.19] };
const layaProbs = (d) => Object.fromEntries(ORDER[d].map((id, i) => [id, LAYA[d][i]]));
// The comparison arm without p4: the whereabouts key loses its seeded 'away' and keeps the lived one (held constant:
// isolated, nothing re-run), so its chance is 0.5.
const P4_LESS_CHANCE = chanceOf(PENNY_WHERE_LIVED);
const offerWithoutP4 = rule5({ ...C[D1].offer, chance: { whereabouts: P4_LESS_CHANCE, answers: 1, followThrough: 1 } });
const pOfferWithoutP4 = r4(offerWithoutP4 / (offerWithoutP4 + rule5(C[D1]["keep-evening"])));

// The story the fixture tells, as conditions on the keyed draws.
function story(seed) {
  const u = (d) => unit(seed, "lives-choice", d);
  const sel = (d) => pick(ORDER[d], LAYA[d] ? layaProbs(d) : FB[d].probabilities, u(d));
  const pWith = FB[D1].probabilities[altId(D1, "offer")];
  return {
    [DF]: sel(DF) === altId(DF, "no"), // Pam's weighed no to Gus's flour
    [D1]: sel(D1) === altId(D1, "offer") && u(D1) >= pWith && u(D1) < pOfferWithoutP4, // Laya offers; the fallback would keep; without p4 it offers
    [D2]: sel(D2) === altId(D2, "yes"),
    [D3]: sel(D3) === altId(D3, "stay"),
    [D4]: sel(D4) === altId(D4, "keep"),
    [D6]: sel(D6) === altId(D6, "offer"),
    [D7]: sel(D7) === altId(D7, "not-now"),
  };
}
if (FIND) {
  for (let s = 1; s < 1e6; s++) if (Object.values(story(s)).every(Boolean)) { console.log(`smallest seed giving the story: ${s}`); process.exit(0); }
  console.error("no seed below 1e6 gives the story"); process.exit(1);
}
{
  const bad = Object.entries(story(SEED)).filter(([, ok]) => !ok).map(([d]) => d);
  if (bad.length) { console.error(`seed ${SEED} no longer gives the story at ${bad.join(", ")}; run --find-seed`); process.exit(1); }
}
const UNIT = Object.fromEntries(Object.keys(C).map((d) => [d, unit(SEED, "lives-choice", d)]));

// ---- the character-inspiration pin: the corrected pack of PR #96 (sim/character-inspiration.json at 38dd92d; the
// seven anchors it narrows belong to other people, so the three archetypes and profiles here are unchanged from 779e3b8,
// and only the provenance sentence and the hash move). Archetype objects copied exactly, sources in the validated
// (ordinal) order. ----
const PACK_HASH = "dd6be67508596b3f63eb2a691d157341d6b857971627f16341de14854826a7ea"; // ContentHash, computed by the C# loader
const PACK_PROVENANCE = "Derived voice cues and reviewed initial self-knowledge from installed Stardew Valley 1.6.15.24356 English assets, reviewed 2026-10-12. Game assembly SHA256 7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE. Source asset hashes and per-anchor admission notes: docs/under-glass/experiments/character-voices-2026-10-12.md. Archetypes are reusable interpretations, not biographies. No farmer-triggered event is a starting memory. Revised 2026-10-14: seven anchors (maru-clinic, linus-tent, demetrius-valley, alex-childhood, clint-pressure, jas-parents, george-evelyn-meeting) narrowed to what their sources state, after docs/under-glass/experiments/history-evidence-audit-2026-10-14.md and its check; the earlier wording is kept in the review. Anchors are private context for models, not simulated memories.";
const ord = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
const ARCHETYPES = [
  { id: "convivial-keeper", drives: "belonging, continuity, a place for others", coping: "offer practical comfort; guard routines and reputation",
    voice: "familiar and attentive, with definite preferences", evaluatorCue: "Preserve connection and familiar life; hospitality does not require self-erasure.",
    sources: ["Characters/Dialogue/Evelyn:Wed", "Characters/Dialogue/Gus:Introduction", "Characters/Dialogue/Caroline:Sat6", "Characters/Dialogue/Lewis:Sun"] },
  { id: "steadfast-carer", drives: "care, stability, doing a duty well", coping: "prepare and worry; neglect own wishes until strained",
    voice: "considerate, careful; can become firm or weary", evaluatorCue: "Care and stability matter; responsibility competes with fatigue and personal wishes.",
    sources: ["Characters/Dialogue/Harvey:Mon2", "Characters/Dialogue/Jodi:Sun8", "Characters/Dialogue/Penny:Wed"] },
  { id: "weary-defender", drives: "dignity, relief, a little control", coping: "push back or retreat; attachments survive resentment",
    voice: "blunt, economical, sometimes bitterly funny", evaluatorCue: "Resist intrusion and pity; weariness or resentment can outweigh a polite response.",
    sources: ["Characters/Dialogue/Shane:Wed8", "Characters/Dialogue/Pam:Thu", "Characters/Dialogue/George:Wed", "Characters/Dialogue/Kent:Mon"] },
].map((a) => ({ ...a, sources: [...a.sources].sort(ord) })).sort((a, b) => ord(a.id, b.id));
const ASSIGNED = { Gus: "convivial-keeper", Pam: "weary-defender", Penny: "steadfast-carer" };
// What each request carries today (CharacterInspiration.cs:138-145): the composed voice and the sorted union of
// the archetype's and the profile's sources. The accents and profile sources are the pack's (personal: they stay
// in requests and in the full pack, never in the snapshot).
const PROFILE = {
  Gus: { accent: "Welcoming, food-specific, practical about business; avoids conflict without losing preferences.",
    sources: ["Characters/Dialogue/Gus:Introduction", "Characters/Dialogue/Gus:Wed", "Data/Events/Saloon:96/f Gus 1000/f Pam 500/p Gus"] },
  Pam: { accent: "Rough contractions, teasing nicknames, blunt appetites; pride turns sharp under judgment.",
    sources: ["Characters/Dialogue/Pam:fall_Sun4", "Characters/Dialogue/Pam:Thu", "Data/Events/Trailer:35/f Penny 1000/p Penny"] },
  Penny: { accent: "Soft, orderly, bookish; hesitates socially but can defend children or confront her mother.",
    sources: ["Characters/Dialogue/Penny:Wed", "Characters/Dialogue/Penny:fall_Fri6", "Data/Events/Trailer:35/f Penny 1000/p Penny"] },
};
function requestCharacter(actor) {
  const a = ARCHETYPES.find((x) => x.id === ASSIGNED[actor]), p = PROFILE[actor];
  return { packHash: PACK_HASH, archetypeId: a.id, voice: `Drives: ${a.drives} Coping: ${a.coping} Voice: ${a.voice} ${p.accent}`,
    evaluatorCue: a.evaluatorCue, background: null, sources: [...new Set([...a.sources, ...p.sources])].sort(ord), provenance: PACK_PROVENANCE };
}
const characterInspiration = {
  schema: 1, hash: PACK_HASH, provenance: PACK_PROVENANCE,
  snapshot: ARCHETYPES, snapshotHash: sha256(canon(ARCHETYPES)),
  artifact: { uri: "git:38dd92d4a523757d8d390f04f927a811c6b0652f:sim/character-inspiration.json",
    sha256: "8ab112744357099a53ca1676eccc5234a696a480d2028dccab8750c02a7b9152" },
};

// ---- elements, in creation order; seq is assigned at the end ----
const T = { records: [], opportunities: [], recalls: [], decisions: [], attempts: [], plans: [], outcomes: [], perceptions: [], learning: [], comparisons: [], threads: [] };
let order = 0;
const push = (tbl, el) => {
  // An opportunity's tape link is observer-only: the character lens reaches a tape only through choice.thought, from
  // the choice's minute (contract 4.3, 5.4).
  if (tbl === "opportunities") for (const k of ["reflection", "question"]) if (k in el) { el.observer = { [k]: el[k], ...el.observer }; delete el[k]; }
  Object.defineProperty(el, "_o", { value: order++, enumerable: false }); T[tbl].push(el); return el;
};
const kb = (who, how, availableAt = PRE, extra = {}) => ({ who, how, availableAt, ...extra });
const obj = (k) => ({ key: k, origin: "object" });

const sources = [
  ["src:data-characters-penny", "canon", "Data/Characters:Penny (Pam Relative_Mother)", "S", null],
  ["src:penny-fall-fri6", "canon", "Characters/Dialogue/Penny:fall_Fri6 (anchor penny-family)", "S", null],
  ["src:penny-wed", "canon", "Characters/Dialogue/Penny:Wed", "S", null],
  ["src:penny-tue", "canon", "Characters/Dialogue/Penny:Tue", "S", null],
  ["src:penny-tue4", "canon", "Characters/Dialogue/Penny:Tue4", "S", "start branch"],
  ["src:penny-mon4", "canon", "Characters/Dialogue/Penny:Mon4", "S", "first statement only; the rest follows the farmer's answer (NA)"],
  ["src:penny-fri4", "canon", "Characters/Dialogue/Penny:Fri4", "S", null],
  ["src:penny-sun4", "canon", "Characters/Dialogue/Penny:Sun4", "S", null],
  ["src:penny-dishes", "canon", "penny_dishes (fixtures/game/Penny.json)", "S", null],
  ["src:penny-fall-thu6", "canon", "Characters/Dialogue/Penny:fall_Thu6", "S", "6 hearts: a present feeling placed at the start by our choice (S·R2; O2, decided)"],
  ["src:penny-mon8", "canon", "Characters/Dialogue/Penny:Mon8", "S", "8 hearts; 'eager to move out' shown only, not seeded (O2, decided)"],
  ["src:penny-thu", "canon", "Characters/Dialogue/Penny:Thu", "S", null],
  ["src:penny-schedule", "canon", "fixtures/game/Penny.json: Tue (Wed, Fri: GOTO Tue) ArchaeologyHouse 09:00-14:00, leaves for bed 18:30; summer (Mon, Thu; summer_Sun alike) Town from 08:10, home 18:00 (penny_dishes), leaves for bed 21:00; spring Mon/Thu/Sun 18:40; Sat in Town 10:00, 12:00, 17:00, leaves for bed 20:00; rain 21:00", "S", "game NPC.cs:5755-5890 (TryLoadSchedule, 1.6.15 decompile): season_day :5823, rain :5848, weekday :5875, season :5879, spring :5887, so a summer Wednesday runs Wed and a summer Thursday runs summer; canon's hours are departures, and she shows asleep on arriving; O1 decided: the baseline; habitual bedtimes are habits, not rules (src:direction-5b)"],
  ["src:pam-schedule", "canon", "fixtures/game/Pam.json spring (it has no summer key, so spring runs in summer): Trailer 08:00, JojaMart 12:00, Saloon 16:00, home to sleep 24:00", "S", "game NPC.cs:5887; once the farmer has the vault bundle (ccVault) her 'bus' key runs instead, Saloon from 17:00 (:5838)"],
  ["src:gus-schedule", "canon", "fixtures/game/Gus.json spring (no summer key, so spring runs in summer; Wednesday and Thursday have no key of their own): the Saloon from 08:00 (Mon: SeedShop 08:00, Saloon 11:00; Tue: CommunityCenter 08:30, Saloon 12:30)", "S", "game NPC.cs:5875-5887"],
  ["src:pam-thu", "canon", "Characters/Dialogue/Pam:Thu (later page)", "S", "the first-hearing hangover page is NA"],
  ["src:pam-fall-thu", "canon", "Characters/Dialogue/Pam:fall_Thu", "S", null],
  ["src:pam-winter-tue4", "canon", "Characters/Dialogue/Pam:winter_Tue4", "S", null],
  ["src:pam-winter-fri", "canon", "Characters/Dialogue/Pam:winter_Fri", "S", null],
  ["src:pam-saloon", "canon", "Characters/Dialogue/Pam:Saloon, Saloon_Entry, Saloon_Thu", "S", null],
  ["src:pam-summer-thu4", "canon", "Characters/Dialogue/Pam:summer_Thu4", "S", null],
  ["src:pam-fri2", "canon", "Characters/Dialogue/Pam:Fri2", "S", "her wish for a hobby besides 'that saloon every night'; perhaps checkers against herself"],
  ["src:pam-winter-thu", "canon", "Characters/Dialogue/Pam:winter_Thu", "S", "first statement only; filing it with the warm reading is ours (E)"],
  ["src:pam-sun", "canon", "Characters/Dialogue/Pam:Sun", "S", null],
  ["src:gus-thu", "canon", "Characters/Dialogue/Gus:Thu (anchor gus-regulars)", "S", null],
  ["src:gus-sun", "canon", "Characters/Dialogue/Gus:Sun", "S", null],
  ["src:gus-thu8", "canon", "Characters/Dialogue/Gus:Thu8", "S", "8 hearts; chronology VERIFY"],
  ["src:gift-tastes-penny", "canon", "Data/NPCGiftTastes:Penny", "S", "support for D52's characterization, not proof of an absolute refusal to fetch"],
  ["src:game-npc-3667", "code", "game NPC.cs:3667 (drinks re-rolled when Penny names Pam's loved gift)", "S", "support, not proof"],
  ["src:D52", "decision", "docs/decisions.md D52, Sid, 2026-10-13 (on the #90 branch)", "A", "Penny will not fetch alcohol"],
  ["src:direction-4", "decision", "Sid's fourth direction, 2026-10-14 (relayed)", "A", "the commitment is not extended to accepting or drinking alcohol; an agreed evening is informal and may disappoint"],
  ["src:direction-5", "decision", "Sid's fifth direction, 2026-10-14: O1-O3 decided", "A", "Penny's sourced routine the baseline; readings seeded modestly; disappointment to mood and the hoped-for evening"],
  ["src:direction-5b", "decision", "Sid's bedtime decision, 2026-10-14 (relayed): bedtimes are habits, not hard rules", "A", "actual sleep and waking follow the energy rule; never unavailable merely because the habitual hour has passed; record the habitual bedtime, sleep onset, waking, energy and the next choice"],
  ["src:pr-b-prototype", "editorial", "PR B's design (Penny's sourced routine as habits; designed, not yet opened): a habit prototype, seeds 1-3, 112 days", "E", "measured on a prototype, not the pilot (VERIFY when PR B lands): Pam wakes at a median 09:01; Penny and Pam awake at home together about 09:00-09:30 on weekdays (the sim keeps her weekday lessons); her body sends her to bed before 21:00 on summer Mondays, Thursdays and Sundays (median 19:52)"],
  ["src:91-answer-7", "decision", "docs/under-glass/specs/needs-bars-spec.md answer 7 [#91] (ErrandHoursBusy, errands only; Sid, 2026-10-13)", "A", "rule 16 follows it for errand asks; wider effects of work stay open"],
  ["src:code-cards", "code", "sim/UnderGlass.Sim/DefaultTown.cs:430-433, 458-464, 466-467 [a9eadaf]: Gus works the Saloon 11-24 and haunts the Square 8-11; Pam's haunts (the Saloon 15-24); Penny's lessons at the Square 10-14, off Sat and Sun, and her haunts (Store, Beach, ClinicYard 14-18); Pierre's Store 9-17, off Wednesday", "built", null],
  ["src:code-errand-uses", "code", "sim/UnderGlass.Sim/DefaultTown.cs:355-358 [#90]: Pam's only use is beer; Gus's are flour and vinegar", "#90", null],
  ["src:code-ask-invitation", "code", "sim/UnderGlass.Sim/Simulation.Ask.cs:81-84 [a9eadaf]: Ask(Invitation), obliged 0.5 liked + 0.3 familiarity + 0.2 chattiness (+0.2 Fond, + InviteKinBond 0.2) against 0.15 + state", "built", null],
  ["src:code-ask-errand", "code", "sim/UnderGlass.Sim/Simulation.Ask.cs:88-92 [#90]: Ask(Errand), obliged 0.5 liked + 0.3 familiarity + 0.2 understanding (+0.2 Fond, + ErrandKinBond 0.2) against 0.2 + state + ErrandCost", "#90", null],
  ["src:code-state-cost", "code", "sim/UnderGlass.Sim/Simulation.Ask.cs:30-31: StateCost = (0.3 tired + 0.3 busy + 0.3 low) (1 - eagerness x bond); AskStateOf, Simulation.Invitations.cs:55-63: busy is 1 only when leaving for work (start less commute) is due within InviteBusyMinutes (60), :59-61", "built", null],
  ["src:code-askfavours", "code", "sim/UnderGlass.Sim/Simulation.Errands.cs:127-159 [#90]: AskFavours draws someone in reach by bond (:150)", "#90", null],
  ["src:assessment-7", "editorial", "lives-gap-assessment.md section 7: the cast's evidence and our filings", "E", null],
  ["src:assessment-9", "editorial", "lives-gap-assessment.md section 9: the proposed records, rules 1-16 and outcomes", "E", "proposed mechanics, not built"],
].map(([id, kind, key, standing, note]) => ({ id, kind, key, standing, ...(note ? { observer: { note } } : {}) }));

// ---- records: pre-run facts, accounts, readings, a commitment; seeded associations and expectations ----
// Pre-run history records (facts, accounts, readings, commitments) carry their structured provenance under observer:
// standing, origin, sources, admission (the audit's R1 or R2, or null when its admission rules do not apply: a game-data
// fact, Sid's decision, our interpretation) and review {status, by, date}. Every reading here is "proposed": filed by
// the assessment, not yet individually reviewed by Sid (O2 asks for that review before the pilot seeds a reading).
const HISTORY = new Set(["fact", "account", "reading", "commitment"]);
const rec = (r) => {
  if (HISTORY.has(r.type) && r.visibleAt === PRE) {
    const st = r.observer.standing, adm = /R1$/.test(st) ? "R1" : /R2$/.test(st) ? "R2" : null;
    if (r.observer.admission !== undefined && r.observer.admission !== adm) throw new Error(`${r.id}: admission ${r.observer.admission} disagrees with standing ${st}`);
    r.observer = { ...r.observer, admission: adm, review: { status: "proposed", by: null, date: null } };
  }
  return push("records", r);
};
rec({ id: "h:pam-mother-of-penny", type: "fact", kind: "Fact", participants: [PAM, PENNY], time: { known: false }, stillTrueAtStart: true,
  knownBy: [kb(PENNY, "CanonStated"), kb(PAM, "CanonStated")], visibleAt: PRE, characterLabel: "Pam is Penny's mother",
  observer: { standing: "S", origin: "canon", sources: ["src:data-characters-penny"], cues: [obj("person:Pam"), obj("person:Penny")] } });
rec({ id: "h:penny-father-left", type: "fact", kind: "Past", participants: [PENNY], time: { known: false }, recurrence: "once", stillTrueAtStart: true,
  knownBy: [kb(PENNY, "CanonStated", PRE, { account: "acct:penny-father-left:Penny" })], visibleAt: PRE,
  characterLabel: "Her father left when she was little",
  observer: { standing: "S·R1", origin: "canon", admission: "R1", sources: ["src:penny-fall-fri6"], absentParticipants: ["her father (not in the town)"],
    timeRelative: "when she was little", notes: "still true at the start: not seen since, by her account",
    othersKnowledge: "unknown: canon gives no other account; Pam's knowledge is not asserted",
    cues: [{ key: "relationship:parent", origin: "proposed", standing: "E", note: "would match Pam: any tie from her father to her mother is ours (E, p2); recall only" },
      { key: "season:fall", origin: "sourced", source: "src:penny-fall-fri6", weight: 2, exclusive: false }] } });
rec({ id: "acct:penny-father-left:Penny", type: "account", holder: PENNY, fact: "h:penny-father-left", mode: "Lived",
  knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, characterLabel: "He left, giving his own reason; she has not seen him since",
  observer: { standing: "S·R1", origin: "canon", sources: ["src:penny-fall-fri6"], qualifications: ["his stated reason is reported speech; his motives are not known"] } });
rec({ id: "rd:penny-abandoned", type: "reading", holder: PENNY, object: "h:penny-father-left", stance: "none", certainty: "stated",
  knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, characterLabel: "She felt abandoned; this time of year brings it back",
  observer: { standing: "S·R1", origin: "canon", sources: ["src:penny-fall-fri6"], effect: "recalled only: no stance toward anyone present (rule 1)",
    cues: [{ key: "season:fall", origin: "sourced", source: "src:penny-fall-fri6", weight: 2, exclusive: false }] } });
rec({ id: "h:penny-teaches", type: "fact", kind: "Practice", participants: [PENNY], recurrence: "job",
  knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, characterLabel: "She teaches Vincent and Jas",
  observer: { standing: "S·R2", origin: "canon", admission: "R2", sources: ["src:penny-wed", "src:penny-tue", "src:penny-tue4", "src:penny-schedule", "src:code-cards"],
    notes: "canon: Tuesday, Wednesday and Friday, 09:00-14:00 in the ArchaeologyHouse (the museum); the sim: the Square, 10:00-14:00, Monday to Friday. Two implementation ambiguities, place and days; PR B's design keeps the sim's (her canon teaching days and place are listed as not represented), and so does this fixture. Wednesday is a lesson day in both, so the busy case holds either way; Thursday is one only in the sim",
    othersKnowledge: "not modelled (a public job)", cues: [obj("activity:teaching"), obj("place:Square")] } });
rec({ id: "rd:penny-teaching-warm", type: "reading", holder: PENNY, object: "activity:teaching", stance: "warm", certainty: "stated",
  knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, characterLabel: "Every child deserves a chance",
  observer: { standing: "S·R2", origin: "canon", sources: ["src:penny-wed"], cues: [obj("activity:teaching")] } });
rec({ id: "h:pam-saloon-evenings", type: "fact", kind: "Practice", participants: [PAM], place: "Saloon", recurrence: "recurring",
  knownBy: [kb(PENNY, "CanonStated", PRE, { account: "acct:pam-saloon-evenings:Penny" }), kb(PAM, "CanonStated", PRE, { account: "acct:pam-saloon-evenings:Pam" }),
    kb(GUS, "CanonStated", PRE, { account: "acct:pam-saloon-evenings:Gus" })],
  visibleAt: PRE, characterLabel: "Pam's evenings at the Saloon",
  observer: { label: "Pam spends her evenings at the Saloon", standing: "S·R2", origin: "canon", admission: "R2",
    sources: ["src:penny-mon4", "src:pam-saloon", "src:pam-summer-thu4", "src:gus-thu"], recurrenceNote: "almost every night (Gus's words; no count)",
    countOnce: "one practice with three accounts; never three experiences", cues: [obj("person:Pam"), obj("place:Saloon"), obj("time:evening")] } });
rec({ id: "acct:pam-saloon-evenings:Penny", type: "account", holder: PENNY, fact: "h:pam-saloon-evenings", mode: "Inferred",
  knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, characterLabel: "What she lives with at home: late nights, then headaches",
  observer: { standing: "E", origin: "authored", sources: ["src:penny-mon4", "src:penny-fri4", "src:assessment-7"],
    qualifications: ["canon gives her reading and the effects at home, not how she knows; filing it as Inferred is ours", "she never claims to see a drink"] } });
rec({ id: "acct:pam-saloon-evenings:Pam", type: "account", holder: PAM, fact: "h:pam-saloon-evenings", mode: "Lived",
  knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE, characterLabel: "Her own evenings there",
  observer: { standing: "S·R2", origin: "canon", sources: ["src:pam-saloon", "src:pam-summer-thu4"] } });
rec({ id: "acct:pam-saloon-evenings:Gus", type: "account", holder: GUS, fact: "h:pam-saloon-evenings", mode: "Witnessed",
  knownBy: [kb(GUS, "CanonStated")], visibleAt: PRE, characterLabel: "He serves her almost every night",
  observer: { standing: "S·R2", origin: "canon", sources: ["src:gus-thu", "src:gus-sun", "src:gus-thu8"] } });
rec({ id: "h:penny-household-burden", type: "fact", kind: "Practice", participants: [PENNY], recurrence: "recurring",
  knownBy: [kb(PENNY, "CanonStated", PRE, { account: "acct:penny-household-burden:Penny" })], visibleAt: PRE,
  characterLabel: "The dishes and cleaning fall to Penny",
  observer: { standing: "S·R2", origin: "canon", admission: "R2", sources: ["src:penny-fri4", "src:penny-sun4", "src:penny-dishes"],
    othersKnowledge: "Pam's side (she expects the chores done, m2) is not part of this excerpt",
    cues: [obj("place:Home:Trailer"), obj("activity:chores"), obj("person:Pam")] } });
rec({ id: "acct:penny-household-burden:Penny", type: "account", holder: PENNY, fact: "h:penny-household-burden", mode: "Lived",
  knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, characterLabel: "She cleans alone while Mom nurses a headache",
  observer: { standing: "S·R2", origin: "canon", sources: ["src:penny-fri4", "src:penny-sun4", "src:penny-dishes", "src:assessment-7"],
    qualifications: ["the chores are Lived", "her stated cause, late nights at the Saloon, is filed as Inferred from effects (E)"] } });

const asId = (hname, object, sign, name) => `as:${hname}:${object}:${sign}:${slug(name)}`;
const AS = {
  pennyCare: asId("Penny", "person:Pam", "+", "worried care"), pennyBurden: asId("Penny", "person:Pam", "-", "burdened"),
  pennySaloon: asId("Penny", "place:Saloon", "-", "wary"), pennyBook: asId("Penny", "activity:reading-at-home", "+", "absorbed"),
  pamPenny: asId("Pam", "person:Penny", "+", "proud"), pamSaloonWarm: asId("Pam", "place:Saloon", "+", "enjoys"),
  pamSaloonWary: asId("Pam", "place:Saloon", "-", "uneasy"), pamGus: asId("Pam", "person:Gus", "+", "fond"),
  gusPam: asId("Gus", "person:Pam", "-", "uneasy"),
  pennyDisEvening: asId("Penny", "situation:evening-at-home-with-Pam", "-", "disappointed"),
};
const EX = {
  pennyWhere: "ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday", gusEvening: "ex:Gus:Pam:whereabouts:Saloon:18-21:weekday",
  pamBeer: "ex:Pam:Penny:answers:errand:beer", pennyAnswers: "ex:Penny:Pam:answers:plan", pennyFollow: "ex:Penny:Pam:followThrough:plan",
  gusPennyAvail: "ex:Gus:Penny:availability:Square:10-14:weekday", gusFlour: "ex:Gus:Pam:answers:errand:flour",
  pamGusAvail: "ex:Pam:Gus:availability:Saloon:18-21:weekday",
};
const reading = (holder, id, object, stance, label, sourcesIds, cues, extra = {}) => rec({
  id, type: "reading", holder, object, stance, certainty: "stated", knownBy: [kb(holder, "CanonStated")], visibleAt: PRE, characterLabel: label,
  observer: { standing: "S·R2", origin: "canon", sources: sourcesIds, cues: cues.map(obj), ...extra } });
reading(PENNY, "rd:penny-saloon-wary", "place:Saloon", "wary", "Mom goes to the Saloon too much", ["src:penny-mon4"], ["place:Saloon", "person:Pam"], { seeds: [AS.pennySaloon] });
reading(PENNY, "rd:penny-expects-pam-away", "person:Pam", "expects", "She expects Mom out in the evening", ["src:penny-mon4"], ["person:Pam", "time:evening", "place:Home:Trailer"],
  { seeds: [EX.pennyWhere], expects: "whereabouts: away from home in the evening" });
reading(PENNY, "rd:penny-pam-burden", "person:Pam", "wary", "No help at home: the burden is hers", ["src:penny-fri4", "src:penny-sun4"], ["person:Pam", "place:Home:Trailer", "place:Saloon"], { seeds: [AS.pennyBurden] });
reading(PENNY, "rd:penny-pam-care", "person:Pam", "warm", "She worries about Mom and wants her happy", ["src:penny-fall-thu6", "src:penny-mon8"], ["person:Pam"],
  { seeds: [AS.pennyCare], placedAtStart: "our choice (O2, decided 2026-10-14): a present feeling carried to the start, seeded modestly" });
reading(PENNY, "rd:penny-reading-home", "activity:reading-at-home", "warm", "In a book she can forget her worries for a while", ["src:penny-thu"], ["activity:reading-at-home", "place:Home:Trailer"], { seeds: [AS.pennyBook] });
rec({ id: "cmt:penny-no-alcohol", type: "commitment", holder: PENNY, scope: { provide: "alcohol", goodsToday: ["beer"] }, to: "anyone", force: "Absolute", revisable: false, since: PRE,
  knownBy: [kb(PENNY, "ProductDecision")], visibleAt: PRE, characterLabel: "She will not provide alcohol",
  observer: { label: "Penny will not provide alcohol (Sid's D52)", standing: "A", origin: "product-decision", sources: ["src:D52"], support: ["src:gift-tastes-penny", "src:game-npc-3667"],
    toNote: "#90's behaviour; D52 names her mom, broadening to anyone is ours (E)",
    excludes: [{ what: "accepting or drinking alcohol", why: "needs a separate decision", source: "src:direction-4" }],
    othersKnowledge: "unknown to Pam: she learns only from Penny's answers (rule 15)", cues: [obj("category:alcohol"), obj("good:beer")] } });
reading(PAM, "rd:pam-penny-warm", "person:Penny", "warm", "Penny is her only girl; she is proud of her", ["src:pam-thu", "src:pam-fall-thu", "src:pam-winter-tue4", "src:pam-winter-fri"], ["person:Penny"],
  { seeds: [AS.pamPenny], countOnce: "four sources, one entry" });
reading(PAM, "rd:pam-saloon-warm", "place:Saloon", "warm", "She enjoys the Saloon and looks forward to it", ["src:pam-saloon", "src:pam-winter-thu"], ["place:Saloon", "time:evening"],
  { seeds: [AS.pamSaloonWarm], countOnce: "two sources, one entry" });
reading(PAM, "rd:pam-saloon-wary", "place:Saloon", "wary", "She always ends up there; not healthy, she says", ["src:pam-summer-thu4", "src:pam-fri2"], ["place:Saloon", "time:evening"], { seeds: [AS.pamSaloonWary] });
rec({ id: "rd:pam-wish-hobby", type: "reading", holder: PAM, object: "activity:game", stance: "none", certainty: "stated",
  knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE, characterLabel: "She wishes for a hobby besides the Saloon every night, maybe checkers",
  observer: { standing: "S·R2", origin: "canon", sources: ["src:pam-fri2"],
    effect: "makes an offer possible (rule 11); gives no warmth toward a game with anyone (O2, decided 2026-10-14); in her words it may be checkers against herself",
    cues: [obj("activity:game"), { key: "place:Saloon", origin: "sourced", source: "src:pam-fri2", weight: 2, exclusive: false }] } });
reading(PAM, "rd:pam-gus-warm", "person:Gus", "warm", "Gus is a good guy", ["src:pam-sun"], ["person:Gus", "place:Saloon"], { seeds: [AS.pamGus] });
reading(GUS, "rd:gus-expects-pam-evenings", "person:Pam", "expects", "Pam comes almost every night", ["src:gus-thu", "src:gus-sun"], ["person:Pam", "place:Saloon"],
  { seeds: [EX.gusEvening], expects: "whereabouts: at the Saloon in each evening band" });
reading(GUS, "rd:gus-pam-wary", "person:Pam", "wary", "She drinks too much; he shouldn't judge", ["src:gus-thu8"], ["person:Pam"], { seeds: [AS.gusPam] });

// Associations seeded by rule 1 (R / (1 + lived entries); none lived here). A record's state lives in snapshots.
const assoc = (holder, id, object, sign, name, strength, causes, visibleAt = PRE, how = "Seeded", extra = {}) => rec({
  id, type: "association", holder, object, sign, name, causes, ...extra.top, knownBy: [kb(holder, how, visibleAt, extra.via ? { via: extra.via } : {})], visibleAt,
  snapshots: [{ asOf: visibleAt, state: { strength } }], characterLabel: extra.characterLabel,
  observer: { origin: visibleAt === PRE ? "seeded" : "run", standing: visibleAt === PRE ? "E (rule 1, from a sourced reading)" : "D", cues: [obj(object)], ...extra.observer } });
assoc(PENNY, AS.pennyCare, "person:Pam", "+", "worried care", 0.05, ["rd:penny-pam-care"]);
assoc(PENNY, AS.pennyBurden, "person:Pam", "-", "burdened", 0.05, ["rd:penny-pam-burden"]);
assoc(PENNY, AS.pennySaloon, "place:Saloon", "-", "wary", 0.05, ["rd:penny-saloon-wary"]);
assoc(PENNY, AS.pennyBook, "activity:reading-at-home", "+", "absorbed", 0.05, ["rd:penny-reading-home"]);
assoc(PAM, AS.pamPenny, "person:Penny", "+", "proud", 0.05, ["rd:pam-penny-warm"]);
assoc(PAM, AS.pamSaloonWarm, "place:Saloon", "+", "enjoys", 0.05, ["rd:pam-saloon-warm"]);
assoc(PAM, AS.pamSaloonWary, "place:Saloon", "-", "uneasy", 0.05, ["rd:pam-saloon-wary"]);
assoc(PAM, AS.pamGus, "person:Gus", "+", "fond", 0.05, ["rd:pam-gus-warm"]);
assoc(GUS, AS.gusPam, "person:Pam", "-", "uneasy", 0.05, ["rd:gus-pam-wary"]);

// Expectations: the seeded entry (source Reading) stays beside the last livedWindow lived entries (rule 9);
// chance = (successes + 1) / (entries + 1). Each state is a snapshot shown from its asOf; each entry carries its minute.
function state(entries) {
  const lived = entries.filter((e) => e.source !== "Reading").length;
  const successes = entries.filter((e) => OK.includes(e.outcome)).length;
  return { entries, lived, seeded: entries.length - lived, successes, chance: r4((successes + 1) / (entries.length + 1)) };
}
const summary = (s) => ({ count: s.entries.length, lived: s.lived, seeded: s.seeded, successes: s.successes, chance: s.chance });
function expectation(holder, hname, subject, key, visibleAt, how, snapshots, extra = {}) {
  return rec({ id: `ex:${hname}:${subject}:${key}`, type: "expectation", holder, subject: names.indexOf(subject), key,
    knownBy: [kb(holder, how, visibleAt, extra.via ? { via: extra.via } : {})], visibleAt, snapshots: snapshots.map(([asOf, entries]) => ({ asOf, state: state(entries) })),
    characterLabel: extra.characterLabel, observer: { origin: extra.origin ?? "run", standing: extra.standing ?? "D", cues: [obj(`person:${subject}`)], ...(extra.notes ? { notes: extra.notes } : {}) } });
}
const seededAway = { at: PRE, outcome: "away", source: "Reading", ref: "rd:penny-expects-pam-away" };
expectation(PENNY, "Penny", "Pam", "whereabouts:Home:Trailer:18-21:weekday", PRE, "Seeded", [[PRE, [seededAway]], [FROM, [seededAway, ...PENNY_WHERE_LIVED]]],
  { origin: "seeded and lived", standing: "E (rule 1) / D", characterLabel: "Where Mom is on a weekday evening",
    notes: "p4's seeded 'away' (0.5), then one lived 'away' on summer 1, a Monday (day 28): the first evening her sourced routine had her at home and awake through the band; Pam was at the Saloon (rule 14, seen absent). The key stands at 0.3333, below 0.5, so rule 14 stops counting her absence. No spring evening counted: she left for bed at 18:30 or 18:40 (sourced habits) and, on PR B's prototype, slept on arriving on most nights; that she was awake through the band only from summer is ours (E), for the energy rule to confirm in the pilot. An excerpt: the update on day 28 is before it, so the state at the excerpt's start is a snapshot" });
// Gus sees Pam at the Saloon most evenings: the last six weekday sightings before the excerpt fill the lived window.
const gusSeen = [22, 23, 24, 25, 28, 29].map((d) => ({ at: at(d, 18, 5), outcome: "found", source: "Seen" }));
const seededFound = { at: PRE, outcome: "found", source: "Reading", ref: "rd:gus-expects-pam-evenings" };
expectation(GUS, "Gus", "Pam", "whereabouts:Saloon:18-21:weekday", PRE, "Seeded", [[FROM, [seededFound, ...gusSeen]]],
  { origin: "seeded and lived", standing: "E (rule 1) / D", characterLabel: "Pam at the Saloon on a weekday evening",
    notes: "an excerpt: the first recorded state is the excerpt's start; before it a reader shows 'earlier state not recorded'. The lived window is full (6), so each new sighting drops the oldest" });

// ---- the base excerpt (needs and favours are [#90]) ----
const acts = [];
const needs = [];
const favours = [];
const invitations = [];
const reflections = [], choiceQuestions = [];
const rowState = (s, eager, b) => ({ tired: s.tired, busy: s.busy, low: s.low, bond: r4(b), eager: r4(eager) });
const NULL_STATE = { tired: null, busy: null, low: null, bond: null, eager: null };
function favour(tick, asker, asked, item, f) {
  const n = favours.filter((x) => x.tick === tick && x.asker === asker && x.asked === asked).length;
  const row = { tick, asker, asked, item, accepted: f.accepted, margin: f.margin, actId: f.actId, ...f.state,
    wontFetch: f.wontFetch ?? false, short: false, running: false, id: `fav:${tick}:${asker}:${asked}:${n}`, answer: f.answer, askMade: f.answer !== "busy" };
  favours.push(row); return row.id;
}
function invitation(tick, asker, asked, kind, f) {
  const n = invitations.filter((x) => x.tick === tick && x.asker === asker && x.asked === asked).length;
  const row = { tick, asker, asked, kind, accepted: f.accepted, margin: f.margin, actId: f.actId, ...f.state,
    id: `inv:${tick}:${asker}:${asked}:${n}`, answer: f.answer, askMade: f.answer !== "busy" };
  invitations.push(row); return row.id;
}

// ---- decisions ----
const MODEL_ID = "model:laya:typed-decisions";
// The option set's identity, without reasons: what a comparison holds constant (an intervention changes reasons, so
// each arm records its own setHash).
function identityHash(alts, excluded, constraints) {
  return fnv({
    eligible: alts.map((a) => a.slug).sort(ord),
    excluded: excluded.map((x) => canon([x.slug, x.why.code])).sort(),
    constraints: constraints.map((c) => canon([c.code, c.value ?? null, c.terms ?? null])).sort(),
  });
}
function setHash(alts, excluded, constraints) {
  return fnv({
    eligible: alts.map((a) => ({ id: a.slug, reasons: a.reasons.map((r) => canon([r.kind, r.code, r.value ?? null])).sort() })).sort((x, y) => ord(x.id, y.id)),
    excluded: excluded.map((x) => canon([x.slug, x.why.code])).sort(),
    constraints: constraints.map((c) => canon([c.code, c.value ?? null, c.terms ?? null])).sort(),
  });
}
function decision(d) {
  const id = d.id;
  d.alternatives = d.alternatives.map(({ slug: s, template, reasons, characterLabel, observer }) =>
    ({ id: altId(id, s), slug: s, ...(template ? { template } : {}), components: C[id][s], reasons, characterLabel, observer }));
  d.excluded = d.excluded.map(({ slug: s, why, characterLabel, observer }) => ({ id: altId(id, s), slug: s, why, characterLabel, observer }));
  const order_ = ORDER[id], fb = FB[id], u = UNIT[id];
  const used = LAYA[id] ? layaProbs(id) : fb.probabilities;
  const selected = pick(order_, used, u);
  const ch = d.choiceIn; delete d.choiceIn;
  const choice = { visibleAt: ch.visibleAt, selected,
    ...(ch.thought ? { thought: { tape: ch.tape, imagined: true } } : {}),
    ...(ch.base ? { base: ch.base } : {}),
    observer: {
      backend: LAYA[id] ? "laya" : "fallback-keyed-draw", probabilities: used,
      sampled: { stream: "lives-choice", parts: [id], unit: u, order: order_ },
      fallback: { weights: fb.weights, probabilities: fb.probabilities, wouldSelect: pick(order_, fb.probabilities, u) },
      ...(LAYA[id] ? { laya: { probabilities: used } } : {}),
      ...(LAYA[id] ? {} : { fallbackReason: ch.fallbackReason }),
      ...(ch.cap ? { cap: ch.cap } : {}),
      provenance: { alternativeSetHash: setHash(d.alternatives, d.excluded, d.constraints), tape: ch.tape ?? null, requestHash: null, model: LAYA[id] ? MODEL_ID : null },
      ...(ch.observer ?? {}),
    } };
  const { label, torn, ...rest } = d;
  return push("decisions", { ...rest, choice, observer: { label, torn } });
}
function requestView(dec) {
  return {
    alternatives: dec.alternatives.map((a) => ({ id: a.slug, reasons: a.reasons.map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null })) })),
    excluded: dec.excluded.map((x) => ({ id: x.slug, why: x.why.code })),
    constraints: dec.constraints.map((c) => ({ code: c.code, value: c.value ?? null, terms: c.terms ?? null })),
    alternativeSetHash: dec.choice.observer.provenance.alternativeSetHash,
  };
}
const bySlug = (dec, probs) => Object.fromEntries(dec.alternatives.map((a) => [a.slug, probs[a.id]]));
const slugOf = (dec, id) => dec.alternatives.find((a) => a.id === id).slug;
const PLACEHOLDER = "(illustrative placeholder: a real run records the exact text, as ReflectionAnswer does today)";
// Balanced evaluation (ResilientReflectionMind.cs:119-147, :184-190): one pass per distinct (kind, line) group of the
// request's choices (every alternative here has its own), each group visiting every slot; the answer is the normalised
// mean of the passes. The passes are invented around the invented answer, so their mean is that answer. Their labels
// are assigned from the request by relabel() below, exactly as BuildPasses assigns them.
function balancedPasses(dec) {
  const slugs = dec.alternatives.map((a) => a.slug), target = bySlug(dec, dec.choice.observer.laya.probabilities), n = slugs.length;
  return slugs.map((_, k) => {
    const w = Object.fromEntries(slugs.map((s, j) => [s, r4(target[s] + (j === k ? 0.02 * (n - 1) : -0.02))]));
    return { pass: k + 1, prompt: PLACEHOLDER, response: PLACEHOLDER, labelsToChoiceIds: null, weights: w, error: null };
  });
}
function layaAnswer(dec, extra) {
  const evaluations = balancedPasses(dec);
  const w = bySlug(dec, dec.choice.observer.laya.probabilities);
  return { thought: extra.thought ?? null, suggestedChoice: extra.suggestedChoice ?? null, weights: w, backend: extra.backend,
    note: "illustrative: " + (extra.thought ? "authored thought; " : "") + "Laya weights and passes invented for this fixture",
    layaPrompt: PLACEHOLDER, generationPrompt: null, layaResponse: PLACEHOLDER, evaluations, generationResponse: null,
    model: MODEL_ID, evaluationMode: "Balanced",
    receipt: { evaluations: evaluations.map((e) => ({ pass: e.pass, servedModel: "typed-decisions", repo: "convaiinnovations/laya/typed-decisions",
      inputTokens: 310 + 7 * e.pass, stateTokens: 290 + 7 * e.pass, stateTokensDropped: 0, truncated: false, serverMs: 33 + e.pass, clientMs: 41 + 2 * e.pass })),
      generation: null } };
}
function authoredAnswer(dec, thought, suggestedChoice) {
  // What AuthoredReflectionMind returns (Reflection.cs:152-154): 3 for its suggested choice and 1 for each other, which
  // the simulation normalises before recording (Simulation.Reflection.cs:284-288). Not torn, so no model is asked; these
  // weights are the mind's, never the answer: the keyed draw over the fallback answers (contract 9.5).
  const n = dec.alternatives.length;
  return { thought, suggestedChoice, weights: Object.fromEntries(dec.alternatives.map((a) => [a.slug, (a.slug === suggestedChoice ? 3 : 1) / (n + 2)])), backend: "authored",
    note: "illustrative: authored thought; not torn, so no model is asked; the authored baseline's weights, not the answer",
    layaPrompt: null, generationPrompt: null, layaResponse: null, evaluations: null, generationResponse: null };
  // ReflectionAnswer's own fields are written even when null (Replay.cs:282-283 serializes them with no ignore condition); the 0.2 additions
  // (model, generatorModel, evaluationMode, receipt, fallback) are written only with Lives on and omitted when null.
}
const reflId = (oppId) => `${SEED}:${oppId}`;

// ============ Wednesday summer 3 (day 30) ============
// == 10:20 Gus's flour need; his draw lands on Penny at her lessons: busy, no ask (rule 16, errand asks only) ==
// Wednesday is a lesson day in canon (Tue, Wed, Fri) and in the sim (Monday to Friday), so the busy case holds in both.
needs.push({ id: 52, tick: at(30, 10, 20), holder: "Gus", item: "flour", until: at(30, 10, 20) + 4 * DAY, state: "Open", promise: -1, settled: -1 });
const favBusy = favour(at(30, 10, 20), "Gus", "Penny", "flour", { answer: "busy", accepted: null, margin: null, actId: null, state: NULL_STATE });
push("attempts", { id: "att:43820:Gus:0", actor: GUS, target: PENNY, kind: "errand-ask", decidedBy: { mechanism: "errand-draw" }, base: ["need:52"],
  knownBy: [kb(GUS, "Lived", at(30, 10, 20))], visibleAt: at(30, 10, 20), resolvedAt: at(30, 10, 20), status: "busy",
  characterLabel: "Gus would ask Penny to fetch flour; she is teaching",
  observer: { label: "Gus, at the Square before his shift, would ask Penny for flour",
    decidedBy: "base: AskFavours' draw among those in reach at the Square (Simulation.Errands.cs:150 [#90]; Free ignores work, Simulation.cs:902) x rule 15's factor, 1 with no entries",
    world: { asked: false, place: "Square", activity: "teaching" }, outcome: "out:43820:Penny:0",
    // A base row is the asked person's record (their answer and state then): visible in the character lens only to the
    // asked person, and only if askMade (contract 5.4, 8). A busy row is visible to nobody; the asker's link is here.
    rows: [favBusy] } });
push("outcomes", { id: "out:43820:Penny:0", about: PENNY, visibleAt: at(30, 10, 20), settledAt: at(30, 10, 20), attempts: ["att:43820:Gus:0"], participants: [GUS, PENNY],
  kind: "busy", world: { asked: false, place: "Square", activity: "teaching" }, base: [favBusy], determinedBy: { rule: "16" },
  mustNotWrite: ["answer", "refusal", "regard", "dread", "knowledge-for-the-target"],
  observer: { label: "Gus's flour, a first try", settledLabel: "Penny is teaching: busy, not a refusal; she is not asked, so she perceives nothing",
    determinedBy: "the person drawn is at her job place in working hours: busy, no ask is made; rule 16 applies to errand asks, as the assessment reads #91's answer 7",
    sources: ["src:assessment-9", "src:91-answer-7"],
    canon: "Penny teaches on a Wednesday in canon and in the sim; the meeting relies on the sim's cards (Gus's Square haunt 08:00-11:00; Penny's lessons at the Square). In canon Gus is at the Saloon from 08:00 on a summer Wednesday (his spring key) and Penny teaches in the ArchaeologyHouse from 09:00: a mechanism fixture (rule 16), not a canon scene" } });
push("perceptions", { id: "per:43820:Gus:0", who: GUS, visibleAt: at(30, 10, 20), of: "att:43820:Gus:0", causes: [],
  perceived: { kind: "busy", subject: PENNY, activity: "teaching", place: "Square", source: "Seen" }, characterLabel: "Gus: Penny is busy teaching",
  observer: { label: "Gus: Penny is busy teaching", notPerceived: ["whether she would have said yes"], observerLinks: { worldOutcome: "out:43820:Penny:0" } } });
// In-run knowledge names what gave it (knownBy.via): here the holder's own perception (contract 5.1).
expectation(GUS, "Gus", "Penny", "availability:Square:10-14:weekday", at(30, 10, 20), "Lived", [[at(30, 10, 20), []]], { characterLabel: "Whether Penny is free at the Square in the morning", via: "per:43820:Gus:0" });
function learn(u) { return push("learning", u); }
// An expectation's entries after one more (rule 9: the oldest lived entry leaves once the lived window is full).
function afterAdding(entries, added) {
  let next = [...entries, added];
  const lived = next.filter((e) => e.source !== "Reading");
  if (lived.length > settings.livedWindow) next = next.filter((e) => e !== lived[0]);
  return next;
}
function exLearn(id, who, visibleAt, record, prevEntries, added, extra = {}) {
  const prev = state(prevEntries);
  let next = [...prevEntries, added], dropped;
  const lived = next.filter((e) => e.source !== "Reading");
  if (lived.length > settings.livedWindow) { dropped = lived[0]; next = next.filter((e) => e !== dropped); }
  return learn({ id, who, visibleAt, record, evidence: extra.evidence, previous: summary(prev), added, ...(dropped ? { dropped } : {}), new: summary(state(next)),
    ...(extra.preserved ? { preserved: extra.preserved } : {}), characterLabel: extra.characterLabel, observer: extra.observer });
}
exLearn("lu:43820:Gus:0", GUS, at(30, 10, 20), EX.gusPennyAvail, [], { at: at(30, 10, 20), outcome: "busy", source: "Seen" },
  { evidence: ["per:43820:Gus:0"], characterLabel: "Gus: Penny is busy at this hour",
    observer: { label: "Gus: Penny is busy at this hour", rule: "16; 9.4 busy", notes: "availability only: no answer, no refusal, no regard, no dread" } });

// == 18:00 Gus sees Pam at the Saloon in the evening band: a passive sighting confirms his weekday key (rule 9: the window
// is full, so the oldest sighting leaves; the seeded entry stays) ==
const gusEntries0 = [seededFound, ...gusSeen];
push("perceptions", { id: "per:44280:Gus:0", who: GUS, visibleAt: at(30, 18, 0), of: null, causes: [],
  perceived: { kind: "found", subject: PAM, place: "Saloon", band: "18-21", source: "Seen" }, characterLabel: "Gus: Pam is here, as most evenings",
  observer: { label: "Gus: Pam at the Saloon in the evening band (Wednesday)", rule: "9.4 seen (passive)",
    notes: "her 15-18 sighting (from about 16:35, illustrative) is aggregated with the band's other sightings and not shown" } });
exLearn("lu:44280:Gus:0", GUS, at(30, 18, 0), EX.gusEvening, gusEntries0, { at: at(30, 18, 0), outcome: "found", source: "Seen" },
  { evidence: ["per:44280:Gus:0"], preserved: ["rd:gus-expects-pam-evenings"], characterLabel: "Gus: Pam's evenings, confirmed by sight",
    observer: { label: "Gus: Pam's weekday evenings, confirmed by sight", rule: "9.4 seen", notes: "the seeded entry stays beside the lived ones; the oldest sighting leaves the full window (rule 9)" } });
const gusEntries1 = afterAdding(gusEntries0, { at: at(30, 18, 0), outcome: "found", source: "Seen" });

// == 18:15 Gus asks Pam to fetch flour (base [#90]); a pending ask; her reflection opens (8a), not torn: the keyed draw answers ==
push("attempts", { id: "att:44295:Gus:0", actor: GUS, target: PAM, kind: "errand-ask", decidedBy: { mechanism: "errand-draw" }, base: ["need:52"],
  knownBy: [kb(GUS, "Lived", at(30, 18, 15))], visibleAt: at(30, 18, 15), resolvedAt: at(30, 18, 16), status: "answered",
  characterLabel: "Gus asks Pam to fetch flour",
  observer: { label: "Gus asks Pam to fetch flour (a pending ask, rule 7)", decidedBy: "base: AskFavours' draw (Simulation.Errands.cs:150 [#90]) x rule 15's factor",
    world: { answer: "no" }, outcome: "out:44296:Pam:0" } });
push("perceptions", { id: "per:44295:Pam:0", who: PAM, visibleAt: at(30, 18, 15), of: "att:44295:Gus:0", causes: [],
  perceived: { kind: "asked", by: GUS, ask: "errand", good: "flour" }, characterLabel: "Pam: Gus asks me to fetch flour", observer: { label: "Pam is asked a favour" } });
push("opportunities", { id: "opp:44295:Pam:0", holder: PAM, visibleAt: at(30, 18, 15), trigger: "asked", causes: ["att:44295:Gus:0"],
  reflection: `reflection:${reflId("opp:44295:Pam:0")}`, characterLabel: "Gus asks a favour",
  observer: { label: "Pam is asked: she recalls a pre-run record about Gus, so her reflection opens", rule: "8a", budget: "one a person-day (her first today)" } });
push("recalls", { id: "rc:44295:Pam:0", holder: PAM, opportunity: "opp:44295:Pam:0", visibleAt: at(30, 18, 15),
  items: [{ record: "rd:pam-gus-warm", slot: "top", matched: ["person:Gus", "place:Saloon"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.4 } } }],
  characterLabel: "Gus is a good guy",
  observer: { label: "Pam recalls that Gus is a good guy", active: ["person:Gus", "place:Saloon", "good:flour", "time:evening", "day:weekday", "situation:asked"],
    bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 } } });
const yesParts = (dec) => C[dec].yes;
const decF = decision({
  id: DF, holder: PAM, opportunity: "opp:44295:Pam:0", recall: "rc:44295:Pam:0", visibleAt: at(30, 18, 15), question: "answer-errand-ask",
  characterLabel: "Fetch Gus's flour tomorrow?", label: "Will Pam fetch Gus's flour tomorrow?",
  alternatives: [
    { slug: "yes", characterLabel: "yes, tomorrow", observer: { label: "yes" }, reasons: [
      { kind: "supports", code: "obliged", value: yesParts(DF).obliged, via: "ask", cites: [], observer: { provenance: ["src:code-ask-errand"],
        label: "the Ask: regard 0.4 (the Pam-Gus friend seed; 'can wait'), familiarity 0.5 (the friends' seed), understanding 0.3 (her card); no Fond (they meet daily)" } },
      { kind: "shown", code: "warm", via: "recall", cites: ["rd:pam-gus-warm"], effect: "context-only", characterLabel: "Gus is a good guy",
        observer: { label: "recalled; the Ask reads regard, not associations (count once)" } },
      { kind: "conflicts", code: "cost", value: 0.2, via: "ask", cites: [], observer: { provenance: ["src:code-ask-errand"], label: "the Ask's base cost; no errand running, the household can pay (ErrandCost 0)" } },
      { kind: "conflicts", code: "state", value: yesParts(DF).costs.state, via: "state", cites: [], characterLabel: "tired, a little low",
        observer: { provenance: ["src:code-state-cost"], label: "tired 0.10, low 0.02 (illustrative), eagerness 0.5, bond 0.45" } } ] },
    { slug: "not-now", characterLabel: "not now", observer: { label: "not now" }, reasons: [
      { kind: "supports", code: "tired", value: C[DF]["not-now"].reason.value, via: "state", cites: [], characterLabel: "tired", observer: { label: "tired and a little low: a reason holds (rule 7)" } }] },
    { slug: "no", characterLabel: "no", observer: { label: "no" }, reasons: [
      { kind: "supports", code: "negative-margin", value: r4(-C[DF].no.margin), via: "ask", cites: [], observer: { label: "the margin's other side" } }] },
  ],
  excluded: [],
  constraints: [{ code: "counter-hours", value: "Store", terms: { from: at(31, 9, 0), until: at(31, 17, 0) }, cites: [], characterLabel: "the Store opens tomorrow at nine",
    observer: { label: "the Store is closed today, Pierre's day off (Wednesday: DefaultTown.cs:467, Works(..., 2)); tomorrow 09:00-17:00" } }],
  torn: { value: false, rule: "6", why: "Pam has only a warm side toward Gus; the top two are not within 10%" },
  choiceIn: { visibleAt: at(30, 18, 16), tape: `reflection:${reflId("opp:44295:Pam:0")}`, thought: true, base: [`fav:${at(30, 18, 16)}:Gus:Pam:0`], fallbackReason: "not-torn" },
});
reflections.push({ request: {
    id: reflId("opp:44295:Pam:0"), tick: at(30, 18, 15), actor: "Pam", subject: "Gus", sourceActId: -1,
    memory: "At 18:15 Gus asked you to fetch flour for him from the Store.",
    context: "Pam, age 50: the Saloon, Wednesday evening. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "yes", kind: "FavourAnswer", line: "Sure, hon. I'll grab it tomorrow." }, { id: "not-now", kind: "FavourAnswer", line: "Not now, Gus. Ask me another day." },
      { id: "no", kind: "FavourAnswer", line: "Get your own flour." }],
    source: null, proposal: null, continuity: null, opportunity: { kind: "asked", sleptAt: -1, wokeAt: -1 }, character: requestCharacter("Pam"),
    recalled: [{ id: "rd:pam-gus-warm", origin: "CanonStated" }], ...requestView(decF) },
  answer: authoredAnswer(decF, "Pierre's in the morning for Gus's flour? Not my errand.", "no"),
  choice: slugOf(decF, decF.choice.selected),
  // Events as Simulation.Reflection.cs:317-320 writes them: the thought, then the chosen line with its status.
  events: [{ tick: at(30, 18, 16), status: "considered", text: "Pierre's in the morning for Gus's flour? Not my errand.", actId: -1 },
    { tick: at(30, 18, 16), status: "accepted", text: "Get your own flour.", actId: -1 }] });
decF.choice.observer.provenance.requestHash = fnv(reflections.at(-1).request);
acts.push([388, at(30, 18, 16), PAM, 0, P.Saloon, 6, 5, GUS, -1, 0, 2]);
const favFlour = favour(at(30, 18, 16), "Gus", "Pam", "flour", { answer: "no", accepted: false, margin: askMargin(C[DF].yes), actId: 388,
  state: rowState(pamState1815, EAGER.Pam, pamGusBond) });
push("outcomes", { id: "out:44296:Pam:0", about: PAM, visibleAt: at(30, 18, 16), settledAt: at(30, 18, 16), attempts: ["att:44295:Gus:0"], participants: [GUS, PAM],
  kind: "declined", world: { answer: "no", act: "act:388", weighed: true }, base: [favFlour, "act:388"], determinedBy: { rule: "7" },
  mustNotWrite: ["association-for-the-asker", "seven-day-wait"],
  observer: { label: "Gus's flour, asked of Pam", settledLabel: "Pam says no, a weighed no (about 1 in 10 at this margin); the hurt is the Refused act's own" } });
push("perceptions", { id: "per:44296:Gus:0", who: GUS, visibleAt: at(30, 18, 16), of: "att:44295:Gus:0", causes: ["act:388"],
  perceived: { kind: "refused", answer: "no", act: "act:388" }, characterLabel: "Gus: Pam said no",
  observer: { label: "Gus: Pam said no", notPerceived: ["Pam's reasons"], observerLinks: { worldOutcome: "out:44296:Pam:0" } } });
expectation(GUS, "Gus", "Pam", "answers:errand:flour", at(30, 18, 16), "Lived", [[at(30, 18, 16), []]], { characterLabel: "Whether Pam fetches flour when asked", via: "per:44296:Gus:0" });
exLearn("lu:44296:Gus:0", GUS, at(30, 18, 16), EX.gusFlour, [], { at: at(30, 18, 16), outcome: "no", source: "Lived", ref: "act:388" },
  { evidence: ["per:44296:Gus:0"], characterLabel: "Gus: one 'no' from Pam for flour", observer: { label: "Gus: one 'no' from Pam for flour", rule: "9.4 refused by weighing" } });

// ============ Thursday summer 4 (day 31) ============
// == 09:10 Pam asks Penny for beer (base [#90]); the commitment answers at once ==
// Both are at home and awake: the sim's weekday morning (PR B's prototype: Pam up at a median 09:01, Penny leaving for her
// lessons at about 09:30). Canon's summer Thursday has Penny leave for Town at 08:10, so there their only shared
// waking minutes at home would be 08:00-08:10: an implementation ambiguity, named in the fixture.
needs.push({ id: 55, tick: at(31, 9, 10), holder: "Pam", item: "beer", until: at(31, 9, 10) + 4 * DAY, state: "Open", promise: -1, settled: -1 });
push("attempts", { id: "att:45190:Pam:0", actor: PAM, target: PENNY, kind: "errand-ask", decidedBy: { mechanism: "errand-draw" }, base: ["need:55"],
  knownBy: [kb(PAM, "Lived", at(31, 9, 10))], visibleAt: at(31, 9, 10), resolvedAt: at(31, 9, 10), status: "answered",
  characterLabel: "Pam asks Penny to fetch her a beer",
  observer: { label: "Pam asks Penny to fetch her a beer", decidedBy: "base: AskFavours' draw (Simulation.Errands.cs:150 [#90]) x rule 15's factor, 1 with no entries",
    world: { answer: "no" }, outcome: "out:45190:Penny:0" } });
push("perceptions", { id: "per:45190:Penny:0", who: PENNY, visibleAt: at(31, 9, 10), of: "att:45190:Pam:0", causes: [],
  perceived: { kind: "asked", by: PAM, ask: "errand", good: "beer" }, characterLabel: "Penny: Mom asks me to fetch her a beer", observer: { label: "Penny is asked for beer" } });
acts.push([412, at(31, 9, 10), PENNY, 0, P["Home:Trailer"], 4, 5, PAM, -1, 0, 1]);
const favBeer = favour(at(31, 9, 10), "Pam", "Penny", "beer", { answer: "no", accepted: false, margin: REFUSED_MARGIN, actId: 412, wontFetch: true,
  state: rowState(pennyState0910, EAGER.Penny, 1) });
push("outcomes", { id: "out:45190:Penny:0", about: PENNY, visibleAt: at(31, 9, 10), settledAt: at(31, 9, 10), attempts: ["att:45190:Pam:0"], participants: [PAM, PENNY],
  kind: "refused-by-commitment", world: { answer: "no", margin: REFUSED_MARGIN, byCommitment: "cmt:penny-no-alcohol", act: "act:412" }, base: [favBeer, "act:412"],
  determinedBy: { rule: "9.7" }, mustNotWrite: ["probabilistic-yes", "coldness-toward-the-asker"],
  observer: { label: "Pam's ask for beer", settledLabel: "Penny says no; her commitment decides it whatever the margin",
    when: "at once: a commitment answers as line 98 does, with no pending minute (9.7; our reading of rule 7)",
    askParts: `illustrative Ask(Errand) on a Thursday morning: obliged 0.88 (0.5 x regard 0.6 + 0.3 x familiarity 0.8 + 0.2 x understanding 0.7 + ErrandKinBond 0.2) against 0.2 and her state, ${BEER.state}: busy 1 (AskStateOf: her lessons at 10:00, leaving at about 09:30, within 60 minutes; the sim's weekday lessons), eagerness 0.75, bond 1. Canon has no Thursday lessons: busy 0 and a margin of 0.68` } });
push("perceptions", { id: "per:45190:Pam:0", who: PAM, visibleAt: at(31, 9, 10), of: "att:45190:Pam:0", causes: ["act:412"],
  perceived: { kind: "refused", answer: "no", act: "act:412" }, characterLabel: "Pam: Penny turned me down",
  observer: { label: "Pam: Penny turned me down", notPerceived: ["Penny's reason: the commitment is hers"], observerLinks: { worldOutcome: "out:45190:Penny:0" } } });
expectation(PAM, "Pam", "Penny", "answers:errand:beer", at(31, 9, 10), "Lived", [[at(31, 9, 10), []]], { characterLabel: "Whether Penny fetches beer when asked", via: "per:45190:Pam:0" });
exLearn("lu:45190:Pam:0", PAM, at(31, 9, 10), EX.pamBeer, [], { at: at(31, 9, 10), outcome: "no", source: "Lived", ref: "act:412" },
  { evidence: ["per:45190:Pam:0"], characterLabel: "Pam: one 'no' from Penny for beer",
    observer: { label: "Pam: one 'no' from Penny for beer", rule: "9.4 refused by commitment; rule 15", notes: "from Penny's answers only, never from Penny's commitment record" } });

// == Penny's reflection opens (rule 10) ==
push("opportunities", { id: "opp:45190:Penny:0", holder: PENNY, visibleAt: at(31, 9, 10), trigger: "refused-by-commitment",
  causes: ["att:45190:Pam:0", favBeer, "cmt:penny-no-alcohol"], reflection: `reflection:${reflId("opp:45190:Penny:0")}`,
  characterLabel: "She has said no to Mom's beer",
  observer: { label: `Penny refused while obliged (+${REFUSED_MARGIN}): her reflection opens`, rule: "10", budget: "rule 10's own, one a person-day", observerLinks: { outcome: "out:45190:Penny:0" } } });
push("recalls", { id: "rc:45190:Penny:0", holder: PENNY, opportunity: "opp:45190:Penny:0", visibleAt: at(31, 9, 10),
  items: [
    { record: "rd:penny-pam-burden", slot: "top", matched: ["person:Pam", "place:Home:Trailer", "place:Saloon"], observer: { score: { overlap: 3, strength: 0.05, significance: 0.6 } } },
    { record: "rd:penny-saloon-wary", slot: "top", matched: ["place:Saloon", "person:Pam"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.4 } } },
    { record: "rd:penny-pam-care", slot: "top", matched: ["person:Pam"], observer: { score: { overlap: 1, strength: 0.05, significance: 0.8 } } },
  ], characterLabel: "The burden, the Saloon, and her worry for Mom",
  observer: { label: "Penny recalls the burden, the Saloon and her worry",
    active: ["person:Pam", "good:beer", "category:alcohol", "place:Saloon", "place:Home:Trailer", "time:morning", "season:summer", "day:weekday", "situation:refused-by-commitment"],
    bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
    runnersUp: [{ record: "rd:penny-reading-home", overlap: 1, why: "lower strength x significance" },
      { record: "rd:penny-abandoned", overlap: 0, why: "its sourced cue (fall) is not active in summer; a strong cue, not a gate" }] } });
// The evening: home from 18:00 (canon's summer key) until her habitual bedtime, 21:00, as she knows it. The window's end
// is an intention and a sourced habit, not a rule: actual sleep and waking follow the energy rule (Sid, 2026-10-14), and
// nothing here makes her unavailable or asleep at 21:00.
const WINDOW = { from: at(31, 18, 0), until: at(31, 21, 0) };
const pennyMorning = decision({
  id: D1, holder: PENNY, opportunity: "opp:45190:Penny:0", recall: "rc:45190:Penny:0", visibleAt: at(31, 9, 10), question: "what-else-to-offer-after-a-refusal",
  characterLabel: "She won't bring beer. What else could she offer Mom?", label: "After saying no to beer, what does Penny offer instead?",
  alternatives: [
    { slug: "offer", template: { kind: "plan-offer", asked: PAM, place: "Home:Trailer", activity: "PlayedGame", from: WINDOW.from, until: WINDOW.until },
      characterLabel: "Ask Mom to stay in with her this evening: a game, until her usual bedtime",
      observer: { label: "ask Pam to spend this evening at home with her: a game, 18:00 to her habitual bedtime (21:00, a sourced habit, not a rule)" }, reasons: [
        { kind: "supports", code: "refused-while-obliged", value: REFUSED_MARGIN, via: "opportunity", cites: [favBeer, "cmt:penny-no-alcohol"], characterLabel: "She had to say no",
          observer: { provenance: ["src:code-ask-errand"], label: `she was obliged (+${REFUSED_MARGIN}) and could not say yes` } },
        { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:penny-pam-care", AS.pennyCare], characterLabel: "She worries about Mom", observer: { label: "her care (p6), seeded at R 0.05" } },
        { kind: "conflicts", code: "cold", value: 0.05, via: "recall", cites: ["rd:penny-pam-burden", AS.pennyBurden], characterLabel: "The chores fall to her", observer: { label: "the burden (p5), seeded at R 0.05" } },
        { kind: "conflicts", code: "chance:whereabouts", value: PENNY_WHERE_CHANCE, via: "expectation", cites: [EX.pennyWhere, "rd:penny-expects-pam-away"], characterLabel: "She expects Mom out in the evening",
          observer: { label: `p4's seeded 'away' and Monday's lived 'away': ${PENNY_WHERE_CHANCE}` } },
        { kind: "shown", code: "wary-place", via: "recall", cites: ["rd:penny-saloon-wary"], effect: "not-option-object", characterLabel: "The Saloon",
          observer: { label: "she is wary of the Saloon; not this option's object" } } ] },
    { slug: "keep-evening", template: { kind: "carry-on", routine: "evening-at-home", place: "Home:Trailer", from: WINDOW.from, until: WINDOW.until },
      characterLabel: "No offer: her usual evening at home", observer: { label: "no offer: her usual summer evening at home (canon: the dishes from 18:00), Pam out" }, reasons: [
        { kind: "supports", code: "routine", value: 1, via: "routine", cites: [], characterLabel: "Her usual evening",
          observer: { provenance: ["src:penny-schedule"], label: "her sourced summer evening at home (O1, decided): base 1; no recalled warmth toward it" } }] },
  ],
  excluded: [
    { slug: "fetch-beer", why: { code: "commitment", cites: ["cmt:penny-no-alcohol"], observer: { label: "absolute: Sid's D52" } }, characterLabel: "Fetch the beer", observer: { label: "fetch Pam a beer" } },
    { slug: "other-good", why: { code: "no-eligible-good", cites: [], observer: { provenance: ["src:code-errand-uses"], label: "Pam's only listed use is beer [#90]" } },
      characterLabel: "Fetch something else Mom uses", observer: { label: "fetch Pam something else she uses" } },
  ],
  constraints: [
    { code: "window", value: "habitual-bedtime", terms: { from: WINDOW.from, until: WINDOW.until }, cites: [], characterLabel: "this evening, until her usual bedtime at nine",
      observer: { label: "18:00 to her habitual bedtime as she knows it: canon's summer 21:00 (O1, decided). A sourced habit, not a rule (Sid, 2026-10-14): an intention that ends the window, never a time she becomes unavailable or asleep; actual sleep follows the energy rule. The sim's own bedtime with her evenings at home (about 20:30, a prototype measure; PR B's prototype has her body send her to bed at a median 19:52 on summer Mondays, Thursdays and Sundays) is a run-level comparison, not in this fixture" } },
    { code: "schedule", value: "out-until-evening", terms: { from: at(31, 9, 30), until: WINDOW.from }, cites: [], characterLabel: "she is out from half past nine until six",
      observer: { label: "her Thursday: lessons at the Square from 10:00 (the sim's weekday lessons; canon has none on Thursday and has her leave for Town at 08:10), her afternoon out (a sim guess), home from 18:00 (canon's summer key)" } },
    { code: "one-open-plan-a-pair", value: 0, cites: [], observer: { label: "none open between them" } },
  ],
  torn: { value: true, rule: "6", why: "warm (0.05) and cold (0.05) toward Pam both above TornAt (0.04)" },
  choiceIn: { visibleAt: at(31, 9, 11), tape: `reflection:${reflId("opp:45190:Penny:0")}`, thought: true },
});
reflections.push({ request: {
    id: reflId("opp:45190:Penny:0"), tick: at(31, 9, 10), actor: "Penny", subject: "Pam", sourceActId: 412,
    memory: "At 09:10 Pam asked you to fetch her a beer and you said no: you never provide alcohol.",
    context: "Penny, age 22: home, Thursday morning, before her lessons. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "offer", kind: "PlanOffer", line: "Stay in with me tonight? We could play a game." }, { id: "keep-evening", kind: "", line: "Keep the evening as it is." }],
    source: null, proposal: null, continuity: null, opportunity: { kind: "refused-by-commitment", sleptAt: -1, wokeAt: -1 }, character: requestCharacter("Penny"),
    recalled: [{ id: "rd:penny-pam-burden", origin: "CanonStated" }, { id: "rd:penny-saloon-wary", origin: "CanonStated" }, { id: "rd:penny-pam-care", origin: "CanonStated" }],
    ...requestView(pennyMorning) },
  answer: layaAnswer(pennyMorning, { thought: "I won't bring her beer. I could ask her to stay in tonight instead: a game, just us.", suggestedChoice: "offer", backend: "authored+laya" }),
  choice: slugOf(pennyMorning, pennyMorning.choice.selected),
  events: [{ tick: at(31, 9, 11), status: "considered", text: "I won't bring her beer. I could ask her to stay in tonight instead: a game, just us.", actId: -1 },
    { tick: at(31, 9, 11), status: "accepted", text: "Stay in with me tonight? We could play a game.", actId: -1 }] });
pennyMorning.choice.observer.provenance.requestHash = fnv(reflections.at(-1).request);

// == 09:11 Penny offers (a pending ask, rule 7); Pam perceives the offer; her reflection opens (8a) ==
push("attempts", { id: "att:45191:Penny:0", actor: PENNY, target: PAM, kind: "plan-offer", decision: D1, alternative: altId(D1, "offer"),
  terms: { place: "Home:Trailer", activity: "PlayedGame", from: WINDOW.from, until: WINDOW.until },
  knownBy: [kb(PENNY, "Lived", at(31, 9, 11))], visibleAt: at(31, 9, 11), resolvedAt: at(31, 9, 12), status: "answered",
  characterLabel: "Penny asks Mom to stay in with her tonight",
  observer: { label: "Penny asks Pam (a pending ask, answered the next minute: rule 7)", world: { answer: "yes" }, outcome: "out:45192:Pam:0" } });
push("perceptions", { id: "per:45191:Pam:0", who: PAM, visibleAt: at(31, 9, 11), of: "att:45191:Penny:0", causes: [],
  perceived: { kind: "asked", by: PENNY, ask: "plan", activity: "PlayedGame", place: "Home:Trailer" }, characterLabel: "Pam: my girl asks me to stay in with her tonight",
  observer: { label: "Pam is asked to stay in" } });
push("opportunities", { id: "opp:45191:Pam:0", holder: PAM, visibleAt: at(31, 9, 11), trigger: "asked", causes: ["att:45191:Penny:0"],
  reflection: `reflection:${reflId("opp:45191:Pam:0")}`, characterLabel: "Penny asks her to stay in tonight",
  observer: { label: "Pam is asked: her reflection opens", rule: "8a", budget: "8a, one a person-day" } });
push("recalls", { id: "rc:45191:Pam:0", holder: PAM, opportunity: "opp:45191:Pam:0", visibleAt: at(31, 9, 11),
  items: [
    { record: "rd:pam-penny-warm", slot: "top", matched: ["person:Penny"], observer: { score: { overlap: 1, strength: 0.05, significance: 0.9 } } },
    { record: EX.pamBeer, slot: "top", matched: ["person:Penny"], observer: { score: { overlap: 1, strength: 1, significance: 0.5 } } },
    { record: "rd:pam-saloon-wary", slot: "top", matched: ["place:Saloon", "time:evening"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.5 } } },
    { record: "rd:pam-saloon-warm", slot: "low-mood", matched: ["place:Saloon", "time:evening"],
      observer: { score: { overlap: 2, strength: 0.05, significance: 0.5 }, why: "mood low after the refusal: one more by warmth alone (our adaptation of III P13)" } },
  ], characterLabel: "Her girl, this morning's no, and both sides of the Saloon",
  observer: { label: "Pam recalls her girl, this morning's no, and both sides of the Saloon",
    active: ["person:Penny", "place:Home:Trailer", "activity:PlayedGame", "time:evening", "place:Saloon", "day:weekday", "situation:asked"],
    bound: { top: 3, reservePreRun: 1, lowMood: true, max: 4 } } });
const pamAnswer = decision({
  id: D2, holder: PAM, opportunity: "opp:45191:Pam:0", recall: "rc:45191:Pam:0", visibleAt: at(31, 9, 11), question: "answer-plan-offer",
  characterLabel: "Stay in with Penny tonight?", label: "Does Pam agree to stay in tonight?",
  alternatives: [
    { slug: "yes", characterLabel: "yes", observer: { label: "yes" }, reasons: [
      { kind: "supports", code: "obliged", value: yesParts(D2).obliged, via: "ask", cites: ["h:pam-mother-of-penny"], characterLabel: "Her girl is asking",
        observer: { provenance: ["src:code-ask-invitation"], label: "the Ask: regard 0.6 (the household seed), familiarity 0.8 (the household seed), chattiness 0.8, kin bond 0.2; no Fond (they meet daily)" } },
      { kind: "shown", code: "warm", via: "recall", cites: ["rd:pam-penny-warm"], effect: "context-only", characterLabel: "Her girl",
        observer: { label: "reflection context only: the Ask reads regard, not associations (count once)" } },
      { kind: "conflicts", code: "cost", value: yesParts(D2).costs.base, via: "ask", cites: [], observer: { provenance: ["src:code-ask-invitation"], label: "the Ask's base cost" } },
      { kind: "conflicts", code: "state", value: yesParts(D2).costs.state, via: "state", cites: [], characterLabel: "Low after this morning",
        observer: { provenance: ["src:code-state-cost"], label: "low after the refusal (illustrative low 0.13, eagerness 0.5, bond 1)" } },
      { kind: "conflicts", code: "displaced-habit", value: yesParts(D2).costs.displacedHabit, via: "option-side", cites: ["acct:pam-saloon-evenings:Pam"], characterLabel: "Her evening at the Saloon",
        observer: { label: "0.4 x her share of the last 14 evenings at the Saloon (all of them)" } },
      { kind: "shown", code: "wary-place", via: "recall", cites: ["rd:pam-saloon-wary"], effect: "torn-test-only", characterLabel: "The Saloon is not healthy",
        observer: { label: "she judges the Saloon not healthy" } } ] },
    { slug: "not-now", characterLabel: "not tonight", observer: { label: "not now" }, reasons: [
      { kind: "supports", code: "low-mood", value: C[D2]["not-now"].reason.value, via: "state", cites: [], characterLabel: "Low after this morning",
        observer: { label: "low after the refusal: a reason holds (rule 7)" } }] },
    { slug: "no", characterLabel: "no", observer: { label: "no" }, reasons: [
      { kind: "supports", code: "negative-margin", value: r4(-C[D2].no.margin), via: "ask", cites: [], observer: { label: "the margin's other side" } }] },
  ],
  excluded: [],
  constraints: [{ code: "window", value: "offered", terms: { from: WINDOW.from, until: WINDOW.until }, cites: [], characterLabel: "this evening, from six until nine" }],
  torn: { value: true, rule: "6", why: "the displaced place (the Saloon) has warm (0.05) and wary (0.05) sides above TornAt" },
  choiceIn: { visibleAt: at(31, 9, 12), tape: `reflection:${reflId("opp:45191:Pam:0")}`, thought: true, base: [`inv:${at(31, 9, 12)}:Penny:Pam:0`] },
});
reflections.push({ request: {
    id: reflId("opp:45191:Pam:0"), tick: at(31, 9, 11), actor: "Pam", subject: "Penny", sourceActId: -1,
    memory: "At 09:11 Penny asked you to spend this evening at home with her, a game, from 18:00 until 21:00.",
    context: "Pam, age 50: home, Thursday morning. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "yes", kind: "PlanAnswer", line: "All right, sweetie. Tonight, then." }, { id: "not-now", kind: "PlanAnswer", line: "Not tonight, maybe another time." },
      { id: "no", kind: "PlanAnswer", line: "I've got my own plans." }],
    source: null, proposal: null, continuity: null, opportunity: { kind: "asked", sleptAt: -1, wokeAt: -1 }, character: requestCharacter("Pam"),
    recalled: [{ id: "rd:pam-penny-warm", origin: "CanonStated" }, { id: EX.pamBeer, origin: "Lived" }, { id: "rd:pam-saloon-wary", origin: "CanonStated" }, { id: "rd:pam-saloon-warm", origin: "CanonStated" }],
    ...requestView(pamAnswer) },
  answer: layaAnswer(pamAnswer, { thought: "A whole evening in with my girl. The Saloon will keep.", suggestedChoice: "yes", backend: "authored+laya" }),
  choice: slugOf(pamAnswer, pamAnswer.choice.selected),
  events: [{ tick: at(31, 9, 12), status: "considered", text: "A whole evening in with my girl. The Saloon will keep.", actId: -1 },
    { tick: at(31, 9, 12), status: "accepted", text: "All right, sweetie. Tonight, then.", actId: -1 }] });
pamAnswer.choice.observer.provenance.requestHash = fnv(reflections.at(-1).request);

// == 09:12 Pam says yes: the invitations row (no act at the answer), the outcome, the plan ==
// The row records the Ask's own margin (obliged less the Ask's parts), as the code writes it (Simulation.Invitations.cs:38-43);
// the decision's margin also subtracts the Lives-only displaced habit.
const invYes = invitation(at(31, 9, 12), "Penny", "Pam", "PlayedGame", { answer: "yes", accepted: true, margin: askMargin(C[D2].yes), actId: null,
  state: rowState(pamState0911, EAGER.Pam, 1) });
push("outcomes", { id: "out:45192:Pam:0", about: PAM, visibleAt: at(31, 9, 12), settledAt: at(31, 9, 12), attempts: ["att:45191:Penny:0"], participants: [PENNY, PAM],
  kind: "agreed", world: { answer: "yes", plan: "plan:45192:Penny:Pam" }, base: [invYes], determinedBy: { rule: "7" }, mustNotWrite: [],
  observer: { label: "Penny's offer of an evening at home", settledLabel: "Pam says yes: an informal agreement, not a Promise" } });
push("plans", { id: "plan:45192:Penny:Pam", visibleAt: at(31, 9, 12), origin: "att:45191:Penny:0", answer: "yes", formal: false,
  place: "Home:Trailer", activity: "PlayedGame", window: { from: WINDOW.from, until: WINDOW.until },
  members: [{ who: PENNY, role: "asker" }, { who: PAM, role: "asked" }],
  knownBy: [kb(PENNY, "Lived", at(31, 9, 12), { via: "per:45192:Penny:0" }), kb(PAM, "Lived", at(31, 9, 12), { via: D2 })], characterLabel: "An evening at home together, a game, 18:00 to 21:00",
  observer: { label: "an agreed evening at the Trailer, 18:00-21:00",
    formalNote: "an agreed evening, not a Promise: informal, and disappointment is still possible (A, direction 4.3, 2026-10-14)",
    windowBasis: "the asker's habitual bedtime as she knows it (canon's summer 21:00, O1 decided): a sourced habit, not a rule, and an intention; it never makes her unavailable or asleep (Sid, 2026-10-14). A member-private basis, so it is not part of the shared plan" } });
push("perceptions", { id: "per:45192:Penny:0", who: PENNY, visibleAt: at(31, 9, 12), of: "att:45191:Penny:0", causes: [],
  perceived: { kind: "agreed", answer: "yes" }, characterLabel: "Penny: Mom said yes",
  observer: { label: "Penny: Mom said yes", observerLinks: { worldOutcome: "out:45192:Pam:0" } } });
expectation(PENNY, "Penny", "Pam", "answers:plan", at(31, 9, 12), "Lived", [[at(31, 9, 12), []]], { characterLabel: "Whether Mom says yes to a plan", via: "per:45192:Penny:0" });
exLearn("lu:45192:Penny:0", PENNY, at(31, 9, 12), EX.pennyAnswers, [], { at: at(31, 9, 12), outcome: "yes", source: "Lived" },
  { evidence: ["per:45192:Penny:0"], characterLabel: "Penny: one 'yes' from Mom to a plan", observer: { label: "Penny: one 'yes' from Pam to a plan", rule: "9.4 answers" } });

// == 17:28 Pam at the Saloon (since 16:35): the plan's follow-through pick (rule 12), torn: a ChoiceQuestion ==
const PAM_AT_SALOON = at(31, 16, 35); // illustrative: canon's schedule (its spring key, which runs in summer) has her leave for the Saloon at 16:00
push("opportunities", { id: "opp:45688:Pam:0", holder: PAM, visibleAt: at(31, 17, 28), trigger: "follow-through", causes: ["plan:45192:Penny:Pam"],
  question: `question:${SEED}:q:45688:Pam:0`, characterLabel: "Walk home now for the evening she agreed to?",
  observer: { label: "Pam at the Saloon, 17:28: the plan or her usual evening?", rule: "12",
    timingNote: "18:00 less her 32-minute walk home; at the Saloon since 16:35 (illustrative; canon's spring key, which runs in summer, has her leave for it at 16:00)" } });
push("recalls", { id: "rc:45688:Pam:0", holder: PAM, opportunity: "opp:45688:Pam:0", visibleAt: at(31, 17, 28),
  items: [
    { record: "rd:pam-saloon-warm", slot: "top", matched: ["place:Saloon", "time:evening"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.5 } } },
    { record: "rd:pam-saloon-wary", slot: "top", matched: ["place:Saloon", "time:evening"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.5 } } },
    { record: "rd:pam-penny-warm", slot: "top", matched: ["person:Penny"], observer: { score: { overlap: 1, strength: 0.05, significance: 0.9 } } },
  ], characterLabel: "The Saloon's two sides, and her girl",
  observer: { label: "Pam recalls the Saloon's two sides and her girl", active: ["person:Penny", "place:Home:Trailer", "place:Saloon", "time:evening", "day:weekday", "situation:plan-due"],
    bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 } } });
const pamFollow = decision({
  id: D3, holder: PAM, opportunity: "opp:45688:Pam:0", recall: "rc:45688:Pam:0", visibleAt: at(31, 17, 28), question: "plan-follow-through",
  characterLabel: "Go home for the evening she agreed to, or stay?", label: "Walk home for the evening she agreed to, or stay at the Saloon?",
  alternatives: [
    { slug: "keep", template: { kind: "go", place: "Home:Trailer", arrive: WINDOW.from }, characterLabel: "walk home for the evening with Penny", observer: { label: "walk home for the plan" }, reasons: [
      { kind: "supports", code: "intended", value: 0.2, via: "plan", cites: ["plan:45192:Penny:Pam"], characterLabel: "She said yes this morning", observer: { label: "her yes this morning" } },
      { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:pam-penny-warm", AS.pamPenny], characterLabel: "Her girl", observer: { label: "her girl (m1), seeded at R 0.05" } },
      { kind: "shown", code: "wish", via: "recall", cites: ["rd:pam-wish-hobby"], effect: "no-warmth", characterLabel: "Her wish for a hobby",
        observer: { label: "the wish makes the activity possible; it gives no warmth toward a game with Penny (O2, decided)" } } ] },
    { slug: "stay", template: { kind: "carry-on", place: "Saloon" }, characterLabel: "stay at the Saloon", observer: { label: "stay at the Saloon (her haunt, 15:00-24:00)" }, reasons: [
      { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:pam-saloon-warm", AS.pamSaloonWarm], characterLabel: "She enjoys it", observer: { label: "she enjoys it (m3)" } },
      { kind: "supports", code: "habit-weight", value: 4, via: "routine", cites: ["acct:pam-saloon-evenings:Pam"], characterLabel: "Her usual evening",
        observer: { provenance: ["src:code-cards"], label: "her authored haunt weight, 4" } },
      { kind: "conflicts", code: "cold", value: 0.05, via: "recall", cites: ["rd:pam-saloon-wary", AS.pamSaloonWary], characterLabel: "Not healthy, she says", observer: { label: "her own judgment (m4)" } } ] },
  ],
  excluded: [],
  constraints: [{ code: "travel", value: 32, cites: [], characterLabel: "a half-hour walk home", observer: { label: "a 32-minute walk: about 63 tiles (the assessment's section 8) at WalkTilesPerMinute 2 (Simulation.cs:68)" } }],
  torn: { value: true, rule: "6", why: "the Saloon's warm and wary sides both above TornAt" },
  choiceIn: { visibleAt: at(31, 17, 29), tape: `question:${SEED}:q:45688:Pam:0` },
});
choiceQuestions.push({ request: { id: `${SEED}:q:45688:Pam:0`, tick: at(31, 17, 28), holder: "Pam", question: "plan-follow-through",
    choices: [{ id: "keep", kind: "go", line: "Walk home now for the evening with Penny." }, { id: "stay", kind: "carry-on", line: "Stay at the Saloon." }],
    recalled: [{ id: "rd:pam-saloon-warm", origin: "CanonStated" }, { id: "rd:pam-saloon-wary", origin: "CanonStated" }, { id: "rd:pam-penny-warm", origin: "CanonStated" }],
    ...requestView(pamFollow) },
  answer: layaAnswer(pamFollow, { backend: "laya" }), choice: slugOf(pamFollow, pamFollow.choice.selected) });
pamFollow.choice.observer.provenance.requestHash = fnv(choiceQuestions.at(-1).request);
push("attempts", { id: "att:45689:Pam:0", actor: PAM, kind: "stay", decision: D3, alternative: altId(D3, "stay"), terms: { place: "Saloon" },
  knownBy: [kb(PAM, "Lived", at(31, 17, 29))], visibleAt: at(31, 17, 29), resolvedAt: at(31, 18, 0), status: "completed", characterLabel: "Pam stays at the Saloon",
  observer: { label: "Pam stays at the Saloon", world: { place: "Saloon", since: PAM_AT_SALOON, through: at(31, 18, 0) }, outcome: "out:45720:Pam:0" } });

// == 17:48 Penny out in town (her afternoon is a sim guess: the Store, one of her 14:00-18:00 haunts): her follow-through pick (rule 12) ==
push("opportunities", { id: "opp:45708:Penny:0", holder: PENNY, visibleAt: at(31, 17, 48), trigger: "follow-through", causes: ["plan:45192:Penny:Pam"],
  question: `question:${SEED}:q:45708:Penny:0`, characterLabel: "Head home now for the evening she offered?",
  observer: { label: "Penny at the Store, 17:48: head home for the plan?", rule: "12", timingNote: "18:00 less a 12-minute walk (illustrative); her routine would bring her home at about 18:12 anyway, to her usual evening, so the pick is between the plan's evening and her usual one" } });
push("recalls", { id: "rc:45708:Penny:0", holder: PENNY, opportunity: "opp:45708:Penny:0", visibleAt: at(31, 17, 48),
  items: [
    { record: "rd:penny-pam-care", slot: "top", matched: ["person:Pam"], observer: { score: { overlap: 1, strength: 0.05, significance: 0.8 } } },
    { record: "rd:penny-expects-pam-away", slot: "top", matched: ["person:Pam", "time:evening", "place:Home:Trailer"], observer: { score: { overlap: 3, strength: 0.05, significance: 0.5 } } },
    { record: "rd:penny-pam-burden", slot: "top", matched: ["person:Pam", "place:Home:Trailer"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.6 } } },
  ], characterLabel: "Her worry, her expectation, the burden",
  observer: { label: "Penny recalls her worry, her expectation and the burden", active: ["person:Pam", "place:Home:Trailer", "time:evening", "day:weekday", "situation:plan-due"],
    bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 } } });
const pennyFollow = decision({
  id: D4, holder: PENNY, opportunity: "opp:45708:Penny:0", recall: "rc:45708:Penny:0", visibleAt: at(31, 17, 48), question: "plan-follow-through",
  characterLabel: "Head home now for the evening she offered?", label: "Head home now for the evening she offered?",
  alternatives: [
    { slug: "keep", template: { kind: "go", place: "Home:Trailer", arrive: WINDOW.from }, characterLabel: "go home now", observer: { label: "go home now" }, reasons: [
      { kind: "supports", code: "intended", value: 0.2, via: "plan", cites: ["plan:45192:Penny:Pam"], characterLabel: "Her own offer", observer: { label: "her own offer" } },
      { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:penny-pam-care", AS.pennyCare], characterLabel: "Her worry for Mom", observer: { label: "her care (p6)" } },
      { kind: "conflicts", code: "cold", value: 0.05, via: "recall", cites: ["rd:penny-pam-burden", AS.pennyBurden], characterLabel: "The burden", observer: { label: "the burden (p5)" } },
      { kind: "conflicts", code: "chance:whereabouts", value: PENNY_WHERE_CHANCE, via: "expectation", cites: [EX.pennyWhere, "rd:penny-expects-pam-away"], characterLabel: "She expects Mom out in the evening",
        observer: { label: `p4's seeded 'away' and Monday's lived 'away': ${PENNY_WHERE_CHANCE}` } },
      { kind: "supports", code: "chance:answers", value: 1, via: "expectation", cites: [EX.pennyAnswers], characterLabel: "Mom said yes", observer: { label: "Pam's yes this morning: 1" } } ] },
    { slug: "stay", template: { kind: "carry-on", routine: "evening-at-home", place: "Home:Trailer", from: WINDOW.from, until: WINDOW.until }, characterLabel: "carry on as usual",
      observer: { label: "carry on as usual: out until 18:00, then her usual evening at home (canon: the dishes), not the plan's" }, reasons: [
      { kind: "supports", code: "routine", value: 1, via: "routine", cites: [], characterLabel: "Her usual evening", observer: { provenance: ["src:penny-schedule"], label: "her sourced summer evening: base 1" } }] },
  ],
  excluded: [], constraints: [{ code: "travel", value: 12, cites: [], characterLabel: "a short walk home", observer: { label: "a 12-minute walk (illustrative)" } }],
  torn: { value: true, rule: "6", why: "the plan's companion (Pam) has warm and cold sides above TornAt" },
  choiceIn: { visibleAt: at(31, 17, 49), tape: `question:${SEED}:q:45708:Penny:0` },
});
choiceQuestions.push({ request: { id: `${SEED}:q:45708:Penny:0`, tick: at(31, 17, 48), holder: "Penny", question: "plan-follow-through",
    choices: [{ id: "keep", kind: "go", line: "Head home now for the evening with Mom." }, { id: "stay", kind: "carry-on", line: "Carry on as usual this evening." }],
    recalled: [{ id: "rd:penny-pam-care", origin: "CanonStated" }, { id: "rd:penny-expects-pam-away", origin: "CanonStated" }, { id: "rd:penny-pam-burden", origin: "CanonStated" }],
    ...requestView(pennyFollow) },
  answer: layaAnswer(pennyFollow, { backend: "laya" }), choice: slugOf(pennyFollow, pennyFollow.choice.selected) });
pennyFollow.choice.observer.provenance.requestHash = fnv(choiceQuestions.at(-1).request);
push("attempts", { id: "att:45709:Penny:0", actor: PENNY, kind: "go", decision: D4, alternative: altId(D4, "keep"), terms: { place: "Home:Trailer" },
  knownBy: [kb(PENNY, "Lived", at(31, 17, 49))], visibleAt: at(31, 17, 49), resolvedAt: at(31, 18, 1), status: "completed", characterLabel: "Penny heads home",
  observer: { label: "Penny heads home", world: { arrived: at(31, 18, 1), place: "Home:Trailer" } } });

// == 18:00 the plan's window opens; Penny is on her way; Pam is at the Saloon ==
push("outcomes", { id: "out:45720:Pam:0", about: PAM, visibleAt: at(31, 18, 0), settledAt: WINDOW.until, plan: "plan:45192:Penny:Pam",
  attempts: ["att:45689:Pam:0", "att:45709:Penny:0"], participants: [PENNY, PAM],
  timeline: [
    { at: at(31, 18, 0), event: "window-opens", observer: { label: "the window opens: neither is at the Trailer (Pam at the Saloon since 16:35; Penny on her way home since 17:49)" } },
    { at: at(31, 18, 1), event: "waiting", attempts: ["att:45721:Penny:0"], observer: { label: "Penny is home and waits" } },
    { at: at(31, 19, 1), event: "wait-ends", observer: { label: "Penny's hour of waiting ends; the game has not begun" } },
    { at: WINDOW.until, event: "window-closes", observer: { label: "the window closes at Penny's habitual bedtime (21:00, a sourced habit, not a rule): it ends the plan, not her evening; when she sleeps follows the energy rule, outside this excerpt" } },
  ],
  kind: "agreed-absent",
  world: { plannedAct: null, spans: [
    { who: PAM, place: "Saloon", from: PAM_AT_SALOON, to: WINDOW.until },
    { who: PENNY, place: "Store", from: at(31, 14, 0), to: at(31, 17, 49) }, { who: PENNY, place: "travelling", from: at(31, 17, 49), to: at(31, 18, 1) },
    { who: PENNY, place: "Home:Trailer", from: at(31, 18, 1), to: WINDOW.until } ] },
  base: [invYes], determinedBy: { rule: "9.4" },
  mustNotWrite: ["refusal", "rejection", "betrayal", "LetDown", "StoodUp", "Promise-Date", "where-Pam-went-except-as-inferred", "regard", "feeling-for-one-who-expected-nothing", "asleep-or-unavailable-from-the-habitual-bedtime"],
  observer: { label: "the agreed evening at the Trailer", settledLabel: "Pam stayed at the Saloon through the window; the game never began",
    spansFrom: "the base moves stream (illustrative here); Penny's afternoon at the Store is a sim guess (canon's summer key has her in Town from 13:00, tiles 63,15), and no span records sleep: sleep is the moves stream's asleep bit, not the habit" } });
push("perceptions", { id: "per:45720:Pam:0", who: PAM, visibleAt: at(31, 18, 0), of: "plan:45192:Penny:Pam", causes: ["att:45689:Pam:0"],
  perceived: { kind: "not-kept-by-me", place: "Saloon" }, characterLabel: "Pam: still at the Saloon as the evening starts",
  observer: { label: "Pam: still at the Saloon", notPerceived: ["that Penny was walking home for the evening"], observerLinks: { worldOutcome: "out:45720:Pam:0" } } });
push("perceptions", { id: "per:45720:Gus:0", who: GUS, visibleAt: at(31, 18, 0), of: null, causes: [],
  perceived: { kind: "found", subject: PAM, place: "Saloon", band: "18-21", source: "Seen" }, characterLabel: "Gus: Pam is here, as most evenings",
  observer: { label: "Gus: Pam at the Saloon in the evening band", rule: "9.4 seen (passive)", notPerceived: ["the plan with Penny"],
    notes: "her 15-18 sighting (from 16:35) is aggregated with the band's other sightings and not shown" } });
exLearn("lu:45720:Gus:0", GUS, at(31, 18, 0), EX.gusEvening, gusEntries1, { at: at(31, 18, 0), outcome: "found", source: "Seen" },
  { evidence: ["per:45720:Gus:0"], preserved: ["rd:gus-expects-pam-evenings"], characterLabel: "Gus: Pam's evenings, confirmed by sight",
    observer: { label: "Gus: Pam's weekday evenings, confirmed by sight", rule: "9.4 seen", notes: "the seeded entry stays beside the lived ones; the oldest sighting leaves the full window (rule 9)" } });

// == 18:01 Penny is home and waits (rule 13) ==
push("attempts", { id: "att:45721:Penny:0", actor: PENNY, target: PAM, kind: "begin-planned-act", decision: D4, alternative: altId(D4, "keep"),
  terms: { activity: "PlayedGame", place: "Home:Trailer", waitUntil: at(31, 19, 1) },
  knownBy: [kb(PENNY, "Lived", at(31, 18, 1))], visibleAt: at(31, 18, 1), resolvedAt: at(31, 19, 1), status: "unsuccessful", characterLabel: "Penny waits at home for Mom",
  observer: { label: "Penny waits for Pam, up to 60 minutes (rule 13)", world: { began: false, waitedUntil: at(31, 19, 1) }, outcome: "out:45720:Pam:0" } });

// == 18:20 an encounter: ordinary warmth (rule 11); Pam offers Gus a game (torn, past her model cap: the fallback) ==
push("opportunities", { id: "opp:45740:Pam:0", holder: PAM, visibleAt: at(31, 18, 20), trigger: "encounter", causes: ["rd:pam-gus-warm"],
  characterLabel: "Gus is in reach: a game?",
  observer: { label: "Gus in reach: Pam thinks of a game", rule: "11", notes: "ordinary warmth, not a crisis (direction 4.5); a controlled fixture verifies the path, natural runs measure its rate" } });
push("recalls", { id: "rc:45740:Pam:0", holder: PAM, opportunity: "opp:45740:Pam:0", visibleAt: at(31, 18, 20),
  items: [
    { record: "rd:pam-gus-warm", slot: "top", matched: ["person:Gus", "place:Saloon"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.4 } } },
    { record: "rd:pam-wish-hobby", slot: "top", matched: ["place:Saloon"], observer: { cueOrigin: "sourced (Pam:Fri2, weight 2)", score: { overlap: 2, strength: 0, significance: 0.3 } } },
  ], characterLabel: "Gus's kindness, and her wish for a hobby",
  observer: { label: "Pam recalls Gus's kindness and her wish for a hobby", active: ["person:Gus", "place:Saloon", "time:evening", "day:weekday", "situation:encounter"],
    bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 } } });
const GAME = { from: at(31, 18, 21), until: at(31, 18, 51) };
const pamOffer = decision({
  id: D6, holder: PAM, opportunity: "opp:45740:Pam:0", recall: "rc:45740:Pam:0", visibleAt: at(31, 18, 20), question: "encounter-offer",
  characterLabel: "Ask Gus for a game?", label: "Ask Gus for a game?",
  alternatives: [
    { slug: "offer", template: { kind: "plan-offer", asked: GUS, place: "Saloon", activity: "PlayedGame", from: GAME.from, until: GAME.until },
      characterLabel: "ask Gus to play a game now", observer: { label: "ask Gus to play a game now, at the Saloon" }, reasons: [
        { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:pam-gus-warm", AS.pamGus], characterLabel: "Gus is a good guy", observer: { label: "m7, seeded at R 0.05" } },
        { kind: "supports", code: "plan-base", value: 0.1, via: "rule", cites: [], observer: { label: "an encounter offer starts small (PlanBase)" } },
        { kind: "supports", code: "chance:availability", value: 1, via: "expectation", cites: [], characterLabel: "He looks free to her",
          observer: { label: "free by her knowledge: no availability entries for Gus yet (rule 11)" } },
        { kind: "shown", code: "wish", via: "recall", cites: ["rd:pam-wish-hobby"], effect: "no-warmth", characterLabel: "Her wish for a hobby",
          observer: { label: "the wish opens the offer; no warmth (O2, decided)" } } ] },
    { slug: "carry-on", template: { kind: "carry-on", place: "Saloon" }, characterLabel: "carry on at the Saloon", observer: { label: "carry on at the Saloon" }, reasons: [
      { kind: "supports", code: "warm", value: 0.05, via: "option-side", cites: [AS.pamSaloonWarm], characterLabel: "She enjoys it", observer: { label: "she enjoys it" } },
      { kind: "conflicts", code: "cold", value: 0.05, via: "option-side", cites: [AS.pamSaloonWary], characterLabel: "Not healthy", observer: { label: "not healthy" } }] },
  ],
  excluded: [],
  constraints: [{ code: "eligible-activity", value: "PlayedGame@Saloon", cites: [], observer: { label: "a game at the Saloon after 17:00; ages within 6 (both 50) (Simulation.Acts.cs:44-46)" } },
    { code: "one-offer-a-pair-a-day", value: 0, cites: [], observer: { label: "none yet today between them" } }],
  torn: { value: true, rule: "6", why: "carry-on's object (the Saloon) has warm and wary sides above TornAt" },
  choiceIn: { visibleAt: at(31, 18, 20), observer: undefined, fallbackReason: "cap",
    cap: { reached: true, limit: settings.modelQuestionsPerPersonDay, used: [D2, D3] } },
});
pamOffer.choice.observer.capNote = "torn, but Pam's two model questions today are used: the fallback answers on the same alternatives";
push("attempts", { id: "att:45740:Pam:0", actor: PAM, target: GUS, kind: "plan-offer", decision: D6, alternative: altId(D6, "offer"),
  terms: { place: "Saloon", activity: "PlayedGame", from: GAME.from, until: GAME.until },
  knownBy: [kb(PAM, "Lived", at(31, 18, 20))], visibleAt: at(31, 18, 20), resolvedAt: at(31, 18, 21), status: "answered", characterLabel: "Pam asks Gus for a game",
  observer: { label: "Pam asks Gus at the counter (a pending ask, rule 7)", world: { answer: "not-now" }, outcome: "out:45741:Gus:0" } });
push("perceptions", { id: "per:45740:Gus:0", who: GUS, visibleAt: at(31, 18, 20), of: "att:45740:Pam:0", causes: [],
  perceived: { kind: "asked", by: PAM, ask: "plan", activity: "PlayedGame", place: "Saloon" }, characterLabel: "Gus: Pam asks me for a game", observer: { label: "Gus is asked for a game" } });
push("opportunities", { id: "opp:45740:Gus:0", holder: GUS, visibleAt: at(31, 18, 20), trigger: "asked", causes: ["att:45740:Pam:0"],
  reflection: `reflection:${reflId("opp:45740:Gus:0")}`, characterLabel: "Pam asks him for a game",
  observer: { label: "Gus is asked: he recalls pre-run records about Pam, so his reflection opens", rule: "8a", budget: "8a, one a person-day (his first today)" } });
push("recalls", { id: "rc:45740:Gus:0", holder: GUS, opportunity: "opp:45740:Gus:0", visibleAt: at(31, 18, 20),
  items: [
    { record: "rd:gus-expects-pam-evenings", slot: "top", matched: ["person:Pam", "place:Saloon"], observer: { score: { overlap: 2, strength: 0.05, significance: 0.5 } } },
    { record: "rd:gus-pam-wary", slot: "top", matched: ["person:Pam"], observer: { score: { overlap: 1, strength: 0.05, significance: 0.6 } } },
    { record: EX.gusEvening, slot: "top", matched: ["person:Pam"], observer: { score: { overlap: 1, strength: 1, significance: 0.3 } } },
  ], characterLabel: "Pam's evenings here, and his unease about her drinking",
  observer: { label: "Gus recalls Pam's evenings and his unease about her drinking", active: ["person:Pam", "place:Saloon", "activity:PlayedGame", "time:evening", "day:weekday", "situation:asked"],
    bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 } } });
const gusAnswer = decision({
  id: D7, holder: GUS, opportunity: "opp:45740:Gus:0", recall: "rc:45740:Gus:0", visibleAt: at(31, 18, 20), question: "answer-plan-offer",
  characterLabel: "A game with Pam now?", label: "Does Gus play a game with Pam now?",
  alternatives: [
    { slug: "yes", characterLabel: "yes, one game", observer: { label: "yes" }, reasons: [
      { kind: "supports", code: "obliged", value: yesParts(D7).obliged, via: "ask", cites: [], observer: { provenance: ["src:code-ask-invitation"],
        label: "the Ask: regard 0.4 (the friend seed; 'can wait'), familiarity 0.5 (the friends' seed), chattiness 0.8; no Fond, no kin bond" } },
      { kind: "conflicts", code: "cost", value: yesParts(D7).costs.base, via: "ask", cites: [], observer: { provenance: ["src:code-ask-invitation"], label: "the Ask's base cost; the game displaces no place of his" } },
      { kind: "conflicts", code: "working", value: yesParts(D7).costs.working, via: "routine", cites: [], characterLabel: "He is working",
        observer: { provenance: ["src:code-cards", "src:code-state-cost"], label: "mid-shift at the Saloon (his card, 11:00-24:00), read as a duty in hand: a Lives-only cost sized like the Ask's busy term (1; eagerness 0.45, bond 0.45). Contingent on rule 16's open point (whether work weighs a non-errand ask). The Ask's own state cost is 0: AskStateOf's busy is 1 only when leaving for work is due within 60 minutes (Simulation.Invitations.cs:59-61), so today's code alone would give Gus a clear yes (+0.36, beyond ClearBand 0.15)" } },
      { kind: "shown", code: "wary", via: "recall", cites: ["rd:gus-pam-wary"], effect: "context-only", characterLabel: "She drinks too much, he thinks",
        observer: { label: "reflection context only: the Ask reads regard, not associations (count once)" } } ] },
    { slug: "not-now", characterLabel: "not now, he's working", observer: { label: "not now" }, reasons: [
      { kind: "supports", code: "working", value: C[D7]["not-now"].reason.value, via: "routine", cites: [], characterLabel: "He is behind the counter",
        observer: { label: "at the counter mid-shift: a reason holds (rule 7): the Ask's state cost (0) plus the Lives-only working term, contingent on rule 16's open point" } }] },
    { slug: "no", characterLabel: "no", observer: { label: "no" }, reasons: [
      { kind: "supports", code: "negative-margin", value: r4(-C[D7].no.margin), via: "ask", cites: [], observer: { label: "the margin's other side" } }] },
  ],
  excluded: [], constraints: [{ code: "window", value: "offered", terms: { from: GAME.from, until: GAME.until }, cites: [], characterLabel: "now, half an hour" }],
  torn: { value: false, rule: "6", why: "Gus has only a wary side toward Pam (g3); the top two (not now, yes) are not within 10%" },
  choiceIn: { visibleAt: at(31, 18, 21), tape: `reflection:${reflId("opp:45740:Gus:0")}`, thought: true, base: [`inv:${at(31, 18, 21)}:Pam:Gus:0`], fallbackReason: "not-torn" },
});
reflections.push({ request: {
    id: reflId("opp:45740:Gus:0"), tick: at(31, 18, 20), actor: "Gus", subject: "Pam", sourceActId: -1,
    memory: "At 18:20 Pam asked you to play a game with her, now, at the Saloon.",
    context: "Gus, age 50: the Saloon, Thursday evening, at the counter. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "yes", kind: "PlanAnswer", line: "Sure, one game. Set up the board." }, { id: "not-now", kind: "PlanAnswer", line: "Not while I'm behind the counter, Pam. Later?" },
      { id: "no", kind: "PlanAnswer", line: "Not tonight." }],
    source: null, proposal: null, continuity: null, opportunity: { kind: "asked", sleptAt: -1, wokeAt: -1 }, character: requestCharacter("Gus"),
    recalled: [{ id: "rd:gus-expects-pam-evenings", origin: "CanonStated" }, { id: "rd:gus-pam-wary", origin: "CanonStated" }, { id: EX.gusEvening, origin: "Seeded" }],
    ...requestView(gusAnswer) },
  answer: authoredAnswer(gusAnswer, "Busy night. She's here every evening; a game will keep.", "not-now"),
  choice: slugOf(gusAnswer, gusAnswer.choice.selected),
  events: [{ tick: at(31, 18, 21), status: "considered", text: "Busy night. She's here every evening; a game will keep.", actId: -1 },
    { tick: at(31, 18, 21), status: "accepted", text: "Not while I'm behind the counter, Pam. Later?", actId: -1 }] });
gusAnswer.choice.observer.provenance.requestHash = fnv(reflections.at(-1).request);

// == 18:21 Gus: not now. The invitations row says so (no act, no refusal) ==
const invNotNow = invitation(at(31, 18, 21), "Pam", "Gus", "PlayedGame", { answer: "not-now", accepted: null, margin: askMargin(C[D7].yes), actId: null,
  state: rowState(gusState1820, EAGER.Gus, gusPamBond) });
push("outcomes", { id: "out:45741:Gus:0", about: GUS, visibleAt: at(31, 18, 21), settledAt: at(31, 18, 21), attempts: ["att:45740:Pam:0"], participants: [PAM, GUS],
  kind: "not-now", world: { answer: "not-now", act: null }, base: [invNotNow], determinedBy: { rule: "7" },
  mustNotWrite: ["refused-act", "seven-day-wait", "regard"],
  observer: { label: "Pam's offer of a game", settledLabel: "Gus: not now, he is working",
    determinedBy: "the fallback's keyed draw over the same three answers Laya would have had; the 'working' reason assumes rule 16's open point resolves as 'work weighs an ask' (contingent; if work stops asks, this would be busy)" } });
// Pam hears his answer (Told: the spoken line); she perceives "not now", not his reason code (his decision's reasons are his).
push("perceptions", { id: "per:45741:Pam:0", who: PAM, visibleAt: at(31, 18, 21), of: "att:45740:Pam:0", causes: [],
  perceived: { kind: "not-now", answer: "not-now", source: "Told" }, characterLabel: "Pam: Gus says not now; he's behind the counter",
  observer: { label: "Pam: Gus says not now", notPerceived: ["what Gus thinks of her drinking", "how he weighed it"], observerLinks: { worldOutcome: "out:45741:Gus:0" },
    heard: "Not while I'm behind the counter, Pam. Later?" } });
expectation(PAM, "Pam", "Gus", "availability:Saloon:18-21:weekday", at(31, 18, 21), "Lived", [[at(31, 18, 21), []]], { characterLabel: "Whether Gus is free at the Saloon on a weekday evening", via: "per:45741:Pam:0" });
exLearn("lu:45741:Pam:0", PAM, at(31, 18, 21), EX.pamGusAvail, [], { at: at(31, 18, 21), outcome: "not-now", source: "Told" },
  { evidence: ["per:45741:Pam:0"], characterLabel: "Pam: Gus is not free at this hour",
    observer: { label: "Pam: Gus is not free at this hour", rule: "9.4 not now", notes: "availability only: no 'no' entry, no hurt" } });

// == 19:01 Penny's wait ends: agreed, absent ==
push("perceptions", { id: "per:45781:Penny:0", who: PENNY, visibleAt: at(31, 19, 1), of: "plan:45192:Penny:Pam", causes: ["att:45721:Penny:0"],
  perceived: { kind: "agreed-absent", absentFrom: at(31, 18, 1), absentTo: at(31, 19, 1), source: "Sought" },
  unknown: [{ about: "whereabouts", subject: PAM, status: "unknown" }, { about: "reason", subject: PAM, status: "unknown" }],
  inferences: [{ about: "whereabouts", subject: PAM, guess: "place:Saloon", certainty: "guess", basis: ["rd:penny-saloon-wary", EX.pennyWhere], establishes: false }],
  characterLabel: "Penny: she said yes, and she isn't here",
  observer: { label: "Penny: agreed, absent", notAsserted: ["rejection", "betrayal", "where Pam went"],
    notPerceived: ["that Pam stayed at the Saloon from 16:35, by her own choice at 17:29"], observerLinks: { worldOutcome: "out:45720:Pam:0" },
    notes: "an inference, not a sighting: it writes no 'found at the Saloon' entry and is never told as fact" } });
expectation(PENNY, "Penny", "Pam", "followThrough:plan", at(31, 19, 1), "Lived", [[at(31, 19, 1), []]], { characterLabel: "Whether Mom keeps a plan", via: "per:45781:Penny:0" });
exLearn("lu:45781:Penny:0", PENNY, at(31, 19, 1), EX.pennyFollow, [], { at: at(31, 19, 1), outcome: "absent", source: "Sought" },
  { evidence: ["per:45781:Penny:0"], characterLabel: "Penny: one agreed evening, not kept",
    observer: { label: "Penny: one agreed evening, not kept", rule: "9.4 agreed, absent", notes: "learning starts at the first outcome (direction 4.2)" } });
// Disappointment (9.4; O3 decided): D x expected (the plan's chance as she held it at the yes) x invested ((intended +
// her warmth toward Pam) / scale); mood and one cold entry toward the hoped-for evening, a situation.
const expectedTerms = [
  { term: "whereabouts", record: EX.pennyWhere, chance: state([seededAway, ...PENNY_WHERE_LIVED]).chance },
  { term: "answers", record: EX.pennyAnswers, chance: state([{ outcome: "yes", source: "Lived" }]).chance },
  { term: "followThrough", record: EX.pennyFollow, chance: state([]).chance },
];
const expected = r4(expectedTerms.reduce((p, x) => p * x.chance, 1));
const warmth = { record: AS.pennyCare, strength: 0.05 };
const invested = r4((settings.intended + warmth.strength) / settings.disappointmentScale);
const disappointment = r4(settings.disappointment * expected * invested);
const EVENING = ["person:Pam", "activity:PlayedGame", "place:Home:Trailer", "time:evening"];
assoc(PENNY, AS.pennyDisEvening, "situation:evening-at-home-with-Pam", "-", "disappointed", 0, ["plan:45192:Penny:Pam", "per:45781:Penny:0"], at(31, 19, 1), "Lived",
  { top: { situation: EVENING }, characterLabel: "The evening at home with Mom that didn't happen", via: "per:45781:Penny:0",
    observer: { cues: EVENING.map(obj), notes: "created by lu:45781:Penny:1 (its first state is the state before that update: 0)" } });
learn({ id: "lu:45781:Penny:1", who: PENNY, visibleAt: at(31, 19, 1), record: AS.pennyDisEvening, evidence: ["per:45781:Penny:0"],
  previous: { strength: 0 }, new: { strength: disappointment }, mood: { change: -disappointment }, interpretation: { formed: false },
  characterLabel: "Penny: disappointed about the evening",
  observer: { label: "Penny: disappointed about the evening", rule: "9.4 agreed, absent: disappointment (proposed; O3 decided)",
    basis: { formula: "9.4: D x expected x invested (first guesses)", D: settings.disappointment, expectedAt: at(31, 9, 12), expectedTerms, expected,
      intended: settings.intended, warmth, investedScale: settings.disappointmentScale, invested },
    notAsserted: ["rejection", "betrayal"],
    notes: "mood changes once, here; one cold entry toward the hoped-for evening (O3, decided), never toward Pam; no regard change (no act was perceived); no 'no' entry and no whereabouts entry (9.5). A later interpretation aimed at Pam needs Penny's own supported interpretation and must not count this outcome again" } });

// ---- comparisons on Penny's 09:10 choice: isolated, fallback-only ----
const D1alts = pennyMorning.alternatives;
const reasonView = (dec) => Object.fromEntries(dec.alternatives.map((a) => [a.id, a.reasons.map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null, cites: r.cites }))]));
function armConsequences(remove, chanceW, warm, recall, torn) {
  const components = {
    [altId(D1, "offer")]: { ...C[D1].offer, chance: { whereabouts: chanceW, answers: 1, followThrough: 1 }, warm },
    [altId(D1, "keep-evening")]: { ...C[D1]["keep-evening"] } };
  const weights = Object.fromEntries(Object.entries(components).map(([k, c]) => [k, rule5(c)]));
  const probabilities = normalise(weights);
  const base = reasonView(pennyMorning);
  const reasons = Object.fromEntries(Object.entries(base).map(([k, rs]) => [k, rs
    .filter((r) => !(r.code === "chance:whereabouts" && chanceW === 1)) // no entries left: no chance reason
    .filter((r) => !r.cites.some((c) => remove.includes(c)) || r.code === "chance:whereabouts")
    .map((r) => (r.code === "chance:whereabouts" ? { ...r, value: chanceW, cites: r.cites.filter((c) => !remove.includes(c)) } : r))]));
  const armSetHash = setHash(pennyMorning.alternatives.map((a) => ({ slug: a.slug, reasons: reasons[a.id] })), pennyMorning.excluded, pennyMorning.constraints);
  return { opportunity: { id: "opp:45190:Penny:0", trigger: "refused-by-commitment" }, decisionExists: true, recall, torn,
    chance: { whereabouts: chanceW, answers: 1, followThrough: 1 }, sides: { warm, cold: 0.05 }, components, reasons, setHash: armSetHash,
    weights: Object.fromEntries(Object.entries(weights).map(([k, v]) => [k, r4(v)])), probabilities, sampled: pick(ORDER[D1], probabilities, UNIT[D1]) };
}
const pennyHistory = T.records.filter((r) => ["fact", "account", "reading", "commitment"].includes(r.type) && r.visibleAt === PRE
  && (r.holder === PENNY || (r.knownBy || []).some((k) => k.who === PENNY))).map((r) => r.id);
const heldConstant = {
  method: { backend: "fallback-keyed-draw", constants: { k: K, seedStrength: settings.seedStrength, tornAt: settings.tornAt } },
  scene: { minute: at(31, 9, 10), place: "Home:Trailer", present: [PAM, PENNY], ask: favBeer, askMargin: REFUSED_MARGIN },
  options: { eligible: D1alts.map((a) => a.id), excluded: pennyMorning.excluded.map((x) => x.id), constraints: pennyMorning.constraints.map((c) => c.code),
    identityHash: identityHash(D1alts, pennyMorning.excluded, pennyMorning.constraints) },
  randomInput: { stream: "lives-choice", parts: [D1], unit: UNIT[D1] },
  // Her lived experience is held constant: the 'away' rule 14 wrote on day 28 stays in both arms (isolated: nothing is re-run).
  personalState: { traits: { boldness: TRAITS.Penny.boldness }, mood: "even", lived: PENNY_WHERE_LIVED.map((e) => ({ record: EX.pennyWhere, ...e })) },
};
const withArm = { id: "arm:with", history: pennyHistory, consequences: armConsequences([], PENNY_WHERE_CHANCE, 0.05, ["rd:penny-pam-burden", "rd:penny-saloon-wary", "rd:penny-pam-care"], true) };
const rmP6 = ["rd:penny-pam-care", AS.pennyCare];
const withoutP6 = { id: "arm:without-p6", history: pennyHistory.filter((x) => x !== "rd:penny-pam-care"),
  consequences: armConsequences(rmP6, PENNY_WHERE_CHANCE, 0, ["rd:penny-pam-burden", "rd:penny-saloon-wary", "rd:penny-reading-home"], false) };
// Without p4 the association it seeded goes, and so does the expectation's seeded entry; the key keeps the lived entry.
const rmP4 = ["rd:penny-saloon-wary", "rd:penny-expects-pam-away", AS.pennySaloon];
const withoutP4 = { id: "arm:without-p4", history: pennyHistory.filter((x) => !["rd:penny-saloon-wary", "rd:penny-expects-pam-away"].includes(x)),
  consequences: armConsequences(rmP4, P4_LESS_CHANCE, 0.05, ["rd:penny-pam-burden", "rd:penny-pam-care", "rd:penny-reading-home"], true) };
const withoutCommitment = { id: "arm:without-commitment", history: pennyHistory.filter((x) => x !== "cmt:penny-no-alcohol"),
  consequences: { opportunity: null, decisionExists: false } };
const differs = (a, b) => ({
  reasons: canon(a.consequences.reasons) !== canon(b.consequences.reasons),
  probabilities: ORDER[D1].some((x) => a.consequences.probabilities[x] !== b.consequences.probabilities[x]),
  sampledAction: a.consequences.sampled !== b.consequences.sampled });
const fmt = (p) => p.toFixed(3);
const notShown = ["hybrid-counterfactual-outcomes", "laya-answers", "later-lives"];
const runChoice = { backend: "laya", selected: pennyMorning.choice.selected };
push("comparisons", { id: "cmp:45190:Penny:0:without-p6", decision: D1, visibleAt: at(31, 9, 11), scope: "isolated-choice", status: "comparable", question: "history-effect",
  intervention: { field: "history", remove: ["rd:penny-pam-care"], seedsRemoved: [AS.pennyCare], evidenceKeys: ["p6"] },
  heldConstant, arms: [withArm, withoutP6],
  unavoidableDifferences: [{ field: "recall", code: "slot-refilled" }, { field: "torn", code: "no-longer-torn" }, { field: "sides", code: "removed-record" }],
  differs: differs(withArm, withoutP6), notShown, runChoice, wholeRun: { simulated: false },
  observer: { label: "Without p6 (Penny's care for Pam)", question: "Does Penny's care for Pam (p6) change what she offers?",
    effects: `the offer loses its warm reason; offer ${fmt(withArm.consequences.probabilities[altId(D1, "offer")])} -> ${fmt(withoutP6.consequences.probabilities[altId(D1, "offer")])}; the same unit draws keep-evening in both arms`,
    unavoidable: { recall: "the third slot goes to the next candidate (rd:penny-reading-home)", torn: "with no warm side toward Pam the choice is no longer torn; a Laya run would route it to the fallback, so the method is held at the fallback in both arms", sides: "the warm side toward Pam is the record removed" },
    notes: "isolated and fallback-only: neither arm is run forward; hybrid counterfactual outcomes, Laya answers and later lives are not shown" } });
push("comparisons", { id: "cmp:45190:Penny:0:without-p4", decision: D1, visibleAt: at(31, 9, 11), scope: "isolated-choice", status: "comparable", question: "history-effect",
  intervention: { field: "history", remove: ["rd:penny-saloon-wary", "rd:penny-expects-pam-away"], seedsRemoved: [AS.pennySaloon, EX.pennyWhere], evidenceKeys: ["p4"] },
  heldConstant, arms: [withArm, withoutP4],
  unavoidableDifferences: [{ field: "recall", code: "slot-refilled" }, { field: "chance", code: "seeded-entry-removed" }],
  differs: differs(withArm, withoutP4), notShown, runChoice, wholeRun: { simulated: false },
  observer: { label: "Without p4 (Penny's reading of the Saloon and of Pam's evenings)", question: "Does Penny's expectation that Pam is out in the evening (p4) change what she offers?",
    effects: `the offer's whereabouts reason keeps only Monday's lived 'away' (${PENNY_WHERE_CHANCE} -> ${P4_LESS_CHANCE}) and the shown Saloon reason goes; offer ${fmt(withArm.consequences.probabilities[altId(D1, "offer")])} -> ${fmt(withoutP4.consequences.probabilities[altId(D1, "offer")])}; the same unit now draws the offer`,
    unavoidable: { recall: "rd:penny-saloon-wary is gone; the third slot goes to rd:penny-reading-home", chance: `the whereabouts key loses its seeded 'away' and keeps the lived one: ${PENNY_WHERE_CHANCE} -> ${P4_LESS_CHANCE}` },
    notes: "kept: acct:pam-saloon-evenings:Penny (also sourced to p5), and her lived 'away' from day 28, held constant. Reasoned, not run: rule 14 wrote that entry because the seeded entry put the key at 0.5; whether a whole run without p4 would have written it, or reached a similar state later, is a whole-run question" } });
push("comparisons", { id: "cmp:45190:Penny:0:without-commitment", decision: D1, visibleAt: at(31, 9, 11), scope: "isolated-choice", status: "non-comparable", question: "history-effect",
  intervention: { field: "history", remove: ["cmt:penny-no-alcohol"], evidenceKeys: ["p7"] },
  heldConstant, arms: [withArm, withoutCommitment], nonComparable: { reason: "opportunity-absent", detail: "no-refusal-by-commitment" },
  notShown, runChoice, wholeRun: { simulated: false },
  observer: { label: "Without Penny's no-alcohol commitment (D52)", question: "Does Penny's commitment shape what she offers?",
    notes: `without the commitment the Ask is a clear yes at +${REFUSED_MARGIN} (beyond ClearBand 0.15): there is no refusal, so rule 10's opportunity and this decision do not arise. No matched decision is manufactured; what she would do instead is a whole-run question, not shown` } });

// ---- threads: an observer index, never read in the character lens ----
push("threads", { id: "thr:penny-pam-evening", visibleAt: at(31, 9, 10),
  stages: [
    { stage: "history", ids: ["rd:penny-pam-burden", "rd:penny-saloon-wary", "rd:penny-pam-care", "rd:penny-expects-pam-away", "cmt:penny-no-alcohol", "rd:pam-penny-warm", "rd:pam-saloon-warm", "rd:pam-saloon-wary", "rd:gus-expects-pam-evenings"] },
    { stage: "recall", ids: ["rc:45190:Penny:0", "rc:45191:Pam:0", "rc:45688:Pam:0", "rc:45708:Penny:0"] },
    { stage: "alternatives", ids: [D1, D2, D3, D4] },
    { stage: "attempt", ids: ["att:45190:Pam:0", "att:45191:Penny:0", "att:45689:Pam:0", "att:45709:Penny:0", "att:45721:Penny:0"] },
    { stage: "outcome", ids: ["out:45190:Penny:0", "out:45192:Pam:0", "out:45720:Pam:0"] },
    { stage: "perceived", ids: ["per:44280:Gus:0", "per:45190:Penny:0", "per:45190:Pam:0", "per:45191:Pam:0", "per:45192:Penny:0", "per:45720:Pam:0", "per:45720:Gus:0", "per:45781:Penny:0"] },
    { stage: "learning", ids: ["lu:44280:Gus:0", "lu:45190:Pam:0", "lu:45192:Penny:0", "lu:45720:Gus:0", "lu:45781:Penny:0", "lu:45781:Penny:1"] },
    { stage: "comparison", ids: ["cmp:45190:Penny:0:without-p6", "cmp:45190:Penny:0:without-p4", "cmp:45190:Penny:0:without-commitment"] },
  ],
  observer: { title: "An evening at home, offered and agreed", people: [PENNY, PAM, GUS],
    // Each summary line cites the elements it summarises; the structure checker gates them at the line's minute.
    summary: [
      { visibleAt: at(31, 9, 10), label: "Pam asks for beer; Penny's commitment says no", cites: ["att:45190:Pam:0", "out:45190:Penny:0"] },
      { visibleAt: at(31, 9, 12), label: "Penny offers an evening at home; Pam says yes", cites: ["att:45191:Penny:0", "out:45192:Pam:0", "plan:45192:Penny:Pam"] },
      { visibleAt: at(31, 17, 29), label: "Pam keeps her usual evening at the Saloon", cites: [D3, "att:45689:Pam:0"] },
      { visibleAt: at(31, 17, 49), label: "Penny walks home for the plan", cites: [D4, "att:45709:Penny:0"] },
      { visibleAt: at(31, 19, 1), label: "Penny has waited an hour; she doesn't know where Pam is", cites: ["att:45721:Penny:0", "per:45781:Penny:0"] },
      { visibleAt: WINDOW.until, label: "The window closes: agreed, absent", cites: ["out:45720:Pam:0"] } ] } });
push("threads", { id: "thr:gus-flour", visibleAt: at(30, 10, 20),
  stages: [
    { stage: "attempt", ids: ["att:43820:Gus:0", "att:44295:Gus:0"] }, { stage: "recall", ids: ["rc:44295:Pam:0"] }, { stage: "alternatives", ids: [DF] },
    { stage: "outcome", ids: ["out:43820:Penny:0", "out:44296:Pam:0"] }, { stage: "perceived", ids: ["per:43820:Gus:0", "per:44295:Pam:0", "per:44296:Gus:0"] },
    { stage: "learning", ids: ["lu:43820:Gus:0", "lu:44296:Gus:0"] } ],
  observer: { title: "Gus's flour: busy at her lessons, then a weighed no", people: [GUS, PENNY, PAM],
    summary: [{ visibleAt: at(30, 10, 20), label: "Gus would ask Penny for flour; she is teaching: busy", cites: ["att:43820:Gus:0", "out:43820:Penny:0"] },
      { visibleAt: at(30, 18, 16), label: "Pam says no to fetching it", cites: ["out:44296:Pam:0"] }] } });
push("threads", { id: "thr:pam-gus-game", visibleAt: at(31, 18, 20),
  stages: [
    { stage: "recall", ids: ["rc:45740:Pam:0", "rc:45740:Gus:0"] }, { stage: "alternatives", ids: [D6, D7] }, { stage: "attempt", ids: ["att:45740:Pam:0"] },
    { stage: "outcome", ids: ["out:45741:Gus:0"] }, { stage: "perceived", ids: ["per:45740:Gus:0", "per:45741:Pam:0"] }, { stage: "learning", ids: ["lu:45741:Pam:0"] } ],
  observer: { title: "At the Saloon: a game offered while Gus is working", people: [PAM, GUS],
    summary: [{ visibleAt: at(31, 18, 20), label: "Pam, warm toward Gus, offers a game", cites: ["att:45740:Pam:0"] },
      { visibleAt: at(31, 18, 21), label: "Gus: not now, he's working", cites: ["out:45741:Gus:0"] }] } });

const unresolved = [
  { ref: "need:52", asOf: TO, status: "open", observer: { label: "Gus's flour need: Penny busy, Pam declined" } },
  { ref: "need:55", asOf: TO, status: "open", observer: { label: "Pam's beer need: Penny refused; what happens next is outside the excerpt" } },
  { ref: "plan:45192:Penny:Pam", asOf: TO, status: "settled", observer: { label: "settled: agreed, absent; nobody told anyone where Pam was (whereabouts are never told)" } },
];

// ---- seq: creation order, then causes before effects within a minute ----
const REF = /^(h|acct|rd|cmt|as|ex|opp|rc|dec|alt|att|plan|out|per|lu|cmp|thr|act|fav|inv|need|reflection|question):\S+$/;
const TIME = (v) => (v === PRE ? -1 : v);
const LATE = { decisions: ["choice"], attempts: ["status", "resolvedAt"], outcomes: ["kind", "world", "base", "determinedBy", "mustNotWrite", "settledAt", "timeline"], threads: ["stages"] };
function refsOf(v, out = []) {
  if (Array.isArray(v)) v.forEach((x) => refsOf(x, out));
  else if (v && typeof v === "object") for (const [k, x] of Object.entries(v)) { if (k !== "observer") refsOf(x, out); }
  else if (typeof v === "string" && REF.test(v)) out.push(v);
  return out;
}
{
  const all = Object.entries(T).flatMap(([tbl, xs]) => xs.map((x) => ({ tbl, x })));
  const byId = new Map(all.map((e) => [e.x.id, e]));
  const deps = new Map(all.map((e) => {
    const skip = new Set(["id", "observer", "knownBy", ...(LATE[e.tbl] || [])]); // knownBy links a fact to each holder's account: no order
    const refs = Object.entries(e.x).filter(([k]) => !skip.has(k)).flatMap(([, v]) => refsOf(v));
    return [e.x.id, refs.filter((r) => r !== e.x.id && byId.has(r) && TIME(byId.get(r).x.visibleAt) === TIME(e.x.visibleAt))];
  }));
  const done = new Set(), seqd = [];
  const rest = [...all].sort((a, b) => TIME(a.x.visibleAt) - TIME(b.x.visibleAt) || a.x._o - b.x._o);
  while (rest.length) {
    const i = rest.findIndex((e) => deps.get(e.x.id).every((d) => done.has(d)));
    if (i < 0) throw new Error("a cycle among same-minute causes: " + rest.slice(0, 4).map((e) => e.x.id + " <- " + deps.get(e.x.id).filter((d) => !done.has(d))).join("; "));
    const [e] = rest.splice(i, 1);
    done.add(e.x.id); seqd.push(e);
  }
  seqd.forEach((e, n) => { e.x.seq = n; });
  for (const tbl of Object.keys(T)) T[tbl] = T[tbl].map((x) => { const { id, seq, ...r } = x; return { id, seq, ...r }; }).sort((a, b) => a.seq - b.seq);
}
// Undefined optional fields (characterLabel on some records) are dropped by JSON.

// Evaluation pass labels exactly as ResilientReflectionMind.BuildPasses builds them (:119-147): the request's choices
// grouped by (kind, line), groups ordered by kind then line and ids within a group by ordinal comparison, labelled o0,
// o1, ... by slot; Balanced pass r puts group (slot + r) mod n in slot s. The structure checker recomputes this.
function passMaps(choices, mode) {
  if (mode === "Raw") return [Object.fromEntries(choices.map((c) => [c.id, [c.id]]))];
  const byKey = new Map();
  for (const c of choices) { const k = JSON.stringify([c.kind, c.line]); if (!byKey.has(k)) byKey.set(k, { kind: c.kind, line: c.line, ids: [] }); byKey.get(k).ids.push(c.id); }
  const groups = [...byKey.values()].sort((a, b) => ord(a.kind, b.kind) || ord(a.line, b.line));
  for (const g of groups) g.ids.sort(ord);
  const count = mode === "Balanced" ? groups.length : 1;
  return Array.from({ length: count }, (_, r) => Object.fromEntries(groups.map((_, s) => [`o${s}`, groups[(s + r) % groups.length].ids])));
}
for (const t of [...reflections, ...choiceQuestions]) {
  if (!t.answer.evaluations) continue;
  const maps = passMaps(t.request.choices, t.answer.evaluationMode);
  if (maps.length !== t.answer.evaluations.length) throw new Error(`${t.request.id}: ${maps.length} passes expected`);
  t.answer.evaluations.forEach((p, i) => { p.labelsToChoiceIds = maps[i]; });
}

const fixture = {
  format: "under-glass-run", version: 1, illustrative: true,
  note: "ILLUSTRATIVE fixture for the proposed Lives observer recording contract v0.2 (2026-10-14). It is not output from an implemented system: no Lives code exists. Base fields are a tiny excerpt shaped like a real recording (needs and favours are [#90]; act ids are not positions); indices are this excerpt's. Laya probabilities, prompts, receipts and thoughts are invented; fallback weights follow the proposed rules 5 and 7 from the assessment's first guesses; keyed draws are Rng.Unit for the seed below, chosen so the draws give this story.",
  seed: SEED, days: RUN_DAYS, label: "illustrative: Lives v0.2, Penny, Pam and Gus, days 30-31", hash: "illustrative",
  clock: { minutesPerDay: DAY, tick: 5, daysPerWeek: 7, daysPerSeason: 28 },
  names, places: places.map((name) => ({ name })), kinds: kinds.map((name) => ({ name })),
  acts, needs, favours, invitations, reflections, choiceQuestions, characterInspiration,
  lives: {
    contract: "under-glass-lives-observer", version: "0.2", status: "proposed", illustrative: true,
    runId: `run:${SEED}:${RUN_DAYS}:illustrative`, rules: "lives-pilot-proposed-2026-10-14",
    run: { method: { reflection: "laya", thought: "authored", evaluator: MODEL_ID, generator: null, evaluationMode: "Balanced" }, source: { commit: null, dirty: null } },
    excerpt: { from: FROM, to: TO, days: [dayOf(30), dayOf(31)] },
    settings, clock: { unit: "game minutes from midnight of day 0", preRun: PRE, day0: "spring 1, a Monday" },
    models: [{ id: MODEL_ID, role: "evaluator", provider: "laya-serve (local sidecar)", requested: "typed-decisions",
      served: { model: "typed-decisions", repo: "convaiinnovations/laya/typed-decisions" }, revision: null, server: "laya-serve 0.3.22", device: "cuda",
      reproducibility: "unverified",
      observer: { sources: ["sim/UnderGlass.Minds/ReflectionMindOptions.cs:11 (LayaModel default)", "sidecar/README.md (checked against v0.3.22)"],
        verify: "a real run records /health.revisions['typed-decisions'] (revision), /health.device and the routing each pass reports; the one hand-recorded revision is 7b928d82 (reflection-reliability-2026-10-08.md:7). VERIFY the Laya response shape upstream (AGENTS rule 8)" } }],
    characters: names.map((name, who) => ({ who, name, archetype: { id: ASSIGNED[name], packHash: PACK_HASH } })),
    sources, ...T, unresolved,
    observer: {
      note: "Illustrative: generated to the proposed contract v0.2; not output from an implemented system.",
      assumes: [
        "Lives on (proposed switch); Company, Invitations, errands [#90] and reflection on",
        "A dry year: the sim has no weather, so no rain days (canon's rain key has her home until 21:00)",
        "O1 decided (2026-10-14): Penny's sourced routine is the baseline. In a dry year her evenings at home and awake through the 18-21 band are summer Mondays, Thursdays and Sundays (canon's summer key: home from 18:00, leaving for bed at 21:00; besides winter 4 from 19:00, and the 9th and 23rd until 18:40), so the agreed evening is Thursday summer 4 (day 31); a Saturday at home is ours (E), only a comparison",
        "Sid's bedtime decision (2026-10-14): bedtimes are habits, not hard rules. The plan's window ends at her habitual bedtime as she knows it, 21:00: a sourced habit and an intention, not a rule. Actual sleep and waking follow the energy rule; nothing here makes her asleep or unavailable before an actual sleep onset, and none falls in this excerpt. The sim's own bedtime with her evenings at home (about 20:30, a prototype measure, seeds 1-3) is a run-level comparison, not in this fixture",
        "O2 decided: present readings seeded modestly (R 0.05) with their standing; Pam's hobby wish makes the activity possible and gives no warmth",
        "O3 decided: an agreed evening's disappointment is mood and one cold entry toward the hoped-for evening, a situation, never toward Pam",
        "Can wait, kept as default: the Pam-Gus friend seed 0.4; rule 16 limited to errand asks; Gus's being mid-shift read as a not-now reason for a game (contingent on rule 16's open point)",
        "Laya enabled, Balanced evaluation, two model questions a person-day; authored reflection thoughts",
      ],
      ambiguities: [
        "Where Penny teaches: canon's ArchaeologyHouse (the museum), 09:00-14:00; the sim's Square, 10:00-14:00 (DefaultTown.cs:462). The fixture keeps the Square, as PR B's design does",
        "When Penny teaches: canon Tuesday, Wednesday and Friday; the sim Monday to Friday. Wednesday is a lesson day in both, so the Wednesday busy case holds either way. Thursday is one only in the sim, whose morning the fixture follows: her lessons at 10:00 make AskStateOf's busy 1 at 09:10, so the beer ask's margin is " + REFUSED_MARGIN + " (canon's Thursday would give 0.68)",
        "Where Gus is at 10:20 on Wednesday: the sim's cards put him at the Square, his 08:00-11:00 haunt (DefaultTown.cs:432), where Penny teaches; canon has him at the Saloon from 08:00 (his spring key runs in summer). The busy draw needs the sim's: a mechanism fixture, not a canon scene",
        "Thursday morning: the 09:10 ask follows the sim's weekday morning (PR B's prototype: Pam up at a median 09:01, Penny leaving for her lessons at about 09:30; prototype measures, VERIFY when PR B lands). Canon's summer Thursday has Pam's day start at 08:00 and Penny leave for Town at 08:10, so their only canon overlap at home is 08:00-08:10",
        "Penny's afternoons are sim guesses: canon's summer key has her in Town (tiles 96,18 from 08:10; 63,15 from 13:00), which has no sim place; the fixture puts her at the Store, one of her 14:00-18:00 haunts in the sim (DefaultTown.cs:463)",
        "Canon's 21:00 is when she leaves for bed (she shows asleep on arriving); the fixture reads it as the end of the evening as she knows it, an intention. PR B's prototype has her body send her to bed before 21:00 on summer Mondays, Thursdays and Sundays (median 19:52; a prototype measure, VERIFY when PR B lands): whether she is awake through the window is the pilot's run to show, as recorded sleep",
        "Penny's lived 'away' on day 28 assumes she was home and awake through that Monday's evening band, as canon's summer key has it; under the energy rule the pilot confirms it or not",
        "Pam's arrival at the Saloon (16:35) and both walking times are illustrative; canon's spring key, which runs in summer (Pam has no summer key), has her leave for the Saloon at 16:00",
      ],
      omitted: ["each person's own answers and refusals as perceptions: self-knowledge is the act itself",
        "passive sightings that only confirm an expectation, aggregated one update a holder, key and day (Gus's 15-18 sightings of Pam)",
        "sleep: no sleep onset or waking falls inside this excerpt; actual sleep is base data (the moves stream's asleep bit; PR B's proposed nights block), never inferred from a habitual bedtime",
        "request.character.background: null here so the fixture does not copy pre-correction anchor text; real requests carry the drawn anchor, or null when none matches (CharacterInspiration.cs:131-134)",
        "per-holder plan copies (9.2): one shared plan holds only shared terms; each member's outcome is in their own perception"] },
  },
};
for (const c of fixture.lives.characters)
  c.history = fixture.lives.records.filter((r) => r.visibleAt === PRE && ["fact", "account", "reading", "commitment"].includes(r.type)
    && (r.holder === c.who || (r.type === "fact" && r.knownBy.some((k) => k.who === c.who)))).map((r) => r.id);

// Compact pretty-printer: short arrays and objects stay on one line.
function pretty(v, ind = "") {
  const W = 120;
  const one = JSON.stringify(v);
  if (ind.length + one.length <= W || v === null || typeof v !== "object") return one;
  const next = ind + "  ";
  const parts = Array.isArray(v) ? v.map((x) => pretty(x, next)) : Object.entries(v).filter(([, x]) => x !== undefined).map(([k, x]) => JSON.stringify(k) + ": " + pretty(x, next));
  const lines = [];
  let cur = "";
  for (const p of parts) {
    if (p.includes("\n")) { if (cur) lines.push(cur); lines.push(p); cur = ""; continue; }
    if (cur && next.length + cur.length + 2 + p.length > W) { lines.push(cur); cur = ""; }
    cur = cur ? cur + ", " + p : p;
  }
  if (cur) lines.push(cur);
  const [open, close] = Array.isArray(v) ? ["[", "]"] : ["{", "}"];
  return open + "\n" + lines.map((l) => next + l).join(",\n") + "\n" + ind + close;
}
const text = pretty(JSON.parse(JSON.stringify(fixture))) + "\n";
fs.writeFileSync(OUT, text);

// Sizes for the contract: measured on this fixture only (compact JSON; labels, characterLabels and observer prose stripped).
function strip(v) {
  if (Array.isArray(v)) return v.map(strip);
  if (v && typeof v === "object") return Object.fromEntries(Object.entries(v).filter(([k]) => !/^(characterLabel|observer)$/.test(k)).map(([k, x]) => [k, strip(x)]));
  return v;
}
const L = JSON.parse(text).lives;
const avg = (arr) => (arr.length ? Math.round(arr.reduce((s, x) => s + JSON.stringify(strip(x)).length, 0) / arr.length) : 0);
console.log(JSON.stringify({ file: OUT, bytes: Buffer.byteLength(text), seed: SEED,
  avgCompactWithoutObserver: { decision: avg(L.decisions), recall: avg(L.recalls), opportunity: avg(L.opportunities), attempt: avg(L.attempts),
    outcome: avg(L.outcomes), perception: avg(L.perceptions), learning: avg(L.learning), preRunRecord: avg(L.records.filter((r) => r.visibleAt === PRE)), comparison: avg(L.comparisons) },
  units: UNIT,
  decisions: Object.fromEntries(L.decisions.map((d) => [d.id, { backend: d.choice.observer.backend, selected: d.choice.selected, used: d.choice.observer.probabilities, fallback: d.choice.observer.fallback.probabilities }])),
  arms: L.comparisons.map((c) => c.arms.map((a) => [a.id, a.consequences.probabilities, a.consequences.sampled])),
  disappointment }, null, 1));
