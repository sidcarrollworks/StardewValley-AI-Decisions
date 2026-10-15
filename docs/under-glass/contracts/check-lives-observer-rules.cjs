#!/usr/bin/env node
// Experimental rule checker for the Lives pilot, against contract v0.2 (lives-observer-recording-v0.2.md, sections 14 and 14.1).
// It recomputes the proposed rules' numbers from what a recording keeps: rule 5 and rule 7 weights (not now sized as
// the asked person's state: the Ask's state cost plus any Lives-only working term), fallback probabilities, base-row
// margins (the Ask's own), the keyed draw against Rng.Unit, Laya only on torn choices (a torn choice the fallback
// answers names a reason that holds: the cap, an authored control's missing evaluator, or a failed tape), warm and cold
// reasons against the associations they cite, seeds at settings.seedStrength (O2), stance-less readings giving no
// warmth, rule 9's chance and seeded entry, 9.4's disappointment found structurally and aimed at the hoped-for evening
// with mood moved once (O3), counting once (9.5), day types (a weekday or weekend key holds entries from such days, and
// a recall's day cue is its own day's), and each comparison arm's recomputation. These are first guesses under review: readers never require this
// checker, and a recording can be structurally valid and fail it. Run check-lives-observer-structure.cjs first.
// Dependency-free. Usage: node check-lives-observer-rules.cjs <file.json>
// Exit 0 when every rule check passes, 1 when any fails, 2 on usage or parse errors.
"use strict";
const fs = require("fs");
const path = require("path");
const { unit } = require(path.join(__dirname, "lives-observer-v0.2-tools", "rng.cjs"));

const file = process.argv[2];
if (!file || process.argv.length > 3) { console.error("usage: node check-lives-observer-rules.cjs <file.json>"); process.exit(2); }
let run;
try { run = JSON.parse(fs.readFileSync(file, "utf8")); } catch (e) { console.error(`cannot read ${file}: ${e.message}`); process.exit(2); }
const L = run.lives;
if (!L || L.version !== "0.2") { console.log(`checked ${file}`); console.log(`FAIL: [version] lives.version is ${JSON.stringify(L?.version)}; this checker reads exactly "0.2"`); process.exit(1); }

const errors = [];
const counts = {};
const fail = (check, msg) => errors.push(`[${check}] ${msg}`);
const tick = (check, n = 1) => { counts[check] = (counts[check] || 0) + n; };
const PRE = "pre-run";
const T = (v) => (v === PRE ? -1 : v);
const EPS = 2e-3;
const S = L.settings || {};
const K = S.k;
const names = run.names || [];
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const deepEqual = (a, b) => canon(a) === canon(b);
// Explicit number checks: a missing or non-numeric value never passes a comparison (NaN compares false both ways).
const near = (a, b, eps = EPS) => Number.isFinite(a) && Number.isFinite(b) && Math.abs(a - b) <= eps;
// The Ask's own parts (Simulation.Ask.cs:81-84, :88-92 [#90]): what a base row's margin records. Rule 11's displaced
// habit and rule 16's working term are Lives-only costs: they enter the decision's margin, never the row's.
const ASK_PARTS = ["base", "state", "errand"];
function pick(order, probs, u) { let x = u; for (const id of order) { x -= probs[id]; if (x < 0) return id; } return order[order.length - 1]; }
const byId = new Map();
for (const t of ["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons"]) for (const x of L[t] || []) byId.set(x.id, x);
const rowById = new Map([...(run.favours || []), ...(run.invitations || [])].map((r) => [r.id, r]));

