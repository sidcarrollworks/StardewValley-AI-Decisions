// Builds ../lives-observer-example-v0.1.json: an ILLUSTRATIVE fixture for the proposed Lives
// observer recording contract v0.1 (../lives-observer-recording-v0.1.md). Numbers that follow from the
// proposed rules 5 and 7 are computed here so the fixture is internally consistent; Laya probabilities,
// sampled units, prompts and thoughts are invented and labelled so.
// Usage: node build-example.cjs ../lives-observer-example-v0.1.json
"use strict";
const fs = require("fs");
const { hash } = require("./rng.cjs");

const out = process.argv[2];
if (!out) { console.error("usage: node build-example.cjs <out.json>"); process.exit(2); }

const r4 = (x) => Math.round(x * 10000) / 10000;
const K = 5;
const SEED = 1;
// Canonical JSON (sorted keys) and FNV-1a 64 as Rng.Hash computes it (one part, 0x1F separator).
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const fnv = (v) => "fnv1a64:" + hash([canon(v)]).toString(16).padStart(16, "0");
const slug = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

// The option set's hash (contract 7.2): eligible slugs with every reason (kind, code, value), excluded
// slugs with their reason code, constraints with their value. Laya and the fallback see exactly this.
function setHash(alts, excluded, constraints) {
  return fnv({
    eligible: alts.map((a) => ({ id: a.slug, reasons: a.reasons.map((r) => JSON.stringify([r.kind, r.code, r.value ?? null])).sort() }))
      .sort((x, y) => (x.id < y.id ? -1 : x.id > y.id ? 1 : 0)),
    excluded: excluded.map((x) => JSON.stringify([x.slug, x.why.code])).sort(),
    constraints: constraints.map((c) => JSON.stringify([c.code, c.value ?? null])).sort(),
  });
}

// ---- the base excerpt: existing recording fields, a few rows only ----
const names = ["Gus", "Pam", "Penny"];
const GUS = 0, PAM = 1, PENNY = 2;
const places = ["Beach", "Home:Trailer", "Saloon", "Square"];
const P = Object.fromEntries(places.map((p, i) => [p, i]));
const kinds = ["Refused", "PlayedGame"];
const D9 = 9 * 1440; // 12960: day 9 (spring 10, a Wednesday), midnight
const at = (h, m) => D9 + h * 60 + m;
const FROM = at(9, 5), TO = at(21, 20);

// ---- rule 5 (proposed): weight = base x chance x exp(k (warm - cold (1 - boldness) + intended - cost)) ----
function rule5(c) {
  const chance = c.chance ? c.chance.whereabouts * c.chance.answers * c.chance.followThrough : 1;
  return c.base * chance * Math.exp(K * (c.warm - c.cold * (1 - c.boldness) + c.intended - c.cost));
}
// ---- rule 7 (proposed): yes e^(k margin), no e^(-k margin), not now e^(k reason) while a reason holds.
// The reason's size is the Ask's own state cost (StateCost: tiredness, a duty due, low mood): our reading (E).
function rule7(c) {
  if (c.answer === "yes" || c.answer === "no") return Math.exp((c.answer === "yes" ? K : -K) * c.margin);
  if (c.answer === "not-now") return Math.exp(K * c.reason.value);
  throw new Error("rule7: unknown answer " + c.answer);
}
const weightOf = (c) => (c.formula === "rule5" ? rule5(c) : rule7(c));
// The Ask's state cost (Simulation.Ask.cs:30-31 [base]): 0.3 x (tired + busy + low) x (1 - eagerness x bond).
const stateCost = (s, eager, bond) => r4(0.3 * (s.tired + s.busy + s.low) * (1 - eager * bond));
const eagerness = (selfRegard, boldness) => ((1 - selfRegard) + (1 - boldness)) / 2;
const bond = (family, liked, fam) => (family ? 1 : Math.min(1, Math.max(0, 0.5 * liked + 0.5 * fam)));
// The three rule-7 answers for one ask, from the Ask's parts.
function answers(dec, a) {
  const margin = r4(a.obliged - Object.values(a.costs).reduce((s, x) => s + x, 0));
  const yes = { id: altId(dec, "yes"), slug: "yes", label: "yes",
    components: { formula: "rule7", answer: "yes", obliged: a.obliged, costs: a.costs, margin }, reasons: a.yesReasons };
  const notNow = { id: altId(dec, "not-now"), slug: "not-now", label: "not now",
    components: { formula: "rule7", answer: "not-now", reason: { code: a.reason.code, value: a.costs.state } }, reasons: a.notNowReasons };
  const no = { id: altId(dec, "no"), slug: "no", label: "no",
    components: { formula: "rule7", answer: "no", margin },
    reasons: [{ kind: "supports", code: "negative-margin", value: r4(-margin), via: "ask", cites: [], label: "the margin's other side" }] };
  return [yes, notNow, no];
}
function normalise(ws) {
  const s = Object.values(ws).reduce((a, b) => a + b, 0);
  return Object.fromEntries(Object.entries(ws).map(([k, v]) => [k, r4(v / s)]));
}
function pick(order, probs, unit) {
  let u = unit;
  for (const id of order) { u -= probs[id]; if (u < 0) return id; }
  return order[order.length - 1];
}
const altId = (dec, s) => `alt:${dec.slice(4)}/${s}`;

// ---------------------------------------------------------------------------------------------
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
  ["src:penny-fall-thu6", "canon", "Characters/Dialogue/Penny:fall_Thu6", "S", "6 hearts: a present feeling placed at the start by our choice (S·R2; O2)"],
  ["src:penny-mon8", "canon", "Characters/Dialogue/Penny:Mon8", "S", "8 hearts; 'eager to move out' shown only, not seeded (O2)"],
  ["src:penny-thu", "canon", "Characters/Dialogue/Penny:Thu", "S", null],
  ["src:pam-thu", "canon", "Characters/Dialogue/Pam:Thu (later page)", "S", "the first-hearing hangover page is NA"],
  ["src:pam-fall-thu", "canon", "Characters/Dialogue/Pam:fall_Thu", "S", null],
  ["src:pam-winter-tue4", "canon", "Characters/Dialogue/Pam:winter_Tue4", "S", null],
  ["src:pam-winter-fri", "canon", "Characters/Dialogue/Pam:winter_Fri", "S", null],
  ["src:pam-saloon", "canon", "Characters/Dialogue/Pam:Saloon, Saloon_Entry, Saloon_Thu", "S", null],
  ["src:pam-summer-thu4", "canon", "Characters/Dialogue/Pam:summer_Thu4", "S", null],
  ["src:pam-fri2", "canon", "Characters/Dialogue/Pam:Fri2", "S", "her wish for a hobby besides 'that saloon every night'; perhaps checkers against herself"],
  ["src:pam-winter-thu", "canon", "Characters/Dialogue/Pam:winter_Thu", "S", "first statement only: a visit to the Saloon to warm up (an attraction); the invitation to the farmer is NA. The assessment's table lists it under m4; filing it with the warm reading is ours (E)"],
  ["src:pam-sun", "canon", "Characters/Dialogue/Pam:Sun", "S", null],
  ["src:gus-thu", "canon", "Characters/Dialogue/Gus:Thu (anchor gus-regulars)", "S", null],
  ["src:gus-sun", "canon", "Characters/Dialogue/Gus:Sun", "S", null],
  ["src:gus-thu8", "canon", "Characters/Dialogue/Gus:Thu8", "S", "8 hearts; chronology VERIFY"],
  ["src:gift-tastes-penny", "canon", "Data/NPCGiftTastes:Penny", "S", "support for D52's characterization, not proof of an absolute refusal to fetch"],
  ["src:game-npc-3667", "code", "game NPC.cs:3667 (drinks re-rolled when Penny names Pam's loved gift)", "S", "support, not proof"],
  ["src:D52", "decision", "docs/decisions.md D52, Sid, 2026-10-13 (on the #90 branch)", "A", "Penny will not fetch alcohol"],
  ["src:direction-4", "decision", "Sid's fourth direction, 2026-10-14 (relayed)", "A", "the commitment is not extended to accepting or drinking alcohol without a separate decision; an agreed evening is informal and may disappoint"],
  ["src:91-answer-7", "decision", "docs/under-glass/specs/needs-bars-spec.md:424, answer 7 [#91] (ErrandHoursBusy, errands only; Sid, 2026-10-13)", "A", "rule 16 follows it for errand asks; wider effects of work stay open"],
  ["src:code-cards", "code", "sim/UnderGlass.Sim/DefaultTown.cs:389-391, 417, 420-421 [base]: Gus works the Saloon 11-24 and haunts the Square 8-11; Pam's and Penny's cards (traits, Penny's job at the Square 10-14, off Sat and Sun)", "built", null],
  ["src:code-errand-uses", "code", "sim/UnderGlass.Sim/DefaultTown.cs:358 [#90]: Pam's only use is beer; Gus's are flour and vinegar (:355)", "#90", null],
  ["src:code-ask-invitation", "code", "sim/UnderGlass.Sim/Simulation.Ask.cs:76-79 [base] (b3224f4 :81-85): Ask(Invitation), obliged 0.5 liked + 0.3 familiarity + 0.2 chattiness (+0.2 Fond, + InviteKinBond 0.2) against 0.15 + state", "built", null],
  ["src:code-ask-errand", "code", "sim/UnderGlass.Sim/Simulation.Ask.cs:89-93 [#90]: Ask(Errand), obliged 0.5 liked + 0.3 familiarity + 0.2 understanding (+0.2 Fond, + ErrandKinBond 0.2) against 0.2 + state + ErrandCost", "#90", null],
  ["src:code-state-cost", "code", "sim/UnderGlass.Sim/Simulation.Ask.cs:30-31 [base]: StateCost = 0.3 (tired + busy + low) (1 - eagerness x bond); AskStateOf, Simulation.Invitations.cs:55-63 [base]: busy means work due within 60 minutes", "built", null],
  ["src:code-askfavours", "code", "sim/UnderGlass.Sim/Simulation.Errands.cs:127-160 [#90]: AskFavours draws someone in reach by bond", "#90", null],
  ["src:code-gathering", "code", "sim/UnderGlass.Sim/Simulation.cs:743 [base] (b3224f4 :760): gatherings only while On(m); the Evening gathering opens at 18:00", "built", null],
  ["src:assessment-7", "editorial", "lives-gap-assessment.md (fourth revision) section 7: the cast's evidence and our filings", "E", null],
  ["src:assessment-9", "editorial", "lives-gap-assessment.md (fourth revision) section 9: the proposed records, rules 1-16 and outcomes", "E", "proposed mechanics, not built"],
].map(([id, kind, key, standing, note]) => ({ id, kind, key, standing, ...(note ? { note } : {}) }));

const PRE = "pre-run";
const kb = (who, how, availableAt = PRE, extra = {}) => ({ who, how, availableAt, ...extra });

