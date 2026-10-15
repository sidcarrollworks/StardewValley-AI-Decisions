// Fault suite for check-lives-observer-structure.cjs. Each case plants one fault in a copy of the fixture (or of its
// real-like variant, or of one of the legitimate run shapes in variants.cjs) and must make the structure checker fail
// with the named check. The suite also proves that the fixture, its real-like variant (no illustrative flag, no labels
// or characterLabels, no observer prose, raw model text in place of placeholders) and every legitimate shape (listed
// in `legit` and variants.cjs: an authored control, model failures, Haiku + Laya, quiet reflections without an
// archetype, Canonical and Raw evaluation, a replay, Lives off, a chunk with pending work) all pass.
// Usage: node mutate-structure.cjs [fixture.json]   (default: ../lives-observer-example-v0.2.json)
"use strict";
const fs = require("fs"), os = require("os"), path = require("path"), { execFileSync } = require("child_process");
const { hash } = require("./rng.cjs");
const { realLike } = require("./make-real-like.cjs");
const { leaks } = require("./reader-lens.cjs");
const { builders, legitShapes, fnvText, setHash, HAIKU, sleepMoves } = require("./variants.cjs");
const { csharpJson } = require("./make-real-like.cjs");
const dir = path.join(__dirname, "..");
const src = process.argv[2] || path.join(dir, "lives-observer-example-v0.2.json");
const base = JSON.parse(fs.readFileSync(src, "utf8"));
const real = realLike(base);
const checker = path.join(dir, "check-lives-observer-structure.cjs");
const tmp = path.join(os.tmpdir(), `lives-structure-mutation-${process.pid}.json`);
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const fnv = (v) => "fnv1a64:" + hash([canon(v)]).toString(16).padStart(16, "0");
const sha = (v) => require("crypto").createHash("sha256").update(canon(v), "utf8").digest("hex");
const dec = (f, id) => f.lives.decisions.find((d) => d.id === id);
const by = (f, tbl, id) => f.lives[tbl].find((x) => x.id === id);
const rec = (f, id) => f.lives.records.find((r) => r.id === id);
const row = (f, id) => [...f.favours, ...f.invitations].find((r) => r.id === id);
const per = (f, id) => f.lives.perceptions.find((x) => x.id === id);
const tape = (f, id) => [...f.reflections, ...f.choiceQuestions].find((t) => t.request.id === id);
const rehash = (f, d, t) => { dec(f, d).choice.observer.provenance.requestHash = fnv(t.request); };
const maxSeq = (f) => Math.max(...["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons", "threads"].flatMap((t) => f.lives[t].map((x) => x.seq)));
const PAM = 1, PENNY = 2, GUS = 0;
const D1 = "dec:45190:Penny:0", D2 = "dec:45191:Pam:0", D3 = "dec:45688:Pam:0", D4 = "dec:45708:Penny:0", D6 = "dec:45740:Pam:0";
const EXW = "ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday", EXG = "ex:Gus:Pam:whereabouts:Saloon:18-21:weekday";