// ---- rules 5 and 7: the fallback's weights from their components ----
function rule5(c) {
  const ch = c.chance ? c.chance.whereabouts * c.chance.answers * c.chance.followThrough : 1;
  return c.base * ch * Math.exp(K * (c.warm - c.cold * (1 - c.boldness) + c.intended - c.cost));
}
function rule7(d, a) {
  const c = a.components;
  const yes = (d.alternatives || []).find((x) => x.components?.answer === "yes")?.components;
  if (!yes) { fail("rule7", `${d.id}: a rule-7 answer set without yes`); return NaN; }
  if (c.answer === "yes") {
    const m = c.obliged - Object.values(c.costs || {}).reduce((s, x) => s + x, 0);
    if (!near(m, c.margin)) fail("rule7", `${a.id}: margin ${c.margin}, obliged ${c.obliged} less costs give ${m.toFixed(4)}`);
    return Math.exp(K * c.margin);
  }
  if (c.answer === "no") {
    if (!near(c.margin, yes.margin)) fail("rule7", `${a.id}: no's margin ${c.margin} is not yes's ${yes.margin}`);
    return Math.exp(-K * yes.margin);
  }
  if (c.answer === "not-now") {
    if (!(Number.isFinite(c.reason?.value) && c.reason.value > 0)) fail("rule7", `${a.id}: not now is eligible only while a reason holds (else excluded as no-reason-holds)`);
    const state = (yes.costs?.state ?? NaN) + (yes.costs?.working ?? 0);
    if (!near(c.reason?.value, state)) fail("rule7", `${a.id}: the not-now reason ${c.reason?.value} is not the asked person's state: the Ask's state cost ${yes.costs?.state} plus the Lives-only working term ${yes.costs?.working ?? 0}`);
    return Math.exp(K * c.reason.value);
  }
  fail("rule7", `${a.id}: unknown rule-7 answer ${c.answer}`);
  return NaN;
}
for (const d of L.decisions || []) {
  const o = d.choice?.observer || {}, fb = o.fallback || {};
  const missing = (d.alternatives || []).filter((a) => !a.components);
  if (missing.length) { fail("rule5", `${d.id}: ${missing.map((a) => a.slug)} carry no components: the rule cannot be checked`); continue; }
  const answers = d.alternatives.filter((a) => a.components.formula === "rule7").map((a) => a.components.answer);
  if (answers.length) {
    for (const need of ["yes", "no"]) if (!answers.includes(need)) fail("rule7", `${d.id}: an answer set without ${need} (Laya and the fallback answer the same three)`);
    if (!answers.includes("not-now") && !(d.excluded || []).some((x) => x.slug === "not-now" && x.why?.code === "no-reason-holds")) fail("rule7", `${d.id}: not now is neither eligible nor excluded as no-reason-holds`);
  }
  const ws = Object.fromEntries(d.alternatives.map((a) => {
    const f = a.components.formula;
    return [a.id, f === "rule5" ? rule5(a.components) : f === "rule7" ? rule7(d, a) : (fail("rule5", `${a.id}: unknown formula ${f}`), NaN)];
  }));
  const total = Object.values(ws).reduce((s, x) => s + x, 0);
  for (const a of d.alternatives) {
    const chk = a.components.formula === "rule7" ? "rule7" : "rule5";
    if (!near(ws[a.id], fb.weights?.[a.id], 1e-3 * Math.max(1, ws[a.id]))) fail(chk, `${a.id}: weight ${fb.weights?.[a.id]}, the components give ${ws[a.id]?.toFixed?.(4)}`);
    if (!near(ws[a.id] / total, fb.probabilities?.[a.id])) fail("fallback", `${a.id}: fallback probability ${fb.probabilities?.[a.id]}, the weights give ${(ws[a.id] / total).toFixed(4)}`);
  }
  for (const b of d.choice?.base || []) {
    const row = rowById.get(b), yes = d.alternatives.find((a) => a.components.answer === "yes")?.components;
    if (!row || !yes) continue;
    const askMargin = (yes.obliged ?? NaN) - ASK_PARTS.reduce((s, k) => s + (yes.costs?.[k] ?? 0), 0);
    if (!near(row.margin, askMargin)) fail("rule7", `${d.id}: ${b} records margin ${row.margin}; the Ask's own margin (obliged less ${ASK_PARTS.join(", ")}) is ${askMargin.toFixed(4)}`);
  }
  tick("rule5"); tick("fallback");
  // The keyed draw: every Lives choice uses Rng.Unit(seed, "lives-choice", decision id), whether or not a reflection opened.
  const s = o.sampled || {};
  if (s.stream !== "lives-choice" || !deepEqual(s.parts, [d.id])) fail("sampling", `${d.id}: the draw must be keyed by "lives-choice" and the decision id, not ${s.stream} ${JSON.stringify(s.parts)}`);
  else if (!near(unit(run.seed, s.stream, ...s.parts), s.unit, 1e-12)) fail("sampling", `${d.id}: unit ${s.unit} is not Rng.Unit(${run.seed}, ${s.stream}, ${d.id}) = ${unit(run.seed, s.stream, ...s.parts)}`);
  if (pick(s.order || [], o.probabilities || {}, s.unit) !== d.choice.selected) fail("sampling", `${d.id}: unit ${s.unit} selects ${pick(s.order || [], o.probabilities || {}, s.unit)}, recorded ${d.choice.selected}`);
  if (pick(s.order || [], fb.probabilities || {}, s.unit) !== fb.wouldSelect) fail("sampling", `${d.id}: the fallback would select ${pick(s.order || [], fb.probabilities || {}, s.unit)}, recorded ${fb.wouldSelect}`);
  tick("sampling");
  // Rule 6: Laya answers only torn choices. A torn choice the fallback answers says why, and the reason holds: the
  // person-day cap was reached, the run names no evaluator (an authored control), or the model failed (the named tape
  // records answer.fallback). A choice that is not torn is answered by the fallback as not-torn.
  const torn = d.observer?.torn?.value, why = o.fallbackReason;
  if (o.backend === "laya" && torn !== true) fail("torn", `${d.id}: Laya answered a choice that is not torn (rule 6)`);
  if (o.backend !== "laya") {
    if (torn !== true && why !== "not-torn") fail("torn", `${d.id}: not torn, so the fallback answers as not-torn, not ${why}`);
    if (torn === true) {
      const tape = [...(run.reflections || []).map((t) => [`reflection:${t.request?.id}`, t]), ...(run.choiceQuestions || []).map((t) => [`question:${t.request?.id}`, t])].find(([id]) => id === o.provenance?.tape)?.[1];
      const holds = why === "cap" ? !!o.cap?.reached : why === "no-evaluator" ? !L.run?.method?.evaluator : why === "model-failed" ? !!tape?.answer?.fallback : false;
      if (!holds) fail("torn", `${d.id}: torn, answered by the fallback, and no recorded reason holds (fallbackReason ${why}: cap reached, no evaluator in the run, or a failed tape)`);
    }
  }
  if (o.cap?.reached && (o.cap.used || []).length < o.cap.limit) fail("torn", `${d.id}: the cap is reached with ${(o.cap.used || []).length} of ${o.cap.limit} questions used`);
  tick("torn");
}

