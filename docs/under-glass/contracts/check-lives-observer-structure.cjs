#!/usr/bin/env node
// Structural checker for the proposed Lives observer recording contract v0.2 (lives-observer-recording-v0.2.md).
// It checks what a reader relies on: version and shape, ids and references, the playback clock and every field gate,
// knowledge and the character lens (nested shapes, codes not prose, references inside the lens), snapshots, bounds
// against the recording's own settings, base-row answers, the archetype pin, one option set for both methods (as Laya
// reads it: request.choices, grouped and labelled as ResilientReflectionMind.BuildPasses does), every hash over one
// canonical JSON (RFC 8785, contract 9.6), model identity, failures and receipts (against the raw text in real output),
// comparison structure, excerpts (their days against the calendar) and pending elements, that sleep is recorded and
// never inferred from a habitual bedtime, and pre-run provenance. It accepts every legitimate run shape:
// authored controls, model failures, hybrids whose evaluator ran on choices that are not torn, quiet reflections
// without an archetype, Raw, Canonical and Balanced evaluation, replays and chunks (lives-observer-v0.2-tools/variants.cjs).
// It never requires the illustrative flag, labels, characterLabels, placeholder model answers or any pilot formula:
// those are check-lives-observer-rules.cjs's (experimental) business. It must pass on real output.
// Dependency-free (node built-ins only). Usage: node check-lives-observer-structure.cjs <file.json>
// Exit 0 when every check passes (or the file has no lives block), 1 when any fails, 2 on usage or parse errors.
"use strict";
const fs = require("fs");
const crypto = require("crypto");

const file = process.argv[2];
if (!file || process.argv.length > 3) { console.error("usage: node check-lives-observer-structure.cjs <file.json>"); process.exit(2); }
let run;
try { run = JSON.parse(fs.readFileSync(file, "utf8")); } catch (e) { console.error(`cannot read ${file}: ${e.message}`); process.exit(2); }

const errors = [];
const counts = {};
const fail = (check, msg) => errors.push(`[${check}] ${msg}`);
const tick = (check, n = 1) => { counts[check] = (counts[check] || 0) + n; };
const PRE = "pre-run";
const T = (v) => (v === PRE ? -1 : typeof v === "number" && Number.isFinite(v) ? v : Infinity); // a missing gate never arrives
const EPS = 2e-3;
const isObj = (v) => v !== null && typeof v === "object" && !Array.isArray(v);
// Canonical JSON (contract 9.6): RFC 8785, the JSON Canonicalization Scheme. Object members sorted by their names'
// UTF-16 code units; arrays in order; no whitespace; null, true and false; numbers as ECMAScript Number::toString
// writes them (what JSON.stringify writes: 26540, 0.05, 1e-7, 1e+21; -0 as 0); strings as JSON.stringify writes them
// (only ", \ and U+0000-U+001F escaped: \b \t \n \f \r, other controls \u00xx in lowercase hex; everything else,
// apostrophes and non-ASCII included, as itself). A string holding a lone surrogate has no canonical form (RFC 8785
// takes I-JSON input): the writer refuses it and so does this checker. Hashes over the canonical text: requestHash,
// alternativeSetHash and identityHash are FNV-1a 64 over its UTF-16 code units with one 0x1F terminator (Rng.Hash with
// one part, Rng.cs:11-24), written "fnv1a64:" + 16 lowercase hex digits; snapshotHash is SHA-256 over its UTF-8 bytes,
// 64 lowercase hex digits. lives-observer-v0.2-tools/canon-vectors.cjs prints test vectors for the C# writer.
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
  if (typeof v === "number" && !Number.isFinite(v)) throw new Error("a non-finite number has no canonical form");
  return JSON.stringify(v);
}
// The first lone surrogate in a value's strings (member names included), as a path, or null.
function loneSurrogate(v, path = "") {
  const bad = (s) => /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/.test(s);
  if (typeof v === "string") return bad(v) ? path || "(value)" : null;
  if (Array.isArray(v)) { for (const [i, x] of v.entries()) { const p = loneSurrogate(x, `${path}[${i}]`); if (p) return p; } return null; }
  if (v && typeof v === "object") for (const [k, x] of Object.entries(v)) { if (bad(k)) return `${path}.${k}`; const p = loneSurrogate(x, `${path}.${k}`); if (p) return p; }
  return null;
}
const deepEqual = (a, b) => canon(a) === canon(b);
const sameSet = (a, b) => a.length === b.length && [...a].sort().join("\n") === [...b].sort().join("\n");
const ord = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
const REF = /^(h|acct|rd|cmt|as|ex|src|opp|rc|dec|alt|att|plan|out|per|lu|cmp|thr|act|fav|inv|need|reflection|question|model):\S+$/;
// Character-safe strings are ids or codes: no spaces, no prose (contract 4.2). An association's name is the one
// spaced exception: the holder's word for a feeling, lowercase, at most four words.
const CODE = /^[A-Za-z0-9][A-Za-z0-9_.+@\-]*(?::[A-Za-z0-9][A-Za-z0-9_.+@\-]*)*$/;
const NAME = /^[a-z]+(?:[ -][a-z]+){0,3}$/;
const ANSWERS = new Set(["yes", "no", "not-now", "busy"]);
const acts = run.acts || [];
const kindsByIndex = (run.kinds || []).map((k) => k.name);
const names = Array.isArray(run.names) ? run.names : [];
const rowsOf = () => [...(run.favours || []).map((r) => ["favour", r]), ...(run.invitations || []).map((r) => ["invitation", r])];

// Base rows (contract section 8). A legacy row (no answer field) is read only when provable; refusal is never
// inferred from a missing act.
function answerOf(r) {
  if (r.answer !== undefined) return r.answer;
  if (r.accepted === true) return "yes";
  const a = acts.find((x) => x[0] === r.actId);
  if (r.accepted === false && a && kindsByIndex[a[3]] === "Refused") return "no";
  return "unknown";
}
function checkRows(excerpt) {
  const actIds = new Map();
  for (const [tbl, r] of rowsOf()) {
    const where = r.id ?? `${tbl} row ${r.tick}:${r.asker}:${r.asked}`;
    const ans = answerOf(r);
    if (r.answer === undefined) { if (ans === "unknown") fail("rows", `${where}: a legacy row without answer whose answer cannot be proven (it stays unknown; refusal is never inferred from a missing act)`); }
    else {
      if (!ANSWERS.has(r.answer)) fail("rows", `${where}: answer ${r.answer}`);
      if (typeof r.askMade !== "boolean" || r.askMade !== (r.answer !== "busy")) fail("rows", `${where}: askMade is false exactly when the answer is busy (no ask was made)`);
      if (r.answer === "yes" && r.accepted !== true) fail("rows", `${where}: a yes is accepted: true`);
      if (r.answer === "no" && r.accepted !== false) fail("rows", `${where}: a no is accepted: false`);
      if ((r.answer === "not-now" || r.answer === "busy") && r.accepted !== null) fail("rows", `${where}: ${r.answer} is neither a yes nor a no: accepted is null, never false`);
      if ((r.answer === "not-now" || r.answer === "busy") && r.actId !== null) fail("rows", `${where}: ${r.answer} begins no act: actId is null`);
      if (r.answer === "no" && r.actId === null) fail("rows", `${where}: a no carries its Refused act`);
      if (r.answer === "busy" && ["margin", "tired", "busy", "low", "bond", "eager"].some((k) => r[k] !== null && r[k] !== undefined)) fail("rows", `${where}: nobody was asked, so the margin and the asked person's state are null`);
      if (!r.id) fail("rows", `${where}: a row with an answer carries its id`);
    }
    if (r.actId === -1) fail("rows", `${where}: -1 is not used for 'no act' on these rows; actId is null`);
    if (r.actId !== null && r.actId !== undefined) {
      if (actIds.has(r.actId)) fail("rows", `${where} and ${actIds.get(r.actId)} share act ${r.actId}: an act belongs to one row`);
      actIds.set(r.actId, where);
      const a = acts.find((x) => x[0] === r.actId);
      if (!a) { if (!excerpt) fail("rows", `${where}: act ${r.actId} is not in the recording`); }
      else {
        const k = kindsByIndex[a[3]];
        if (ans === "no" && (k !== "Refused" || a[2] !== names.indexOf(r.asked) || a[7] !== names.indexOf(r.asker) || a[1] !== r.tick))
          fail("rows", `${where}: a no's act is the asked person's Refused act toward the asker, that minute`);
        if (ans === "yes" && (k === "Refused" || a[2] !== names.indexOf(r.asker))) fail("rows", `${where}: a yes's act is the asker's asked-for act, never a Refused act`);
      }
    }
    tick("rows");
  }
}

// ---- 0. no lives block: nothing of Lives to check (a reader shows "no Lives data" and never rebuilds one) ----
const L = run.lives;
if (L === undefined) {
  if (run.format !== "under-glass-run") fail("version", `format is ${run.format}, expected under-glass-run`);
  checkRows(false); // legacy rows are still readable only when provable (section 8)
  console.log(`checked ${file}`);
  console.log(`no Lives data: nothing of Lives to check; base rows checked: ${counts.rows || 0}`);
  if (errors.length) { console.log(`FAIL: ${errors.length} problem(s)`); for (const e of errors) console.log("  " + e); process.exit(1); }
  console.log("PASS: no lives block (base rows readable)");
  process.exit(0);
}

// ---- 1. version and shape ----
if (run.format !== "under-glass-run") fail("version", `format is ${run.format}, expected under-glass-run`);
if (run.version !== 1) fail("version", `top-level version is ${run.version}; the lives block is additive and leaves it at 1`);
if (!isObj(L)) { console.log(`checked ${file}`); console.log("FAIL: [version] lives is not an object"); process.exit(1); }
if (L.contract !== "under-glass-lives-observer") fail("version", `lives.contract is ${L.contract}`);
if (L.version !== "0.2") { console.log(`checked ${file}`); console.log(`FAIL: [version] lives.version is ${JSON.stringify(L.version)}; this checker reads exactly "0.2" (readers accept exact 0.x versions only)`); process.exit(1); }
if (!Array.isArray(run.names)) fail("version", "names must be an array");
const TABLES = ["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons", "threads"];
const LIVES_KEYS = ["contract", "version", "status", "illustrative", "runId", "rules", "run", "excerpt", "settings", "clock", "models", "characters", "sources", ...TABLES, "unresolved", "observer"];
for (const k of Object.keys(L)) if (!LIVES_KEYS.includes(k)) fail("lens", `lives.${k} is not a field of the lives block (0.1's external, note and assumes are gone; block prose goes under lives.observer)`);
for (const t of [...TABLES, "sources", "models", "characters", "unresolved"]) if (!Array.isArray(L[t])) fail("version", `lives.${t} must be an array`);
for (const t of [...TABLES, "sources", "models", "characters", "unresolved"]) if (!Array.isArray(L[t])) L[t] = [];
const S = isObj(L.settings) ? L.settings : (fail("version", "lives.settings is missing"), {});
const R = isObj(S.recall) ? S.recall : (fail("version", "settings.recall is missing"), {});
for (const k of ["livedWindow", "associationsPerObject", "modelQuestionsPerPersonDay"]) if (typeof S[k] !== "number") fail("version", `settings.${k} is missing`);
for (const k of ["top", "max", "lowMoodExtra", "runnersUpShown"]) if (typeof R[k] !== "number") fail("version", `settings.recall.${k} is missing`);
for (const t of TABLES) for (const x of L[t]) {
  if (!isObj(x) || typeof x.id !== "string") { fail("version", `${t}: an element without a string id`); continue; }
  if (!Number.isInteger(x.seq) || x.seq < 0) fail("version", `${x.id}: seq must be a non-negative integer`);
  if (!(x.visibleAt === PRE || Number.isFinite(x.visibleAt))) fail("version", `${x.id}: visibleAt must be a minute or "pre-run"`);
}
tick("version");

// ---- illustrative or real ----
const notImplemented = /not output from an implemented system/i;
const ILLUSTRATIVE = run.illustrative === true || L.illustrative === true;
if (ILLUSTRATIVE) {
  if (run.illustrative !== true || L.illustrative !== true) fail("illustrative", "illustrative must be set at the top level and in lives together");
  if (!notImplemented.test(run.note || "")) fail("illustrative", "top-level note must say it is not output from an implemented system");
  if (!notImplemented.test(L.observer?.note || "")) fail("illustrative", "lives.observer.note must say it is not output from an implemented system");
} else {
  if (!/^run:\d+:\d+:\S+$/.test(L.runId || "")) fail("illustrative", "a real recording carries runId run:<seed>:<days>:<hash>");
  if (!/^[0-9a-f]{7,40}$/.test(L.run?.source?.commit || "")) fail("illustrative", "a real recording names its source commit (lives.run.source.commit)");
}
tick("illustrative");

// ---- the excerpt (contract 6.4): a declared window, validated, never trusted ----
const EXCERPT = isObj(L.excerpt) ? L.excerpt : null;
if (L.excerpt !== undefined && !EXCERPT) fail("excerpt", "lives.excerpt must be an object {from, to}");
if (EXCERPT) {
  for (const k of Object.keys(EXCERPT)) if (!["from", "to", "days", "chunk"].includes(k)) fail("excerpt", `excerpt.${k} is not a field of an excerpt`);
  if (!(Number.isFinite(EXCERPT.from) && Number.isFinite(EXCERPT.to) && EXCERPT.from <= EXCERPT.to)) fail("excerpt", `excerpt {from ${EXCERPT.from}, to ${EXCERPT.to}} must be two minutes with from <= to`);
  if (!ILLUSTRATIVE && !(isObj(EXCERPT.chunk) && typeof EXCERPT.chunk.adapter === "string" && Number.isInteger(EXCERPT.chunk.index)))
    fail("excerpt", "a real recording declares an excerpt only as a storage chunk: excerpt.chunk {adapter, index}");
}
const inWindow = (m) => !EXCERPT || !(Number.isFinite(EXCERPT.from) && Number.isFinite(EXCERPT.to)) || (m >= EXCERPT.from && m <= EXCERPT.to);
// The calendar (contract 6.1): minutes count from midnight of day 0, spring 1, a Monday; the recording's clock gives the
// minutes a day, days a week and days a season (Replay.cs:190). An excerpt's days[] is metadata a reader shows: each
// entry agrees with the clock, and together they are the window's days in order.
const CLOCK = { perDay: run.clock?.minutesPerDay ?? 1440, perWeek: run.clock?.daysPerWeek ?? 7, perSeason: run.clock?.daysPerSeason ?? 28 };
const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], SEASONS = ["spring", "summer", "fall", "winter"];
const dayOfMinute = (m) => Math.floor(m / CLOCK.perDay);
if (EXCERPT && EXCERPT.days !== undefined) {
  const days = EXCERPT.days;
  if (!Array.isArray(days) || days.some((d) => !isObj(d))) fail("excerpt", "excerpt.days is a list of {day, season, date, weekday}");
  else {
    for (const d of days) {
      for (const k of Object.keys(d)) if (!["day", "season", "date", "weekday"].includes(k)) fail("excerpt", `excerpt.days: ${k} is not a field of a day`);
      if (!Number.isInteger(d.day) || d.day < 0) { fail("excerpt", `excerpt.days: day ${d.day} is not a day number`); continue; }
      const want = { season: SEASONS[Math.floor(d.day / CLOCK.perSeason) % 4], date: (d.day % CLOCK.perSeason) + 1, weekday: CLOCK.perWeek === 7 ? WEEKDAYS[d.day % 7] : d.weekday };
      for (const k of ["season", "date", "weekday"]) if (d[k] !== want[k]) fail("excerpt", `excerpt.days: day ${d.day} is ${want.weekday} ${want.season} ${want.date} by the clock (day 0 is spring 1, a Monday), not ${d.weekday} ${d.season} ${d.date}`);
    }
    if (Number.isFinite(EXCERPT.from) && Number.isFinite(EXCERPT.to)) {
      const listed = days.map((d) => d.day), first = dayOfMinute(EXCERPT.from), last = dayOfMinute(EXCERPT.to);
      const want = Array.from({ length: Math.max(0, last - first + 1) }, (_, i) => first + i);
      if (listed.join(",") !== want.join(",")) fail("excerpt", `excerpt.days lists days [${listed}]; the window ${EXCERPT.from}-${EXCERPT.to} covers days [${want}]`);
    }
  }
}
tick("excerpt");

