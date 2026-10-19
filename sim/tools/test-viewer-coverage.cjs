const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const {join}=require('node:path');
const {Script,createContext}=require('node:vm');
const {index,view}=require('../viewer/coverage-model.js');
const base=()=>({format:'under-glass-run',version:1,names:['Pam','Penny']});
function choice(id='one',person='Pam',at=100,backend='laya') {
  const decision={opportunity:{id,holder:person,at,trigger:'offer',options:[{id:'yes',line:'Ask for a game.'},{id:'no',line:'Leave the evening open.'}]},selectedId:'no'};
  const evaluation=backend===null?null:{request:{fallback:decision,model:'typed-decisions'},answer:{backend,model:'typed-decisions',reportedModel:'laya-rl-agent',called:true,packet:{omitted:[],characters:1000}},selectedId:'yes'};
  return {phase:'offer',decision,evaluation};
}
const runWith=(...choices)=>({...base(),livesEvenings:{version:1,choices,decisions:choices.flatMap(c=>c.evaluation?[c.evaluation]:[])}});
test('deduplicates embedded and separate evaluations; displays actual selection, not fallback',()=>{
  const c=choice(),run=runWith(c,c),original=JSON.stringify(run),v=view(index(run));
  assert.equal(v.total,1);assert.equal(v.evaluated,1);assert.equal(v.called,1);
  assert.equal(v.rows[0].line,'Ask for a game.');assert.equal(v.rows[0].fallbackLine,'Leave the evening open.');
  assert.equal(v.changed,1);assert.equal(JSON.stringify(run),original);
});
test('rewinding hides evaluation, line, omissions and aggregates until the application minute',()=>{
  const c=choice();c.evaluation.answer.packet.omitted=['secret'];const data=index(runWith(c));
  for(const at of [0,100,100.99]) {const v=view(data,{at});assert.equal(v.total,0);assert.equal(v.called,0);assert.equal(v.omissions,0);assert.deepEqual(v.rows,[]);}
  assert.equal(view(data,{at:101}).total,1);assert.equal(view(data,{at:99,whole:true}).total,1);
  assert.equal(view(data,{at:50}).total,0);
});
test('live whole-recording mode cannot expose answers beyond received time',()=>{
  const run=runWith(choice());run.live={at:100,partial:true};const v=view(index(run),{whole:true});
  assert.equal(v.total,0);assert.equal(v.partial,true);
});
test('reindexing a new live snapshot updates counts without modifying either recording',()=>{
  const first=runWith(choice());first.live={at:100,partial:true};
  const second=structuredClone(first);second.live.at=101;
  assert.equal(view(index(first),{whole:true}).total,0);assert.equal(view(index(second),{whole:true}).total,1);
});
test('person filter applies to choices and diagnostics',()=>{
  const run=runWith(choice('one'),choice('two','Penny',120));
  const v=view(index(run),{person:'Penny'});assert.equal(v.total,1);assert.equal(v.called,1);assert.equal(v.rows[0].person,'Penny');
  assert.equal(view(index(run),{person:'Gus'}).total,0);
});
test('Laya, fallback, no evaluation and unknown backends stay distinct',()=>{
  const run=runWith(...['laya','authored-fallback','authored',null,'other'].map((b,i)=>choice(String(i),'Pam',100,b)));
  assert.deepEqual(view(index(run)).counts,{laya:1,fallback:2,none:1,unknown:1});
});
test('missing evidence remains unknown, never an inferred successful call or empty omission list',()=>{
  const c=choice();delete c.evaluation.answer.called;delete c.evaluation.answer.packet;
  const v=view(index(runWith(c)));assert.equal(v.called,0);assert.equal(v.callsUnknown,1);assert.equal(v.omissionUnknown,1);
  assert.equal(v.rows[0].omitted,null);
});
test('failed HTTP and fallback before HTTP have different recorded participation',()=>{
  const called=choice('a','Pam',100,'authored-fallback'),notCalled=choice('b','Pam',101,'authored-fallback');
  notCalled.evaluation.answer.called=false;
  const v=view(index(runWith(called,notCalled)));assert.equal(v.counts.fallback,2);assert.equal(v.called,1);
});
test('reported model alias stays separate from request and answer model',()=>{
  const r=view(index(runWith(choice()))).rows[0];assert.equal(r.configured,'typed-decisions');assert.equal(r.reported,'laya-rl-agent');
});
test('unmatched evaluations do not increase coverage and obey person/time gates',()=>{
  const run=runWith();run.livesEvenings.decisions=[choice().evaluation];const d=index(run);
  assert.equal(view(d).total,0);assert.equal(view(d).evaluated,0);assert.equal(view(d).orphans,1);
  assert.equal(view(d,{at:100}).orphans,0);assert.equal(view(d,{person:'Penny'}).orphans,0);
});
test('absent choice list is not a recorded zero',()=>{
  const missing=view(index(base()));assert.equal(missing.total,null);assert.equal(missing.reflectionCount,null);
  assert.equal(missing.other.livesRouting.choices,null);
  assert.equal(view(index(runWith())).total,0);
});
test('conflicting duplicates refuse to present misleading totals',()=>{
  const c=choice(),run=runWith(c);run.livesEvenings.decisions=structuredClone(run.livesEvenings.decisions);
  run.livesEvenings.decisions[0].selectedId='no';assert.throws(()=>index(run),/Conflicting/);
  const other=structuredClone(c);other.phase='answer';assert.throws(()=>index(runWith(c,other)),/Conflicting/);
});
test('mismatched evaluation fallback is rejected',()=>{
  const c=choice();c.evaluation=structuredClone(c.evaluation);c.evaluation.request.fallback.selectedId='yes';
  assert.throws(()=>index(runWith(c)),/disagrees/);
});
test('different object key order is not a conflicting duplicate',()=>{
  const run=runWith(choice()),e=run.livesEvenings.decisions[0];run.livesEvenings.decisions=[{selectedId:e.selectedId,answer:e.answer,request:e.request}];
  assert.equal(view(index(run)).evaluated,1);
});
test('routing/company counts stay separate and follow time and character',()=>{
  const run=runWith(choice());run.livesRouting={version:1,backend:'authored-keyed-draw',decisions:[choice('route','Penny',80,null)]};
  run.livesCompany={version:1,choices:[choice('seek','Pam',90,null)],answers:[{episodeId:'e',holder:'Penny',at:91}]};
  const d=index(run);assert.equal(view(d).total,1);assert.equal(view(d).other.livesCompany.answers,1);
  assert.equal(view(d,{person:'Penny',at:90}).other.livesRouting.choices,1);
  assert.equal(view(d,{person:'Penny',at:90}).other.livesCompany.answers,0);
});
test('reflection passes deduplicate but never count as additional choices or HTTP calls',()=>{
  const run=base(),pass={pass:0,response:'answer'};
  run.reflections=[{request:{id:'r',actor:'Penny',tick:100},events:[{status:'considered',tick:103}],answer:{backend:'local-llm+laya',evaluations:[pass,pass,{pass:1,error:'invalid'}],generationResponse:'idea',layaResponse:'same answer'}}];
  const d=index(run),v=view(d);assert.equal(v.reflectionCount,1);assert.equal(v.passes,2);assert.equal(v.passResponses,1);assert.equal(v.passErrors,1);assert.equal(v.generated,1);
  assert.equal(view(d,{at:102}).reflectionCount,0);assert.equal(view(d,{at:102}).generated,0);assert.equal(view(d,{person:'Pam'}).passes,0);
});
test('a pending reflection does not expose its eventual response, even in whole-recording mode',()=>{
  const run=base();run.reflections=[{request:{id:'r',actor:'Penny',tick:100},events:[],answer:{backend:'laya'}}];
  assert.equal(view(index(run),{whole:true}).reflectionCount,0);
});
test('unknown schema and invalid opportunity clock are rejected',()=>{
  assert.throws(()=>index({...base(),version:2}),/version/);
  assert.throws(()=>index({...base(),livesEvenings:{version:2}}),/Unsupported/);
  const c=choice();c.decision.opportunity.at=null;assert.throws(()=>index(runWith(c)),/time/);
});
test('navigation retains coverage tab when moving between tools',()=>{
  const context=createContext({window:{},document:{readyState:'loading',addEventListener(){}},URL,URLSearchParams});
  new Script(readFileSync(join(__dirname,'../viewer/tools.js'),'utf8')).runInContext(context);
  assert.equal(context.window.UnderGlassTools.readState('#t=300&person=Penny&tab=coverage').tab,'coverage');
});
test('viewer and coverage renderer parse; coverage mounts against current run and playback hook',()=>{
  const html=readFileSync(join(__dirname,'../viewer/index.html'),'utf8');
  new Script(html.slice(html.indexOf('<script>')+8,html.lastIndexOf('</script>')));
  new Script(readFileSync(join(__dirname,'../viewer/coverage.js'),'utf8'));
  assert.match(html,/S\.tab === "coverage"\) window\.underGlassCoveragePanel\?\.render/);
  assert.match(html,/state: \(\) => \(\{run:R\?\.run, at:S\.t/);
});
