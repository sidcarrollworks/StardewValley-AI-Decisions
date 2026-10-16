// A minimal character-lens reader for contract v0.2, section 4.3, used to test that the per-viewer rule is load-bearing.
// view(run, who, t) returns every character-safe value a conforming reader shows person `who` at minute t: the elements
// that person can see (5.4), each through its per-viewer projection (4.2's last column: a perceiver of an attempt sees
// actor, target, kind and terms; a knower sees only their own knownBy entry), with the field gates of 6.1. With
// { literal: true } it is the non-conforming reader that only hides observer.* (4.1 read alone). leaks() lists every
// typed id such a reader shows that the person cannot see at that minute. Codex's adapter is the real reader; this is a
// reference for the rule, not a viewer. Usage: node reader-lens.cjs <recording.json>
"use strict";
const fs = require("fs");
const PRE = "pre-run";
const T = (v) => (v === PRE ? -1 : typeof v === "number" && Number.isFinite(v) ? v : Infinity);
const TABLES = ["records", "opportunities", "recalls", "decisions", "attempts", "plans", "perceptions", "learning"]; // outcomes, comparisons, threads: never
const REF = /^(h|acct|rd|cmt|as|ex|src|opp|rc|dec|alt|att|plan|out|per|lu|cmp|thr|act|fav|inv|need|reflection|question|model):\S+$/;
const isObj = (v) => v !== null && typeof v === "object" && !Array.isArray(v);

function indexOf(run) {
  const L = run.lives, idx = new Map(), put = (id, kind, obj) => idx.set(id, { kind, obj });
  for (const t of [...TABLES, "outcomes", "comparisons", "threads"]) for (const x of L[t] || []) put(x.id, t, x);
  for (const d of L.decisions || []) for (const a of [...(d.alternatives || []), ...(d.excluded || [])]) put(a.id, "alternative", { decision: d });
  for (const a of run.acts || []) put(`act:${a[0]}`, "act", a);
  for (const n of run.needs || []) put(`need:${n.id}`, "need", n);
  for (const r of [...(run.favours || []), ...(run.invitations || [])]) if (r.id) put(r.id, "row", r);
  for (const t of run.reflections || []) put(`reflection:${t.request.id}`, "reflection", t);
  for (const t of run.choiceQuestions || []) put(`question:${t.request.id}`, "question", t);
  for (const s of L.sources || []) put(s.id, "source", s);
  for (const m of L.models || []) put(m.id, "model", m);
  return idx;
}
// Contract 5.4: may person x see this id at minute t?
function sees(run, idx, x, id, t) {
  const e = idx.get(id); if (!e) return false;
  const o = e.obj, names = run.names;
  switch (e.kind) {
    case "records": case "plans": return T(o.visibleAt) <= t && (o.knownBy || []).some((k) => k.who === x && T(k.availableAt) <= t);
    case "attempts": return (o.actor === x && T(o.visibleAt) <= t) || (run.lives.perceptions || []).some((p) => p.of === id && p.who === x && T(p.visibleAt) <= t);
    case "opportunities": case "recalls": case "decisions": return o.holder === x && T(o.visibleAt) <= t;
    case "alternative": return o.decision.holder === x && T(o.decision.visibleAt) <= t;
    case "perceptions": case "learning": return o.who === x && T(o.visibleAt) <= t;
    case "act": return (o[2] === x || o[7] === x) && o[1] <= t;
    case "need": return names.indexOf(o.holder) === x && o.tick <= t;
    case "row": return o.tick <= t && names.indexOf(o.asked) === x && o.askMade !== false && o.answer !== "busy";
    case "reflection": return (run.lives.decisions || []).some((d) => d.holder === x && d.choice?.thought?.tape === id && T(d.choice.visibleAt) <= t);
    default: return false; // question tapes, outcomes, comparisons, threads, sources, models
  }
}
// What the reader shows: [{ where, value }] for every character-safe leaf value.
function view(run, who, t, { literal = false } = {}) {
  const idx = indexOf(run), out = [];
  const walk = (v, where) => {
    if (Array.isArray(v)) v.forEach((x, i) => walk(x, `${where}[${i}]`));
    else if (isObj(v)) { for (const [k, x] of Object.entries(v)) if (k !== "observer") walk(x, `${where}.${k}`); }
    else out.push({ where, value: v });
  };
  for (const tbl of TABLES) for (const x of run.lives[tbl] || []) {
    if (!sees(run, idx, who, x.id, t)) continue;
    const perceiver = tbl === "attempts" && x.actor !== who;
    for (const [k, v] of Object.entries(x)) {
      if (k === "observer" || k === "id") continue;
      if (!literal && perceiver && !["actor", "target", "kind", "terms", "visibleAt"].includes(k)) continue;
      if (k === "choice" && T(v?.visibleAt) > t) continue;
      if (k === "status" && T(x.resolvedAt) > t) continue;
      if (k === "knownBy") { for (const [i, e] of v.entries()) if ((literal || e.who === who) && T(e.availableAt) <= t) walk(e, `${x.id}.knownBy[${i}]`); continue; }
      if (k === "snapshots") { for (const [i, s] of v.entries()) if (T(s.asOf) <= t) walk(s, `${x.id}.snapshots[${i}]`); continue; }
      walk(v, `${x.id}.${k}`);
    }
  }
  return out;
}
function gateMinutes(run) {
  const m = new Set();
  const add = (v) => { if (typeof v === "number") m.add(v); };
  for (const tbl of TABLES) for (const x of run.lives[tbl] || []) {
    add(x.visibleAt); add(x.choice?.visibleAt); add(x.resolvedAt);
    for (const k of x.knownBy || []) add(k.availableAt);
    for (const s of x.snapshots || []) add(s.asOf);
  }
  add(run.lives.excerpt?.to);
  return [...m].sort((a, b) => a - b);
}
function leaks(run, opts = {}) {
  const idx = indexOf(run), found = new Map();
  for (let who = 0; who < run.names.length; who++) for (const t of gateMinutes(run)) for (const { where, value } of view(run, who, t, opts)) {
    if (typeof value !== "string" || !REF.test(value) || !idx.has(value) || sees(run, idx, who, value, t)) continue;
    const key = `${run.names[who]}: ${where} -> ${value}`;
    if (!found.has(key)) found.set(key, t);
  }
  return [...found].map(([k, t]) => `${k} (from ${t})`);
}
module.exports = { view, leaks, sees, indexOf };
if (require.main === module) {
  const file = process.argv[2];
  if (!file) { console.error("usage: node reader-lens.cjs <recording.json>"); process.exit(2); }
  const run = JSON.parse(fs.readFileSync(file, "utf8"));
  for (const literal of [false, true]) {
    const l = leaks(run, { literal });
    console.log(`${literal ? "literal reader (hides observer.* only)" : "conforming reader (contract 4.3)"}: ${l.length} id(s) shown outside the lens`);
    for (const x of l.slice(0, 12)) console.log("  " + x);
  }
}