// ---- 2. the id index ----
const index = new Map(); // id -> { kind, obj, at }
function add(id, kind, obj, at) {
  if (typeof id !== "string") { fail("ids", `${kind} without a string id`); return; }
  if (index.has(id)) fail("ids", `duplicate id ${id}`);
  index.set(id, { kind, obj, at });
}
for (const [i, a] of acts.entries()) {
  add(`act:${a[0]}`, "act", a, a[1]);
  if (!EXCERPT && a[0] !== i) fail("ids", `act ${a[0]} is at position ${i}: outside an excerpt an act's id is its position (Replay.cs:231-236)`);
}
for (const [kind, r] of rowsOf()) if (typeof r.id === "string") add(r.id, kind, r, r.tick);
for (const n of run.needs || []) add(`need:${n.id}`, "need", n, n.tick);
const TAPES = [...(run.reflections || []).map((t) => ["reflection", t]), ...(run.choiceQuestions || []).map((t) => ["question", t])];
for (const [k, t] of TAPES) add(`${k}:${t.request?.id}`, k, t, t.request?.tick);
for (const s of L.sources) add(s.id, "source", s, -1);
for (const m of L.models) add(m.id, "model", m, -1);
const prefixOf = { fact: "h", account: "acct", reading: "rd", commitment: "cmt", association: "as", expectation: "ex" };
for (const r of L.records) {
  const p = prefixOf[r.type];
  if (!p) fail("ids", `record ${r.id} has unknown type ${r.type}`);
  else if (!r.id.startsWith(p + ":")) fail("ids", `record ${r.id} of type ${r.type} should start with ${p}:`);
  add(r.id, "records", r, T(r.visibleAt));
}
const tablePrefix = { opportunities: "opp", recalls: "rc", decisions: "dec", attempts: "att", plans: "plan", outcomes: "out", perceptions: "per", learning: "lu", comparisons: "cmp", threads: "thr" };
for (const [t, p] of Object.entries(tablePrefix)) for (const x of L[t]) {
  if (!x.id.startsWith(p + ":")) fail("ids", `${t} entry ${x.id} should start with ${p}:`);
  add(x.id, t, x, T(x.visibleAt));
}
for (const d of L.decisions) {
  for (const a of d.alternatives || []) add(a.id, "alternative", { ...a, decision: d.id, eligible: true }, T(d.visibleAt));
  for (const a of d.excluded || []) add(a.id, "alternative", { ...a, decision: d.id, eligible: false }, T(d.visibleAt));
}
{ // id forms (contract section 3)
  const seen = new Map();
  const ordinal = (k) => { const n = seen.get(k) ?? 0; seen.set(k, n + 1); return n; };
  for (const [tbl, prefix, whoKey] of [["opportunities", "opp", "holder"], ["attempts", "att", "actor"], ["perceptions", "per", "who"], ["learning", "lu", "who"], ["outcomes", "out", "about"]]) {
    for (const x of [...L[tbl]].sort((a, b) => a.seq - b.seq)) {
      const who = x[whoKey];
      if (!Number.isInteger(who)) { fail("ids", `${x.id}: no ${whoKey}`); continue; }
      const want = `${prefix}:${x.visibleAt}:${names[who]}:${ordinal(`${tbl}|${x.visibleAt}|${who}`)}`;
      if (x.id !== want) fail("ids", `${x.id} should be ${want} (minute, ${whoKey}'s name, that person's ordinal that minute, in seq order)`);
    }
  }
  const oppById = new Map(L.opportunities.map((o) => [o.id, o]));
  for (const [tbl, prefix] of [["recalls", "rc"], ["decisions", "dec"]]) for (const x of L[tbl]) {
    const o = oppById.get(x.opportunity);
    if (!o) { fail("ids", `${x.id}: every ${tbl.slice(0, -1)} belongs to one opportunity`); continue; }
    if (x.id !== `${prefix}:${o.id.slice(4)}`) fail("ids", `${x.id} should take its opportunity's body: ${prefix}:${o.id.slice(4)}`);
    if (x.visibleAt !== o.visibleAt) fail("ids", `${x.id} is posed at ${x.visibleAt}, its opportunity at ${o.visibleAt}`);
    if (x.holder !== o.holder) fail("ids", `${x.id}: its holder is not its opportunity's`);
  }
  for (const d of L.decisions) for (const a of [...(d.alternatives || []), ...(d.excluded || [])])
    if (a.id !== `alt:${d.id.slice(4)}/${a.slug}`) fail("ids", `${a.id} should be alt:${d.id.slice(4)}/${a.slug}`);
  for (const p of L.plans) {
    const asker = (p.members || []).find((m) => m.role === "asker"), asked = (p.members || []).find((m) => m.role === "asked");
    const want = asker && asked ? `plan:${p.visibleAt}:${names[asker.who]}:${names[asked.who]}` : null;
    if (p.id !== want) fail("ids", `${p.id} should be ${want}`);
  }
  for (const c of L.comparisons) if (!c.id.startsWith(`cmp:${(c.decision || "").slice(4)}:`)) fail("ids", `${c.id} should start with cmp:${(c.decision || "").slice(4)}:`);
  for (const r of L.records) {
    const sl = (s) => String(s).toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
    if (r.type === "association" && r.id !== `as:${names[r.holder]}:${r.object}:${r.sign}:${sl(r.name)}`)
      fail("ids", `${r.id} should be as:${names[r.holder]}:${r.object}:${r.sign}:${sl(r.name)} (holder, object, sign, name)`);
    if (r.type === "expectation" && r.id !== `ex:${names[r.holder]}:${names[r.subject]}:${r.key}`) fail("ids", `${r.id} should be ex:${names[r.holder]}:${names[r.subject]}:${r.key}`);
  }
  // An opportunity's tape link is observer-only (4.3): observer.reflection or observer.question.
  for (const o of L.opportunities) {
    const refl = o.observer?.reflection, q = o.observer?.question;
    if (refl && refl !== `reflection:${run.seed}:${o.id}`) fail("ids", `${o.id}: its reflection should be reflection:${run.seed}:${o.id}`);
    if (q && q !== `question:${run.seed}:q:${o.id.slice(4)}`) fail("ids", `${o.id}: its question should be question:${run.seed}:q:${o.id.slice(4)}`);
  }
  for (const [tbl, prefix] of [["favours", "fav"], ["invitations", "inv"]]) {
    const n = new Map();
    for (const r of run[tbl] || []) {
      if (r.id === undefined) continue; // a legacy row (section 8)
      const k = `${r.tick}:${r.asker}:${r.asked}`, i = n.get(k) ?? 0; n.set(k, i + 1);
      if (r.id !== `${prefix}:${k}:${i}`) fail("ids", `${tbl} row ${r.id} should be ${prefix}:${k}:${i}`);
    }
  }
}
tick("ids");

// ---- 3. references resolve; people are indices ----
let refCount = 0;
function walkRefs(v, path, key) {
  if (Array.isArray(v)) { v.forEach((x, i) => walkRefs(x, `${path}[${i}]`, key)); return; }
  if (isObj(v)) { for (const [k, x] of Object.entries(v)) walkRefs(x, `${path}.${k}`, k); return; }
  if (typeof v === "string" && REF.test(v) && key !== "id") { refCount++; if (!index.has(v)) fail("refs", `${path} -> ${v} does not resolve`); }
}
for (const t of [...TABLES, "unresolved", "characters"]) walkRefs(L[t], `lives.${t}`, null);
const PEOPLE = new Set(["who", "holder", "actor", "target", "asked", "subject", "about", "by", "teller"]);
function walkPeople(v, path, key) {
  if (Array.isArray(v)) { v.forEach((x, i) => walkPeople(x, `${path}[${i}]`, key)); return; }
  if (isObj(v)) { for (const [k, x] of Object.entries(v)) walkPeople(x, `${path}.${k}`, k); return; }
  const list = key === "participants" || key === "present" || key === "people";
  if ((PEOPLE.has(key) || list) && typeof v === "number" && !(Number.isInteger(v) && v >= 0 && v < names.length)) fail("refs", `${path} = ${v} is not an index into names`);
}
for (const t of TABLES) walkPeople(L[t], `lives.${t}`, null);
for (const c of L.characters) if (names[c.who] !== c.name) fail("refs", `character ${c.who} is ${names[c.who]}, not ${c.name}`);
counts.refs = refCount;

// ---- pending elements (contract 6.1): a gate not reached when the recording or excerpt ends ----
const pendingRefs = new Set(L.unresolved.filter((u) => u.status === "open").map((u) => u.ref));
const pendingOutcome = (o) => pendingRefs.has(o.id) || (o.plan && pendingRefs.has(o.plan));
const pendingAttempts = new Set();
for (const o of L.outcomes) if (pendingOutcome(o)) for (const a of [...(o.attempts || []), ...(o.timeline || []).flatMap((e) => e.attempts || [])]) pendingAttempts.add(a);
const pendingAttempt = (a) => pendingRefs.has(a.id) || pendingAttempts.has(a.id) || (a.observer?.outcome && pendingRefs.has(a.observer.outcome));

// ---- knowledge: who can see what, and when (the character lens) ----
// vis(): when the observer can first see an id. A decision is citable once its choice is.
function vis(id) {
  const e = index.get(id);
  if (!e) return Infinity;
  if (e.kind === "decisions") return T(e.obj.choice ? e.obj.choice.visibleAt : e.obj.visibleAt);
  return e.at;
}
const perceptionsOf = new Map();
for (const p of L.perceptions) if (p.of) { if (!perceptionsOf.has(p.of)) perceptionsOf.set(p.of, []); perceptionsOf.get(p.of).push(p); }
// A tape reaches a character only as the thought of their own decision, from that decision's choice minute (4.3, 5.4).
const thoughtOwner = new Map();
for (const d of L.decisions) if (d.choice?.thought?.tape) thoughtOwner.set(d.choice.thought.tape, d);
const OBSERVER_ONLY = new Set(["outcomes", "comparisons", "threads", "source", "model"]);
// sees(x, id, t): may person x see this id at minute t in the character lens?
function sees(x, id, t) {
  const e = index.get(id);
  if (!e) return false;
  const o = e.obj;
  switch (e.kind) {
    case "records": case "plans": return T(o.visibleAt) <= t && (o.knownBy || []).some((k) => k.who === x && T(k.availableAt) <= t);
    case "attempts": return (o.actor === x && T(o.visibleAt) <= t) || (perceptionsOf.get(id) || []).some((p) => p.who === x && T(p.visibleAt) <= t);
    case "opportunities": case "recalls": case "decisions": return o.holder === x && T(o.visibleAt) <= t;
    case "alternative": { const d = index.get(o.decision)?.obj; return !!d && d.holder === x && T(d.visibleAt) <= t; }
    case "perceptions": case "learning": return o.who === x && T(o.visibleAt) <= t;
    case "act": return (o[2] === x || o[7] === x) && o[1] <= t;
    case "need": return names.indexOf(o.holder) === x && o.tick <= t;
    // A base row is the asked person's record (their answer, their state and the Ask's margin then): it reaches the
    // character lens only for the asked person, and only if an ask was made. The asker knows the answer only through
    // their own perception of it (5.2, 5.4, 8).
    case "favour": case "invitation": return o.tick <= t && names.indexOf(o.asked) === x && o.askMade !== false && o.answer !== "busy";
    case "reflection": { const d = thoughtOwner.get(id); return !!d && d.holder === x && o.request?.actor === names[x] && T(d.choice.visibleAt) <= t; }
    default: return false; // question tapes, outcomes, comparisons, threads, sources, models: never in a character's lens
  }
}
// The first recorded state of an association or expectation (contract 6.3): references before it are refused.
const firstAsOf = (r) => Math.min(...(r.snapshots || []).map((s) => T(s.asOf)));

