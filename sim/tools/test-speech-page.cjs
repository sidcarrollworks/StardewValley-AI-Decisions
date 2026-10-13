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
new Script(rules + '\n;globalThis.api = { weight, effectiveChoices, lineProblems, catalogProblems, serialize, RELATIONSHIPS, MOODS };').runInContext(context);
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
  assert.deepEqual(plain(api.catalogProblems([line('Only archetype line here.', { archetypes: ['weary-defender'] })], ['GaveGift']).map((p) => p.problem)),
    ['every kind needs at least one line with no archetype']);
});

test('the shipped catalog has no problems and an export reproduces its stored text', () => {
  const stored = readFileSync(join(__dirname, '../UnderGlass.Sim/speech-catalog.json'), 'utf8').replace(/\r\n/g, '\n');
  const pack = JSON.parse(stored);
  assert.deepEqual(plain(api.catalogProblems(pack.lines, [...new Set(pack.lines.map((l) => l.kind))])), []);
  assert.equal(api.serialize(pack), stored.replace(/\n$/, ''));
});
