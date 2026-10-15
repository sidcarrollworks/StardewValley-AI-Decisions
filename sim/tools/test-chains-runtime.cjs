const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const {join}=require('node:path');
const M=require('../viewer/chains-model.js');
const cases=JSON.parse(readFileSync(join(__dirname,'../viewer/fixtures/lives-runtime-excerpts.json'),'utf8')).cases;
const open=name=>M.open(cases[name]);
const view=(model,at,person='Penny',observer=false)=>M.view(model,{tick:at,who:model.run.names.indexOf(person),observer,thread:'all'});
const choice=(v,holder,at)=>v.nodes.find(n=>n.table==='decisions'&&n.owner===holder&&n.at===at);

test('actual pilot excerpts use a separate runtime adapter without mutating recordings',()=>{
  const before=JSON.stringify(cases['laya-seed7']),m=open('laya-seed7');
  assert.equal(m.lives.runtime,true);assert.equal(m.lives.version,'runtime-1');
  assert.equal(m.lives.illustrative,undefined);view(m,1200);assert.equal(JSON.stringify(cases['laya-seed7']),before);
});
test('unknown runtime versions and malformed evening tables are rejected explicitly',()=>{
  for(const name of ['livesRouting','livesCompany','livesEvenings']){const r=structuredClone(cases['laya-seed7']);r[name].version=99;assert.throws(()=>M.open(r),/Unsupported/);}
  const r=structuredClone(cases['laya-seed7']);delete r.livesEvenings.choices;assert.throws(()=>M.open(r),/Missing evening choices/);
});
test('the offer chain connects sourced own history through agreement to the actual completed game',()=>{
  const m=open('authored-seed7'),v=view(m,1200,'Pam',true),pam=m.run.names.indexOf('Pam'),d=choice(v,pam,435),path=M.choicePath(v,d.id);
  assert.ok([...path.before].some(id=>v.all.get(id)?.label==='I am proud of Penny and hope she has a good life.'));
  assert.equal([...path.after].filter(id=>v.all.get(id)?.label==='Finished the game together'&&v.all.get(id)?.owner===pam).length,1);
  assert.ok([...path.after].some(id=>v.all.get(id)?.table==='learning'));
});
test('Penny can follow her own intention to the missed evening without seeing Pam choose other plans',()=>{
  const m=open('laya-seed7'),v=view(m,1200),penny=m.run.names.indexOf('Penny'),pam=m.run.names.indexOf('Pam'),d=choice(v,penny,1020),path=M.choicePath(v,d.id);
  assert.equal(choice(v,pam,1020),undefined);
  assert.ok([...path.after].some(id=>v.all.get(id)?.label==='Did not see them during the agreed window'));
  const text=JSON.stringify(v.nodes);assert.ok(!text.includes('chose-other-plans'));assert.ok(!text.includes('modelWeights'));
  const perceived=v.nodes.find(n=>n.label==='Did not see them during the agreed window');
  assert.equal(perceived.data.perceived.observedMinutes,42);assert.equal(perceived.data.perceived.sawPartner,false);
  assert.equal(perceived.data.unknown[0].status,'unknown');
});
test('rewinding hides settlement, learned disappointment and the other persons later answer',()=>{
  const m=open('laya-seed7'),early=view(m,436,'Pam'),before=view(m,1199),after=view(m,1200);
  assert.ok(!early.nodes.some(n=>n.label==='Heard yes'));
  assert.ok(!before.nodes.some(n=>n.label==='Did not see them during the agreed window'));
  assert.ok(!before.nodes.some(n=>n.label==='Disappointed about the evening'));
  assert.ok(after.nodes.some(n=>n.label==='Disappointed about the evening'));
  assert.ok(!JSON.stringify(view(m,436,'Pam',true).nodes).includes('not-seen-during-plan'));
});
test('learning cards show recorded signed strength and do not invent an unrecorded prior chance',()=>{
  const v=view(open('laya-seed7'),1200),feeling=v.nodes.find(n=>n.table==='learning'&&n.label==='Disappointed about the evening');
  assert.equal(feeling.data.previous,null);assert.equal(feeling.data.next.strength,-0.025);
  const learned=v.nodes.find(n=>n.table==='learning'&&n.label==='Will Pam keep the plan?');
  assert.equal(learned.data.previous,null);assert.equal(learned.data.next.chance,0.5);
});
test('warm and wary evidence stay distinct and refer only to the holders recorded history',()=>{
  const m=open('laya-seed7'),v=view(m,1020),d=choice(v,m.run.names.indexOf('Penny'),1020),b=M.neighborhood(v,d.id);
  assert.ok(b.memories.some(n=>n.label==='Warmth toward Pam'));
  assert.ok(b.memories.some(n=>n.label==='Uneasy about Pam'));
  assert.ok(b.memories.every(n=>n.owner===m.run.names.indexOf('Penny')));
  assert.ok([...b.roles.values()].some(r=>r.has('supports')));assert.ok([...b.roles.values()].some(r=>r.has('conflicts')));
});
test('observer sees actual Laya selection and raw weights without substituting authored choice',()=>{
  const m=open('laya-seed7'),v=view(m,1200,'Pam',true),d=choice(v,m.run.names.indexOf('Pam'),1020);
  const original=cases['laya-seed7'].livesEvenings.choices.find(c=>c.decision.opportunity.holder==='Pam'&&c.decision.opportunity.at===1020);
  assert.equal(d.data.choice.selected,original.evaluation.selectedId);
  assert.deepEqual(d.data.choice.modelWeights,original.evaluation.answer.weights);
  for(const a of original.decision.alternatives)assert.equal(d.data.choice.probabilities[a.option.id],a.probability);
});
test('matched history comparisons retain actual recorded arms and remain observer-only',()=>{
  const m=open('laya-seed7');assert.equal(view(m,1200).comparisons.length,0);
  const v=view(m,1200,'Penny',true),c=M.comparison(m,v.comparisons[0]);
  assert.equal(c.arms[0].method,'authored');assert.equal(c.arms[0].draw,c.arms[1].draw);
  assert.ok(c.heldConstant.includes('random draw'));assert.equal(c.differs.sampledAction,false);
});
test('Gus follows his own observed miss to concern and its expiry, with no private household plan',()=>{
  const m=open('gus'),v=view(m,7020,'Gus'),d=v.nodes.find(n=>n.table==='decisions'),path=M.choicePath(v,d.id);
  assert.ok([...path.before].some(id=>v.all.get(id)?.label==='Did not see them where expected'));
  assert.ok([...path.after].some(id=>v.all.get(id)?.label==='Waiting for a chance to help'));
  assert.ok([...path.after].some(id=>v.all.get(id)?.label==='No chance to help arose'));
  assert.ok(!v.nodes.some(n=>n.table==='plans'));assert.ok(!view(m,7019,'Gus').nodes.some(n=>n.label==='No chance to help arose'));
});
test('timeline excludes starting history and unrelated routine memory updates',()=>{
  const m=open('laya-seed7'),penny=m.run.names.indexOf('Penny'),events=M.eventIndex(m,penny,false);
  assert.ok(events.every(e=>e.at>=0));assert.ok(events.some(e=>e.at===1200));
  assert.ok(!events.some(e=>e.at===435)); // Pam privately considers the offer.
  assert.ok(events.length<30);
});
test('all projected links obey the same clock and lens, and attempts do not claim an unrecorded journey',()=>{
  const m=open('laya-seed7');for(const at of [435,436,437,1020,1199,1200])for(const observer of [false,true]){
    const v=view(m,at,'Penny',observer);for(const n of v.nodes)for(const ref of n.refs)assert.ok(v.all.has(ref));
  }
  const v=view(m,1020),n=v.nodes.find(n=>n.table==='attempts'&&n.data.kind==='planned-intention');
  assert.equal(n.data.attempted,null);assert.equal(M.cardStatus(n).label,'Waiting for the evening');
});