// ---- 4. the lens partition: nested character-safe shapes, codes not prose, prose only under observer ----
const HOW = ["CanonStated", "ProductDecision", "Authored", "Seeded", "Lived", "Witnessed", "Told", "Inferred", "Unknown"];
const SOURCE = { enum: ["Seen", "Sought", "Told", "Lived"] };
const KNOWN_BY = { obj: { who: "person", how: { enum: HOW }, availableAt: "minute", teller: "person", basis: "ids", account: "id", via: "id" } };
const ENTRY = { obj: { at: "minute", outcome: "code", source: { enum: ["Reading", ...SOURCE.enum] }, ref: "id" } };
const KB = { arr: KNOWN_BY };
const SPEC = {
  fact: { type: "code", participants: "persons", kind: { enum: ["Fact", "Past", "Practice"] }, place: "code", time: { obj: { known: "bool", at: "minute" } },
    recurrence: { enum: ["once", "recurring", "job"] }, stillTrueAtStart: "bool", knownBy: KB, causes: "ids" },
  account: { type: "code", holder: "person", fact: "id", mode: { enum: ["Lived", "Witnessed", "Told", "Inferred"] }, knownBy: KB, causes: "ids" },
  reading: { type: "code", holder: "person", object: "idOrCode", stance: { enum: ["warm", "wary", "expects", "none"] }, certainty: { enum: ["stated", "guess"] }, knownBy: KB, causes: "ids" },
  commitment: { type: "code", holder: "person", scope: { obj: { provide: "code", goodsToday: "codes" } }, to: "personOrCode", force: "numOrCode", revisable: "bool", since: "minute", knownBy: KB, causes: "ids" },
  association: { type: "code", holder: "person", object: "code", sign: { enum: ["+", "-"] }, name: "name", situation: "codes", causes: "ids", knownBy: KB,
    snapshots: { arr: { obj: { asOf: "minute", state: { obj: { strength: "num" } } } } } },
  expectation: { type: "code", holder: "person", subject: "person", key: "code", knownBy: KB, causes: "ids",
    snapshots: { arr: { obj: { asOf: "minute", state: { obj: { entries: { arr: ENTRY }, lived: "num", seeded: "num", successes: "num", chance: "num" } } } } } },
  opportunities: { holder: "person", trigger: { enum: ["refused-by-commitment", "asked", "follow-through", "encounter", "outcome-contradicts", "plan-broken"] }, causes: "ids" },
  recalls: { holder: "person", opportunity: "id", items: { arr: { obj: { record: "id", slot: { enum: ["top", "low-mood"] }, matched: "codes", observer: "observer" } } } },
  decisions: { holder: "person", opportunity: "id", recall: "id", question: "code",
    alternatives: { arr: { obj: { id: "id", slug: "code", characterLabel: "label", observer: "observer",
      template: { obj: { kind: { enum: ["plan-offer", "carry-on", "go"] }, asked: "person", place: "code", activity: "code", routine: "code", from: "minute", until: "minute", arrive: "minute" } },
      components: (where, v) => validate(where, v, { obj: v?.formula === "rule7"
        ? { formula: "code", answer: { enum: ["yes", "no", "not-now"] }, obliged: "num", margin: "num", reason: { obj: { code: "code", value: "num" } },
          costs: { obj: { base: "num", state: "num", errand: "num", displacedHabit: "num", working: "num" } } }
        : { formula: "code", base: "num", chance: { obj: { whereabouts: "num", answers: "num", followThrough: "num" } }, warm: "num", cold: "num", boldness: "num", intended: "num", cost: "num" } }),
      reasons: { arr: { obj: { kind: { enum: ["supports", "conflicts", "shown"] }, code: "code", value: "num", cites: "ids", characterLabel: "label", observer: "observer",
        via: { enum: ["recall", "option-side", "expectation", "opportunity", "plan", "ask", "state", "routine", "rule"] },
        effect: { enum: ["context-only", "not-option-object", "no-warmth", "torn-test-only"] } } } } } } },
    excluded: { arr: { obj: { id: "id", slug: "code", why: { obj: { code: "code", cites: "ids", observer: "observer" } }, characterLabel: "label", observer: "observer" } } },
    constraints: { arr: { obj: { code: "code", value: "numOrCode", terms: { obj: { from: "minute", until: "minute" } }, cites: "ids", characterLabel: "label", observer: "observer" } } },
    choice: { obj: { visibleAt: "minute", selected: "id", thought: { obj: { tape: "id", imagined: "bool" } }, base: "ids", observer: "observer" } } },
  attempts: { actor: "person", target: "person", kind: { enum: ["errand-ask", "plan-offer", "go", "stay", "begin-planned-act"] }, decision: "id", alternative: "id",
    decidedBy: { obj: { mechanism: "code" } }, base: "ids", terms: { obj: { place: "code", activity: "code", from: "minute", until: "minute", waitUntil: "minute" } },
    knownBy: KB, resolvedAt: "minute", status: { enum: ["answered", "busy", "completed", "unsuccessful"] } },
  plans: { origin: "id", answer: { enum: ["yes"] }, formal: "bool", place: "code", activity: "code", window: { obj: { from: "minute", until: "minute" } },
    members: { arr: { obj: { who: "person", role: { enum: ["asker", "asked"] } } } }, knownBy: KB },
  perceptions: { who: "person", of: "id", causes: "ids", perceived: (where, v) => perceivedShape(where, v),
    unknown: { arr: { obj: { about: { enum: ["whereabouts", "reason", "answer"] }, subject: "person", status: { enum: ["unknown"] } } } },
    inferences: { arr: { obj: { about: { enum: ["whereabouts", "reason"] }, subject: "person", guess: "code", certainty: { enum: ["guess"] }, basis: "ids", establishes: "bool" } } } },
  learning: { who: "person", record: "id", evidence: "ids", previous: "any", new: "any", added: ENTRY, dropped: ENTRY, preserved: "ids",
    mood: { obj: { change: "num" } }, interpretation: { obj: { formed: "bool", about: "person", code: "code", supportedBy: "ids" } } },
  outcomes: { about: "person", attempts: "ids", plan: "id", participants: "persons", settledAt: "minute", base: "ids", mustNotWrite: "codes",
    kind: { enum: ["refused-by-commitment", "declined", "not-now", "busy", "agreed", "agreed-absent", "not-found", "together"] },
    timeline: { arr: { obj: { at: "minute", event: "code", attempts: "ids", observer: "observer" } } }, world: "codes-anywhere", determinedBy: { obj: { rule: "code" } } },
  comparisons: { decision: "id", scope: "code", status: { enum: ["comparable", "non-comparable"] }, question: "code", intervention: "codes-anywhere", heldConstant: "codes-anywhere",
    arms: "codes-anywhere", unavoidableDifferences: "codes-anywhere", differs: "codes-anywhere", nonComparable: "codes-anywhere", notShown: "codes", runChoice: "codes-anywhere", wholeRun: "codes-anywhere" },
  threads: { stages: { arr: { obj: { stage: "code", ids: "ids" } } } },
};
// What a person perceived, by kind (contract 12): a closed vocabulary of codes, ids, people and minutes.
const PERCEIVED = {
  asked: { by: "person", ask: { enum: ["errand", "plan"] }, good: "code", activity: "code", place: "code" },
  busy: { subject: "person", activity: "code", place: "code", source: SOURCE },
  refused: { answer: { enum: ["no"] }, act: "id" },
  agreed: { answer: { enum: ["yes"] } },
  "not-now": { answer: { enum: ["not-now"] }, source: SOURCE },
  "not-kept-by-me": { place: "code" },
  found: { subject: "person", place: "code", band: "code", source: SOURCE },
  away: { subject: "person", place: "code", band: "code", source: SOURCE },
  "agreed-absent": { absentFrom: "minute", absentTo: "minute", source: SOURCE },
  together: { subject: "person", activity: "code", place: "code" },
};
function perceivedShape(where, v) {
  if (!isObj(v)) { fail("lens", `${where} must be an object of codes`); return; }
  const spec = PERCEIVED[v.kind];
  if (!spec) { fail("lens", `${where}.kind ${JSON.stringify(v.kind)} is not a perceived kind (${Object.keys(PERCEIVED).join(", ")})`); return; }
  validate(where, v, { obj: { kind: "code", ...spec } });
}
let shapeCount = 0;
function codesAnywhere(where, v) { // observer-only tables: structured truth at the top level holds ids, codes, numbers, people
  if (Array.isArray(v)) { v.forEach((x, i) => codesAnywhere(`${where}[${i}]`, x)); return; }
  if (isObj(v)) { for (const [k, x] of Object.entries(v)) { if (k === "observer") continue; if (k === "characterLabel") fail("lens", `${where}.characterLabel: observer-only tables carry no character text`); else codesAnywhere(`${where}.${k}`, x); } return; }
  if (typeof v === "string" && !REF.test(v) && !CODE.test(v) && !/^[+-]$/.test(v)) fail("lens", `${where} = ${JSON.stringify(v.slice(0, 60))}: prose outside observer (only ids and codes)`);
}
function validate(where, v, spec) {
  shapeCount++;
  if (v === null || v === undefined) return;
  if (typeof spec === "function") { spec(where, v); return; }
  if (typeof spec === "string") {
    const bad = (what) => fail("lens", `${where} = ${JSON.stringify(v).slice(0, 80)} is not ${what}`);
    switch (spec) {
      case "any": case "observer": case "self": return;
      case "label": if (typeof v !== "string") bad("text"); return;
      case "id": if (typeof v !== "string" || !REF.test(v)) bad("a typed id"); return;
      case "ids": if (!Array.isArray(v) || v.some((x) => typeof x !== "string" || !REF.test(x))) bad("a list of typed ids"); return;
      case "person": if (!Number.isInteger(v)) bad("a person index"); return;
      case "persons": if (!Array.isArray(v) || v.some((x) => !Number.isInteger(x))) bad("a list of person indices"); return;
      case "minute": if (!(v === PRE || Number.isFinite(v))) bad("a minute"); return;
      case "num": if (!Number.isFinite(v)) bad("a number"); return;
      case "bool": if (typeof v !== "boolean") bad("true or false"); return;
      case "code": if (typeof v !== "string" || !CODE.test(v)) bad("a code (no spaces, no prose)"); return;
      case "codes": if (!Array.isArray(v) || v.some((x) => typeof x !== "string" || !CODE.test(x))) bad("a list of codes"); return;
      case "name": if (typeof v !== "string" || !NAME.test(v)) bad("a feeling's name (lowercase words, at most four)"); return;
      case "idOrCode": if (typeof v !== "string" || !(REF.test(v) || CODE.test(v))) bad("an id or a code"); return;
      case "personOrCode": if (!(Number.isInteger(v) || (typeof v === "string" && CODE.test(v)))) bad("a person or a code"); return;
      case "numOrCode": if (!(Number.isFinite(v) || (typeof v === "string" && CODE.test(v)))) bad("a number or a code"); return;
      case "codes-anywhere": codesAnywhere(where, v); return;
      default: throw new Error("unknown spec " + spec);
    }
  }
  if (spec.enum) { if (!spec.enum.includes(v)) fail("lens", `${where} = ${JSON.stringify(v)} is not one of ${spec.enum.join(", ")}`); return; }
  if (spec.arr) { if (!Array.isArray(v)) fail("lens", `${where} must be a list`); else v.forEach((x, i) => validate(`${where}[${i}]`, x, spec.arr)); return; }
  if (spec.obj) {
    if (!isObj(v)) { fail("lens", `${where} must be an object`); return; }
    for (const [k, x] of Object.entries(v)) {
      if (!(k in spec.obj)) fail("lens", `${where} carries ${k} outside observer (not a character-safe field here)`);
      else validate(`${where}.${k}`, x, spec.obj[k]);
    }
  }
}
const COMMON = { id: "self", seq: "num", visibleAt: "minute", characterLabel: "label", observer: "observer" };
const NO_CHARACTER_TEXT = new Set(["outcomes", "comparisons", "threads"]);
const PROSE = new Set(["label", "labels", "title", "summary", "note", "notes", "settledLabel", "notPerceived", "notAsserted", "observerLinks", "qualifications",
  "unitSource", "timingNote", "formalNote", "toNote", "windowBasis", "untilBasis", "placedAtStart", "othersKnowledge", "countOnce", "absentParticipants",
  "reflectionNote", "intention", "attempted", "expects", "effects", "verify", "assumes"]);
function proseWalk(v, path, inObserver) {
  if (Array.isArray(v)) { v.forEach((x, i) => proseWalk(x, `${path}[${i}]`, inObserver)); return; }
  if (!isObj(v)) return;
  for (const [k, x] of Object.entries(v)) {
    if (k === "observer") { proseWalk(x, `${path}.observer`, true); continue; }
    if (!inObserver && PROSE.has(k)) fail("lens", `${path}.${k}: observer prose outside observer (only characterLabel may hold character text)`);
    proseWalk(x, `${path}.${k}`, inObserver);
  }
}
const recordOf = (id) => { const e = index.get(id); return e && e.kind === "records" ? e.obj : null; };
for (const t of TABLES) for (const x of L[t]) {
  const spec = t === "records" ? SPEC[x.type] : SPEC[t];
  if (!spec) continue;
  validate(x.id, x, { obj: { ...COMMON, ...spec } });
  if (NO_CHARACTER_TEXT.has(t) && "characterLabel" in x) fail("lens", `${x.id}: ${t} are observer-only and carry no character text`);
  proseWalk(x, x.id, false);
  if (t === "learning") { // previous and new hold the record's own state shape (6.3) and nothing else; mood is its own field
    const rec = recordOf(x.record);
    if (rec?.type === "expectation") for (const k of ["previous", "new"]) validate(`${x.id}.${k}`, x[k], { obj: { count: "num", lived: "num", seeded: "num", successes: "num", chance: "num" } });
    if (rec?.type === "association") for (const k of ["previous", "new"]) validate(`${x.id}.${k}`, x[k], { obj: { strength: "num" } });
    if (x.mood !== undefined && !Number.isFinite(x.mood?.change)) fail("lens", `${x.id}: mood is {change}, a number`);
  }
  tick("lens");
}
// Observer-only and provenance tables: their prose goes under observer (4.1).
for (const u of L.unresolved) { validate(`unresolved ${u.ref}`, u, { obj: { ref: "id", asOf: "minute", status: { enum: ["open", "settled"] }, observer: "observer" } }); }
for (const s of L.sources) { validate(`source ${s.id}`, s, { obj: { id: "self", kind: "code", key: "label", standing: "label", observer: "observer" } }); proseWalk(s, `source ${s.id}`, false); }
for (const m of L.models) {
  validate(`model ${m.id}`, m, { obj: { id: "self", role: "code", provider: "label", requested: "label", served: "any", revision: "label", server: "label", device: "label",
    reproducibility: "code", service: "any", settings: "any", health: "any", pricing: "any", observer: "observer" } });
  proseWalk(m, `model ${m.id}`, false);
}
if (L.run !== undefined) validate("lives.run", L.run, { obj: { method: { obj: { reflection: { enum: ["authored", "laya", "hybrid", "tape"] }, thought: "code", evaluator: "id", generator: "id", evaluationMode: "code" } },
  source: { obj: { commit: "label", dirty: "bool" } }, tapeSource: { obj: { runId: "label", sha256: "label" } } } });
// Block metadata is not exempt (4.2): settings hold the run's numbers only, and the clock carries no prose key. 0.1's
// settings notes (derivedReadingAfterNote, disappointmentNote) and its Lives flag are gone; block prose goes under
// lives.observer.
const numbersOnly = (v, p) => {
  if (isObj(v)) { for (const [k, x] of Object.entries(v)) numbersOnly(x, `${p}.${k}`); return; }
  if (typeof v !== "number" || !Number.isFinite(v)) fail("lens", `${p} = ${JSON.stringify(v).slice(0, 60)}: settings hold the run's numbers only (prose goes under lives.observer)`);
};
if (isObj(L.settings)) numbersOnly(L.settings, "lives.settings");
if (isObj(L.clock)) proseWalk(L.clock, "lives.clock", false);
// No characterLabel repeats observer-only prose (secret truth, world outcomes, thread titles and summaries, comparison
// prose) verbatim, none names a typed id, and a shared record's label speaks for every knower, not in one knower's
// kinship voice. Label content beyond this needs review: it is not machine-verified (contract 4.1).
{
  const secret = [];
  const collect = (v) => { if (typeof v === "string" && v.length >= 16) secret.push(v); else if (Array.isArray(v)) v.forEach(collect); else if (isObj(v)) Object.values(v).forEach(collect); };
  for (const x of L.perceptions) { collect(x.observer?.notPerceived); collect(x.observer?.notAsserted); collect(x.observer?.notes); }
  for (const t of ["outcomes", "comparisons", "threads"]) for (const x of L[t]) collect(x.observer);
  for (const t of TABLES) for (const x of L[t]) collect(x.observer?.notes);
  const labels = [];
  const find = (v, path) => {
    if (Array.isArray(v)) v.forEach((x, i) => find(x, `${path}[${i}]`));
    else if (isObj(v)) for (const [k, x] of Object.entries(v)) { if (k === "observer") continue; if (k === "characterLabel") labels.push([path, x]); else find(x, `${path}.${k}`); }
  };
  for (const t of TABLES) for (const x of L[t]) find(x, x.id);
  for (const [path, s] of labels) {
    if (typeof s !== "string") { fail("lens", `${path}.characterLabel is not text`); continue; }
    const hit = secret.find((p) => s.includes(p));
    if (hit) fail("lens", `${path}.characterLabel repeats observer-only text: "${hit}"`);
    if (/\b(out|cmp|thr|src|model|lu|rc|dec|opp|att|per|h|rd|cmt|as|ex|fav|inv):\S/.test(s)) fail("lens", `${path}.characterLabel names a typed id`);
  }
  for (const x of [...L.records, ...L.plans]) if ((x.knownBy || []).length > 1 && /\b(Mom|Mum|Dad|my girl|her girl|my boy|my mother|my father)\b/.test(x.characterLabel || ""))
    fail("lens", `${x.id}: a shared record's characterLabel speaks in one knower's kinship voice ("${x.characterLabel}"); it must hold for every knower`);
  tick("lens", labels.length);
}
counts.shapes = shapeCount;

// ---- 5. the clock: every value inside an element is at or before the minute it is shown ----
const MINUTE_KEYS = new Set(["visibleAt", "availableAt", "asOf", "settledAt", "resolvedAt", "at", "from", "to", "until", "arrived", "waitedUntil", "firstSeen",
  "tick", "minute", "since", "through", "absentFrom", "absentTo", "expectedAt", "arrive", "waitUntil", "began"]);
