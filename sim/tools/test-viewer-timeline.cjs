// Exercise the actual viewer helpers without a browser, a model, or third-party packages.
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');
const { test } = require('node:test');
const assert = require('node:assert/strict');
const page = readFileSync(join(__dirname, '../viewer/index.html'), 'utf8');
const script = page.slice(page.indexOf('<script>') + 8, page.lastIndexOf('</script>'));
new Script(script); // Also reject syntax errors anywhere in the shipped viewer.
const helpers = script.slice(script.indexOf('const RULER_INSET'), script.indexOf('function drawStrip()'));
const scope = script.slice(script.indexOf('function observerScope('), script.indexOf('function momentTick('));

function scene() {
  const context = createContext({
    R: {
      end: 999, names: ['Evelyn', 'George', 'Penny'],
      people: [{ district: 'core' }, { district: 'core' }, { district: 'east' }],
      kinds: [{ tier: 'News' }, { tier: 'Scandal' }],
      acts: [{ id: 0, kind: 0 }, { id: 1, kind: 1 }],
      ties: [{ a: 0, b: 1, what: 'friendship' }],
      reflections: [{ request: { actor: 'Penny' }, events: [{ status: 'considered' }] }],
      timeline: [
        { type: 'act', tick: 100, people: [0, 1], act: 0 },
        { type: 'act', tick: 200, people: [2], act: 1 },
        { type: 'tie', tick: 300, people: [0, 1], tie: 0 },
        { type: 'reflection', tick: 400, people: [2], reflection: 0, event: 0 },
      ],
      actText: (act) => act.id === 0 ? 'Evelyn helped George' : 'Penny stole',
    },
    S: { person: -1, hood: 'all', t: 250 },
    when: (tick) => `minute ${tick}`,
    reflectionAt: (record, tick) => ({ request: record.request, answer: { thought: 'A private idea' } }),
    isWakingDream: () => false,
    reflectionEventText: () => 'Recorded outcome',
  });
  new Script(scope + helpers).runInContext(context);
  return context;
}

test('unfocused timeline shows turning points, not every routine act', () => {
  const c = scene();
  assert.deepEqual(Array.from(c.timelineMarkers(1000), (m) => m.event.tick), [200, 300]);
});

test('following includes received encounters and ties but excludes other people’s private thoughts', () => {
  const c = scene();
  c.S.person = 1;
  assert.deepEqual(Array.from(c.timelineMarkers(1000).filter((m) => m.focused), (m) => m.event.tick), [100, 300]);
  c.S.person = 2;
  assert.deepEqual(Array.from(c.timelineMarkers(1000).filter((m) => m.focused), (m) => m.event.tick), [200, 400]);
  c.S.hood = 'core';
  assert.equal(c.timelineMarkers(1000).filter((m) => m.focused).length, 0);
});

test('nearby hits stay on their own row and choose the nearest exact event time', () => {
  const c = scene();
  c.S.person = 1;
  c.R.timeline.push({ type: 'act', tick: 103, people: [0, 1], act: 0 });
  const x = 6 + .103 * 988;
  const hits = c.timelineHits(x, 44, 1000);
  assert.equal(hits.length, 2);
  assert.equal(hits[0].event.tick, 103);
  assert.equal(c.timelineHits(x, 17, 1000).length, 0);
});

test('future descriptions never access the recorded act, thought or relationship content', () => {
  const c = scene();
  c.R.actText = c.reflectionAt = () => { throw new Error('Future content was accessed'); };
  c.R.ties = new Proxy([], { get() { throw new Error('Future tie was accessed'); } });
  for (const event of c.R.timeline)
    assert.match(c.timelineDescription(event, 0), /Upcoming recorded moment/);
});

