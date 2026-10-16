// Derives a "real-like" variant of the illustrative fixture, to prove the structure checker never requires what a
// real writer may omit: no illustrative flags or notes, no characterLabels, no observer prose (labels, notes, titles,
// summaries, notPerceived and the like), and raw model text shaped like the services' JSON in place of placeholders,
// with receipts that agree with it. Every number, id, code and structured observer field is kept. The output is a
// test artifact for the mutation suite; it is still invented content and is never published as a recording.
// The raw Laya request and response follow ResilientReflectionMind as built (a9eadaf): the prompt is
// JsonSerializer.Serialize({state, model, questions: {q: {type: "choice", instructions, criteria}}}) with criteria
// {label: "<kind>: <line>"} ("no act" for an empty kind) over the labels BuildPasses assigns (:119-147, :270-353); the
// response is read at answers.q.probabilities (:394-418). The rest of the response (model "laya-rl-agent", usage,
// routing) follows a real recording (out/scene-context-2026-10-12/hybrid-haiku-scene2-seed7-7d.json) and the
// installed laya 0.3.22 serve.py/agent.py as read on 2026-10-14 (VERIFY upstream, AGENTS rule 8). Raw text is escaped
// as System.Text.Json's default encoder writes it (apostrophes and non-ASCII as \uXXXX), as a C# writer would.
// Usage: node make-real-like.cjs <fixture.json> <out.json>
"use strict";
const fs = require("fs");

const PROSE = new Set(["label", "labels", "title", "summary", "note", "notes", "settledLabel", "notPerceived", "notAsserted", "qualifications", "timingNote",
  "formalNote", "toNote", "windowBasis", "placedAtStart", "othersKnowledge", "countOnce", "absentParticipants", "decidedBy", "determinedBy", "effects",
  "unavoidable", "question", "canon", "askParts", "when", "spansFrom", "recurrenceNote", "timeRelative", "effect", "excludes", "capNote", "expects",
  "why", "unitSource", "verify", "observerLinks", "heard"]);
function stripObserver(v) { // inside an observer object: drop prose, keep structured data
  if (Array.isArray(v)) return v.map(stripObserver);
  if (v && typeof v === "object") return Object.fromEntries(Object.entries(v).filter(([k]) => !PROSE.has(k)).map(([k, x]) => [k, stripObserver(x)]));
  return v;
}
function strip(v) { // outside observer: drop characterLabel; strip observer objects
  if (Array.isArray(v)) return v.map(strip);
  if (v && typeof v === "object") {
    const out = {};
    for (const [k, x] of Object.entries(v)) {
      if (k === "characterLabel") continue;
      if (k === "observer") { const o = stripObserver(x); if (Object.keys(o).length) out.observer = o; continue; }
      out[k] = strip(x);
    }
    return out;
  }
  return v;
}

// ---- ResilientReflectionMind's wire format, as built ----
const ord = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
// JSON text as System.Text.Json's default JavaScriptEncoder writes strings: the quote, HTML-sensitive characters and
// every UTF-16 code unit outside printable ASCII as \uXXXX (uppercase hex); backslash and the usual control escapes as
// \\ \n \r \t \b \f. Doubles as .NET's shortest round-trip form writes them: E notation with a signed, at least
// two-digit exponent when the exponent is below -5 or at least 15 (1E-07, 1E+21), -0 as -0. VERIFY against .NET for
// characters and numbers not exercised here; the point is only that these bytes are not canonical (contract 9.6).
function csNumber(v) {
  if (Object.is(v, -0)) return "-0";
  const s = String(v), m = /^(-?)(\d+)(?:\.(\d+))?(?:e([+-]\d+))?$/.exec(s);
  if (!m) return s;
  const [, sign, ip, fp = "", e = "0"] = m;
  let digits, exp;
  if (/^0+$/.test(ip)) { const lead = fp.match(/^0*/)[0].length; digits = fp.slice(lead); exp = Number(e) - lead - 1; }
  else { digits = ip.replace(/^0+/, "") + fp; exp = Number(e) + ip.replace(/^0+/, "").length - 1; }
  digits = digits.replace(/0+$/, "") || "0";
  if (exp >= 15 || exp < -5) return `${sign}${digits[0]}${digits.length > 1 ? "." + digits.slice(1) : ""}E${exp < 0 ? "-" : "+"}${String(Math.abs(exp)).padStart(2, "0")}`;
  return s;
}
function csString(s) {
  let out = '"';
  for (let i = 0; i < s.length; i++) {
    const c = s.charCodeAt(i), ch = s[i];
    const short = { "\\": "\\\\", "\n": "\\n", "\r": "\\r", "\t": "\\t", "\b": "\\b", "\f": "\\f" }[ch];
    if (short) out += short;
    else if (c < 0x20 || c > 0x7e || "\"'<>&+`".includes(ch)) out += "\\u" + c.toString(16).toUpperCase().padStart(4, "0");
    else out += ch;
  }
  return out + '"';
}
function csharpJson(v) {
  if (Array.isArray(v)) return "[" + v.map(csharpJson).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.entries(v).filter(([, x]) => x !== undefined).map(([k, x]) => csString(k) + ":" + csharpJson(x)).join(",") + "}";
  return typeof v === "string" ? csString(v) : typeof v === "number" ? csNumber(v) : JSON.stringify(v);
}
// BuildPasses (:119-147): labels and the ids each carries, per pass.
function passMaps(choices, mode) {
  if (mode === "Raw") return [Object.fromEntries(choices.map((c) => [c.id, [c.id]]))];
  const byKey = new Map();
  for (const c of choices) { const k = JSON.stringify([c.kind, c.line]); if (!byKey.has(k)) byKey.set(k, { kind: c.kind, line: c.line, ids: [] }); byKey.get(k).ids.push(c.id); }
  const groups = [...byKey.values()].sort((a, b) => ord(a.kind, b.kind) || ord(a.line, b.line));
  for (const g of groups) g.ids.sort(ord);
  const count = mode === "Balanced" ? groups.length : 1;
  return Array.from({ length: count }, (_, r) => Object.fromEntries(groups.map((_, s) => [`o${s}`, groups[(s + r) % groups.length].ids])));
}
const criterion = (c) => `${c.kind.length === 0 ? "no act" : c.kind}: ${c.line}`;
const INSTRUCTIONS = { character: "Choose for this person. Read every line. Ideas aren't evidence. Reject or delay is valid.",
  plain: "Which response fits this person? Read each actual line. The thought is imagined, not evidence. Rejecting or delaying is valid." };
