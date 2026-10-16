#!/usr/bin/env node
// Checks a recording (or an illustrative fixture) against the proposed Lives observer contract v0.1
// (revised 2026-10-14 after its check). Dependency-free. Usage: node check-lives-observer.cjs <file.json>
// Exit code 0 when every check passes, 1 when any fails, 2 on usage or parse errors.
"use strict";
const fs = require("fs");

const file = process.argv[2];
if (!file) { console.error("usage: node check-lives-observer.cjs <file.json>"); process.exit(2); }
let run;
try { run = JSON.parse(fs.readFileSync(file, "utf8")); } catch (e) { console.error(`cannot read ${file}: ${e.message}`); process.exit(2); }

const errors = [];
const counts = {};
const fail = (check, msg) => errors.push(`[${check}] ${msg}`);
const tick = (check, n = 1) => { counts[check] = (counts[check] || 0) + n; };
const PRE = "pre-run";
const T = (v) => (v === PRE ? -1 : v); // pre-run sorts before minute 0
const EPS = 2e-3;

// ---- FNV-1a 64 over UTF-16 code units, one part, 0x1F separator (as Rng.Hash) ----
const M64 = (1n << 64n) - 1n;
function fnvHash(s) {
  let h = 14695981039346656037n;
  for (let i = 0; i < s.length; i++) { h ^= BigInt(s.charCodeAt(i)); h = (h * 1099511628211n) & M64; }
  h ^= 0x1Fn; h = (h * 1099511628211n) & M64;
  return "fnv1a64:" + h.toString(16).padStart(16, "0");
}
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const deepEqual = (a, b) => canon(a) === canon(b);
function pick(order, probs, unit) {
  let u = unit;
  for (const id of order) { u -= probs[id]; if (u < 0) return id; }
  return order[order.length - 1];
}
const slugify = (s) => String(s).toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
// The option set's hash (contract 7.2): eligible slugs with every reason (kind, code, value), excluded slugs with
// their reason code, constraints with their value. Built from a decision or from a tape request alike.
function setHash(eligible, excluded, constraints) {
  return fnvHash(canon({
    eligible: eligible.map((a) => ({ id: a.id, reasons: (a.reasons || []).map((r) => JSON.stringify([r.kind, r.code, r.value ?? null])).sort() }))
      .sort((x, y) => (x.id < y.id ? -1 : x.id > y.id ? 1 : 0)),
    excluded: excluded.map((x) => JSON.stringify([x.id, x.why])).sort(),
    constraints: constraints.map((c) => JSON.stringify([c.code, c.value ?? null])).sort(),
  }));
}
// A decision's own view, in the tape request's shape.
const viewOf = (d) => ({
  alternatives: (d.alternatives || []).map((a) => ({ id: a.slug, reasons: (a.reasons || []).map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null })) })),
  excluded: (d.excluded || []).map((x) => ({ id: x.slug, why: x.why?.code })),
  constraints: (d.constraints || []).map((c) => ({ code: c.code, value: c.value ?? null })),
});

// ---- 1. shape and labels ----
const L = run.lives;
if (run.format !== "under-glass-run") fail("shape", `format is ${run.format}, expected under-glass-run`);
if (run.version !== 1) fail("shape", `top-level version is ${run.version}; the lives block is additive and leaves it at 1`);
if (!L || typeof L !== "object") { console.error("no lives block: nothing to check"); process.exit(1); }
if (L.contract !== "under-glass-lives-observer") fail("shape", `lives.contract is ${L.contract}`);
if (L.version !== "0.1") fail("shape", `lives.version is ${L.version}; this checker reads 0.1`);
tick("shape");

const notImplemented = /not output from an implemented system/i;
if (run.illustrative === true || L.illustrative === true) {
  if (run.illustrative !== true) fail("illustrative", "top-level illustrative must be true when lives.illustrative is");
  if (L.illustrative !== true) fail("illustrative", "lives.illustrative must be true when the top level is");
  if (!notImplemented.test(run.note || "")) fail("illustrative", "top-level note must say it is not output from an implemented system");
  if (!notImplemented.test(L.note || "")) fail("illustrative", "lives.note must say it is not output from an implemented system");
  tick("illustrative");
} else if (!/^run:/.test(L.runId || "")) fail("illustrative", "neither illustrative nor a run id");

const names = run.names || [];
const S = L.settings || {};
const R = S.recall || {};
const K = S.k;
const WINDOW = S.livedWindow;
if (typeof WINDOW !== "number") fail("shape", "settings.livedWindow (lived entries an expectation keeps beside its seeded entry) is missing");

// ---- 2. the id index ----
const index = new Map(); // id -> { kind, obj, at }
function add(id, kind, obj, at) {
  if (typeof id !== "string") { fail("ids", `${kind} without a string id`); return; }
  if (index.has(id)) fail("ids", `duplicate id ${id}`);
  index.set(id, { kind, obj, at });
}
const prefixOf = { fact: "h", account: "acct", reading: "rd", commitment: "cmt", association: "as", expectation: "ex" };
for (const a of run.acts || []) add(`act:${a[0]}`, "act", a, a[1]);
for (const f of run.favours || []) add(`favour:${f.actId}`, "favour", f, f.tick);
for (const i of run.invitations || []) add(`invitation:${i.tick}:${i.asker}:${i.asked}`, "invitation", i, i.tick);
for (const n of run.needs || []) add(`need:${n.id}`, "need", n, n.tick);
for (const t of run.reflections || []) add(`reflection:${t.request.id}`, "reflection", t, t.request.tick);
for (const t of run.choiceQuestions || []) add(`question:${t.request.id}`, "question", t, t.request.tick);
for (const s of L.sources || []) add(s.id, "source", s, -1);
for (const m of L.models || []) add(m.id, "model", m, -1);
for (const r of L.records || []) {
  const p = prefixOf[r.type];
  if (!p) fail("ids", `record ${r.id} has unknown type ${r.type}`);
  else if (!r.id.startsWith(p + ":")) fail("ids", `record ${r.id} of type ${r.type} should start with ${p}:`);
  add(r.id, "record", r, T(r.visibleAt));
}
const table = { opportunities: "opp", recalls: "rc", decisions: "dec", attempts: "att", plans: "plan", outcomes: "out", perceptions: "per", learning: "lu", comparisons: "cmp", threads: "thr" };
for (const [k, p] of Object.entries(table)) for (const x of L[k] || []) {
  if (!x.id.startsWith(p + ":")) fail("ids", `${k} entry ${x.id} should start with ${p}:`);
  add(x.id, k, x, T(x.visibleAt));
}
for (const d of L.decisions || []) {
  for (const a of d.alternatives || []) add(a.id, "alternative", { ...a, decision: d.id, eligible: true }, T(d.visibleAt));
  for (const a of d.excluded || []) add(a.id, "alternative", { ...a, decision: d.id, eligible: false }, T(d.visibleAt));
}
tick("ids");

