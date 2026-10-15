// The speech coverage page (sim/viewer/speech.html) computes which lines fit a speaker with its own copy of
// SpeechCatalog's rule. These tests hold that copy to the shared fixture the C# tests also read, to the
// loader's validation, and to the catalog's stored format. Dependency-free: node --test sim/tools/test-speech-page.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');

const page = readFileSync(join(__dirname, '../viewer/speech.html'), 'utf8');
const rules = page.slice(page.indexOf('// rules:begin'), page.indexOf('// rules:end'));
const context = createContext({});
new Script(rules + '\n;globalThis.api = { catalogVoices, weight, effectiveChoices, lineProblems, catalogProblems, serialize, draftEdits, mergeDraft, RELATIONSHIPS, MOODS };').runInContext(context);
const api = context.api;
// Arrays made inside the sandbox have its own prototype; compare plain copies.
const plain = (value) => JSON.parse(JSON.stringify(value));
const fixture = JSON.parse(readFileSync(join(__dirname, 'fixtures/speech-weights.json'), 'utf8'));
const line = (text, extra = {}) => ({ id: 'x', kind: 'GaveGift', archetypes: [], relationship: [], mood: [], text, tags: ['t'], ...extra });

test('the page weighs every fixture line exactly as SpeechCatalog.Weight does', () => {
  for (const c of fixture.cases) {
    const lines = fixture.pack.lines.filter((l) => l.kind === c.kind);
    assert.deepEqual(lines.map((l) => l.id).sort(), Object.keys(c.weights).sort());
    for (const l of lines)
      assert.equal(api.weight(l, c.archetype, c.relationship, c.mood), c.weights[l.id], `${l.id} for ${c.archetype}, ${c.relationship}, ${c.mood}`);
  }
});

test('effective choices count equally likely lines', () => {
  assert.equal(api.effectiveChoices([]), 0);
  assert.ok(Math.abs(api.effectiveChoices([2, 2, 2, 2]) - 4) < 1e-9);
  assert.ok(api.effectiveChoices([30, 1, 1, 1]) < 2);
});

test('the page rejects what SpeechCatalog.Read rejects', () => {
  const ids = new Map([['x', 1]]);
  assert.deepEqual(plain(api.lineProblems(line('For you, {subject}. Take it.'), ids)), []);
  for (const bad of [line('Too short'), line('a'.repeat(111)), line('Hello {name}, take this.'), line('Costs $5 but take it.'),
    line(' leading space is not allowed'), line('Fine line here.', { relationship: ['enemy'] }), line('Fine line here.', { mood: ['grim'] }),
    line('A line without a kind here.', { kind: '' }), line('Two lines\nin one string.')])
    assert.ok(api.lineProblems(bad, ids).length > 0, JSON.stringify(bad));
  assert.ok(api.lineProblems(line('One line here now.'), new Map([['x', 2]])).some((p) => /twice/.test(p)));
  // {item} names an errand's thing, and only errand lines may carry it (SpeechCatalog.ItemKinds).
  assert.deepEqual(plain(api.lineProblems(line('Could you bring me {item}, {subject}?', { kind: 'AskedFavour' }), ids)), []);
  assert.deepEqual(plain(api.lineProblems(line('Here is {item}, a bit late.', { kind: 'HandedOver.Late' }), ids)), []);
  assert.ok(api.lineProblems(line('Here is {item} for you.', { kind: 'GaveGift' }), ids).some((p) => /errand/.test(p)));
  // {item} is said in lower case ("some flour"), so it never starts a sentence.
  for (const text of ['{item}, as you asked. Enjoy.', 'There. {item}, as you asked.', 'Done! {item} for you.'])
    assert.ok(api.lineProblems(line(text, { kind: 'HandedOver' }), ids).some((p) => /start a sentence/.test(p)), text);
  assert.deepEqual(plain(api.lineProblems(line('There you go: {item}, as you asked.', { kind: 'HandedOver' }), ids)), []);
  assert.deepEqual(plain(api.catalogProblems([line('Only archetype line here.', { archetypes: ['weary-defender'] })], ['GaveGift']).map((p) => p.problem)),
    ['every kind needs at least one line with no archetype']);
});