test("actual game onset is visible to its participants only at the recorded start",()=>{
 const m=open("authored-seed7"),find=t=>view(m,t).nodes.find(n=>n.table==="attempts"&&n.data.kind==="planned-intention");
 assert.equal(M.cardStatus(find(1079)).label,"Waiting for the evening");
 assert.equal(M.cardStatus(find(1080)).label,"Playing together");
 assert.equal(M.cardStatus(find(1109)).label,"Completed");
 assert.ok(M.eventIndex(m,m.run.names.indexOf("Penny"),false).some(e=>e.at===1080));
});
const continuity=JSON.parse(readFileSync(join(__dirname,'../viewer/fixtures/lives-continuity-excerpt.json'),'utf8'));

test('Penny continues beyond her first evening through a recorded search and personal outcome',()=>{
  const m=M.open(continuity),v=view(m,1600),d=choice(v,m.run.names.indexOf('Penny'),1494),path=M.choicePath(v,d.id);
  assert.equal(M.cardStatus(d).detail,'Look for Alex at Square and ask about a game.');
  assert.ok([...path.after].some(id=>v.all.get(id)?.label==='Did not see them during the search'));
  assert.ok([...path.after].some(id=>v.all.get(id)?.table==='learning'));
  const before=view(m,1530);assert.ok(!before.nodes.some(n=>n.at===1531));
  const own=v.nodes.find(n=>n.label==='Did not see them during the search');assert.equal(own.data.unknown[0].status,'unknown');
  assert.ok(!view(m,1600,'Alex').nodes.some(n=>n.id===d.id));
});