// The prompt one pass sends (BuildLayaPrompt :306-312, BuildCharacterLayaPrompt :348-353). The state text here is a
// stand-in; a real one is built from the request's thought, context, memory and scene.
function layaPrompt(request, map, thought, model) {
  const byId = new Map(request.choices.map((c) => [c.id, c]));
  const criteria = Object.fromEntries(Object.entries(map).map(([label, ids]) => [label, criterion(byId.get(ids[0]))]));
  const who = request.actor ?? request.holder;
  const state = request.character
    ? `${who} about ${request.subject}.\nPrivate idea: ${thought ?? ""}\nDisposition: ${request.character.evaluatorCue}\nNow: ${request.context}\nKnown encounter: ${request.memory}`
    : `Person: ${who}. About: ${request.subject ?? request.question}.\nImagined thought: ${thought ?? ""}`;
  return csharpJson({ state, model, questions: { q: { type: "choice", instructions: request.character ? INSTRUCTIONS.character : INSTRUCTIONS.plain, criteria } } });
}
// The service's reply for one pass: probabilities over the labels whose normalised, group-split mass gives the pass's
// weights (ParseWeights :394-418; :172-173).
function layaResponse(map, weights, usage, served) {
  const probs = Object.fromEntries(Object.entries(map).map(([label, ids]) => [label, Math.round(ids.reduce((s, id) => s + weights[id], 0) * 1e6) / 1e6]));
  const best = Object.entries(probs).sort((a, b) => b[1] - a[1])[0];
  return csharpJson({ model: "laya-rl-agent", answers: { q: { type: "choice", choice: best[0], probabilities: probs, confidence: 0.1, answer_confidence: best[1], action: { act_probability: 1 } } },
    usage: { input_tokens: usage.inputTokens, output_tokens: 0, state_tokens: usage.stateTokens, state_tokens_dropped: usage.stateTokensDropped, truncated: usage.truncated, truncated_questions: [] },
    routing: { model: served.model, repo: served.repo, reason: `explicit model='${served.model}'`, detection: null, workflow: null } });
}
// Fills one tape's raw evaluator text from its passes and receipts.
function rawEvaluations(t, served, model) {
  const a = t.answer;
  for (const [i, p] of a.evaluations.entries()) {
    const r = a.receipt?.evaluations?.[i] ?? { inputTokens: 300, stateTokens: 280, stateTokensDropped: 0, truncated: false };
    p.prompt = layaPrompt(t.request, p.labelsToChoiceIds, a.thought, model);
    p.response = p.error ? null : layaResponse(p.labelsToChoiceIds, p.weights, r, served);
  }
  a.layaPrompt = a.evaluations.at(-1).prompt; a.layaResponse = a.evaluations.at(-1).response;
}

function realLike(fixture) {
  const f = JSON.parse(JSON.stringify(fixture));
  const models = Object.fromEntries((f.lives.models || []).map((m) => [m.id, m]));
  for (const t of [...(f.reflections || []), ...(f.choiceQuestions || [])]) {
    const a = t.answer;
    if (a.evaluations) {
      rawEvaluations(t, models[a.model].served, models[a.model].requested);
      a.note = `${a.thought ? "Authored thought; " : ""}Laya evaluated the candidate lines (${a.evaluationMode}, ${a.evaluations.length} pass(es)).`;
    } else a.note = `Authored proposal lives:${t.request.id}; weights favor its proposed response.`;
  }
  delete f.illustrative; delete f.note;
  f.hash = "5d0c1b8e2a7f4c39"; f.label = `--days ${f.days} --reflection laya --evaluation balanced --character-inspiration sim/character-inspiration.json --lives`;
  const L = f.lives;
  delete L.illustrative; delete L.observer; delete L.status;
  L.runId = `run:${f.seed}:${f.days}:${f.hash}`;
  L.run.source = { commit: "779e3b85326d6bf78497c00c731248894e8bfd92", dirty: false };
  // A real recording declares an excerpt only as a storage chunk (contract 2.4, 6.4): the chunk names its adapter.
  if (L.excerpt) L.excerpt = { from: L.excerpt.from, to: L.excerpt.to, chunk: { adapter: "day-chunks", index: 0 } };
  for (const s of L.sources) delete s.note;
  for (const k of ["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons", "threads", "unresolved", "models"])
    L[k] = strip(L[k]);
  return f;
}
module.exports = { realLike, csharpJson, passMaps, criterion, layaPrompt, layaResponse, rawEvaluations };
if (require.main === module) {
  const [src, out] = process.argv.slice(2);
  if (!src || !out) { console.error("usage: node make-real-like.cjs <fixture.json> <out.json>"); process.exit(2); }
  fs.writeFileSync(out, JSON.stringify(realLike(JSON.parse(fs.readFileSync(src, "utf8")))));
  console.log(`wrote ${out}`);
}
