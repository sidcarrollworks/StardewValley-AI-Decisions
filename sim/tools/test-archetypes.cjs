const {test} = require('node:test');
const assert = require('node:assert/strict');
const {readFileSync} = require('node:fs');
const {join} = require('node:path');
const M = require('../viewer/archetypes-model.js');
const original = M.read(readFileSync(join(__dirname,'../character-inspiration.json'),'utf8'));
const pack = () => M.copy(original);

test('studio round trip preserves the shipped pack and all personal accounts', () => {
  assert.deepEqual(M.read(M.serialize(original)), original);
  assert.equal(original.archetypes.length, 12);
});
test('duplicate copies only reusable fields and leaves characters and source pack unchanged', () => {
  const before=pack(), result=M.create(before,'test-copy',before.archetypes[0].id);
  assert.deepEqual(result.characters,before.characters);
  assert.equal(before.archetypes.length,original.archetypes.length);
  const copy=result.archetypes.at(-1);
  assert.deepEqual({...copy,id:before.archetypes[0].id},before.archetypes[0]);
  copy.sources.push('Authored revision'); assert.notDeepEqual(copy.sources,before.archetypes[0].sources);
  assert.ok(!result.characters.some(c=>c.archetypeId==='test-copy'));
});
test('new archetypes remain drafts until required content is complete; IDs cannot collide', () => {
  const p=pack(); assert.throws(()=>M.create(p,p.archetypes[0].id)); assert.throws(()=>M.create(p,'<script>'));
  const draft=M.create(p,'new-voice'); assert.throws(()=>M.serialize(draft),/drives/);
  Object.assign(draft.archetypes.at(-1),{drives:'Belonging',coping:'Listen',voice:'Direct',evaluatorCue:'Hear others out.'});
  assert.doesNotThrow(()=>M.serialize(draft));
});
test('validation accounts for individual accents as well as generic composed voice limits', () => {
  const p=pack(), a=p.archetypes[0], c=p.characters.find(c=>c.archetypeId===a.id);
  a.drives='x';a.coping='x';a.voice='x'.repeat(290); c.voiceAccent='x'.repeat(80);
  assert.ok(M.compose(a).length<=360);
  assert.ok(M.problems(p).some(e=>e.includes(c.name+' composed voice')));
  a.evaluatorCue='x'.repeat(91); assert.ok(M.problems(p).some(e=>e.includes('decision cue')));
});
test('unsupported schemas, unknown fields and conditional or malformed backgrounds cannot be exported', () => {
  for(const mutate of [p=>p.schema=2,p=>p.archetypes[0].invented='yes',p=>p.characters[0].archetypeId='missing',
    p=>p.characters[0].background[0].condition='after event',p=>p.characters[0].background[0].sources=[],p=>p.archetypes[0].voice='line\nbreak']){
    const p=pack();mutate(p);assert.throws(()=>M.serialize(p));
  }
});
test('change summaries distinguish additions, edits and reverted fields', () => {
  const p=pack();p.archetypes[0].coping+=' New thought.';
  assert.deepEqual(M.changes(original,p),[{id:p.archetypes[0].id,fields:['coping']}]);
  p.archetypes[0].coping=original.archetypes[0].coping;assert.deepEqual(M.changes(original,p),[]);
  assert.deepEqual(M.changes(original,M.create(p,'another-voice')),[{id:'another-voice',fields:['new archetype']}]);
});
test('unfinished drafts restore only against their exact source and preserve character data', () => {
  const p=pack();p.archetypes[0].drives='';const saved=M.savedDraft(original,p);
  assert.deepEqual(M.restore(saved,original),p);assert.throws(()=>M.serialize(M.restore(saved,original)));
  const revised=pack();revised.provenance+=' Correction.';assert.throws(()=>M.restore(saved,revised),/source pack has changed/);
  p.characters[0].name='Someone else';assert.throws(()=>M.restore(M.savedDraft(original,p),original),/personal character data/);
});
test('draft recovery rejects damaged editor shapes without losing the supplied recovery text', () => {
  const p=pack();p.archetypes[0].sources=null;const saved=M.savedDraft(original,p);
  assert.throws(()=>M.restore(saved,original),/damaged/);
  assert.equal(JSON.parse(saved).pack.archetypes[0].sources,null);
});