// ---- 3. every referenced id exists; people are valid indices ----
const REF = /^(h|acct|rd|cmt|as|ex|src|opp|rc|dec|alt|att|plan|out|per|lu|cmp|thr|act|favour|invitation|need|reflection|question|model):\S+$/;
let refs = 0;
function walk(v, path, key) {
  if (Array.isArray(v)) { v.forEach((x, i) => walk(x, `${path}[${i}]`, key)); return; }
  if (v && typeof v === "object") { for (const [k, x] of Object.entries(v)) walk(x, `${path}.${k}`, k); return; }
  if (typeof v === "string" && REF.test(v) && key !== "id") { refs++; if (!index.has(v)) fail("refs", `${path} -> ${v} does not resolve`); }
}
walk(L, "lives", null);
const PEOPLE_FIELDS = new Set(["who", "holder", "actor", "target", "asked", "subject", "about"]);
function walkPeople(v, path, key) {
  if (Array.isArray(v)) { v.forEach((x, i) => walkPeople(x, `${path}[${i}]`, key)); return; }
  if (v && typeof v === "object") { for (const [k, x] of Object.entries(v)) walkPeople(x, `${path}.${k}`, k); return; }
  const isPersonList = key === "participants" || key === "present" || key === "people";
  if ((PEOPLE_FIELDS.has(key) || isPersonList) && typeof v === "number" && !(Number.isInteger(v) && v >= 0 && v < names.length))
    fail("refs", `${path} = ${v} is not an index into names`);
}
walkPeople(L, "lives", null);
for (const c of L.characters || []) {
  if (names[c.who] !== c.name) fail("refs", `character ${c.who} is ${names[c.who]}, not ${c.name}`);
}
counts.refs = refs;

// ---- 4. id forms (contract 3): minute, the person's name, and that person's ordinal that minute ----
{
  const seen = new Map();
  const ordinal = (k) => { const n = seen.get(k) ?? 0; seen.set(k, n + 1); return n; };
  for (const [tbl, prefix, whoKey] of [["opportunities", "opp", "holder"], ["attempts", "att", "actor"], ["perceptions", "per", "who"], ["learning", "lu", "who"], ["outcomes", "out", "about"]]) {
    for (const x of L[tbl] || []) {
      const who = x[whoKey];
      if (!Number.isInteger(who)) { fail("ids", `${x.id}: no ${whoKey}`); continue; }
      const want = `${prefix}:${x.visibleAt}:${names[who]}:${ordinal(`${tbl}|${x.visibleAt}|${who}`)}`;
      if (x.id !== want) fail("ids", `${x.id} should be ${want} (minute, ${whoKey}'s name, that person's ordinal that minute)`);
      tick("ids");
    }
  }
  const oppById = new Map((L.opportunities || []).map((o) => [o.id, o]));
  for (const [tbl, prefix] of [["recalls", "rc"], ["decisions", "dec"]]) for (const x of L[tbl] || []) {
    const o = oppById.get(x.opportunity);
    if (!o) { fail("ids", `${x.id}: every ${tbl.slice(0, -1)} belongs to one opportunity`); continue; }
    if (x.id !== `${prefix}:${o.id.slice(4)}`) fail("ids", `${x.id} should take its opportunity's body: ${prefix}:${o.id.slice(4)}`);
    if (x.visibleAt !== o.visibleAt) fail("ids", `${x.id} is posed at ${x.visibleAt}, its opportunity at ${o.visibleAt}`);
    if (x.holder !== o.holder) fail("ids", `${x.id}: its holder is not its opportunity's`);
    tick("ids");
  }
  for (const d of L.decisions || []) for (const a of [...(d.alternatives || []), ...(d.excluded || [])])
    if (a.id !== `alt:${d.id.slice(4)}/${a.slug}`) fail("ids", `${a.id} should be alt:${d.id.slice(4)}/${a.slug}`);
  for (const p of L.plans || []) {
    const asker = (p.members || []).find((m) => m.role === "asker"), asked = (p.members || []).find((m) => m.role === "asked");
    const want = asker && asked ? `plan:${p.visibleAt}:${names[asker.who]}:${names[asked.who]}` : null;
    if (p.id !== want) fail("ids", `${p.id} should be ${want}`);
  }
  for (const c of L.comparisons || []) if (!c.id.startsWith(`cmp:${(c.decision || "").slice(4)}:`)) fail("ids", `${c.id} should start with cmp:${(c.decision || "").slice(4)}:`);
  for (const r of L.records || []) {
    if (r.type === "association" && r.id !== `as:${names[r.holder]}:${r.object}:${r.sign}:${slugify(r.name)}`)
      fail("ids", `${r.id} should be as:${names[r.holder]}:${r.object}:${r.sign}:${slugify(r.name)} (holder, object, sign and name: names are unique per holder and object)`);
    if (r.type === "expectation" && r.id !== `ex:${names[r.holder]}:${names[r.subject]}:${r.key}`)
      fail("ids", `${r.id} should be ex:${names[r.holder]}:${names[r.subject]}:${r.key}`);
  }
  for (const o of L.opportunities || []) if (o.reflection && o.reflection !== `reflection:${run.seed}:${o.id}`)
    fail("ids", `${o.id}: its reflection should be reflection:${run.seed}:${o.id}`);
  for (const d of L.decisions || []) { const t = d.choice?.provenance?.tape; if (t && t.startsWith("question:") && t !== `question:${run.seed}:q:${d.id.slice(4)}`)
    fail("ids", `${d.id}: its question should be question:${run.seed}:q:${d.id.slice(4)}`); }
}

// ---- visibility of anything by id (the observer's clock) ----
function vis(id) {
  const e = index.get(id);
  if (!e) return Infinity;
  if (e.kind === "decisions") return T(e.obj.choice ? e.obj.choice.visibleAt : e.obj.visibleAt);
  return e.at;
}