// Terms are intentions (6.1): inside an alternative's template, a plan's window, an attempt's or a constraint's terms,
// only the intended minutes (from, until, arrive, waitUntil) may lie in the future; every other value is gated.
const TERMS = new Set(["template", "window", "terms"]);
const INTENT = new Set(["from", "until", "arrive", "waitUntil"]);
function gateWalk(v, gate, path, key, where, inTerms = false) {
  if (Array.isArray(v)) { v.forEach((x, i) => gateWalk(x, gate, `${path}[${i}]`, key, where, inTerms)); return; }
  if (isObj(v)) { for (const [k, x] of Object.entries(v)) gateWalk(x, gate, `${path}.${k}`, k, where, inTerms || TERMS.has(k)); return; }
  if (MINUTE_KEYS.has(key) && (typeof v === "number" || v === PRE) && !(inTerms && INTENT.has(key)) && T(v) > gate) fail("clock", `${where}: ${path} = ${v} is later than the minute it is shown (${gate})`);
  if (typeof v === "string" && key !== "id" && REF.test(v) && index.has(v) && vis(v) > gate) fail("clock", `${where}: ${path} -> ${v} (visible ${vis(v)}) is later than the minute it is shown (${gate})`);
}
// Declared field gates (contract section 6). Everything else is gated by the element's visibleAt.
function gatesOf(tbl, x) {
  const g = {};
  if (tbl === "decisions") g.choice = x.choice?.visibleAt;
  if (tbl === "attempts") Object.assign(g, { status: x.resolvedAt, resolvedAt: x.resolvedAt, "observer.world": x.resolvedAt, "observer.outcome": x.resolvedAt });
  if (tbl === "outcomes") for (const k of ["settledAt", "kind", "world", "base", "determinedBy", "mustNotWrite", "observer.settledLabel"]) g[k] = x.settledAt;
  return g;
}
function gateElement(x, tbl) {
  const base = T(x.visibleAt), gates = gatesOf(tbl, x);
  const fieldGate = (k) => { if (!(k in gates)) return base; if (gates[k] === undefined) { fail("clock", `${x.id}: ${k} is time-gated but its gate minute is missing`); return -Infinity; } return T(gates[k]); };
  for (const [k, v] of Object.entries(x)) {
    if (k === "id") continue;
    if (k === "knownBy") { for (const kn of v) { if (T(kn.availableAt) < base) fail("clock", `${x.id}: known by ${names[kn.who]} before it exists`); gateWalk(kn, Math.max(base, T(kn.availableAt)), `${x.id}.knownBy`, null, x.id); } continue; }
    if (k === "snapshots") { for (const [i, s] of v.entries()) gateWalk(s, T(s.asOf), `${x.id}.snapshots[${i}]`, null, x.id); continue; }
    if (k === "timeline") {
      let last = base;
      for (const [i, e] of v.entries()) { if (T(e.at) < last) fail("clock", `${x.id}: timeline entry ${i} is out of order or before the element`); last = T(e.at); gateWalk(e, T(e.at), `${x.id}.timeline[${i}]`, null, x.id); }
      continue;
    }
    if (tbl === "threads" && k === "stages") continue; // an index of ids, each shown from its own minute
    if (k === "observer" && isObj(v)) {
      for (const [ok, ov] of Object.entries(v)) {
        // Each thread summary line is shown from its own minute, with the ids it cites (D8: no line ahead of its events).
        if (tbl === "threads" && ok === "summary") { for (const s of ov) gateWalk(s, T(s.visibleAt), `${x.id}.observer.summary`, null, x.id); continue; }
        gateWalk(ov, fieldGate(`observer.${ok}`), `${x.id}.observer.${ok}`, ok, x.id);
      }
      continue;
    }
    gateWalk(v, fieldGate(k), `${x.id}.${k}`, k, x.id, TERMS.has(k));
  }
  tick("clock");
}
for (const t of TABLES) for (const x of L[t]) gateElement(x, t);
for (const u of L.unresolved) { if (u.asOf === undefined) fail("clock", `unresolved ${u.ref}: needs asOf`); else gateWalk(u, T(u.asOf), `unresolved ${u.ref}`, null, `unresolved ${u.ref}`); }
for (const d of L.decisions) {
  if (!d.choice) { if (!pendingRefs.has(d.id)) fail("clock", `${d.id}: no choice, and the decision is not listed as pending (lives.unresolved, status open)`); continue; }
  if (T(d.choice.visibleAt) < T(d.visibleAt)) fail("clock", `${d.id}: its choice is visible before the question was posed`);
}
for (const a of L.attempts) {
  if (a.resolvedAt === undefined) { if (!pendingAttempt(a)) fail("clock", `${a.id}: resolvedAt is missing and the attempt is not pending (lives.unresolved)`); }
  else if (T(a.resolvedAt) < T(a.visibleAt)) fail("clock", `${a.id}: resolvedAt is before the attempt`);
}
for (const o of L.outcomes) {
  if (o.settledAt === undefined) { if (!pendingOutcome(o)) fail("clock", `${o.id}: settledAt is missing and the outcome is not pending (lives.unresolved)`); }
  else if (T(o.settledAt) < T(o.visibleAt)) fail("clock", `${o.id}: settledAt is before the outcome`);
}
for (const p of L.plans) if ("outcome" in p) fail("clock", `${p.id}: a plan never links its outcome (the outcome points at the plan)`);
for (const th of L.threads) for (const s of th.observer?.summary || []) if (!Array.isArray(s.cites) || !s.cites.length) fail("clock", `${th.id}: a summary line cites the elements it summarises (so its minute can be checked)`);
// Causes before effects within a minute: references in creation-gated, character-safe fields to elements shown the
// same minute must have a smaller seq (readers order a minute's elements by seq).
{
  const LATE = { decisions: ["choice"], attempts: ["status", "resolvedAt"], outcomes: ["kind", "world", "base", "determinedBy", "mustNotWrite", "settledAt", "timeline"], threads: ["stages"] };
  const refsOf = (v, out = []) => {
    if (Array.isArray(v)) v.forEach((x) => refsOf(x, out));
    else if (isObj(v)) for (const [k, x] of Object.entries(v)) { if (k !== "observer") refsOf(x, out); }
    else if (typeof v === "string" && REF.test(v)) out.push(v);
    return out;
  };
  const seqs = new Set();
  for (const t of TABLES) for (const x of L[t]) {
    if (seqs.has(x.seq)) fail("clock", `${x.id}: seq ${x.seq} is used twice`); seqs.add(x.seq);
    const skip = new Set(["id", "observer", "knownBy", ...(LATE[t] || [])]);
    for (const [k, v] of Object.entries(x)) {
      if (skip.has(k)) continue;
      for (const r of refsOf(v)) {
        const e = index.get(r);
        if (!e || r === x.id || !TABLES.includes(e.kind) || vis(r) !== T(x.visibleAt)) continue;
        if (!(e.obj.seq < x.seq)) fail("clock", `${x.id} (seq ${x.seq}) cites ${r} (seq ${e.obj.seq}) shown the same minute: a cause must come first`);
      }
    }
  }
  tick("clock");
}
// An excerpt holds what happened inside it (6.4).
if (EXCERPT && Number.isFinite(EXCERPT.from) && Number.isFinite(EXCERPT.to)) {
  for (const t of TABLES) for (const x of L[t]) if (typeof x.visibleAt === "number" && !inWindow(x.visibleAt)) fail("excerpt", `${x.id} at ${x.visibleAt} lies outside the excerpt [${EXCERPT.from}, ${EXCERPT.to}]`);
  for (const a of L.attempts) if (typeof a.resolvedAt === "number" && !inWindow(a.resolvedAt)) fail("excerpt", `${a.id} resolves at ${a.resolvedAt}, outside the excerpt`);
  for (const o of L.outcomes) if (typeof o.settledAt === "number" && !inWindow(o.settledAt)) fail("excerpt", `${o.id} settles at ${o.settledAt}, outside the excerpt`);
  for (const a of acts) if (!inWindow(a[1])) fail("excerpt", `act ${a[0]} at ${a[1]} lies outside the excerpt`);
  for (const [, r] of rowsOf()) if (!inWindow(r.tick)) fail("excerpt", `${r.id ?? "a row"} at ${r.tick} lies outside the excerpt`);
  for (const n of run.needs || []) if (!inWindow(n.tick)) fail("excerpt", `need:${n.id} at ${n.tick} lies outside the excerpt`);
  for (const [, t] of TAPES) if (!inWindow(t.request?.tick)) fail("excerpt", `tape ${t.request?.id} at ${t.request?.tick} lies outside the excerpt`);
  for (const u of L.unresolved) if (typeof u.asOf === "number" && !inWindow(u.asOf)) fail("excerpt", `unresolved ${u.ref} asOf ${u.asOf} lies outside the excerpt`);
  tick("excerpt");
}

// ---- sleep is recorded, never scheduled (contract 6.5; Sid's bedtime decision, 2026-10-14) ----
// A habitual bedtime is an intention: it may end a window or a template, or be a constraint's value. It never makes
// anyone asleep or unavailable. Actual sleep is base data: the moves stream's asleep bit (Replay.cs:94-115, :237,
// :322-339 [built]). So in the fields that state where someone is or why they cannot act (an exclusion's why, a busy
// perception's activity or place, an expectation entry's outcome, an outcome's timeline event or a world span's place),
// a habit's code is refused, and "asleep" holds only for someone the moves stream shows asleep at that minute.
const HABIT_CODE = /(^|[-:])(bed|bedtime|in-bed|to-bed|habitual-bedtime|past-bedtime)($|[-:])/;
const SLEEP_CODE = new Set(["asleep", "sleeping"]);
let sleepSpans = null; // person index -> [[minute, asleep]], decoded on first use
function asleepAt(who, m) {
  if (!sleepSpans) {
    sleepSpans = new Map();
    const tickMinutes = run.clock?.tick ?? 5;
    (Array.isArray(run.moves) ? run.moves : []).forEach((b64, i) => {
      const rows = [];
      try {
        const bytes = Buffer.from(String(b64), "base64");
        let k = 0, tick = 0;
        while (k < bytes.length) {
          let dt = 0, shift = 0, b;
          do { b = bytes[k++]; dt |= (b & 0x7f) << shift; shift += 7; } while (b & 0x80);
          tick += dt;
          const place = bytes[k]; k += 3;
          rows.push([tick * tickMinutes, (place & 0x80) !== 0]);
        }
      } catch { /* an unreadable stream shows nobody asleep */ }
      sleepSpans.set(i, rows);
    });
  }
  let state = false;
  for (const [t, asleep] of sleepSpans.get(who) || []) { if (t > m) break; state = asleep; }
  return state;
}
function sleepClaim(where, code, at, people) {
  if (typeof code !== "string") return;
  if (HABIT_CODE.test(code)) fail("sleep", `${where} = ${code}: a habitual bedtime is an intention, never a state; nobody is asleep or unavailable because the hour has passed (sleep is recorded, not scheduled)`);
  else if (SLEEP_CODE.has(code) && !people.some((p) => asleepAt(p, T(at))))
    fail("sleep", `${where} = ${code} at ${at}: no recorded sleep (the moves stream's asleep bit) for ${people.map((p) => names[p] ?? p).join(" or ") || "anyone"} at that minute`);
}
{
  const everyone = names.map((_, i) => i);
  for (const d of L.decisions) for (const x of d.excluded || []) sleepClaim(`${x.id}.why.code`, x.why?.code, d.visibleAt, everyone.filter((p) => p !== d.holder));
  for (const p of L.perceptions) if (isObj(p.perceived)) for (const k of ["activity", "place"])
    if (k in p.perceived) sleepClaim(`${p.id}.perceived.${k}`, p.perceived[k], p.visibleAt, Number.isInteger(p.perceived.subject) ? [p.perceived.subject] : []);
  for (const r of L.records) if (r.type === "expectation") for (const s of r.snapshots || []) for (const e of s.state?.entries || [])
    if (typeof e.at === "number") sleepClaim(`${r.id} entry at ${e.at}`, e.outcome, e.at, [r.subject]);
  for (const u of L.learning) {
    const r = index.get(u.record)?.obj;
    if (r?.type === "expectation") for (const k of ["added", "dropped"]) if (isObj(u[k]) && typeof u[k].at === "number") sleepClaim(`${u.id}.${k}.outcome`, u[k].outcome, u[k].at, [r.subject]);
  }
  for (const o of L.outcomes) {
    for (const [i, e] of (o.timeline || []).entries()) sleepClaim(`${o.id}.timeline[${i}].event`, e.event, e.at, o.participants || []);
    const spans = Array.isArray(o.world?.spans) ? o.world.spans : [];
    for (const [i, sp] of spans.entries()) if (isObj(sp)) sleepClaim(`${o.id}.world.spans[${i}].place`, sp.place, sp.from, Number.isInteger(sp.who) ? [sp.who] : []);
  }
  tick("sleep");
}

