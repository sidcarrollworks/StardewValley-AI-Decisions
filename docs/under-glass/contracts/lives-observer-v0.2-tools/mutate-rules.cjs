// Fault suite for check-lives-observer-rules.cjs (the experimental pilot rules). Each case plants one fault that a
// reader cannot see (the recording stays structurally valid) and must make the rules checker fail with the named
// check. The suite reports, for each case, whether the structure checker still passes: the two tiers are separate.
// It also runs the rules checker on the legitimate run shapes of variants.cjs: each must pass rule 6 (a torn choice the
// fallback answers names a reason that holds), and those whose story is unchanged must pass every rule.
// Usage: node mutate-rules.cjs [fixture.json]   (default: ../lives-observer-example-v0.2.json)
"use strict";
const fs = require("fs"), os = require("os"), path = require("path"), { execFileSync } = require("child_process");
const { realLike } = require("./make-real-like.cjs");
const { legitShapes } = require("./variants.cjs");
const dir = path.join(__dirname, "..");
const src = process.argv[2] || path.join(dir, "lives-observer-example-v0.2.json");
const base = JSON.parse(fs.readFileSync(src, "utf8"));
const rules = path.join(dir, "check-lives-observer-rules.cjs"), structure = path.join(dir, "check-lives-observer-structure.cjs");
const tmp = path.join(os.tmpdir(), `lives-rules-mutation-${process.pid}.json`);
const dec = (f, id) => f.lives.decisions.find((d) => d.id === id);
const by = (f, tbl, id) => f.lives[tbl].find((x) => x.id === id);
const rec = (f, id) => f.lives.records.find((r) => r.id === id);
const row = (f, id) => [...f.favours, ...f.invitations].find((r) => r.id === id);
const maxSeq = (f) => Math.max(...["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons", "threads"].flatMap((t) => f.lives[t].map((x) => x.seq)));
const D2 = "dec:45191:Pam:0", D3 = "dec:45688:Pam:0", D4 = "dec:45708:Penny:0", D6 = "dec:45740:Pam:0", D7 = "dec:45740:Gus:0";
const EXW = "ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday", EXG = "ex:Gus:Pam:whereabouts:Saloon:18-21:weekday";
const DIS = "as:Penny:situation:evening-at-home-with-Pam:-:disappointed";

