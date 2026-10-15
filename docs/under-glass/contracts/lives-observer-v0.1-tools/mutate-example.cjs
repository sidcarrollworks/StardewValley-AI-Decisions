// Mutation test for check-lives-observer.cjs: each mutation must make the checker fail with the named check.
// Usage: node mutate-example.cjs [fixture.json]  (default: ../lives-observer-example-v0.1.json)
"use strict";
const fs = require("fs"), os = require("os"), path = require("path"), { execFileSync } = require("child_process");
const { hash } = require("./rng.cjs");
const dir = path.join(__dirname, "..");
const src = process.argv[2] || path.join(dir, "lives-observer-example-v0.1.json");
const base = JSON.parse(fs.readFileSync(src, "utf8"));
const tmp = path.join(os.tmpdir(), "lives-observer-mutation.json");
const clone = () => JSON.parse(JSON.stringify(base));
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const fnv = (v) => "fnv1a64:" + hash([canon(v)]).toString(16).padStart(16, "0");
const dec = (f, id) => f.lives.decisions.find((d) => d.id === id);
const by = (f, tbl, id) => f.lives[tbl].find((x) => x.id === id);
const rec = (f, id) => f.lives.records.find((r) => r.id === id);
const cases = [
  ["refs", "a dangling record id", (f) => { f.lives.records = f.lives.records.filter((r) => r.id !== "rd:penny-pam-care"); }],
  ["knowledge", "Penny cites Pam's private reading", (f) => { dec(f, "dec:13510:Penny:0").alternatives[0].reasons[1].cites.push("rd:pam-saloon-wary"); }],
  ["knowledge", "Penny cites a record before it exists", (f) => { dec(f, "dec:13510:Penny:0").alternatives[0].reasons[1].cites.push("ex:Penny:Pam:followThrough:plan"); }],
  ["knowledge", "a world outcome used as a reason", (f) => { dec(f, "dec:13510:Penny:0").alternatives[0].reasons[1].cites.push("out:13510:Penny:0"); }],
  ["knowledge", "learning from another person's perception", (f) => { by(f, "learning", "lu:14101:Penny:0").evidence = ["per:14045:Gus:0"]; }],
  ["knowledge", "an inference that establishes a fact", (f) => { by(f, "perceptions", "per:14101:Penny:0").inferences[0].establishes = true; }],
  ["knowledge", "the plan holds Pam's private follow-through (character lens)", (f) => { by(f, "plans", "plan:13512:Penny:Pam").members[1].followThrough = { decision: "dec:14010:Pam:0" }; }],
  ["knowledge", "Sid's commitment filed as our interpretation", (f) => { rec(f, "cmt:penny-no-alcohol").knownBy[0].how = "Authored"; }],
  ["knowledge", "a disappointment not sized by 9.4's formula", (f) => { by(f, "learning", "lu:14101:Penny:1").new.strength = 0.05; }],
  ["knowledge", "a disappointment's expectation not as she held it at the yes", (f) => { const b = by(f, "learning", "lu:14101:Penny:1").basis; b.expectedTerms[0].chance = 0.25; b.expected = 0.25; by(f, "learning", "lu:14101:Penny:1").new.strength = 0.0125; }],
  ["knowledge", "a disappointment aimed at Pam, not the evening (O3)", (f) => { const r = rec(f, "as:Penny:situation:evening-at-home-with-Pam:-:disappointed"); r.id = "as:Penny:person:Pam:-:disappointed"; r.object = "person:Pam"; delete r.situation; by(f, "learning", "lu:14101:Penny:1").record = r.id; }],
  ["knowledge", "a whereabouts entry from the agreed evening (9.5)", (f) => { f.lives.learning.push({ id: "lu:14101:Penny:2", who: 2, visibleAt: 14101, record: "ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday", rule: "9.4 not found", previous: { entries: 2, lived: 1, seeded: 1, found: 0, chance: 0.3333 }, new: { entries: 3, lived: 2, seeded: 1, found: 0, chance: 0.25 }, evidence: ["per:14101:Penny:0"], preserved: ["rd:penny-expects-pam-away"] }); }],
  ["bounds", "five records recalled", (f) => { const r = f.lives.recalls[0]; r.items.push({ ...r.items[0] }, { ...r.items[1] }); }],
  ["bounds", "a recalled item copies text", (f) => { f.lives.recalls[0].items[0].text = "the whole account"; }],
  ["bounds", "Gus's seeded entry leaves the window (rule 9)", (f) => { const u = by(f, "learning", "lu:14045:Gus:0"); u.new = { ...u.new, entries: 6, lived: 6, seeded: 0, found: 6, chance: 1, dropped: { at: "pre-run", outcome: "found", source: "Reading" } }; }],
  ["bounds", "seven lived entries beside the seeded one", (f) => { const r = rec(f, "ex:Gus:Pam:whereabouts:Saloon:18-21:weekday"); r.initial.entries.push({ day: 0, outcome: "found", source: "Seen" }, { day: 1, outcome: "found", source: "Seen" }); r.initial.lived = 7; r.initial.chance = 1; }],
  ["alternatives", "Laya gets an extra alternative", (f) => { dec(f, "dec:13511:Pam:0").choice.laya.probabilities["alt:13511:Pam:0/maybe"] = 0; }],
  ["alternatives", "a rule-5 weight that does not follow its components", (f) => { dec(f, "dec:14010:Pam:0").choice.fallback.weights["alt:14010:Pam:0/keep"] = 2; }],
  ["alternatives", "Pam's obliged set to 0.1 (rule 7 recomputed)", (f) => { dec(f, "dec:13511:Pam:0").alternatives[0].components.obliged = 0.1; }],
  ["alternatives", "a not-now reason that is not the state cost", (f) => { dec(f, "dec:14060:Gus:0").alternatives[1].components.reason.value = 0.5; }],
  ["alternatives", "the invitations row disagrees with the decision's margin", (f) => { f.invitations[0].margin = 0.2; }],
  ["sampling", "the recorded selection disagrees with the unit", (f) => { dec(f, "dec:14055:Pam:0").choice.selected = "alt:14055:Pam:0/yes"; }],
  ["sampling", "a reflection's choice keyed by the reflection stream", (f) => { const s = dec(f, "dec:13510:Penny:0").choice.sampled; s.stream = "reflection-choice"; s.parts = ["1:opp:13510:Penny:0"]; }],
  ["provenance", "the tape's request changed after hashing", (f) => { f.reflections[0].request.memory += " (edited)"; }],
  ["provenance", "Laya saw no constraints (hash recomputed)", (f) => { const t = f.choiceQuestions[0]; t.request.constraints = []; dec(f, "dec:14010:Pam:0").choice.provenance.requestHash = fnv(t.request); }],
  ["provenance", "a torn choice silently answered by the fallback", (f) => { delete dec(f, "dec:14060:Pam:0").choice.cap; }],
  ["provenance", "the fallback's reflection names a model", (f) => { f.reflections[2].answer.model = "model:laya:typed-decisions"; }],
  ["provenance", "a Laya answer without its prompt", (f) => { delete f.choiceQuestions[1].answer.layaPrompt; dec(f, "dec:14028:Penny:0").choice.provenance.requestHash = fnv(f.choiceQuestions[1].request); }],
  ["clock", "a perception before its cause", (f) => { by(f, "perceptions", "per:13512:Penny:0").visibleAt = 13500; }],
  ["clock", "an attempt before the decision's answer", (f) => { by(f, "attempts", "att:13511:Penny:0").visibleAt = 13510; }],
  ["clock", "an attempt's result shown when it starts", (f) => { by(f, "attempts", "att:14041:Penny:0").resolvedAt = 14041; }],
  ["clock", "an outcome's kind shown before it settles", (f) => { by(f, "outcomes", "out:14040:Pam:0").settledAt = 14040; }],
  ["clock", "the plan links its later outcome", (f) => { by(f, "plans", "plan:13512:Penny:Pam").outcome = "out:14040:Pam:0"; }],
  ["clock", "an expectation's start state from a later day", (f) => { rec(f, "ex:Gus:Pam:whereabouts:Saloon:15-18:weekday").initial.asOf = 2345; }],
  ["ids", "an opportunity numbered 1 when it is the holder's first that minute", (f) => { by(f, "opportunities", "opp:14010:Pam:0").id = "opp:14010:Pam:1"; }],
  ["ids", "an association id without its name", (f) => { rec(f, "as:Penny:situation:evening-at-home-with-Pam:-:disappointed").id = "as:Penny:situation:evening-at-home-with-Pam:-"; }],
  ["comparison", "a held-constant field differs", (f) => { f.lives.comparisons[0].arms[1].inputs.worldState.minute += 1; }],
  ["comparison", "a second history removed but not listed", (f) => { const h = f.lives.comparisons[0].arms[1].inputs.history; h.splice(h.indexOf("rd:penny-reading-home"), 1); }],
  ["comparison", "an unlisted derived difference", (f) => { f.lives.comparisons[1].unavoidableDifferences = f.lives.comparisons[1].unavoidableDifferences.filter((u) => u.field !== "chance"); }],
  ["comparison", "the intervention removes the decision (invalid)", (f) => { f.lives.comparisons[0].arms[1].derived.decisionExists = false; }],
  ["comparison", "differs.reasons misreported", (f) => { f.lives.comparisons[0].differs.reasons = false; }],
  ["illustrative", "the illustrative label removed", (f) => { delete f.illustrative; }],
  ["intention", "an attempt that is not the selected alternative", (f) => { by(f, "attempts", "att:14011:Pam:0").alternative = "alt:14010:Pam:0/keep"; }],
  ["archetypes", "an archetype not in the referenced pack", (f) => { f.lives.characters[0].archetype = "donor-copy"; }],
];
let ok = 0;
for (const [check, what, mutate] of cases) {
  const f = clone(); mutate(f); fs.writeFileSync(tmp, JSON.stringify(f));
  let out = "", code = 0;
  try { out = execFileSync("node", [path.join(dir, "check-lives-observer.cjs"), tmp], { encoding: "utf8" }); } catch (e) { out = e.stdout; code = e.status; }
  const hit = code === 1 && out.includes(`[${check}]`);
  console.log(`${hit ? "caught " : "MISSED "} ${check.padEnd(12)} ${what}`);
  if (!hit) console.log(out.split("\n").slice(3, 8).join("\n"));
  if (hit) ok++;
}
fs.unlinkSync(tmp);
console.log(`${ok}/${cases.length} mutations caught`);
process.exit(ok === cases.length ? 0 : 1);