// ---- 6. knowledge and the character lens: every reference in character-safe content is visible to each viewer ----
// Viewers (4.3): a record's or plan's knowers from their availableAt, each seeing only their own knownBy entry; an
// attempt's actor, and anyone holding their own perception of it (who see only its actor, target, kind and terms); the
// holder of an opportunity, recall or decision; the owner of a perception or learning update.
const PERSONAL = new Set(["account", "reading", "commitment", "association", "expectation"]);
function viewersOf(t, x) {
  if (t === "records" || t === "plans") return (x.knownBy || []).map((k) => ({ who: k.who, from: Math.max(T(x.visibleAt), T(k.availableAt)), own: k }));
  if (t === "attempts") return [{ who: x.actor, from: T(x.visibleAt), actor: true },
    ...(perceptionsOf.get(x.id) || []).filter((p) => p.who !== x.actor).map((p) => ({ who: p.who, from: T(p.visibleAt), actor: false }))];
  if (["opportunities", "recalls", "decisions"].includes(t)) return [{ who: x.holder, from: T(x.visibleAt) }];
  if (t === "perceptions" || t === "learning") return [{ who: x.who, from: T(x.visibleAt) }];
  return [];
}
function lensWalk(v, viewer, t, path, where) {
  if (Array.isArray(v)) { v.forEach((x, i) => lensWalk(x, viewer, t, `${path}[${i}]`, where)); return; }
  if (isObj(v)) { for (const [k, x] of Object.entries(v)) if (k !== "observer") lensWalk(x, viewer, t, `${path}.${k}`, where); return; }
  if (typeof v !== "string" || !REF.test(v) || !index.has(v)) return;
  const kind = index.get(v).kind;
  if (OBSERVER_ONLY.has(kind)) { fail("lens", `${where}: ${path} -> ${v}: an observer-only id in character-safe content (put it under observer)`); return; }
  if (!sees(viewer.who, v, t)) fail("lens", `${where}: ${path} -> ${v}, which ${names[viewer.who]} cannot see at ${t} (a reference outside the lens)`);
  const r = index.get(v).obj;
  if (kind === "records" && (r.type === "expectation" || r.type === "association") && firstAsOf(r) > t)
    fail("lens", `${where}: ${path} -> ${v} at ${t}, before its first recorded state (asOf ${firstAsOf(r)})`);
}
function lensElement(t, x) {
  for (const viewer of viewersOf(t, x)) {
    for (const [k, v] of Object.entries(x)) {
      if (["id", "observer"].includes(k)) continue;
      if (t === "attempts" && !viewer.actor && !["actor", "target", "kind", "terms", "visibleAt"].includes(k)) continue; // a perceiver sees only these
      let gate = viewer.from;
      if (t === "decisions" && k === "choice") gate = Math.max(gate, T(v?.visibleAt));
      if (t === "attempts" && k === "status") gate = Math.max(gate, T(x.resolvedAt));
      if (k === "knownBy") { for (const e of v.filter((e) => e.who === viewer.who)) lensWalk(e, viewer, Math.max(gate, T(e.availableAt)), `${x.id}.knownBy`, x.id); continue; } // a knower sees only their own entry
      if (k === "snapshots") { for (const [i, s] of v.entries()) lensWalk(s, viewer, Math.max(gate, T(s.asOf)), `${x.id}.snapshots[${i}]`, x.id); continue; }
      lensWalk(v, viewer, gate, `${x.id}.${k}`, x.id);
    }
    tick("knowledge");
  }
}
for (const t of TABLES) for (const x of L[t]) lensElement(t, x);
// A thought's tape (4.3, 5.4): the holder's own reflection; what the request names was theirs to know at its minute,
// and nothing on the tape is later than the choice it is shown with.
for (const [tapeId, d] of thoughtOwner) {
  const e = index.get(tapeId);
  if (!e || e.kind !== "reflection") continue;
  const req = e.obj.request || {}, gate = T(d.choice.visibleAt);
  for (const r of req.recalled || []) if (!sees(d.holder, r.id, T(req.tick))) fail("lens", `tape ${req.id}: recalled ${r.id}, which ${names[d.holder]} cannot see at ${req.tick}`);
  for (const ev of e.obj.events || []) if (T(ev.tick) > gate) fail("clock", `tape ${req.id}: event at ${ev.tick}, later than the choice it is shown with (${gate})`);
}
for (const r of L.records) if (PERSONAL.has(r.type) && (r.knownBy || []).some((k) => k.who !== r.holder)) fail("knowledge", `${r.id}: a personal ${r.type} is known only by its holder`);
for (const u of L.learning) {
  const rec = index.get(u.record)?.obj;
  if (!rec || !["association", "expectation"].includes(rec.type)) fail("knowledge", `${u.id}: learning changes ${u.record}, not an association or expectation`);
  else if (rec.holder !== u.who) fail("knowledge", `${u.id}: ${names[u.who]} changes ${names[rec.holder]}'s record`);
  if (!(u.evidence || []).length) fail("knowledge", `${u.id}: no evidence`);
  for (const ev of u.evidence || []) { const p = index.get(ev)?.obj; if (!ev.startsWith("per:") || !p || p.who !== u.who) fail("knowledge", `${u.id}: evidence ${ev} is not ${names[u.who]}'s own perception`); }
  // A whereabouts entry (found, away) rests on a sighting of that person, never on a guess (5.1: no inference promoted).
  if (rec?.type === "expectation" && ["found", "away"].includes(u.added?.outcome)) {
    const ok = (u.evidence || []).some((ev) => { const p = index.get(ev)?.obj; return p && ["found", "away"].includes(p.perceived?.kind) && p.perceived.subject === rec.subject
      && !(p.unknown || []).some((x) => x.subject === rec.subject && x.about === "whereabouts"); });
    if (!ok) fail("knowledge", `${u.id}: a ${u.added.outcome} entry for ${names[rec.subject]} rests on no sighting of ${names[rec.subject]} (an inference never becomes an entry)`);
  }
  if (u.interpretation?.formed === true && !(u.interpretation.supportedBy || []).length) fail("knowledge", `${u.id}: a formed interpretation names what supports it (supportedBy: the holder's own ids)`);
  tick("knowledge");
}
// Perceptions (5.2): an ask or its answer is perceived only by the one asked or the one asking, through the attempt;
// of: null only for a passive sighting; an unknown or a guess is about a party to what was perceived.
const ASK_ROLE = { asked: "target", refused: "actor", agreed: "actor", "not-now": "actor", busy: "actor" };
const PLAN_KINDS = new Set(["agreed-absent", "not-kept-by-me", "together"]);
const notAskedAttempt = (a) => a.status === "busy" || [...(a.base || []), ...(a.observer?.rows || [])].some((b) => { const r = index.get(b)?.obj; return r && (r.answer === "busy" || r.askMade === false); });
for (const p of L.perceptions) {
  for (const inf of p.inferences || []) if (inf.establishes !== false) fail("knowledge", `${p.id}: an inference must say establishes: false`);
  for (const u of p.unknown || []) if (u.status !== "unknown") fail("knowledge", `${p.id}: an unknown is marked ${u.status}`);
  const kind = p.perceived?.kind, of = p.of ? index.get(p.of) : null, parties = new Set();
  if (ASK_ROLE[kind]) {
    if (!of || of.kind !== "attempts") fail("knowledge", `${p.id}: perceiving an ask or its answer (${kind}) names the attempt perceived (of): being asked is known only through the ask`);
    else {
      if (of.obj[ASK_ROLE[kind]] !== p.who) fail("knowledge", `${p.id}: ${names[p.who]} perceives ${kind} of ${p.of} but is not its ${ASK_ROLE[kind]}`);
      if (kind === "asked" && notAskedAttempt(of.obj)) fail("knowledge", `${p.id}: ${names[p.who]} perceives being asked by ${p.of}, but no ask was made (busy)`);
    }
  } else if (PLAN_KINDS.has(kind)) {
    if (!of || of.kind !== "plans" || !(of.obj.members || []).some((m) => m.who === p.who)) fail("knowledge", `${p.id}: ${kind} is perceived of a plan ${names[p.who]} is a member of`);
  } else if (kind === "found" || kind === "away") {
    if (p.of != null) fail("knowledge", `${p.id}: a passive sighting perceives no attempt or plan (of: null)`);
    if (p.perceived.subject === p.who) fail("knowledge", `${p.id}: a sighting is of someone else`);
  }
  if (of?.kind === "attempts") { parties.add(of.obj.actor); parties.add(of.obj.target); }
  if (of?.kind === "plans") for (const m of of.obj.members || []) parties.add(m.who);
  for (const k of ["subject", "by"]) if (Number.isInteger(p.perceived?.[k])) parties.add(p.perceived[k]);
  for (const [what, list] of [["unknown", p.unknown], ["inference", p.inferences]]) for (const u of list || [])
    if (!parties.has(u.subject)) fail("knowledge", `${p.id}: an ${what} about ${names[u.subject]}, who is not party to what was perceived`);
  // Where a person is unknown, nothing perceived places them (5.1: an inference is never promoted to a sighting).
  for (const u of p.unknown || []) if (u.about === "whereabouts") {
    const placed = p.perceived?.place !== undefined && (p.perceived.subject === u.subject || (p.perceived.subject === undefined && !["not-kept-by-me", "asked"].includes(kind)));
    if (placed) fail("knowledge", `${p.id}: perceives a place for ${names[u.subject]} while listing their whereabouts as unknown`);
  }
}
// How each knower knows (5.1): pre-run kinds only before the run; in-run knowledge names what gave it (via).
const PRE_HOW = new Set(["CanonStated", "ProductDecision", "Authored", "Seeded"]);
function partyTo(x, id) { // is this id the knower's own: their perception, decision, attempt, update, an act or row they are party to?
  const e = index.get(id);
  if (!e) return false;
  const o = e.obj;
  switch (e.kind) {
    case "perceptions": case "learning": return o.who === x;
    case "decisions": case "opportunities": return o.holder === x;
    case "attempts": return o.actor === x;
    case "act": return o[2] === x || o[7] === x;
    case "favour": case "invitation": return names.indexOf(o.asked) === x && o.askMade !== false && o.answer !== "busy";
    default: return false;
  }
}
for (const r of [...L.records, ...L.plans, ...L.attempts]) {
  for (const k of r.knownBy || []) {
    if (!HOW.includes(k.how)) fail("knowledge", `${r.id}: how ${k.how} is not a known value`);
    const st = r.observer?.standing;
    if (k.how === "ProductDecision" && st !== undefined && st !== "A") fail("knowledge", `${r.id}: known as a product decision but its standing is ${st}, not A`);
    if (k.how === "Authored" && st !== undefined && !/^E/.test(st)) fail("knowledge", `${r.id}: 'Authored' marks our interpretation (E), but its standing is ${st}`);
    if (k.how === "Told" && k.teller === undefined) fail("knowledge", `${r.id}: told, with no teller`);
    if (k.how === "Inferred" && !(k.basis || []).length) fail("knowledge", `${r.id}: inferred, with no basis`);
    if (PRE_HOW.has(k.how) && k.availableAt !== PRE) fail("knowledge", `${r.id}: ${k.how} is pre-run knowledge; in the run a person knows by Lived, Witnessed, Told or Inferred`);
    if (L.attempts.includes(r) || k.availableAt === PRE || !["Lived", "Witnessed", "Told"].includes(k.how)) continue;
    if (!k.via) { fail("knowledge", `${r.id}: ${names[k.who]}'s in-run knowledge (${k.how}) names what gave it (via)`); continue; }
    const e = index.get(k.via);
    if (k.how === "Witnessed" && !(e?.kind === "perceptions" && e.obj.who === k.who)) fail("knowledge", `${r.id}: witnessed knowledge rests on ${names[k.who]}'s own perception, not ${k.via}`);
    if (k.how === "Lived" && !partyTo(k.who, k.via)) fail("knowledge", `${r.id}: lived knowledge rests on ${names[k.who]}'s own act, answer, perception or update, not ${k.via}`);
    if (k.how === "Told" && !(e?.kind === "act" && e.obj[2] === k.teller && e.obj[7] === k.who)) fail("knowledge", `${r.id}: told knowledge rests on a telling act from ${names[k.teller]} to ${names[k.who]} (via), not ${k.via}`);
    if (e && !sees(k.who, k.via, T(k.availableAt))) fail("knowledge", `${r.id}: ${names[k.who]} cannot see ${k.via} at ${k.availableAt}`);
    // An inference never becomes a fact: a fact known through a perception that lists one of its participants under
    // unknown, or only guesses about them, is refused.
    if (r.type === "fact" && e?.kind === "perceptions") {
      const p = e.obj, guessed = new Set([...(p.unknown || []), ...(p.inferences || [])].map((u) => u.subject));
      for (const who of r.participants || []) if (guessed.has(who) && p.perceived?.subject !== who) fail("knowledge", `${r.id}: a fact about ${names[who]} that ${names[k.who]} knows only through ${p.id}, which leaves it unknown or guessed`);
    }
  }
  if (r.observer?.origin === "product-decision" && r.observer?.standing !== "A") fail("knowledge", `${r.id}: origin product-decision needs standing A`);
  if (r.observer?.origin === "authored" && !/^E/.test(r.observer?.standing || "")) fail("knowledge", `${r.id}: origin authored needs standing E`);
}

// ---- 7. attempts: who knows an ask (5.2: being the target is not knowledge) ----
for (const a of L.attempts) {
  const kb = a.knownBy || [];
  if (kb.length !== 1 || kb[0].who !== a.actor || kb[0].how !== "Lived" || kb[0].availableAt !== a.visibleAt)
    fail("attempts", `${a.id}: knownBy is exactly its actor, Lived, from visibleAt; anyone else knows it only through their own perception`);
  for (const p of perceptionsOf.get(a.id) || []) {
    if (p.who === a.actor) continue;
    if (p.who !== a.target) fail("attempts", `${p.id}: ${names[p.who]} perceives ${a.id}, aimed at someone else (a bystander needs a witnessing rule this contract does not define)`);
    else if (notAskedAttempt(a)) fail("attempts", `${p.id}: ${names[p.who]} perceives ${a.id}, but no ask was made (busy): the target perceives nothing`);
  }
  tick("attempts");
}

// ---- 8. expectation and association snapshots; learning replays forward ----
const sumOf = (st) => ({ count: (st.entries || []).length, lived: st.lived, seeded: st.seeded, successes: st.successes, chance: st.chance });
const sameState = (a, b) => ["count", "lived", "seeded", "successes"].every((k) => a[k] === b[k]) && Math.abs(a.chance - b.chance) <= EPS;
for (const r of L.records) {
  if (r.type !== "expectation" && r.type !== "association") { if ("snapshots" in r) fail("snapshots", `${r.id}: only associations and expectations carry snapshots`); continue; }
  const snaps = r.snapshots || [];
  if (!snaps.length) { fail("snapshots", `${r.id}: no recorded state (snapshots)`); continue; }
  for (let i = 1; i < snaps.length; i++) if (T(snaps[i].asOf) < T(snaps[i - 1].asOf)) fail("snapshots", `${r.id}: snapshots out of order`);
  const first = T(snaps[0].asOf), vAt = T(r.visibleAt);
  if (first !== vAt && !(EXCERPT && vAt < T(EXCERPT.from) && first === T(EXCERPT.from)))
    fail("snapshots", `${r.id}: the first snapshot is at its creation (asOf = visibleAt), or at the excerpt's start for an older record`);
  for (const s of snaps) {
    if (r.type === "expectation") {
      const st = s.state || {}, e = st.entries;
      if (!Array.isArray(e)) { fail("snapshots", `${r.id}: an expectation state lists its entries`); continue; }
      const lived = e.filter((x) => x.source !== "Reading").length, seeded = e.length - lived;
      if (st.lived !== lived || st.seeded !== seeded) fail("snapshots", `${r.id}: state says ${st.lived} lived and ${st.seeded} seeded; its entries hold ${lived} and ${seeded}`);
      if (lived > S.livedWindow) fail("bounds", `${r.id}: ${lived} lived entries, more than livedWindow ${S.livedWindow}`);
      if (seeded > 1) fail("bounds", `${r.id}: more than one seeded entry`);
      if (!(st.successes >= 0 && st.successes <= e.length) || !(st.chance > 0 && st.chance <= 1)) fail("snapshots", `${r.id}: successes or chance out of range`);
      for (const x of e) if (x.at === undefined) fail("snapshots", `${r.id}: every entry carries its own minute (at)`);
    } else if (!Number.isFinite(s.state?.strength)) fail("snapshots", `${r.id}: an association state holds its strength`);
  }
  // Replay: snapshots first within a minute, then learning updates by seq; each update starts from the current state.
  const updates = L.learning.filter((u) => u.record === r.id);
  const events = [...snaps.map((s) => ({ t: T(s.asOf), k: 0, s })), ...updates.map((u) => ({ t: T(u.visibleAt), k: 1, seq: u.seq, u }))]
    .sort((a, b) => a.t - b.t || a.k - b.k || (a.seq ?? 0) - (b.seq ?? 0));
  let cur = null, since = 0;
  for (const ev of events) {
    if (ev.s) {
      const st = r.type === "expectation" ? sumOf(ev.s.state || {}) : { strength: ev.s.state?.strength };
      if (cur && since && !(r.type === "expectation" ? sameState(cur, st) : Math.abs(cur.strength - st.strength) <= EPS))
        fail("snapshots", `${r.id}: the snapshot at ${ev.s.asOf} disagrees with the recorded updates before it`);
      if (cur && !since && !deepEqual(cur, st) && !(EXCERPT && ev.t <= T(EXCERPT.from))) fail("snapshots", `${r.id}: the snapshot at ${ev.s.asOf} changes the state with no recorded update`);
      cur = st; since = 0;
      continue;
    }
    const u = ev.u;
    if (!cur) { fail("snapshots", `${u.id}: updates ${r.id} at ${u.visibleAt}, before its first recorded state: updates apply forward only`); continue; }
    if (r.type === "expectation") {
      if (!u.previous || !sameState(cur, u.previous)) fail("snapshots", `${u.id}: previous (${JSON.stringify(u.previous)}) is not ${r.id}'s state then (${JSON.stringify(cur)})`);
      const a = u.added, d = u.dropped;
      if (!a || T(a.at) > T(u.visibleAt)) fail("snapshots", `${u.id}: an update adds one entry, at or before its own minute`);
      const isLived = (x) => x && x.source !== "Reading";
      const pv = u.previous || {};
      const want = { count: pv.count + 1 - (d ? 1 : 0), lived: pv.lived + (isLived(a) ? 1 : 0) - (isLived(d) ? 1 : 0),
        seeded: pv.seeded + (a && !isLived(a) ? 1 : 0) - (d && !isLived(d) ? 1 : 0) };
      for (const k of ["count", "lived", "seeded"]) if (u.new?.[k] !== want[k]) fail("snapshots", `${u.id}: new.${k} is ${u.new?.[k]}; previous, added and dropped give ${want[k]}`);
      if ((u.new?.lived ?? 0) > S.livedWindow) fail("bounds", `${u.id}: ${u.new.lived} lived entries, more than livedWindow ${S.livedWindow}`);
      if ((u.new?.seeded ?? 0) > 1) fail("bounds", `${u.id}: more than one seeded entry`);
      cur = { count: u.new?.count, lived: u.new?.lived, seeded: u.new?.seeded, successes: u.new?.successes, chance: u.new?.chance };
    } else {
      if (!Number.isFinite(u.previous?.strength) || Math.abs(u.previous.strength - cur.strength) > EPS) fail("snapshots", `${u.id}: previous strength ${u.previous?.strength} is not ${r.id}'s ${cur.strength}`);
      if (!Number.isFinite(u.new?.strength)) fail("snapshots", `${u.id}: an association update records its new strength`);
      cur = { strength: u.new?.strength };
    }
    since++;
  }
  tick("snapshots");
}