// ---- 5. the playback clock ----
// 5a. Nothing is visible before its causes.
function before(check, child, childAt, causes) {
  for (const c of causes.filter(Boolean)) {
    const at = vis(c);
    if (at > T(childAt)) fail(check, `${child} (visible ${childAt}) precedes its cause ${c} (visible ${at})`);
  }
  tick(check);
}
for (const x of L.recalls || []) before("clock", x.id, x.visibleAt, [x.opportunity]);
for (const x of L.opportunities || []) before("clock", x.id, x.visibleAt, x.causes || []);
for (const d of L.decisions || []) {
  before("clock", d.id, d.visibleAt, [d.opportunity, d.recall]);
  if (T(d.choice.visibleAt) < T(d.visibleAt)) fail("clock", `${d.id}: its choice is visible before the question was posed`);
}
for (const a of L.attempts || []) {
  before("clock", a.id, a.visibleAt, [a.decision, ...(a.causes || []), ...(a.base || [])]);
  if (a.resolvedAt === undefined) fail("clock", `${a.id}: an attempt needs resolvedAt (its status, world and outcome are shown from then)`);
  else if (T(a.resolvedAt) < T(a.visibleAt)) fail("clock", `${a.id} resolves before it is visible`);
}
for (const o of L.outcomes || []) {
  before("clock", o.id, o.visibleAt, [o.plan]);
  const first = Math.min(...(o.attempts || []).map(vis));
  if (first > T(o.visibleAt)) fail("clock", `${o.id} is visible before any of its attempts`);
  if (o.settledAt === undefined) fail("clock", `${o.id}: an outcome needs settledAt (its kind and world are shown from then)`);
  else if (T(o.settledAt) < T(o.visibleAt)) fail("clock", `${o.id} settles before it is visible`);
}
for (const p of L.plans || []) before("clock", p.id, p.visibleAt, [p.origin]);
for (const p of L.perceptions || []) before("clock", p.id, p.visibleAt, [p.of, ...(p.causes || [])]);
for (const u of L.learning || []) before("clock", u.id, u.visibleAt, [...(u.evidence || []), u.record]);
for (const c of L.comparisons || []) before("clock", c.id, c.visibleAt, [c.decision]);
for (const r of L.records || []) {
  before("clock", r.id, r.visibleAt, r.causes || []);
  for (const k of r.knownBy || []) if (T(k.availableAt) < T(r.visibleAt)) fail("clock", `${r.id}: known by ${k.who} before the record exists`);
}
for (const t of L.threads || []) {
  const ids = (t.stages || []).flatMap((s) => s.ids);
  const events = ids.map((id) => vis(id)).filter((x) => x >= 0);
  if (events.length && T(t.visibleAt) < Math.min(...events)) fail("clock", `${t.id} is visible before its first event`);
  for (const s of t.summary || []) if (T(s.visibleAt) < T(t.visibleAt)) fail("clock", `${t.id}: a summary line precedes the thread`);
  tick("clock");
}

// 5b. Nothing inside an element is later than the minute it is shown (contract 12). Every minute value and every
// referenced id must be at or before its gate: the element's visibleAt, unless a declared time-gated field says
// otherwise. Terms (a `template`, a `window`) name intended future minutes and are exempt.
const MINUTE_KEYS = new Set(["visibleAt", "availableAt", "asOf", "settledAt", "resolvedAt", "at", "from", "to", "until", "arrived", "waitedUntil", "firstSeen", "tick", "minute", "since"]);
const TERMS = new Set(["template", "window"]);
const GATED = {
  decisions: (x) => ({ choice: x.choice?.visibleAt }),
  attempts: (x) => ({ resolvedAt: x.resolvedAt, status: x.resolvedAt, world: x.resolvedAt, outcome: x.resolvedAt }),
  outcomes: (x) => ({ settledAt: x.settledAt, kind: x.settledAt, world: x.settledAt, mustNotWrite: x.settledAt, determinedBy: x.settledAt, settledLabel: x.settledAt, base: x.settledAt }),
  records: (x) => (x.type === "expectation" ? { initial: x.initial?.asOf } : {}),
};
function gateWalk(v, gate, path, key, where) {
  if (Array.isArray(v)) { v.forEach((x, i) => gateWalk(x, gate, `${path}[${i}]`, key, where)); return; }
  if (v && typeof v === "object") {
    for (const [k, x] of Object.entries(v)) if (!TERMS.has(k)) gateWalk(x, gate, `${path}.${k}`, k, where);
    return;
  }
  if (MINUTE_KEYS.has(key) && (typeof v === "number" || v === PRE) && T(v) > gate) fail("clock", `${where}: ${path} = ${v} is later than the minute it is shown (${gate})`);
  if (key === "day" && typeof v === "number" && (gate < 0 || v > Math.floor(gate / 1440))) fail("clock", `${where}: ${path} = day ${v} is later than the minute it is shown (${gate})`);
  if (typeof v === "string" && key !== "id" && REF.test(v) && index.has(v) && vis(v) > gate) fail("clock", `${where}: ${path} -> ${v} (visible ${vis(v)}) is later than the minute it is shown (${gate})`);
}
function gateElement(x, tbl) {
  const base = T(x.visibleAt);
  const gates = GATED[tbl] ? GATED[tbl](x) : {};
  for (const [k, v] of Object.entries(x)) {
    if (k === "id" || TERMS.has(k)) continue;
    if (k === "knownBy") { for (const kn of v) gateWalk(kn, Math.max(base, T(kn.availableAt)), `${x.id}.knownBy`, null, x.id); continue; }
    if (k === "timeline") {
      let last = base;
      for (const [i, e] of v.entries()) {
        if (T(e.at) < last) fail("clock", `${x.id}: timeline entry ${i} at ${e.at} is out of order or before the element`);
        last = T(e.at);
        gateWalk(e, T(e.at), `${x.id}.timeline[${i}]`, null, x.id);
      }
      continue;
    }
    if (tbl === "threads" && k === "stages") continue; // an index of ids: the viewer shows each from its own minute
    if (tbl === "threads" && k === "summary") { for (const s of v) gateWalk(s, T(s.visibleAt), `${x.id}.summary`, null, x.id); continue; }
    let gate = base;
    if (k in gates) {
      if (gates[k] === undefined) { fail("clock", `${x.id}: ${k} is time-gated but its gate minute is missing`); continue; }
      gate = T(gates[k]);
    }
    gateWalk(v, gate, `${x.id}.${k}`, k, x.id);
  }
  tick("clock");
}
for (const tbl of ["opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons", "threads"])
  for (const x of L[tbl] || []) gateElement(x, tbl);
for (const r of L.records || []) gateElement(r, "records");
for (const u of L.unresolved || []) {
  if (u.asOf === undefined) fail("clock", `unresolved ${u.ref}: needs asOf`);
  else gateWalk(u, T(u.asOf), `unresolved ${u.ref}`, null, `unresolved ${u.ref}`);
}

