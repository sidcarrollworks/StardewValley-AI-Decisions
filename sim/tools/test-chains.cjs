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