// ---- 9. bounds against the recording's own settings ----
for (const x of L.recalls) {
  const items = x.items || [];
  if (items.length > R.max) fail("bounds", `${x.id}: ${items.length} recalled, bound ${R.max}`);
  const top = items.filter((i) => i.slot === "top").length, low = items.filter((i) => i.slot === "low-mood").length;
  if (top > R.top) fail("bounds", `${x.id}: ${top} top slots, bound ${R.top}`);
  if (low > (x.observer?.bound?.lowMood ? R.lowMoodExtra : 0)) fail("bounds", `${x.id}: a low-mood slot without low mood`);
  if ((x.observer?.runnersUp || []).length > R.runnersUpShown) fail("bounds", `${x.id}: too many runners-up shown`);
  tick("bounds");
}
for (const [, t] of TAPES) {
  const rec = t.request?.recalled || [];
  if (rec.length > R.max) fail("bounds", `tape ${t.request.id}: recalled list longer than the bound`);
  for (const r of rec) if (Object.keys(r).some((k) => !["id", "origin"].includes(k))) fail("bounds", `tape ${t.request.id}: recalled entries carry more than id and origin`);
}
{
  const per = new Map();
  for (const r of L.records) if (r.type === "association") { const k = `${r.holder}|${r.object}`; per.set(k, (per.get(k) || 0) + 1); }
  for (const [k, n] of per) if (n > S.associationsPerObject) fail("bounds", `${names[+k.split("|")[0]]} holds ${n} associations toward ${k.split("|")[1]}, bound ${S.associationsPerObject}`);
  const layaPerDay = new Map();
  for (const d of L.decisions) if (d.choice?.observer?.backend === "laya") { const k = `${d.holder}:${Math.floor(T(d.visibleAt) / 1440)}`; layaPerDay.set(k, (layaPerDay.get(k) || 0) + 1); }
  for (const [k, n] of layaPerDay) if (n > S.modelQuestionsPerPersonDay) fail("bounds", `${names[+k.split(":")[0]]}: ${n} model questions on day ${k.split(":")[1]}, bound ${S.modelQuestionsPerPersonDay}`);
  tick("bounds");
}

// ---- 10. base rows: one answer each, kept distinct (contract section 8) ----
checkRows(!!EXCERPT);
for (const d of L.decisions) for (const b of d.choice?.base || []) {
  const e = index.get(b);
  if (!e || !["favour", "invitation"].includes(e.kind)) { fail("rows", `${d.id}: base ${b} is not a favours or invitations row`); continue; }
  const r = e.obj, slug = (d.alternatives || []).find((a) => a.id === d.choice.selected)?.slug;
  if (answerOf(r) !== slug) fail("rows", `${d.id}: ${b} records ${answerOf(r)}, the decision chose ${slug}`);
  if (r.tick !== d.choice.visibleAt) fail("rows", `${d.id}: ${b} is at ${r.tick}, the answer at ${d.choice.visibleAt}`);
  if (r.asked !== names[d.holder]) fail("rows", `${d.id}: ${b} was asked of ${r.asked}, not the decision's holder`);
}
{
  const KIND_ANSWER = { "refused-by-commitment": "no", declined: "no", "not-now": "not-now", busy: "busy", agreed: "yes", "agreed-absent": "yes", together: "yes" };
  for (const o of L.outcomes) for (const b of o.base || []) {
    const e = index.get(b);
    if (e && ["favour", "invitation"].includes(e.kind) && KIND_ANSWER[o.kind] && answerOf(e.obj) !== KIND_ANSWER[o.kind]) fail("rows", `${o.id}: ${o.kind} rests on ${b}, whose answer is ${answerOf(e.obj)}`);
  }
}

// ---- 11. the archetype pin (contract section 11) ----
{
  const ci = run.characterInspiration;
  const pinned = L.characters.filter((c) => c.archetype != null);
  const named = [...pinned.map((c) => c.archetype?.id), ...TAPES.map(([, t]) => t.request?.character?.archetypeId)].filter(Boolean);
  const ARCH_KEYS = ["coping", "drives", "evaluatorCue", "id", "sources", "voice"];
  const snapIds = new Map();
  if (named.length) {
    if (!isObj(ci)) fail("pin", "archetypes are named but the recording has no characterInspiration receipt: a reader shows 'Recorded profile unavailable'");
    else {
      if (ci.schema !== 1 || !/^[0-9a-f]{64}$/.test(ci.hash || "")) fail("pin", "characterInspiration needs schema 1 and a 64-hex content hash");
      if (!Array.isArray(ci.snapshot)) fail("pin", "characterInspiration.snapshot is missing: an archetype cannot be resolved without its recorded snapshot (never from today's pack)");
      else {
        const lone = loneSurrogate(ci.snapshot, "snapshot");
        const h = lone ? null : crypto.createHash("sha256").update(canon(ci.snapshot), "utf8").digest("hex");
        if (lone) fail("pin", `${lone} holds a lone surrogate: the snapshot has no canonical form (contract 9.6)`);
        else if (ci.snapshotHash !== h) fail("pin", `snapshotHash ${ci.snapshotHash} is not the SHA-256 of the snapshot's canonical JSON (${h})`);
        for (const a of ci.snapshot) {
          if (!isObj(a) || !sameSet(Object.keys(a), ARCH_KEYS)) fail("pin", `snapshot entry ${a?.id} must hold exactly ${ARCH_KEYS.join(", ")}: no profile, accent, background, assignment or history`);
          if (snapIds.has(a?.id)) fail("pin", `snapshot entry ${a.id} appears twice`);
          snapIds.set(a?.id, a);
        }
        const sorted = [...ci.snapshot].map((a) => a.id).sort(ord);
        if (!deepEqual(sorted, ci.snapshot.map((a) => a.id))) fail("pin", "snapshot entries are in ordinal id order");
        // The snapshot holds every archetype the recording names: its characters' and every request's (11).
        for (const id of new Set(named)) if (!snapIds.has(id)) fail("pin", `archetype ${id} is named in the recording but not in the snapshot`);
      }
      if (ci.artifact !== undefined && (typeof ci.artifact.uri !== "string" || !/^[0-9a-f]{64}$/.test(ci.artifact.sha256 || ""))) fail("pin", "artifact needs a uri and the SHA-256 of its exact bytes");
    }
  }
  for (const c of L.characters) {
    if (Object.keys(c).some((k) => !["who", "name", "archetype", "history"].includes(k))) fail("pin", `${c.name}: characters hold who, name, archetype and history only`);
    if (c.archetype == null) continue;
    if (!isObj(c.archetype) || typeof c.archetype.id !== "string") { fail("pin", `${c.name}: archetype is {id, packHash}`); continue; }
    if (c.archetype.packHash !== ci?.hash) fail("pin", `${c.name}: packHash ${c.archetype.packHash} differs from the receipt's hash: 'Recorded profile unavailable'`);
    for (const id of c.history || []) { const r = index.get(id)?.obj; if (r && r.holder !== c.who && !(r.knownBy || []).some((k) => k.who === c.who)) fail("pin", `${c.name} lists ${id}, which is not theirs`); }
  }
  for (const r of L.records) if ("archetype" in r || r.observer?.archetype) fail("pin", `${r.id} names an archetype: records belong to people, archetypes carry no life`);
  // Every request's character block agrees with the pin (what the models were given).
  for (const [, t] of TAPES) {
    const ch = t.request?.character;
    if (!ch) continue;
    const who = names.indexOf(t.request.actor ?? t.request.holder), c = L.characters.find((x) => x.who === who);
    const a = snapIds.get(ch.archetypeId);
    if (ch.packHash !== ci?.hash) fail("pin", `tape ${t.request.id}: request.character.packHash differs from the receipt`);
    if (c && c.archetype && ch.archetypeId !== c.archetype.id) fail("pin", `tape ${t.request.id}: archetype ${ch.archetypeId}, the character's is ${c.archetype.id}`);
    if (!a) continue;
    if (ch.evaluatorCue !== a.evaluatorCue) fail("pin", `tape ${t.request.id}: evaluatorCue differs from the recorded archetype's`);
    if (!String(ch.voice).startsWith(`Drives: ${a.drives} Coping: ${a.coping} Voice: ${a.voice}`)) fail("pin", `tape ${t.request.id}: the voice does not start with the archetype's composed voice (CharacterInspiration.cs:144-145)`);
    if (!a.sources.every((s) => (ch.sources || []).includes(s))) fail("pin", `tape ${t.request.id}: the request's sources do not include the archetype's`);
    if (ch.provenance !== ci?.provenance) fail("pin", `tape ${t.request.id}: provenance differs from the receipt's`);
  }
  tick("pin");
}

// ---- 12. one option set for both methods; the tape is the request the model saw ----
function setHash(eligible, excluded, constraints) {
  return fnvHash(canon({
    eligible: eligible.map((a) => ({ id: a.id, reasons: (a.reasons || []).map((r) => canon([r.kind, r.code, r.value ?? null])).sort() })).sort((x, y) => ord(x.id, y.id)),
    excluded: excluded.map((x) => canon([x.id, x.why])).sort(),
    constraints: constraints.map((c) => canon([c.code, c.value ?? null, c.terms ?? null])).sort(),
  }));
}
function identityHash(d) { // the option set without reasons: what a comparison holds constant (13)
  return fnvHash(canon({
    eligible: (d.alternatives || []).map((a) => a.slug).sort(ord),
    excluded: (d.excluded || []).map((x) => canon([x.slug, x.why?.code])).sort(),
    constraints: (d.constraints || []).map((c) => canon([c.code, c.value ?? null, c.terms ?? null])).sort(),
  }));
}
const viewOf = (d, reasonsById) => ({
  alternatives: (d.alternatives || []).map((a) => ({ id: a.slug, reasons: ((reasonsById ? reasonsById[a.id] : a.reasons) || []).map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null })) })),
  excluded: (d.excluded || []).map((x) => ({ id: x.slug, why: x.why?.code })),
  constraints: (d.constraints || []).map((c) => ({ code: c.code, value: c.value ?? null, terms: c.terms ?? null })),
});
function tapeAgrees(d, tape) {
  const c = d.choice, p = c.observer.provenance, view = viewOf(d), req = tape.request, slugs = (d.alternatives || []).map((a) => a.slug);
  const lone = loneSurrogate(req, "request");
  if (lone) fail("options", `${d.id}: ${lone} holds a lone surrogate: the request has no canonical form (contract 9.6; the writer refuses it)`);
  else if (fnvHash(canon(req)) !== p.requestHash) fail("options", `${d.id}: requestHash does not match the tape's request (FNV-1a 64 of its canonical JSON, contract 9.6)`);
  if (req.tick !== d.visibleAt) fail("options", `${d.id}: the tape's request is at ${req.tick}`);
  if ((req.actor ?? req.holder) !== names[d.holder]) fail("options", `${d.id}: the tape belongs to ${req.actor ?? req.holder}`);
  for (const k of ["alternatives", "excluded", "constraints"]) if (!deepEqual(req[k], view[k])) fail("options", `${d.id}: the tape's ${k} differ from the decision's`);
  if (req.alternativeSetHash !== p.alternativeSetHash) fail("options", `${d.id}: the tape saw a different option set`);
  if (setHash(req.alternatives || [], req.excluded || [], req.constraints || []) !== req.alternativeSetHash) fail("options", `${d.id}: the tape's alternativeSetHash does not match its own content`);
  // The evaluator sees request.choices (ResilientReflectionMind.cs:277): the same eligible set, refusal and not now included.
  if (!Array.isArray(req.choices) || !sameSet(req.choices.map((x) => x.id), slugs)) fail("options", `${d.id}: the tape's choices (the lines the evaluator sees) are not the eligible set [${slugs}]`);
  if (!sameSet(Object.keys(tape.answer?.weights || {}), slugs)) fail("options", `${d.id}: the tape's weights are not over the eligible set`);
  for (const ev of tape.answer?.evaluations || []) {
    const offered = [...new Set(Object.values(ev.labelsToChoiceIds || {}).flat())];
    if (!sameSet(offered, slugs)) fail("options", `${d.id}: evaluation pass ${ev.pass} offers [${offered}], not every eligible alternative`);
  }
  const slugTo = Object.fromEntries((d.alternatives || []).map((a) => [a.slug, a.id]));
  if (slugTo[tape.choice] !== c.selected) fail("options", `${d.id}: the tape chose ${tape.choice}`);
  const rc = index.get(d.recall)?.obj;
  if (rc && !sameSet((req.recalled || []).map((r) => r.id), (rc.items || []).map((i) => i.record))) fail("options", `${d.id}: the tape's recalled ids differ from the recall`);
  return slugTo;
}
const FALLBACK_REASONS = ["not-torn", "cap", "no-evaluator", "model-failed"];
const EVALUATED = new Set(["authored+laya", "local-llm+laya", "laya"]); // backends whose weights an evaluator gave
for (const d of L.decisions) {
  if (!d.choice) continue; // pending
  const elig = (d.alternatives || []).map((a) => a.id), excl = (d.excluded || []).map((a) => a.id);
  const c = d.choice, o = c.observer || {};
  if (!elig.length) fail("options", `${d.id}: no eligible alternative`);
  if (excl.some((x) => elig.includes(x))) fail("options", `${d.id}: an alternative both eligible and excluded`);
  for (const a of d.excluded || []) if (!a.why?.code) fail("options", `${d.id}: ${a.id} excluded without a reason code`);
  for (const a of d.alternatives || []) for (const r of a.reasons || []) if (!r.code) fail("options", `${a.id}: a reason without its code`);
  if (!elig.includes(c.selected)) fail("options", `${d.id}: selected ${c.selected} is not eligible`);
  if (!["laya", "fallback-keyed-draw"].includes(o.backend)) fail("options", `${d.id}: backend ${o.backend}`);
  const fb = o.fallback?.probabilities || {};
  if (!sameSet(Object.keys(fb), elig)) fail("options", `${d.id}: the fallback's alternatives differ from the eligible set`);
  if (o.backend === "laya" && !sameSet(Object.keys(o.laya?.probabilities || {}), elig)) fail("options", `${d.id}: Laya's alternatives differ from the fallback's`);
  if (!sameSet(Object.keys(o.probabilities || {}), elig)) fail("options", `${d.id}: the used probabilities cover other alternatives`);
  for (const [n, probs] of [["used", o.probabilities || {}], ["fallback", fb], ...(o.laya ? [["laya", o.laya.probabilities || {}]] : [])]) {
    const sum = Object.values(probs).reduce((s, x) => s + x, 0);
    if (Math.abs(sum - 1) > EPS || Object.values(probs).some((x) => !(Number.isFinite(x) && x >= 0))) fail("options", `${d.id}: ${n} probabilities sum to ${sum}`);
  }
  if (!deepEqual(o.backend === "laya" ? o.laya?.probabilities : fb, o.probabilities)) fail("options", `${d.id}: probabilities are not the ${o.backend} ones`);
  const view = viewOf(d);
  if (o.provenance?.alternativeSetHash !== setHash(view.alternatives, view.excluded, view.constraints)) fail("options", `${d.id}: alternativeSetHash does not match its alternatives, reasons, exclusions and constraints`);
  if (!sameSet(o.sampled?.order || [], elig)) fail("options", `${d.id}: the draw's order is not the eligible set`);
  if (!(Number.isFinite(o.sampled?.unit) && o.sampled.unit >= 0 && o.sampled.unit < 1)) fail("options", `${d.id}: unit ${o.sampled?.unit} outside [0, 1)`);
  if (!elig.includes(o.fallback?.wouldSelect)) fail("options", `${d.id}: the fallback's wouldSelect is not eligible`);
  const tape = o.provenance?.tape ? index.get(o.provenance.tape)?.obj : null;
  if (o.provenance?.tape && !tape) fail("options", `${d.id}: tape ${o.provenance.tape} is missing`);
  if (o.backend === "laya") {
    if ("fallbackReason" in o) fail("options", `${d.id}: a Laya answer has no fallbackReason`);
    if (!o.provenance?.model || index.get(o.provenance.model)?.kind !== "model") fail("models", `${d.id}: a model answer names its lives.models entry`);
    if (!tape) fail("options", `${d.id}: a model answer without a tape entry`);
    else {
      const slugTo = tapeAgrees(d, tape);
      if (tape.answer?.fallback || !EVALUATED.has(tape.answer?.backend)) fail("models", `${d.id}: Laya answered, but the tape's answer (${tape.answer?.backend}${tape.answer?.fallback ? ", failed" : ""}) is not an evaluator's`);
      for (const [s, w] of Object.entries(tape.answer.weights || {})) if (!(Math.abs(w - (o.laya?.probabilities?.[slugTo[s]] ?? NaN)) <= EPS)) fail("models", `${d.id}: the tape's weight for ${s} is not Laya's probability`);
      if (tape.answer.model !== o.provenance.model) fail("models", `${d.id}: the tape's model is ${tape.answer.model}`);
    }
  } else {
    // A fallback answer says why (9.3): the choice was not torn, the person-day cap was reached, the run has no
    // evaluator (an authored control), or the model failed (its failed tape is named and records answer.fallback).
    if (!FALLBACK_REASONS.includes(o.fallbackReason)) fail("options", `${d.id}: a fallback answer says why: fallbackReason ${o.fallbackReason} is not one of ${FALLBACK_REASONS.join(", ")}`);
    if (o.fallbackReason === "cap" && !o.cap?.reached) fail("options", `${d.id}: fallbackReason cap without a reached cap`);
    if (o.fallbackReason === "no-evaluator" && L.run?.method?.evaluator) fail("options", `${d.id}: fallbackReason no-evaluator, but the run names an evaluator`);
    if (o.provenance?.model) fail("models", `${d.id}: a fallback draw names a model`);
    const failed = !!tape?.answer?.fallback;
    if (o.fallbackReason === "model-failed" && !failed) fail("options", `${d.id}: fallbackReason model-failed names the failed tape, whose answer records its fallback {stage, code}`);
    if (tape) {
      const isQuestion = o.provenance.tape.startsWith("question:");
      if (isQuestion && o.fallbackReason !== "model-failed") fail("options", `${d.id}: a fallback draw names a question tape only when the model failed on it`);
      else {
        // The tape records what the mind returned (contract 9.5); it is not the answer, the keyed draw over the
        // fallback is. An authored thought keeps the authored baseline's weights (checked with the tape, section 13 of
        // this checker); an evaluator that ran on a choice the rule did not let it answer (not torn, or past the cap)
        // keeps its passes (ResilientReflectionMind evaluates whenever Laya is configured, :56, :85-89); a failed
        // question has no authored proposal, so its weights are the fallback's.
        const slugTo = tapeAgrees(d, tape);
        const evaluated = !failed && EVALUATED.has(tape.answer?.backend);
        if (evaluated && !["not-torn", "cap"].includes(o.fallbackReason))
          fail("options", `${d.id}: an evaluated, unfailed answer (${tape.answer.backend}) is set aside only when the choice is not torn or the cap is reached, not for ${o.fallbackReason}`);
        if (isQuestion) for (const [s, w] of Object.entries(tape.answer?.weights || {})) if (!(Math.abs(w - (fb[slugTo[s]] ?? NaN)) <= EPS))
          fail("options", `${d.id}: a failed question has no authored proposal: its weight for ${s} is the fallback's`);
      }
    }
  }
  // The thought (contract 9.5): the holder's own reflection, shown from the choice's minute, marked imagined.
  if (c.thought) {
    const t = index.get(c.thought.tape);
    if (!t || t.kind !== "reflection") fail("thought", `${d.id}: the thought comes only from a reflection tape`);
    else if (t.obj.request.actor !== names[d.holder]) fail("thought", `${d.id}: the thought's reflection belongs to ${t.obj.request.actor}`);
    else if (t.obj.request.tick > T(c.visibleAt)) fail("thought", `${d.id}: the thought's reflection is later than the choice`);
    if (c.thought.imagined !== true) fail("thought", `${d.id}: a thought is imagined, never evidence`);
    tick("thought");
  }
  tick("options");
}