const cases = [
  // version and shape
  ["version", "lives.version 0.1: readers accept exact versions only", (f) => { f.lives.version = "0.1"; }],
  ["version", "a table that is not an array", (f) => { f.lives.plans = {}; }],
  ["version", "an element without seq", (f) => { delete by(f, "perceptions", "per:45781:Penny:0").seq; }],
  ["illustrative", "the illustrative flag set in lives only", (f) => { delete f.illustrative; }],
  ["illustrative", "a recording with no illustrative flag and no source commit", (f) => { delete f.illustrative; delete f.lives.illustrative; f.lives.run.source.commit = null; }],
  // ids
  ["ids", "an opportunity numbered 1 when it is the holder's first that minute", (f) => { by(f, "opportunities", "opp:45688:Pam:0").id = "opp:45688:Pam:1"; }],
  ["ids", "an association id without its name", (f) => { rec(f, "as:Penny:situation:evening-at-home-with-Pam:-:disappointed").id = "as:Penny:situation:evening-at-home-with-Pam:-"; }],
  ["ids", "a favours row id that does not name its minute, pair and ordinal", (f) => { row(f, "fav:43820:Gus:Penny:0").id = "fav:43820:Gus:Penny:7"; }],
  ["ids", "the same id in two tables", (f) => { by(f, "perceptions", "per:45191:Pam:0").id = "opp:45191:Pam:0"; }],
  // references
  ["refs", "a dangling record id", (f) => { f.lives.records = f.lives.records.filter((r) => r.id !== "rd:penny-pam-care"); }],
  ["refs", "a person index outside names", (f) => { by(f, "perceptions", "per:45720:Gus:0").perceived.subject = 7; }],
  // the lens partition
  ["lens", "notPerceived shown to the character (moved out of observer)", (f) => { const p = by(f, "perceptions", "per:45781:Penny:0"); p.notPerceived = p.observer.notPerceived; }],
  ["lens", "an observer thread title in a character element", (f) => { by(f, "perceptions", "per:45720:Gus:0").characterLabel = "Gus: " + by(f, "threads", "thr:penny-pam-evening").observer.title; }],
  ["lens", "a thread title at the thread's top level", (f) => { const t = by(f, "threads", "thr:pam-gus-game"); t.title = t.observer.title; }],
  ["lens", "a top-level label, as 0.1 had", (f) => { dec(f, D1).label = "After saying no to beer, what does Penny offer instead?"; }],
  ["lens", "a secret world truth in a characterLabel", (f) => { by(f, "perceptions", "per:45781:Penny:0").characterLabel = "Penny knows " + by(f, "perceptions", "per:45781:Penny:0").observer.notPerceived[0]; }],
  ["lens", "a reference outside the lens: Penny's reason cites Pam's private reading", (f) => { dec(f, D1).alternatives[0].reasons[1].cites.push("rd:pam-saloon-wary"); }],
  ["lens", "a world outcome cited in character-safe content", (f) => { by(f, "perceptions", "per:45781:Penny:0").causes.push("out:45720:Pam:0"); }],
  ["lens", "a source cited outside observer", (f) => { dec(f, D1).excluded[1].why.cites = ["src:code-errand-uses"]; }],
  ["lens", "a characterLabel on a world outcome", (f) => { by(f, "outcomes", "out:45720:Pam:0").characterLabel = "the evening"; }],
  ["lens", "the shared plan holds Pam's private follow-through", (f) => { by(f, "plans", "plan:45192:Penny:Pam").members[1].followThrough = { decision: D3 }; }],
  ["lens", "a shared fact cites one knower's account for all its knowers", (f) => { rec(f, "h:pam-saloon-evenings").causes = ["acct:pam-saloon-evenings:Penny"]; }],
  ["lens", "an expectation cited before its first recorded state", (f) => { const r = rec(f, EXW); r.snapshots = [r.snapshots.at(-1)]; r.snapshots[0].asOf = 45500; }],
  // the clock and its gates
  ["clock", "a perception before its cause", (f) => { by(f, "perceptions", "per:45192:Penny:0").visibleAt = 45180; }],
  ["clock", "an attempt's result shown when it starts", (f) => { by(f, "attempts", "att:45721:Penny:0").resolvedAt = 45721; }],
  ["clock", "an outcome's kind shown before it settles", (f) => { by(f, "outcomes", "out:45720:Pam:0").settledAt = 45720; }],
  ["clock", "a plan that links its outcome", (f) => { by(f, "plans", "plan:45192:Penny:Pam").outcome = "out:45720:Pam:0"; }],
  ["clock", "an effect ordered before its cause within a minute (seq)", (f) => { by(f, "perceptions", "per:45192:Penny:0").seq = maxSeq(f) + 1; }],
  ["clock", "an expectation entry later than its snapshot", (f) => { rec(f, EXG).snapshots[0].state.entries[1].at = 43810; }],
  ["clock", "a decision's choice before its question", (f) => { dec(f, D4).choice.visibleAt = 45680; }],
  // knowledge
  ["knowledge", "learning from another person's perception", (f) => { by(f, "learning", "lu:45781:Penny:0").evidence = ["per:45720:Gus:0"]; }],
  ["knowledge", "an inference that establishes a fact", (f) => { by(f, "perceptions", "per:45781:Penny:0").inferences[0].establishes = true; }],
  ["knowledge", "Sid's commitment filed as our interpretation", (f) => { rec(f, "cmt:penny-no-alcohol").knownBy[0].how = "Authored"; }],
  ["knowledge", "a personal reading known by someone else", (f) => { rec(f, "rd:penny-pam-care").knownBy.push({ who: PAM, how: "Told", availableAt: "pre-run", teller: PENNY }); }],
  // attempts: being the target is not knowledge
  ["attempts", "Penny told of Gus's busy attempt (a knownBy entry for the target)", (f) => { by(f, "attempts", "att:43820:Gus:0").knownBy.push({ who: PENNY, how: "Told", availableAt: 43820, teller: GUS }); }],
  ["attempts", "Penny perceives Gus's busy attempt", (f) => { f.lives.perceptions.push({ id: "per:43820:Penny:0", seq: maxSeq(f) + 1, who: PENNY, visibleAt: 43820, of: "att:43820:Gus:0", causes: [], perceived: { kind: "asked", by: GUS, ask: "errand", good: "flour" } }); }],
  ["attempts", "the asked target listed in the asker's knownBy", (f) => { by(f, "attempts", "att:45190:Pam:0").knownBy.push({ who: PENNY, how: "Lived", availableAt: 45190 }); }],
  // snapshots and replay
  ["snapshots", "an update applied before the record's first recorded state", (f) => { rec(f, EXG).snapshots[0].asOf = 45730; }],
  ["snapshots", "an update whose previous is not the record's state", (f) => { by(f, "learning", "lu:45720:Gus:0").previous.count = 4; }],
  ["snapshots", "a created record whose first state is not at its creation", (f) => { rec(f, "ex:Penny:Pam:answers:plan").snapshots[0].asOf = 45191; }],
  // bounds against the recording's settings
  ["bounds", "five records recalled", (f) => { const r = by(f, "recalls", "rc:45191:Pam:0"); r.items.push({ ...r.items[0] }); }],
  ["bounds", "a low-mood slot without low mood", (f) => { by(f, "recalls", "rc:45191:Pam:0").observer.bound.lowMood = false; }],
  ["bounds", "seven lived entries beside the seeded one", (f) => { const s = rec(f, EXG).snapshots[0].state; for (const d of [21]) s.entries.push({ at: d * 1440 + 1085, outcome: "found", source: "Seen" }); s.lived = 7; s.successes = 8; s.chance = 1; }],
  ["bounds", "more model questions in a person-day than settings allow", (f) => { f.lives.settings.modelQuestionsPerPersonDay = 1; }],
  ["bounds", "more associations toward one object than settings allow", (f) => { f.lives.settings.associationsPerObject = 1; }],
  // base rows: answers kept distinct
  ["rows", "not now encoded as accepted: false", (f) => { row(f, "inv:45741:Pam:Gus:0").accepted = false; }],
  ["rows", "not now encoded as a refusal with no act", (f) => { const r = row(f, "inv:45741:Pam:Gus:0"); r.answer = "no"; r.accepted = false; }],
  ["rows", "two rows colliding on one actId", (f) => { row(f, "inv:45192:Penny:Pam:0").actId = 412; }],
  ["rows", "-1 for 'no act', as 0.1 had", (f) => { row(f, "inv:45192:Penny:Pam:0").actId = -1; }],
  ["rows", "a busy draw recorded as an ask", (f) => { row(f, "fav:43820:Gus:Penny:0").askMade = true; }],
  ["rows", "a legacy row whose answer cannot be proven", (f) => { const r = row(f, "fav:44296:Gus:Pam:0"); delete r.answer; delete r.askMade; r.actId = 999; }],
  ["rows", "a base row disagreeing with the decision's answer", (f) => { const r = row(f, "inv:45192:Penny:Pam:0"); r.answer = "not-now"; r.accepted = null; }],
  // the archetype pin
  ["pin", "an archetype resolved without its snapshot", (f) => { delete f.characterInspiration.snapshot; }],
  ["pin", "a snapshot edited after hashing", (f) => { f.characterInspiration.snapshot[0].voice += " (edited)"; }],
  ["pin", "a character pinned to another pack", (f) => { f.lives.characters[0].archetype.packHash = "0".repeat(64); }],
  ["pin", "a snapshot carrying a personal profile (hash recomputed)", (f) => { const ci = f.characterInspiration; ci.snapshot[1].background = [{ id: "pam-penny", text: "x" }]; ci.snapshotHash = sha(ci.snapshot); }],
  ["pin", "a request given another archetype's cue", (f) => { f.reflections[0].request.character.evaluatorCue = "Resist intrusion and pity."; }],
  ["pin", "a record naming an archetype", (f) => { rec(f, "rd:penny-pam-care").observer.archetype = "steadfast-carer"; }],
  // one option set for both methods
  ["options", "Laya gets an extra alternative", (f) => { dec(f, D2).choice.observer.laya.probabilities["alt:45191:Pam:0/maybe"] = 0; }],
  ["options", "the tape's request changed after hashing", (f) => { f.reflections[1].request.memory += " (edited)"; }],
  ["options", "Laya saw no constraints (request hash recomputed)", (f) => { const t = f.choiceQuestions[0]; t.request.constraints = []; dec(f, D3).choice.observer.provenance.requestHash = fnv(t.request); }],
  ["options", "the option-set hash does not cover a changed reason value", (f) => { dec(f, D1).alternatives[0].reasons[1].value = 0.06; }],
  // models and receipts
  ["models", "Balanced weights that are not the passes' mean", (f) => { f.reflections[1].answer.evaluations[0].weights.offer += 0.1; }],
  ["models", "a Laya answer without its evaluator", (f) => { f.choiceQuestions[1].answer.model = null; }],
  ["models", "the fallback's reflection names a model", (f) => { f.reflections[3].answer.model = "model:laya:typed-decisions"; }],
  ["models", "a receipt that skips a pass", (f) => { f.reflections[1].answer.receipt.evaluations.pop(); }],
  ["models", "real output whose raw Laya text is a placeholder", (f) => { f.choiceQuestions[0].answer.evaluations[0].response = "(placeholder)"; }, "real"],
  ["models", "real output whose receipt disagrees with the raw routing", (f) => { f.reflections[1].answer.receipt.evaluations[0].inputTokens += 1; }, "real"],
  // the thought
  ["thought", "a thought from another person's reflection", (f) => { dec(f, D2).choice.thought.tape = f.lives.opportunities.find((o) => o.id === "opp:45190:Penny:0").observer.reflection; }],
  ["thought", "a thought not marked imagined", (f) => { dec(f, D1).choice.thought.imagined = false; }],
  // comparisons
  ["comparisons", "a non-comparable case given a matched decision", (f) => { const c = by(f, "comparisons", "cmp:45190:Penny:0:without-commitment"); Object.assign(c.arms[1].consequences, { probabilities: c.arms[0].consequences.probabilities, sampled: c.arms[0].consequences.sampled }); }],
  ["comparisons", "a comparable case whose intervention removes the opportunity", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p6").arms[1].consequences.opportunity = null; }],
  ["comparisons", "a held random input that is not the decision's draw", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p6").heldConstant.randomInput.unit = 0.5; }],
  ["comparisons", "a second history removed but not listed", (f) => { const h = by(f, "comparisons", "cmp:45190:Penny:0:without-p6").arms[1].history; h.splice(h.indexOf("rd:penny-reading-home"), 1); }],
  ["comparisons", "differs.reasons misreported", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p6").differs.reasons = false; }],
  ["comparisons", "notShown omits later lives", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p4").notShown = ["laya-answers"]; }],
  ["comparisons", "an unlisted consequence difference", (f) => { const c = by(f, "comparisons", "cmp:45190:Penny:0:without-p4"); c.unavoidableDifferences = c.unavoidableDifferences.filter((u) => u.field !== "chance"); }],
  ["comparisons", "a comparison that is not fallback-only", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p4").heldConstant.method.backend = "laya"; }],
  // intention
  ["intention", "an attempt that is not the selected alternative", (f) => { by(f, "attempts", "att:45689:Pam:0").alternative = "alt:45688:Pam:0/keep"; }],

  // ---- fix pass (knowledge, lens and direction), 2026-10-14: each planted fault a review found passing ----
  // a closed character-safe vocabulary: nested keys allowlisted, strings are codes
  ["lens", "secret truth as a value under an unlisted key in perceived", (f) => { per(f, "per:45781:Penny:0").perceived.detail = "Pam stayed at the Saloon from 16:35, by her own choice at 17:29"; }],
  ["lens", "a future minute under an unlisted key in perceived", (f) => { per(f, "per:45781:Penny:0").perceived.pamHomeAt = 45871; }],
  ["lens", "secret truth in an unknown's about", (f) => { per(f, "per:45781:Penny:0").unknown[0].about = "that Pam stayed at the Saloon from 16:35"; }],
  ["lens", "prose inside a plan's activity code", (f) => { by(f, "plans", "plan:45192:Penny:Pam").activity = "PlayedGame (Pam will stay at the Saloon instead)"; }],
  ["lens", "a member-private basis under an unlisted key in an attempt's terms", (f) => { by(f, "attempts", "att:45191:Penny:0").terms.note2 = "Penny's own bedtime decides the end"; }],
  ["lens", "the outcome's minute under an unlisted key in a plan's window", (f) => { by(f, "plans", "plan:45192:Penny:Pam").window.pamArrivedHomeAt = 45871; }],
  ["lens", "secret truth in a commitment's scope", (f) => { rec(f, "cmt:penny-no-alcohol").scope.becauseOf = "Pam drinks at the Saloon until 23:00"; }],
  ["lens", "an extra field in decidedBy", (f) => { by(f, "attempts", "att:43820:Gus:0").decidedBy.detail = "Penny was teaching in the Square"; }],
  ["lens", "a place perceived for the absent person (an inference promoted)", (f) => { per(f, "per:45781:Penny:0").perceived.place = "Saloon"; }],
  ["lens", "another's choice as a perceived code", (f) => { per(f, "per:45781:Penny:0").perceived.because = "stayed-at-saloon-by-choice"; }],
  ["lens", "another's private reading in perceived", (f) => { per(f, "per:45741:Pam:0").perceived.thinks = "she drinks too much"; }],
  ["lens", "free prose about the busy attempt in Penny's perception", (f) => { per(f, "per:45190:Penny:0").perceived.aside = "Gus would have asked her for flour on Wednesday"; }],
  ["lens", "the answerer's reason code copied into the asker's perception", (f) => { per(f, "per:45741:Pam:0").perceived.reason = "working"; }],
  ["lens", "the answerer's reason code copied into the asker's expectation entry", (f) => { by(f, "learning", "lu:45741:Pam:0").added.reason = "working"; }],
  ["lens", "an outcome-revealing code on an unresolved entry", (f) => { f.lives.unresolved[2].kind = "agreed-absent"; }],
  ["lens", "prose at a model entry's top level", (f) => { f.lives.models[0].verify = f.lives.models[0].observer.verify; }],
  ["lens", "a shared plan's label in one knower's kinship voice", (f) => { by(f, "plans", "plan:45192:Penny:Pam").characterLabel = "An evening at home with Mom"; }],
  // being the target is not knowledge (5.2)
  ["knowledge", "Penny perceives Gus's never-made ask through codes alone (of: null)", (f) => { f.lives.perceptions.push({ id: "per:43820:Penny:0", seq: maxSeq(f) + 1, who: PENNY, visibleAt: 43820, of: null, causes: [], perceived: { kind: "asked", by: GUS, ask: "errand", good: "flour" } }); }],
  ["lens", "an unknown naming Gus's ask in Penny's perception", (f) => { per(f, "per:45781:Penny:0").unknown.push({ about: "flour-ask", subject: GUS, status: "unknown" }); }],
  ["knowledge", "an unknown about Gus, who is no party to what Penny perceived", (f) => { per(f, "per:45781:Penny:0").unknown.push({ about: "answer", subject: GUS, status: "unknown" }); }],
  ["knowledge", "a sighting of someone the same perception lists as unknown", (f) => { per(f, "per:45720:Gus:0").unknown = [{ about: "whereabouts", subject: PAM, status: "unknown" }]; }],
  // base rows are the asked person's: never in the asker's lens (5.4, 8)
  ["lens", "Pam's perception cites the favours row carrying Penny's wontFetch (her commitment)", (f) => { per(f, "per:45190:Pam:0").causes.push("fav:45190:Pam:Penny:0"); }],
  ["lens", "Gus's perception cites the row with Pam's state and margin", (f) => { per(f, "per:44296:Gus:0").causes.push("fav:44296:Gus:Pam:0"); }],
  ["lens", "Penny's expectation entry cites the row of Pam's yes (Pam's state)", (f) => { by(f, "learning", "lu:45192:Penny:0").added.ref = "inv:45192:Penny:Pam:0"; }],
  ["lens", "Gus's attempt names the busy row in character-safe content", (f) => { const a = by(f, "attempts", "att:43820:Gus:0"); a.base.push(a.observer.rows[0]); }],
  // tapes: the character lens reaches a tape only as its holder's thought, from the choice's minute (4.3, 5.4)
  ["lens", "an opportunity's tape link at its top level (the answer shown at the opportunity minute)", (f) => { const o = by(f, "opportunities", "opp:45190:Penny:0"); o.reflection = o.observer.reflection; delete o.observer.reflection; }],
  ["lens", "Penny's perception cites her tape before the choice is visible", (f) => { per(f, "per:45190:Penny:0").causes.push("reflection:7315:opp:45190:Penny:0"); }],
  ["lens", "a question tape cited in the character lens", (f) => { per(f, "per:45720:Pam:0").causes.push("question:7315:q:45688:Pam:0"); }],
  ["clock", "a tape event later than the choice it is shown with", (f) => { tape(f, "7315:opp:45190:Penny:0").events[0].tick = 45192; }],
  ["lens", "a tape recalls a record its holder cannot see (request hash recomputed)", (f) => { const t = tape(f, "7315:opp:45190:Penny:0"); t.request.recalled.push({ id: "rd:pam-saloon-wary", origin: "CanonStated" }); rehash(f, D1, t); }],
  // one option set for both methods, as the evaluator saw it (9.4)
  ["options", "Laya never saw 'not now' (choices, weights and passes without it; request hash recomputed)", (f) => {
    const t = tape(f, "7315:opp:45191:Pam:0"), ids = ["yes", "no"];
    t.request.choices = t.request.choices.filter((c) => c.id !== "not-now");
    for (const e of t.answer.evaluations) { delete e.weights["not-now"]; for (const [k, v] of Object.entries(e.labelsToChoiceIds)) if (v.includes("not-now")) delete e.labelsToChoiceIds[k]; }
    t.answer.evaluations = t.answer.evaluations.slice(0, 2); t.answer.receipt.evaluations = t.answer.receipt.evaluations.slice(0, 2);
    const norm = (w) => { const s = ids.reduce((a, k) => a + w[k], 0); return Object.fromEntries(ids.map((k) => [k, w[k] / s])); };
    t.answer.weights = norm(Object.fromEntries(ids.map((k) => [k, t.answer.evaluations.reduce((s, p) => s + norm(p.weights)[k], 0) / 2])));
    rehash(f, D2, t);
    const o = dec(f, D2).choice.observer;
    o.laya.probabilities = { "alt:45191:Pam:0/yes": t.answer.weights.yes, "alt:45191:Pam:0/not-now": 0, "alt:45191:Pam:0/no": t.answer.weights.no };
    o.probabilities = { ...o.laya.probabilities }; }],
  ["options", "a question tape whose option lines omit an eligible alternative (request hash recomputed)", (f) => { const t = tape(f, "7315:q:45688:Pam:0"); t.request.choices = t.request.choices.filter((c) => c.id !== "stay"); rehash(f, D3, t); }],
  // pre-run provenance: standing, origin, sources, admission, review (7)
  ["provenance", "readings without standing or sources", (f) => { for (const r of f.lives.records) if (r.type === "reading") { delete r.observer.standing; delete r.observer.sources; } }],
  ["provenance", "a reading without its review status", (f) => { delete rec(f, "rd:penny-pam-care").observer.review; }],
  ["provenance", "an admission that disagrees with the standing", (f) => { rec(f, "rd:penny-pam-care").observer.admission = "R1"; }],
  ["provenance", "a record seeded from a rejected reading", (f) => { rec(f, "rd:penny-pam-care").observer.review = { status: "rejected", by: "Sid", date: "2026-10-14" }; }],
  // in-run knowledge names what gave it; an inference never becomes a fact or an entry (5.1)
  ["knowledge", "an inference promoted to a witnessed fact (no via)", (f) => { f.lives.records.push({ id: "h:pam-at-saloon-tonight", seq: maxSeq(f) + 1, type: "fact", kind: "Fact", participants: [PAM], place: "Saloon",
    knownBy: [{ who: PENNY, how: "Witnessed", availableAt: 45781 }], visibleAt: 45781, observer: { standing: "D", origin: "run" } }); }],
  ["knowledge", "a fact witnessed through a perception that only guesses", (f) => { f.lives.records.push({ id: "h:pam-at-saloon-tonight", seq: maxSeq(f) + 1, type: "fact", kind: "Fact", participants: [PAM], place: "Saloon",
    knownBy: [{ who: PENNY, how: "Witnessed", availableAt: 45781, via: "per:45781:Penny:0" }], visibleAt: 45781, observer: { standing: "D", origin: "run" } }); }],
  ["knowledge", "Penny told of Gus's flour with no telling act", (f) => { f.lives.records.push({ id: "h:gus-wanted-flour", seq: maxSeq(f) + 1, type: "fact", kind: "Fact", participants: [GUS],
    knownBy: [{ who: PENNY, how: "Told", teller: GUS, availableAt: 43821 }, { who: GUS, how: "Lived", availableAt: 43820, via: "att:43820:Gus:0" }], visibleAt: 43820, causes: [], observer: { standing: "D", origin: "run" } }); }],
  ["knowledge", "a whereabouts entry resting on a guess", (f) => { f.lives.learning.push({ id: "lu:45781:Penny:2", seq: maxSeq(f) + 1, who: PENNY, visibleAt: 45781, record: EXW, evidence: ["per:45781:Penny:0"],
    previous: { count: 2, lived: 1, seeded: 1, successes: 0, chance: 0.3333 }, added: { at: 45781, outcome: "found", source: "Sought" },
    new: { count: 3, lived: 2, seeded: 1, successes: 1, chance: 0.5 }, preserved: ["rd:penny-expects-pam-away"] }); }],
  ["knowledge", "an in-run expectation known with no via", (f) => { delete rec(f, "ex:Penny:Pam:followThrough:plan").knownBy[0].via; }],
  // observer thread summaries keep their own gates (6.1)
  ["clock", "a thread summary line ahead of the events it cites", (f) => { const s = by(f, "threads", "thr:penny-pam-evening").observer.summary; s[2] = { visibleAt: 45689, label: "Pam stays at the Saloon; Penny heads home", cites: ["att:45689:Pam:0", "att:45709:Penny:0"] }; s.splice(3, 1); }],
  ["clock", "a thread summary line that cites nothing", (f) => { delete by(f, "threads", "thr:pam-gus-game").observer.summary[1].cites; }],
  // mood is its own field; an association's state is {strength} (6.3, 12)
  ["lens", "an association update carrying extra state (a regard toward Pam)", (f) => { by(f, "learning", "lu:45781:Penny:1").new.regardTowardPam = -0.2; }],
  ["lens", "mood written into the association's state (the draft's moodChange)", (f) => { const u = by(f, "learning", "lu:45781:Penny:1"); u.new.moodChange = u.mood.change; delete u.mood; }],
  // comparisons hold the option set's identity; each arm hashes its own reasons (13)
  ["comparisons", "the reason-bearing decision hash held constant", (f) => { const o = by(f, "comparisons", "cmp:45190:Penny:0:without-p6").heldConstant.options; o.setHash = dec(f, D1).choice.observer.provenance.alternativeSetHash; delete o.identityHash; }],
  ["comparisons", "an arm whose setHash is not its own reasons'", (f) => { const c = by(f, "comparisons", "cmp:45190:Penny:0:without-p4"); c.arms[1].consequences.setHash = c.arms[0].consequences.setHash; }],
  // Balanced counts (kind, line) groups and splits a group's mass equally (10)
  ["models", "Balanced with one pass per alternative where two share a (kind, line) group", (f) => { const t = tape(f, "7315:opp:45191:Pam:0"); t.request.choices.find((c) => c.id === "not-now").line = t.request.choices.find((c) => c.id === "no").line; rehash(f, D2, t); }],
  ["models", "a shared group whose mass a pass splits unequally", (f) => { sharedGroup(f); const p = tape(f, "7315:opp:45191:Pam:0").answer.evaluations[0]; p.weights["not-now"] = 0.19; p.weights.no = 0.06; }],

  // ---- fix pass (checker realism, hashing, runtime fidelity), 2026-10-14 ----
  // canonical JSON and hashes, defined once (9.6): a C# writer's default bytes are not the canonical form
  ["options", "requestHash over System.Text.Json's default text (apostrophes as \\u0027, members unsorted)", (f) => { const t = tape(f, "7315:opp:45190:Penny:0"); dec(f, D1).choice.observer.provenance.requestHash = fnvText(csharpJson(t.request)); }],
  ["options", "requestHash with numbers as .NET writes them (1E-05), members sorted", (f) => {
    const t = tape(f, "7315:q:45688:Pam:0"), d = dec(f, D3);
    d.alternatives[0].reasons[0].value = 0.00001; t.request.alternatives[0].reasons[0].value = 0.00001;
    const v = { alternatives: d.alternatives.map((a) => ({ id: a.slug, reasons: a.reasons.map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null })) })), excluded: [], constraints: d.constraints.map((c) => ({ code: c.code, value: c.value ?? null, terms: c.terms ?? null })) };
    d.choice.observer.provenance.alternativeSetHash = t.request.alternativeSetHash = setHash(v.alternatives, v.excluded, v.constraints);
    d.choice.observer.provenance.requestHash = fnvText(canon(t.request).replace("0.00001", "1E-05")); }],
  ["options", "a request string with a lone surrogate (hash recomputed over JSON.stringify's escape)", (f) => { const t = tape(f, "7315:opp:45190:Penny:0"); t.request.memory += "\ud800"; rehash(f, D1, t); }],
  ["pin", "a snapshot string with a lone surrogate (hash recomputed)", (f) => { const ci = f.characterInspiration; ci.snapshot[0].voice += "\udc00"; ci.snapshotHash = sha(ci.snapshot); }],
  // the evaluator's passes exactly as ResilientReflectionMind builds them (10)
  ["models", "two pass labels swapped (not BuildPasses' slots)", (f) => { const m = tape(f, "7315:opp:45191:Pam:0").answer.evaluations[0].labelsToChoiceIds; [m.o0, m.o1] = [m.o1, m.o0]; }],
  ["models", "Balanced passes in the wrong rotation (renumbered)", (f) => { const a = tape(f, "7315:opp:45191:Pam:0").answer; a.evaluations.reverse().forEach((p, i) => { p.pass = i + 1; }); a.receipt.evaluations.reverse().forEach((p, i) => { p.pass = i + 1; }); }],
  ["models", "pass labels named A, B, C (not o0, o1, o2)", (f) => { for (const p of tape(f, "7315:opp:45191:Pam:0").answer.evaluations) p.labelsToChoiceIds = Object.fromEntries(Object.values(p.labelsToChoiceIds).map((ids, i) => [String.fromCharCode(65 + i), ids])); }],
  ["models", "a pass whose weights are not normalised", (f) => { const p = tape(f, "7315:q:45688:Pam:0").answer.evaluations[0]; p.weights.keep += 0.1; p.weights.stay += 0.1; }],
  ["models", "an authored thought's weights set to the fallback's (not what AuthoredReflectionMind returns)", (f) => { const d = dec(f, "dec:44295:Pam:0"), t = tape(f, "7315:opp:44295:Pam:0"); t.answer.weights = Object.fromEntries(d.alternatives.map((a) => [a.slug, d.choice.observer.fallback.probabilities[a.id]])); }],
  ["models", "a question tape recording a reflection backend (authored+laya)", (f) => { tape(f, "7315:q:45688:Pam:0").answer.backend = "authored+laya"; }],
  ["models", "a reflection tape recording the proposed question backend (laya)", (f) => { tape(f, "7315:opp:45191:Pam:0").answer.backend = "laya"; }],
  // failures recorded explicitly (9.3, 10)
  ["models", "a fallback {stage, code} on an answer that did not fall back", (f) => { tape(f, "7315:opp:45191:Pam:0").answer.fallback = { stage: "Laya", code: "timeout", httpStatus: null }; }],
  ["models", "a failed pass on an answer that is not an authored-fallback", (f) => { const p = tape(f, "7315:opp:45191:Pam:0").answer.evaluations[2]; p.error = "HTTP 503."; p.weights = null; }],
  ["models", "an authored-fallback that does not say what failed", (f) => { delete tape(f, "7315:q:45708:Penny:0").answer.fallback; }, "Laya times out on a torn question (model-failed; the failed tape named)"],
  ["models", "a failure code outside the stage's list", (f) => { tape(f, "7315:q:45708:Penny:0").answer.fallback.code = "slow"; }, "Laya times out on a torn question (model-failed; the failed tape named)"],
  ["options", "a failed question whose weights are not the fallback's", (f) => { tape(f, "7315:q:45708:Penny:0").answer.weights = { keep: 0.5, stay: 0.5 }; }, "Laya times out on a torn question (model-failed; the failed tape named)"],
  ["models", "a Laya answer resting on a failed tape", (f) => { const a = tape(f, "7315:q:45708:Penny:0").answer; a.backend = "authored-fallback"; a.fallback = { stage: "Laya", code: "timeout", httpStatus: null }; }],
  ["options", "fallbackReason model-failed naming a tape that did not fail", (f) => { dec(f, "dec:44295:Pam:0").choice.observer.fallbackReason = "model-failed"; }],
  ["options", "a torn choice answered as no-evaluator in a run that names an evaluator", (f) => { const o = dec(f, D6).choice.observer; o.fallbackReason = "no-evaluator"; delete o.cap; }],
  ["options", "an evaluated, unfailed reflection set aside as model-failed", (f) => { const o = dec(f, D2).choice.observer; o.backend = "fallback-keyed-draw"; o.probabilities = { ...o.fallback.probabilities }; delete o.laya; o.provenance.model = null; o.fallbackReason = "model-failed"; }],
  ["models", "a model that answered in the recording but reports nothing served", (f) => { f.lives.models[0].served.model = null; }],
  ["models", "an authored control whose method names an evaluator", (f) => { f.lives.run.method.evaluator = "model:laya:typed-decisions"; }, "authored control: no evaluator, every torn choice the fallback's (no-evaluator)"],
  // real output: the raw text is what Laya read, and the receipt digests it (10)
  ["models", "real output whose raw prompt omits an eligible line", (f) => { const p = tape(f, "7315:opp:45191:Pam:0").answer.evaluations[0], x = JSON.parse(p.prompt); delete x.questions.q.criteria.o2; p.prompt = JSON.stringify(x); }, "real"],
  ["models", "real output whose raw prompt shows a line the request does not hold", (f) => { const p = tape(f, "7315:opp:45191:Pam:0").answer.evaluations[1], x = JSON.parse(p.prompt); x.questions.q.criteria.o0 = "PlanAnswer: Sure, any time."; p.prompt = JSON.stringify(x); }, "real"],
  ["models", "real output whose pass weights do not follow from the raw probabilities", (f) => { const p = tape(f, "7315:q:45708:Penny:0").answer.evaluations[0], x = JSON.parse(p.response); const [a, b] = Object.keys(x.answers.q.probabilities); [x.answers.q.probabilities[a], x.answers.q.probabilities[b]] = [x.answers.q.probabilities[b], x.answers.q.probabilities[a]]; p.response = JSON.stringify(x); }, "real"],
  ["models", "real output whose layaResponse is not the last pass's", (f) => { const a = tape(f, "7315:opp:45191:Pam:0").answer; a.layaResponse = a.evaluations[0].response; }, "real"],
  ["models", "real output whose receipt disagrees with the raw usage (state tokens)", (f) => { tape(f, "7315:opp:45191:Pam:0").answer.receipt.evaluations[1].stateTokens += 3; }, "real"],
  ["models", "a generated thought whose receipt disagrees with the raw response", (f) => { tape(f, "7315:opp:45190:Penny:0").answer.receipt.generation.outputTokens += 1; }, "Haiku + Laya: generated thoughts; Laya also evaluates the choices that are not torn (set aside)"],
  ["models", "a hybrid tape that does not name its generator", (f) => { delete tape(f, "7315:opp:44295:Pam:0").answer.generatorModel; }, "Haiku + Laya with the proposed thought-only path on choices that are not torn (local-llm)"],
  // replay provenance belongs to the run (10)
  ["models", "a replayed run that does not name its tape", (f) => { delete f.lives.run.tapeSource; }, "a replayed run (method tape, lives.run.tapeSource)"],
  ["models", "a tapeSource on a run that is not a replay", (f) => { f.lives.run.tapeSource = { runId: f.lives.runId, sha256: "0".repeat(64) }; }, "real"],
  // excerpts and chunks, validated rather than trusted (6.4)
  ["excerpt", "an excerpt whose from is after its to", (f) => { f.lives.excerpt.from = 45960; }],
  ["excerpt", "an element after the excerpt's end", (f) => { f.lives.excerpt.to = 45780; }],
  ["excerpt", "a real recording's excerpt that is not a storage chunk", (f) => { delete f.lives.excerpt.chunk; }, "real"],
  ["excerpt", "elements before the chunk's declared start", (f) => { f.lives.excerpt.from = 44080; f.lives.excerpt.to = 45900; }, "a storage chunk ending mid-evening, with pending elements"],
  // pending elements are listed (6.1)
  ["clock", "an attempt left unresolved and not listed as pending", (f) => { const a = by(f, "attempts", "att:45709:Penny:0"); delete a.resolvedAt; delete a.status; }],
  ["clock", "a chunk's open plan not listed, so its outcome is not pending", (f) => { f.lives.unresolved = f.lives.unresolved.filter((u) => !u.ref.startsWith("plan:")); }, "a storage chunk ending mid-evening, with pending elements"],
  // ---- fix pass (change list and adapter impact), 2026-10-14: a 0.1 path left in place by a careless port. Each is a
  // field Codex's 0.1 adapter reads (chains-model.js at aef9d7f, cited as CM) or a row of the change list; each is refused.
  ["lens", "0.1's learning rule at the top level (CM:149)", (f) => { const u = by(f, "learning", "lu:45781:Penny:1"); u.rule = u.observer.rule; }],
  ["snapshots", "a new association's first update from 0.1's previous: null (CM:213's 'new')", (f) => { by(f, "learning", "lu:45781:Penny:1").previous = null; }],
  ["lens", "0.1's new.name on an association update", (f) => { by(f, "learning", "lu:45781:Penny:1").new.name = "disappointed"; }],
  ["lens", "0.1's new.entries as a count (CM:97, CM:147)", (f) => { const u = by(f, "learning", "lu:43820:Gus:0"); u.new.entries = u.new.count; }],
  ["lens", "0.1's expectation initial (CM:93-94, CM:189)", (f) => { const r = rec(f, EXW); r.initial = { asOf: r.snapshots[0].asOf, ...r.snapshots[0].state }; }],
  ["lens", "0.1's association strength at the top level (CM:91)", (f) => { rec(f, "as:Penny:person:Pam:+:worried-care").strength = 0.05; }],
  ["lens", "0.1's record provenance at the top level (CM:100)", (f) => { const r = rec(f, "rd:penny-pam-care"); r.standing = r.observer.standing; r.sources = r.observer.sources; }],
  ["lens", "0.1's qualifications at the top level, shown to the character (CM:91)", (f) => { const r = f.lives.records.find((x) => x.observer?.qualifications); r.qualifications = r.observer.qualifications; }],
  ["lens", "0.1's recall details at the top level (CM:106)", (f) => { const r = by(f, "recalls", "rc:45190:Penny:0"); r.active = r.observer.active; r.bound = r.observer.bound; }],
  ["lens", "0.1's choice.provenance, the thought's link (CM:114-115)", (f) => { const d = dec(f, D1); d.choice.provenance = d.choice.observer.provenance; }],
  ["lens", "0.1's choice.backend and probabilities (CM:114)", (f) => { const d = dec(f, D1); d.choice.backend = d.choice.observer.backend; d.choice.probabilities = d.choice.observer.probabilities; }],
  ["lens", "0.1's attempt world at the top level (CM:128)", (f) => { const a = f.lives.attempts.find((x) => x.observer?.world); a.world = a.observer.world; }],
  ["lens", "0.1's attempted prose on an attempt", (f) => { by(f, "attempts", "att:45191:Penny:0").attempted = "asks Pam to stay in"; }],
  ["lens", "0.1's untilBasis on the shared plan's window", (f) => { const p = by(f, "plans", "plan:45192:Penny:Pam"); p.window.untilBasis = p.observer.windowBasis; }],
  ["lens", "0.1's perceived.observed prose (CJ:234)", (f) => { per(f, "per:45781:Penny:0").perceived.observed = "Pam not home from 18:01 to 19:01"; }],
  ["lens", "0.1's unknown.about as prose (CJ:235)", (f) => { per(f, "per:45781:Penny:0").unknown[0].about = "where Pam went"; }],
  ["lens", "0.1's perception rule at the top level", (f) => { const p = f.lives.perceptions.find((x) => x.observer?.rule); p.rule = p.observer.rule; }],
  ["lens", "0.1's outcome timeline label (CM:136)", (f) => { const o = by(f, "outcomes", "out:45720:Pam:0"); o.timeline[0].label = o.timeline[0].observer.label; }],
  ["lens", "0.1's settledLabel at the outcome's top level (CM:137)", (f) => { const o = by(f, "outcomes", "out:45720:Pam:0"); o.settledLabel = o.observer.settledLabel; }],
  ["lens", "0.1's thread summary at the top level (CM:173)", (f) => { const t = by(f, "threads", "thr:penny-pam-evening"); t.summary = t.observer.summary; }],
  ["lens", "a comparison question as 0.1's prose (CJ:272's heading)", (f) => { const c = by(f, "comparisons", "cmp:45190:Penny:0:without-p6"); c.question = c.observer.question; }],
  ["lens", "runChoice as 0.1's string", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p6").runChoice = "the run's own choice used Laya (offer, 0.46)"; }],
  ["lens", "an arm labelled as in 0.1 (CM:204)", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p6").arms[0].label = "with every history"; }],
  ["comparisons", "0.1's arm inputs and derived in place of consequences (CM:204-205)", (f) => { const a = by(f, "comparisons", "cmp:45190:Penny:0:without-p6").arms[1]; a.derived = a.consequences; delete a.consequences; }],
  ["comparisons", "0.1's heldConstant as a list of names", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-p4").heldConstant = ["worldState", "availableActions", "otherPersonalState", "randomInput", "method"]; }],
  ["comparisons", "a non-comparable case that still reports differs (CJ:273)", (f) => { by(f, "comparisons", "cmp:45190:Penny:0:without-commitment").differs = { reasons: true, probabilities: true, sampledAction: true }; }],
  ["rows", "a busy row carrying the Ask's margin, though nobody was asked", (f) => { row(f, "fav:43820:Gus:Penny:0").margin = 0.36; }],
  ["rows", "a busy row carrying the asked person's state", (f) => { Object.assign(row(f, "fav:43820:Gus:Penny:0"), { tired: 0.1, busy: 0, low: 0, bond: 0.4, eager: 0.5 }); }],
  ["pin", "0.1's archetype as a bare id", (f) => { f.lives.characters[0].archetype = f.lives.characters[0].archetype.id; }],
  ["lens", "0.1's lives.external (a path and archetype ids)", (f) => { f.lives.external = { archetypes: { source: "sim/character-inspiration.json characters[].archetypeId", ids: ["convivial-keeper"] } }; }],
  ["lens", "0.1's block prose lives.note outside lives.observer", (f) => { f.lives.note = f.lives.observer.note; }],
  ["lens", "0.1's settings note (disappointmentNote)", (f) => { f.lives.settings.disappointmentNote = "9.4's first guess, D x expected x invested"; }],
  ["lens", "0.1's settings flag (Lives: true)", (f) => { f.lives.settings.Lives = true; }],
  ["lens", "a prose note in lives.clock", (f) => { f.lives.clock.note = "minutes from midnight of day 0"; }],
  ["excerpt", "0.1's excerpt {day, season, weekday} in place of days[]", (f) => { Object.assign(f.lives.excerpt, { day: 31, season: "summer", weekday: "Thu" }); delete f.lives.excerpt.days; }],
  ["lens", "0.1's model family and name", (f) => { Object.assign(f.lives.models[0], { family: "laya", name: "typed-decisions" }); }],
  ["lens", "0.1's unresolved label and prose status", (f) => { const u = f.lives.unresolved[0]; u.label = u.observer.label; u.status = "open at excerpt end"; }],
  // ---- fix pass (the evening on summer 3-4; habitual bedtimes are habits, Sid 2026-10-14), 2026-10-14 ----
  // a habitual bedtime is an intention: nothing makes anyone asleep or unavailable from it; sleep is recorded (moves)
  ["sleep", "the habitual bedtime as an event that ends Penny's evening (goes-to-bed at 21:00)", (f) => { by(f, "outcomes", "out:45720:Pam:0").timeline.push({ at: 45900, event: "goes-to-bed" }); }],
  ["sleep", "Penny marked asleep at her habitual bedtime, with no recorded sleep", (f) => { by(f, "outcomes", "out:45720:Pam:0").world.spans.push({ who: PENNY, place: "asleep", from: 45900, to: 45900 }); }],
  ["sleep", "an alternative excluded because a habitual hour has passed (why past-bedtime)", (f) => { dec(f, D6).excluded.push({ id: "alt:45740:Pam:0/offer-penny", slug: "offer-penny", why: { code: "past-bedtime", cites: [] } }); }],
  ["sleep", "an availability entry 'asleep' with no recorded sleep", (f) => { by(f, "learning", "lu:43820:Gus:0").added.outcome = "asleep"; }],
  ["sleep", "a busy perception of someone 'sleeping' with no recorded sleep", (f) => { per(f, "per:43820:Gus:0").perceived.activity = "sleeping"; }],
  ["sleep", "an 'asleep' event before the moves stream's onset (the habit's 21:00, onset 21:30)", (f) => {
    f.moves = sleepMoves(f, 45930); by(f, "outcomes", "out:45720:Pam:0").timeline.push({ at: 45900, event: "asleep" }); }],
  // the calendar: an excerpt's days agree with the clock (day 0 is spring 1, a Monday) and cover its window
  ["excerpt", "excerpt.days naming day 31 a Saturday", (f) => { f.lives.excerpt.days[1].weekday = "Sat"; }],
  ["excerpt", "excerpt.days in the wrong season (day 31 is summer 4)", (f) => { f.lives.excerpt.days[1].season = "spring"; f.lives.excerpt.days[1].date = 4; }],
  ["excerpt", "excerpt.days left on spring 19-20 while the window is days 30-31", (f) => { f.lives.excerpt.days = [{ day: 18, season: "spring", date: 19, weekday: "Fri" }, { day: 19, season: "spring", date: 20, weekday: "Sat" }]; }],
];

// Legitimate shapes that must pass (beyond the fixture and the real-like variant).
const legit = [
  ["Balanced with two choices sharing a (kind, line) group: one pass per group", (f) => {
    const t = tape(f, "7315:opp:45191:Pam:0");
    t.request.choices.find((c) => c.id === "not-now").line = t.request.choices.find((c) => c.id === "no").line;
    t.answer.evaluations = [
      { pass: 1, prompt: t.answer.evaluations[0].prompt, response: t.answer.evaluations[0].response, labelsToChoiceIds: { o0: ["yes"], o1: ["no", "not-now"] }, weights: { yes: 0.75, "not-now": 0.125, no: 0.125 }, error: null },
      { pass: 2, prompt: t.answer.evaluations[1].prompt, response: t.answer.evaluations[1].response, labelsToChoiceIds: { o0: ["no", "not-now"], o1: ["yes"] }, weights: { yes: 0.69, "not-now": 0.155, no: 0.155 }, error: null }];
    t.answer.receipt.evaluations = t.answer.receipt.evaluations.slice(0, 2);
    t.answer.weights = { yes: 0.72, "not-now": 0.14, no: 0.14 };
    rehash(f, D2, t);
    const o = dec(f, D2).choice.observer;
    o.laya.probabilities = { "alt:45191:Pam:0/yes": 0.72, "alt:45191:Pam:0/not-now": 0.14, "alt:45191:Pam:0/no": 0.14 };
    o.probabilities = { ...o.laya.probabilities }; }],
];
const sharedGroup = legit[0][1];
const shapes = legitShapes(base); // [name, recording]: built from the real-like variant
const shapeByName = new Map(shapes);

function runChecker(f) {
  fs.writeFileSync(tmp, JSON.stringify(f));
  try { return { code: 0, out: execFileSync("node", [checker, tmp], { encoding: "utf8" }) }; } catch (e) { return { code: e.status, out: e.stdout }; }
}
let ok = 0, bad = 0;
for (const [label, f] of [["fixture", base], ["real-like variant", real], ...legit.map(([what, mutate]) => { const g = JSON.parse(JSON.stringify(base)); mutate(g); return [what, g]; }), ...shapes]) {
  const r = runChecker(f);
  console.log(`${r.code === 0 ? "passes " : "FAILS  "} ${label}`);
  if (r.code !== 0) { bad++; console.log(r.out.split("\n").slice(3, 10).join("\n")); }
}
const byCheck = {};
for (const [check, what, mutate, on] of cases) {
  if (on && on !== "real" && !shapeByName.has(on)) throw new Error("no legitimate shape named " + on);
  const f = JSON.parse(JSON.stringify(on === "real" ? real : on ? shapeByName.get(on) : base));
  mutate(f);
  const r = runChecker(f);
  const hit = r.code === 1 && r.out.includes(`[${check}]`);
  console.log(`${hit ? "caught " : "MISSED "} ${check.padEnd(12)} ${on === "real" ? "(real-like) " : on ? "(" + on.split(":")[0].split(" (")[0] + ") " : ""}${what}`);
  if (!hit) console.log(r.out.split("\n").slice(3, 8).join("\n"));
  else console.log("           " + r.out.split("\n").find((l) => l.includes(`[${check}]`)).trim().slice(0, 160));
  if (hit) ok++;
  byCheck[check] = (byCheck[check] || 0) + (hit ? 1 : 0);
}
try { fs.unlinkSync(tmp); } catch {}
// The per-viewer rule (contract 4.1-4.3) is load-bearing: a conforming reader shows no id outside the lens, while a
// reader that only hides observer.* does (an attempt's actor-only fields to its perceiver, other knowers' entries).
let readerOk = true;
for (const [label, f] of [["fixture", base], ["real-like variant", real]]) {
  const conforming = leaks(f), literal = leaks(f, { literal: true });
  const ok1 = conforming.length === 0, ok2 = literal.length > 0;
  readerOk = readerOk && ok1 && ok2;
  console.log(`${ok1 ? "passes " : "FAILS  "} reader (4.3) on the ${label}: ${conforming.length} id(s) outside the lens${ok1 ? "" : ": " + conforming.slice(0, 3).join("; ")}`);
  console.log(`${ok2 ? "caught " : "MISSED "} lens         a reader that only hides observer.* (${label}): ${literal.length} id(s) outside the lens, e.g. ${literal[0] ?? "none"}`);
}
console.log(`by check: ${Object.entries(byCheck).map(([k, v]) => `${k} ${v}`).join(", ")}`);
console.log(`${ok}/${cases.length} structure mutations caught; fixture, real-like variant and ${legit.length + shapes.length} legitimate shape(s) ${bad ? "DO NOT all pass" : "all pass"}`);
process.exit(ok === cases.length && !bad && readerOk ? 0 : 1);
