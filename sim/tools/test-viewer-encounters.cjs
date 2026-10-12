const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');
const { test } = require('node:test');
const assert = require('node:assert/strict');
const page = readFileSync(join(__dirname, '../viewer/index.html'), 'utf8');
const script = page.slice(page.indexOf('<script>') + 8, page.lastIndexOf('</script>'));
new Script(script);
const helpers = script.slice(script.indexOf('function encounterView('), script.indexOf('// End pure encounter helpers.'));
const context = createContext({});
new Script(helpers + ';globalThis.view = encounterView; globalThis.groups = encounterGroups;').runInContext(context);
const plain = (x) => JSON.parse(JSON.stringify(x));

test('old recordings stay unclassified instead of acquiring invented reasons', () => {
  assert.deepEqual(plain(context.view(undefined, 100)), { category: 'unknown', label: 'Context unavailable', views: [] });
});
test('future appraisals are never inspected and rewinding restores the earlier account', () => {
  const old = { account: { holder: 'Penny', tick: 10 }, category: 'everyday' };
  const later = { account: { holder: 'Penny', tick: 20 }, category: 'relationship' };
  const future = { account: { holder: 'George', tick: 30 }, get category() { throw Error('future leak'); } };
  assert.equal(context.view([old, later, future], 15).category, 'everyday');
  assert.equal(context.view([old, later, future], 25).category, 'relationship');
  assert.equal(context.view([old, later, future], 15).views[0], old);
});
test('reciprocal everyday care groups all three source events and hides later members on rewind', () => {
  const acts = [
    { id: 0, tick: 1010, actor: 0, target: 1, kind: 0, place: 0 },
    { id: 1, tick: 1048, actor: 0, target: 1, kind: 0, place: 0 },
    { id: 2, tick: 1053, actor: 1, target: 0, kind: 0, place: 0 },
  ];
  assert.deepEqual(plain(context.groups(acts, () => 'everyday', 1053)).map(g => g.map(a => a.id)), [[0, 1, 2]]);
  assert.deepEqual(plain(context.groups(acts, () => 'everyday', 1049)).map(g => g.map(a => a.id)), [[0, 1]]);
});
test('meaningful exceptions and unknown records retain individual events', () => {
  const acts = [0, 1, 2].map(id => ({ id, tick: 10 + id, actor: 0, target: 1, kind: 0, place: 0 }));
  assert.deepEqual(plain(context.groups(acts, () => 'relationship', 50)).map(g => g.length), [1, 1, 1]);
  assert.deepEqual(plain(context.groups(acts, () => 'unknown', 50)).map(g => g.length), [1, 1, 1]);
});
test('different places, kinds and distant occurrences are not merged', () => {
  const acts = [
    { id: 0, tick: 10, actor: 0, target: 1, kind: 0, place: 0 },
    { id: 1, tick: 11, actor: 0, target: 1, kind: 1, place: 0 },
    { id: 2, tick: 12, actor: 0, target: 1, kind: 0, place: 1 },
    { id: 3, tick: 110, actor: 0, target: 1, kind: 0, place: 0 },
  ];
  assert.deepEqual(plain(context.groups(acts, () => 'everyday', 200)).map(g => g.length), [1, 1, 1, 1]);
});