// ---- 13. models and receipts (contract section 10) ----
{
  const models = new Map(L.models.map((m) => [m.id, m]));
  // A model that never answered in this recording (every call failed: a generator down all run) has nothing served to
  // report; one that answered names what the service reported.
  const answered = new Set(TAPES.filter(([, t]) => t.answer?.backend !== "authored-fallback").flatMap(([, t]) => [t.answer?.model, t.answer?.generatorModel]).filter(Boolean));
  for (const m of L.models) {
    if (!["evaluator", "generator"].includes(m.role)) fail("models", `${m.id}: role ${m.role}`);
    if (!isObj(m.served) || !(typeof m.served.model === "string" || (m.served.model === null && !answered.has(m.id))))
      fail("models", `${m.id}: served.model (what the service reports) is missing${answered.has(m.id) ? ", though it answered in this recording" : ""}`);
  }
  // Backends: [names an evaluator, names a generator]; null: either. Built: authored, authored+laya, local-llm+laya,
  // authored-fallback (Reflection.cs:153, ResilientReflectionMind.cs:89, :103, Simulation.Reflection.cs:281). Proposed:
  // laya (a question tape, choiceQuestions) and local-llm (a thought-only generated reflection, no evaluator).
  const BACKENDS = { authored: [false, false], "authored+laya": [true, false], laya: [true, false], "local-llm+laya": [true, true], "local-llm": [false, true], "authored-fallback": [null, null] };
  const ON_TAPE = { reflection: new Set(["authored", "authored+laya", "local-llm+laya", "local-llm", "authored-fallback"]), question: new Set(["laya", "authored-fallback"]) };
  // Where a model call failed (contract 10): the stage ResilientReflectionMind names (:64, :73, :84), or the simulation's
  // own answer check (Simulation.Reflection.cs:274-281), and a code for what failed.
  const FAILURE = { Laya: ["timeout", "http", "invalid-json", "invalid-data", "io"], "Local generation": ["timeout", "http", "invalid-json", "invalid-data", "io"], Validation: ["invalid-answer"] };
  const ANSWER_KEYS = ["thought", "suggestedChoice", "weights", "backend", "note", "layaPrompt", "generationPrompt", "layaResponse", "evaluations", "generationResponse",
    "model", "generatorModel", "evaluationMode", "receipt", "fallback"];
  // The evaluation passes exactly as ResilientReflectionMind.BuildPasses builds them (:119-147): Raw, one pass labelled
  // by the choices' own ids; otherwise the choices grouped by (kind, line), the groups ordered by kind then line and each
  // group's ids by ordinal comparison, labelled o0, o1, ... by slot; Canonical one pass, Balanced one pass per group with
  // pass r putting group (slot + r) mod n in slot s.
  function expectedPasses(choices, mode) {
    if (mode === "Raw") return [Object.fromEntries(choices.map((c) => [c.id, [c.id]]))];
    const byKey = new Map();
    for (const c of choices) { const k = JSON.stringify([c.kind, c.line]); if (!byKey.has(k)) byKey.set(k, { kind: c.kind, line: c.line, ids: [] }); byKey.get(k).ids.push(c.id); }
    const groups = [...byKey.values()].sort((a, b) => ord(a.kind, b.kind) || ord(a.line, b.line));
    for (const g of groups) g.ids.sort(ord);
    const n = mode === "Balanced" ? groups.length : 1;
    return Array.from({ length: n }, (_, r) => Object.fromEntries(groups.map((_, s) => [`o${s}`, groups[(s + r) % groups.length].ids])));
  }
  // What Laya reads for a label (BuildLayaPrompt, :277; BuildCharacterLayaPrompt, :321-322): "<kind>: <line>", "no act"
  // for an empty kind.
  const criterion = (c) => `${c.kind.length === 0 ? "no act" : c.kind}: ${c.line}`;
  for (const [tapeKind, t] of TAPES) {
    const a = t.answer || {}, where = `tape ${t.request?.id}`, choices = Array.isArray(t.request?.choices) ? t.request.choices : null;
    for (const k of Object.keys(a)) if (!ANSWER_KEYS.includes(k)) fail("models", `${where}: answer.${k} is not a ReflectionAnswer field or a 0.2 addition (replay provenance is lives.run.tapeSource)`);
    if (!(a.backend in BACKENDS)) { fail("models", `${where}: backend ${a.backend}`); continue; }
    if (!ON_TAPE[tapeKind].has(a.backend)) fail("models", `${where}: backend ${a.backend} is not one a ${tapeKind} tape records (${[...ON_TAPE[tapeKind]].join(", ")})`);
    for (const k of ["weights", "backend", "layaPrompt", "layaResponse", "evaluations", "generationPrompt", "generationResponse"]) if (!(k in a)) fail("models", `${where}: answer lacks ${k} (ReflectionAnswer's field; null when unused)`);
    if (!choices || choices.some((c) => typeof c.id !== "string" || typeof c.kind !== "string" || typeof c.line !== "string")) { fail("models", `${where}: request.choices lists {id, kind, line} for every option the mind was given`); continue; }
    const ids = choices.map((c) => c.id), byId = new Map(choices.map((c) => [c.id, c]));
    // Every answer's weights are over exactly the request's choices, normalised as the simulation records them
    // (Simulation.Reflection.cs:276-288).
    const w = a.weights || {}, wsum = ids.reduce((s, k) => s + (w[k] ?? NaN), 0);
    if (!sameSet(Object.keys(w), ids) || ids.some((k) => !(Number.isFinite(w[k]) && w[k] >= 0)) || !(Math.abs(wsum - 1) <= EPS)) fail("models", `${where}: the answer's weights are not normalised over the request's choices (sum ${wsum})`);
    // A failure is recorded explicitly: authored-fallback carries fallback {stage, code, httpStatus}, and nothing else does.
    if (a.backend === "authored-fallback") {
      const f = a.fallback;
      if (!isObj(f) || !FAILURE[f.stage]?.includes(f.code)) fail("models", `${where}: authored-fallback records what failed: fallback {stage: ${Object.keys(FAILURE).join(" | ")}, code} (codes by stage: ${JSON.stringify(FAILURE)})`);
      else if (!(f.httpStatus === null || f.httpStatus === undefined || (f.code === "http" && Number.isInteger(f.httpStatus)))) fail("models", `${where}: fallback.httpStatus is an HTTP status only for code http, else null`);
    } else if (a.fallback !== undefined) fail("models", `${where}: answer.fallback is recorded only on an authored-fallback answer`);
    for (const ev of a.evaluations || []) if (ev.error != null && a.backend !== "authored-fallback") fail("models", `${where}: pass ${ev.pass} failed (${ev.error}) but the answer is not an authored-fallback`);
    // An authored thought's weights are the authored baseline's: 3 for the suggested choice, 1 for each other
    // (Reflection.cs:153), normalised (Simulation.Reflection.cs:284-288); a failed call keeps that baseline
    // (ResilientReflectionMind.cs:101; Simulation.Reflection.cs:280). Never the answer of a Lives choice (9.5).
    if (tapeKind === "reflection" && ["authored", "authored-fallback"].includes(a.backend)) {
      if (!ids.includes(a.suggestedChoice) || !byId.get(a.suggestedChoice)?.kind) fail("models", `${where}: an authored answer suggests one of its executable choices`);
      else for (const id of ids) { const want = (id === a.suggestedChoice ? 3 : 1) / (ids.length + 2); if (!(Math.abs((w[id] ?? NaN) - want) <= EPS)) fail("models", `${where}: ${a.backend} weight ${id} is ${w[id]}; the authored baseline gives ${want.toFixed(4)} (3 for the suggested choice, 1 for each other, normalised)`); }
    }
    const [needsEval, needsGen] = BACKENDS[a.backend];
    if (needsEval === true && models.get(a.model)?.role !== "evaluator") fail("models", `${where}: ${a.backend} names its evaluator (a lives.models evaluator id)`);
    if (needsEval === false && a.model) fail("models", `${where}: ${a.backend} names no evaluator`);
    if (needsEval === null && a.model && models.get(a.model)?.role !== "evaluator") fail("models", `${where}: ${a.model} is not a lives.models evaluator`);
    if (needsGen === true && models.get(a.generatorModel)?.role !== "generator") fail("models", `${where}: ${a.backend} names its generator`);
    if (needsGen === false && a.generatorModel) fail("models", `${where}: ${a.backend} names no generator`);
    if (needsGen === null && a.generatorModel && models.get(a.generatorModel)?.role !== "generator") fail("models", `${where}: ${a.generatorModel} is not a lives.models generator`);
    const ev = a.evaluations || [];
    if (needsEval === false && ev.length) fail("models", `${where}: ${a.backend} records no evaluation passes`);
    if (needsEval || ev.length) {
      const mode = a.evaluationMode;
      if (!["Raw", "Canonical", "Balanced"].includes(mode)) { fail("models", `${where}: evaluationMode ${mode}`); continue; }
      const want = expectedPasses(choices, mode);
      // A complete answer has every pass; a failed one stops at the pass that failed (EvaluateAsync, :160-181).
      if (needsEval === true && ev.length !== want.length) fail("models", `${where}: ${mode} runs ${want.length} pass(es) over ${new Set(choices.map((c) => JSON.stringify([c.kind, c.line]))).size} distinct (kind, line) group(s); recorded ${ev.length}`);
      if (ev.length > want.length) fail("models", `${where}: ${ev.length} passes recorded, ${mode} runs ${want.length}`);
      const complete = [];
      for (const [i, p] of ev.entries()) {
        if (p.pass !== i + 1) fail("models", `${where}: passes are numbered 1, 2, ... in order (found ${p.pass} at ${i + 1})`);
        if (want[i] && !deepEqual(p.labelsToChoiceIds, want[i])) fail("models", `${where}: pass ${p.pass} maps ${JSON.stringify(p.labelsToChoiceIds)}; ResilientReflectionMind builds ${JSON.stringify(want[i])} (labels by slot, groups by (kind, line), rotated by pass)`);
        if (p.error != null) { if (i !== ev.length - 1) fail("models", `${where}: a failed pass is the last one recorded`); if (p.weights != null) fail("models", `${where}: a failed pass has no weights`); continue; }
        const pw = p.weights || {}, psum = ids.reduce((s, k) => s + (pw[k] ?? NaN), 0);
        if (!sameSet(Object.keys(pw), ids) || !(Math.abs(psum - 1) <= EPS)) fail("models", `${where}: pass ${p.pass}'s weights are not normalised over the request's choices (ParseWeights, :413-418; sum ${psum})`);
        // A label's mass is split equally between its group's ids (:172-173).
        for (const [label, gids] of Object.entries(p.labelsToChoiceIds || {})) {
          const ws = gids.map((id) => pw[id]);
          if (ws.some((x) => !(Number.isFinite(x) && Math.abs(x - ws[0]) <= EPS))) fail("models", `${where}: pass ${p.pass} label ${label} splits its mass unequally between [${gids}]`);
        }
        complete.push(pw);
        if (!ILLUSTRATIVE) { // real output: the raw prompt and response are the source of truth (10)
          let prompt, raw;
          try { prompt = JSON.parse(p.prompt); } catch { fail("models", `${where}: pass ${p.pass}'s raw prompt is not the JSON sent to Laya`); }
          try { raw = JSON.parse(p.response); } catch { fail("models", `${where}: pass ${p.pass}'s raw response is not the service's JSON`); }
          if (prompt) {
            const q = prompt.questions?.q, labels = p.labelsToChoiceIds || {};
            const wantCriteria = Object.fromEntries(Object.entries(labels).map(([label, gids]) => [label, byId.get(gids[0]) ? criterion(byId.get(gids[0])) : null]));
            if (q?.type !== "choice" || !deepEqual(q.criteria, wantCriteria)) fail("models", `${where}: pass ${p.pass}'s prompt does not offer exactly the request's choices as Laya reads them ("<kind>: <line>", ResilientReflectionMind.cs:277, :321-322)`);
          }
          if (raw) {
            const probs = raw.answers?.q?.probabilities;
            if (!isObj(probs) || !sameSet(Object.keys(probs), Object.keys(p.labelsToChoiceIds || {}))) fail("models", `${where}: pass ${p.pass}'s raw answers.q.probabilities are not over its labels (ParseWeights, :394-412)`);
            else {
              const total = Object.values(probs).reduce((s, x) => s + x, 0);
              for (const [label, gids] of Object.entries(p.labelsToChoiceIds)) for (const id of gids)
                if (!(Math.abs((pw[id] ?? NaN) - probs[label] / total / gids.length) <= EPS)) fail("models", `${where}: pass ${p.pass}'s weight for ${id} does not follow from the raw probability of ${label} (normalised, split over its group)`);
            }
          }
        }
      }
      if (!ILLUSTRATIVE && ev.length && (a.layaPrompt !== ev.at(-1).prompt || a.layaResponse !== ev.at(-1).response)) fail("models", `${where}: layaPrompt and layaResponse are the last pass's (ResilientReflectionMind.cs:92, :105, :108)`);
      // The answer: one pass's weights as they are, or the normalised mean of the passes' (:184-190).
      if (needsEval === true && complete.length === want.length && complete.length) {
        const mean = Object.fromEntries(ids.map((k) => [k, complete.reduce((s, pw) => s + (pw[k] ?? NaN), 0) / complete.length]));
        const tot = ids.reduce((s, k) => s + mean[k], 0);
        for (const k of ids) { const x = complete.length === 1 ? complete[0][k] : mean[k] / tot; if (!(Math.abs(x - w[k]) <= EPS)) fail("models", `${where}: weight ${k} ${w[k]} does not follow from the passes (${x?.toFixed?.(4)})`); }
      }
      if (needsEval === true) {
        const m = models.get(a.model), rc = a.receipt?.evaluations;
        if (!Array.isArray(rc) || rc.length !== ev.length) fail("models", `${where}: the receipt lists every evaluation pass`);
        else for (const [i, p] of rc.entries()) {
          if (p.pass !== ev[i].pass) fail("models", `${where}: receipt ${i + 1} is for pass ${p.pass}`);
          if (m && (p.servedModel !== m.served.model || (m.served.repo && p.repo !== m.served.repo))) fail("models", `${where}: pass ${p.pass} was served by ${p.servedModel}, the model entry says ${m.served.model}`);
          if (!ILLUSTRATIVE) { // real output: the receipt digests the raw response and must agree with it
            let raw; try { raw = JSON.parse(ev[i].response); } catch { continue; }
            if (raw.routing?.model !== p.servedModel || raw.routing?.repo !== p.repo) fail("models", `${where}: pass ${p.pass}'s receipt disagrees with the raw routing`);
            if (raw.usage?.input_tokens !== p.inputTokens || raw.usage?.state_tokens !== p.stateTokens || raw.usage?.state_tokens_dropped !== p.stateTokensDropped || raw.usage?.truncated !== p.truncated)
              fail("models", `${where}: pass ${p.pass}'s receipt disagrees with the raw usage`);
          }
        }
      }
    }
    if (needsGen === true && !ILLUSTRATIVE) {
      let gp, gr; try { gp = JSON.parse(a.generationPrompt); gr = JSON.parse(a.generationResponse); } catch { fail("models", `${where}: the generator's raw request or response is not JSON`); continue; }
      const g = models.get(a.generatorModel), r = a.receipt?.generation;
      if (g && (gr.model !== g.served.model)) fail("models", `${where}: the generator reported ${gr.model}, the model entry says ${g.served.model}`);
      if (gr.seed !== gp.seed) fail("models", `${where}: the generator's echoed seed differs from the request's`);
      if (!isObj(r)) fail("models", `${where}: a generated thought carries its receipt (receipt.generation)`);
      else if (r.model !== gr.model || r.seed !== gr.seed || r.inputTokens !== (gr.usage?.input_tokens ?? gr.usage?.prompt_tokens) || r.outputTokens !== (gr.usage?.output_tokens ?? gr.usage?.completion_tokens))
        fail("models", `${where}: the generator's receipt disagrees with its raw response (model, seed, tokens)`);
    }
    tick("models");
  }
  const meth = L.run?.method;
  if (!isObj(meth)) fail("models", "lives.run.method is missing");
  else {
    if (["laya", "hybrid"].includes(meth.reflection) && models.get(meth.evaluator)?.role !== "evaluator") fail("models", "lives.run.method names its evaluator");
    if (meth.reflection === "hybrid" && models.get(meth.generator)?.role !== "generator") fail("models", "a hybrid run names its generator");
    if (meth.reflection === "authored" && (meth.evaluator || meth.generator)) fail("models", "an authored control names no model");
    // Replay provenance belongs to the run, not to answers: a replay returns the recorded answer unchanged
    // (Reflection.cs:192), receipts included; the source run's lives.models come with it so every answer's ids resolve.
    if (meth.reflection === "tape" && !(/^run:\d+:\d+:\S+$/.test(L.run.tapeSource?.runId || "") && /^[0-9a-f]{64}$/.test(L.run.tapeSource?.sha256 || ""))) fail("models", "a replayed run names its tape (lives.run.tapeSource {runId, sha256})");
    if (meth.reflection !== "tape" && L.run.tapeSource !== undefined) fail("models", "only a replayed run (method.reflection tape) names a tapeSource");
  }
}

