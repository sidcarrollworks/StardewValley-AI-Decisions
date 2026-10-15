const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const {join}=require('node:path');
const M=require('../viewer/chains-model.js');
const fixture=JSON.parse(readFileSync(join(__dirname,'../viewer/fixtures/lives-observer-example-v0.1.json'),'utf8'));
const model=M.open(fixture), penny=fixture.names.indexOf('Penny'), pam=fixture.names.indexOf('Pam');
const view=(tick,options={})=>M.view(model,{tick,who:penny,...options});
const node=(v,id)=>v.all.get(id);
test('old runs remain unclassified; unknown Lives versions and duplicate ids are rejected',()=>{
  assert.equal(M.open({format:'under-glass-run',version:1,names:[]}).missing,true);
  const f=structuredClone(fixture);f.lives.version='0.2';assert.throws(()=>M.open(f),/not supported/);
  f.lives.version='0.1';f.lives.records.push(f.lives.records[0]);assert.throws(()=>M.open(f),/duplicate/);
});
test('a pending decision exposes alternatives but no future choice, model or draw',()=>{
  const before=node(view(13510,{observer:true}),'dec:13510:Penny:0');
  assert.equal(before.data.alternatives.length,2);assert.equal(before.data.choice,undefined);
  assert.equal(before.data.thought,undefined);
  const after=node(view(13511,{observer:true}),'dec:13510:Penny:0');assert.equal(after.data.choice.backend,'laya');
  assert.match(after.data.thought,/I won't bring her beer/);
  assert.equal(node(view(13510,{observer:true}),'dec:13510:Penny:0').data.choice,undefined);
});
test('character projection excludes other minds, world truth and diagnostic secrets',()=>{
  const v=view(14240);const text=JSON.stringify(v.nodes);
  assert.equal(v.all.has('dec:14010:Pam:0'),false);assert.equal(v.all.has('out:14040:Pam:0'),false);
  assert.equal(text.includes('Pam was home until 17:31'),false);assert.equal(text.includes('observerLinks'),false);
  assert.equal(node(v,'dec:13510:Penny:0').data.choice.probabilities,undefined);
  assert.deepEqual(v.comparisons,[]);
});
test('Penny retains unknown whereabouts and an explicitly non-evidential guess',()=>{
  const p=node(view(14101),'per:14101:Penny:0');
  assert.equal(p.data.unknown[0].status,'unknown');assert.equal(p.data.inferences[0].guess,'the Saloon');
  assert.equal(p.data.inferences[0].establishes,false);
  assert.equal(node(view(14100),'per:14101:Penny:0'),undefined);
});
test('a busy target does not learn about an ask that was never made',()=>{
  assert.equal(view(13580).all.has('att:13580:Gus:0'),false);
  assert.equal(view(13580,{who:0}).all.has('att:13580:Gus:0'),true);
});
test('knownBy is acquisition-gated independently of record creation',()=>{
  const f=structuredClone(fixture);const r=f.lives.records.find(x=>x.id==='rd:penny-pam-care');r.knownBy[0].availableAt=14000;
  const m=M.open(f);
  assert.equal(M.view(m,{tick:13999,who:penny}).all.has(r.id),false);
  assert.equal(M.view(m,{tick:14000,who:penny}).all.has(r.id),true);
});
test('outcome settlement and timeline entries do not leak backwards',()=>{
  const id='out:14040:Pam:0',early=node(view(14040,{observer:true}),id),late=node(view(14240,{observer:true}),id);
  assert.equal(early.data.kind,undefined);assert.equal(early.data.timeline.length,1);
  assert.equal(early.label,'the agreed evening at the Trailer');assert.equal(late.data.kind,'agreed-absent');
  assert.equal(JSON.stringify(early).includes('Pam reaches the Saloon'),false);
});
test('attempt resolution is hidden until its own gate',()=>{
  assert.equal(node(view(14041),'att:14041:Penny:0').data.status,undefined);
  assert.ok(node(view(14101),'att:14041:Penny:0').data.status);
});
test('choice cards expose the chosen line only after the answer arrives',()=>{
  assert.equal(M.cardStatus(node(view(13510),'dec:13510:Penny:0')).label,'Deciding');
  const n=node(view(13511),'dec:13510:Penny:0'),s=M.cardStatus(n);
  assert.equal(s.label,'Chose');assert.equal(s.detail,n.data.alternatives.find(a=>a.id===n.data.choice.selected).label);
  assert.equal(n.updatedAt,13511);
});
test('attempt badges separate waiting, completion, unsuccessful plans and answers',()=>{
  assert.equal(M.cardStatus(node(view(14041),'att:14041:Penny:0')).label,'Waiting');
  assert.equal(M.cardStatus(node(view(14101),'att:14041:Penny:0')).label,'Plan did not happen');
  assert.equal(M.cardStatus(node(view(14040),'att:14029:Penny:0')).label,'On the way');
  assert.equal(M.cardStatus(node(view(14041),'att:14029:Penny:0')).label,'Completed');
  assert.equal(M.cardStatus(node(view(13511),'att:13511:Penny:0')).label,'Awaiting response');
  assert.equal(M.cardStatus(node(view(13512),'att:13511:Penny:0')).label,'Accepted');
  assert.equal(M.cardStatus(node(view(13580,{who:0}),'att:13580:Gus:0')).label,'Blocked · busy');
  assert.equal(M.cardStatus(node(view(14056,{who:0}),'att:14055:Gus:0')).label,'Declined');
  assert.equal(M.cardStatus(node(view(14061,{who:pam}),'att:14060:Pam:0')).label,'Not now');
});
test('current moment includes older updated cards, rewinds, and labels the last update between events',()=>{
  const v=view(14101),m=M.currentMoment(v.nodes,14101);
  assert.equal(m.exact,true);assert.equal(m.at,14101);assert.ok(m.ids.includes('att:14041:Penny:0'));assert.ok(m.ids.includes('lu:14101:Penny:1'));
  const later=M.currentMoment(view(14200).nodes,14200);assert.equal(later.at,14101);assert.equal(later.exact,false);
  assert.ok(M.currentMoment(view(14100).nodes,14100).at<14101);
  assert.deepEqual(M.currentMoment([],14000),{at:-1,exact:false,ids:[]});
});
test('current highlights and statuses respect acquisition and private resolution gates',()=>{
  const f=structuredClone(fixture),r=f.lives.attempts.find(r=>r.id==='att:14011:Pam:0');
  r.knownBy=[{who:penny,availableAt:14012}];
  const m=M.open(f),n=M.view(m,{tick:14050,who:penny}).all.get(r.id);
  assert.equal(M.cardStatus(n).label,'Outcome unknown');assert.equal(n.updatedAt,14012);
  assert.equal(M.moments(m,penny,false).includes(14043),false);
  const record=f.lives.records.find(r=>r.id==='rd:penny-pam-care');record.knownBy[0].availableAt=14000;
  assert.equal(M.view(M.open(f),{tick:14000,who:penny}).all.get(record.id).updatedAt,14000);
});
test('expectations use recorded states as of the clock, never backfilled snapshots',()=>{
  const id='ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday';
  assert.equal(node(view(13504),id).data.state.chance,undefined);assert.equal(node(view(13505),id).data.state.chance,0.3333);
  const answer='ex:Pam:Penny:answers:errand:beer';assert.equal(node(view(13510,{who:pam}),answer).data.state.chance,0.5);
  const learning='lu:14101:Penny:0';assert.equal(node(view(14100),learning),undefined);
  const change=node(view(14101),learning);assert.equal(change.data.previous.chance,1);assert.equal(change.data.next.chance,0.5);
  assert.equal(node(view(14240),'lu:14101:Penny:1').data.next.chance,undefined);
  const f=structuredClone(fixture);f.lives.learning.find(r=>r.id===learning).previous=null;
  assert.equal(M.view(M.open(f),{tick:14101,who:penny}).all.get(learning).data.previous,null);
});
test('expectation and feeling labels explain the subject without exposing storage keys',()=>{
  const v=view(14240,{observer:true});
  const home=node(v,'ex:Penny:Pam:whereabouts:Home:Trailer:18-21:weekday');
  assert.equal(home.label,'Will Pam be at home on weekday evenings?');
  assert.equal(home.data.key,'whereabouts:Home:Trailer:18-21:weekday');
  assert.equal(node(v,'ex:Gus:Penny:availability:Square:10-14:weekday').label,'Will Penny be free at the town square on weekday late mornings and early afternoons?');
  assert.equal(node(v,'ex:Pam:Penny:answers:errand:beer').label,'Will Penny fetch beer?');
  assert.equal(node(v,'ex:Penny:Pam:followThrough:plan').label,'Will Pam keep a plan?');
  for(const n of v.all.values())if(['expectation','association'].includes(n.data.type))assert.doesNotMatch(n.label,/whereabouts:|availability:|answers:|followThrough:|person:|place:|activity:|situation:/);
  const worry=[...v.all.values()].find(n=>n.data.name==='worried care');
  assert.equal(worry.label,'Worried about Pam');assert.equal(worry.data.object,'person:Pam');
});
test('unrecognized expectation keys keep their exact details without inventing a meaning',()=>{
  const f=structuredClone(fixture),r=f.lives.records.find(r=>r.type==='expectation');r.key='new-kind:secret-code';
  const n=M.view(M.open(f),{tick:14240,who:penny}).all.get(r.id);
  assert.equal(n.label,'Expectation about Pam');assert.equal(n.data.key,r.key);
});
test('learning metrics separate a new feeling from its mood impact and probability changes',()=>{
  assert.deepEqual(M.learningMetrics(node(view(14240),'lu:14101:Penny:1')), [
    {key:'strength',label:'Feeling strength',before:null,after:0.0167,delta:null,status:'new'},
    {key:'mood',label:'Mood',delta:-0.0167,status:'down'}
  ]);
  assert.deepEqual(M.learningMetrics(node(view(14240),'lu:14101:Penny:0')), [
    {key:'chance',label:'Expected chance',before:1,after:0.5,delta:-0.5,status:'down'}
  ]);
  assert.equal(M.learningMetrics(node(view(14240),'lu:13512:Penny:0'))[0].status,'same');
});
test('learning direction preserves zero, small changes and missing priors without assigning valence',()=>{
  const metrics=(previous,next)=>M.learningMetrics({table:'learning',data:{previous,next}});
  assert.equal(metrics({strength:0},{strength:0.01})[0].status,'up');
  assert.equal(metrics({strength:0.1},{strength:0})[0].status,'down');
  assert.equal(metrics({strength:0.1},{strength:0.10001})[0].status,'up');
  assert.equal(metrics({strength:0},{strength:0})[0].status,'same');
  assert.equal(metrics({},{strength:0.01})[0].status,'unknown');
  assert.equal(metrics(null,{chance:0.5})[0].status,'unknown');
  const f=structuredClone(fixture);delete f.lives.learning.find(r=>r.id==='lu:14101:Penny:1').previous;
  assert.equal(M.learningMetrics(M.view(M.open(f),{tick:14240,who:penny}).all.get('lu:14101:Penny:1'))[0].status,'unknown');
  assert.deepEqual(M.learningMetrics({table:'records',data:{strength:0.5}}),[]);
});
test('personal labels omit their owner while preserving other people and observer attribution',()=>{
  const personal=view(14240),observer=view(14240,{observer:true});
  assert.equal(node(personal,'att:14041:Penny:0').label,'Waits for Pam');
  assert.equal(node(observer,'att:14041:Penny:0').label,'Penny waits for Pam');
  assert.equal(node(personal,'per:14101:Penny:0').label,"She said yes, and she isn't here");
  assert.equal(node(personal,'dec:13510:Penny:0').label,'After saying no to beer, what to offer instead?');
  const opportunity=[...personal.all.values()].find(n=>n.table==='opportunities');
  assert.doesNotMatch(opportunity.label,/\+0\.60/);assert.match(opportunity.data.recordedLabel,/\+0\.60/);
  const f=structuredClone(fixture),r=f.lives.records.find(r=>r.holder===penny && r.type==='reading');
  r.label='Pennyworth reminds Penny of Pam';
  assert.equal(M.view(M.open(f),{tick:14240,who:penny}).all.get(r.id).label,r.label);
  r.label='Penny and Pam agreed to stay home';
  assert.equal(M.view(M.open(f),{tick:14240,who:penny}).all.get(r.id).label,r.label);
  r.label="Penny's account: she cleans alone while Pam nurses a headache";
  assert.equal(M.view(M.open(f),{tick:14240,who:penny}).all.get(r.id).label,'Account: she cleans alone while Pam nurses a headache');
});
test('character threads never use observer titles or summaries',()=>{
  const f=structuredClone(fixture);for(const t of f.lives.threads){t.title='SECRET';t.summary=[{visibleAt:0,label:'SECRET'}];}
  const v=M.view(M.open(f),{tick:14240,who:penny});assert.equal(JSON.stringify(v.threads).includes('SECRET'),false);
});
test('all projected dependency links resolve within the same lens and clock',()=>{
  for(const observer of [false,true])for(const tick of [0,13510,13511,14040,14101,14240]){
    const v=view(tick,{observer});for(const n of v.nodes)for(const id of n.refs)assert.ok(v.all.has(id),`${tick} ${id}`);
  }
});
test('two history comparisons distinguish changed probabilities from changed sampled action',()=>{
  const v=view(13511,{observer:true});assert.equal(v.comparisons.length,2);
  const [care,away]=v.comparisons.map(c=>M.comparison(model,c));
  assert.equal(care.differs.probabilities,true);assert.equal(care.differs.sampledAction,false);assert.equal(away.differs.sampledAction,true);
  for(const c of [care,away]){assert.equal(c.arms[0].draw,c.arms[1].draw);assert.equal(c.arms[0].method,c.arms[1].method);assert.equal(c.wholeRun.simulated,false);}
  assert.equal(view(13510,{observer:true}).comparisons.length,0);
});
test('recorded probabilities are rendered without applying experimental formulas',()=>{
  const f=structuredClone(fixture);f.lives.decisions[0].choice.probabilities['alt:13510:Penny:0/offer']=0.123;
  assert.equal(M.view(M.open(f),{tick:13511,who:penny,observer:true}).all.get('dec:13510:Penny:0').data.choice.probabilities['alt:13510:Penny:0/offer'],0.123);
});
test('projection leaves fixture data untouched and excludes unknown character fields',()=>{
  const f=structuredClone(fixture);f.lives.perceptions.at(-1).futureSecret='HIDDEN';const original=JSON.stringify(f);const v=M.view(M.open(f),{tick:14240,who:penny});
  assert.equal(JSON.stringify(v.nodes).includes('HIDDEN'),false);assert.equal(JSON.stringify(f),original);
});
test('choice path follows recalled evidence through attempts, perceptions and learning',()=>{
  const v=view(14101),path=M.choicePath(v,'dec:13510:Penny:0');
  for(const id of ['rd:penny-pam-care','att:13511:Penny:0','plan:13512:Penny:Pam','dec:14028:Penny:0','att:14041:Penny:0','per:14101:Penny:0','lu:14101:Penny:1'])assert.ok(path.ids.has(id),id);
  assert.ok(path.roles.get('rd:penny-pam-care').has('supports'));
  assert.ok(path.roles.get('rd:penny-pam-burden').has('conflicts'));
  assert.ok(path.roles.get('rd:penny-saloon-wary').has('context'));
  assert.equal(path.ids.has('rd:penny-reading-home'),false,'an unchosen option is not a reason for the selected one');
});
test('choice paths obey the clock and knowledge lens without inventing future outcomes',()=>{
  const pending=M.choicePath(view(13510),'dec:13510:Penny:0');assert.equal(pending.pending,true);assert.equal(pending.chosen,null);assert.equal(pending.after.size,1);
  assert.equal([...pending.roles.values()].some(r=>r.has('supports')),false);
  const early=M.choicePath(view(13511),'dec:13510:Penny:0');assert.ok(early.ids.has('att:13511:Penny:0'));assert.equal(early.ids.has('per:13512:Penny:0'),false);
  const personal=M.choicePath(view(14240),'dec:13510:Penny:0');assert.equal(personal.ids.has('out:14040:Pam:0'),false);assert.equal(personal.ids.has('dec:14010:Pam:0'),false);
  const observer=M.choicePath(view(14240,{observer:true}),'dec:13510:Penny:0');assert.ok(observer.ids.has('out:14040:Pam:0'));
  assert.equal(M.choicePath(view(14240),'missing'),null);
});
test('a shared memory does not make an unrelated choice a consequence',()=>{
  const v=view(14101),other={id:'unrelated',table:'decisions',data:{alternatives:[]},refs:['rd:penny-pam-care']};
  v.nodes.push(other);const path=M.choicePath(v,'dec:13510:Penny:0');assert.equal(path.ids.has(other.id),false);
  for(const e of path.edges){assert.ok(path.ids.has(e.from));assert.ok(path.ids.has(e.to));}
});
test('choice traversal handles cycles without changing the projected references',()=>{
  const v=view(14101);v.nodes.find(n=>n.id==='rd:penny-pam-care').refs.push('dec:13510:Penny:0');
  const before=JSON.stringify(v.nodes),path=M.choicePath(v,'dec:13510:Penny:0');assert.ok(path.ids.size<=v.nodes.length);assert.equal(JSON.stringify(v.nodes),before);
});

test('event timeline excludes starting history and indexes answer and resolution moments',()=>{
  const tape=M.eventIndex(model,penny,false);
  assert.ok(tape.every(e=>e.at>=0));
  assert.ok(tape.find(e=>e.at===13511).ids.includes('dec:13510:Penny:0'));
  assert.ok(tape.find(e=>e.at===14101).ids.includes('att:14041:Penny:0'));
  assert.equal(JSON.stringify(tape).includes('rd:penny-pam-care'),false);
  assert.ok(tape.every(e=>Object.keys(e).join(',')==='at,ids'),'index contains no future labels or outcomes');
});
test('event timeline respects personal knowledge and stays separate from observer outcomes',()=>{
  const personal=M.eventIndex(model,penny,false),observer=M.eventIndex(model,penny,true);
  assert.equal(personal.some(e=>e.ids.includes('out:14040:Pam:0')),false);
  assert.ok(observer.some(e=>e.ids.includes('out:14040:Pam:0')));
  assert.equal(personal.some(e=>e.ids.includes('dec:14010:Pam:0')),false);
  for(const e of personal)for(const id of e.ids)assert.ok(M.view(model,{tick:e.at,who:penny,thread:'all'}).all.has(id));
});
test('crowded timelines stay bounded and retain every event across a six-month window',()=>{
  const events=Array.from({length:12000},(_,i)=>({at:i*22,ids:['event:'+i]}));
  const before=JSON.stringify(events),bins=M.timelineBins(events,0,180*1440,40);
  assert.ok(bins.length<=40);assert.ok(bins.length>10,'dense recordings retain their distribution instead of merging the whole season');assert.equal(bins.reduce((n,b)=>n+b.count,0),events.filter(e=>e.at<=180*1440).length);
  assert.equal(JSON.stringify(events),before);
  const zoom=M.timelineBins(events,10000,10100,10);assert.ok(zoom.every(b=>b.from>=10000&&b.to<=10100));
});
test('nearby markers combine without hiding distinct moments within the group',()=>{
  const bins=M.timelineBins([{at:99,ids:['a']},{at:101,ids:['b']},{at:800,ids:['c']}],0,1000,10);
  assert.equal(bins.length,2);assert.deepEqual(bins[0].events.map(e=>e.at),[99,101]);assert.equal(bins[0].count,2);
});
test('focused tree separates memories from events and uses only the chosen option reasons',()=>{
  const v=view(14101,{thread:'all'}),b=M.neighborhood(v,'dec:13510:Penny:0');
  assert.ok(b.memories.some(n=>n.id==='rd:penny-pam-care'));assert.ok(b.before.every(n=>n.table!=='records'));
  assert.equal(b.memories.some(n=>n.id==='rd:penny-reading-home'),false);
  assert.equal(M.neighborhood(v,'rd:penny-reading-home').after.some(n=>n.id==='dec:13510:Penny:0'),false,'a citation for an unchosen option is not a chosen consequence');
  assert.ok(b.after.some(n=>n.id==='att:13511:Penny:0'));assert.equal(b.after.some(n=>n.id==='per:14101:Penny:0'),false,'descendants do not become direct children');
  assert.equal(M.neighborhood(view(13510),'att:13511:Penny:0'),null);
});
test('attempt outcomes remain on the consequence side rather than becoming causes',()=>{
  const attempt={id:'attempt',table:'attempts',refs:['decision','outcome'],data:{}},decision={id:'decision',table:'decisions',refs:[],data:{alternatives:[]}},outcome={id:'outcome',table:'outcomes',refs:['attempt'],data:{}};
  const b=M.neighborhood({nodes:[attempt,decision,outcome]},'attempt');
  assert.deepEqual(b.before.map(n=>n.id),['decision']);assert.deepEqual(b.after.map(n=>n.id),['outcome']);
  assert.equal(M.neighborhood({nodes:[attempt,decision,outcome]},'outcome').after.length,0);
});
test('all-events view includes permitted events outside a selected thread',()=>{
  const f=structuredClone(fixture),row=structuredClone(f.lives.opportunities[0]);row.id='outside-thread';row.holder=penny;row.visibleAt=14000;f.lives.opportunities.push(row);
  const m=M.open(f);assert.ok(M.view(m,{tick:14101,who:penny,thread:'all'}).nodes.some(n=>n.id===row.id));assert.equal(M.view(m,{tick:14101,who:penny}).nodes.some(n=>n.id===row.id),false);
});

test('marker summaries use their own event time and never return hidden record data',()=>{
  const summaries=M.eventSummary(model,{at:13510,ids:['dec:13510:Penny:0','dec:14010:Pam:0']},penny,false);
  assert.equal(summaries.length,1);assert.deepEqual(Object.keys(summaries[0]),['id','stage','label']);
  assert.equal(JSON.stringify(summaries).includes('probabilities'),false);
});