// ---- records: history facts, accounts, readings, a commitment, associations, expectations ----
const records = [
  { id: "h:pam-mother-of-penny", type: "fact", kind: "Fact", participants: [PAM, PENNY], standing: "S", origin: "canon",
    sources: ["src:data-characters-penny"], time: { known: false }, stillTrueAtStart: true,
    knownBy: [kb(PENNY, "CanonStated"), kb(PAM, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Pam", origin: "object" }, { key: "person:Penny", origin: "object" }],
    label: "Pam is Penny's mother" },
  { id: "h:penny-father-left", type: "fact", kind: "Past", admission: "R1", participants: [PENNY], absentParticipants: ["her father (not in the town)"],
    standing: "S", origin: "canon", sources: ["src:penny-fall-fri6"],
    time: { known: false, relative: "when she was little" }, recurrence: "once", stillTrueAtStart: "not seen since, by her account",
    knownBy: [kb(PENNY, "CanonStated", PRE, { account: "acct:penny-father-left:Penny" })],
    othersKnowledge: "unknown: canon gives no other account; Pam's knowledge is not asserted",
    cues: [{ key: "relationship:parent", origin: "proposed", standing: "E", note: "would match Pam: any tie from her father to her mother is ours (E, p2); recall only, never a meaning for an absence" },
      { key: "season:fall", origin: "sourced", source: "src:penny-fall-fri6", weight: 2, exclusive: false }],
    visibleAt: PRE, label: "Penny's father left when she was little" },
  { id: "acct:penny-father-left:Penny", type: "account", holder: PENNY, fact: "h:penny-father-left", mode: "Lived", standing: "S·R1",
    origin: "canon", sources: ["src:penny-fall-fri6"], qualifications: ["his stated reason is reported speech; his motives are not known"],
    knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, label: "her account: he left, giving his own reason; not seen since" },
  { id: "rd:penny-abandoned", type: "reading", holder: PENNY, object: "h:penny-father-left", stance: "none", certainty: "stated", standing: "S·R1",
    origin: "canon", sources: ["src:penny-fall-fri6"], effect: "recalled only: no stance toward anyone present (rule 1)",
    cues: [{ key: "season:fall", origin: "sourced", source: "src:penny-fall-fri6", weight: 2, exclusive: false }],
    knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, label: "her reading: she felt abandoned; this time of year brings it back" },
  { id: "h:penny-teaches", type: "fact", kind: "Practice", admission: "R2", participants: [PENNY], standing: "S·R2", origin: "canon",
    sources: ["src:penny-wed", "src:penny-tue", "src:penny-tue4", "src:code-cards"], recurrence: "her job (base: the Square, 10:00-14:00, not Sat or Sun)",
    knownBy: [kb(PENNY, "CanonStated")], othersKnowledge: "not modelled (a public job)", visibleAt: PRE,
    cues: [{ key: "activity:teaching", origin: "object" }, { key: "place:Square", origin: "object" }], label: "Penny teaches Vincent and Jas" },
  { id: "rd:penny-teaching-warm", type: "reading", holder: PENNY, object: "activity:teaching", stance: "warm", certainty: "stated", standing: "S·R2",
    origin: "canon", sources: ["src:penny-wed"], knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "activity:teaching", origin: "object" }], label: "every child deserves a chance (her reading)" },
  { id: "h:pam-saloon-evenings", type: "fact", kind: "Practice", admission: "R2", participants: [PAM], place: "Saloon", standing: "S·R2", origin: "canon",
    recurrence: "almost every night (Gus's words; no count)", countOnce: "one practice with three accounts; never three experiences",
    sources: ["src:penny-mon4", "src:pam-saloon", "src:pam-summer-thu4", "src:gus-thu"],
    knownBy: [kb(PENNY, "CanonStated", PRE, { account: "acct:pam-saloon-evenings:Penny" }), kb(PAM, "CanonStated", PRE, { account: "acct:pam-saloon-evenings:Pam" }),
      kb(GUS, "CanonStated", PRE, { account: "acct:pam-saloon-evenings:Gus" })],
    cues: [{ key: "person:Pam", origin: "object" }, { key: "place:Saloon", origin: "object" }, { key: "time:evening", origin: "object" }],
    visibleAt: PRE, label: "Pam spends her evenings at the Saloon" },
  { id: "acct:pam-saloon-evenings:Penny", type: "account", holder: PENNY, fact: "h:pam-saloon-evenings", mode: "Inferred", standing: "E",
    origin: "authored", sources: ["src:penny-mon4", "src:penny-fri4", "src:assessment-7"],
    qualifications: ["canon gives her reading and the effects at home, not how she knows; filing it as Inferred is ours", "she never claims to see a drink"],
    knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, label: "Penny's account: inferred from what she lives with at home" },
  { id: "acct:pam-saloon-evenings:Pam", type: "account", holder: PAM, fact: "h:pam-saloon-evenings", mode: "Lived", standing: "S·R2",
    origin: "canon", sources: ["src:pam-saloon", "src:pam-summer-thu4"], knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE,
    label: "Pam's account: her own evenings there" },
  { id: "acct:pam-saloon-evenings:Gus", type: "account", holder: GUS, fact: "h:pam-saloon-evenings", mode: "Witnessed", standing: "S·R2",
    origin: "canon", sources: ["src:gus-thu", "src:gus-sun", "src:gus-thu8"], knownBy: [kb(GUS, "CanonStated")], visibleAt: PRE,
    label: "Gus's account: he serves her almost every night" },
  { id: "h:penny-household-burden", type: "fact", kind: "Practice", admission: "R2", participants: [PENNY], standing: "S·R2", origin: "canon",
    sources: ["src:penny-fri4", "src:penny-sun4", "src:penny-dishes"], recurrence: "recurring (no count)",
    knownBy: [kb(PENNY, "CanonStated", PRE, { account: "acct:penny-household-burden:Penny" })],
    othersKnowledge: "Pam's side (she expects the chores done, m2) is not part of this excerpt",
    cues: [{ key: "place:Home:Trailer", origin: "object" }, { key: "activity:chores", origin: "object" }, { key: "person:Pam", origin: "object" }],
    visibleAt: PRE, label: "the dishes and cleaning fall to Penny" },
  { id: "acct:penny-household-burden:Penny", type: "account", holder: PENNY, fact: "h:penny-household-burden", mode: "Lived", standing: "S·R2",
    origin: "canon", sources: ["src:penny-fri4", "src:penny-sun4", "src:penny-dishes", "src:assessment-7"],
    qualifications: ["the chores are Lived", "her stated cause, late nights at the Saloon, is filed as Inferred from effects (E)"],
    knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE, label: "Penny's account: she cleans alone while Pam nurses a headache" },
];

// Associations: one record per holder, object and name (named sentiments are keyed so, Simulation.Feelings.cs:469-471).
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
  pennyWhere: "ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday", gusAfternoon: "ex:Gus:Pam:whereabouts:Saloon:15-18:weekday",
  gusEvening: "ex:Gus:Pam:whereabouts:Saloon:18-21:weekday", pamBeer: "ex:Pam:Penny:answers:errand:beer", pennyAnswers: "ex:Penny:Pam:answers:plan",
  pennyFollow: "ex:Penny:Pam:followThrough:plan", gusPennyAvail: "ex:Gus:Penny:availability:Square:10-14:weekday",
  gusFlour: "ex:Gus:Pam:answers:errand:flour", pamGusAvail: "ex:Pam:Gus:availability:Saloon:18-21:weekday",
};

records.push(
  // Penny's readings
  { id: "rd:penny-saloon-wary", type: "reading", holder: PENNY, object: "place:Saloon", stance: "wary", certainty: "stated", standing: "S·R2",
    origin: "canon", sources: ["src:penny-mon4"], seeds: [AS.pennySaloon], knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "place:Saloon", origin: "object" }, { key: "person:Pam", origin: "object" }], label: "her mother goes to the Saloon too much (her reading)" },
  { id: "rd:penny-expects-pam-away", type: "reading", holder: PENNY, object: "person:Pam", stance: "expects", expects: "whereabouts: away from home in the evening",
    certainty: "stated", standing: "S·R2", origin: "canon", sources: ["src:penny-mon4"], seeds: [EX.pennyWhere],
    knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Pam", origin: "object" }, { key: "time:evening", origin: "object" }, { key: "place:Home:Trailer", origin: "object" }],
    label: "she expects Pam out in the evening" },
  { id: "rd:penny-pam-burden", type: "reading", holder: PENNY, object: "person:Pam", stance: "wary", certainty: "stated", standing: "S·R2",
    origin: "canon", sources: ["src:penny-fri4", "src:penny-sun4"], seeds: [AS.pennyBurden], knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Pam", origin: "object" }, { key: "place:Home:Trailer", origin: "object" }, { key: "place:Saloon", origin: "object" }],
    label: "no help at home: the burden is hers (her reading)" },
  { id: "rd:penny-pam-care", type: "reading", holder: PENNY, object: "person:Pam", stance: "warm", certainty: "stated", standing: "S·R2",
    origin: "canon", sources: ["src:penny-fall-thu6", "src:penny-mon8"], placedAtStart: "our choice (O2): a present feeling carried to the start",
    seeds: [AS.pennyCare], knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Pam", origin: "object" }], label: "she worries about her mother and wants her happy (her reading)" },
  { id: "rd:penny-reading-home", type: "reading", holder: PENNY, object: "activity:reading-at-home", stance: "warm", certainty: "stated", standing: "S·R2",
    origin: "canon", sources: ["src:penny-thu"], seeds: [AS.pennyBook], knownBy: [kb(PENNY, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "activity:reading-at-home", origin: "object" }, { key: "place:Home:Trailer", origin: "object" }],
    label: "in a book she can forget her worries for a while (her reading)" },
  { id: "cmt:penny-no-alcohol", type: "commitment", holder: PENNY, scope: { provide: "alcohol", goodsToday: ["beer"] }, to: "anyone",
    toNote: "#90's behaviour; D52 names her mom, broadening to anyone is ours (E)", force: "Absolute", revisable: false, since: PRE,
    standing: "A", origin: "product-decision", sources: ["src:D52"], support: ["src:gift-tastes-penny", "src:game-npc-3667"],
    excludes: [{ what: "accepting or drinking alcohol", why: "needs a separate decision", source: "src:direction-4" }],
    knownBy: [kb(PENNY, "ProductDecision")], othersKnowledge: "unknown to Pam: she learns only from Penny's answers (rule 15)", visibleAt: PRE,
    cues: [{ key: "category:alcohol", origin: "object" }, { key: "good:beer", origin: "object" }],
    label: "Penny will not provide alcohol (Sid's D52)" },
  // Pam's readings
  { id: "rd:pam-penny-warm", type: "reading", holder: PAM, object: "person:Penny", stance: "warm", certainty: "stated", standing: "S·R2", origin: "canon",
    sources: ["src:pam-thu", "src:pam-fall-thu", "src:pam-winter-tue4", "src:pam-winter-fri"], countOnce: "four sources, one entry",
    seeds: [AS.pamPenny], knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Penny", origin: "object" }], label: "Penny is her only girl; proud of her (her reading)" },
  { id: "rd:pam-saloon-warm", type: "reading", holder: PAM, object: "place:Saloon", stance: "warm", certainty: "stated", standing: "S·R2", origin: "canon",
    sources: ["src:pam-saloon", "src:pam-winter-thu"], countOnce: "two sources, one entry", seeds: [AS.pamSaloonWarm], knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "place:Saloon", origin: "object" }, { key: "time:evening", origin: "object" }], label: "she enjoys the Saloon, looks forward to it, goes there to warm up (her reading)" },
  { id: "rd:pam-saloon-wary", type: "reading", holder: PAM, object: "place:Saloon", stance: "wary", certainty: "stated", standing: "S·R2", origin: "canon",
    sources: ["src:pam-summer-thu4", "src:pam-fri2"], seeds: [AS.pamSaloonWary], knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "place:Saloon", origin: "object" }, { key: "time:evening", origin: "object" }], label: "she always ends up there; not healthy (her own judgment)" },
  { id: "rd:pam-wish-hobby", type: "reading", holder: PAM, object: "activity:game", stance: "none", certainty: "stated", standing: "S·R2", origin: "canon",
    sources: ["src:pam-fri2"],
    effect: "cue only: whether a stated wish gives warmth is O2's second question; reading it as a game with someone is ours (E); in her words it may be checkers against herself",
    knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "activity:game", origin: "object" }, { key: "place:Saloon", origin: "sourced", source: "src:pam-fri2", weight: 2, exclusive: false }],
    label: "she wishes for a hobby besides the Saloon every night, maybe checkers" },
  { id: "rd:pam-gus-warm", type: "reading", holder: PAM, object: "person:Gus", stance: "warm", certainty: "stated", standing: "S·R2", origin: "canon",
    sources: ["src:pam-sun"], seeds: [AS.pamGus], knownBy: [kb(PAM, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Gus", origin: "object" }, { key: "place:Saloon", origin: "object" }], label: "Gus is a good guy (her reading)" },
  // Gus's readings
  { id: "rd:gus-expects-pam-evenings", type: "reading", holder: GUS, object: "person:Pam", stance: "expects", expects: "whereabouts: at the Saloon in each evening band",
    certainty: "stated", standing: "S·R2", origin: "canon", sources: ["src:gus-thu", "src:gus-sun"], seeds: [EX.gusEvening],
    knownBy: [kb(GUS, "CanonStated")], visibleAt: PRE, cues: [{ key: "person:Pam", origin: "object" }, { key: "place:Saloon", origin: "object" }],
    label: "Pam comes almost every night (his reading; he depends on regulars)" },
  { id: "rd:gus-pam-wary", type: "reading", holder: GUS, object: "person:Pam", stance: "wary", certainty: "stated", standing: "S·R2", origin: "canon",
    sources: ["src:gus-thu8"], seeds: [AS.gusPam], knownBy: [kb(GUS, "CanonStated")], visibleAt: PRE,
    cues: [{ key: "person:Pam", origin: "object" }], label: "she drinks too much; he shouldn't judge (his conflicted reading)" },
);

// Associations seeded by rule 1 (R / (1 + lived entries); none lived in this excerpt).
const assoc = (holder, id, object, sign, name, strength, causes, visibleAt = PRE, how = "Seeded") => ({
  id, type: "association", holder, object, sign, name, strength, causes,
  origin: visibleAt === PRE ? "seeded" : "run", standing: visibleAt === PRE ? "E (rule 1, from a sourced reading)" : "D",
  knownBy: [kb(holder, how, visibleAt)], visibleAt,
  cues: [{ key: object, origin: "object" }],
});
records.push(
  assoc(PENNY, AS.pennyCare, "person:Pam", "+", "worried care", 0.05, ["rd:penny-pam-care"]),
  assoc(PENNY, AS.pennyBurden, "person:Pam", "-", "burdened", 0.05, ["rd:penny-pam-burden"]),
  assoc(PENNY, AS.pennySaloon, "place:Saloon", "-", "wary", 0.05, ["rd:penny-saloon-wary"]),
  assoc(PENNY, AS.pennyBook, "activity:reading-at-home", "+", "absorbed", 0.05, ["rd:penny-reading-home"]),
  assoc(PAM, AS.pamPenny, "person:Penny", "+", "proud", 0.05, ["rd:pam-penny-warm"]),
  assoc(PAM, AS.pamSaloonWarm, "place:Saloon", "+", "enjoys", 0.05, ["rd:pam-saloon-warm"]),
  assoc(PAM, AS.pamSaloonWary, "place:Saloon", "-", "uneasy", 0.05, ["rd:pam-saloon-wary"]),
  assoc(PAM, AS.pamGus, "person:Gus", "+", "fond", 0.05, ["rd:pam-gus-warm"]),
  assoc(GUS, AS.gusPam, "person:Pam", "-", "uneasy", 0.05, ["rd:gus-pam-wary"]),
);

// Expectations: keyed records; the seeded entry (source Reading) stays beside the last `livedWindow` lived
// entries (rule 9); chance = (successes + 1) / (entries + 1). `initial` is the state as of `asOf`.
const OK = ["found", "yes", "together", "free"];
function state(entries) {
  const lived = entries.filter((e) => e.source !== "Reading").length;
  const found = entries.filter((e) => OK.includes(e.outcome)).length;
  return { entries: entries.length, lived, seeded: entries.length - lived, found, chance: r4((found + 1) / (entries.length + 1)) };
}
const ex = (holder, hname, subject, key, entries, visibleAt, how, note) => {
  const s = state(entries);
  const asOf = visibleAt === PRE || visibleAt < FROM ? FROM : visibleAt;
  return {
    id: `ex:${hname}:${subject}:${key}`, type: "expectation", holder, subject: names.indexOf(subject), key,
    initial: { asOf, entries, lived: s.lived, seeded: s.seeded, chance: s.chance },
    origin: entries.some((e) => e.source === "Reading") ? "seeded and lived" : entries.length ? "run" : "run (created empty)",
    standing: entries.length ? "E (rule 1) / D" : "D", knownBy: [kb(holder, how, visibleAt)], visibleAt,
    cues: [{ key: `person:${subject}`, origin: "object" }], ...(note ? { note } : {}),
  };
};
const wk = (d, outcome, source = "Seen") => ({ day: d, outcome, source });
records.push(
  ex(PENNY, "Penny", "Pam", "whereabouts:Home:Trailer:18-21:weekday",
    [{ at: PRE, outcome: "away", source: "Reading", cites: "rd:penny-expects-pam-away" }, wk(0, "missed", "seen-absent")], PRE, "Seeded",
    "day 0's miss was written because p4 left her chance at 0.5 (rule 14 needs 0.5 or more); none since"),
  ex(GUS, "Gus", "Pam", "whereabouts:Saloon:15-18:weekday", [1, 2, 3, 4, 7, 8].map((d) => wk(d, "found")), 1 * 1440 + 15 * 60 + 5, "Lived"),
  ex(GUS, "Gus", "Pam", "whereabouts:Saloon:18-21:weekday",
    [{ at: PRE, outcome: "found", source: "Reading", cites: "rd:gus-expects-pam-evenings" }, ...[2, 3, 4, 7, 8].map((d) => wk(d, "found"))], PRE, "Seeded"),
  ex(PAM, "Pam", "Penny", "answers:errand:beer", [], at(9, 10), "Lived"),
  ex(PENNY, "Penny", "Pam", "answers:plan", [], at(9, 12), "Lived"),
  ex(GUS, "Gus", "Penny", "availability:Square:10-14:weekday", [], at(10, 20), "Lived"),
  ex(GUS, "Gus", "Pam", "answers:errand:flour", [], at(18, 16), "Lived"),
  ex(PAM, "Pam", "Gus", "availability:Saloon:18-21:weekday", [], at(18, 21), "Lived"),
  ex(PENNY, "Penny", "Pam", "followThrough:plan", [], at(19, 1), "Lived"),
);

// ---- base excerpt rows (existing fields; needs and favours are [#90]) ----
const acts = [
  [412, at(9, 10), PENNY, 0, P["Home:Trailer"], 4, 5, PAM, -1, 0, 1],
  [431, at(18, 16), PAM, 0, P.Saloon, 6, 5, GUS, -1, 0, 3],
];
const needs = [
  { id: 36, tick: at(9, 10), holder: "Pam", item: "beer", until: at(9, 10) + 4 * 1440, state: "Open", promise: -1, settled: -1 },
  { id: 37, tick: at(10, 20), holder: "Gus", item: "flour", until: at(10, 20) + 4 * 1440, state: "Open", promise: -1, settled: -1 },
];
// Traits from the cards (DefaultTown.cs [base]): Pam chattiness 0.8, boldness 0.7, understanding 0.3, self-regard 0.3;
// Penny 0.4, 0.2, 0.7, 0.3; Gus 0.8, 0.5, 0.6, 0.6.
const EAGER = { Pam: eagerness(0.3, 0.7), Penny: eagerness(0.3, 0.2), Gus: eagerness(0.6, 0.5) };
const favours = [
  { tick: at(9, 10), asker: "Pam", asked: "Penny", item: "beer", accepted: false, margin: 0.6, actId: 412, tired: 0, busy: 1, low: 0,
    bond: 1, eager: r4(EAGER.Penny), wontFetch: true, short: false, running: false },
];
const invitations = [];

// ---- settings (proposed LivesOptions; first guesses from the assessment) ----
const settings = { Lives: true, k: K, intended: 0.2, seedStrength: 0.05, disappointment: 0.05, disappointmentScale: 0.25, tornAt: 0.04, planBase: 0.1, displacedHabit: 0.4,
  clearBand: 0.15, waitMinutes: 60, livedWindow: 6, associationsPerObject: 3, derivedReadingAfter: 3, sourcedCue: 2,
  derivedReadingAfterNote: "a tunable heuristic for when a Derived reading summarises a key; learning starts at the first relevant outcome (direction 4.2)",
  disappointmentNote: "D and the invested scale in 9.4, the assessment's first guesses (0.05 and 0.25)",
  recall: { top: 3, reservePreRun: 1, lowMoodExtra: 1, max: 4, runnersUpShown: 2 }, modelQuestionsPerPersonDay: 2 };

const model = { id: "model:laya:typed-decisions", family: "Laya", name: "typed-decisions", server: "laya-serve 0.3.22",
  sources: ["sim/UnderGlass.Minds/ReflectionMindOptions.cs:11 (LayaModel default)", "sidecar/README.md (checked against v0.3.22)"],
  verify: "a real run records the name and version the server reports; the recording keeps neither today" };

// ---- tables ----
const decisions = [], attempts = [], opportunities = [], recalls = [], outcomes = [], perceptions = [], learning = [], plans = [];
const reflections = [], choiceQuestions = [];

function decision(d) {
  const eligible = d.alternatives.map((a) => a.id);
  const fbW = Object.fromEntries(d.alternatives.map((a) => [a.id, weightOf(a.components)]));
  const fbP = normalise(fbW);
  const ch = d.choiceIn;
  const order = eligible;
  const used = ch.backend === "laya" ? Object.fromEntries(eligible.map((id, i) => [id, ch.laya[i]])) : fbP;
  const selected = pick(order, used, ch.unit);
  const choice = {
    visibleAt: ch.visibleAt, backend: ch.backend, selected, probabilities: used,
    sampled: { stream: "lives-choice", parts: [d.id], unit: ch.unit, unitSource: "illustrative: a real run computes Rng.Unit(seed, \"lives-choice\", decision id)", order },
    fallback: { weights: Object.fromEntries(Object.entries(fbW).map(([k, v]) => [k, r4(v)])), probabilities: fbP, wouldSelect: pick(order, fbP, ch.unit) },
    ...(ch.backend === "laya" ? { laya: { probabilities: used, source: "illustrative: invented for this fixture" } } : {}),
    ...(ch.cap ? { cap: ch.cap } : {}),
    ...(ch.base ? { base: ch.base } : {}),
    provenance: { alternativeSetHash: setHash(d.alternatives, d.excluded, d.constraints), tape: ch.tape ?? null, requestHash: null,
      model: ch.backend === "laya" ? model.id : null, thought: ch.thought ?? null },
  };
  delete d.choiceIn;
  const dec = { ...d, choice };
  decisions.push(dec);
  return dec;
}
// The tape request's view of a decision: the same alternatives, reasons, exclusions and constraints.
function requestView(dec) {
  return {
    alternatives: dec.alternatives.map((a) => ({ id: a.slug, reasons: a.reasons.map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null })) })),
    excluded: dec.excluded.map((x) => ({ id: x.slug, why: x.why.code })),
    constraints: dec.constraints.map((c) => ({ code: c.code, value: c.value ?? null })),
    alternativeSetHash: dec.choice.provenance.alternativeSetHash,
  };
}
const bySlug = (dec, probs) => Object.fromEntries(dec.alternatives.map((a) => [a.slug, probs[a.id]]));
const slugOf = (dec, id) => dec.alternatives.find((a) => a.id === id).slug;
const PLACEHOLDER = "(illustrative placeholder: a real run records the exact text, as ReflectionAnswer does today)";
function layaAnswer(dec, extra = {}) {
  const w = bySlug(dec, dec.choice.laya.probabilities);
  const labels = Object.fromEntries(dec.alternatives.map((a, i) => [String.fromCharCode(65 + i), [a.slug]]));
  return { ...extra, weights: w, backend: extra.backend ?? "laya", model: model.id, evaluationMode: "Raw",
    layaPrompt: PLACEHOLDER, layaResponse: PLACEHOLDER,
    evaluations: [{ pass: 1, prompt: PLACEHOLDER, response: PLACEHOLDER, labelsToChoiceIds: labels, weights: w }],
    note: "illustrative: " + (extra.thought ? "authored thought; " : "") + "Laya weights invented for this fixture" };
}