test('routine routing exposes the recorded legacy selection without inventing probabilities or arrival',()=>{
  const m=M.open(continuity),v=view(m,1600,'Penny',true),d=choice(v,m.run.names.indexOf('Penny'),1532);
  assert.equal(d.label,'Where to spend time?');assert.equal(M.cardStatus(d).detail,'Spend time at home.');
  const original=continuity.livesRouting.decisions.find(c=>c.decision.opportunity.at===1532&&c.decision.opportunity.holder==='Penny');
  assert.equal(d.data.choice.selected,original.selectedOption);
  if(original.usedLegacy)assert.deepEqual(d.data.choice.probabilities,{});
  assert.ok(!v.nodes.some(n=>n.table==='attempts'&&n.refs.includes(d.id)));
});

test('unchanged whereabouts confirmations are tucked away but changed estimates remain on the timeline',()=>{
  const m=M.open(continuity),who=m.run.names.indexOf('Gus'),quiet=M.routineChecks(m,4320,who,false);
  assert.ok(quiet.length>0);assert.ok(quiet.every(n=>n.holder===who));
  const prominent=new Set(M.eventIndex(m,who,false).flatMap(e=>e.ids));
  assert.ok(quiet.every(n=>!prominent.has(n.id)));
  assert.ok(M.eventIndex(m,who,false,true).some(e=>e.ids.includes(quiet[0].id)));
  assert.ok(M.routineChecks(m,quiet[0].at-1,who,false).every(n=>n.id!==quiet[0].id));
  const changed=m.lives.learning.filter(n=>n.previous?.chance!==n.new?.chance&&Number.isFinite(n.new?.chance));
  assert.ok(changed.length);assert.ok(changed.every(n=>M.eventIndex(m,n.holder,false).some(e=>e.ids.includes(n.id))));
});

test('routing and company work independently of the evening block and preserve source recordings',()=>{
  const input=structuredClone(continuity);delete input.livesEvenings;const before=JSON.stringify(input),m=M.open(input);
  assert.equal(m.missing,false);assert.ok(view(m,4320).nodes.some(n=>n.table==='decisions'&&n.at>1440));assert.equal(JSON.stringify(input),before);
  delete input.livesCompany;const routing=M.open(input);assert.ok(view(routing,4320).nodes.some(n=>n.label==='Where to spend time?'));
});

test('company outcome chains use explicit references and obey every knowledge and time gate',()=>{
  const m=M.open(continuity);
  for(const at of [1494,1531,2880,4320])for(const person of ['Penny','Pam','Gus'])for(const observer of [false,true]){
    const v=view(m,at,person,observer);
    for(const n of v.nodes){assert.ok(M.time(n.at)<=at);for(const ref of n.refs)assert.ok(v.all.has(ref),ref);}
    if(!observer){assert.ok(v.nodes.every(n=>n.table!=='outcomes'));assert.ok(v.nodes.filter(n=>n.table==='decisions').every(n=>n.owner===m.run.names.indexOf(person)));}
  }
});