// ---- 6. knowledge: a character uses only what they could know, when they could know it ----
function knows(who, id, at) {
  const e = index.get(id);
  if (!e) return false;
  const o = e.obj;
  switch (e.kind) {
    case "record": case "plans": return (o.knownBy || []).some((k) => k.who === who && T(k.availableAt) <= at);
    case "act": return (o[2] === who || o[7] === who) && o[1] <= at;
    case "favour": case "invitation": return (names.indexOf(o.asker) === who || names.indexOf(o.asked) === who) && o.tick <= at;
    case "need": return names.indexOf(o.holder) === who && o.tick <= at;
    case "attempts": return (o.actor === who || o.target === who) && T(o.visibleAt) <= at;
    case "perceptions": return o.who === who && T(o.visibleAt) <= at;
    case "decisions": case "recalls": case "opportunities": return o.holder === who && T(o.visibleAt) <= at;
    default: return false; // outcomes are observer-only; sources and models are provenance, never knowledge
  }
}
function uses(check, who, at, ids, where) {
  for (const id of ids || []) {
    if (!knows(who, id, T(at))) fail(check, `${where}: ${names[who]} uses ${id} at ${at} without knowing it then`);
    tick(check);
  }
}
for (const x of L.recalls || []) uses("knowledge", x.holder, x.visibleAt, (x.items || []).map((i) => i.record), x.id);
for (const x of L.opportunities || []) uses("knowledge", x.holder, x.visibleAt, x.causes, x.id);
for (const d of L.decisions || []) {
  for (const a of d.alternatives || []) for (const r of a.reasons || []) uses("knowledge", d.holder, d.visibleAt, r.cites, `${a.id} reason ${r.code}`);
  for (const a of d.excluded || []) uses("knowledge", d.holder, d.visibleAt, (a.why?.cites || []).filter((c) => !c.startsWith("src:")), `${a.id} exclusion`);
  for (const c of d.constraints || []) uses("knowledge", d.holder, d.visibleAt, (c.cites || []).filter((x) => !x.startsWith("src:")), `${d.id} constraint ${c.code}`);
}
for (const p of L.perceptions || []) {
  uses("knowledge", p.who, p.visibleAt, [p.of, ...(p.causes || [])].filter(Boolean), p.id);
  for (const inf of p.inferences || []) {
    uses("knowledge", p.who, p.visibleAt, inf.basis, `${p.id} inference`);
    if (inf.establishes !== false) fail("knowledge", `${p.id}: an inference must not establish a fact`);
  }
  for (const u of p.unknown || []) if (u.status !== "unknown") fail("knowledge", `${p.id}: an unknown is marked ${u.status}`);
}
for (const u of L.learning || []) {
  const rec = index.get(u.record)?.obj;
  if (!rec || !["association", "expectation"].includes(rec.type)) fail("knowledge", `${u.id}: learning changes ${u.record}, not an association or expectation`);
  else if (rec.holder !== u.who) fail("knowledge", `${u.id}: ${names[u.who]} changes ${names[rec.holder]}'s record`);
  if (!(u.evidence || []).length) fail("knowledge", `${u.id}: no evidence`);
  for (const ev of u.evidence || []) {
    const p = index.get(ev)?.obj;
    if (!ev.startsWith("per:") || !p || p.who !== u.who) fail("knowledge", `${u.id}: evidence ${ev} is not ${names[u.who]}'s own perception`);
    tick("knowledge");
  }
  for (const id of u.preserved || []) if (!index.has(id)) fail("knowledge", `${u.id}: preserved ${id} is missing`);
}
for (const o of L.outcomes || []) {
  if (o.observerOnly !== true) fail("knowledge", `${o.id}: world outcomes are observer-only`);
  if (o.knownBy) fail("knowledge", `${o.id}: a world outcome has no knowers; characters hold perceptions`);
}
// 6b. The character lens (contract 12): everything a shared record or plan holds must be known to each knower
// from when they know it; an entry inside one knower's knownBy is checked for that knower only.
const PROVENANCE_KEYS = new Set(["id", "sources", "support", "source", "provenance", "observerLinks", "knownBy"]);
function idsIn(v, out = []) {
  if (Array.isArray(v)) v.forEach((x) => idsIn(x, out));
  else if (v && typeof v === "object") for (const [k, x] of Object.entries(v)) { if (!PROVENANCE_KEYS.has(k)) idsIn(x, out); }
  else if (typeof v === "string" && REF.test(v) && !/^(src|model):/.test(v)) out.push(v);
  return out;
}
for (const x of [...(L.records || []), ...(L.plans || [])]) {
  const shared = idsIn(x);
  for (const k of x.knownBy || []) {
    const at = Math.max(T(k.availableAt), T(x.visibleAt));
    for (const id of shared) if (id !== x.id && !knows(k.who, id, at)) fail("knowledge", `${x.id}: known by ${names[k.who]}, yet it holds ${id}, which ${names[k.who]} does not know at ${k.availableAt} (the character lens would leak it)`);
    for (const id of idsIn(k)) if (!knows(k.who, id, at)) fail("knowledge", `${x.id}: ${names[k.who]}'s own entry holds ${id}, unknown to them`);
    tick("knowledge");
  }
}
// 6c. Who knows how (contract 4): ProductDecision is Sid's decision (A); Authored is our interpretation (E).
const HOW = new Set(["CanonStated", "ProductDecision", "Authored", "Seeded", "Lived", "Witnessed", "Told", "Inferred", "Unknown"]);
for (const r of [...(L.records || []), ...(L.plans || [])]) {
  for (const k of r.knownBy || []) {
    if (!HOW.has(k.how)) fail("knowledge", `${r.id}: how ${k.how} is not a known value`);
    if (k.how === "ProductDecision" && r.standing !== "A") fail("knowledge", `${r.id}: known as a product decision but its standing is ${r.standing}, not A`);
    if (k.how === "Authored" && !/^E/.test(r.standing || "")) fail("knowledge", `${r.id}: 'Authored' marks our interpretation (E), but its standing is ${r.standing}`);
    if (k.how === "Told" && k.teller === undefined) fail("knowledge", `${r.id}: told, with no teller`);
  }
  if (r.origin === "product-decision" && r.standing !== "A") fail("knowledge", `${r.id}: origin product-decision needs standing A`);
  if (r.origin === "authored" && !/^E/.test(r.standing || "")) fail("knowledge", `${r.id}: origin authored needs standing E`);
  if (["authored-decision", "editorial"].includes(r.origin)) fail("knowledge", `${r.id}: origin ${r.origin} was renamed (product-decision for A, authored for E)`);
}

