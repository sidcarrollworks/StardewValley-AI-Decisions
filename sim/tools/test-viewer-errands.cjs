// Errands in the viewer (needs spec): each errand act names the thing from the recording's favours
// and promises, a refused favour reads as one (not as an invitation), and the errand's note says why
// it was answered so and how the promise stands, only once the clock reaches each part.
// Dependency-free: node --test sim/tools/test-viewer-errands.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');

const page = readFileSync(join(__dirname, '../viewer/index.html'), 'utf8');
const script = page.slice(page.indexOf('<script>') + 8, page.lastIndexOf('</script>'));
const verbs = script.slice(script.indexOf('const VERB = {'), script.indexOf('const MOTIVE = {'));
const helpers = script.slice(script.indexOf('const RULER_INSET'), script.indexOf('function drawStrip()'));

// Pam needs beer: Penny says no (act 0); Sam says yes (act 1), buys it (act 2) and brings it late (act 3).
const favours = [
  { actId: 0, tick: 600, asker: 'Pam', asked: 'Penny', item: 'beer', accepted: false, tired: 0, busy: 0, low: 0, bond: 1, eager: 0.75, reluctant: true, short: false, running: false },
  { actId: 1, tick: 700, asker: 'Pam', asked: 'Sam', item: 'beer', accepted: true, tired: 0.7, busy: 0, low: 0, bond: 0.3, eager: 0.4, reluctant: false, short: false, running: false },
];
const promise = { id: 0, kind: 'Errand', by: 'Sam', to: 'Pam', made: 700, madeAct: 1, dueTo: 4320, state: 'Kept', settled: 4400, settledAct: 3, late: true, item: 'beer', fetchedAct: 2 };

function scene(withErrands = true) {
  const context = createContext({ R: null, S: { t: 0 }, dateOf: (t) => `day ${Math.floor(t / 1440)}` });
  new Script(verbs + helpers).runInContext(context);
  const m = { acts: [{ tick: 600 }, { tick: 700 }, { tick: 900 }, { tick: 4400 }] };
  if (withErrands) {
    // As the run model builds them.
    m.favourByAct = new Map(favours.map((x) => [x.actId, x]));
    m.promiseByAct = new Map([[1, promise], [2, promise], [3, promise]]);
    m.errandItemByAct = new Map([[0, 'beer'], [1, 'beer'], [2, 'beer'], [3, 'beer']]);
  }
  context.R = m;
  return context;
}

const verb = (c, kind, ctx) => new Script(`VERB[${JSON.stringify(kind)}](${JSON.stringify(ctx)})`).runInContext(c);

test('errand acts name the thing, and a refused favour is not an invitation', () => {
  const c = scene();
  assert.equal(verb(c, 'AskedFavour', { id: 1, target: 'Sam' }), 'asked Sam to fetch beer');
  assert.equal(verb(c, 'Fetched', { id: 2, target: 'Gus' }), 'bought beer from Gus');
  assert.equal(verb(c, 'HandedOver', { id: 3, target: 'Pam' }), 'brought Pam the beer they asked for');
  assert.equal(verb(c, 'LetDown', { id: 3, target: 'Pam' }), 'never brought Pam the beer they asked for');
  assert.equal(verb(c, 'Refused', { id: 0, target: 'Pam' }), "turned down Pam's request for beer");
  assert.equal(verb(c, 'Refused', { id: 9, target: 'Sam' }), "turned down Sam's invitation");
});

test('an older recording without errands still reads', () => {
  const c = scene(false);
  assert.equal(verb(c, 'AskedFavour', { id: 1, target: 'Sam' }), 'asked Sam to fetch something');
  assert.equal(verb(c, 'Refused', { id: 0, target: 'Pam' }), "turned down Pam's invitation");
  assert.equal(c.errandNote(1, 5000), '');
});

test('the note says why, and how the promise stands, only once the clock reaches it', () => {
  const c = scene();
  assert.equal(c.errandNote(0, 599), '');
  assert.equal(c.errandNote(0, 600), "Pam asked Penny to fetch beer; Penny said no (won't fetch it)");
  assert.equal(c.errandNote(1, 700), 'Pam asked Sam to fetch beer; Sam said yes (tired). Sam promised Pam beer, due by day 2: not yet bought');
  assert.equal(c.errandNote(2, 900), 'Sam promised Pam beer, due by day 2: bought, not yet handed over');
  assert.equal(c.errandNote(3, 4399), 'Sam promised Pam beer, due by day 2: bought, not yet handed over');
  assert.equal(c.errandNote(3, 4400), 'Sam promised Pam beer, due by day 2: brought late');
  const broken = scene();
  broken.R.promiseByAct.set(3, { ...promise, state: 'Broken', late: false, fetchedAct: -1 });
  assert.equal(broken.errandNote(3, 4400), 'Sam promised Pam beer, due by day 2: never brought');
});