test('the shipped catalog has no problems and an export reproduces its stored text', () => {
  const stored = readFileSync(join(__dirname, '../UnderGlass.Sim/speech-catalog.json'), 'utf8').replace(/\r\n/g, '\n');
  const pack = JSON.parse(stored);
  assert.deepEqual(plain(api.catalogProblems(pack.lines, [...new Set(pack.lines.map((l) => l.kind))])), []);
  assert.equal(api.serialize(pack), stored.replace(/\n$/, ''));
});

test('a draft made against another version replays its edits and never loses that version\'s lines', () => {
  const a = line('First line of the old version.', { id: 'a' }), b = line('Second line of the old version.', { id: 'b' });
  const added = line('A line the new version added.', { id: 'n' });
  // An older draft that kept only its lines: an edit to a and nothing about n.
  const legacy = { base: 'old', lines: [{ ...a, mood: ['low'] }, b] };
  const plan = api.draftEdits(legacy, [a, b, added]);
  assert.deepEqual(plain(plan.map((e) => e.id)), ['a']);
  const merged = api.mergeDraft([a, b, added], plan, false);
  assert.deepEqual(plain(merged.lines.map((l) => [l.id, l.mood])), [['a', ['low']], ['b', []], ['n', []]]);

  // A recorded draft: an edit whose line is unchanged applies; one this version changed or removed is
  // skipped; an addition under an id this version also uses is kept under a fresh id; a removal applies
  // only to the very line it removed.
  const changedB = { ...b, text: 'Second line, changed upstream.' };
  const edits = [
    { id: 'a', before: a, after: { ...a, text: 'First line, edited in the browser.' } },
    { id: 'b', before: b, after: { ...b, text: 'Second line, edited in the browser.' } },
    { id: 'gone', before: line('A line upstream removed.', { id: 'gone' }), after: line('Edited anyway.', { id: 'gone' }) },
    { id: 'n', before: null, after: line('The draft added this under n.', { id: 'n' }) },
    { id: 'c', before: null, after: line('The draft added this line.', { id: 'c' }) },
  ];
  const checked = api.mergeDraft([a, changedB, added], edits, true);
  const text = Object.fromEntries(checked.lines.map((l) => [l.id, l.text]));
  assert.equal(text.a, 'First line, edited in the browser.');
  assert.equal(text.b, 'Second line, changed upstream.');
  assert.equal(text.n, 'A line the new version added.');
  assert.equal(text['n-draft1'], 'The draft added this under n.');
  assert.equal(text.c, 'The draft added this line.');
  assert.ok(!('gone' in text));
  assert.deepEqual(plain(checked.skipped.map((e) => e.id)), ['b', 'gone']);
  assert.deepEqual(plain(checked.renamed), [{ from: 'n', to: 'n-draft1' }]);
  const removal = api.mergeDraft([a, changedB], [{ id: 'a', before: a, after: null }, { id: 'b', before: b, after: null }], true);
  assert.deepEqual(plain(removal.lines.map((l) => l.id)), ['b']);
});

test('Newcomer remains editable in an empty catalog and survives catalog round trips', () => {
  assert.deepEqual(plain(api.catalogVoices([])), ['newcomer']);
  const own=line('Hello, I just moved here.', {archetypes:['newcomer']});
  assert.equal(api.weight(own,'newcomer','stranger','even'),3);
  assert.equal(api.weight(own,null,'stranger','even'),0);
  assert.equal(api.weight(own,'weary-defender','stranger','even'),0);
  assert.deepEqual(plain(api.lineProblems(own,new Map([['x',1]]))),[]);
});