// ---- rule 9: chance = (successes + 1) / (entries + 1); the seeded entry never leaves ----
const OK = new Set(["found", "yes", "together", "free"]);
for (const r of (L.records || []).filter((x) => x.type === "expectation")) {
  for (const s of r.snapshots || []) {
    const e = s.state.entries || [], succ = e.filter((x) => OK.has(x.outcome)).length;
    if (s.state.successes !== succ) fail("rule9", `${r.id}: ${s.state.successes} successes recorded, the entries hold ${succ}`);
    if (!near((succ + 1) / (e.length + 1), s.state.chance)) fail("rule9", `${r.id}: chance ${s.state.chance} does not follow from ${succ} of ${e.length}`);
  }
  const seededBy = (r.snapshots || []).flatMap((s) => s.state.entries || []).find((x) => x.source === "Reading")?.ref;
  for (const u of (L.learning || []).filter((x) => x.record === r.id)) {
    for (const st of [u.previous, u.new]) if (!st || !near((st.successes + 1) / (st.count + 1), st.chance)) fail("rule9", `${u.id}: chance ${st?.chance} does not follow from ${st?.successes} of ${st?.count}`);
    const want = u.previous.successes + (OK.has(u.added?.outcome) ? 1 : 0) - (u.dropped && OK.has(u.dropped.outcome) ? 1 : 0);
    if (u.new.successes !== want) fail("rule9", `${u.id}: successes ${u.new.successes}, the added and dropped entries give ${want}`);
    if (u.dropped?.source === "Reading" || u.new.seeded !== u.previous.seeded) fail("rule9", `${u.id}: the seeded entry never leaves (rule 9)`);
    if (u.dropped && u.previous.lived < S.livedWindow) fail("rule9", `${u.id}: drops a lived entry before the window (${S.livedWindow}) is full`);
    if (seededBy && !(u.preserved || []).includes(seededBy)) fail("rule9", `${u.id}: revises what ${seededBy} seeded but does not list it as preserved`);
    tick("rule9");
  }
}
// ---- day types (contract 7; ours, E: Saturday and Sunday are the weekend; day 0 is a Monday) ----
// A key whose day type is weekday or weekend holds entries from such days only, and a recall's active day cue is the
// day type of its own minute.
const perDay = run.clock?.minutesPerDay ?? 1440, perWeek = run.clock?.daysPerWeek ?? 7;
const dayTypeOf = (m) => ((Math.floor(m / perDay) % perWeek) >= 5 ? "weekend" : "weekday");
for (const r of (L.records || []).filter((x) => x.type === "expectation")) {
  const want = /:(weekday|weekend)$/.exec(r.key || "")?.[1];
  if (!want) continue;
  const entries = [...(r.snapshots || []).flatMap((s) => s.state?.entries || []),
    ...(L.learning || []).filter((u) => u.record === r.id).flatMap((u) => [u.added, u.dropped]).filter(Boolean)];
  for (const e of entries) if (typeof e.at === "number" && dayTypeOf(e.at) !== want)
    fail("daytype", `${r.id}: an entry at ${e.at} (day ${Math.floor(e.at / perDay)}, a ${dayTypeOf(e.at)}) under a ${want} key`);
  tick("daytype");
}
for (const x of L.recalls || []) {
  const cue = (x.observer?.active || []).find((c) => /^day:(weekday|weekend)$/.test(c));
  if (!cue || typeof x.visibleAt !== "number") continue;
  if (cue !== `day:${dayTypeOf(x.visibleAt)}`) fail("daytype", `${x.id}: the active cue ${cue} on day ${Math.floor(x.visibleAt / perDay)}, a ${dayTypeOf(x.visibleAt)}`);
  tick("daytype");
}
// An expectation's chance as its holder held it at minute t: no record yet is no entries (1).
function chanceAt(id, t) {
  const r = byId.get(id);
  if (!r || r.type !== "expectation") return undefined;
  if (T(r.visibleAt) > t) return 1;
  const snaps = (r.snapshots || []).filter((s) => T(s.asOf) <= t);
  if (!snaps.length) return undefined;
  const last = snaps.at(-1);
  let c = last.state.chance;
  for (const u of (L.learning || []).filter((x) => x.record === id && T(x.visibleAt) <= t && T(x.visibleAt) >= T(last.asOf)).sort((a, b) => T(a.visibleAt) - T(b.visibleAt) || a.seq - b.seq)) c = u.new.chance;
  return c;
}
function strengthAt(id, t) {
  const r = byId.get(id);
  if (!r || r.type !== "association") return undefined;
  const snaps = (r.snapshots || []).filter((s) => T(s.asOf) <= t);
  if (!snaps.length) return undefined;
  let s = snaps.at(-1).state.strength;
  for (const u of (L.learning || []).filter((x) => x.record === id && T(x.visibleAt) <= t && T(x.visibleAt) >= T(snaps.at(-1).asOf))) s = u.new.strength;
  return s;
}

