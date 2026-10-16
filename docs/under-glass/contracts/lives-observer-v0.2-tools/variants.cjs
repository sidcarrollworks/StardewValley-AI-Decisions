// Legitimate real-run shapes for contract v0.2, built from the real-like variant (make-real-like.cjs), so the fault
// suites can prove the structure checker accepts every kind of run the pilot must record, not only the fixture's:
// an authored control, model failures on a torn question and mid-way through a torn reflection's passes, Haiku + Laya
// with the evaluator run on choices that are not torn (as ResilientReflectionMind does today) and with the proposed
// thought-only generator, a hybrid whose generator never answers, quiet non-Lives reflections (one by a person with no archetype), Canonical and Raw evaluation,
// a replayed run, a recording with Lives off, and a storage chunk that ends mid-evening with work pending.
// Each is still invented content (VERIFY: the services' raw text beyond the fields the checker reads), never a recording.
"use strict";
const crypto = require("crypto");
const { realLike, csharpJson, passMaps, rawEvaluations } = require("./make-real-like.cjs");
const { hash } = require("./rng.cjs");

const clone = (x) => JSON.parse(JSON.stringify(x));
function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  return JSON.stringify(v);
}
const fnvText = (s) => "fnv1a64:" + hash([s]).toString(16).padStart(16, "0");
const fnv = (v) => fnvText(canon(v));
const sha = (s) => crypto.createHash("sha256").update(s, "utf8").digest("hex");
const ord = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
const r4 = (x) => Math.round(x * 10000) / 10000;
const dec = (f, id) => f.lives.decisions.find((d) => d.id === id);
const tape = (f, id) => [...(f.reflections || []), ...(f.choiceQuestions || [])].find((t) => t.request.id === id);
const LAYA = "model:laya:typed-decisions";
const D1 = "dec:45190:Penny:0", D2 = "dec:45191:Pam:0", D3 = "dec:45688:Pam:0", D4 = "dec:45708:Penny:0", D6 = "dec:45740:Pam:0";
// The option set's hash, as the checker computes it (contract 9.4, 9.6).
function setHash(eligible, excluded, constraints) {
  return fnv({
    eligible: eligible.map((a) => ({ id: a.id, reasons: (a.reasons || []).map((r) => canon([r.kind, r.code, r.value ?? null])).sort() })).sort((x, y) => ord(x.id, y.id)),
    excluded: excluded.map((x) => canon([x.id, x.why])).sort(),
    constraints: constraints.map((c) => canon([c.code, c.value ?? null, c.terms ?? null])).sort(),
  });
}
// A decision answered by the keyed draw over its fallback instead of Laya.
function toFallback(d, reason) {
  const o = d.choice.observer;
  o.backend = "fallback-keyed-draw"; o.probabilities = { ...o.fallback.probabilities }; delete o.laya;
  o.provenance.model = null; o.fallbackReason = reason; delete o.cap;
}
// The authored baseline AuthoredReflectionMind returns (Reflection.cs:153), normalised (Simulation.Reflection.cs:284-288).
const authoredWeights = (choices, suggested) => Object.fromEntries(choices.map((c) => [c.id, (c.id === suggested ? 3 : 1) / (choices.length + 2)]));
// Invented evaluator passes around a target answer, labelled as BuildPasses labels them; their normalised mean is the target.
function passesAround(choices, mode, target) {
  const maps = passMaps(choices, mode), ids = choices.map((c) => c.id), n = maps.length;
  return maps.map((map, k) => {
    const lead = Object.values(map)[0];
    const w = Object.fromEntries(ids.map((id) => [id, n === 1 ? target[id] : r4(target[id] + (lead.includes(id) ? 0.02 * (ids.length - lead.length) / lead.length : -0.02))]));
    return { pass: k + 1, prompt: null, response: null, labelsToChoiceIds: map, weights: w, error: null };
  });
}
const meanOf = (passes, ids) => { const m = Object.fromEntries(ids.map((id) => [id, passes.reduce((s, p) => s + p.weights[id], 0) / passes.length])); const t = ids.reduce((s, id) => s + m[id], 0); return Object.fromEntries(ids.map((id) => [id, m[id] / t])); };
const receiptOf = (passes) => ({ evaluations: passes.map((p) => ({ pass: p.pass, servedModel: "typed-decisions", repo: "convaiinnovations/laya/typed-decisions",
  inputTokens: 300 + p.pass, stateTokens: 280 + p.pass, stateTokensDropped: 0, truncated: false, serverMs: 30, clientMs: 40 })), generation: null });