// ---- 7. expectations keep their seeded entry beside at most livedWindow lived ones (rule 9) ----
const OK = ["found", "yes", "together", "free"];
const seededBy = new Map();
for (const r of L.records || []) for (const s of r.seeds || []) seededBy.set(s, r.id);
function stateCheck(where, st) {
  if (!st || st.entries === undefined) return;
  if (st.lived === undefined || st.seeded === undefined) { fail("bounds", `${where}: an expectation state names its lived and seeded entries`); return; }
  if (st.entries !== st.lived + st.seeded) fail("bounds", `${where}: ${st.entries} entries are not ${st.lived} lived + ${st.seeded} seeded`);
  if (st.lived > WINDOW) fail("bounds", `${where}: ${st.lived} lived entries, more than the window of ${WINDOW}`);
  if (st.seeded > 1) fail("bounds", `${where}: more than one seeded entry`);
  if (st.found !== undefined && st.chance !== undefined && Math.abs((st.found + 1) / (st.entries + 1) - st.chance) > EPS)
    fail("knowledge", `${where}: chance ${st.chance} does not follow from ${st.found} of ${st.entries}`);
}
const lastState = new Map();
for (const r of L.records || []) {
  if (r.type !== "expectation") continue;
  const e = r.initial?.entries || [];
  const lived = e.filter((x) => x.source !== "Reading").length, seeded = e.length - lived, found = e.filter((x) => OK.includes(x.outcome)).length;
  if (r.initial?.lived !== lived || r.initial?.seeded !== seeded) fail("bounds", `${r.id}: initial says ${r.initial?.lived} lived and ${r.initial?.seeded} seeded; its entries hold ${lived} and ${seeded}`);
  stateCheck(r.id, { entries: e.length, lived, seeded, found, chance: r.initial?.chance });
  if (seededBy.has(r.id) && seeded !== 1) fail("bounds", `${r.id}: seeded by ${seededBy.get(r.id)}, so its seeded entry must stay (rule 9)`);
  lastState.set(r.id, { entries: e.length, seeded, chance: r.initial?.chance });
  tick("bounds");
}
for (const u of L.learning || []) {
  const rec = index.get(u.record)?.obj;
  if (!rec || rec.type !== "expectation") continue;
  stateCheck(`${u.id} previous`, u.previous);
  stateCheck(`${u.id} new`, u.new);
  const last = lastState.get(rec.id);
  if (last && u.previous && (u.previous.entries !== last.entries || Math.abs(u.previous.chance - last.chance) > EPS))
    fail("bounds", `${u.id}: previous state (${u.previous.entries}, ${u.previous.chance}) is not the record's last (${last.entries}, ${last.chance})`);
  if (u.previous && u.new && u.new.seeded !== u.previous.seeded) fail("bounds", `${u.id}: the seeded entry never leaves (rule 9)`);
  if (u.new?.dropped?.source === "Reading") fail("bounds", `${u.id}: drops the seeded entry (rule 9)`);
  if (seededBy.has(rec.id) && !(u.preserved || []).includes(seededBy.get(rec.id))) fail("bounds", `${u.id}: revises what ${seededBy.get(rec.id)} seeded but does not list it as preserved`);
  if (u.new) lastState.set(rec.id, { entries: u.new.entries, seeded: u.new.seeded, chance: u.new.chance });
  tick("bounds");
}
// An expectation's chance as its holder held it at minute t: no record yet is no entries (1); then the
// start state and each update visible by t.
function chanceAt(id, t) {
  const r = index.get(id)?.obj;
  if (!r || r.type !== "expectation") return undefined;
  if (T(r.visibleAt) > t) return 1;
  let c = r.initial?.chance ?? 1;
  for (const u of (L.learning || []).filter((x) => x.record === id && T(x.visibleAt) <= t).sort((a, b) => T(a.visibleAt) - T(b.visibleAt)))
    c = u.new?.chance ?? c;
  return c;
}
// Disappointment (9.4, proposed): D x expected (the plan's chance as the holder held it at the yes) x invested
// ((intended + the holder's warmth toward the person) / scale), aimed at the hoped-for evening, a situation.
for (const u of L.learning || []) {
  const b = u.basis;
  if (!b || b.D === undefined) continue;
  if (b.D !== S.disappointment) fail("knowledge", `${u.id}: D ${b.D} is not settings.disappointment ${S.disappointment}`);
  if (b.intended !== S.intended) fail("knowledge", `${u.id}: intended ${b.intended} is not settings.intended ${S.intended}`);
  if (b.investedScale !== S.disappointmentScale) fail("knowledge", `${u.id}: invested scale ${b.investedScale} is not settings.disappointmentScale ${S.disappointmentScale}`);
  for (const x of b.expectedTerms || []) {
    const rec = index.get(x.record)?.obj;
    if (!rec || rec.holder !== u.who) fail("knowledge", `${u.id}: the ${x.term} term reads ${x.record}, not the holder's own expectation`);
    const c = chanceAt(x.record, T(b.expectedAt));
    if (c === undefined || Math.abs(c - x.chance) > EPS) fail("knowledge", `${u.id}: ${x.term} chance at the yes is ${c} in ${x.record}, not ${x.chance}`);
  }
  const expected = (b.expectedTerms || []).reduce((q, x) => q * x.chance, 1);
  if (!(b.expectedTerms || []).length || Math.abs(expected - b.expected) > EPS) fail("knowledge", `${u.id}: expected ${b.expected}, its terms give ${expected.toFixed(4)}`);
  const warm = index.get(b.warmth?.record)?.obj;
  if (!warm || warm.holder !== u.who || warm.sign !== "+" || Math.abs(warm.strength - b.warmth.strength) > EPS)
    fail("knowledge", `${u.id}: warmth ${b.warmth?.strength} is not the holder's own warm entry ${b.warmth?.record}`);
  const invested = (b.intended + (b.warmth?.strength ?? NaN)) / b.investedScale;
  if (Math.abs(invested - b.invested) > EPS) fail("knowledge", `${u.id}: invested ${b.invested}, its terms give ${invested.toFixed(4)}`);
  const want = b.D * b.expected * b.invested;
  if (Math.abs((u.new?.strength ?? NaN) - want) > EPS) fail("knowledge", `${u.id}: strength ${u.new?.strength}, 9.4's formula gives ${want.toFixed(4)}`);
  const target = index.get(u.record)?.obj;
  if (!target?.object?.startsWith("situation:") || !(target.situation || []).length)
    fail("knowledge", `${u.id}: a disappointment is aimed at the hoped-for evening, a situation with its cue keys (9.4; O3 as the assessment leans), not ${target?.object}`);
  tick("knowledge");
}
// Counting once (9.5): an agreed evening writes one follow-through entry and one disappointment, never a
// whereabouts entry (the plan, not habit, set where each was to be).
for (const u of L.learning || []) {
  const rec = index.get(u.record)?.obj;
  if (rec?.type !== "expectation" || !rec.key?.startsWith("whereabouts")) continue;
  const fromPlan = (u.evidence || []).map((e) => index.get(e)?.obj).filter((p) => p?.of?.startsWith?.("plan:"));
  if (fromPlan.length) fail("knowledge", `${u.id}: a whereabouts entry from ${fromPlan[0].id}, an agreed evening (9.5 writes follow-through only)`);
  tick("knowledge");
}

// ---- 8. recall is bounded and holds ids, not copies ----
const ITEM_KEYS = new Set(["record", "slot", "matched", "score", "why", "cueOrigin"]);
for (const x of L.recalls || []) {
  const items = x.items || [];
  const max = Math.min(x.bound?.max ?? Infinity, R.max ?? Infinity);
  if (items.length > max) fail("bounds", `${x.id}: ${items.length} recalled, bound ${max}`);
  const top = items.filter((i) => i.slot === "top").length;
  const low = items.filter((i) => i.slot === "low-mood").length;
  if (top > (R.top ?? Infinity)) fail("bounds", `${x.id}: ${top} top slots`);
  if (low > (x.bound?.lowMood ? R.lowMoodExtra ?? 0 : 0)) fail("bounds", `${x.id}: a low-mood slot without low mood`);
  if ((x.runnersUp || []).length > (R.runnersUpShown ?? Infinity)) fail("bounds", `${x.id}: too many runners-up shown`);
  for (const i of items) {
    for (const k of Object.keys(i)) if (!ITEM_KEYS.has(k)) fail("bounds", `${x.id}: recalled item carries ${k}; recall holds ids and cues, not copies`);
    if (i.why && i.why.length > 120) fail("bounds", `${x.id}: a long reason in a recalled item`);
  }
  tick("bounds");
}
for (const t of [...(run.reflections || []), ...(run.choiceQuestions || [])]) {
  const rec = t.request.recalled || [];
  if (rec.length > (R.max ?? Infinity)) fail("bounds", `tape ${t.request.id}: recalled list longer than the bound`);
  for (const r of rec) if (Object.keys(r).some((k) => !["id", "origin"].includes(k))) fail("bounds", `tape ${t.request.id}: recalled entries carry more than id and origin`);
}