// == 09:10 Pam asks Penny for beer (base errand ask [#90]); the commitment answers at once ==
attempts.push({ id: "att:13510:Pam:0", actor: PAM, target: PENNY, kind: "errand-ask", visibleAt: at(9, 10), decision: null,
  decidedBy: "base: AskFavours' draw (Simulation.Errands.cs:146 [#90]) x rule 15's factor, 1 with no entries",
  base: ["need:36"], attempted: "asks Penny to fetch a beer", resolvedAt: at(9, 10), status: "answered", outcome: "out:13510:Penny:0",
  label: "Pam asks Penny to fetch her a beer" });
outcomes.push({ id: "out:13510:Penny:0", about: PENNY, visibleAt: at(9, 10), settledAt: at(9, 10), attempts: ["att:13510:Pam:0"], participants: [PAM, PENNY],
  kind: "refused-by-commitment", base: ["favour:412", "act:412"],
  world: { answer: "no", margin: 0.6, byCommitment: "cmt:penny-no-alcohol", act: "act:412", when: "at once: a commitment answers as line 99 does, with no pending minute (9.7; our reading of rule 7)" },
  mustNotWrite: ["a probabilistic yes", "any coldness toward Pam in Penny's records"], observerOnly: true,
  settledLabel: "Penny says no; her commitment decides it whatever the margin", label: "Pam's ask for beer" });
perceptions.push({ id: "per:13510:Pam:0", who: PAM, visibleAt: at(9, 10), of: "att:13510:Pam:0", causes: ["act:412"],
  perceived: { kind: "refused", answer: "no", act: "act:412" }, notPerceived: ["Penny's reason: the commitment is hers"],
  observerLinks: { worldOutcome: "out:13510:Penny:0" }, label: "Pam: Penny turned me down" });
learning.push({ id: "lu:13510:Pam:0", who: PAM, visibleAt: at(9, 10), record: EX.pamBeer, rule: "9.4 refused by commitment; rule 15",
  previous: state([]), new: { ...state([{ outcome: "no", source: "Lived" }]), last: { outcome: "no", source: "Lived", act: "act:412" } },
  evidence: ["per:13510:Pam:0"], note: "from Penny's answers only, never from Penny's commitment record", label: "Pam: one 'no' from Penny for beer" });

// == Penny's reflection opens (rule 10) ==
opportunities.push({ id: "opp:13510:Penny:0", holder: PENNY, visibleAt: at(9, 10), trigger: "refused-by-commitment", rule: "10",
  causes: ["favour:412", "cmt:penny-no-alcohol"], reflection: "reflection:1:opp:13510:Penny:0", budget: "rule 10's own, one a person-day",
  observerLinks: { outcome: "out:13510:Penny:0" }, label: "Penny refused while obliged (+0.60): her reflection opens" });
recalls.push({ id: "rc:13510:Penny:0", holder: PENNY, opportunity: "opp:13510:Penny:0", visibleAt: at(9, 10),
  active: ["person:Pam", "good:beer", "category:alcohol", "place:Saloon", "place:Home:Trailer", "time:morning", "season:spring", "situation:refused-by-commitment"],
  bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
  items: [
    { record: "rd:penny-pam-burden", slot: "top", matched: ["person:Pam", "place:Home:Trailer", "place:Saloon"], score: { overlap: 3, strength: 0.05, significance: 0.6 } },
    { record: "rd:penny-saloon-wary", slot: "top", matched: ["place:Saloon", "person:Pam"], score: { overlap: 2, strength: 0.05, significance: 0.4 } },
    { record: "rd:penny-pam-care", slot: "top", matched: ["person:Pam"], score: { overlap: 1, strength: 0.05, significance: 0.8 } },
  ],
  runnersUp: [{ record: "rd:penny-reading-home", overlap: 1, why: "lower strength x significance" },
    { record: "rd:penny-abandoned", overlap: 0, why: "its sourced cue (fall) is not active in spring; a strong cue, not a gate" }],
  label: "Penny recalls the burden, the Saloon and her worry" });