function evaluate(f, t, mode, target) {
  const a = t.answer, ids = t.request.choices.map((c) => c.id);
  a.evaluations = passesAround(t.request.choices, mode, target);
  a.weights = a.evaluations.length === 1 ? { ...a.evaluations[0].weights } : meanOf(a.evaluations, ids);
  a.model = LAYA; a.evaluationMode = mode; a.receipt = receiptOf(a.evaluations);
  const m = f.lives.models.find((x) => x.id === LAYA);
  rawEvaluations(t, m.served, m.requested);
}
// A generated thought: the request ResilientReflectionMind sends (:254-267, its seed :263) and a reply shaped like the
// generator service's (a real one is in out/scene-context-2026-10-12/hybrid-haiku-scene2-seed7-7d.json).
const HAIKU = { id: "model:anthropic:haiku", role: "generator", provider: "anthropic", requested: "claude-haiku-5-5", served: { model: "claude-haiku-5-5", repo: null },
  revision: "claude-haiku-5-5", server: "sim claude generator service", device: "anthropic-api", reproducibility: "tape-only" };
function generated(t) {
  const a = t.answer, seed = crypto.createHash("sha256").update(t.request.id, "utf8").digest().readInt32LE(0) & 0x7fffffff;
  a.generationPrompt = csharpJson({ model: HAIKU.requested, messages: [{ role: "system", content: "(instructions)" }, { role: "user", content: "(context)" }],
    temperature: 0.7, seed, max_tokens: 180, stream: false, response_format: { type: "json_object" } });
  const reply = { id: `claude-${seed}`, object: "chat.completion", model: HAIKU.served.model,
    choices: [{ index: 0, message: { role: "assistant", content: JSON.stringify({ thought: a.thought, suggestedChoice: a.suggestedChoice }) }, finish_reason: "stop" }],
    usage: { prompt_tokens: 1400, completion_tokens: 60, total_tokens: 1460, input_tokens: 1400, output_tokens: 60 }, seed, revision: HAIKU.revision, device: "anthropic-api", provider: "anthropic", request_id: `req_${seed}` };
  a.generationResponse = JSON.stringify(reply);
  a.generatorModel = HAIKU.id;
  a.receipt = { ...(a.receipt || { evaluations: null }), generation: { model: reply.model, revision: reply.revision, device: reply.device, provider: reply.provider, requestId: reply.request_id,
    messageId: reply.id, seed, finishReason: "stop", inputTokens: 1400, outputTokens: 60, serviceMs: null, clientMs: null } };
}