// ---- 14. comparisons: isolated, fallback-only, with an honest status (contract section 13) ----
const NOT_SHOWN = ["hybrid-counterfactual-outcomes", "laya-answers", "later-lives"];
const reasonView = (d) => Object.fromEntries((d.alternatives || []).map((a) => [a.id, (a.reasons || []).map((r) => ({ kind: r.kind, code: r.code, value: r.value ?? null, cites: r.cites || [] }))]));
for (const c of L.comparisons) {
  const d = index.get(c.decision)?.obj;
  const [a, b] = c.arms || [];
  if (c.scope !== "isolated-choice") fail("comparisons", `${c.id}: scope ${c.scope}; only isolated choices are recorded here`);
  if (c.wholeRun?.simulated !== false) fail("comparisons", `${c.id}: arms are never run forward (wholeRun.simulated false)`);
  if (!NOT_SHOWN.every((x) => (c.notShown || []).includes(x))) fail("comparisons", `${c.id}: notShown lists ${NOT_SHOWN.join(", ")}`);
  if (!a || !b || c.arms.length !== 2 || a.id === b.id) { fail("comparisons", `${c.id}: exactly two arms with distinct ids`); continue; }
  const h = c.heldConstant || {};
  for (const k of ["method", "scene", "options", "randomInput", "personalState"]) if (!isObj(h[k])) fail("comparisons", `${c.id}: heldConstant.${k} is missing`);
  if (h.method?.backend !== "fallback-keyed-draw") fail("comparisons", `${c.id}: the method held constant is the fallback`);
  if (d) {
    if (!deepEqual(h.options?.eligible, d.alternatives.map((x) => x.id)) || !deepEqual(h.options?.excluded, (d.excluded || []).map((x) => x.id))
      || h.options?.identityHash !== identityHash(d)) fail("comparisons", `${c.id}: the options held constant (eligible, excluded, identityHash) are not ${d.id}'s`);
    const s = d.choice?.observer?.sampled || {};
    if (!deepEqual(h.randomInput, { stream: s.stream, parts: s.parts, unit: s.unit })) fail("comparisons", `${c.id}: the random input held constant is not ${d.id}'s draw`);
    if (h.scene?.minute !== d.visibleAt) fail("comparisons", `${c.id}: the scene is not ${d.id}'s minute`);
    if (T(c.visibleAt) < T(d.choice?.visibleAt)) fail("comparisons", `${c.id}: shown before ${d.id}'s choice`);
  }
  const removed = (a.history || []).filter((x) => !(b.history || []).includes(x)), added = (b.history || []).filter((x) => !(a.history || []).includes(x));
  if (c.intervention?.field !== "history" || !sameSet(removed, c.intervention?.remove || []) || added.length) fail("comparisons", `${c.id}: the arms' histories differ by [${removed}] and [${added}], not exactly the intervention`);
  for (const arm of [a, b]) for (const k of Object.keys(arm)) if (!["id", "history", "consequences"].includes(k)) fail("comparisons", `${c.id}/${arm.id}: an arm holds only its history and consequences (${k})`);
  const elig = h.options?.eligible || [];
  const DECISION_PARTS = ["recall", "torn", "chance", "sides", "components", "reasons", "setHash", "weights", "probabilities", "sampled"];
  if (c.status === "non-comparable") {
    if (!c.nonComparable?.reason) fail("comparisons", `${c.id}: a non-comparable case states its reason`);
    if ("differs" in c || "unavoidableDifferences" in c) fail("comparisons", `${c.id}: a non-comparable case reports no differences`);
    const gone = [a, b].filter((arm) => arm.consequences?.decisionExists === false);
    if (gone.length !== 1) fail("comparisons", `${c.id}: exactly one arm lacks the decision`);
    for (const arm of gone) {
      if (arm.consequences.opportunity !== null) fail("comparisons", `${c.id}/${arm.id}: without the opportunity, opportunity is null`);
      for (const k of DECISION_PARTS) if (k in arm.consequences) fail("comparisons", `${c.id}/${arm.id}: no matched decision is manufactured (${k})`);
    }
  } else if (c.status === "comparable") {
    if ("nonComparable" in c) fail("comparisons", `${c.id}: a comparable case has no nonComparable reason`);
    for (const arm of [a, b]) {
      const q = arm.consequences || {};
      if (q.decisionExists !== true) fail("comparisons", `${c.id}/${arm.id}: comparable arms both hold the decision`);
      if (d && q.opportunity?.id !== d.opportunity) fail("comparisons", `${c.id}/${arm.id}: the arm's opportunity is not ${d.id}'s`);
      if (!sameSet(Object.keys(q.probabilities || {}), elig)) fail("comparisons", `${c.id}/${arm.id}: probabilities over other alternatives`);
      if (!elig.includes(q.sampled)) fail("comparisons", `${c.id}/${arm.id}: sampled ${q.sampled} is not eligible`);
      // Each arm's option set, its own reasons included, has its own hash; the identity held constant has none.
      if (d) { const v = viewOf(d, q.reasons || {}); if (q.setHash !== setHash(v.alternatives, v.excluded, v.constraints)) fail("comparisons", `${c.id}/${arm.id}: setHash does not match the arm's alternatives and reasons`); }
    }
    const qa = a.consequences || {}, qb = b.consequences || {};
    if (!deepEqual(qa.opportunity, qb.opportunity)) fail("comparisons", `${c.id}: the intervention changes the opportunity: record it as non-comparable`);
    const effects = new Set(["components", "weights", "probabilities", "sampled", "reasons", "setHash"]);
    const listed = new Set((c.unavoidableDifferences || []).map((u) => u.field));
    for (const k of new Set([...Object.keys(qa), ...Object.keys(qb)])) if (!deepEqual(qa[k], qb[k]) && !effects.has(k) && !listed.has(k)) fail("comparisons", `${c.id}: consequence ${k} differs and is not listed as unavoidable`);
    for (const k of listed) if (!(k in qa)) fail("comparisons", `${c.id}: unavoidable difference ${k} is not a consequence`);
    const pd = elig.some((x) => qa.probabilities?.[x] !== qb.probabilities?.[x]);
    if (c.differs?.probabilities !== pd) fail("comparisons", `${c.id}: differs.probabilities should be ${pd}`);
    if (c.differs?.sampledAction !== (qa.sampled !== qb.sampled)) fail("comparisons", `${c.id}: differs.sampledAction should be ${qa.sampled !== qb.sampled}`);
    if (c.differs?.reasons !== !deepEqual(qa.reasons, qb.reasons)) fail("comparisons", `${c.id}: differs.reasons should be ${!deepEqual(qa.reasons, qb.reasons)}`);
  } else fail("comparisons", `${c.id}: status ${c.status}`);
  // The "with" arm holds the recorded decision: its reasons, its option-set hash and fallback probabilities.
  const withArm = [a, b].find((arm) => arm.consequences?.decisionExists && deepEqual([...(arm.history || [])].sort(), [...(c.arms.find((x) => x !== arm).history || []), ...(c.intervention?.remove || [])].sort()));
  if (d && withArm) {
    for (const x of elig) if (!(Math.abs((withArm.consequences.probabilities?.[x] ?? NaN) - (d.choice?.observer?.fallback?.probabilities?.[x] ?? NaN)) <= EPS)) fail("comparisons", `${c.id}: ${withArm.id} does not hold ${d.id}'s fallback probabilities`);
    if (!deepEqual(withArm.consequences.reasons, reasonView(d))) fail("comparisons", `${c.id}: ${withArm.id}'s reasons are not ${d.id}'s`);
    if (withArm.consequences.setHash !== d.choice?.observer?.provenance?.alternativeSetHash) fail("comparisons", `${c.id}: ${withArm.id}'s setHash is not ${d.id}'s alternativeSetHash`);
  } else if (d) fail("comparisons", `${c.id}: one arm holds the full history and the recorded decision`);
  tick("comparisons");
}

// ---- 15. intention, attempt, outcome ----
for (const a of L.attempts) {
  if (a.decision) {
    const d = index.get(a.decision)?.obj;
    if (d && d.choice && a.alternative !== d.choice.selected) fail("intention", `${a.id} attempts ${a.alternative}, not the selected ${d.choice.selected}`);
    if (d && d.holder !== a.actor) fail("intention", `${a.id}: the actor is not the decision's holder`);
    if (d && d.choice && T(a.visibleAt) < T(d.choice.visibleAt)) fail("intention", `${a.id}: attempted before its choice`);
  } else if (!a.decidedBy?.mechanism) fail("intention", `${a.id}: no decision and no decidedBy.mechanism`);
  if (a.status === undefined && !pendingAttempt(a)) fail("intention", `${a.id}: no status and not pending`);
  tick("intention");
}
for (const o of L.outcomes) if (o.kind === undefined && !pendingOutcome(o)) fail("intention", `${o.id}: no kind and not pending`);

// ---- 16. provenance of the records a run starts from (contract 7) ----
const HISTORY = new Set(["fact", "account", "reading", "commitment"]);
const REVIEW = new Set(["proposed", "reviewed", "rejected"]);
for (const r of L.records) {
  const o = r.observer || {};
  if (typeof o.standing !== "string" || typeof o.origin !== "string") fail("provenance", `${r.id}: every record names its standing and origin (observer)`);
  if (r.visibleAt === PRE && HISTORY.has(r.type)) {
    if (!["canon", "product-decision", "authored"].includes(o.origin)) fail("provenance", `${r.id}: a history's origin is canon, product-decision or authored, not ${o.origin}`);
    if (!Array.isArray(o.sources) || !o.sources.length || o.sources.some((s) => index.get(s)?.kind !== "source")) fail("provenance", `${r.id}: a history names its sources (src: ids)`);
    if (!("admission" in o)) fail("provenance", `${r.id}: a history records its admission (R1, R2, or null where the audit's rules do not apply)`);
    else {
      const want = /R1$/.test(o.standing || "") ? "R1" : /R2$/.test(o.standing || "") ? "R2" : null;
      if (o.admission !== want) fail("provenance", `${r.id}: admission ${o.admission} disagrees with standing ${o.standing}`);
    }
    if (!isObj(o.review) || !REVIEW.has(o.review.status)) fail("provenance", `${r.id}: a history records its review {status: proposed | reviewed | rejected, by, date}`);
    else if (o.review.status === "reviewed" && !(typeof o.review.by === "string" && typeof o.review.date === "string")) fail("provenance", `${r.id}: a reviewed history names who reviewed it and when`);
  }
}
// A seeded record names the reading it came from; nothing is seeded from a rejected reading.
for (const r of L.records) {
  if (!(r.knownBy || []).some((k) => k.how === "Seeded")) continue;
  const from = r.type === "association" ? (r.causes || []) : (r.snapshots || []).flatMap((s) => s.state?.entries || []).filter((e) => e.source === "Reading").map((e) => e.ref);
  const readings = from.map((id) => recordOf(id)).filter((x) => x?.type === "reading");
  if (!readings.length) fail("provenance", `${r.id}: a seeded record names the reading it was seeded from`);
  for (const x of readings) if (x.observer?.review?.status === "rejected") fail("provenance", `${r.id}: seeded from ${x.id}, a rejected reading`);
}
tick("provenance");

// ---- report ----
console.log(`checked ${file}`);
console.log(`lives ${L.version}${ILLUSTRATIVE ? " (illustrative)" : " (no illustrative flag)"}: ${L.records.length} records, ${L.decisions.length} decisions, ${L.comparisons.length} comparisons, ${index.size} ids`);
console.log(`structure checks run: ${Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(", ")}`);
if (errors.length) {
  console.log(`FAIL: ${errors.length} problem(s)`);
  for (const e of errors) console.log("  " + e);
  process.exit(1);
}
console.log("PASS: version, excerpt and calendar, ids, references, clock and gates, sleep recorded not scheduled, nested shapes and codes, knowledge and the lens partition, attempts, snapshots, bounds, base rows, archetype pin, option set, models and receipts, comparisons, intention, provenance");