// ---- 9. decisions: one option set for both methods; weights follow the stated formulas ----
function rule5(c) {
  const ch = c.chance ? c.chance.whereabouts * c.chance.answers * c.chance.followThrough : 1;
  return c.base * ch * Math.exp(K * (c.warm - c.cold * (1 - c.boldness) + c.intended - c.cost));
}
// Rule 7 (contract 7.1): yes e^(k x margin), margin = obliged - the sum of costs; no e^(-k x margin); not now
// e^(k x reason) while a reason holds, the reason sized as the Ask's own state cost.
function rule7(d, a) {
  const c = a.components;
  const yes = (d.alternatives || []).find((x) => x.components?.answer === "yes")?.components;
  if (!yes) { fail("alternatives", `${d.id}: a rule-7 answer set without yes`); return NaN; }
  if (c.answer === "yes") {
    const m = c.obliged - Object.values(c.costs || {}).reduce((s, x) => s + x, 0);
    if (Math.abs(m - c.margin) > EPS) fail("alternatives", `${a.id}: margin ${c.margin}, obliged ${c.obliged} less costs give ${m.toFixed(4)}`);
    return Math.exp(K * m);
  }
  if (c.answer === "no") {
    if (Math.abs(c.margin - yes.margin) > EPS) fail("alternatives", `${a.id}: no's margin ${c.margin} is not yes's ${yes.margin}`);
    return Math.exp(-K * yes.margin);
  }
  if (c.answer === "not-now") {
    if (!(c.reason?.value > 0)) fail("alternatives", `${a.id}: not now is eligible only while a reason holds`);
    if (Math.abs((c.reason?.value ?? NaN) - (yes.costs?.state ?? NaN)) > EPS) fail("alternatives", `${a.id}: the reason ${c.reason?.value} is not the Ask's state cost ${yes.costs?.state}`);
    return Math.exp(K * c.reason.value);
  }
  fail("alternatives", `${a.id}: unknown rule-7 answer ${c.answer}`);
  return NaN;
}
const sameSet = (a, b) => a.length === b.length && [...a].sort().join("\n") === [...b].sort().join("\n");
const layaPerDay = new Map();
const EVAL_MODES = new Set(["Raw", "Canonical", "Balanced"]);
function tapeAgrees(d, tape) {
  const c = d.choice, view = viewOf(d);
  const req = tape.request;
  if (fnvHash(canon(req)) !== c.provenance.requestHash) fail("provenance", `${d.id}: requestHash does not match the tape's request`);
  if (req.tick !== d.visibleAt) fail("provenance", `${d.id}: the tape's request is at ${req.tick}`);
  if (!deepEqual(req.alternatives, view.alternatives)) fail("provenance", `${d.id}: the tape's alternatives or reasons differ from the decision's`);
  if (!deepEqual(req.excluded, view.excluded)) fail("provenance", `${d.id}: the tape's exclusions differ from the decision's`);
  if (!deepEqual(req.constraints, view.constraints)) fail("provenance", `${d.id}: the tape's constraints differ from the decision's`);
  if (req.alternativeSetHash !== c.provenance.alternativeSetHash) fail("provenance", `${d.id}: the tape saw a different option set`);
  if (setHash(req.alternatives || [], req.excluded || [], req.constraints || []) !== req.alternativeSetHash) fail("provenance", `${d.id}: the tape's alternativeSetHash does not match its own content`);
  const slugOf = Object.fromEntries((d.alternatives || []).map((a) => [a.slug, a.id]));
  if (slugOf[tape.choice] !== c.selected) fail("provenance", `${d.id}: the tape chose ${tape.choice}`);
  const recalled = (req.recalled || []).map((r) => r.id);
  const rc = index.get(d.recall)?.obj;
  if (rc && !sameSet(recalled, (rc.items || []).map((i) => i.record))) fail("provenance", `${d.id}: the tape's recalled ids differ from the recall`);
  return slugOf;
}
for (const d of L.decisions || []) {
  const elig = (d.alternatives || []).map((a) => a.id);
  const excl = (d.excluded || []).map((a) => a.id);
  if (!elig.length) fail("alternatives", `${d.id}: no eligible alternative`);
  if (excl.some((x) => elig.includes(x))) fail("alternatives", `${d.id}: an alternative both eligible and excluded`);
  for (const a of d.excluded || []) if (!a.why || !a.why.code) fail("alternatives", `${d.id}: ${a.id} excluded without a reason`);
  for (const a of d.alternatives || []) if (!(a.reasons || []).length) fail("alternatives", `${d.id}: ${a.id} has no reasons`);
  const answers = (d.alternatives || []).filter((a) => a.components?.formula === "rule7").map((a) => a.components.answer);
  if (answers.length) {
    for (const need of ["yes", "no"]) if (!answers.includes(need)) fail("alternatives", `${d.id}: an answer set without ${need}`);
    if (!answers.includes("not-now") && !(d.excluded || []).some((x) => x.slug === "not-now" && x.why?.code === "no-reason-holds"))
      fail("alternatives", `${d.id}: not now is neither eligible nor excluded as no-reason-holds`);
  }
  const c = d.choice;
  const fb = c.fallback?.probabilities || {};
  if (!sameSet(Object.keys(fb), elig)) fail("alternatives", `${d.id}: the fallback's alternatives differ from the eligible set`);
  if (c.backend === "laya" && !sameSet(Object.keys(c.laya?.probabilities || {}), elig)) fail("alternatives", `${d.id}: Laya's alternatives differ from the fallback's`);
  if (!sameSet(Object.keys(c.probabilities || {}), elig)) fail("alternatives", `${d.id}: the used probabilities cover other alternatives`);
  for (const [name, probs] of [["used", c.probabilities], ["fallback", fb], ...(c.laya ? [["laya", c.laya.probabilities]] : [])]) {
    const sum = Object.values(probs).reduce((s, x) => s + x, 0);
    if (Math.abs(sum - 1) > EPS || Object.values(probs).some((x) => !(x >= 0))) fail("alternatives", `${d.id}: ${name} probabilities sum to ${sum}`);
  }
  const used = c.backend === "laya" ? c.laya?.probabilities : fb;
  if (!deepEqual(used, c.probabilities)) fail("alternatives", `${d.id}: probabilities are not the ${c.backend} ones`);
  // Recompute the fallback from its components.
  const ws = Object.fromEntries((d.alternatives || []).map((a) => {
    const f = a.components?.formula;
    return [a.id, f === "rule5" ? rule5(a.components) : f === "rule7" ? rule7(d, a) : (fail("alternatives", `${a.id}: unknown formula ${f}`), NaN)];
  }));
  const total = Object.values(ws).reduce((s, x) => s + x, 0);
  for (const a of d.alternatives || []) {
    if (!(Math.abs(ws[a.id] - (c.fallback.weights[a.id] ?? NaN)) <= 1e-3 * Math.max(1, ws[a.id]))) fail("alternatives", `${a.id}: weight ${c.fallback.weights[a.id]}, components give ${ws[a.id]?.toFixed?.(4)}`);
    if (!(Math.abs(ws[a.id] / total - fb[a.id]) <= EPS)) fail("alternatives", `${a.id}: fallback probability ${fb[a.id]}, weights give ${(ws[a.id] / total).toFixed(4)}`);
  }
  // Base rows the answer wrote (invitations, favours): same margin, same answer, same minute.
  for (const b of c.base || []) {
    const row = index.get(b)?.obj;
    const yes = (d.alternatives || []).find((a) => a.components?.answer === "yes")?.components;
    if (!row || !yes) { fail("alternatives", `${d.id}: base row ${b} without a rule-7 yes`); continue; }
    if (Math.abs(row.margin - yes.margin) > EPS) fail("alternatives", `${d.id}: ${b} records margin ${row.margin}, the decision ${yes.margin}`);
    const saidYes = (d.alternatives || []).find((a) => a.id === c.selected)?.slug === "yes";
    if (row.accepted !== saidYes) fail("alternatives", `${d.id}: ${b} says accepted ${row.accepted}, the decision chose ${c.selected}`);
    if (row.tick !== c.visibleAt) fail("alternatives", `${d.id}: ${b} is at ${row.tick}, the answer at ${c.visibleAt}`);
  }
  // The set's hash covers alternatives, reasons, exclusions and constraints, whichever method answers.
  const view = viewOf(d);
  if (c.provenance?.alternativeSetHash !== setHash(view.alternatives, view.excluded, view.constraints)) fail("alternatives", `${d.id}: alternativeSetHash does not match its alternatives, reasons and constraints`);
  tick("alternatives");
  // Sampling: every Lives choice is keyed by lives-choice and its decision id, whether or not a reflection opened.
  const s = c.sampled || {};
  if (s.stream !== "lives-choice" || !deepEqual(s.parts, [d.id])) fail("sampling", `${d.id}: the draw must be keyed by "lives-choice" and the decision id, not ${s.stream} ${JSON.stringify(s.parts)}`);
  if (!sameSet(s.order || [], elig)) fail("sampling", `${d.id}: the draw order is not the eligible set`);
  if (!(s.unit >= 0 && s.unit < 1)) fail("sampling", `${d.id}: unit ${s.unit} outside [0, 1)`);
  if (pick(s.order, c.probabilities, s.unit) !== c.selected) fail("sampling", `${d.id}: unit ${s.unit} selects ${pick(s.order, c.probabilities, s.unit)}, recorded ${c.selected}`);
  if (pick(s.order, fb, s.unit) !== c.fallback.wouldSelect) fail("sampling", `${d.id}: the fallback would select ${pick(s.order, fb, s.unit)}, recorded ${c.fallback.wouldSelect}`);
  if (!elig.includes(c.selected)) fail("sampling", `${d.id}: selected ${c.selected} is not eligible`);
  tick("sampling");
  // Method and provenance.
  const tape = c.provenance?.tape ? index.get(c.provenance.tape)?.obj : null;
  if (c.backend === "laya") {
    if (!d.torn?.value) fail("provenance", `${d.id}: Laya answered a choice that is not torn (rule 6)`);
    if (!c.provenance?.model || !index.has(c.provenance.model)) fail("provenance", `${d.id}: a model answer without a model`);
    if (!tape) fail("provenance", `${d.id}: a model answer without a tape entry`);
    else {
      const slugOf = tapeAgrees(d, tape);
      for (const [slug, w] of Object.entries(tape.answer.weights || {}))
        if (Math.abs(w - (c.laya.probabilities[slugOf[slug]] ?? NaN)) > EPS) fail("provenance", `${d.id}: the tape's weight for ${slug} differs`);
      if (tape.answer.model !== c.provenance.model) fail("provenance", `${d.id}: the tape's model is ${tape.answer.model}`);
      if (!EVAL_MODES.has(tape.answer.evaluationMode)) fail("provenance", `${d.id}: the tape's evaluationMode is ${tape.answer.evaluationMode}`);
      for (const k of ["layaPrompt", "layaResponse", "evaluations"]) if (tape.answer[k] === undefined) fail("provenance", `${d.id}: the tape's answer lacks ${k} (ReflectionAnswer's existing field)`);
      const lastPass = (tape.answer.evaluations || []).at(-1);
      if (lastPass && !deepEqual(Object.fromEntries(Object.entries(lastPass.weights || {}).map(([k, v]) => [k, Math.round(v * 1e4)])),
        Object.fromEntries(Object.entries(tape.answer.weights || {}).map(([k, v]) => [k, Math.round(v * 1e4)]))))
        fail("provenance", `${d.id}: the last evaluation's weights are not the answer's`);
    }
    const key = `${d.holder}:${Math.floor(T(d.visibleAt) / 1440)}`;
    layaPerDay.set(key, (layaPerDay.get(key) || 0) + 1);
  } else {
    if (c.provenance?.model) fail("provenance", `${d.id}: a fallback draw names a model`);
    if (c.provenance?.tape) {
      // A reflection that opened without a torn choice: the thought is taped, the keyed draw answers.
      if (!c.provenance.tape.startsWith("reflection:") || !tape) fail("provenance", `${d.id}: a fallback draw may name only a reflection tape`);
      else {
        if (/laya/i.test(tape.answer.backend || "") || tape.answer.model) fail("provenance", `${d.id}: the reflection's answer names a model, but the fallback answered`);
        const slugOf = tapeAgrees(d, tape);
        for (const [slug, w] of Object.entries(tape.answer.weights || {}))
          if (Math.abs(w - (fb[slugOf[slug]] ?? NaN)) > EPS) fail("provenance", `${d.id}: the reflection's weight for ${slug} is not the fallback's`);
      }
    }
    if (d.torn?.value && !c.cap?.reached) fail("provenance", `${d.id}: torn, answered by the fallback with no reason recorded`);
  }
  tick("provenance");
}
for (const [key, n] of layaPerDay) if (n > (S.modelQuestionsPerPersonDay ?? Infinity)) fail("provenance", `${names[+key.split(":")[0]]}: ${n} model questions on day ${key.split(":")[1]}`);
for (const a of L.attempts || []) {
  if (a.decision) {
    const d = index.get(a.decision)?.obj;
    if (d && a.alternative !== d.choice.selected) fail("intention", `${a.id} attempts ${a.alternative}, not the selected ${d.choice.selected}`);
    if (d && d.holder !== a.actor) fail("intention", `${a.id}: the actor is not the decision's holder`);
  } else if (!a.decidedBy) fail("intention", `${a.id}: no decision and no decidedBy`);
  if (!["answered", "busy", "completed", "unsuccessful"].includes(a.status)) fail("intention", `${a.id}: status ${a.status}`);
  tick("intention");
}
const KINDS = new Set(["refused-by-commitment", "declined", "not-now", "busy", "agreed", "agreed-absent", "not-found", "together"]);
for (const o of L.outcomes || []) if (!KINDS.has(o.kind)) fail("intention", `${o.id}: outcome kind ${o.kind}`);

