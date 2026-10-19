/* Read-only recording diagnostics. No simulation rules, requests or inferred model identities. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.UnderGlassCoverageModel = api;
})(globalThis, () => {
  'use strict';
  const list = value => Array.isArray(value) ? value : [];
  const canonical = value => JSON.stringify(value, function (_, v) {
    return v && typeof v === 'object' && !Array.isArray(v)
      ? Object.fromEntries(Object.keys(v).sort().map(k => [k, v[k]])) : v;
  });
  function put(map, key, value, kind) {
    if (!key) throw new Error(`A ${kind} has no identity.`);
    if (map.has(key) && canonical(map.get(key)) !== canonical(value))
      throw new Error(`Conflicting ${kind} receipts. Coverage cannot be counted reliably.`);
    map.set(key, value);
  }
  function block(run, key) {
    const value = run[key];
    if (value && (value.version ?? 1) !== 1) throw new Error(`Unsupported ${key} version.`);
    return value;
  }
  function opportunity(decision) {
    const op = decision?.opportunity;
    if (!op?.id || typeof op.holder !== 'string' || !Number.isInteger(op.at))
      throw new Error('A choice is missing its identity, character or time.');
    return op;
  }
  function line(decision, id) {
    return list(decision?.alternatives).find(a => a.option?.id === id)?.option.line
      ?? list(decision?.opportunity?.options).find(o => o.id === id)?.line
      ?? id ?? 'Selection not recorded';
  }
  function index(run) {
    if (run?.format !== 'under-glass-run' || run.version !== 1) throw new Error('Expected a version 1 recording.');
    const evening = block(run, 'livesEvenings'), choices = new Map(), evaluations = new Map();
    for (const c of list(evening?.choices)) {
      const op = opportunity(c.decision);
      put(choices, op.id, c, 'choice');
      if (c.evaluation) put(evaluations, opportunity(c.evaluation.request?.fallback).id, c.evaluation, 'evaluation');
      if (c.evaluation && opportunity(c.evaluation.request?.fallback).id !== op.id)
        throw new Error('An evaluation points to a different choice.');
    }
    for (const e of list(evening?.decisions)) put(evaluations, opportunity(e.request?.fallback).id, e, 'evaluation');
    const rows = [], orphans = [];
    for (const [id, e] of evaluations) {
      const op = opportunity(e.request.fallback);
      if (!e.answer) throw new Error('An evaluation has no answer receipt.');
      if (choices.has(id) && canonical(choices.get(id).decision) !== canonical(e.request.fallback))
        throw new Error('An evaluation disagrees with its recorded choice.');
      if (!choices.has(id)) orphans.push({person:op.holder, at:op.at + 1});
    }
    for (const [id, c] of choices) {
      const op = opportunity(c.decision), evaluation = evaluations.get(id), answer = evaluation?.answer;
      const status = !evaluation ? 'none' : answer.backend === 'laya' ? 'laya'
        : ['authored', 'authored-fallback'].includes(answer.backend) ? 'fallback' : 'unknown';
      const selectedId = evaluation ? evaluation.selectedId : c.decision.selectedId;
      // The asynchronous evening answer applies in the minute after the opportunity.
      rows.push({id, person:op.holder, at:op.at + 1, opportunityAt:op.at,
        phase:c.phase ?? op.trigger, status, selectedId, line:line(c.decision, selectedId),
        fallbackLine:line(c.decision, c.decision.selectedId),
        disagreement:evaluation?.selectedId != null && c.decision.selectedId != null
          ? evaluation.selectedId !== c.decision.selectedId : null,
        called:typeof answer?.called === 'boolean' ? answer.called : null,
        omitted:Array.isArray(answer?.packet?.omitted) ? answer.packet.omitted : null,
        characters:answer?.packet?.characters ?? null, configured:evaluation?.request?.model ?? null,
        model:answer?.model ?? null, reported:answer?.reportedModel ?? null,
        backend:answer?.backend ?? null, note:answer?.note ?? null});
    }
    rows.sort((a,b) => b.at-a.at || a.id.localeCompare(b.id));
    const other = {};
    for (const [key, field] of [['livesRouting','decisions'], ['livesCompany','choices']]) {
      const b = block(run, key), items = new Map(), answers = new Map();
      for (const c of list(b?.[field])) {
        if(c.evaluation && (!c.evaluation.answer || canonical(c.evaluation.request?.fallback)!==canonical(c.decision)))
          throw new Error('An evaluation disagrees with its recorded choice.');
        put(items, opportunity(c.decision).id, c, 'choice');
      }
      for (const a of list(b?.answers)) put(answers, JSON.stringify([a.episodeId,a.holder,a.at]), a, 'company answer');
      other[key] = {recorded:Array.isArray(b?.[field]), answersRecorded:Array.isArray(b?.answers), backend:b?.backend,
        rows:[...items.values()].map(c => ({person:opportunity(c.decision).holder,
          at:opportunity(c.decision).at + (c.evaluation ? 1 : 0), id:opportunity(c.decision).id,
          line:line(c.decision,c.evaluation?.selectedId ?? c.selectedOption ?? c.decision.selectedId),
          backend:c.evaluation?.answer?.backend ?? null,
          resolution:c.resolution ?? null, resolvedAt:c.resolvedAt ?? null,
          note:c.evaluation?.answer?.note ?? null})),
        answers:[...answers.values()].map(a => ({person:a.holder,at:a.at}))};
    }
    const reflections = new Map();
    for (const r of list(run.reflections)) {
      const passes = new Map();
      for (const p of list(r.answer?.evaluations)) put(passes, String(p.pass), p, 'reflection pass');
      put(reflections, r.request?.id, {...r, answer:r.answer && {...r.answer,
        evaluations:r.answer.evaluations == null ? null : [...passes.values()]}}, 'reflection');
    }
    return {rows, orphans, other, settings:evening?.settings ?? {}, partial:run.live?.partial === true,
      liveAt:Number.isFinite(run.live?.at) ? run.live.at : Infinity,
      recorded:Array.isArray(evening?.choices), reflectionsRecorded:Array.isArray(run.reflections),
      reflections:[...reflections.values()].map(r => ({id:r.request.id, person:r.request.actor,
        at:list(r.events).find(e => e.status === 'considered')?.tick ?? null, answer:r.answer}))};
  }
  function view(data, {person='', at=Infinity, whole=false}={}) {
    const limit = Math.min(whole ? Infinity : at, data.liveAt);
    const visible = r => (!person || r.person === person) && Number.isFinite(r.at) && r.at <= limit;
    const rows = data.rows.filter(visible), counts = {laya:0,fallback:0,none:0,unknown:0};
    for (const r of rows) counts[r.status]++;
    const other = Object.fromEntries(Object.entries(data.other).map(([k,b]) => [k, {
      choices:b.recorded ? b.rows.filter(visible).length : null,
      rows:b.rows.filter(visible).map(r=>({...r,resolution:r.resolvedAt != null && r.resolvedAt<=limit ? r.resolution : null})),
      laya:b.rows.filter(r=>visible(r)&&r.backend==='laya').length,
      fallback:b.rows.filter(r=>visible(r)&&['authored','authored-fallback'].includes(r.backend)).length,
      answers:b.answersRecorded ? b.answers.filter(visible).length : null, backend:b.backend ?? null}]));
    const reflections = data.reflections.filter(visible), passes = reflections.flatMap(r => list(r.answer?.evaluations));
    return {rows, counts, total:data.recorded ? rows.length : null, orphans:data.orphans.filter(visible).length,
      evaluated:rows.filter(r => r.status !== 'none').length,
      called:rows.filter(r => r.called === true).length,
      callsUnknown:rows.filter(r => r.status !== 'none' && r.called === null).length,
      omissions:rows.filter(r => r.omitted?.length).length,
      omissionUnknown:rows.filter(r => r.status !== 'none' && r.omitted === null).length,
      changed:rows.filter(r => r.disagreement === true).length,
      comparable:rows.filter(r => r.disagreement !== null).length,
      other, reflections, reflectionCount:data.reflectionsRecorded ? reflections.length : null,
      passes:passes.length, passUnknown:reflections.filter(r => r.answer?.evaluations == null).length,
      passResponses:passes.filter(p => p.response != null).length, passErrors:passes.filter(p => p.error != null).length,
      generated:reflections.filter(r => r.answer?.generationResponse != null).length,
      partial:data.partial, settings:data.settings};
  }
  return {index, view, line};
});