const cases = [
  ["rule5", "a rule-5 weight that does not follow its components", (f) => { dec(f, D3).choice.observer.fallback.weights["alt:45688:Pam:0/keep"] = 2; }],
  ["rule7", "Pam's obliged set to 0.1 (rule 7 recomputed)", (f) => { dec(f, D2).alternatives[0].components.obliged = 0.1; }],
  ["rule7", "a not-now reason that is not the asked person's state", (f) => { dec(f, D7).alternatives[1].components.reason.value = 0.5; }],
  ["rule7", "a not-now reason without the Lives-only working term (the Ask's state cost alone)", (f) => { const a = dec(f, D7).alternatives; a[1].components.reason.value = a[0].components.costs.state; }],
  ["rule7", "not now eligible while no reason holds", (f) => { dec(f, D7).alternatives[1].components.reason.value = 0; }],
  ["rule7", "the invitations row disagrees with the Ask's margin", (f) => { row(f, "inv:45192:Penny:Pam:0").margin = 0.2; }],
  ["rule7", "a row margin that subtracts the Lives-only displaced habit (the decision's margin, not the Ask's)", (f) => { row(f, "inv:45192:Penny:Pam:0").margin = dec(f, D2).alternatives[0].components.margin; }],
  ["fallback", "fallback probabilities that do not normalise the weights", (f) => { const o = dec(f, D6).choice.observer; const p = { "alt:45740:Pam:0/offer": 0.1973, "alt:45740:Pam:0/carry-on": 0.8027 }; o.fallback.probabilities = p; o.probabilities = { ...p }; }],
  ["sampling", "a unit that is not Rng.Unit for its decision", (f) => { dec(f, D7).choice.observer.sampled.unit = 0.5; }],
  ["sampling", "the recorded selection disagrees with the unit", (f) => { dec(f, D7).choice.observer.sampled.unit = 0.1; }],
  ["sampling", "a Lives choice keyed by the reflection stream", (f) => { const s = dec(f, D7).choice.observer.sampled; s.stream = "reflection-choice"; s.parts = [`${f.seed}:opp:45740:Gus:0`]; }],
  ["torn", "Laya answered a choice that is not torn", (f) => { dec(f, D4).observer.torn.value = false; }],
  ["torn", "a torn choice silently answered by the fallback", (f) => { delete dec(f, D6).choice.observer.cap; }],
  ["rule9", "a chance that does not follow from its entries", (f) => { rec(f, EXW).snapshots[0].state.chance = 0.6; }],
  ["rule9", "the seeded entry dropped from Gus's evenings", (f) => { const u = by(f, "learning", "lu:45720:Gus:0"); u.dropped = { at: "pre-run", outcome: "found", source: "Reading", ref: "rd:gus-expects-pam-evenings" }; Object.assign(u.new, { count: 7, lived: 7, seeded: 0, successes: 7, chance: 1 }); }],
  ["rule9", "a revision that does not list the reading it revises as preserved", (f) => { delete by(f, "learning", "lu:45720:Gus:0").preserved; }],
  ["disappointment", "a disappointment not sized by 9.4's formula", (f) => { const u = by(f, "learning", "lu:45781:Penny:1"); u.new.strength = 0.05; u.mood.change = -0.05; }],
  ["disappointment", "a disappointment's expectation not as she held it at the yes", (f) => { const u = by(f, "learning", "lu:45781:Penny:1"), b = u.observer.basis; b.expectedTerms[0].chance = 0.25; b.expected = 0.25; u.new.strength = 0.0125; u.mood.change = -0.0125; }],
  ["disappointment", "a disappointment aimed at Pam, not the hoped-for evening (O3)", (f) => { const r = rec(f, DIS); r.id = "as:Penny:person:Pam:-:disappointed"; r.object = "person:Pam"; delete r.situation; by(f, "learning", "lu:45781:Penny:1").record = r.id; }],
  ["disappointment", "mood moved twice for one outcome", (f) => { by(f, "learning", "lu:45781:Penny:0").mood = { change: -0.0167 }; }],
  ["disappointment", "a warmth term that is not her own warm entry", (f) => { by(f, "learning", "lu:45781:Penny:1").observer.basis.warmth.record = "as:Pam:person:Penny:+:proud"; }],
  ["countOnce", "a whereabouts entry written from the agreed evening (9.5)", (f) => { f.lives.learning.push({ id: "lu:45781:Penny:2", seq: maxSeq(f) + 1, who: 2, visibleAt: 45781, record: EXW, evidence: ["per:45781:Penny:0"],
    previous: { count: 2, lived: 1, seeded: 1, successes: 0, chance: 0.3333 }, added: { at: 45781, outcome: "missed", source: "Sought" },
    new: { count: 3, lived: 2, seeded: 1, successes: 0, chance: 0.25 }, preserved: ["rd:penny-expects-pam-away"] }); }],
  // ---- fix pass (knowledge, lens and direction), 2026-10-14: each planted fault a review found passing ----
  // disappointment found structurally (an update on a missed evening's perception), never by its basis (O3, 9.4)
  ["disappointment", "a second cold entry, toward Pam, from the same missed evening (no basis, no mood)", (f) => {
    f.lives.records.push({ id: "as:Penny:person:Pam:-:let-down", seq: maxSeq(f) + 1, type: "association", holder: 2, object: "person:Pam", sign: "-", name: "let down", causes: ["per:45781:Penny:0"],
      knownBy: [{ who: 2, how: "Lived", availableAt: 45781, via: "per:45781:Penny:0" }], visibleAt: 45781, snapshots: [{ asOf: 45781, state: { strength: 0 } }], observer: { standing: "D", origin: "run" } });
    f.lives.learning.push({ id: "lu:45781:Penny:2", seq: maxSeq(f) + 2, who: 2, visibleAt: 45781, record: "as:Penny:person:Pam:-:let-down", evidence: ["per:45781:Penny:0"],
      previous: { strength: 0 }, new: { strength: 0.2 }, interpretation: { formed: true, about: 1, code: "let-down", supportedBy: ["ex:Penny:Pam:followThrough:plan"] } }); }],
  ["disappointment", "a disappointment with no mood (basis kept)", (f) => { delete by(f, "learning", "lu:45781:Penny:1").mood; }],
  ["disappointment", "a large disappointment with no basis and no mood", (f) => { const u = by(f, "learning", "lu:45781:Penny:1"); delete u.observer.basis; delete u.mood; u.new.strength = 0.9; }],
  ["disappointment", "a disappointment with no basis (mood kept)", (f) => { delete by(f, "learning", "lu:45781:Penny:1").observer.basis; }],
  ["disappointment", "a disappointment with no strength and no mood", (f) => { const u = by(f, "learning", "lu:45781:Penny:1"); delete u.new.strength; delete u.mood; }],
  ["disappointment", "the disappointment written as an interpretation of Pam", (f) => { by(f, "learning", "lu:45781:Penny:1").interpretation = { formed: true, about: 1, code: "let-down", supportedBy: ["per:45781:Penny:0"] }; }],
  // seeds are modest; a side is its associations; a stance-less reading gives no warmth (O2)
  ["seeds", "a present reading seeded strongly (Penny's care at 0.5)", (f) => { rec(f, "as:Penny:person:Pam:+:worried-care").snapshots[0].state.strength = 0.5; }],
  ["seeds", "a seeded association at 0.6 while reasons still cite 0.05", (f) => { rec(f, "as:Pam:place:Saloon:+:enjoys").snapshots[0].state.strength = 0.6; }],
  ["sides", "a warm reason whose value is not its association's strength", (f) => { const r = dec(f, D6).alternatives[0].reasons.find((x) => x.code === "warm"); r.value = 0.3; dec(f, D6).alternatives[0].components.warm = 0.3; }],
  ["sides", "the hobby wish cited as warmth", (f) => { dec(f, D3).alternatives[0].reasons.find((x) => x.code === "warm").cites.push("rd:pam-wish-hobby"); }],
  ["sides", "the hobby wish turned into a warm reason", (f) => { const r = dec(f, D3).alternatives[0].reasons.find((x) => x.code === "wish"); r.kind = "supports"; r.code = "warm"; r.value = 0.05; delete r.effect; }],
  ["sides", "components' warm side not the sum of the warm reasons", (f) => { dec(f, D4).alternatives[0].components.warm = 0.1; }],
  ["rule5", "no components anywhere (the rules cannot be checked)", (f) => { for (const d of f.lives.decisions) for (const a of d.alternatives) delete a.components; }],
  ["comparison", "an arm's weight that does not follow from its components", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p6").arms[1].consequences.weights["alt:45190:Penny:0/offer"] = 0.5; }],
  ["comparison", "an arm whose components change beyond its recorded consequences", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p4").arms[1].consequences.components["alt:45190:Penny:0/offer"].boldness = 0.5; }],
  ["comparison", "an arm whose sampled action is not the held unit's", (f) => { const c = by(f, "comparisons", "cmp:45190:Penny:0:without-p4"); c.arms[1].consequences.sampled = "alt:45190:Penny:0/keep-evening"; c.differs.sampledAction = false; }],
  // ---- fix pass (checker realism), 2026-10-14: rule 6's reasons (9.3) ----
  ["torn", "a torn choice the fallback answers as if it were not torn", (f) => { dec(f, D6).choice.observer.fallbackReason = "not-torn"; }],
  ["torn", "a choice that is not torn, answered by the fallback for the cap", (f) => { const o = dec(f, D7).choice.observer; o.fallbackReason = "cap"; o.cap = { reached: true, limit: 2, used: ["dec:44295:Pam:0", "dec:45190:Penny:0"] }; }],
  ["torn", "a torn choice answered for model-failed with no failed tape", (f) => { dec(f, D6).choice.observer.fallbackReason = "model-failed"; }],
  // ---- fix pass (the evening on summer 3-4; day types), 2026-10-14: a key's day type and a recall's day cue follow the calendar
  ["daytype", "a Saturday sighting filed under Gus's weekday key", (f) => { rec(f, EXG).snapshots[0].state.entries.find((e) => e.at === 29 * 1440 + 1085).at = 26 * 1440 + 1085; }],
  ["daytype", "Penny's weekday whereabouts key holding a Sunday absence", (f) => { rec(f, EXW).snapshots.at(-1).state.entries.find((e) => e.source === "Seen").at = 27 * 1440 + 1260; }],
  ["daytype", "a weekend cue active on a Thursday morning (Penny's 09:10 recall)", (f) => { const a = by(f, "recalls", "rc:45190:Penny:0").observer.active; a[a.indexOf("day:weekday")] = "day:weekend"; }],
];
// Legitimate shapes (variants.cjs): every one passes rule 6; "all" ones pass every rule. The authored control keeps
// the fixture's selections, so its 09:10 draw (which the fallback alone answers "keep the evening as it is") is not checked.
const LEGIT_ALL = new Set(["Laya times out on a torn question (model-failed; the failed tape named)", "Laya fails on pass 2 of a torn reflection (authored-fallback, HTTP 503)",
  "Haiku + Laya: generated thoughts; Laya also evaluates the choices that are not torn (set aside)", "Haiku + Laya with the proposed thought-only path on choices that are not torn (local-llm)",
  "quiet non-Lives reflections: one by a person with no archetype, one by Gus", "Canonical evaluation: one pass over the (kind, line) groups", "Raw evaluation: one pass labelled by the choice ids",
  "a replayed run (method tape, lives.run.tapeSource)", "Penny's actual sleep onset in the evening's timeline, backed by the moves stream (sleep recorded, not scheduled)"]);

function run(checker, f) {
  fs.writeFileSync(tmp, JSON.stringify(f));
  try { return { code: 0, out: execFileSync("node", [checker, tmp], { encoding: "utf8" }) }; } catch (e) { return { code: e.status, out: e.stdout }; }
}
let ok = 0, still = 0;
const real = realLike(base);
let legitBad = 0;
for (const [label, f] of [["fixture", base], ["real-like variant", real]]) {
  const r = run(rules, f);
  console.log(`${r.code === 0 ? "passes " : "FAILS  "} ${label} (rules checker)`);
  if (r.code !== 0) { legitBad++; console.log(r.out.split("\n").slice(3, 10).join("\n")); }
}
for (const [label, f] of legitShapes(base).filter(([name]) => name !== "Lives off: no lives block, base rows as today's writer writes them")) {
  const r = run(rules, f), torn = r.out.split("\n").filter((l) => l.includes("[torn]"));
  const pass = LEGIT_ALL.has(label) ? r.code === 0 : torn.length === 0;
  if (!pass) legitBad++;
  console.log(`${pass ? "passes " : "FAILS  "} ${label} (rules checker: ${LEGIT_ALL.has(label) ? "every rule" : "rule 6"})`);
  if (!pass) console.log((LEGIT_ALL.has(label) ? r.out.split("\n").slice(3, 10) : torn).join("\n"));
}
const byCheck = {};
for (const [check, what, mutate] of cases) {
  const f = JSON.parse(JSON.stringify(base));
  mutate(f);
  const r = run(rules, f), s = run(structure, f);
  const hit = r.code === 1 && r.out.includes(`[${check}]`);
  if (s.code === 0) still++;
  console.log(`${hit ? "caught " : "MISSED "} ${check.padEnd(15)} ${what}${s.code === 0 ? "" : "   (structure also fails)"}`);
  if (!hit) console.log(r.out.split("\n").slice(3, 8).join("\n"));
  else console.log("           " + r.out.split("\n").find((l) => l.includes(`[${check}]`)).trim().slice(0, 160));
  if (hit) ok++;
  byCheck[check] = (byCheck[check] || 0) + (hit ? 1 : 0);
}
try { fs.unlinkSync(tmp); } catch {}
console.log(`by check: ${Object.entries(byCheck).map(([k, v]) => `${k} ${v}`).join(", ")}`);
console.log(`${ok}/${cases.length} rule mutations caught; ${still}/${cases.length} of them pass the structure checker (a reader would accept them)`);
process.exit(ok === cases.length && !legitBad ? 0 : 1);