// ---- O2: seeds are modest, a side's value is its associations, a stance-less reading gives no warmth ----
for (const r of (L.records || []).filter((x) => x.type === "association" && (x.knownBy || []).some((k) => k.how === "Seeded"))) {
  const first = (r.snapshots || [])[0]?.state?.strength;
  if (!near(first, S.seedStrength)) fail("seeds", `${r.id}: seeded at ${first}, not settings.seedStrength ${S.seedStrength} (O2: present readings seed modestly)`);
  tick("seeds");
}
const isStanceless = (id) => { const r = byId.get(id); return r?.type === "reading" && r.stance === "none"; };
for (const d of L.decisions || []) for (const a of d.alternatives || []) {
  const sides = { warm: 0, cold: 0 };
  for (const r of a.reasons || []) {
    if ((r.kind === "supports" || r.code === "warm") && (r.cites || []).some(isStanceless))
      fail("sides", `${a.id}: the ${r.code} reason cites a stance-less reading (${(r.cites || []).filter(isStanceless)}): it may open an offer, never give warmth (O2)`);
    if (r.kind === "shown" || !["warm", "cold"].includes(r.code)) continue;
    const assocs = (r.cites || []).filter((id) => byId.get(id)?.type === "association");
    const sum = assocs.reduce((t, id) => t + (strengthAt(id, T(d.visibleAt)) ?? NaN), 0);
    if (!assocs.length) fail("sides", `${a.id}: the ${r.code} reason (${r.value}) cites no association: a side's value is the strength of the holder's own associations`);
    else if (!near(r.value, sum)) fail("sides", `${a.id}: the ${r.code} reason is ${r.value}; its associations [${assocs}] stood at ${sum.toFixed(4)} at ${d.visibleAt}`);
    sides[r.code] += r.value ?? NaN;
  }
  if (a.components?.formula === "rule5") for (const k of ["warm", "cold"]) if (!near(a.components[k], sides[k]))
    fail("sides", `${a.id}: components.${k} ${a.components[k]} is not the sum of its ${k} reasons (${sides[k]})`);
  tick("sides");
}

