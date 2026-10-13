// Spoken lines in the viewer: shown only once the clock reaches them, never invented for older
// recordings, and never read for a future act. Dependency-free: node --test sim/tools/test-viewer-speech.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');

const page = readFileSync(join(__dirname, '../viewer/index.html'), 'utf8');
const script = page.slice(page.indexOf('<script>') + 8, page.lastIndexOf('</script>'));
const helpers = script.slice(script.indexOf('const RULER_INSET'), script.indexOf('function drawStrip()'));
const scope = script.slice(script.indexOf('function observerScope('), script.indexOf('function momentTick('));

function scene(speech) {
  const context = createContext({
    R: { end: 2000, names: ['Evelyn', 'George'], people: [{ family: [] }, { family: [] }], kinds: [{ tier: 'News', name: 'HelpedSomeone' }],
      acts: [{ id: 0, kind: 0, tick: 100 }, { id: 1, kind: 0, tick: 300 }], ties: [], reflections: [],
      timeline: [{ type: 'act', tick: 100, people: [0, 1], act: 0 }, { type: 'act', tick: 300, people: [1, 0], act: 1 }],
      actText: (a) => (a.id === 0 ? 'Evelyn helped George' : 'George helped Evelyn'), speechByAct: speech },
    S: { person: -1, hood: 'all', t: 0 }, when: (t) => `d${Math.floor(t / 1440)}`,
    reflectionAt: () => null, isWakingDream: () => false, reflectionEventText: () => '',
  });
  new Script(helpers + scope).runInContext(context);
  return context;
}

test('a line appears with its act once the clock reaches it, and not before', () => {
  const c = scene(new Map([[0, { actId: 0, tick: 100, speaker: 'Evelyn', line: 'Here, let me.' }]]));
  const act = c.R.timeline[0];
  assert.match(c.timelineDescription(act, 99), /Upcoming recorded moment/);
  assert.equal(c.timelineDescription(act, 100), 'Evelyn helped George · “Here, let me.”');
  assert.equal(c.speechOf(0, 99), '');
  assert.equal(c.speechOf(0, 100), 'Here, let me.');
});

test('an act without a recorded line reads as before, and an old recording has no table', () => {
  const c = scene(new Map([[0, { actId: 0, tick: 100, line: 'Here, let me.' }]]));
  assert.equal(c.timelineDescription(c.R.timeline[1], 300), 'George helped Evelyn');
  const old = scene(undefined);
  assert.equal(old.speechOf(0, 100), '');
  assert.equal(old.timelineDescription(old.R.timeline[0], 100), 'Evelyn helped George');
});

test('a future act never touches the speech table', () => {
  const c = scene(new Proxy(new Map(), { get() { throw new Error('Future speech was accessed'); } }));
  for (const event of c.R.timeline) assert.match(c.timelineDescription(event, 0), /Upcoming recorded moment/);
});

test('a line is text, never markup', () => {
  const c = scene(new Map([[0, { actId: 0, tick: 100, line: '<b>bold</b> & "quoted"' }]]));
  assert.equal(c.speechOf(0, 100), '<b>bold</b> & "quoted"');
  assert.equal(c.timelineDescription(c.R.timeline[0], 100), 'Evelyn helped George · “<b>bold</b> & "quoted"”');
});
