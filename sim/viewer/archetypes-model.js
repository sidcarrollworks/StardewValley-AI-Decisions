/* Schema 1 editor contract; matches CharacterInspirationPack.CopyValidated. */
(function (root) {
  'use strict';
  const copy = value => JSON.parse(JSON.stringify(value));
  const compose = (a, accent = '') => `Drives: ${a.drives} Coping: ${a.coping} Voice: ${a.voice}${accent.length ? ' ' + accent : ''}`;
  function problems(pack) {
    const errors = [], control = /[\u0000-\u001f\u007f-\u009f]/;
    const text = (value, max, label, empty = false) => {
      if (typeof value !== 'string' || (!empty && !value.trim()) || value.length > max || control.test(value))
        errors.push(`${label}: ${empty ? '0' : '1'}–${max} characters, without control characters.`);
    };
    const object = (value, keys, label) => {
      if (!value || typeof value !== 'object' || Array.isArray(value)) { errors.push(`${label}: expected an object.`); return false; }
      for (const key of Object.keys(value)) if (!keys.includes(key)) errors.push(`${label}: unsupported field “${key}”.`);
      return true;
    };
    const strings = (value, empty, label) => {
      if (!Array.isArray(value) || (!empty && !value.length)) { errors.push(`${label}: expected ${empty ? 'a' : 'a nonempty'} list.`); return; }
      value.forEach(v => text(v, 1000, label));
    };
    if (!object(pack, ['schema','provenance','archetypes','characters'], 'Pack')) return errors;
    if (pack.schema !== 1) errors.push('Only schema 1 packs are supported.');
    text(pack.provenance, 2000, 'Pack provenance');
    if (!Array.isArray(pack.archetypes) || !pack.archetypes.length || !Array.isArray(pack.characters)) return [...errors, 'A pack needs archetypes and a character list.'];
    const ids = new Map(), names = new Set();
    for (const a of pack.archetypes) {
      if (!object(a, ['id','drives','coping','voice','evaluatorCue','sources'], 'Archetype')) continue;
      text(a.id, 100, 'Archetype ID');
      if (ids.has(a.id)) errors.push(`Duplicate archetype: ${a.id}.`);
      ids.set(a.id, a);
      for (const field of ['drives','coping','voice']) text(a[field], 300, `${a.id} ${field}`);
      text(a.evaluatorCue, 90, `${a.id} decision cue`);
      text(compose(a), 360, `${a.id} composed voice`);
      strings(a.sources, false, `${a.id} sources`);
    }
    for (const c of pack.characters) {
      if (!object(c, ['name','archetypeId','voiceAccent','background','sources'], 'Character')) continue;
      text(c.name, 100, 'Character name');
      if (names.has(c.name)) errors.push(`Duplicate character: ${c.name}.`);
      names.add(c.name);
      const a = ids.get(c.archetypeId);
      if (!a) errors.push(`Unknown archetype for ${c.name}.`);
      text(c.voiceAccent, 300, `${c.name} voice accent`, true);
      if (a && typeof c.voiceAccent === 'string') text(compose(a, c.voiceAccent), 360, `${c.name} composed voice (including personal accent)`);
      if (c.sources != null) strings(c.sources, false, `${c.name} sources`);
      if (!Array.isArray(c.background)) { errors.push(`Missing background list for ${c.name}.`); continue; }
      const backgrounds = new Set();
      for (const b of c.background) {
        if (!object(b, ['id','text','subjects','sources','condition'], `${c.name} background`)) continue;
        text(b.id, 100, `${c.name} background ID`); text(b.text, 120, `${c.name}/${b.id} background`);
        if (backgrounds.has(b.id)) errors.push(`Duplicate background ${c.name}/${b.id}.`);
        backgrounds.add(b.id);
        if (b.condition != null) errors.push(`Conditional background ${c.name}/${b.id} is not initial self-knowledge.`);
        strings(b.subjects, true, `${c.name}/${b.id} subjects`); strings(b.sources, false, `${c.name}/${b.id} sources`);
      }
    }
    return errors;
  }
  function read(text) {
    const pack = JSON.parse(text), errors = problems(pack);
    if (errors.length) throw new Error(errors.join('\n'));
    return pack;
  }
  function create(pack, id, sourceId = null) {
    if (!/^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/.test(id) || id.length > 100) throw new Error('Use a unique lowercase ID with words separated by hyphens.');
    if (pack.archetypes.some(a => a.id === id)) throw new Error('That archetype ID already exists.');
    const source = sourceId ? pack.archetypes.find(a => a.id === sourceId) : null;
    if (sourceId && !source) throw new Error('The source archetype no longer exists.');
    const a = source ? copy(source) : { drives:'', coping:'', voice:'', evaluatorCue:'', sources:[`Authored in Archetype Studio: ${id}`] };
    a.id = id;
    // Personal accents, biographies, assignments and speech are deliberately not copied.
    return { ...copy(pack), archetypes:[...copy(pack.archetypes), a] };
  }
  function changes(base, pack) {
    const before = new Map(base.archetypes.map(a => [a.id, a]));
    return pack.archetypes.flatMap(a => !before.has(a.id) ? [{id:a.id, fields:['new archetype']}] :
      ['drives','coping','voice','evaluatorCue','sources'].some(k => JSON.stringify(a[k]) !== JSON.stringify(before.get(a.id)[k]))
        ? [{id:a.id, fields:['drives','coping','voice','evaluatorCue','sources'].filter(k => JSON.stringify(a[k]) !== JSON.stringify(before.get(a.id)[k]))}] : []);
  }
  function serialize(pack) { const errors = problems(pack); if (errors.length) throw new Error(errors.join('\n')); return JSON.stringify(pack, null, 2) + '\n'; }
  function savedDraft(base, pack) { return JSON.stringify({version:1, base:JSON.stringify(base), pack}); }
  function restore(text, base) {
    const draft = JSON.parse(text);
    if (draft.version !== 1 || draft.base !== JSON.stringify(base)) throw new Error('The source pack has changed. Download the saved draft to compare it before importing.');
    // Incomplete edits may be restored, but never exported without full validation.
    if (!draft.pack || draft.pack.schema !== 1 || typeof draft.pack.provenance !== 'string' ||
      !Array.isArray(draft.pack.archetypes) || !draft.pack.archetypes.length ||
      JSON.stringify(draft.pack.characters) !== JSON.stringify(base.characters) ||
      draft.pack.archetypes.some(a => !a || ['id','drives','coping','voice','evaluatorCue'].some(k => typeof a[k] !== 'string') ||
        !Array.isArray(a.sources) || a.sources.some(s => typeof s !== 'string')))
      throw new Error('The saved draft is damaged or changes personal character data.');
    return copy(draft.pack);
  }
  const api = {copy, compose, problems, read, create, changes, serialize, savedDraft, restore};
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.ArchetypeModel = api;
})(globalThis);
