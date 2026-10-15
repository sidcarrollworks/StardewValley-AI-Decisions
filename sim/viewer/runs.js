(() => {
  'use strict';
  const $=id=>document.getElementById(id), controls=new Map();
  let schema,token,initialized=false,selectedLog=null,submitting=false,previousJobs='';
  let serviceRows=[],startingModels=false,activeRun=false,previousServices='';
  const el=(tag,text,cls)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;};
  const human=s=>s.replace(/([a-z])([A-Z])/g,'$1 $2');
  const error=e=>{$('error').hidden=!e;$('error').textContent=e?.message??e??'';};
  const button=(text,fn)=>{const b=el('button',text);b.type='button';b.onclick=fn;return b;};
  async function post(path,data){const r=await fetch('/api/run-studio'+path,{method:'POST',headers:{'Content-Type':'application/json','X-Run-Studio-Token':token},body:JSON.stringify(data)});const v=await r.json();if(!r.ok)throw new Error(v.error);return v;}
  function field(o){
    const root=el('div',undefined,'field'),id='option-'+o.flag;let input;
    if(o.type==='catalog'||o.type==='steps'){
      root.classList.add('wide');root.append(el('span',o.label));input=el('div',undefined,'check-grid');
      o.choices.forEach((v,i)=>{const l=el('label'),c=el('input');c.type='checkbox';c.value=v;l.append(c,el('span',o.type==='steps'?schema.stepLabels[i]:human(v)));input.append(l);});
    }else{
      const label=el('label',o.label);label.htmlFor=id;root.append(label);
      if(o.type==='choice'){input=el('select');o.choices.forEach(v=>{const item=el('option',v||'Town default / off');item.value=v;input.append(item);});}
      else if(o.repeat){input=el('textarea');input.placeholder=o.flag==='trait'?'Penny=Kindness:0.8':'One Name=value per line';root.classList.add('wide');}
      else{input=el('input');input.type=o.type==='boolean'?'checkbox':o.type==='number'?'number':'text';if(input.type==='number')input.step='any';if(o.type==='integer')input.inputMode='numeric';}
      if(o.type==='town'){const list=el('datalist');list.id='towns';for(const name of [...o.choices,'pelican:46@7','pelican:60@7'])list.append(Object.assign(el('option'),{value:name}));root.append(list);input.setAttribute('list',list.id);root.append(el('small','Generated towns: 31, 46 or 60 people (pelican:60@7). Or upload a town JSON.'));}
      if(o.type==='boolean')root.classList.add('check');
    }
    input.id=id;root.append(input);controls.set(o.flag,{input,o});
    if(o.repeat){
      const builder=el('div',undefined,'override-builder'),name=el('select'),value=el('input');value.placeholder='Value';value.setAttribute('aria-label',o.label+' value');
      if(o.type==='override'){
        const search=el('input');search.placeholder='Find an option…';search.setAttribute('aria-label','Find '+o.label.toLowerCase());
        const fill=()=>{name.replaceChildren(...schema.knobs[o.flag].filter(k=>k.name.toLowerCase().includes(search.value.toLowerCase())).map(k=>Object.assign(el('option',`${human(k.name)} · ${k.value}`),{value:k.name})));};search.oninput=fill;fill();builder.append(search);
        name.onchange=()=>{const k=schema.knobs[o.flag].find(k=>k.name===name.value);value.value=k?.value??'';};name.onchange();
        builder.append(name,value,button('Add override',()=>{if(!name.value)return;input.value+=(input.value.trim()?'\n':'')+`${name.value}=${value.value}`;updateCommand();}));
      }else{
        const person=el('input');person.placeholder='Character name';person.setAttribute('aria-label','Trait character');person.setAttribute('list','characters');const list=el('datalist');list.id='characters';schema.characters.forEach(v=>list.append(Object.assign(el('option'),{value:v})));root.append(list);
        schema.traits.forEach(v=>name.append(Object.assign(el('option',human(v)),{value:v})));builder.append(person,name,value,button('Add trait',()=>{input.value+=(input.value.trim()?'\n':'')+`${person.value}=${name.value}:${value.value}`;updateCommand();}));
      }
      name.setAttribute('aria-label',o.label+' name');root.insertBefore(builder,input);root.append(el('small','Applied in the order listed. Leave empty to use existing values.'));
    }
    if(o.type==='path'||o.type==='town'){
      const up=el('input');up.type='file';up.accept='.json,application/json';up.setAttribute('aria-label','Upload '+o.label.toLowerCase());
      up.onchange=async()=>{try{const f=up.files[0];if(!f)return;const result=await post('/uploads',{name:f.name,text:await f.text()});input.value=(o.type==='town'?'file:':'')+result.path;updateCommand();error(null);}catch(e){error(e);}finally{up.value='';}};
      const wrap=el('div',undefined,'upload');wrap.append(up);root.append(wrap);
    }
    if(o.type==='path'){input.placeholder=o.flag==='character-inspiration'?'sim/character-inspiration.json':'Path in this checkout or upload JSON';root.append(el('small','Uploads are saved as new copies.'));
    }
    if(o.type==='url')input.placeholder=o.flag==='laya-url'?'http://127.0.0.1:8000':'http://127.0.0.1:8080';
    return root;
  }
  function initialize(){
    const groups=['Run','Mind','Speech','Models','Actions','Advanced','Overrides'];
    for(const group of groups){const fields=el('div',undefined,'run-fields');for(const o of schema.options.filter(o=>o.group===group))fields.append(field(o));
      if(group==='Run')$('options').append(fields);else{const d=el('details',undefined,'tool-disclosure');d.dataset.group=group;d.open=group==='Mind'||group==='Speech';d.append(el('summary',group),fields);$('options').append(d);}}
    preset('haiku');initialized=true;
  }
  function set(key,value){const c=controls.get(key);if(!c)return;const {input,o}=c;if(o.type==='boolean')input.checked=!!value;else if(o.type==='catalog'||o.type==='steps'){const values=o.type==='catalog'?String(value??'').toLowerCase().split(','):String(value??'').split('');input.querySelectorAll('input').forEach(n=>n.checked=values.includes(n.value.toLowerCase()));}else input.value=Array.isArray(value)?value.join('\n'):value??'';}
  function defaults(){for(const [key,{o}] of controls)set(key,o.type==='boolean'?false:o.default);$('output-json').checked=true;$('output-html').checked=false;updateCommand();}
  function values(){const result={};for(const [key,{input,o}] of controls){let v=o.type==='boolean'?input.checked:o.type==='catalog'||o.type==='steps'?[...input.querySelectorAll('input:checked')].map(n=>n.value).join(o.type==='catalog'?',':''):o.repeat?input.value.split('\n').map(s=>s.trim()).filter(Boolean):input.value.trim();if(v!==false&&v!==''&&(!Array.isArray(v)||v.length))result[key]=v;}return result;}
  function payload(){return {name:$('run-name').value,options:values(),formats:[$('output-json').checked?'json':null,$('output-html').checked?'html':null].filter(Boolean)};}
  function updateCommand(){const a=[];for(const [key,v] of Object.entries(values())){for(const x of Array.isArray(v)?v:[v]){a.push('--'+key);if(x!==true)a.push(String(x));}}$('command').textContent=a.map(v=>/\s/.test(v)?JSON.stringify(v):v).join(' ')+'\nOutput paths are assigned when the run starts.';renderServices();}
  function preset(name){if(!schema)return;defaults();const mode=['haiku','qwen'].includes(name)?'hybrid':name;set('reflection',mode);set('speech',true);set('newcomer-voice',true);set('character-inspiration','sim/character-inspiration.json');set('catalog',schema.options.find(o=>o.flag==='catalog').choices.filter(v=>v!=='Watch').join(','));if(mode!=='authored')set('laya-url','http://127.0.0.1:8000');if(mode==='hybrid'){set('llm-url',name==='haiku'?'http://127.0.0.1:8083':'http://127.0.0.1:8080');set('llm-model',name==='haiku'?'claude-haiku-5-5':'Qwen/Qwen3-0.6B');}updateCommand();}
  document.querySelectorAll('[data-preset]').forEach(b=>b.onclick=()=>preset(b.dataset.preset));
  function requiredServices(){const v=values();if(!['hybrid','laya'].includes(v.reflection))return [];
    const local=(url,port)=>{try{const u=new URL(url);return ['localhost','127.0.0.1'].includes(u.hostname)&&u.port===String(port)&&u.pathname==='/';}catch{return false;}};
    if(!local(v['laya-url']||'http://127.0.0.1:8000',8000))return null;
    if(v.reflection==='laya')return ['laya'];
    if(local(v['llm-url'],8083)&&v['llm-model']==='claude-haiku-5-5')return ['laya','haiku'];
    if(local(v['llm-url'],8080)&&v['llm-model']==='Qwen/Qwen3-0.6B')return ['laya','qwen'];return null;}
  function renderServices(){
    const key=JSON.stringify([serviceRows,activeRun]);if(key!==previousServices){previousServices=key;
    $('model-services').replaceChildren(...serviceRows.map(s=>{const row=el('div',undefined,'model-server'),state=el('span',s.label+' · '+s.state,'job-state '+(s.state==='ready'?'succeeded':s.state==='starting'?'running':s.state==='failed'||s.state==='unavailable'?'failed':''));row.append(state);
      if(s.owned){const stop=button('Stop '+s.label,async()=>{try{await post('/services',{action:'stop',service:s.id});await refresh();}catch(e){error(e);}});stop.disabled=activeRun;row.append(stop);}else if(s.state==='ready')row.append(el('span','already running','meta'));
      if(s.log){const log=el('a','Log');log.href=s.log;log.target='_blank';row.append(log);}if(s.message)state.title=s.message;return row;}));}
    const names=requiredServices(),ready=names?.length&&names.every(name=>serviceRows.some(s=>s.id===name&&s.state==='ready'));
    $('start-models').disabled=startingModels||!serviceRows.length||!names?.length||ready;
    $('start-models').textContent=startingModels?'Starting servers…':ready?'Servers ready':'Start required servers';
    $('model-help').textContent=names===null?'Custom services: start them with their own launchers.':!names.length?'This mode needs no live model servers.':ready?'Health checks passed.':'Start servers before generating. Offline models can cause authored fallbacks.';
  }
  $('start-models').onclick=async()=>{try{startingModels=true;renderServices();error(null);await post('/services',{action:'start',services:requiredServices()});await refresh();}catch(e){error(e);}finally{startingModels=false;renderServices();}};
  $('defaults').onclick=()=>schema&&defaults();$('run-form').oninput=updateCommand;
  $('run-form').onsubmit=async e=>{e.preventDefault();if(submitting)return;try{submitting=true;$('generate').disabled=true;error(null);await post('/jobs',payload());await refresh();}catch(e){error(e);}finally{submitting=false;}};
  $('save-settings').onclick=()=>{const a=el('a');a.href=URL.createObjectURL(new Blob([JSON.stringify({format:'under-glass-run-settings',...payload()},null,2)],{type:'application/json'}));a.download='run-settings.json';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000);};
  $('load-settings').onchange=async e=>{try{const data=JSON.parse(await e.target.files[0].text());if(data.format!=='under-glass-run-settings'||!data.options)throw new Error('Choose run settings exported from this page.');defaults();for(const [key,value] of Object.entries(data.options)){if(!controls.has(key))throw new Error('Unknown option: '+key);set(key,value);}$('run-name').value=data.name||'Imported run';$('output-json').checked=data.formats?.includes('json');$('output-html').checked=data.formats?.includes('html');updateCommand();error(null);}catch(e){error(e);}finally{e.target.value='';}};
  function renderJobs(jobs){
    const key=JSON.stringify(jobs);if(key===previousJobs)return;previousJobs=key;$('jobs').replaceChildren();
    if(!jobs.length)$('jobs').append(el('p','No runs yet.','meta'));
    for(const j of jobs){const card=el('article',undefined,'run-job');card.append(el('h3',j.name));const status=el('div');status.append(el('span',j.status,'job-state '+j.status));const duration=el('span',undefined,'meta');duration.dataset.started=j.started;if(j.finished)duration.dataset.finished=j.finished;status.append(duration);card.append(status);
      if(j.status==='succeeded'&&j.finished>j.started)card.append(el('p',`${j.options.days||28} simulated ${Number(j.options.days||28)===1?'day':'days'} · ${(Number(j.options.days||28)/(j.finished-j.started)).toFixed(1)} days/sec`,'meta'));
      const actions=el('div',undefined,'tool-actions');
      if(j.status==='succeeded'){const url=j.outputs.json||j.outputs.html;const link=el('a','Open Run Viewer →','key-link');link.href='index.html?run='+encodeURIComponent(url);actions.append(link);for(const [fmt,path] of Object.entries(j.outputs)){const d=el('a','Download '+fmt.toUpperCase());d.href=path;d.download='';actions.append(d);}}
      if(['running','cancelling'].includes(j.status)){const cancel=button('Cancel run',async()=>{try{await post('/jobs/'+j.id+'/cancel',{});await refresh();}catch(e){error(e);}});cancel.disabled=j.status==='cancelling';actions.append(cancel);}
      actions.append(button('Use settings',()=>{defaults();Object.entries(j.options).forEach(([k,v])=>set(k,v));$('run-name').value=j.name+' copy';$('output-json').checked=j.formats.includes('json');$('output-html').checked=j.formats.includes('html');updateCommand();}),button('Show output',async()=>{selectedLog=j.id;$('job-log').hidden=false;$('job-log').open=true;await log();}));card.append(actions);if(j.error)card.append(el('p',j.error));$('jobs').append(card);
    }
  }
  async function log(){if(!selectedLog)return;const r=await fetch('/api/run-studio/jobs/'+selectedLog+'/log');if(r.ok)$('log-text').textContent=(await r.json()).text||'No output yet. The simulation is running; elapsed time is shown above.';}
  async function refresh(){try{const r=await fetch('/api/run-studio',{cache:'no-store'});if(!r.ok)throw new Error('Run service unavailable. Start the updated preview launcher (see Runner setup).');const data=await r.json();token=data.token;if(data.ready&&!initialized){schema=data.schema;initialize();}serviceRows=data.services||[];activeRun=data.jobs.some(j=>['running','cancelling'].includes(j.status));renderServices();$('service').textContent=data.error||(!data.ready?'Preparing the replay engine…':'Local runner ready · one run at a time');$('generate').disabled=submitting||!data.ready||activeRun;renderJobs(data.jobs);await log();}catch(e){$('service').textContent=e.message;$('generate').disabled=true;}document.querySelectorAll('[data-started]').forEach(n=>{const elapsed=Math.max(0,(Number(n.dataset.finished)||Date.now()/1000)-Number(n.dataset.started)),seconds=Math.floor(elapsed);n.textContent=elapsed<10?`${elapsed.toFixed(1)}s`:`${Math.floor(seconds/60)}m ${seconds%60}s`;});}
  window.underGlassToolPage={page:'runs'};
  async function poll(){await refresh();setTimeout(poll,1500);}poll();
})();
