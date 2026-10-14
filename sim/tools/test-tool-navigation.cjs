const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');
const base = 'http://127.0.0.1:8766/sim/viewer/index.html';
const code = readFileSync(join(__dirname, '../viewer/tools.js'), 'utf8');
function scope(extra = {}) {
  const c = createContext({ URL, URLSearchParams, window: {}, document: { readyState: 'loading', addEventListener() {} }, ...extra });
  new Script(code).runInContext(c);
  return c.window.UnderGlassTools;
}
const api = scope();
const run = { format: 'under-glass-run', version: 1, names: ['Penny', 'Pam', 'Gus'] };

test('tool round trip retains exact recording, time, character and tab without nesting return URLs', () => {
  const source = api.recordingUrl('../../out/a run.html?variant=1#old', base);
  const speech = new URL(api.toolUrl('speech', source, { t: 2120, person: 'Penny', tab: 'inner' }, base));
  assert.equal(speech.searchParams.get('run'), 'http://127.0.0.1:8766/out/a%20run.html?variant=1');
  const studio = new URL(api.toolUrl('archetypes', speech.searchParams.get('run'), api.readState(speech.hash), speech.href));
  assert.equal(studio.pathname, '/sim/viewer/archetypes.html');
  const chains = new URL(api.toolUrl('chains', studio.searchParams.get('run'), api.readState(studio.hash), studio.href));
  assert.equal(chains.pathname, '/sim/viewer/chains.html');
  const dashboard = new URL(api.toolUrl('dashboard', chains.searchParams.get('run'), api.readState(chains.hash), chains.href));
  assert.equal(dashboard.pathname, '/sim/viewer/index.html');
  assert.equal(dashboard.hash, '#t=2120&person=Penny&tab=inner');
  assert.equal(dashboard.searchParams.get('run'), source);
});

test('recording URLs reject external origins, credentials and executable schemes', () => {
  for (const input of ['https://example.com/run.json', '//example.com/run.json', 'javascript:alert(1)', 'data:text/html,test', 'file:///C:/run.json', 'http://user:pass@127.0.0.1:8766/run.json'])
    assert.throws(() => api.recordingUrl(input, base));
  assert.throws(() => api.toolUrl('unknown', null, {}, base));
});

test('invalid view state cannot select an unknown tab or non-finite time', () => {
  for (const t of ['NaN', 'Infinity', '-1']) assert.equal(api.readState('#t=' + t + '&tab=bad').t, undefined);
  assert.equal(api.readState('#tab=bad').tab, undefined);
  assert.equal(api.readState('#t=12.9&person=Jas').t, 12);
});

test('JSON imports require a town recording, not a comparison report or catalog', () => {
  assert.deepEqual(Array.from(api.parseRecording(JSON.stringify(run)).names), run.names);
  for (const bad of [{ schema: 1, lines: [] }, { ...run, version: 2 }, { ...run, names: null }])
    assert.throws(() => api.parseRecording(JSON.stringify(bad)));
});

test('HTML import reads only the inert run-data node and refuses pages without one', () => {
  let selector;
  const withParser = scope({ DOMParser: class {
    parseFromString(text, type) {
      assert.equal(type, 'text/html');
      return { querySelector(s) { selector = s; return text === '<no run>' ? null : { textContent: JSON.stringify(run) }; } };
    }
  } });
  assert.deepEqual(Array.from(withParser.parseRecording('<saved dashboard>').names), run.names);
  assert.equal(selector, 'script#run-data[type="application/json"]');
  assert.throws(() => withParser.parseRecording('<no run>'), /does not contain/);
});

test('server reads refuse redirects, report missing files and do not fetch foreign recordings', async () => {
  let requests = 0;
  const reader = scope({ fetch: async (url, options) => {
    requests++;
    assert.equal(options.redirect, 'error');
    return { ok: false, status: 404 };
  } });
  await assert.rejects(reader.readRecording('/missing.json', base), /404/);
  await assert.rejects(reader.readRecording('https://example.com/run.json', base), /preview server/);
  assert.equal(requests, 1);
});

test('a local file does not inherit a server path in navigation', () => {
  const url = new URL(api.toolUrl('speech', null, { t: 10 }, base));
  assert.equal(url.searchParams.has('run'), false);
});

 test('illustrative Lives excerpts are not loaded as full dashboard or speech recordings', () => {
  const fixture = {...run, illustrative:true, lives:{version:'0.1'}};
  assert.throws(()=>api.compatibleRecording(fixture,'dashboard'), /Life chains/);
  assert.throws(()=>api.compatibleRecording(fixture,'speech'), /Life chains/);
  assert.equal(api.compatibleRecording(fixture,'chains'),fixture);
  assert.equal(api.compatibleRecording(run,'dashboard'),run);
});