test('descriptions follow the current clock when stepping and rewinding', () => {
  const c = scene(), act = c.R.timeline[0], thought = c.R.timeline[3];
  assert.equal(c.timelineDescription(act, 100), 'Evelyn helped George');
  assert.equal(c.timelineDescription(thought, 400), 'Penny imagined: A private idea');
  assert.match(c.timelineDescription(thought, 399), /Upcoming recorded moment/);
  assert.equal(c.timelineDescription(c.R.timeline[2], 300), 'Evelyn and George became friends');
});

test('clicking a marker pauses and jumps exactly; empty ruler space retains scrubbing', () => {
  const c = scene();
  c.S.person = 1;
  c.$ = () => ({ getBoundingClientRect: () => ({ left: 10, top: 20, width: 1000 }) });
  const calls = [];
  c.setPlaying = (on) => calls.push(['playing', on]);
  c.hideTip = () => {};
  c.setTime = (tick) => calls.push(['time', tick]);
  c.clickTimeline({ clientX: 10 + 6 + .1 * 988, clientY: 64 });
  assert.deepEqual(calls, [['playing', false], ['time', 100]]);
  calls.length = 0;
  c.clickTimeline({ clientX: 510, clientY: 22 });
  assert.deepEqual(calls, [['playing', false], ['time', 500]]);
});

function reflectionScene() {
  const context = createContext({ R: {
    names: ['Penny', 'Pierre'],
    kinds: [{ name: 'Thanked' }, { name: 'Apologised' }],
    acts: [{ id: 3, kind: 0, tick: 200 }, { id: 4, kind: 1, tick: 300 }],
    life: [{ person: 0, act: 4, role: 'Did', tick: 300, resolved: 310, outcome: 'Refused' }],
  } });
  const summary = script.slice(script.indexOf('function reflectionSummary('), script.indexOf('function observerItemsAt('));
  const inner = script.slice(script.indexOf('function reflectionAt('), script.indexOf('function reflectionJump('));
  const text = script.slice(script.indexOf('function reflectionEventText('), script.indexOf('function reflectionCard('));
  new Script(summary + inner + text).runInContext(context);
  return context;
}

test('new reflection actions describe thanks and apologies after the decision becomes known', () => {
  const c = reflectionScene();
  const record = { request: { tick: 100, actor: 'Penny', subject: 'Pierre', choices: [
    { id: 'thank', kind: 'Thanked', line: 'Thank you.' }, { id: 'apologise', kind: 'Apologised', line: 'I am sorry.' }],
  }, answer: { thought: 'I could thank him.' }, choice: 'thank', events: [
    { tick: 101, status: 'considered' }, { tick: 101, status: 'waiting' },
  ] };
  assert.equal(c.reflectionAt(record, 99), null);
  assert.match(c.reflectionSummary(c.reflectionAt(record, 100), 100).title, /reflecting/);
  assert.equal(c.reflectionSummary(c.reflectionAt(record, 101), 101).title, 'Penny wants to thank Pierre');
  record.choice = 'apologise';
  assert.equal(c.reflectionSummary(c.reflectionAt(record, 101), 101).title, 'Penny wants to apologise to Pierre');
  assert.equal(c.reflectionAt(record, 100).choice, null);
});

test('thanks completion and apology outcomes use the recorded act and cannot leak later answers', () => {
  const c = reflectionScene(), request = { actor: 'Penny', subject: 'Pierre' };
  assert.match(c.reflectionEventText({ tick: 203, status: 'outcome', actId: 3,
    text: "The encounter's recorded outcome: None." }, request, 203), /thank-you finished/);
  const event = { tick: 310, status: 'outcome', actId: 4, text: "The encounter's recorded outcome: Refused." };
  assert.equal(c.reflectionEventText(event, request, 309), '');
  assert.equal(c.reflectionEventText(event, request, 310), 'Pierre refused the apology.');
  assert.equal(c.reflectionEventText({ tick: 300, status: 'outcome', actId: 4,
    text: "The encounter's recorded outcome: None." }, request, 300), 'This encounter has no tracked response.');
});