// ---- 9.4: disappointment = D x expected x invested, mood and one cold entry toward the hoped-for evening (O3) ----
// Found structurally, never by a label or basis: an association update whose evidence is a perception of an agreed
// evening missed (perceived.kind agreed-absent, of a plan), or a cold association caused by a plan. Each is THE
// disappointment: basis, mood, a situation with its cue keys, no interpretation, at most one per perception. A later
// interpretation aimed at the person rests on the holder's own supported interpretation (interpretation.supportedBy),
// never again on that perception, and moves no mood: the outcome counts once.
const agreedAbsent = (id) => { const p = byId.get(id); return p && p.perceived?.kind === "agreed-absent" && String(p.of || "").startsWith("plan:"); };
const disappointments = new Map(); // perception -> update
for (const u of L.learning || []) {
  const rec = byId.get(u.record);
  if (rec?.type !== "association") continue;
  const ev = (u.evidence || []).filter(agreedAbsent);
  const fromPlan = rec.sign === "-" && (rec.causes || []).some((c) => String(c).startsWith("plan:"));
  if (!ev.length && !fromPlan) continue;
  if (u.interpretation?.formed === true) {
    fail("disappointment", `${u.id}: an interpretation of ${rec.object} rests again on the missed evening (${ev.join(", ") || "a plan"}): the outcome counts once; it rests on the holder's own supported interpretation (supportedBy), never on that perception`);
    continue;
  }
  for (const p of ev) { if (disappointments.has(p)) fail("disappointment", `${u.id} and ${disappointments.get(p)} are both written from ${p}: one disappointment per missed evening (counted twice)`); else disappointments.set(p, u.id); }
  if (!rec.object?.startsWith("situation:") || !(rec.situation || []).length || rec.sign !== "-")
    fail("disappointment", `${u.id}: a disappointment is one cold entry toward the hoped-for evening, a situation with its cue keys (O3, decided), not ${rec.object}`);
  if (u.interpretation?.formed !== false) fail("disappointment", `${u.id}: no interpretation of the person is formed automatically (O3): interpretation.formed is false`);
  if (!Number.isFinite(u.mood?.change)) fail("disappointment", `${u.id}: a disappointment moves mood once (mood.change), and none is recorded`);
  const b = u.observer?.basis;
  if (!b) { fail("disappointment", `${u.id}: a disappointment without observer.basis: 9.4's formula cannot be checked`); tick("disappointment"); continue; }
  if (b.D !== S.disappointment) fail("disappointment", `${u.id}: D ${b.D} is not settings.disappointment ${S.disappointment}`);
  if (b.intended !== S.intended) fail("disappointment", `${u.id}: intended ${b.intended} is not settings.intended ${S.intended}`);
  if (b.investedScale !== S.disappointmentScale) fail("disappointment", `${u.id}: invested scale ${b.investedScale} is not settings.disappointmentScale ${S.disappointmentScale}`);
  for (const x of b.expectedTerms || []) {
    const r = byId.get(x.record);
    if (!r || r.holder !== u.who) fail("disappointment", `${u.id}: the ${x.term} term reads ${x.record}, not the holder's own expectation`);
    const c = chanceAt(x.record, T(b.expectedAt));
    if (!near(c, x.chance)) fail("disappointment", `${u.id}: the ${x.term} chance at the yes (${b.expectedAt}) is ${c} in ${x.record}, not ${x.chance}`);
  }
  const expected = (b.expectedTerms || []).reduce((q, x) => q * x.chance, 1);
  if (!(b.expectedTerms || []).length || !near(expected, b.expected)) fail("disappointment", `${u.id}: expected ${b.expected}, its terms give ${expected.toFixed(4)}`);
  const warm = byId.get(b.warmth?.record);
  const ws = strengthAt(b.warmth?.record, T(b.expectedAt));
  if (!warm || warm.holder !== u.who || warm.sign !== "+" || !near(ws, b.warmth?.strength)) fail("disappointment", `${u.id}: warmth ${b.warmth?.strength} is not the holder's own warm entry ${b.warmth?.record} as it stood at the yes`);
  const invested = (b.intended + (b.warmth?.strength ?? NaN)) / b.investedScale;
  if (!near(invested, b.invested)) fail("disappointment", `${u.id}: invested ${b.invested}, its terms give ${invested.toFixed(4)}`);
  const want = b.D * b.expected * b.invested;
  if (!near((u.new?.strength ?? NaN) - (u.previous?.strength ?? NaN), want)) fail("disappointment", `${u.id}: strength ${u.new?.strength} (from ${u.previous?.strength}), 9.4's formula gives a step of ${want.toFixed(4)}`);
  if (!near(u.mood?.change, -want)) fail("disappointment", `${u.id}: mood changes once, by ${(-want).toFixed(4)}, not ${u.mood?.change}`);
  tick("disappointment");
}
{ // mood moves once per outcome: one mood change per perception it rests on
  const seen = new Map();
  for (const u of L.learning || []) if (u.mood !== undefined) for (const ev of u.evidence || []) {
    if (seen.has(ev)) fail("disappointment", `${u.id} and ${seen.get(ev)} both move mood on ${ev}: counted twice`);
    seen.set(ev, u.id);
  }
}
// ---- 9.5: counting once. An agreed evening writes follow-through, never a whereabouts entry ----
for (const u of L.learning || []) {
  const rec = byId.get(u.record);
  if (rec?.type !== "expectation" || !rec.key?.startsWith("whereabouts")) continue;
  const fromPlan = (u.evidence || []).map((e) => byId.get(e)).filter((p) => p?.of?.startsWith?.("plan:"));
  if (fromPlan.length) fail("countOnce", `${u.id}: a whereabouts entry from ${fromPlan[0].id}, an agreed evening (9.5 writes follow-through only)`);
  tick("countOnce");
}

