/* Read-only adapter for the proposed Lives 0.1 format. No simulation rules live here. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.UnderGlassChains = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, () => {
  'use strict';
  const TABLES = ['records','opportunities','recalls','decisions','attempts','plans','outcomes','perceptions','learning'];
  const STAGES = {records:'History',opportunities:'Recall',recalls:'Recall',decisions:'Choice',attempts:'Attempt',plans:'Attempt',outcomes:'Outcome',perceptions:'Perceived',learning:'Learned'};
  const time = v => v === 'pre-run' ? -1 : Number.isFinite(v) ? v : Infinity;
  const arrived = (v, tick) => time(v) <= tick;
  const copy = x => JSON.parse(JSON.stringify(x));
  const pick = (x, keys) => Object.fromEntries(keys.filter(k => x[k] !== undefined).map(k => [k, copy(x[k])]));
  const human = s => String(s ?? '').replace(/[-_]/g,' ');
  function open(run) {
    if (run?.format !== 'under-glass-run' || run.version !== 1 || !Array.isArray(run.names)) throw Error('Open a version 1 Under Glass recording.');
    if (!run.lives) return {run, missing:true};
    const l = run.lives;
    if (l.contract !== 'under-glass-lives-observer' || l.version !== '0.1') throw Error(`Lives ${l.version ?? 'unknown'} is not supported. This viewer reads proposed version 0.1 only.`);
    const index = new Map();
    for (const table of TABLES) {
      if (!Array.isArray(l[table])) throw Error(`Missing Lives table: ${table}.`);
      for (const row of l[table]) {
        if (!row || typeof row.id !== 'string' || index.has(row.id) || time(row.visibleAt) === Infinity) throw Error(`Invalid or duplicate Lives record in ${table}.`);
        index.set(row.id, {table, row});
      }
    }
    if (!Array.isArray(l.threads) || !Array.isArray(l.comparisons)) throw Error('Missing Lives threads or comparisons.');
    return {run, lives:l, index, missing:false};
  }
  function known(row, who, tick) { return (row.knownBy ?? []).some(k => k.who === who && arrived(k.availableAt,tick)); }
  function permitted(model, table, row, tick, who, observer) {
    if (!arrived(row.visibleAt,tick)) return false;
    if (observer) return true;
    if (table === 'records' || table === 'plans') return known(row,who,tick);
    if (table === 'outcomes') return false;
    if (table === 'attempts') {
      // Being the target of an unspoken/busy attempt does not grant knowledge of it.
      return row.actor === who || known(row,who,tick) || model.lives.perceptions.some(p => p.who === who && p.of === row.id && arrived(p.visibleAt,tick));
    }
    return (row.holder ?? row.who) === who;
  }
  const capital = s => s ? s[0].toUpperCase()+s.slice(1) : s;
  const place = s => ({'Home:Trailer':'home','Saloon':'the Saloon','Square':'the town square'})[s];
  function expectationLabel(r, names) {
    const subject=names[r.subject] ?? 'this person', parts=String(r.key ?? '').split(':');
    if(['whereabouts','availability'].includes(parts[0])) {
      const location=place(parts.slice(1,-2).join(':')), hours=parts.at(-2), days=parts.at(-1);
      const period={'18-21':'evenings','15-18':'afternoons','10-14':'late mornings and early afternoons'}[hours];
      const schedule={weekday:'weekday',weekend:'weekend'}[days];
      if(location && period && schedule) return `Will ${subject} be ${parts[0]==='availability'?'free ':''}at ${location} on ${schedule} ${period}?`;
    }
    if(r.key==='answers:plan') return `Will ${subject} agree to a plan?`;
    if(r.key==='followThrough:plan') return `Will ${subject} keep a plan?`;
    if(parts[0]==='answers' && parts[1]==='errand' && parts.length===3 && ['beer','flour'].includes(parts[2])) return `Will ${subject} fetch ${parts[2]}?`;
    // Unknown keys stay inspectable, without guessing at a new contract's meaning.
    return `Expectation about ${subject}`;
  }
  function associationLabel(r) {
    const object=String(r.object ?? ''), split=object.indexOf(':'), kind=object.slice(0,split), value=object.slice(split+1);
    const target=kind==='person'?value:kind==='place'?place(value):['activity','situation'].includes(kind)?(value.startsWith('evening-')?'an ':'')+human(value):null;
    if(!target)return 'Remembered feeling';
    const wording={'worried care':`Worried about ${target}`,burdened:`Burdened by ${target}`,wary:`Wary of ${target}`,absorbed:`Absorbed in ${target}`,proud:`Proud of ${target}`,enjoys:`Enjoys ${target}`,uneasy:`Uneasy about ${target}`,fond:`Fond of ${target}`,disappointed:`Disappointed about ${target}`};
    return wording[r.name] ?? `${capital(human(r.name ?? 'Feeling'))} · ${target}`;
  }
  function recordLabel(r, names) {
    return r.label ?? (r.type === 'association' ? associationLabel(r) : r.type === 'expectation' ? expectationLabel(r,names) : human(r.type));
  }
  function personalLabel(label, name) {
    if(!name)return label;
    // Only remove an explicit owner prefix/subject, never a target or a name inside a word.
    if(label.startsWith(`${name}: `))return capital(label.slice(name.length+2));
    const accountPrefix=`${name}'s account: `;
    if(label.startsWith(accountPrefix))return `Account: ${label.slice(accountPrefix.length)}`;
    const rest=label.slice(name.length+1);
    if(label.startsWith(`${name} `) && /^(recalls|asks|waits|heads|leaves|refused|will|is|at)\b/.test(rest))return capital(rest);
    for(const start of ['Does','Will'])if(label.startsWith(`${start} ${name} `))return capital(label.slice(start.length+name.length+2));
    return label.replace(`what does ${name} `,'what to ');
  }
  function project(model, table, r, tick, who, observer) {
    let data = {}, label = r.label ?? human(r.kind ?? table), refs = [];
    const add = (...xs) => { for (const x of xs.flat(Infinity)) if (typeof x === 'string') refs.push(x); };
    if (table === 'records') {
      label = recordLabel(r,model.run.names);
      data = pick(r,['type','stance','certainty','scope','force','object','key','sign','name','strength','qualifications']);
      if (r.type === 'expectation') {
        data.state = arrived(r.initial?.asOf,tick) ? pick(r.initial,['asOf','chance','lived','seeded']) : {status:'Earlier state not recorded in this excerpt'};
        // Apply only recorded updates, never recompute a probability or lose its earlier state.
        for (const u of [...model.lives.learning].sort((a,b)=>time(a.visibleAt)-time(b.visibleAt)))
          if (u.record === r.id && arrived(u.visibleAt,tick)) data.state = pick(u.new,['chance','lived','seeded','entries','strength']);
      }
      add(r.fact, r.causes, typeof r.object === 'string' ? r.object : null);
      if (observer) data.provenance = pick(r,['origin','standing','sources','support','cues']);
    } else if (table === 'opportunities') {
      data = pick(r,['trigger']); add(r.causes);
    } else if (table === 'recalls') {
      data = {items:(r.items ?? []).map(x=>pick(x,['record','matched','slot']))};
      add(r.opportunity, (r.items ?? []).map(x=>x.record));
      if (observer) data.recallDetails = pick(r,['active','bound','runnersUp']);
    } else if (table === 'decisions') {
      data.alternatives = (r.alternatives ?? []).map(a => ({...pick(a,['id','slug','label']), reasons:(a.reasons ?? []).map(z=>pick(z,['kind','code','value','label','cites','effect']))}));
      data.excluded = (r.excluded ?? []).map(a=>({label:a.label, reason:a.why?.code, cites:a.why?.cites ?? []}));
      data.constraints = (r.constraints ?? []).map(c=>pick(c,['code','value']));
      if (arrived(r.choice?.visibleAt,tick)) {
        data.choice = pick(r.choice,['selected']);
        if (observer) Object.assign(data.choice,pick(r.choice,['backend','probabilities','fallback','sampled','provenance']));
        const tape = r.choice.provenance?.tape;
        if (typeof tape === 'string' && tape.startsWith('reflection:')) {
          const reflection = (model.run.reflections ?? []).find(x=>x.request?.id === tape.slice('reflection:'.length));
          if (reflection?.request.actor === model.run.names[r.holder] && typeof reflection.answer?.thought === 'string')
            data.thought = reflection.answer.thought;
        }
      }
      add(r.opportunity,r.recall, ...(r.alternatives ?? []).flatMap(a=>(a.reasons ?? []).flatMap(z=>z.cites ?? [])));
    } else if (table === 'attempts') {
      data = pick(r,['kind','attempted']); add(r.decision);
      if (arrived(r.resolvedAt,tick) && (observer || r.actor === who)) {
        data.status = r.status; if (observer) { data.world = r.world; add(r.outcome); }
      }
    } else if (table === 'plans') {
      data = pick(r,['answer','activity','place','window','formal']); add(r.origin);
    } else if (table === 'outcomes') {
      data.timeline = (r.timeline ?? []).filter(x=>arrived(x.at,tick)).map(x=>pick(x,['at','label']));
      if (arrived(r.settledAt,tick)) { label = r.settledLabel ?? label; Object.assign(data,pick(r,['kind','world'])); }
      else data.status = 'Still unfolding';
      add(r.attempts);
    } else if (table === 'perceptions') {
      data = pick(r,['perceived','unknown']);
      data.inferences = (r.inferences ?? []).map(x=>pick(x,['about','guess','certainty','establishes']));
      add(r.of,r.causes, ...(r.inferences ?? []).map(x=>x.basis ?? []));
      // notPerceived and observerLinks contain privileged truth, not the person's knowledge.
      if (observer) data.observerContext = pick(r,['notPerceived','notAsserted','observerLinks']);
    } else if (table === 'learning') {
      data = {previous:r.previous ? pick(r.previous,['chance','strength','entries']) : null, next:pick(r.new,['chance','strength','entries','moodChange'])};
      add(r.record,r.evidence);
      if (observer) data.rule = r.rule;
    }
    // No generic raw-record inspector: unknown fields and diagnostic prose stay out of character views.
    // Numeric motive annotations and decision-document codes belong in the inspector.
    const withoutAnnotations=label.replace(/ \([+-]\d+(?:\.\d+)?\)/g,'').replace(/ \(Sid's D\d+\)/g,'');
    if(withoutAnnotations!==label){data.recordedLabel=label;label=withoutAnnotations;}
    const owner=r.holder ?? r.who ?? r.actor ?? null;
    if(!observer && owner===who)label=personalLabel(label,model.run.names[who]);
    return {id:r.id, table, stage:STAGES[table], at:r.visibleAt, label, owner, data, refs:[...new Set(refs)]};
  }
  function view(model, {tick, who=0, observer=false, thread=null}) {
    if (model.missing) return {nodes:[],threads:[],moments:[],comparisons:[]};
    const nodes = [];
    for (const [id,{table,row}] of model.index) if (permitted(model,table,row,tick,who,observer)) nodes.push(project(model,table,row,tick,who,observer));
    const byId = new Map(nodes.map(n=>[n.id,n]));
    // Every link and reference label must obey the same lens/clock as its destination.
    for (const n of nodes) {
      n.refs = n.refs.filter(id=>byId.has(id));
      if (n.data.items) n.data.items = n.data.items.filter(x=>byId.has(x.record));
      for (const a of n.data.alternatives ?? []) for (const r of a.reasons) r.cites = (r.cites ?? []).filter(id=>byId.has(id));
      for (const a of n.data.excluded ?? []) a.cites = a.cites.filter(id=>byId.has(id));
    }
    const threads = model.lives.threads.filter(t=>arrived(t.visibleAt,tick)).map((t,i)=> {
      const ids = (t.stages ?? []).flatMap(s=>s.ids).filter(id=>byId.has(id));
      const summary = observer ? (t.summary ?? []).filter(s=>arrived(s.visibleAt,tick)).at(-1)?.label : null;
      const latest = ids.map(id=>byId.get(id)).sort((a,b)=>time(b.at)-time(a.at))[0];
      return {id:t.id, title:observer ? summary ?? `Thread ${i+1}` : latest?.label ?? `Thread ${i+1} · ${model.run.names[who]}`, summary:observer ? summary : latest?.label, ids};
    }).filter(t=>t.ids.length);
    const selected = threads.find(t=>t.id===thread) ?? threads[0];
    const chosen = new Set(selected?.ids ?? []);
    // Include recorded supporting records recursively, not arbitrary nearby events.
    const visit = id => { const n=byId.get(id); for(const r of n?.refs ?? []) if(!chosen.has(r)) { chosen.add(r); visit(r); } };
    [...chosen].forEach(visit);
    return {nodes:nodes.filter(n=>chosen.has(n.id)).sort((a,b)=>time(a.at)-time(b.at)), all:byId, threads, selected,
      comparisons:observer ? model.lives.comparisons.filter(c=>arrived(c.visibleAt,tick) && chosen.has(c.decision)) : []};
  }
  function moments(model, who, observer) {
    if(model.missing) return [];
    const result = new Set([0]);
    for(const {table,row} of model.index.values()) {
      const gates=[row.visibleAt,row.choice?.visibleAt,row.resolvedAt,row.initial?.asOf];
      if(observer) gates.push(row.settledAt,...(row.timeline ?? []).map(x=>x.at));
      for(const g of gates) if(Number.isFinite(g) && permitted(model,table,row,g,who,observer)) result.add(g);
      for(const k of row.knownBy ?? []) if(k.who===who && Number.isFinite(k.availableAt)) result.add(k.availableAt);
    }
    return [...result].sort((a,b)=>a-b);
  }
  function comparison(model,c) {
    if(c.scope !== 'isolated-choice' || c.arms?.length!==2) throw Error('Only recorded two-arm isolated-choice comparisons are supported.');
    const decision=model.index.get(c.decision)?.row;
    const alternatives = decision?.alternatives ?? [];
    const names = Object.fromEntries(alternatives.map(a=>[a.id,a.label]));
    return {id:c.id,question:c.question,intervention:c.intervention,heldConstant:c.heldConstant,validity:c.validity,
      differs:c.differs,runChoice:c.runChoice,wholeRun:c.wholeRun,unavoidableDifferences:c.unavoidableDifferences,
      arms:c.arms.map(a=>({label:a.label,method:a.inputs.method.backend,draw:a.inputs.randomInput.unit,selected:names[a.derived.sampled] ?? a.derived.sampled,
        options:alternatives.map(x=>({id:x.id,label:x.label,probability:a.derived.probabilities[x.id],reasons:a.derived.reasons[x.slug] ?? []}))}))};
  }
  return {open,view,moments,comparison,time,human};
});