// ---- 10. matched comparisons: only the intervention differs ----
const reasonView = (d) => Object.fromEntries((d.alternatives || []).map((a) => [a.slug, (a.reasons || []).map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null, cites: r.cites || [] }))]));
for (const c of L.comparisons || []) {
  const [a, b] = c.arms || [];
  if (!a || !b || c.arms.length !== 2) { fail("comparison", `${c.id}: needs exactly two arms`); continue; }
  if (a.id === b.id) fail("comparison", `${c.id}: arms share an id`);
  if (c.scope !== "isolated-choice") fail("comparison", `${c.id}: scope ${c.scope}; only isolated choices are matched here`);
  if (c.wholeRun?.simulated !== false) fail("comparison", `${c.id}: must say whether arms were run forward`);
  for (const k of c.heldConstant || []) {
    if (!(k in a.inputs) || !(k in b.inputs)) fail("comparison", `${c.id}: held-constant ${k} missing from an arm`);
    else if (!deepEqual(a.inputs[k], b.inputs[k])) fail("comparison", `${c.id}: held-constant ${k} differs between arms`);
  }
  const keys = new Set([...Object.keys(a.inputs), ...Object.keys(b.inputs)]);
  const differing = [...keys].filter((k) => !deepEqual(a.inputs[k], b.inputs[k]));
  if (differing.length !== 1 || differing[0] !== c.intervention?.field) fail("comparison", `${c.id}: inputs differing are [${differing}], the intervention is ${c.intervention?.field}`);
  for (const k of keys) if (k !== c.intervention?.field && !(c.heldConstant || []).includes(k)) fail("comparison", `${c.id}: input ${k} is neither held constant nor the intervention`);
  if (c.intervention?.field === "history") {
    const removed = a.inputs.history.filter((x) => !b.inputs.history.includes(x));
    const added = b.inputs.history.filter((x) => !a.inputs.history.includes(x));
    if (!sameSet(removed, c.intervention.remove || []) || added.length) fail("comparison", `${c.id}: the history differs by [${removed}] and [${added}], not the listed intervention`);
  }
  // Validity: the intervention may not change the opportunity or whether the decision exists.
  for (const arm of [a, b]) if (arm.derived?.decisionExists !== true) fail("comparison", `${c.id}/${arm.id}: invalid: the decision does not exist in this arm`);
  if (!deepEqual(a.derived?.opportunity, b.derived?.opportunity)) fail("comparison", `${c.id}: invalid: the intervention changes the opportunity`);
  const elig = a.inputs.availableActions?.eligible || [];
  for (const arm of [a, b]) {
    if (!sameSet(Object.keys(arm.derived.probabilities), elig)) fail("comparison", `${c.id}/${arm.id}: probabilities over other alternatives`);
    const sel = pick(elig, arm.derived.probabilities, arm.inputs.randomInput.unit);
    if (sel !== arm.derived.sampled) fail("comparison", `${c.id}/${arm.id}: the unit selects ${sel}, recorded ${arm.derived.sampled}`);
  }
  const effects = new Set(["weights", "probabilities", "sampled", "reasons"]);
  const listed = new Set((c.unavoidableDifferences || []).map((u) => u.field));
  for (const k of new Set([...Object.keys(a.derived), ...Object.keys(b.derived)]))
    if (!deepEqual(a.derived[k], b.derived[k]) && !effects.has(k) && !listed.has(k)) fail("comparison", `${c.id}: derived ${k} differs and is not listed as unavoidable`);
  for (const k of listed) if (!(k in a.derived)) fail("comparison", `${c.id}: unavoidable difference ${k} is not a derived field`);
  const probsDiffer = elig.some((x) => Math.abs(a.derived.probabilities[x] - b.derived.probabilities[x]) > 1e-9);
  if (c.differs?.probabilities !== probsDiffer) fail("comparison", `${c.id}: differs.probabilities should be ${probsDiffer}`);
  if (c.differs?.sampledAction !== (a.derived.sampled !== b.derived.sampled)) fail("comparison", `${c.id}: differs.sampledAction is wrong`);
  if (c.differs?.reasons !== !deepEqual(a.derived.reasons, b.derived.reasons)) fail("comparison", `${c.id}: differs.reasons should be ${!deepEqual(a.derived.reasons, b.derived.reasons)}`);
  const d = index.get(c.decision)?.obj;
  if (d) {
    if (!sameSet(elig, d.alternatives.map((x) => x.id))) fail("comparison", `${c.id}: arms offer other alternatives than ${d.id}`);
    const ri = a.inputs.randomInput;
    if (ri.unit !== d.choice.sampled.unit || ri.stream !== d.choice.sampled.stream || !deepEqual(ri.parts, d.choice.sampled.parts)) fail("comparison", `${c.id}: arms use another random input than ${d.id}`);
    if (a.derived.opportunity?.id !== d.opportunity) fail("comparison", `${c.id}: the arms' opportunity is not ${d.id}'s`);
    if (a.inputs.method?.backend === "fallback-keyed-draw") {
      for (const x of elig) if (Math.abs(a.derived.probabilities[x] - d.choice.fallback.probabilities[x]) > EPS) fail("comparison", `${c.id}: arm ${a.id} does not reproduce ${d.id}'s fallback`);
      if (!deepEqual(a.derived.reasons, reasonView(d))) fail("comparison", `${c.id}: arm ${a.id}'s reasons are not ${d.id}'s`);
    }
  }
  tick("comparison");
}