// ---- comparisons: each comparable arm recomputed from its components (rule 5), drawn with the held unit ----
for (const c of L.comparisons || []) {
  const d = byId.get(c.decision);
  if (!d) continue;
  const elig = c.heldConstant?.options?.eligible || [], u = c.heldConstant?.randomInput?.unit;
  for (const arm of c.arms || []) {
    const q = arm.consequences || {};
    if (!q.decisionExists) continue;
    if (!q.components) { fail("comparison", `${c.id}/${arm.id}: no components: the arm cannot be recomputed`); continue; }
    const ws = Object.fromEntries(elig.map((x) => [x, rule5(q.components[x])]));
    const tot = Object.values(ws).reduce((s, x) => s + x, 0);
    for (const x of elig) {
      if (!(Math.abs(ws[x] - (q.weights?.[x] ?? NaN)) <= 1e-3 * Math.max(1, ws[x]))) fail("comparison", `${c.id}/${arm.id}: weight for ${x} is ${q.weights?.[x]}, its components give ${ws[x].toFixed(4)}`);
      if (!(Math.abs(ws[x] / tot - (q.probabilities?.[x] ?? NaN)) <= EPS)) fail("comparison", `${c.id}/${arm.id}: probability for ${x} does not follow from its weights`);
      const dc = d.alternatives.find((a) => a.id === x)?.components || {}, ac = q.components[x] || {};
      for (const k of new Set([...Object.keys(dc), ...Object.keys(ac)])) {
        if (deepEqual(dc[k], ac[k])) continue;
        if (k === "chance" && deepEqual(ac.chance, q.chance)) continue;
        if ((k === "warm" || k === "cold") && ac[k] === q.sides?.[k]) continue;
        fail("comparison", `${c.id}/${arm.id}: ${x}'s ${k} differs from the decision's for no recorded consequence (chance or sides)`);
      }
    }
    if (pick(elig, q.probabilities || {}, u) !== q.sampled) fail("comparison", `${c.id}/${arm.id}: the held unit ${u} selects ${pick(elig, q.probabilities || {}, u)}, recorded ${q.sampled}`);
    tick("comparison");
  }
}

// ---- report ----
console.log(`checked ${file}`);
console.log(`lives ${L.version}${L.illustrative ? " (illustrative)" : ""}: pilot rules ${L.rules}; seed ${run.seed}`);
console.log(`rule checks run: ${Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(", ")}`);
if (errors.length) {
  console.log(`FAIL: ${errors.length} problem(s)`);
  for (const e of errors) console.log("  " + e);
  process.exit(1);
}
console.log("PASS: rules 5 and 7, fallback probabilities, keyed draws, rule 6 and the cap, seeds and sides (O2), rule 9, day types, disappointment (9.4, O3), counting once (9.5), comparison arms");