const builders = {
  // The authored control (an acceptance run): no models; every torn choice is answered by the fallback because the run
  // has no evaluator, and no question tape exists. D1's selection is kept here although the fallback alone would keep
  // her usual evening: the structure tier does not recompute draws (the rules suite checks only rule 6 on this shape).
  "authored control: no evaluator, every torn choice the fallback's (no-evaluator)": (real) => {
    const f = clone(real), L = f.lives;
    L.models = []; L.run.method = { reflection: "authored", thought: "authored", evaluator: null, generator: null, evaluationMode: null };
    for (const d of L.decisions) if (d.choice.observer.backend === "laya" || d.choice.observer.fallbackReason === "cap") toFallback(d, "no-evaluator");
    for (const id of [D3, D4]) { const p = dec(f, id).choice.observer.provenance; p.tape = null; p.requestHash = null; }
    for (const o of L.opportunities) if (o.observer) delete o.observer.question;
    f.choiceQuestions = [];
    for (const t of f.reflections) {
      const a = t.answer;
      Object.assign(a, { backend: "authored", weights: authoredWeights(t.request.choices, a.suggestedChoice), layaPrompt: null, layaResponse: null, evaluations: null,
        note: `Authored proposal lives:${t.request.id}; weights favor its proposed response.` });
      for (const k of ["model", "evaluationMode", "receipt"]) delete a[k];
    }
    for (const c of L.comparisons) if (c.runChoice) c.runChoice.backend = "fallback-keyed-draw";
    return f;
  },
  // Laya times out on Penny's torn 17:48 question: the fallback answers (model-failed), the failed question tape is named.
  "Laya times out on a torn question (model-failed; the failed tape named)": (real) => {
    const f = clone(real), d = dec(f, D4);
    toFallback(d, "model-failed");
    const t = tape(f, "7315:q:45708:Penny:0"), a = t.answer;
    const slugOf = Object.fromEntries(d.alternatives.map((x) => [x.id, x.slug]));
    a.backend = "authored-fallback"; a.weights = Object.fromEntries(Object.entries(d.choice.observer.fallback.probabilities).map(([k, v]) => [slugOf[k], v]));
    a.evaluations = [{ pass: 1, prompt: null, response: null, labelsToChoiceIds: passMaps(t.request.choices, "Balanced")[0], weights: null,
      error: "Laya Balanced evaluation timed out after 4000 ms; all 2 passes are required." }];
    a.receipt = null; a.fallback = { stage: "Laya", code: "timeout", httpStatus: null };
    a.note = "Laya failed: Laya Balanced evaluation timed out after 4000 ms; all 2 passes are required. Authored fallback replaced the model proposal and weights.";
    const m = f.lives.models[0]; rawEvaluations(t, m.served, m.requested);
    return f;
  },
  // Laya answers pass 1 of Pam's torn 09:11 reflection, then returns HTTP 503: the mind falls back to its authored
  // baseline (ResilientReflectionMind.cs:98-110) and the keyed draw answers (model-failed).
  "Laya fails on pass 2 of a torn reflection (authored-fallback, HTTP 503)": (real) => {
    const f = clone(real), d = dec(f, D2);
    toFallback(d, "model-failed");
    const t = tape(f, "7315:opp:45191:Pam:0"), a = t.answer;
    a.backend = "authored-fallback"; a.weights = authoredWeights(t.request.choices, a.suggestedChoice);
    a.evaluations = [a.evaluations[0], { pass: 2, prompt: null, response: null, labelsToChoiceIds: a.evaluations[1].labelsToChoiceIds, weights: null, error: "HTTP 503." }];
    a.receipt = null; a.fallback = { stage: "Laya", code: "http", httpStatus: 503 };
    a.note = "Laya failed: HTTP 503. Authored fallback replaced the model proposal and weights.";
    const m = f.lives.models[0]; rawEvaluations(t, m.served, m.requested);
    a.evaluations[1].response = JSON.stringify({ detail: "model loading" }); a.layaResponse = a.evaluations[1].response;
    return f;
  },
  // Haiku + Laya as ResilientReflectionMind runs today: every reflection's thought generated, and Laya evaluates every
  // reflection (:56, :85-89), including the two whose choice is not torn; the keyed draw still answers those (not-torn).
  "Haiku + Laya: generated thoughts; Laya also evaluates the choices that are not torn (set aside)": (real) => {
    const f = clone(real), L = f.lives;
    L.models.push(clone(HAIKU));
    L.run.method = { reflection: "hybrid", thought: "generator", evaluator: LAYA, generator: HAIKU.id, evaluationMode: "Balanced" };
    for (const t of f.reflections) {
      const a = t.answer;
      if (a.backend === "authored") {
        const ids = t.request.choices.map((c) => c.id);
        evaluate(f, t, "Balanced", Object.fromEntries(ids.map((id) => [id, id === a.suggestedChoice ? 0.5 : 0.5 / (ids.length - 1)])));
      }
      a.backend = "local-llm+laya"; generated(t);
      a.note = `Local model imagined the thought; Laya evaluated the candidate lines (Balanced, ${a.evaluations.length} pass(es)).`;
    }
    return f;
  },
  // The proposed thought-only path (contract 9.5, 10): on a choice that is not torn the generator imagines the thought
  // and no evaluator is called (backend local-llm).
  "Haiku + Laya with the proposed thought-only path on choices that are not torn (local-llm)": (real) => {
    const f = clone(real), L = f.lives;
    L.models.push(clone(HAIKU));
    L.run.method = { reflection: "hybrid", thought: "generator", evaluator: LAYA, generator: HAIKU.id, evaluationMode: "Balanced" };
    for (const t of f.reflections) {
      const a = t.answer;
      a.backend = a.backend === "authored" ? "local-llm" : "local-llm+laya"; generated(t);
    }
    return f;
  },
  // A hybrid run whose generator never answers (HTTP 503 throughout, as out/reflection-morning-hybrid-acts-only.json
  // records for 82 of 82 reflections): every reflection is an authored-fallback with no evaluator pass (generation comes
  // first, ResilientReflectionMind.cs:71-83); the torn ones are answered model-failed; the generator's entry has nothing
  // served to report. The 09:10 selection is kept, as in the authored control (rule 6 only in the rules suite).
  "a hybrid run whose generator never answers (HTTP 503 throughout)": (real) => {
    const f = clone(real), L = f.lives;
    L.models.push({ ...clone(HAIKU), served: { model: null, repo: null }, revision: null });
    L.run.method = { reflection: "hybrid", thought: "generator", evaluator: LAYA, generator: HAIKU.id, evaluationMode: "Balanced" };
    for (const t of f.reflections) {
      const a = t.answer, d = L.decisions.find((x) => x.choice.observer.provenance.tape === `reflection:${t.request.id}`);
      const seed = crypto.createHash("sha256").update(t.request.id, "utf8").digest().readInt32LE(0) & 0x7fffffff;
      Object.assign(a, { backend: "authored-fallback", weights: authoredWeights(t.request.choices, a.suggestedChoice), layaPrompt: null, layaResponse: null, evaluations: null,
        generationPrompt: csharpJson({ model: HAIKU.requested, messages: [{ role: "system", content: "(instructions)" }, { role: "user", content: "(context)" }], temperature: 0.7, seed, max_tokens: 180, stream: false, response_format: { type: "json_object" } }),
        generationResponse: JSON.stringify({ error: "Service Unavailable" }), generatorModel: HAIKU.id, fallback: { stage: "Local generation", code: "http", httpStatus: 503 },
        note: "Local generation failed: HTTP 503. Authored fallback replaced the model proposal and weights." });
      for (const k of ["model", "evaluationMode", "receipt"]) delete a[k];
      if (d.choice.observer.backend === "laya") toFallback(d, "model-failed");
    }
    return f;
  },
  // Quiet (non-Lives) reflections in the same run: the Newcomer has no character profile, so the request carries no
  // character block (CharacterInspiration.Resolve returns null, CharacterInspiration.cs:126-129; Reflection.cs:112-113);
  // Gus's carries his archetype, which the snapshot holds.
  "quiet non-Lives reflections: one by a person with no archetype, one by Gus": (real) => {
    const f = clone(real);
    f.names.push("Newcomer");
    const choices = [{ id: "gift", kind: "GaveGift", line: "I'd like you to have this. Thank you for what you did." },
      { id: "help", kind: "HelpedSomeone", line: "You were kind to me. Let me give you a hand." },
      { id: "thank", kind: "Thanked", line: "Thank you for what you did. I wanted you to know I noticed." },
      { id: "defer", kind: "", line: "I need to think about this another time." }, { id: "reject", kind: "", line: "No. I don't want to act on this thought." }];
    const quiet = (id, tick, actor, subject, character, thought, suggested) => {
      const t = { request: { id, tick, actor, subject, sourceActId: 388, memory: `At ${tick} you remember ${subject} welcomed you (Welcomed); confidence 1.00.`,
        context: `${actor}: quiet free time.`, choices, source: { kind: "Welcomed", ownDeed: false, valence: 1, regard: 0.1 }, proposal: null, continuity: null, opportunity: null,
        ...(character ? { character } : {}) },
        answer: { thought, suggestedChoice: suggested, weights: null, backend: "authored+laya", note: "Authored thought; Laya evaluated the candidate lines (Balanced, 5 pass(es)).",
          layaPrompt: null, generationPrompt: null, layaResponse: null, evaluations: null, generationResponse: null },
        choice: suggested, events: [{ tick: tick + 1, status: "considered", text: thought, actId: -1 }, { tick: tick + 1, status: "accepted", text: choices.find((c) => c.id === suggested).line, actId: -1 }] };
      evaluate(f, t, "Balanced", { gift: 0.15, help: 0.15, thank: 0.4, defer: 0.15, reject: 0.15 });
      return t;
    };
    f.reflections.push(quiet("7315:43980:Newcomer:0", 43980, "Newcomer", "Gus", null, "Gus was decent to me. Maybe I just thank him.", "thank"));
    f.reflections.push(quiet("7315:44380:Gus:0", 44380, "Gus", "Pam", clone(tape(f, "7315:opp:45740:Gus:0").request.character), "Pam stood me a word of thanks; I could return it.", "thank"));
    return f;
  },
  // Canonical evaluation: one pass over the (kind, line) groups, labelled by slot (ResilientReflectionMind.cs:128-132).
  "Canonical evaluation: one pass over the (kind, line) groups": (real) => {
    const f = clone(real), t = tape(f, "7315:opp:45191:Pam:0");
    evaluate(f, t, "Canonical", t.answer.weights);
    return f;
  },
  // Raw evaluation: one pass labelled by the choices' own ids (:121-123).
  "Raw evaluation: one pass labelled by the choice ids": (real) => {
    const f = clone(real), t = tape(f, "7315:opp:45191:Pam:0");
    evaluate(f, t, "Raw", t.answer.weights);
    return f;
  },
  // A replay from the tape: answers returned unchanged (Reflection.cs:192), receipts included; the run names its tape.
  "a replayed run (method tape, lives.run.tapeSource)": (real) => {
    const f = clone(real);
    f.lives.run.method = { reflection: "tape", thought: null, evaluator: null, generator: null, evaluationMode: null };
    f.lives.run.tapeSource = { runId: real.lives.runId, sha256: sha(JSON.stringify(real)) };
    return f;
  },
  // Lives off: no lives block, no answer additions, no snapshot; base rows as today (no id, answer or askMade), and only
  // rows today's code writes (no busy or not-now rows, and no yes without its act: only Lives makes those).
  "Lives off: no lives block, base rows as today's writer writes them": (real) => {
    const f = clone(real);
    delete f.lives; delete f.choiceQuestions;
    for (const k of ["snapshot", "snapshotHash", "artifact"]) delete f.characterInspiration[k];
    for (const t of f.reflections) for (const k of ["model", "generatorModel", "evaluationMode", "receipt", "fallback"]) delete t.answer[k];
    for (const tbl of ["favours", "invitations"]) f[tbl] = f[tbl].filter((r) => r.answer !== "busy" && r.answer !== "not-now" && r.actId !== null).map((r) => { const { id, answer, askMade, ...rest } = r; return rest; });
    return f;
  },
  // A storage chunk that ends at 18:30 on Thursday: the evening's outcome, Penny's wait and the plan are pending, listed
  // in lives.unresolved (contract 6.1, 6.4); nothing after the chunk's end is recorded.
  "a storage chunk ending mid-evening, with pending elements": (real) => {
    const f = clone(real), L = f.lives, END = 45750;
    const T = (v) => (v === "pre-run" ? -1 : v);
    L.excerpt.to = END;
    for (const t of ["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons", "threads"]) L[t] = L[t].filter((x) => T(x.visibleAt) <= END);
    for (const r of L.records) if (r.snapshots) r.snapshots = r.snapshots.filter((s) => T(s.asOf) <= END);
    for (const a of L.attempts) if (a.resolvedAt > END) { delete a.resolvedAt; delete a.status; if (a.observer) { delete a.observer.world; delete a.observer.outcome; } }
    for (const o of L.outcomes) if (o.settledAt > END) {
      for (const k of ["settledAt", "kind", "world", "base", "determinedBy", "mustNotWrite"]) delete o[k];
      if (o.observer) delete o.observer.settledLabel;
      o.timeline = (o.timeline || []).filter((e) => e.at <= END);
    }
    const live = new Set([...["records", "opportunities", "recalls", "decisions", "attempts", "plans", "outcomes", "perceptions", "learning", "comparisons"].flatMap((t) => L[t].map((x) => x.id)),
      ...L.decisions.flatMap((d) => [...d.alternatives, ...(d.excluded || [])].map((a) => a.id))]);
    const keep = (v) => typeof v !== "string" || !/^(h|acct|rd|cmt|as|ex|opp|rc|dec|alt|att|plan|out|per|lu|cmp):/.test(v) || live.has(v);
    for (const th of L.threads) {
      th.stages = th.stages.map((s) => ({ ...s, ids: s.ids.filter(keep) })).filter((s) => s.ids.length);
      if (th.observer?.summary) th.observer.summary = th.observer.summary.filter((s) => s.visibleAt <= END && s.cites.every(keep));
    }
    for (const c of L.characters) c.history = (c.history || []).filter(keep);
    L.unresolved = [...L.unresolved.filter((u) => !u.ref.startsWith("plan:")).map((u) => ({ ...u, asOf: END })), { ref: "plan:45192:Penny:Pam", asOf: END, status: "open" }];
    f.acts = f.acts.filter((a) => a[1] <= END);
    return f;
  },
};
// The base moves stream (Replay.cs:94-115, :237, :322-339): one base64 stream a person, rows of a varint tick delta, a
// place byte (128 added when asleep), x and y. Penny at home from 18:05 and asleep from `onset` (a 5-minute tick);
// everyone else unrecorded here. Sleep is recorded this way, never inferred from a habitual bedtime (contract 6.5).
function sleepMoves(f, onset) {
  const pack = (rows) => { const out = []; let last = 0; for (const [m, place, x, y, asleep] of rows) { let t = m / 5 - last; last = m / 5;
    while (t >= 0x80) { out.push((t & 0x7f) | 0x80); t >>>= 7; } out.push(t); out.push(place | (asleep ? 0x80 : 0), x, y); } return Buffer.from(out).toString("base64"); };
  const home = f.places.findIndex((p) => p.name === "Home:Trailer"), penny = f.names.indexOf("Penny");
  return f.names.map((_, i) => (i === penny ? pack([[45725, home, 4, 4, false], [onset, home, 2, 2, true]]) : ""));
}
builders["Penny's actual sleep onset in the evening's timeline, backed by the moves stream (sleep recorded, not scheduled)"] = (real) => {
  // PR B's prototype has her body send her to bed before 21:00 on summer Thursdays (median 19:52): here she falls asleep at
  // 20:30, in the window, and the outcome's timeline says so from the recorded onset. The habit's 21:00 changes nothing.
  const f = clone(real);
  f.moves = sleepMoves(f, 45870);
  const o = f.lives.outcomes.find((x) => x.id === "out:45720:Pam:0");
  o.timeline.splice(o.timeline.findIndex((e) => e.event === "window-closes"), 0, { at: 45870, event: "asleep" });
  return f;
};
// Built once from a fixture: [name, recording].
function legitShapes(fixture) {
  const real = realLike(fixture);
  return Object.entries(builders).map(([name, build]) => [name, build(real)]);
}
module.exports = { builders, legitShapes, canon, fnv, fnvText, sha, setHash, toFallback, authoredWeights, sleepMoves, HAIKU, LAYA, D1, D2, D3, D4, D6 };
