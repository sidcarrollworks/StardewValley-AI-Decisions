/* Read-only graph projection for illustrative Lives 0.1 and the runtime evening adapter. */
(function (root, factory) {
  const runtime = typeof module === 'object' && module.exports ? require('./chains-runtime.js') : root.UnderGlassRuntimeChains;
  const api = factory(runtime);
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.UnderGlassChains = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, runtime => {
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
    if(run.lives?.runtime)throw Error('Runtime graphs must be built from evening receipts.');
    const l = run.lives || runtime.adapt(run);
    if (!l) return {run, missing:true};
    if (!l.runtime && (l.contract !== 'under-glass-lives-observer' || l.version !== '0.1')) throw Error(`Lives ${l.version ?? 'unknown'} is not supported. This viewer reads proposed version 0.1 only.`);
    const index = new Map();
    for (const table of TABLES) {
      if (!Array.isArray(l[table])) throw Error(`Missing Lives table: ${table}.`);
      for (const row of l[table]) {
        if (!row || typeof row.id !== 'string' || index.has(row.id) || time(row.visibleAt) === Infinity) throw Error(`Invalid or duplicate Lives record in ${table}.`);
        index.set(row.id, {table, row});
      }
    }
    if (!Array.isArray(l.threads) || !Array.isArray(l.comparisons)) throw Error('Missing Lives threads or comparisons.');
    const learningByRecord=new Map(), perceptionsByOf=new Map();
    for(const row of l.learning){if(!learningByRecord.has(row.record))learningByRecord.set(row.record,[]);learningByRecord.get(row.record).push(row);}
    for(const list of learningByRecord.values())list.sort((a,b)=>time(a.visibleAt)-time(b.visibleAt));
    for(const row of l.perceptions){if(!perceptionsByOf.has(row.of))perceptionsByOf.set(row.of,[]);perceptionsByOf.get(row.of).push(row);}
    return {run, lives:l, index, learningByRecord, perceptionsByOf, missing:false};
  }
  function known(row, who, tick) { return (row.knownBy ?? []).some(k => k.who === who && arrived(k.availableAt,tick)); }
  function permitted(model, table, row, tick, who, observer) {
    if (!arrived(row.visibleAt,tick)) return false;
    if (observer) return true;
    if (table === 'records' || table === 'plans') return known(row,who,tick);
    if (table === 'outcomes') return false;
    if (table === 'attempts') {
      // Being the target of an unspoken/busy attempt does not grant knowledge of it.
      return row.actor === who || known(row,who,tick) || (model.perceptionsByOf.get(row.id)??[]).some(p => p.who === who && arrived(p.visibleAt,tick));
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
    let data = {}, label = r.label ?? human(r.kind ?? table), refs = [], updatedAt=time(r.visibleAt);
    const updated=v=>{if(arrived(v,tick))updatedAt=Math.max(updatedAt,time(v));};
    if(!observer && (['records','plans'].includes(table) || (table==='attempts' && r.actor!==who))) {
      const acquired=(r.knownBy??[]).filter(k=>k.who===who && arrived(k.availableAt,tick)).map(k=>time(k.availableAt));
      if(table==='attempts')acquired.push(...(model.perceptionsByOf.get(r.id)??[]).filter(p=>p.who===who && arrived(p.visibleAt,tick)).map(p=>time(p.visibleAt)));
      if(acquired.length)updated(Math.min(...acquired));
    }
    const add = (...xs) => { for (const x of xs.flat(Infinity)) if (typeof x === 'string') refs.push(x); };
    if (table === 'records') {
      label = recordLabel(r,model.run.names);
      data = pick(r,['type','stance','certainty','scope','force','object','key','sign','name','strength','qualifications']);
      if (r.type === 'expectation') {
        updated(r.initial?.asOf);
        data.state = arrived(r.initial?.asOf,tick) ? pick(r.initial,['asOf','chance','lived','seeded']) : {status:'Earlier state not recorded in this excerpt'};
        // Apply only recorded updates, never recompute a probability or lose its earlier state.
        for (const u of (model.learningByRecord.get(r.id)??[]))
          if (u.record === r.id && arrived(u.visibleAt,tick)) {data.state = pick(u.new,['chance','lived','seeded','entries','strength']);updated(u.visibleAt);}
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
        updated(r.choice.visibleAt);
        data.choice = pick(r.choice,['selected']);
        if (observer) Object.assign(data.choice,pick(r.choice,['backend','probabilities','fallback','sampled','provenance','modelWeights']));
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
      data.inProgress=observer || r.actor===who;
      if(observer||r.actor===who){const progress=(r.progress??[]).filter(p=>arrived(p.at,tick)).at(-1);if(progress){data.progress=progress.status;updated(progress.at);}}
      if (arrived(r.resolvedAt,tick) && (observer || r.actor === who)) {
        updated(r.resolvedAt);data.inProgress=false;
        data.status = r.status; if (observer) { data.world = r.world; add(r.outcome); }
        const perception=(model.perceptionsByOf.get(r.id)??[]).filter(p=>p.who===r.actor && arrived(p.visibleAt,tick)).sort((a,b)=>time(b.visibleAt)-time(a.visibleAt))[0];
        if(perception){data.response=pick(perception.perceived,['answer','kind']);updated(perception.visibleAt);}
      }
    } else if (table === 'plans') {
      data = pick(r,['answer','activity','place','window','formal']); add(r.origin);
    } else if (table === 'outcomes') {
      updated(r.settledAt);for(const entry of r.timeline??[])updated(entry.at);
      data.timeline = (r.timeline ?? []).filter(x=>arrived(x.at,tick)).map(x=>pick(x,['at','label']));
      if (arrived(r.settledAt,tick)) { label = r.settledLabel ?? label; Object.assign(data,pick(r,['kind','world'])); }
      else data.status = 'Still unfolding';
      add(r.attempts,r.plan);
    } else if (table === 'perceptions') {
      data = pick(r,['perceived','unknown']);
      data.inferences = (r.inferences ?? []).map(x=>pick(x,['about','guess','certainty','establishes']));
      add(r.of,r.causes, ...(r.inferences ?? []).map(x=>x.basis ?? []));
      // notPerceived and observerLinks contain privileged truth, not the person's knowledge.
      if (observer) data.observerContext = pick(r,['notPerceived','notAsserted','observerLinks']);
    } else if (table === 'learning') {
      data = {previous:r.previous === null ? null : r.previous ? pick(r.previous,['chance','strength','entries']) : undefined, next:pick(r.new,['chance','strength','entries','moodChange'])};
      add(r.record,r.evidence);
      if (observer) data.rule = r.rule;
    }
    // No generic raw-record inspector: unknown fields and diagnostic prose stay out of character views.
    // Numeric motive annotations and decision-document codes belong in the inspector.
    const withoutAnnotations=label.replace(/ \([+-]\d+(?:\.\d+)?\)/g,'').replace(/ \(Sid's D\d+\)/g,'');
    if(withoutAnnotations!==label){data.recordedLabel=label;label=withoutAnnotations;}
    const owner=r.holder ?? r.who ?? r.actor ?? null;
    if(!observer && owner===who)label=personalLabel(label,model.run.names[who]);
    return {id:r.id, table, stage:STAGES[table], at:r.visibleAt, updatedAt, label, owner, data, refs:[...new Set(refs)]};
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
    const selected = thread==='all' ? null : threads.find(t=>t.id===thread) ?? threads[0];
    const chosen = new Set(thread==='all' ? nodes.map(n=>n.id) : selected?.ids ?? []);
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
      const gates=[row.visibleAt,row.choice?.visibleAt,row.initial?.asOf];
      if(observer || row.actor===who)gates.push(row.resolvedAt,...(row.progress??[]).map(p=>p.at));
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
  function learningMetrics(node) {
    if(node.table!=='learning')return [];
    const {previous,next}=node.data, metrics=[];
    const direction=d=>d>0?'up':d<0?'down':'same';
    for(const [key,label] of [['chance','Expected chance'],['strength','Feeling strength']])if(Number.isFinite(next?.[key])){
      const before=Number.isFinite(previous?.[key])?previous[key]:null, after=next[key], delta=before===null?null:after-before;
      metrics.push({key,label,before,after,delta,status:delta===null?(key==='strength'&&previous===null?'new':'unknown'):direction(delta)});
    }
    if(Number.isFinite(next?.moodChange))metrics.push({key:'mood',label:'Mood',delta:next.moodChange,status:direction(next.moodChange)});
    return metrics;
  }
  function cardStatus(n){
    const d=n.data;
    if(n.table==='decisions')return d.choice?{tone:'up',symbol:'✓',label:'Chose',detail:d.alternatives.find(a=>a.id===d.choice.selected)?.label??'Option not recorded'}:{tone:'new',symbol:'◷',label:'Deciding'};
    if(n.table==='plans')return {tone:'up',symbol:'✓',label:'Agreed plan'};
    if(n.table!=='attempts')return null;
    if(!d.status&&d.progress==='playing')return {tone:'up',symbol:'▶',label:'Playing together'};
    if(!d.status)return d.inProgress?{tone:'new',symbol:'◷',label:d.kind==='planned-intention'?'Waiting for the evening':d.kind==='go'?'On the way':d.kind==='begin-planned-act'?'Waiting':'Awaiting response'}:{tone:'unknown',symbol:'?',label:'Outcome unknown'};
    if(d.status==='answered'){
      if(d.response?.answer==='yes')return {tone:'up',symbol:'✓',label:'Accepted'};
      if(d.response?.answer==='no')return {tone:'down',symbol:'×',label:'Declined'};
      if(d.response?.kind==='not-now')return {tone:'new',symbol:'◷',label:'Not now'};
      return {tone:'up',symbol:'✓',label:'Answered'};
    }
    return ({completed:{tone:'up',symbol:'✓',label:'Completed'},unsuccessful:{tone:'down',symbol:'×',label:['begin-planned-act','planned-intention'].includes(d.kind)?'Plan did not happen':'Unsuccessful'},busy:{tone:'new',symbol:'◷',label:'Blocked · busy'},interrupted:{tone:'down',symbol:'×',label:'Interrupted'}})[d.status]??{tone:'unknown',symbol:'?',label:'Status unavailable'};
  }
  function currentMoment(nodes,tick){
    const at=Math.max(-1,...nodes.map(n=>n.updatedAt));
    return {at,exact:at===tick,ids:at<0?[]:nodes.filter(n=>n.updatedAt===at).map(n=>n.id)};
  }
  function selectedReferences(n){
      if(n.table!=='decisions'||!n.data.choice)return n.refs;
      const allCites=new Set(n.data.alternatives.flatMap(a=>a.reasons.flatMap(r=>r.cites)));
      const chosen=n.data.alternatives.find(a=>a.id===n.data.choice.selected);
      const chosenCites=new Set((chosen?.reasons??[]).flatMap(r=>r.cites));
      return n.refs.filter(id=>!allCites.has(id)||chosenCites.has(id));
  }
  function choicePath(view,root){
    const nodes=new Map(view.nodes.map(n=>[n.id,n])),choice=nodes.get(root);
    if(choice?.table!=='decisions')return null;
    const edges=[];
    for(const n of nodes.values())for(const from of selectedReferences(n))if(nodes.has(from))edges.push({from,to:n.id});
    const parents=new Map(),children=new Map();
    for(const e of edges){if(!parents.has(e.to))parents.set(e.to,[]);parents.get(e.to).push(e.from);if(!children.has(e.from))children.set(e.from,[]);children.get(e.from).push(e.to);}
    const walk=links=>{const seen=new Set(),todo=[root];while(todo.length){const id=todo.pop();if(seen.has(id))continue;seen.add(id);todo.push(...(links.get(id)??[]));}return seen;};
    const before=walk(parents),after=walk(children);
    const ids=new Set([...before,...after]),roles=new Map();
    const chosen=choice.data.alternatives.find(a=>a.id===choice.data.choice?.selected);
    for(const reason of chosen?.reasons??[])for(const id of reason.cites)if(ids.has(id)){
      const kind=reason.kind==='supports'?'supports':reason.kind==='conflicts'?'conflicts':'context';
      if(!roles.has(id))roles.set(id,new Set());roles.get(id).add(kind);
    }
    for(const id of before)if(id!==root&&!roles.has(id))roles.set(id,new Set(['context']));
    return {root,ids,before,after,roles,chosen:chosen?.label??null,pending:!choice.data.choice,
      edges:edges.filter(e=>ids.has(e.from)&&ids.has(e.to)).map(e=>({...e,roles:e.to===root?[...(roles.get(e.from)??['context'])]:[]}))};
  }
  // Index moments, not future text. Project an event only at the time being inspected.
  function eventIndex(model,who,observer){
    if(model.missing)return [];
    const groups=new Map();
    for(const {table,row} of model.index.values()){
      // Runtime learning already marks the update; its memory snapshot stays in the tree.
      if(model.lives.runtime&&table==='records')continue;
      const gates=[row.visibleAt];
      if(table==='decisions')gates.push(row.choice?.visibleAt);
      if(table==='attempts'&&(observer||row.actor===who))gates.push(row.resolvedAt,...(row.progress??[]).map(p=>p.at));
      if(observer&&table==='outcomes')gates.push(row.settledAt,...(row.timeline??[]).map(x=>x.at));
      if(!observer){
        gates.push(...(row.knownBy??[]).filter(k=>k.who===who).map(k=>k.availableAt));
        if(table==='attempts')gates.push(...(model.perceptionsByOf.get(row.id)??[]).filter(p=>p.who===who).map(p=>p.visibleAt));
      }
      for(const at of new Set(gates))if(Number.isFinite(at)&&permitted(model,table,row,at,who,observer)){
        if(!groups.has(at))groups.set(at,new Set());groups.get(at).add(row.id);
      }
    }
    return [...groups].sort((a,b)=>a[0]-b[0]).map(([at,ids])=>({at,ids:[...ids]}));
  }
  function timelineBins(events,start,end,count=60){
    const limit=Math.max(1,Math.floor(count)),gap=Math.max(1,end-start)/limit,cells=new Map();
    for(const e of events.filter(e=>e.at>=start&&e.at<=end).sort((a,b)=>a.at-b.at)){
      const key=Math.min(limit-1,Math.floor((e.at-start)/gap));
      if(!cells.has(key))cells.set(key,[]);cells.get(key).push(e);
    }
    const bins=[];
    for(const cell of cells.values()){
      const last=bins.at(-1),center=xs=>(xs[0].at+xs.at(-1).at)/2;
      if(last&&center(cell)-center(last)<gap)last.push(...cell);else bins.push([...cell]);
    }
    return bins.map(events=>({from:events[0].at,to:events.at(-1).at,events,count:events.reduce((n,e)=>n+e.ids.length,0)}));
  }
  function eventSummary(model,event,who,observer){
    return event.ids.flatMap(id=>{
      const entry=model.index.get(id);if(!entry||!permitted(model,entry.table,entry.row,event.at,who,observer))return [];
      const n=project(model,entry.table,entry.row,event.at,who,observer);return [{id:n.id,stage:n.stage,label:n.label}];
    });
  }
  function neighborhood(v,id){
    const nodes=new Map(v.nodes.map(n=>[n.id,n])),root=nodes.get(id);if(!root)return null;
    const path=choicePath(v,id),parents=path?[...path.before].filter(x=>x!==id):root.refs;
    const before=parents.map(x=>nodes.get(x)).filter(Boolean);
    const memories=before.filter(n=>n.table==='records');
    // Only immediate event edges; click a branch to walk one step, never imply a transitive edge.
    const direct=new Set(selectedReferences(root)),inputs=before.filter(n=>n.table!=='records'&&direct.has(n.id)&&!(root.table==='attempts'&&n.table==='outcomes'));
    const after=v.nodes.filter(n=>n.id!==id&&((selectedReferences(n).includes(id)&&!(n.table==='attempts'&&root.table==='outcomes'))||(root.table==='attempts'&&n.table==='outcomes'&&direct.has(n.id))));
    return {root,before:inputs,memories,after,roles:path?.roles??new Map()};
  }
  // Presentation time is continuous; recorded evidence remains gated by whole minutes.
  function advancePlayback(at,elapsedMs,rate,end){
    return Math.min(end,at+Math.max(0,Math.min(elapsedMs,250))*rate/1000);
  }
  function timelineRuler(start,end,width=800){
    const span=Math.max(1,end-start),count=Math.max(2,Math.floor(width/100));
    const steps=[1,2,5,10,15,30,60,120,180,360,720,1440,2880,10080,20160,40320];
    const step=steps.find(n=>n>=span/count)??Math.ceil(span/count/1440)*1440;
    const day=n=>`Day ${Math.floor(n/1440)+1}`;
    const first=day(start),last=day(Math.max(start,end-1));
    const ticks=[];
    for(let at=Math.ceil(start/step)*step;at<=end;at+=step){
      const label=step>=1440?day(at):`${String(Math.floor(at%1440/60)).padStart(2,'0')}:${String(at%60).padStart(2,'0')}`;
      ticks.push({at,label:step<1440&&first!==last&&at%1440===0?`${day(at)} · ${label}`:label,position:(at-start)/span*100});
    }
    return {label:first===last?first:`${first} – ${last}`,ticks};
  }
  return {open,view,moments,comparison,time,human,learningMetrics,cardStatus,currentMoment,choicePath,eventIndex,timelineBins,eventSummary,neighborhood,advancePlayback,timelineRuler};
});