const D1 = "dec:13510:Penny:0";
const pennyMorning = decision({
  id: D1, holder: PENNY, opportunity: "opp:13510:Penny:0", recall: "rc:13510:Penny:0", visibleAt: at(9, 10),
  question: "what-else-to-offer-after-a-refusal", label: "After saying no to beer, what does Penny offer instead?",
  alternatives: [
    { id: altId(D1, "offer"), slug: "offer", label: "ask Pam to spend this evening at home with her: a game, 18:00 to her bedtime",
      template: { kind: "plan-offer", asked: PAM, place: "Home:Trailer", activity: "PlayedGame", from: at(18, 0), until: at(21, 20) },
      components: { formula: "rule5", base: 0.6, chance: { whereabouts: 0.3333, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0.05, boldness: 0.2, intended: 0, cost: 0 },
      reasons: [
        { kind: "supports", code: "refused-while-obliged", value: 0.6, via: "opportunity", cites: ["favour:412", "cmt:penny-no-alcohol"], provenance: ["src:code-ask-errand"], label: "she was obliged (+0.60) and could not say yes" },
        { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:penny-pam-care", AS.pennyCare], label: "she worries about her mother" },
        { kind: "conflicts", code: "cold", value: 0.05, via: "recall", cites: ["rd:penny-pam-burden", AS.pennyBurden], label: "the chores fall to her" },
        { kind: "conflicts", code: "chance:whereabouts", value: 0.3333, via: "expectation", cites: [EX.pennyWhere, "rd:penny-expects-pam-away"], label: "she expects Pam out in the evening" },
        { kind: "shown", code: "wary-place", via: "recall", cites: ["rd:penny-saloon-wary"], effect: "none: the Saloon is not this option's object", label: "she is wary of the Saloon" },
      ] },
    { id: altId(D1, "keep-evening"), slug: "keep-evening", label: "no offer: keep her evening, reading at home",
      template: { kind: "carry-on", place: "Home:Trailer", activity: "reading-at-home" },
      components: { formula: "rule5", base: 1, warm: 0.05, cold: 0, boldness: 0.2, intended: 0, cost: 0 },
      reasons: [{ kind: "supports", code: "warm", value: 0.05, via: "option-side", cites: ["rd:penny-reading-home", AS.pennyBook], label: "a book lets her forget for a while" }] },
  ],
  excluded: [
    { id: altId(D1, "fetch-beer"), slug: "fetch-beer", label: "fetch Pam a beer", why: { code: "commitment", cites: ["cmt:penny-no-alcohol"], label: "absolute: Sid's D52" } },
    { id: altId(D1, "other-good"), slug: "other-good", label: "fetch Pam something else she uses", why: { code: "no-eligible-good", cites: ["src:code-errand-uses"], label: "Pam's only listed use is beer [#90]" } },
  ],
  constraints: [
    { code: "schedule", value: "teaching at the Square 10:00-14:00 on weekdays; she leaves about 09:35", cites: ["h:penny-teaches"] },
    { code: "window", value: "18:00 to her usual bedtime as she knows it: the sim's, about 21:20 (O1(a))", cites: [] },
    { code: "one-open-plan-a-pair", value: "none open between them", cites: [] },
  ],
  torn: { value: true, rule: "6", why: "warm (0.05) and cold (0.05) toward Pam both above TornAt (0.04)" },
  choiceIn: { backend: "laya", visibleAt: at(9, 11), laya: [0.46, 0.54], unit: 0.163,
    tape: "reflection:1:opp:13510:Penny:0", thought: { backend: "authored", imagined: true, note: "the thought is private and never evidence" } },
});
reflections.push({ request: {
    id: `${SEED}:opp:13510:Penny:0`, tick: at(9, 10), actor: "Penny", subject: "Pam", sourceActId: 412,
    memory: "At 09:10 Pam asked you to fetch her a beer and you said no: you never provide alcohol.",
    context: "Penny, age 22: home, morning; teaching at 10:00. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "offer", kind: "PlanOffer", line: "Stay in with me tonight? We could play a game." }, { id: "keep-evening", kind: "", line: "Keep the evening for her book." }],
    opportunity: { kind: "refused-by-commitment", sleptAt: -1, wokeAt: -1 },
    recalled: [{ id: "rd:penny-pam-burden", origin: "CanonStated" }, { id: "rd:penny-saloon-wary", origin: "CanonStated" }, { id: "rd:penny-pam-care", origin: "CanonStated" }],
    ...requestView(pennyMorning) },
  answer: layaAnswer(pennyMorning, { thought: "I won't bring her beer. I could ask her to stay in tonight instead: a game, just us.", suggestedChoice: "offer", backend: "authored+laya" }),
  choice: slugOf(pennyMorning, pennyMorning.choice.selected),
  events: [{ tick: at(9, 11), status: "considered", text: "I won't bring her beer. I could ask her to stay in tonight instead: a game, just us.", actId: -1 },
    { tick: at(9, 11), status: "accepted", text: "Stay in with me tonight? We could play a game.", actId: -1 }] });
pennyMorning.choice.provenance.requestHash = fnv(reflections[0].request);

// == 09:11 Penny offers (a pending ask, rule 7); Pam's reflection opens (rule 8a) ==
attempts.push({ id: "att:13511:Penny:0", actor: PENNY, target: PAM, kind: "plan-offer", visibleAt: at(9, 11), decision: D1,
  alternative: altId(D1, "offer"), intention: "an evening at home with Pam, a game, 18:00 to 21:20",
  attempted: "asks Pam (a pending ask, answered the next minute: rule 7)", resolvedAt: at(9, 12), status: "answered", outcome: "out:13512:Pam:0",
  label: "Penny asks Pam to stay in tonight" });
opportunities.push({ id: "opp:13511:Pam:0", holder: PAM, visibleAt: at(9, 11), trigger: "asked", rule: "8a", causes: ["att:13511:Penny:0"],
  reflection: "reflection:1:opp:13511:Pam:0", budget: "8a, one a person-day", label: "Pam is asked: her reflection opens" });
recalls.push({ id: "rc:13511:Pam:0", holder: PAM, opportunity: "opp:13511:Pam:0", visibleAt: at(9, 11),
  active: ["person:Penny", "place:Home:Trailer", "activity:PlayedGame", "time:evening", "place:Saloon", "situation:asked"],
  bound: { top: 3, reservePreRun: 1, lowMood: true, max: 4 },
  items: [
    { record: "rd:pam-penny-warm", slot: "top", matched: ["person:Penny"], score: { overlap: 1, strength: 0.05, significance: 0.9 } },
    { record: EX.pamBeer, slot: "top", matched: ["person:Penny"], score: { overlap: 1, strength: 1, significance: 0.5 } },
    { record: "rd:pam-saloon-wary", slot: "top", matched: ["place:Saloon", "time:evening"], score: { overlap: 2, strength: 0.05, significance: 0.5 } },
    { record: "rd:pam-saloon-warm", slot: "low-mood", matched: ["place:Saloon", "time:evening"], score: { overlap: 2, strength: 0.05, significance: 0.5 }, why: "mood low after the refusal: one more by warmth alone (our adaptation of III P13)" },
  ],
  label: "Pam recalls her girl, this morning's no, and both sides of the Saloon" });
const D2 = "dec:13511:Pam:0";
const pamState0911 = { tired: 0, busy: 0, low: 0.1333 }; // illustrative: low after the refusal
const pamAnswer = decision({
  id: D2, holder: PAM, opportunity: "opp:13511:Pam:0", recall: "rc:13511:Pam:0", visibleAt: at(9, 11),
  question: "answer-plan-offer", label: "Does Pam agree to stay in tonight?",
  alternatives: answers(D2, {
    obliged: 0.9, costs: { base: 0.15, state: stateCost(pamState0911, EAGER.Pam, 1), displacedHabit: 0.4 },
    reason: { code: "low-mood" },
    yesReasons: [
      { kind: "supports", code: "obliged", value: 0.9, via: "ask", cites: ["h:pam-mother-of-penny"], provenance: ["src:code-ask-invitation"],
        label: "the Ask: regard 0.6 (the household seed), familiarity 0.8 (the household seed), chattiness 0.8, kin bond 0.2; no Fond (they meet daily)" },
      { kind: "shown", code: "warm", via: "recall", cites: ["rd:pam-penny-warm"], effect: "reflection context only: the Ask reads regard, not associations (count once)", label: "her girl" },
      { kind: "conflicts", code: "cost", value: 0.15, via: "ask", cites: [], provenance: ["src:code-ask-invitation"], label: "the Ask's base cost" },
      { kind: "conflicts", code: "state", value: stateCost(pamState0911, EAGER.Pam, 1), via: "state", cites: [], provenance: ["src:code-state-cost"], label: "low after the refusal (illustrative low 0.13, eagerness 0.5, bond 1)" },
      { kind: "conflicts", code: "displaced-habit", value: 0.4, via: "option-side", cites: ["acct:pam-saloon-evenings:Pam"], label: "0.4 x her share of the last 14 evenings at the Saloon (all of them)" },
      { kind: "shown", code: "wary-place", via: "recall", cites: ["rd:pam-saloon-wary"], effect: "torn test only", label: "she judges the Saloon not healthy" },
    ],
    notNowReasons: [{ kind: "supports", code: "low-mood", value: stateCost(pamState0911, EAGER.Pam, 1), via: "state", cites: [], label: "low after the refusal: a reason holds (rule 7)" }],
  }),
  excluded: [],
  constraints: [{ code: "same-set-both-backends", value: "yes, not now, no", cites: [] }],
  torn: { value: true, rule: "6", why: "the displaced place (the Saloon) has warm (0.05) and wary (0.05) sides above TornAt" },
  choiceIn: { backend: "laya", visibleAt: at(9, 12), laya: [0.71, 0.21, 0.08], unit: 0.412, tape: "reflection:1:opp:13511:Pam:0",
    thought: { backend: "authored", imagined: true }, base: ["invitation:13512:Penny:Pam"] },
});
reflections.push({ request: {
    id: `${SEED}:opp:13511:Pam:0`, tick: at(9, 11), actor: "Pam", subject: "Penny", sourceActId: -1,
    memory: "At 09:11 Penny asked you to spend this evening at home with her, a game, from 18:00.",
    context: "Pam, age 50: home, morning. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "yes", kind: "PlanAnswer", line: "All right, sweetie. Tonight, then." }, { id: "not-now", kind: "PlanAnswer", line: "Not tonight, maybe another time." }, { id: "no", kind: "PlanAnswer", line: "I've got my own plans." }],
    opportunity: { kind: "asked", sleptAt: -1, wokeAt: -1 },
    recalled: [{ id: "rd:pam-penny-warm", origin: "CanonStated" }, { id: EX.pamBeer, origin: "Lived" }, { id: "rd:pam-saloon-wary", origin: "CanonStated" }, { id: "rd:pam-saloon-warm", origin: "CanonStated" }],
    ...requestView(pamAnswer) },
  answer: layaAnswer(pamAnswer, { thought: "A whole evening in with my girl. The Saloon will keep.", suggestedChoice: "yes", backend: "authored+laya" }),
  choice: slugOf(pamAnswer, pamAnswer.choice.selected),
  events: [{ tick: at(9, 12), status: "considered", text: "A whole evening in with my girl. The Saloon will keep.", actId: -1 },
    { tick: at(9, 12), status: "accepted", text: "All right, sweetie. Tonight, then.", actId: -1 }] });
pamAnswer.choice.provenance.requestHash = fnv(reflections[1].request);
const yesMargin = pamAnswer.alternatives[0].components.margin;
invitations.push({ tick: at(9, 12), asker: "Penny", asked: "Pam", kind: "PlayedGame", accepted: true, margin: yesMargin, actId: -1,
  tired: 0, busy: 0, low: pamState0911.low, bond: 1, eager: r4(EAGER.Pam) });

outcomes.push({ id: "out:13512:Pam:0", about: PAM, visibleAt: at(9, 12), settledAt: at(9, 12), attempts: ["att:13511:Penny:0"], participants: [PENNY, PAM],
  kind: "agreed", base: ["invitation:13512:Penny:Pam"], world: { answer: "yes", plan: "plan:13512:Penny:Pam" }, observerOnly: true,
  settledLabel: "Pam says yes: an informal agreement, not a Promise", label: "Penny's offer of an evening at home" });
plans.push({ id: "plan:13512:Penny:Pam", visibleAt: at(9, 12), origin: "att:13511:Penny:0", answer: "yes", formal: false,
  formalNote: "an agreed evening, not a Promise: informal, and disappointment is still possible (A, direction 4.3, 2026-10-14)",
  place: "Home:Trailer", activity: "PlayedGame", window: { from: at(18, 0), until: at(21, 20), untilBasis: "Penny's usual bedtime as she knows it (O1(a))" },
  members: [{ who: PENNY, role: "asker" }, { who: PAM, role: "asked" }],
  knownBy: [kb(PENNY, "Lived", at(9, 12)), kb(PAM, "Lived", at(9, 12))],
  label: "an evening at home together, 18:00-21:20" });
perceptions.push({ id: "per:13512:Penny:0", who: PENNY, visibleAt: at(9, 12), of: "att:13511:Penny:0", causes: ["invitation:13512:Penny:Pam"],
  perceived: { kind: "agreed", answer: "yes" }, observerLinks: { worldOutcome: "out:13512:Pam:0" }, label: "Penny: Mom said yes" });
learning.push({ id: "lu:13512:Penny:0", who: PENNY, visibleAt: at(9, 12), record: EX.pennyAnswers, rule: "9.4 answers",
  previous: state([]), new: { ...state([{ outcome: "yes", source: "Lived" }]), last: { outcome: "yes", source: "Lived" } }, evidence: ["per:13512:Penny:0"],
  label: "Penny: one 'yes' from Pam to a plan" });

// == 10:20 Gus's flour need; his draw lands on Penny at her lessons: busy (rule 16, errand asks only) ==
attempts.push({ id: "att:13580:Gus:0", actor: GUS, target: PENNY, kind: "errand-ask", visibleAt: at(10, 20), decision: null,
  decidedBy: "base: AskFavours' draw among those in reach at the Square (Simulation.Errands.cs:146 [#90]; Free ignores work, Simulation.cs:890 [base]) x rule 15's factor, 1 with no entries",
  base: ["need:37"], attempted: "would ask Penny to fetch flour", resolvedAt: at(10, 20), status: "busy", outcome: "out:13580:Penny:0",
  label: "Gus, at the Square before his shift, would ask Penny for flour" });
outcomes.push({ id: "out:13580:Penny:0", about: PENNY, visibleAt: at(10, 20), settledAt: at(10, 20), attempts: ["att:13580:Gus:0"], participants: [GUS, PENNY],
  kind: "busy", determinedBy: { rule: "16", sources: ["src:assessment-9", "src:91-answer-7"],
    note: "the person drawn is at her job place (the Square) in working hours (10:00-14:00): busy, no ask is made; rule 16 applies to errand asks, as the assessment reads #91's answer 7" },
  world: { asked: false, place: "Square", job: "teaching, 10:00-14:00" }, mustNotWrite: ["an answer", "a refusal", "regard", "dread"], observerOnly: true,
  settledLabel: "Penny is teaching: busy, not a refusal; she is not asked, so she perceives nothing", label: "Gus's flour, a first try" });
perceptions.push({ id: "per:13580:Gus:0", who: GUS, visibleAt: at(10, 20), of: "att:13580:Gus:0", causes: [],
  perceived: { kind: "busy", subject: PENNY, reason: "at her lessons", place: "Square", source: "Seen" }, notPerceived: ["whether she would have said yes"],
  observerLinks: { worldOutcome: "out:13580:Penny:0" }, label: "Gus: Penny is busy teaching" });
learning.push({ id: "lu:13580:Gus:0", who: GUS, visibleAt: at(10, 20), record: EX.gusPennyAvail, rule: "16; 9.4 busy",
  previous: state([]), new: { ...state([wk(9, "busy")]), last: { day: 9, outcome: "busy", source: "Seen" } }, evidence: ["per:13580:Gus:0"],
  note: "availability only: no answer, no refusal, no regard, no dread", label: "Gus: Penny is busy at this hour" });

// == 17:30 Pam's follow-through pick (rule 12): torn, a ChoiceQuestion ==
opportunities.push({ id: "opp:14010:Pam:0", holder: PAM, visibleAt: at(17, 30), trigger: "follow-through", rule: "12",
  causes: ["plan:13512:Penny:Pam"], question: "question:1:q:14010:Pam:0",
  timingNote: "her home stay ended 30 minutes before the window; treated as the plan's follow-through pick (illustrative timing)",
  label: "Pam at home, 17:30: the plan or her usual evening?" });
recalls.push({ id: "rc:14010:Pam:0", holder: PAM, opportunity: "opp:14010:Pam:0", visibleAt: at(17, 30),
  active: ["person:Penny", "place:Home:Trailer", "place:Saloon", "time:evening", "situation:plan-due"],
  bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
  items: [
    { record: "rd:pam-saloon-warm", slot: "top", matched: ["place:Saloon", "time:evening"], score: { overlap: 2, strength: 0.05, significance: 0.5 } },
    { record: "rd:pam-saloon-wary", slot: "top", matched: ["place:Saloon", "time:evening"], score: { overlap: 2, strength: 0.05, significance: 0.5 } },
    { record: "rd:pam-penny-warm", slot: "top", matched: ["person:Penny"], score: { overlap: 1, strength: 0.05, significance: 0.9 } },
  ], label: "Pam recalls the Saloon's two sides and her girl" });
const D3 = "dec:14010:Pam:0";
const pamFollow = decision({
  id: D3, holder: PAM, opportunity: "opp:14010:Pam:0", recall: "rc:14010:Pam:0", visibleAt: at(17, 30),
  question: "plan-follow-through", label: "Stay in for the evening she agreed to, or go to the Saloon?",
  alternatives: [
    { id: altId(D3, "keep"), slug: "keep", label: "stay home for the plan",
      components: { formula: "rule5", base: 1, chance: { whereabouts: 1, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0, boldness: 0.7, intended: 0.2, cost: 0 },
      reasons: [
        { kind: "supports", code: "intended", value: 0.2, via: "plan", cites: ["plan:13512:Penny:Pam"], label: "she said yes this morning" },
        { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:pam-penny-warm", AS.pamPenny], label: "her girl" },
        { kind: "shown", code: "wish", via: "cue", cites: ["rd:pam-wish-hobby"], effect: "none unless Sid's answer to O2's second question gives a wish warmth: it would add 0.05 warm", label: "her wish for a hobby besides the Saloon" },
      ] },
    { id: altId(D3, "saloon"), slug: "saloon", label: "go to the Saloon (her haunt, 15:00-24:00)",
      components: { formula: "rule5", base: 4, warm: 0.05, cold: 0.05, boldness: 0.7, intended: 0, cost: 0 },
      reasons: [
        { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:pam-saloon-warm", AS.pamSaloonWarm], label: "she enjoys it" },
        { kind: "supports", code: "habit-weight", value: 4, via: "routine", cites: ["acct:pam-saloon-evenings:Pam"], provenance: ["src:code-cards"], label: "her authored haunt weight" },
        { kind: "conflicts", code: "cold", value: 0.05, via: "recall", cites: ["rd:pam-saloon-wary", AS.pamSaloonWary], label: "not healthy, she says" },
      ] },
  ],
  excluded: [
    { id: altId(D3, "evening-hub"), slug: "evening-hub", label: "the Evening gathering", why: { code: "not-open", cites: ["src:code-gathering"], label: "not on before 18:00" } },
    { id: altId(D3, "square"), slug: "square", label: "the Square", why: { code: "closed", cites: ["src:code-cards"], label: "her Square haunt ends at 17:00" } },
  ],
  constraints: [{ code: "physical", value: "a 32-minute walk to the Saloon", cites: [] }],
  torn: { value: true, rule: "6", why: "the Saloon's warm and wary sides both above TornAt" },
  choiceIn: { backend: "laya", visibleAt: at(17, 31), laya: [0.38, 0.62], unit: 0.655, tape: "question:1:q:14010:Pam:0" },
});
choiceQuestions.push({ request: { id: `${SEED}:q:14010:Pam:0`, tick: at(17, 30), holder: "Pam", question: "plan-follow-through",
    recalled: [{ id: "rd:pam-saloon-warm", origin: "CanonStated" }, { id: "rd:pam-saloon-wary", origin: "CanonStated" }, { id: "rd:pam-penny-warm", origin: "CanonStated" }],
    ...requestView(pamFollow) },
  answer: layaAnswer(pamFollow), choice: slugOf(pamFollow, pamFollow.choice.selected) });
pamFollow.choice.provenance.requestHash = fnv(choiceQuestions[0].request);
attempts.push({ id: "att:14011:Pam:0", actor: PAM, kind: "go", visibleAt: at(17, 31), decision: D3, alternative: altId(D3, "saloon"),
  intention: "spend the evening at the Saloon", attempted: "walks from the Trailer to the Saloon",
  resolvedAt: at(18, 3), status: "completed", world: { arrived: at(18, 3), place: "Saloon" }, label: "Pam leaves for the Saloon" });

// == 17:48 Penny's follow-through pick (rule 12) ==
opportunities.push({ id: "opp:14028:Penny:0", holder: PENNY, visibleAt: at(17, 48), trigger: "follow-through", rule: "12",
  causes: ["plan:13512:Penny:Pam"], question: "question:1:q:14028:Penny:0", label: "Penny at the Beach, 17:48: head home for the plan?" });
recalls.push({ id: "rc:14028:Penny:0", holder: PENNY, opportunity: "opp:14028:Penny:0", visibleAt: at(17, 48),
  active: ["person:Pam", "place:Home:Trailer", "time:evening", "situation:plan-due"], bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
  items: [
    { record: "rd:penny-pam-care", slot: "top", matched: ["person:Pam"], score: { overlap: 1, strength: 0.05, significance: 0.8 } },
    { record: "rd:penny-expects-pam-away", slot: "top", matched: ["person:Pam", "time:evening", "place:Home:Trailer"], score: { overlap: 3, strength: 0.05, significance: 0.5 } },
    { record: "rd:penny-pam-burden", slot: "top", matched: ["person:Pam", "place:Home:Trailer"], score: { overlap: 2, strength: 0.05, significance: 0.6 } },
  ], label: "Penny recalls her worry, her expectation and the burden" });
const D4 = "dec:14028:Penny:0";
const pennyFollow = decision({
  id: D4, holder: PENNY, opportunity: "opp:14028:Penny:0", recall: "rc:14028:Penny:0", visibleAt: at(17, 48),
  question: "plan-follow-through", label: "Head home now for the evening she offered?",
  alternatives: [
    { id: altId(D4, "keep"), slug: "keep", label: "go home now",
      components: { formula: "rule5", base: 1, chance: { whereabouts: 0.3333, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0.05, boldness: 0.2, intended: 0.2, cost: 0 },
      reasons: [
        { kind: "supports", code: "intended", value: 0.2, via: "plan", cites: ["plan:13512:Penny:Pam"], label: "her own offer" },
        { kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:penny-pam-care"], label: "her worry for Pam" },
        { kind: "conflicts", code: "cold", value: 0.05, via: "recall", cites: ["rd:penny-pam-burden"], label: "the burden" },
        { kind: "conflicts", code: "chance:whereabouts", value: 0.3333, via: "expectation", cites: [EX.pennyWhere], label: "she expects Pam out in the evening" },
        { kind: "supports", code: "chance:answers", value: 1, via: "expectation", cites: [EX.pennyAnswers], label: "Pam said yes" },
      ] },
    { id: altId(D4, "stay"), slug: "stay", label: "stay at the Beach until it closes at 18:00",
      components: { formula: "rule5", base: 1, warm: 0, cold: 0, boldness: 0.2, intended: 0, cost: 0 }, reasons: [{ kind: "supports", code: "carry-on", value: 1, via: "routine", cites: [], label: "her haunt, 12 more minutes" }] },
  ],
  excluded: [], constraints: [{ code: "physical", value: "a 12-minute walk home", cites: [] }],
  torn: { value: true, rule: "6", why: "the plan's companion (Pam) has warm and cold sides above TornAt" },
  choiceIn: { backend: "laya", visibleAt: at(17, 49), laya: [0.81, 0.19], unit: 0.205, tape: "question:1:q:14028:Penny:0" },
});
choiceQuestions.push({ request: { id: `${SEED}:q:14028:Penny:0`, tick: at(17, 48), holder: "Penny", question: "plan-follow-through",
    recalled: [{ id: "rd:penny-pam-care", origin: "CanonStated" }, { id: "rd:penny-expects-pam-away", origin: "CanonStated" }, { id: "rd:penny-pam-burden", origin: "CanonStated" }],
    ...requestView(pennyFollow) },
  answer: layaAnswer(pennyFollow), choice: slugOf(pennyFollow, pennyFollow.choice.selected) });
pennyFollow.choice.provenance.requestHash = fnv(choiceQuestions[1].request);
attempts.push({ id: "att:14029:Penny:0", actor: PENNY, kind: "go", visibleAt: at(17, 49), decision: D4, alternative: altId(D4, "keep"),
  intention: "be home for the evening with Pam", attempted: "walks home from the Beach",
  resolvedAt: at(18, 1), status: "completed", world: { arrived: at(18, 1), place: "Home:Trailer", crossed: "no sighting span with Pam on the way (illustrative)" },
  label: "Penny heads home" });

// == 18:00 the plan's window opens; neither is home yet ==
outcomes.push({ id: "out:14040:Pam:0", about: PAM, visibleAt: at(18, 0), settledAt: at(21, 20), plan: "plan:13512:Penny:Pam",
  attempts: ["att:14011:Pam:0", "att:14029:Penny:0"], participants: [PENNY, PAM],
  timeline: [
    { at: at(18, 0), label: "the window opens: neither is at the Trailer (Pam on her way to the Saloon since 17:31, Penny on her way home since 17:49)" },
    { at: at(18, 1), attempts: ["att:14041:Penny:0"], label: "Penny is home and waits" },
    { at: at(18, 3), label: "Pam reaches the Saloon" },
    { at: at(19, 1), label: "Penny's hour of waiting ends; the game has not begun" },
  ],
  kind: "agreed-absent",
  world: { plannedAct: null, spans: [
    { who: PAM, place: "Home:Trailer", from: at(15, 35), to: at(17, 31) }, { who: PAM, place: "travelling", from: at(17, 31), to: at(18, 3) },
    { who: PAM, place: "Saloon", from: at(18, 3), to: at(21, 20) }, { who: PENNY, place: "Home:Trailer", from: at(18, 1), to: at(21, 20) } ],
    spansFrom: "the base moves stream (illustrative here)" },
  mustNotWrite: ["a refusal, rejection or betrayal", "LetDown, StoodUp or a Promise(Date)", "where Pam went, except as Inferred", "regard (no act perceived)", "a feeling for one who expected nothing"],
  observerOnly: true, settledLabel: "Pam was at the Saloon from 18:03 to the window's end; the game never began", label: "the agreed evening at the Trailer" });
perceptions.push({ id: "per:14040:Pam:0", who: PAM, visibleAt: at(18, 0), of: "plan:13512:Penny:Pam", causes: ["att:14011:Pam:0"],
  perceived: { kind: "not-kept-by-me", note: "she knows she chose the Saloon" }, notPerceived: ["Penny's arrival and wait", "how Penny felt"],
  observerLinks: { worldOutcome: "out:14040:Pam:0" }, label: "Pam: on my way to the Saloon" });
perceptions.push({ id: "per:14040:Gus:0", who: GUS, visibleAt: at(18, 0), of: null, causes: [],
  perceived: { kind: "missed", subject: PAM, place: "Saloon", band: "15-18", source: "seen-absent" }, rule: "14 passive absence",
  notPerceived: ["where Pam was", "the plan with Penny"], observerLinks: { worldOutcome: "out:14040:Pam:0" }, label: "Gus: no Pam this afternoon" });
const gusAfternoon = [1, 2, 3, 4, 7, 8].map((d) => wk(d, "found"));
learning.push({ id: "lu:14040:Gus:0", who: GUS, visibleAt: at(18, 0), record: EX.gusAfternoon, rule: "14",
  previous: state(gusAfternoon), new: { ...state([...gusAfternoon.slice(1), wk(9, "missed", "seen-absent")]), dropped: { day: 1, outcome: "found", source: "Seen" }, last: { day: 9, outcome: "missed", source: "seen-absent" } },
  evidence: ["per:14040:Gus:0"], note: "a lived key: the oldest lived entry leaves the window", label: "Gus: Pam's afternoons, a little less sure" });
attempts.push({ id: "att:14041:Penny:0", actor: PENNY, target: PAM, kind: "begin-planned-act", visibleAt: at(18, 1), decision: D4,
  alternative: altId(D4, "keep"), intention: "begin the game when both are home (rule 13)", attempted: "waits at home for Pam, up to 60 minutes",
  resolvedAt: at(19, 1), status: "unsuccessful", world: { began: false, waitedUntil: at(19, 1) }, outcome: "out:14040:Pam:0", label: "Penny waits for Pam" });

// == 18:05 Gus sees Pam arrive ==
perceptions.push({ id: "per:14045:Gus:0", who: GUS, visibleAt: at(18, 5), of: null, causes: [],
  perceived: { kind: "found", subject: PAM, place: "Saloon", band: "18-21", firstSeen: at(18, 5), source: "Seen", reading: "Pam arrives late" },
  notPerceived: ["where Pam was before", "the plan with Penny"], observerLinks: { worldOutcome: "out:14040:Pam:0" },
  reflection: "not opened: the afternoon miss contradicts lived entries, not a recalled reading (rule 8b)", label: "Gus: Pam arrives late" });
const gusEvening = [{ at: PRE, outcome: "found", source: "Reading", cites: "rd:gus-expects-pam-evenings" }, ...[2, 3, 4, 7, 8].map((d) => wk(d, "found"))];
learning.push({ id: "lu:14045:Gus:0", who: GUS, visibleAt: at(18, 5), record: EX.gusEvening, rule: "9.4 seen",
  previous: state(gusEvening), new: { ...state([...gusEvening, wk(9, "found")]), last: { day: 9, outcome: "found", source: "Seen" } },
  evidence: ["per:14045:Gus:0"], preserved: ["rd:gus-expects-pam-evenings"],
  note: "the seeded entry stays beside the six latest lived ones (rule 9): seven entries, seven found", label: "Gus: Pam's evenings, confirmed by sight" });

// == 18:15 Gus asks Pam to fetch flour (base [#90]); a pending ask, answered 18:16 by the fallback ==
attempts.push({ id: "att:14055:Gus:0", actor: GUS, target: PAM, kind: "errand-ask", visibleAt: at(18, 15), decision: null,
  decidedBy: "base: AskFavours' draw (Simulation.Errands.cs:146 [#90]) x rule 15's factor, 1 with no entries",
  base: ["need:37"], attempted: "asks Pam to fetch flour (the Store opens tomorrow)", resolvedAt: at(18, 16), status: "answered", outcome: "out:14056:Pam:0",
  label: "Gus asks Pam to fetch flour" });
opportunities.push({ id: "opp:14055:Pam:0", holder: PAM, visibleAt: at(18, 15), trigger: "asked", rule: "8a", causes: ["att:14055:Gus:0"],
  reflection: null, reflectionNote: "not opened: 8a's budget (one a person-day) was used at 09:11", label: "Pam is asked a favour" });
recalls.push({ id: "rc:14055:Pam:0", holder: PAM, opportunity: "opp:14055:Pam:0", visibleAt: at(18, 15),
  active: ["person:Gus", "place:Saloon", "good:flour", "time:evening", "situation:asked"], bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
  items: [{ record: "rd:pam-gus-warm", slot: "top", matched: ["person:Gus", "place:Saloon"], score: { overlap: 2, strength: 0.05, significance: 0.4 } }],
  label: "Pam recalls that Gus is a good guy" });
const D5 = "dec:14055:Pam:0";
const pamState1815 = { tired: 0.1, busy: 0, low: 0.02 }; // illustrative
const pamGusBond = bond(false, 0.4, 0.5);
const pamFlour = decision({
  id: D5, holder: PAM, opportunity: "opp:14055:Pam:0", recall: "rc:14055:Pam:0", visibleAt: at(18, 15),
  question: "answer-errand-ask", label: "Will Pam fetch Gus's flour tomorrow?",
  alternatives: answers(D5, {
    obliged: r4(0.5 * 0.4 + 0.3 * 0.5 + 0.2 * 0.3), costs: { base: 0.2, state: stateCost(pamState1815, EAGER.Pam, pamGusBond), errand: 0 },
    reason: { code: "tired" },
    yesReasons: [
      { kind: "supports", code: "obliged", value: r4(0.5 * 0.4 + 0.3 * 0.5 + 0.2 * 0.3), via: "ask", cites: [], provenance: ["src:code-ask-errand"],
        label: "the Ask: regard 0.4 (the Pam-Gus friend seed; 'can wait'), familiarity 0.5 (the friends' seed; day 9's may be higher), understanding 0.3 (her card); no Fond (they meet daily)" },
      { kind: "shown", code: "warm", via: "recall", cites: ["rd:pam-gus-warm"], effect: "recalled; the Ask reads regard, not associations (count once)", label: "Gus is a good guy" },
      { kind: "conflicts", code: "cost", value: 0.2, via: "ask", cites: [], provenance: ["src:code-ask-errand"], label: "the Ask's base cost; no errand running, the household can pay (ErrandCost 0)" },
      { kind: "conflicts", code: "state", value: stateCost(pamState1815, EAGER.Pam, pamGusBond), via: "state", cites: [], provenance: ["src:code-state-cost"], label: "tired 0.10, low 0.02 (illustrative), eagerness 0.5, bond 0.45" },
    ],
    notNowReasons: [{ kind: "supports", code: "tired", value: stateCost(pamState1815, EAGER.Pam, pamGusBond), via: "state", cites: [], label: "tired and a little low: a reason holds (rule 7)" }],
  }),
  excluded: [], constraints: [{ code: "same-set-both-backends", value: "yes, not now, no", cites: [] },
    { code: "counter-hours", value: "the Store is closed on Wednesdays (Pierre's day off); tomorrow 09:00-17:00", cites: [] }],
  torn: { value: false, rule: "6", why: "Gus has only a warm side for Pam; the top two are not within 10%" },
  choiceIn: { backend: "fallback-keyed-draw", visibleAt: at(18, 16), unit: 0.93, base: ["favour:431"] },
});
const flourMargin = pamFlour.alternatives[0].components.margin;
favours.push({ tick: at(18, 16), asker: "Gus", asked: "Pam", item: "flour", accepted: false, margin: flourMargin, actId: 431,
  tired: pamState1815.tired, busy: 0, low: pamState1815.low, bond: r4(pamGusBond), eager: r4(EAGER.Pam), wontFetch: false, short: false, running: false });
outcomes.push({ id: "out:14056:Pam:0", about: PAM, visibleAt: at(18, 16), settledAt: at(18, 16), attempts: ["att:14055:Gus:0"], participants: [GUS, PAM],
  kind: "declined", base: ["favour:431", "act:431"], world: { answer: "no", act: "act:431", weighed: true }, observerOnly: true,
  mustNotWrite: ["an association for the asker", "a fixed 7-day wait"],
  settledLabel: "Pam says no, a weighed no (about 1 in 10 at this margin); the hurt is the Refused act's own", label: "Gus's flour, asked of Pam" });
perceptions.push({ id: "per:14056:Gus:0", who: GUS, visibleAt: at(18, 16), of: "att:14055:Gus:0", causes: ["act:431"],
  perceived: { kind: "refused", answer: "no", act: "act:431" }, notPerceived: ["Pam's reasons"], observerLinks: { worldOutcome: "out:14056:Pam:0" }, label: "Gus: Pam said no" });
learning.push({ id: "lu:14056:Gus:0", who: GUS, visibleAt: at(18, 16), record: EX.gusFlour, rule: "9.4 refused by weighing",
  previous: state([]), new: { ...state([{ outcome: "no", source: "Lived" }]), last: { outcome: "no", source: "Lived", act: "act:431" } },
  evidence: ["per:14056:Gus:0"], label: "Gus: one 'no' from Pam for flour" });

// == 18:20 an encounter: ordinary warmth (rule 11); Pam offers Gus a game; he answers 18:21 ==
opportunities.push({ id: "opp:14060:Pam:0", holder: PAM, visibleAt: at(18, 20), trigger: "encounter", rule: "11",
  causes: ["rd:pam-gus-warm"], reflection: null, note: "ordinary warmth, not a crisis (direction 4.5); a controlled fixture verifies the path, natural runs measure its rate",
  label: "Gus in reach: Pam thinks of a game" });
recalls.push({ id: "rc:14060:Pam:0", holder: PAM, opportunity: "opp:14060:Pam:0", visibleAt: at(18, 20),
  active: ["person:Gus", "place:Saloon", "time:evening", "situation:encounter"], bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
  items: [
    { record: "rd:pam-gus-warm", slot: "top", matched: ["person:Gus", "place:Saloon"], score: { overlap: 2, strength: 0.05, significance: 0.4 } },
    { record: "rd:pam-wish-hobby", slot: "top", matched: ["place:Saloon"], cueOrigin: "sourced (Pam:Fri2, weight 2)", score: { overlap: 2, strength: 0, significance: 0.3 } },
  ], label: "Pam recalls Gus's kindness and her wish for a hobby" });
const D6 = "dec:14060:Pam:0";
const pamOffer = decision({
  id: D6, holder: PAM, opportunity: "opp:14060:Pam:0", recall: "rc:14060:Pam:0", visibleAt: at(18, 20),
  question: "encounter-offer", label: "Ask Gus for a game?",
  alternatives: [
    { id: altId(D6, "offer"), slug: "offer", label: "ask Gus to play a game now, at the Saloon",
      template: { kind: "plan-offer", asked: GUS, place: "Saloon", activity: "PlayedGame", from: at(18, 21), until: at(18, 51) },
      components: { formula: "rule5", base: 0.1, chance: { whereabouts: 1, answers: 1, followThrough: 1 }, warm: 0.05, cold: 0, boldness: 0.7, intended: 0, cost: 0 },
      reasons: [{ kind: "supports", code: "warm", value: 0.05, via: "recall", cites: ["rd:pam-gus-warm", AS.pamGus], label: "Gus is a good guy" },
        { kind: "supports", code: "plan-base", value: 0.1, via: "rule", cites: [], label: "an encounter offer starts small (PlanBase)" },
        { kind: "supports", code: "chance:availability", value: 1, via: "expectation", cites: [], label: "free by her knowledge: no availability entries for Gus yet (rule 11)" },
        { kind: "shown", code: "wish", via: "cue", cites: ["rd:pam-wish-hobby"], effect: "none unless Sid's answer to O2's second question gives a wish warmth", label: "her wish for a hobby" }] },
    { id: altId(D6, "carry-on"), slug: "carry-on", label: "carry on at the Saloon",
      components: { formula: "rule5", base: 1, warm: 0.05, cold: 0.05, boldness: 0.7, intended: 0, cost: 0 },
      reasons: [{ kind: "supports", code: "warm", value: 0.05, via: "option-side", cites: [AS.pamSaloonWarm], label: "she enjoys it" },
        { kind: "conflicts", code: "cold", value: 0.05, via: "option-side", cites: [AS.pamSaloonWary], label: "not healthy" }] },
  ],
  excluded: [], constraints: [{ code: "eligible-activity", value: "a game at the Saloon after 17:00; ages within 6 (both 50)", cites: [] },
    { code: "one-offer-a-pair-a-day", value: "none yet today between them", cites: [] }],
  torn: { value: true, rule: "6", why: "carry-on's object (the Saloon) has warm and wary sides above TornAt" },
  choiceIn: { backend: "fallback-keyed-draw", visibleAt: at(18, 20), unit: 0.061,
    cap: { reached: true, limit: 2, used: [D2, D3], note: "torn, but Pam's two model questions today are used: the fallback answers on the same alternatives" } },
});
attempts.push({ id: "att:14060:Pam:0", actor: PAM, target: GUS, kind: "plan-offer", visibleAt: at(18, 20), decision: D6, alternative: altId(D6, "offer"),
  intention: "a game with Gus", attempted: "asks Gus at the counter (a pending ask, rule 7)", resolvedAt: at(18, 21), status: "answered", outcome: "out:14061:Gus:0",
  label: "Pam asks Gus for a game" });
opportunities.push({ id: "opp:14060:Gus:0", holder: GUS, visibleAt: at(18, 20), trigger: "asked", rule: "8a", causes: ["att:14060:Pam:0"],
  reflection: "reflection:1:opp:14060:Gus:0", budget: "8a, one a person-day (his first today)",
  label: "Gus is asked: he recalls pre-run records about Pam, so his reflection opens" });
recalls.push({ id: "rc:14060:Gus:0", holder: GUS, opportunity: "opp:14060:Gus:0", visibleAt: at(18, 20),
  active: ["person:Pam", "place:Saloon", "activity:PlayedGame", "time:evening", "situation:asked"], bound: { top: 3, reservePreRun: 1, lowMood: false, max: 4 },
  items: [
    { record: "rd:gus-expects-pam-evenings", slot: "top", matched: ["person:Pam", "place:Saloon"], score: { overlap: 2, strength: 0.05, significance: 0.5 } },
    { record: "rd:gus-pam-wary", slot: "top", matched: ["person:Pam"], score: { overlap: 1, strength: 0.05, significance: 0.6 } },
    { record: EX.gusEvening, slot: "top", matched: ["person:Pam"], score: { overlap: 1, strength: 1, significance: 0.3 } },
  ], label: "Gus recalls Pam's evenings and his unease about her drinking" });
const D7 = "dec:14060:Gus:0";
const gusState = { tired: 0, busy: 1, low: 0 }; // busy 1: being mid-shift read as a duty in hand (contingent, E)
const gusPamBond = bond(false, 0.4, 0.5);
const gusAnswer = decision({
  id: D7, holder: GUS, opportunity: "opp:14060:Gus:0", recall: "rc:14060:Gus:0", visibleAt: at(18, 20),
  question: "answer-plan-offer", label: "Does Gus play a game with Pam now?",
  alternatives: answers(D7, {
    obliged: r4(0.5 * 0.4 + 0.3 * 0.5 + 0.2 * 0.8), costs: { base: 0.15, state: stateCost(gusState, EAGER.Gus, gusPamBond), displacedHabit: 0 },
    reason: { code: "working" },
    yesReasons: [
      { kind: "supports", code: "obliged", value: r4(0.5 * 0.4 + 0.3 * 0.5 + 0.2 * 0.8), via: "ask", cites: [], provenance: ["src:code-ask-invitation"],
        label: "the Ask: regard 0.4 (the friend seed; 'can wait'), familiarity 0.5 (the friends' seed), chattiness 0.8; no Fond, no kin bond" },
      { kind: "conflicts", code: "cost", value: 0.15, via: "ask", cites: [], provenance: ["src:code-ask-invitation"], label: "the Ask's base cost; the game displaces no place of his (he is at the Saloon)" },
      { kind: "conflicts", code: "state", value: stateCost(gusState, EAGER.Gus, gusPamBond), via: "state", cites: [], provenance: ["src:code-state-cost"],
        label: "mid-shift, read as a duty in hand (busy 1; eagerness 0.45, bond 0.45). Contingent: today busy means work due within 60 minutes, and whether work weighs a non-errand ask is rule 16's open point" },
      { kind: "shown", code: "wary", via: "recall", cites: ["rd:gus-pam-wary"], effect: "reflection context only: the Ask reads regard, not associations (count once)", label: "she drinks too much, he thinks" },
    ],
    notNowReasons: [{ kind: "supports", code: "working", value: stateCost(gusState, EAGER.Gus, gusPamBond), via: "state", cites: [],
      label: "at the counter mid-shift: a reason holds (rule 7), contingent on rule 16's open point" }],
  }),
  excluded: [], constraints: [{ code: "same-set-both-backends", value: "yes, not now, no", cites: [] }],
  torn: { value: false, rule: "6", why: "Gus has only a wary side toward Pam (g3); the top two (not now, yes) are not within 10%" },
  choiceIn: { backend: "fallback-keyed-draw", visibleAt: at(18, 21), unit: 0.533, tape: "reflection:1:opp:14060:Gus:0",
    thought: { backend: "authored", imagined: true, note: "not torn: the reflection gives only the thought; the keyed draw answers" } },
});
reflections.push({ request: {
    id: `${SEED}:opp:14060:Gus:0`, tick: at(18, 20), actor: "Gus", subject: "Pam", sourceActId: -1,
    memory: "At 18:20 Pam asked you to play a game with her, now, at the Saloon.",
    context: "Gus, age 50: the Saloon, evening, at the counter. Choose for this person, not for a well-behaved town.",
    choices: [{ id: "yes", kind: "PlanAnswer", line: "Sure, one game. Set up the board." }, { id: "not-now", kind: "PlanAnswer", line: "Not while I'm behind the counter, Pam. Later?" }, { id: "no", kind: "PlanAnswer", line: "Not tonight." }],
    opportunity: { kind: "asked", sleptAt: -1, wokeAt: -1 },
    recalled: [{ id: "rd:gus-expects-pam-evenings", origin: "CanonStated" }, { id: "rd:gus-pam-wary", origin: "CanonStated" }, { id: EX.gusEvening, origin: "Seeded" }],
    ...requestView(gusAnswer) },
  answer: { thought: "Busy night. She's here every evening; a game will keep.", suggestedChoice: "not-now", weights: bySlug(gusAnswer, gusAnswer.choice.fallback.probabilities),
    backend: "authored", note: "illustrative: authored thought; not torn, so no model is asked and the weights are the fallback's" },
  choice: slugOf(gusAnswer, gusAnswer.choice.selected),
  events: [{ tick: at(18, 21), status: "considered", text: "Busy night. She's here every evening; a game will keep.", actId: -1 }] });
gusAnswer.choice.provenance.requestHash = fnv(reflections[2].request);
outcomes.push({ id: "out:14061:Gus:0", about: GUS, visibleAt: at(18, 21), settledAt: at(18, 21), attempts: ["att:14060:Pam:0"], participants: [PAM, GUS],
  kind: "not-now", determinedBy: { rule: "7", sources: ["src:assessment-9"],
    note: "the fallback's keyed draw over the same three answers Laya would have had; the 'working' reason assumes rule 16's open point resolves as 'work weighs an ask' (contingent; if work stops asks, this would be busy)" },
  world: { answer: "not-now", act: null, baseRow: "none: not now has no base row (contract 16, point 1)" },
  mustNotWrite: ["the Refused act", "an invitations row", "a 7-day wait", "regard"], observerOnly: true,
  settledLabel: "Gus: not now, he is working", label: "Pam's offer of a game" });
perceptions.push({ id: "per:14061:Pam:0", who: PAM, visibleAt: at(18, 21), of: "att:14060:Pam:0", causes: [],
  perceived: { kind: "not-now", reason: "at work" }, notPerceived: ["what Gus thinks of her drinking"], observerLinks: { worldOutcome: "out:14061:Gus:0" },
  label: "Pam: Gus can't now, he's working" });
learning.push({ id: "lu:14061:Pam:0", who: PAM, visibleAt: at(18, 21), record: EX.pamGusAvail, rule: "9.4 not now",
  previous: state([]), new: { ...state([wk(9, "not-now", "Lived")]), last: { day: 9, outcome: "not-now", source: "Lived", reason: "at work" } },
  evidence: ["per:14061:Pam:0"], note: "availability only: no 'no' entry, no hurt", label: "Pam: Gus is not free at this hour" });

// == 19:01 Penny's wait ends: agreed, absent ==
perceptions.push({ id: "per:14101:Penny:0", who: PENNY, visibleAt: at(19, 1), of: "plan:13512:Penny:Pam", causes: ["att:14041:Penny:0"],
  perceived: { kind: "agreed-absent", observed: "Pam not home from 18:01 to 19:01", source: "Sought" },
  unknown: [{ about: "where Pam went", status: "unknown" }, { about: "why", status: "unknown" }],
  inferences: [{ about: "where Pam went", guess: "the Saloon", certainty: "guess", basis: ["rd:penny-saloon-wary", EX.pennyWhere], establishes: false,
    note: "an inference, not a sighting: it writes no 'found at the Saloon' entry and is never told as fact" }],
  notAsserted: ["rejection", "betrayal", "where Pam went"], notPerceived: ["that Pam was home until 17:31"],
  observerLinks: { worldOutcome: "out:14040:Pam:0" }, label: "Penny: she said yes, and she isn't here" });
const pennyWhere = [{ at: PRE, outcome: "away", source: "Reading", cites: "rd:penny-expects-pam-away" }, wk(0, "missed", "seen-absent")];
// Disappointment (9.4): D x expected (the plan's chance as she held it at the yes) x invested ((intended + her
// warmth toward Pam) / scale). Her terms at 09:12: whereabouts in the window as her key stood, answers after
// Pam's yes, follow-through with no entries yet (the key is created at its first outcome).
const expectedTerms = [
  { term: "whereabouts", record: EX.pennyWhere, chance: state(pennyWhere).chance },
  { term: "answers", record: EX.pennyAnswers, chance: state([{ outcome: "yes", source: "Lived" }]).chance },
  { term: "followThrough", record: EX.pennyFollow, chance: state([]).chance },
];
const expected = r4(expectedTerms.reduce((p, x) => p * x.chance, 1));
const warmth = { record: AS.pennyCare, strength: 0.05 };
const invested = r4((settings.intended + warmth.strength) / settings.disappointmentScale);
const disappointment = r4(settings.disappointment * expected * invested);
const EVENING = ["person:Pam", "activity:PlayedGame", "place:Home:Trailer", "time:evening"];
records.push(
  { ...assoc(PENNY, AS.pennyDisEvening, "situation:evening-at-home-with-Pam", "-", "disappointed", disappointment, ["plan:13512:Penny:Pam", "per:14101:Penny:0"], at(19, 1), "Lived"),
    situation: EVENING, cues: EVENING.map((key) => ({ key, origin: "object" })),
    note: "one disappointment, aimed at the hoped-for evening, a situation (9.2, 9.4; O3 as the assessment leans), one cause (the plan); counted once (9.5): no 'no' entry, no whereabouts entry, no regard change, no rejection. Read only by her own keep pick at a later plan for such an evening (rule 12), and recalled; never by an offer to Pam" },
);
const disBasis = { formula: "9.4: D x expected x invested (first guesses)", D: settings.disappointment,
  expectedAt: at(9, 12), expectedTerms, expected, intended: settings.intended, warmth, investedScale: settings.disappointmentScale, invested,
  knowledgeDependence: "every term is her own: the plan's chance from her keys at the yes, her intention and her warmth toward Pam; one who expected nothing (chance 0) or set nothing aside feels nothing" };
learning.push(
  { id: "lu:14101:Penny:0", who: PENNY, visibleAt: at(19, 1), record: EX.pennyFollow, rule: "9.4 agreed, absent",
    previous: state([]), new: { ...state([{ outcome: "absent", source: "Sought" }]), last: { outcome: "absent", source: "Sought" } },
    evidence: ["per:14101:Penny:0"], note: "learning starts at the first outcome (direction 4.2)", label: "Penny: one agreed evening, not kept" },
  { id: "lu:14101:Penny:1", who: PENNY, visibleAt: at(19, 1), record: AS.pennyDisEvening, rule: "9.4 agreed, absent: disappointment (proposed)",
    previous: null, new: { strength: disappointment, name: "disappointed", moodChange: -disappointment }, basis: disBasis,
    evidence: ["per:14101:Penny:0"], notAsserted: ["rejection", "betrayal"], interpretation: { formed: false },
    note: "mood changes once, here; no regard change (no act perceived); no whereabouts entry: the plan, not habit, set where Pam was to be (9.5)",
    label: "Penny: disappointed about the evening" },
);

// ---- comparisons: the morning choice with and without one history ----
function armReasons(remove, chanceW) {
  return Object.fromEntries(pennyMorning.alternatives.map((a) => [a.slug, a.reasons
    .filter((r) => !(r.cites || []).some((c) => remove.includes(c)) || r.code === "chance:whereabouts")
    .map((r) => ({ kind: r.kind, code: r.code, value: r.code === "chance:whereabouts" ? chanceW : (r.value ?? null),
      cites: (r.cites || []).filter((c) => !remove.includes(c)) }))]));
}
function arm(id, label, history, chanceW, warm, recall, torn, remove) {
  const offer = rule5({ base: 0.6, chance: { whereabouts: chanceW, answers: 1, followThrough: 1 }, warm, cold: 0.05, boldness: 0.2, intended: 0, cost: 0 });
  const keep = rule5({ base: 1, warm: 0.05, cold: 0, boldness: 0.2, intended: 0, cost: 0 });
  const ids = pennyMorning.alternatives.map((a) => a.id);
  const probs = normalise({ [ids[0]]: offer, [ids[1]]: keep });
  return { id, label,
    inputs: {
      history,
      worldState: { minute: at(9, 10), place: "Home:Trailer", present: [PAM, PENNY], ask: "favour:412", askMargin: 0.6, window: [at(18, 0), at(21, 20)] },
      availableActions: { eligible: ids, excluded: pennyMorning.excluded.map((x) => x.id) },
      otherPersonalState: { traits: { boldness: 0.2 }, mood: "even", lived: [{ record: EX.pennyWhere, entry: { day: 0, outcome: "missed", source: "seen-absent" } }],
        commitments: ["cmt:penny-no-alcohol"] },
      randomInput: { stream: "lives-choice", parts: [D1], unit: 0.163 },
      method: { backend: "fallback-keyed-draw", constants: { k: K, seedStrength: 0.05, tornAt: 0.04 } },
    },
    derived: {
      opportunity: { id: "opp:13510:Penny:0", trigger: "refused-by-commitment", why: "rule 10 opens on the refusal and the margin, whatever is recalled" },
      decisionExists: true,
      recall, torn,
      chance: { whereabouts: chanceW, answers: 1, followThrough: 1 },
      sides: { warm, cold: 0.05 },
      reasons: armReasons(remove, chanceW),
      weights: { [ids[0]]: r4(offer), [ids[1]]: r4(keep) },
      probabilities: probs,
      sampled: pick(ids, probs, 0.163),
    } };
}
const pennyHistory = records.filter((r) => ["fact", "account", "reading", "commitment"].includes(r.type) && (r.holder === PENNY || (r.knownBy || []).some((k) => k.who === PENNY && k.availableAt === PRE)) && r.visibleAt === PRE).map((r) => r.id);
const withArm = arm("arm:with", "with every history (the decision's inputs, fallback method)", pennyHistory, 0.3333, 0.05,
  ["rd:penny-pam-burden", "rd:penny-saloon-wary", "rd:penny-pam-care"], true, []);
const rmP6 = ["rd:penny-pam-care", AS.pennyCare];
const withoutP6 = arm("arm:without-p6", "without p6 (Penny's care for Pam)", pennyHistory.filter((x) => x !== "rd:penny-pam-care"), 0.3333, 0,
  ["rd:penny-pam-burden", "rd:penny-saloon-wary", "rd:penny-reading-home"], false, rmP6);
const rmP4 = ["rd:penny-saloon-wary", "rd:penny-expects-pam-away", AS.pennySaloon];
const withoutP4 = arm("arm:without-p4", "without p4 (Penny's reading of the Saloon and of Pam's evenings)",
  pennyHistory.filter((x) => !["rd:penny-saloon-wary", "rd:penny-expects-pam-away"].includes(x)), 0.5, 0.05,
  ["rd:penny-pam-burden", "rd:penny-pam-care", "rd:penny-reading-home"], true, rmP4);
const held = ["worldState", "availableActions", "otherPersonalState", "randomInput", "method"];
const fmt = (p) => p.toFixed(3);
const comparisons = [
  { id: "cmp:13510:Penny:0:without-p6", scope: "isolated-choice", decision: D1, visibleAt: at(9, 11),
    question: "Does Penny's care for Pam (p6) change what she offers?",
    intervention: { field: "history", remove: ["rd:penny-pam-care"], seedsRemoved: [AS.pennyCare], evidenceKeys: ["p6"] },
    validity: "valid: the opportunity (rule 10) and the decision exist in both arms",
    heldConstant: held, arms: [withArm, withoutP6],
    unavoidableDifferences: [
      { field: "recall", why: "the third slot goes to the next candidate (rd:penny-reading-home)" },
      { field: "torn", why: "with no warm side toward Pam the choice is no longer torn; a Laya-enabled run would route it to the fallback, so the method is held at the fallback in both arms" },
      { field: "sides", why: "the warm side toward Pam is the record removed" },
    ],
    effects: { reasons: "the offer loses its 'warm' reason", probabilities: `offer ${fmt(withArm.derived.probabilities[altId(D1, "offer")])} -> ${fmt(withoutP6.derived.probabilities[altId(D1, "offer")])}`, sampledAction: "keep-evening in both arms" },
    differs: { reasons: true, probabilities: true, sampledAction: false },
    runChoice: "the run's own choice used Laya (offer, 0.46); this comparison isolates the history under one method, the fallback",
    wholeRun: { simulated: false, note: "not a whole run: neither arm is run forward, so later divergence is not shown" } },
  { id: "cmp:13510:Penny:0:without-p4", scope: "isolated-choice", decision: D1, visibleAt: at(9, 11),
    question: "Does Penny's expectation that Pam is out in the evening (p4) change what she offers?",
    intervention: { field: "history", remove: ["rd:penny-saloon-wary", "rd:penny-expects-pam-away"],
      seedsRemoved: [AS.pennySaloon, `the 'away' entry (source Reading) in ${EX.pennyWhere}`], evidenceKeys: ["p4"], kept: ["acct:pam-saloon-evenings:Penny (also sourced to p5)"] },
    validity: "valid: the opportunity (rule 10) and the decision exist in both arms",
    heldConstant: held, arms: [withArm, withoutP4],
    unavoidableDifferences: [
      { field: "recall", why: "rd:penny-saloon-wary is gone; the third slot goes to rd:penny-reading-home" },
      { field: "chance", why: "the expectation keeps day 0's lived miss but loses the seeded 'away': 1/3 -> 1/2" },
    ],
    effects: { reasons: "the offer's whereabouts reason moves 0.333 -> 0.5 and loses its p4 cite; the 'shown' Saloon reason goes",
      probabilities: `offer ${fmt(withArm.derived.probabilities[altId(D1, "offer")])} -> ${fmt(withoutP4.derived.probabilities[altId(D1, "offer")])}`, sampledAction: "keep-evening -> offer" },
    differs: { reasons: true, probabilities: true, sampledAction: true },
    runChoice: "the run's own choice used Laya (offer, 0.46); this comparison isolates the history under one method, the fallback",
    wholeRun: { simulated: false, note: "reasoned, not run: in a whole run without p4, rule 14 would have written misses on days 0 and 1 (her chance stays at or above 0.5 until then), reaching 1/3 by day 9, so this isolated difference could vanish; that is why isolated and whole-run comparisons are kept apart" } },
];

const threads = [
  { id: "thr:penny-pam-evening", title: "An evening at home, offered and agreed", people: [PENNY, PAM, GUS], visibleAt: at(9, 10),
    stages: [
      { stage: "history", ids: ["rd:penny-pam-burden", "rd:penny-saloon-wary", "rd:penny-pam-care", "rd:penny-expects-pam-away", "cmt:penny-no-alcohol", "rd:pam-penny-warm", "rd:pam-saloon-warm", "rd:pam-saloon-wary", "rd:gus-expects-pam-evenings"] },
      { stage: "recall", ids: ["rc:13510:Penny:0", "rc:13511:Pam:0", "rc:14010:Pam:0", "rc:14028:Penny:0"] },
      { stage: "alternatives", ids: [D1, D2, D3, D4] },
      { stage: "attempt", ids: ["att:13510:Pam:0", "att:13511:Penny:0", "att:14011:Pam:0", "att:14029:Penny:0", "att:14041:Penny:0"] },
      { stage: "outcome", ids: ["out:13510:Penny:0", "out:13512:Pam:0", "out:14040:Pam:0"] },
      { stage: "perceived", ids: ["per:13510:Pam:0", "per:13512:Penny:0", "per:14040:Pam:0", "per:14040:Gus:0", "per:14045:Gus:0", "per:14101:Penny:0"] },
      { stage: "learning", ids: ["lu:13510:Pam:0", "lu:13512:Penny:0", "lu:14040:Gus:0", "lu:14045:Gus:0", "lu:14101:Penny:0", "lu:14101:Penny:1"] },
      { stage: "comparison", ids: ["cmp:13510:Penny:0:without-p6", "cmp:13510:Penny:0:without-p4"] },
    ],
    summary: [
      { visibleAt: at(9, 10), label: "Pam asks for beer; Penny's commitment says no" },
      { visibleAt: at(9, 12), label: "Penny offers an evening at home; Pam says yes" },
      { visibleAt: at(18, 0), label: "The evening starts; neither is home yet" },
      { visibleAt: at(19, 1), label: "Penny has waited an hour; she doesn't know where Pam is" },
      { visibleAt: at(21, 20), label: "The window closes: agreed, absent" },
    ] },
  { id: "thr:gus-flour", title: "Gus's flour: busy at her lessons, then a weighed no", people: [GUS, PENNY, PAM], visibleAt: at(10, 20),
    stages: [
      { stage: "attempt", ids: ["att:13580:Gus:0", "att:14055:Gus:0"] },
      { stage: "recall", ids: ["rc:14055:Pam:0"] },
      { stage: "alternatives", ids: [D5] },
      { stage: "outcome", ids: ["out:13580:Penny:0", "out:14056:Pam:0"] },
      { stage: "perceived", ids: ["per:13580:Gus:0", "per:14056:Gus:0"] },
      { stage: "learning", ids: ["lu:13580:Gus:0", "lu:14056:Gus:0"] },
    ],
    summary: [
      { visibleAt: at(10, 20), label: "Gus would ask Penny for flour; she is teaching: busy" },
      { visibleAt: at(18, 16), label: "Pam says no to fetching it" },
    ] },
  { id: "thr:pam-gus-game", title: "At the Saloon: a game offered while Gus is working", people: [PAM, GUS], visibleAt: at(18, 20),
    stages: [
      { stage: "recall", ids: ["rc:14060:Pam:0", "rc:14060:Gus:0"] },
      { stage: "alternatives", ids: [D6, D7] },
      { stage: "attempt", ids: ["att:14060:Pam:0"] },
      { stage: "outcome", ids: ["out:14061:Gus:0"] },
      { stage: "perceived", ids: ["per:14061:Pam:0"] },
      { stage: "learning", ids: ["lu:14061:Pam:0"] },
    ],
    summary: [
      { visibleAt: at(18, 20), label: "Pam, warm toward Gus, offers a game" },
      { visibleAt: at(18, 21), label: "Gus: not now, he's working" },
    ] },
];

const unresolved = [
  { ref: "need:36", asOf: TO, status: "open at excerpt end", label: "Pam's beer need: Penny refused; what happens next is outside the excerpt" },
  { ref: "need:37", asOf: TO, status: "open at excerpt end", label: "Gus's flour need: Penny busy, Pam declined" },
  { ref: "plan:13512:Penny:Pam", asOf: TO, status: "settled: agreed, absent", label: "nobody told anyone where Pam was; whereabouts are never told" },
];

const fixture = {
  format: "under-glass-run", version: 1, illustrative: true,
  note: "ILLUSTRATIVE fixture for the proposed Lives observer recording contract v0.1 (2026-10-14, revised after its check). It is not output from an implemented system: no Lives code exists. Base fields are a tiny excerpt shaped like a real recording (needs and favours are [#90]); indices are this excerpt's. Laya probabilities, sampled units, prompts and thoughts are invented; fallback weights follow the proposed rules 5 and 7 from the assessment's first guesses.",
  seed: SEED, days: 28, label: "illustrative: Lives v0.1, Penny, Pam and Gus, day 9", hash: "illustrative",
  clock: { minutesPerDay: 1440, tick: 5, daysPerWeek: 7, daysPerSeason: 28 },
  names, places: places.map((name) => ({ name })), kinds: kinds.map((name) => ({ name })),
  acts, needs, favours, invitations, reflections, choiceQuestions,
  lives: {
    contract: "under-glass-lives-observer", version: "0.1", status: "proposed", illustrative: true,
    note: "Illustrative: hand-built to the proposed contract; not output from an implemented system.",
    runId: "run:1:28:illustrative", rules: "lives-slice-proposed-2026-10-14",
    assumes: [
      "Lives on (proposed switch); Company, Invitations, errands [#90] and reflection on",
      "Decided (A, 2026-10-14): Penny's evening place corrected as her character data; an agreed evening is informal and may disappoint (direction 4.3)",
      "O1(a): the sim's bedtime, about 21:20 (exact bedtime fidelity stays Sid's separate choice)",
      "O2 as the assessment leans: present readings with a stance seed one entry each; Pam's hobby wish opens offers but gives no warmth (her keep pick would otherwise gain 0.05 warm)",
      "O3 as the assessment leans: an agreed evening's disappointment is mood and one cold entry toward the hoped-for evening, a situation, never toward Pam",
      "Can wait, kept as default: the Pam-Gus friend seed 0.4; rule 16 limited to errand asks; this fixture also reads Gus's being mid-shift as a not-now reason for a game (contingent on rule 16's open point)",
      "Laya enabled, two model questions a person-day; authored reflection thoughts",
    ],
    excerpt: { day: 9, season: "spring", weekday: "Wed", from: FROM, to: TO,
      omitted: ["Penny's evening reflection (rule 8c, a plan broken): not part of this excerpt", "picks where nothing was recalled (today's draw): they write no Lives record",
        "each person's own answers and refusals as perceptions: self-knowledge is the act itself",
        "per-holder plan copies: 9.2's Plan is written for each, with an outcome; this excerpt keeps one shared plan and shows each holder's outcome through their own perception (per:14040:Pam:0, per:14101:Penny:0), so the plan never carries Pam's private follow-through"] },
    settings, clock: { unit: "game minutes from midnight of day 0", preRun: PRE, dayStart: D9 },
    models: [model],
    external: { archetypes: { source: "sim/character-inspiration.json characters[].archetypeId", ids: ["convivial-keeper", "steadfast-carer", "weary-defender"] } },
    characters: [
      { who: GUS, name: "Gus", archetype: "convivial-keeper" },
      { who: PAM, name: "Pam", archetype: "weary-defender" },
      { who: PENNY, name: "Penny", archetype: "steadfast-carer" },
    ],
    sources, records, opportunities, recalls, decisions, attempts, plans, outcomes, perceptions, learning, comparisons, threads, unresolved,
  },
};
for (const c of fixture.lives.characters)
  c.history = records.filter((r) => r.visibleAt === PRE && ["fact", "account", "reading", "commitment"].includes(r.type)
    && (r.holder === c.who || (r.type === "fact" && r.knownBy.some((k) => k.who === c.who)))).map((r) => r.id);

// Compact pretty-printer: short arrays and objects stay on one line.
function pretty(v, ind = "") {
  const W = 120;
  const one = JSON.stringify(v);
  if (ind.length + one.length <= W || v === null || typeof v !== "object") return one;
  const next = ind + "  ";
  const parts = Array.isArray(v) ? v.map((x) => pretty(x, next)) : Object.entries(v).map(([k, x]) => JSON.stringify(k) + ": " + pretty(x, next));
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
fs.writeFileSync(out, pretty(fixture) + "\n");

// Size notes for the contract (compact JSON, labels and notes stripped).
function strip(v) {
  if (Array.isArray(v)) return v.map(strip);
  if (v && typeof v === "object") return Object.fromEntries(Object.entries(v).filter(([k]) => !/^(label|settledLabel|note|.*Note|why|title|formalNote|knowledgeDependence|shareBasis|formula|source|unitSource)$/.test(k)).map(([k, x]) => [k, strip(x)]));
  return v;
}
const L = fixture.lives;
const bytes = (arr) => arr.length ? Math.round(arr.reduce((s, x) => s + JSON.stringify(strip(x)).length, 0) / arr.length) : 0;
console.log(JSON.stringify({ file: out, bytes: fs.statSync(out).size,
  avgCompact: { decision: bytes(L.decisions), recall: bytes(L.recalls), opportunity: bytes(L.opportunities), attempt: bytes(L.attempts),
    outcome: bytes(L.outcomes), perception: bytes(L.perceptions), learning: bytes(L.learning), record: bytes(L.records.filter((r) => r.visibleAt === PRE)), comparison: bytes(L.comparisons) },
  probabilities: Object.fromEntries(L.decisions.map((d) => [d.id, { backend: d.choice.backend, selected: d.choice.selected, used: d.choice.probabilities, fallback: d.choice.fallback.probabilities }])),
  margins: Object.fromEntries(L.decisions.filter((d) => d.alternatives[0].components.formula === "rule7").map((d) => [d.id, d.alternatives.map((a) => a.components)])),
  arms: comparisons.map((c) => c.arms.map((a) => [a.id, a.derived.probabilities, a.derived.sampled])) }, null, 1));