// ---- 11. archetypes are referenced, never carry a life ----
const archIds = new Set(L.external?.archetypes?.ids || []);
for (const c of L.characters || []) {
  if (c.archetype && !archIds.has(c.archetype)) fail("archetypes", `${c.name}: archetype ${c.archetype} is not a referenced id`);
  for (const id of c.history || []) {
    const r = index.get(id)?.obj;
    if (!r) continue;
    const holds = r.holder === c.who || (r.knownBy || []).some((k) => k.who === c.who);
    if (!holds) fail("archetypes", `${c.name} lists ${id}, which is not theirs`);
  }
  tick("archetypes");
}
if (L.archetypes) for (const a of L.archetypes) for (const k of Object.keys(a))
  if (/history|record|commitment|account|reading/i.test(k)) fail("archetypes", `archetype ${a.id} carries ${k}: lives stay with characters`);
for (const r of L.records || []) if ("archetype" in r) fail("archetypes", `${r.id} names an archetype: records belong to people`);

// ---- report ----
const summary = Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(", ");
console.log(`checked ${file}`);
console.log(`lives ${L.version}${L.illustrative ? " (illustrative)" : ""}: ${(L.records || []).length} records, ${(L.decisions || []).length} decisions, ${(L.comparisons || []).length} comparisons, ${index.size} ids`);
console.log(`checks run: ${summary}`);
if (errors.length) {
  console.log(`FAIL: ${errors.length} problem(s)`);
  for (const e of errors) console.log("  " + e);
  process.exit(1);
}
console.log("PASS: ids, references, clock and gates, knowledge and lens, windows, bounds, alternatives and formulas, sampling, provenance, comparisons and archetypes hold");
