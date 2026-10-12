const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');
const { test } = require('node:test');
const assert = require('node:assert/strict');
const page = readFileSync(join(__dirname, '../viewer/wording.html'), 'utf8');
const script = page.slice(page.indexOf('<script>') + 8, page.lastIndexOf('</script>'));
class Element {
  constructor(tag) { this.tag=tag;this.children=[];this.value='';this.checked=false;this.hidden=false;this.text=''; }
  set textContent(value) { this.text=String(value);this.children=[]; }
  get textContent() { return this.text+this.children.map(n=>n.textContent).join('\n'); }
  set innerHTML(_) { throw Error('Unsafe HTML rendering'); }
  append(...nodes) { this.children.push(...nodes); }
  replaceChildren(...nodes) { this.text='';this.children=nodes; }
  get firstChild() { return this.children[0]; }
  addEventListener() {}
}
function viewer() {
  const ids=Object.fromEntries(['person','hide-names','open-trial','receipt','error','cases','wording-data'].map(id=>[id,new Element('div')]));
  const context=createContext({document:{getElementById:id=>ids[id],createElement:tag=>new Element(tag)}});
  new Script(script+';this.loadTrial=load;this.renderTrial=render;').runInContext(context);
  return {ids,context};
}
function frame(actor) {
  return {definition:{id:actor.toLowerCase()+'-help',actor,lifeStage:'Child',relationship:'Acquaintance',scene:'Newcomer helped '+actor,intention:{kind:'Thanked',target:'Newcomer',motive:'Gratitude',stance:'Sincere'},authoredExamples:[{text:'I will say thanks.',note:'Withheld'}]},voice:'Short clauses',voiceSources:[],references:[{text:'An actual line.',nodeIndex:0,speaker:actor,attribution:'dialogue-asset',source:{asset:'Characters/Dialogue/'+actor,key:'Mon'},conditions:{rawKey:'Mon'}}],observations:[{role:'profile-only',replicate:1,seed:7,thought:'I should thank them.',formatValid:true,elapsedMilliseconds:123,copyFlags:[]},{role:'dialogue-reference',replicate:1,seed:7,thought:null,error:'Timed out',formatValid:false,elapsedMilliseconds:5000,copyFlags:[]}]};
}
function trial(cases) { return {suite:'wording',cases,answerCount:cases.length*2,failureCount:cases.length,copyFlagCount:0,model:'test',gameVersion:'test'}; }
test('Vincent is the starting view and switching characters shows only the selected fixed frame',()=>{
 const {ids,context}=viewer();context.loadTrial(trial([frame('George'),frame('Vincent')]));
 assert.equal(ids.person.value,'Vincent');assert.match(ids.cases.textContent,/Vincent · vincent-help/);assert.doesNotMatch(ids.cases.textContent,/George · george-help/);
 ids.person.value='George';context.renderTrial();assert.match(ids.cases.textContent,/George · george-help/);assert.doesNotMatch(ids.cases.textContent,/Vincent · vincent-help/);
});
test('failed generator replies stay visible without substituting authored wording',()=>{
 const {ids,context}=viewer();const c=frame('Vincent');c.observations[1].candidateThought='The invalid candidate is still here.';context.loadTrial(trial([c]));
 assert.match(ids.cases.textContent,/Failed reply: Timed out/);assert.match(ids.cases.textContent,/Format failed/);
 const card=ids.cases.children[0];const arms=card.children.find(n=>n.className==='grid');
 assert.doesNotMatch(arms.children[1].textContent,/I will say thanks/);
 assert.match(arms.children[1].textContent,/The invalid candidate is still here/);
});
test('source and generated markup render as literal text and copy flags identify the source',()=>{
 const {ids,context}=viewer();const c=frame('Vincent');c.references[0].text='<img src=x onerror=alert(1)>';c.observations[0].thought='</script><script>alert(1)</script>';c.observations[0].copyFlags=[{referenceId:'Vincent:Mon',match:'exact normalized line'}];
 c.observations[0].seed=4434080816128553118;c.observations[0].prompt='{"seed":4434080816128553118}';
 c.observations[0].receipt={seed:4434080816128553118};c.observations[0].rawResponse='{"seed":4434080816128553118}';
 context.loadTrial(trial([c]));assert.match(ids.cases.textContent,/<img src=x onerror=alert\(1\)>/);assert.match(ids.cases.textContent,/<script>alert\(1\)<\/script>/);assert.match(ids.cases.textContent,/exact normalized line \(Vincent:Mon\)/);assert.doesNotMatch(ids.cases.textContent,/\[object Object\]/);
 assert.match(ids.cases.textContent,/matching seed 4434080816128553118/);
 assert.match(ids.cases.textContent,/"seed": "4434080816128553118"/);
});
test('the viewer rejects old decision trials instead of implying fixed-intention comparison',()=>{
 const {context}=viewer();assert.throws(()=>context.loadTrial({suite:'voices',comparisons:[]}),/fixed-intention/);
});
