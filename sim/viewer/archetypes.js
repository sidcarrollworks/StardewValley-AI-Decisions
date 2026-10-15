/* Local drafts only: importing/exporting never writes a repository file or changes a run. */
(() => {
  'use strict';
  const M = window.ArchetypeModel, $ = id => document.getElementById(id);
  const KEY = 'under-glass-archetype-draft-v1';
  const EDIT_NOTE = ' Studio revisions are authored edits; inherited source references require review.';
  let base, pack, selected, catalog = null, catalogError = '', storageWorks = true, pendingDraft = null, cloneFrom = null;
  let mode='archetypes';
  const label = id => id.replaceAll('-', ' ');
  const node = (tag, text, className) => { const e = document.createElement(tag); if (text != null) e.textContent = text; if (className) e.className = className; return e; };
  const dirty = () => !!base && JSON.stringify(pack) !== JSON.stringify(base);
  const error = message => { $('studio-error').textContent = message; $('studio-error').hidden = !message; };
  function confirmAction(message, action) {
    const opener = document.activeElement;
    $('confirm-message').textContent = message; $('confirm-action').hidden = false;
    $('confirm-no').onclick = () => { $('confirm-action').hidden = true; opener?.focus(); };
    $('confirm-yes').onclick = () => { $('confirm-action').hidden = true; action(); opener?.focus(); };
    $('confirm-no').focus();
  }
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && !$('confirm-action').hidden) $('confirm-no').click();
  });
  function download(text, filename) {
    const url = URL.createObjectURL(new Blob([text], {type:'application/json'})), a = node('a');
    a.href = url; a.download = filename; document.body.append(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  function save() {
    if (!dirty()) { updateStatus(); return; }
    try { localStorage.setItem(KEY, M.savedDraft(base, pack)); } catch { storageWorks = false; }
    updateStatus();
  }
  function edited() {
    if (!pack.provenance.includes(EDIT_NOTE.trim())) pack.provenance += EDIT_NOTE;
    $('provenance').value = pack.provenance;
    save(); renderList();
  }
  function updateStatus() {
    const issues = M.problems(pack), changes = M.changes(base, pack);
    $('export-pack').disabled = issues.length > 0 || !!pendingDraft;
    $('discard-draft').disabled = !dirty();
    $('draft-status').textContent = !storageWorks ? 'Browser storage unavailable · download to keep edits' : dirty() ? 'Draft saved in this browser' : 'Source pack · no edits';
    $('change-count').textContent = dirty() ? `· ${changes.length} profiles changed` : '';
    $('change-list').replaceChildren(...changes.map(c => node('li', `${c.kind==='character'?'Character · ':''}${label(c.id)}: ${c.fields.join(', ')}`)));
    if (pack.provenance !== base.provenance) $('change-list').append(node('li', 'Pack provenance updated.'));
    const problems = $('validation');
    if (problems) { problems.textContent = issues.length ? 'Before export:\n' + issues.join('\n') : ''; problems.hidden = !issues.length; }
    if(mode==='characters') {
      const c=pack.characters.find(c=>c.name===selected), a=pack.archetypes.find(a=>a.id===c?.archetypeId);
      if(c&&a){const length=M.compose(a,c.voiceAccent).length;
        $('budget-text').textContent=`${length}/360 voice characters, including archetype`;
        $('voice-budget').value=Math.min(360,length);$('voice-preview').textContent=M.compose(a,c.voiceAccent);
        $('count-voiceAccent').textContent=`${c.voiceAccent.length}/300`;
        c.background.forEach((b,i)=>{if($('context-count-'+i))$('context-count-'+i).textContent=`${b.text.length}/120`;});
      }
      return;
    }
    const a = pack.archetypes.find(a => a.id === selected);
    if (!a) return;
    const lengths = [M.compose(a).length, ...pack.characters.filter(c => c.archetypeId === selected).map(c => M.compose(a,c.voiceAccent).length)];
    if ($('voice-budget')) {
      $('voice-budget').value = Math.min(360, Math.max(...lengths));
      $('budget-text').textContent = `${M.compose(a).length}/360 voice characters · ${Math.max(...lengths)}/360 with longest personal accent`;
    }
    for (const key of ['drives','coping','voice','evaluatorCue']) if ($(`count-${key}`)) $(`count-${key}`).textContent = `${a[key].length}/${key === 'evaluatorCue' ? 90 : 300}`;
  }
  function renderList() {
    if (!pack) return;
    if(mode==='characters'){
      const q=$('search').value.toLowerCase(), changed=new Set(M.changes(base,pack).filter(c=>c.kind==='character').map(c=>c.id));
      const entries=pack.characters.filter(c=>[c.name,label(c.archetypeId),c.voiceAccent,...c.background.map(b=>b.text)].join(' ').toLowerCase().includes(q));
      $('list-count').textContent=`${entries.length} of ${pack.characters.length} characters`;
      $('archetype-list').replaceChildren(...entries.map(c=>{const b=node('button',null,'archetype-pick');b.type='button';b.setAttribute('aria-current',String(c.name===selected));
        b.append(node('span',c.name),node('span',`${label(c.archetypeId)}${changed.has(c.name)?' · edited':''}`,'meta'));
        b.onclick=()=>{selected=c.name;renderList();renderEditor();};return b;}));
      if(!entries.length)$('archetype-list').append(node('p','No matching characters.','meta'));
      return;
    }
    const q = $('search').value.toLowerCase(), changes = new Set(M.changes(base,pack).map(c=>c.id));
    const entries = pack.archetypes.filter(a => [label(a.id),a.drives,a.coping,a.voice,...pack.characters.filter(c=>c.archetypeId===a.id).map(c=>c.name)].join(' ').toLowerCase().includes(q));
    $('list-count').textContent = `${entries.length} of ${pack.archetypes.length} archetypes`;
    $('archetype-list').replaceChildren(...entries.map(a => {
      const b = node('button',null,'archetype-pick'); b.type='button'; b.setAttribute('aria-current',String(selected===a.id));
      const count=pack.characters.filter(c=>c.archetypeId===a.id).length;
      b.append(node('span',label(a.id)),node('span',`${count} character${count===1?'':'s'}${changes.has(a.id)?' · edited':''}`,'meta'));
      b.onclick=()=>{selected=a.id; renderList(); renderEditor();}; return b;
    }));
    if (!entries.length) $('archetype-list').append(node('p','No matching archetypes.','meta'));
  }
  function details(title) { const d=node('details',null,'tool-disclosure'); d.append(node('summary',title)); return d; }
  function field(a,key,title,hint,compact=false) {
    const box=node('div',null,compact?'studio-advanced':'studio-field'), l=node('label',title), input=node('textarea');
    input.id=`edit-${key}`; l.htmlFor=input.id; input.value=a[key]; input.rows=compact?2:4;
    const count=node('span',null,'meta'); count.id=`count-${key}`;
    const help=node('span',hint,'meta'); help.id=`help-${key}`; input.setAttribute('aria-describedby',`${help.id} ${count.id}`);
    input.oninput=()=>{a[key]=input.value; edited();};
    box.append(l,help,input,count); return box;
  }
  function renderEditor() {
    if(mode==='characters'){renderCharacter();return;}
    const host=$('archetype-editor'), a=pack.archetypes.find(a=>a.id===selected);
    if (!a) return;
    host.replaceChildren();
    const head=node('div',null,'studio-editor-head'), title=node('div'); title.append(node('span',a.id,'label'),node('h2',label(a.id)));
    const duplicate=node('button','Duplicate archetype'); duplicate.onclick=()=>openCreate(a.id); head.append(title,duplicate); host.append(head);
    const fields=node('div',null,'studio-fields'); fields.append(field(a,'drives','What matters','Motivations and competing priorities.'),field(a,'coping','Under pressure','Habitual responses when things are difficult.'),field(a,'voice','How they sound','Cadence, directness and emotional expression.')); host.append(fields);
    const budget=node('div',null,'studio-budget'), meter=node('progress'); meter.id='voice-budget'; meter.max=360; meter.setAttribute('aria-label','Composed voice length'); const budgetText=node('span'); budgetText.id='budget-text'; budget.append(meter,budgetText); host.append(budget);
    const validation=node('p',null,'studio-problems'); validation.id='validation'; validation.setAttribute('role','status'); host.append(validation);
    const advanced=details('Decision cue & source evidence');
    advanced.append(field(a,'evaluatorCue','Decision cue','Compact disposition supplied to Laya; 90 characters maximum.',true));
    const sourceLabel=node('label','Source references · one per line'), sources=node('textarea'); sources.id='edit-sources'; sourceLabel.htmlFor=sources.id; sources.rows=4; sources.value=a.sources.join('\n');
    sources.oninput=()=>{a.sources=sources.value.split('\n').map(v=>v.trim()).filter(Boolean); edited();};
    advanced.append(sourceLabel,sources,node('p','References support review; editing a tendency does not make it sourced canon.','meta')); host.append(advanced);
    const characters=details(`Characters using this archetype · ${pack.characters.filter(c=>c.archetypeId===a.id).length}`);
    characters.append(node('p','Personal accents and background context belong to each character and are not copied when you duplicate an archetype.','meta'));
    for (const c of pack.characters.filter(c=>c.archetypeId===a.id)) {
      const d=node('details',null,'studio-character'); d.append(node('summary',c.name),node('p',c.voiceAccent||'No additional voice accent.','meta'));
      const edit=node('button','Edit '+c.name);edit.onclick=()=>switchMode('characters',c.name);d.append(edit);
      const list=node('ul'); for (const b of c.background) {const li=node('li',b.text); li.append(node('p',b.sources.join(' · '),'studio-sources')); list.append(li);} d.append(list); characters.append(d);
    }
    host.append(characters);
    const lines=catalog?.lines?.filter(l=>l.archetypes?.includes(a.id))||[], examples=details(`Authored speech · ${catalog ? lines.length+' lines' : 'unavailable'}`);
    if (!catalog) examples.append(node('p',catalogError||'Loading speech examples…','meta'));
    else if (!lines.length) examples.append(node('p','No authored lines tagged with this archetype. Duplicating a profile does not duplicate its speech.','meta'));
    const shown=lines.slice(0,3), renderLine=l=>{const card=node('div',null,'studio-example'); card.append(node('span',l.kind,'label'),node('p',l.text)); return card;};
    examples.append(...shown.map(renderLine));
    if(lines.length>3){const more=details(`Show ${lines.length-3} more lines`); more.append(...lines.slice(3).map(renderLine)); examples.append(more);}
    const speech=node('a','Open speech editor →','key-link'); speech.href='speech.html'; speech.dataset.toolLink='speech'; examples.append(speech); host.append(examples);
    const preview=details('Exact generator voice preview'), text=node('p',M.compose(a),'studio-sources'); text.id='voice-preview'; preview.append(text); host.append(preview);
    // Refresh the preview as fields are typed without replacing the focused editor.
    fields.addEventListener('input',()=>{text.textContent=M.compose(a);});
    if(pendingDraft) {for(const input of host.querySelectorAll('button,textarea'))input.disabled=true;}
    updateStatus();
  }
  function switchMode(value, id){
    if(!pack)return;mode=value;selected=id??(mode==='characters'?pack.characters[0]?.name:pack.archetypes[0].id);
    $('search').value='';$('search-label').textContent=mode==='characters'?'Find a character':'Find an archetype';
    $('mode-archetypes').setAttribute('aria-pressed',String(mode==='archetypes'));$('mode-characters').setAttribute('aria-pressed',String(mode==='characters'));
    $('new-archetype').hidden=mode==='characters';$('create-form').hidden=true;renderList();renderEditor();
    $('archetype-editor').setAttribute('aria-label',mode==='characters'?'Selected character':'Selected archetype');
  }
  function renderCharacter(){
    const host=$('archetype-editor'),c=pack.characters.find(c=>c.name===selected);host.replaceChildren();
    if(!c){host.append(node('p','This pack has no named characters.','meta'));return;}
    host.append(node('h2',c.name));
    const assignment=node('div',null,'studio-assignment'),l=node('label','Archetype'),select=node('select');select.id='character-archetype';l.htmlFor=select.id;
    pack.archetypes.forEach(a=>{const o=node('option',label(a.id));o.value=a.id;select.append(o);});select.value=c.archetypeId;
    select.onchange=()=>{c.archetypeId=select.value;edited();renderCharacter();};
    const jump=node('button','Edit shared archetype');jump.onclick=()=>switchMode('archetypes',c.archetypeId);assignment.append(l,select,jump);host.append(assignment);
    host.append(field(c,'voiceAccent','Personal voice','What distinguishes this person within their archetype.',true));
    const meter=node('progress');meter.id='voice-budget';meter.max=360;meter.setAttribute('aria-label','Composed voice length');const budget=node('div',null,'studio-budget'),count=node('span');count.id='budget-text';budget.append(meter,count);host.append(budget);
    host.append(node('h3',`Background prompt lines · ${c.background.length}`),node('p','Personal context for inspiration. These lines are not simulated memories.','meta'));
    c.background.forEach((b,i)=>{
      const card=node('article',null,'studio-context'),text=node('textarea'),label=node('label','Background line '+(i+1));text.id='context-'+i;label.htmlFor=text.id;text.value=b.text;text.rows=2;
      const count=node('span',null,'meta');count.id='context-count-'+i;text.oninput=()=>{b.text=text.value;edited();};card.append(label,text,count);
      const more=details('Relevance & source references');
      const addInput=(title,value,fn)=>{const lab=node('label',title),input=node('textarea');input.value=value;input.rows=2;input.setAttribute('aria-label',title+' for background '+(i+1));input.oninput=()=>{fn(input.value);edited();};more.append(lab,input);};
      addInput('ID',b.id,v=>b.id=v.trim());addInput('Relevant people · one per line',b.subjects.join('\n'),v=>b.subjects=v.split('\n').map(s=>s.trim()).filter(Boolean));
      more.append(node('p','Leave people empty for general context. Matching people make this line eligible for that remembered subject.','meta'));
      addInput('Source references · one per line',b.sources.join('\n'),v=>b.sources=v.split('\n').map(s=>s.trim()).filter(Boolean));
      more.append(node('p','Edits are authored interpretations until reviewed against these sources.','meta'));card.append(more);
      const remove=node('button','Remove line');remove.className='context-remove';remove.setAttribute('aria-label','Remove background line '+(i+1));remove.onclick=()=>confirmAction('Remove this background line from the draft?',()=>{c.background.splice(i,1);edited();renderCharacter();});card.append(remove);host.append(card);
    });
    const add=node('button','Add background line');add.onclick=()=>{c.background.push(M.newBackground(c));edited();renderCharacter();$('context-'+(c.background.length-1)).focus();};host.append(add);
    const sources=details('Personal voice references'),input=node('textarea');input.setAttribute('aria-label','Personal voice references');input.value=(c.sources??[]).join('\n');input.oninput=()=>{c.sources=input.value.split('\n').map(v=>v.trim()).filter(Boolean);edited();};sources.append(input);host.append(sources);
    const preview=details('Exact generator voice preview'),voice=node('p',null,'studio-sources');voice.id='voice-preview';preview.append(voice);host.append(preview);
    const validation=node('p',null,'studio-problems');validation.id='validation';validation.setAttribute('role','status');host.append(validation);
    if(pendingDraft)for(const input of host.querySelectorAll('button,textarea,select'))input.disabled=true;
    updateStatus();
  }
  function openCreate(sourceId) {
    cloneFrom=sourceId; $('create-label').textContent=sourceId?`Duplicate ${label(sourceId)} · new ID`:'New archetype ID';
    $('new-id').value=sourceId?`${sourceId}-copy`:''; $('create-form').hidden=false; $('new-id').focus();
  }
  function load(packText, source) {
    const loaded=M.read(packText); base=M.copy(loaded); pack=loaded; pendingDraft=null;
    $('saved-draft').hidden=true; $('create-form').hidden=true;
    try {
      const saved=localStorage.getItem(KEY);
      if(saved){try{pack=M.restore(saved,base);}catch(e){pendingDraft=saved; $('saved-draft').hidden=false; $('saved-message').textContent=e.message; $('restore-draft').textContent='Use loaded pack instead';}}
    } catch {storageWorks=false;}
    selected=mode==='characters'?pack.characters[0]?.name:pack.archetypes[0].id; $('pack-source').textContent=source; $('provenance').value=pack.provenance; $('provenance').disabled=!!pendingDraft; $('new-archetype').disabled=!!pendingDraft;
    renderList(); renderEditor(); error('');
  }
  $('search').oninput=renderList;
  $('mode-archetypes').onclick=()=>switchMode('archetypes');$('mode-characters').onclick=()=>switchMode('characters');
  $('new-archetype').onclick=()=>openCreate(null);
  $('cancel-create').onclick=()=>{$('create-form').hidden=true;};
  $('create-form').onsubmit=e=>{e.preventDefault(); try{pack=M.create(pack,$('new-id').value.trim(),cloneFrom);selected=pack.archetypes.at(-1).id;$('create-form').hidden=true;edited();renderEditor();error('');}catch(e){error(e.message);}};
  $('provenance').oninput=()=>{pack.provenance=$('provenance').value; save();};
  $('open-pack').onclick=()=>$('pack-file').click();
  $('pack-file').onchange=async()=>{const file=$('pack-file').files[0]; if(!file)return;try{
    const text=await file.text();M.read(text);
    const replace=()=>{try{localStorage.removeItem(KEY);}catch{storageWorks=false;}load(text,file.name);};
    if(dirty()||pendingDraft) confirmAction('Replace the current draft? Download it first if you want to keep it.',replace);
    else load(text,file.name);
  }catch(e){error(e.message);}finally{$('pack-file').value='';}};
  $('export-pack').onclick=()=>{try{download(M.serialize(pack),'character-inspiration.json');error('');}catch(e){error(e.message);}};
  $('discard-draft').onclick=()=>confirmAction('Discard all draft changes and return to the loaded source pack?',()=>{pack=M.copy(base);try{localStorage.removeItem(KEY);}catch{}$('provenance').value=pack.provenance;switchMode(mode);});
  $('recover-draft').onclick=()=>{try{download(JSON.stringify(JSON.parse(pendingDraft).pack,null,2),'archetype-draft-recovery.json');}catch{download(pendingDraft,'archetype-draft-recovery.json');}};
  $('restore-draft').onclick=()=>confirmAction('Discard the saved draft and use this source pack? Download the saved draft first to keep it.',()=>{try{localStorage.removeItem(KEY);}catch{}pendingDraft=null;$('saved-draft').hidden=true;$('new-archetype').disabled=false;$('provenance').disabled=false;renderEditor();});
  window.addEventListener('beforeunload',e=>{if(dirty()&&!storageWorks){e.preventDefault();e.returnValue='';}});
  async function fetchJson(path){const response=await fetch(path,{cache:'no-store'});if(!response.ok)throw new Error(`Could not load ${path} (${response.status}).`);return response.text();}
  Promise.allSettled([fetchJson('../character-inspiration.json'),fetchJson('../UnderGlass.Sim/speech-catalog.json')]).then(results=>{
    if(results[1].status==='fulfilled'){try{catalog=JSON.parse(results[1].value);}catch{catalogError='The speech catalog could not be read.';}}else catalogError='Speech examples unavailable. Open the speech editor to choose a catalog.';
    if(results[0].status==='fulfilled'){try{load(results[0].value,'sim/character-inspiration.json');}catch(e){error(e.message);$('draft-status').textContent='Pack could not be opened';}}
    else{error('Open character-inspiration.json to begin. The default pack is unavailable.');$('draft-status').textContent='No pack loaded';}
  });
  window.underGlassToolPage={page:'archetypes'};
  if(/^https?:$/.test(location.protocol)){const script=document.createElement('script');script.src='/sim/viewer/tools.js?v=20261014-6';document.head.append(script);}
})();
