const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { Script, createContext } = require('node:vm');
const read = (name) => readFileSync(join(__dirname, '../viewer', name), 'utf8');
const speech = read('speech.html');
const coverage = createContext({});
new Script(speech.match(/const AVOIDED = .*?;/)[0] + '\n' + speech.match(/const median = .*?; };/)[0]
  + '\n' + speech.slice(speech.indexOf('// coverage:begin'), speech.indexOf('// coverage:end'))
  + '\nthis.summary = coverageSummary;').runInContext(coverage);
const plain = (v) => JSON.parse(JSON.stringify(v));

test('coverage excludes unreachable situations and counts untyped speakers separately', () => {
  assert.deepEqual(plain(coverage.summary([
    { arch:'carer', possible:true, n:3, own:1 },
    { arch:'carer', possible:true, n:7, own:0 },
    { arch:'builder', possible:true, n:0, own:0 },
    { arch:'builder', possible:false, n:0, own:0 },
    { arch:'', possible:true, n:12, own:0 },
  ])), { total:3, median:3, thin:2, noOwn:2, plainMedian:12 });
});

test('empty coverage has finite values and a changed line pool updates the summary', () => {
  assert.deepEqual(plain(coverage.summary([])), { total:0, median:0, thin:0, noOwn:0, plainMedian:0 });
  const situations = [{ arch:'carer', possible:true, n:3, own:0 }];
  const before = coverage.summary(situations);
  situations[0] = { ...situations[0], n:4, own:1 };
  const after = coverage.summary(situations);
  assert.equal(before.thin, 1); assert.equal(after.thin, 0);
  assert.equal(before.noOwn, 1); assert.equal(after.noOwn, 0);
  assert.equal(after.median, 4);
});

test('shared tools retain the standalone dashboard palette, typography and control primitives', () => {
  const primitives = ':root {' + read('index.html').split(':root {')[1].split('/* The masthead:')[0];
  const normalize = (s) => s.replace(/\s+/g, ' ').trim();
  assert.ok(normalize(read('ui.css')).includes(normalize(primitives)));
  for (const name of ['speech.html', 'morning.html', 'tools.html']) {
    assert.match(read(name), /href="ui\.css(?:\?[^" ]+)?"/);
    assert.match(read(name), /src="ui.js"/);
    assert.match(read(name), /data-theme-toggle/);
  }
});

function theme({ saved = null, host = null, system = false, blocked = false } = {}) {
  const attrs = { 'data-theme':host }, listeners = {}, docListeners = {}, buttonEvents = {}, pressed = {};
  const button = { setAttribute:(k,v) => pressed[k] = v, addEventListener:(k,v) => buttonEvents[k] = v };
  const watch = { matches:system, addEventListener:(k,v) => listeners.media = v };
  const localStorage = {
    getItem(key) { assert.equal(key, 'under-glass-mode'); if (blocked) throw Error('blocked'); return saved; },
    setItem(key, value) { assert.equal(key, 'under-glass-mode'); if (blocked) throw Error('blocked'); saved = value; },
  };
  const document = { documentElement:{ getAttribute:(k) => attrs[k], setAttribute:(k,v) => attrs[k] = v },
    querySelectorAll:() => [button], addEventListener:(k,v) => docListeners[k] = v };
  new Script(read('ui.js')).runInContext(createContext({ document, localStorage,
    window:{ matchMedia:() => watch, addEventListener:(k,v) => listeners[k] = v },
    MutationObserver:class { observe() {} },
  }));
  docListeners.DOMContentLoaded();
  return { attrs, pressed, listeners, click:() => buttonEvents.click(), setSaved:(v) => saved = v };
}

test('theme uses the dashboard preference and responds to changes from another tool', () => {
  const t = theme({ saved:'light', host:'dark', system:true });
  assert.equal(t.attrs['data-mode'], 'light');
  assert.equal(t.pressed['aria-pressed'], 'false');
  t.click(); assert.equal(t.attrs['data-mode'], 'dark');
  t.setSaved('light'); t.listeners.storage({ key:'under-glass-mode' });
  assert.equal(t.pressed['aria-pressed'], 'false');
  t.setSaved(null); t.listeners.storage({ key:null });
  assert.equal(t.attrs['data-mode'], 'dark');
});

test('theme falls back to host or system when storage is unavailable or invalid', () => {
  assert.equal(theme({ saved:'invalid', system:true }).attrs['data-mode'], 'dark');
  const t = theme({ blocked:true, host:'light', system:true });
  assert.equal(t.attrs['data-mode'], 'light');
  t.click(); assert.equal(t.attrs['data-mode'], 'dark');
});
