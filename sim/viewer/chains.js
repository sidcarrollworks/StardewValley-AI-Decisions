(() => {
  'use strict';
  const M=window.UnderGlassChains, $=id=>document.getElementById(id);
  const EXAMPLE='fixtures/lives-observer-example-v0.1.json';
  let model=null, source=null, tick=0, who=0, observer=false, thread=null, selected=null, current=null, timer=null, expanded=new Set();
  const el=(tag,text,cls)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;};
  const button=(text,fn,cls)=>{const b=el('button',text,cls);b.type='button';b.onclick=fn;return b;};
  const pct=n=>Number.isFinite(n)?`${(n*100).toFixed(1)}%`:'Not recorded';
  const clock=n=>n==='pre-run'?'Before the run':`Day ${Math.floor(n/1440)+1} · ${String(Math.floor(n%1440/60)).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`;
  function error(e){$('error').hidden=false;$('error').textContent=e.message;}
  function stop(){clearInterval(timer);timer=null;$('play').textContent='Play';}
  function details(parent,title,value){const d=el('details',undefined,'tool-disclosure');d.append(el('summary',title));const body=el('div',undefined,'detail-body');renderValue(body,value);d.append(body);parent.append(d);return d;}
  function renderValue(parent,value){
    if(value===null || value===undefined){parent.append(el('span','Not recorded'));return;}
    if(Array.isArray(value)){const ul=el('ul');for(const v of value){const li=el('li');renderValue(li,v);ul.append(li);}parent.append(ul);return;}
    if(typeof value==='object'){const dl=el('dl',undefined,'detail-list');for(const [k,v] of Object.entries(value)){dl.append(el('dt',M.human(k)));const dd=el('dd');renderValue(dd,v);dl.append(dd);}parent.append(dl);return;}
    parent.append(el('span',String(value)));
  }
  function notify(){history.replaceState(null,'',`${location.pathname}${location.search}#${new URLSearchParams({t:tick,person:model?.run.names[who]??'',tab:'inner'})}`);window.dispatchEvent(new Event('under-glass-recording'));}
  function accept(run,url,state={}){
    const parsed=M.open(run);stop();model=parsed;source=url;selected=null;thread=null;expanded.clear();$('error').hidden=true;
    $('workspace').hidden=model.missing;$('empty').hidden=!model.missing;$('illustrative').hidden=!(run.illustrative||run.lives?.illustrative);
    $('source-state').textContent=model.missing?'No Lives data':`Lives ${model.lives.version} · ${run.lives.illustrative?'illustrative fixture':'recorded run'}`;
    if(model.missing){notify();return;}
    const chosen=run.names.indexOf(state.person);who=chosen>=0?chosen:Math.max(0,run.names.indexOf('Penny'));
    $('person').replaceChildren(...run.names.map((name,i)=>{const o=el('option',name);o.value=i;return o;}));$('person').value=who;
    const moments=M.moments(model,who,true);$('time').max=Math.max(0,...moments);
    tick=Number.isFinite(state.t)?Math.max(0,Math.min(state.t,Number($('time').max))):Math.min(...model.lives.threads.map(t=>M.time(t.visibleAt)).filter(t=>t>=0),Number($('time').max));
    $('time').min=Math.min(tick,model.lives.excerpt?.from??0);
    $('recording-info').replaceChildren(el('p',`Format: proposed Lives ${model.lives.version}. Rules: ${model.lives.rules}.`),el('p','This adapter does not validate experimental formulas or run a simulation. It reads recorded links and values.'));
    $('recording-info').append(el('p','Archetype IDs identify assignments only. This example has no immutable profile snapshot; the recorded archetype definition is unavailable. Current Studio drafts are never substituted.'));
    if(run.lives.illustrative)$('recording-info').append(el('p','Hand-built Penny–Pam–Gus excerpt supplied with the contract. The two history comparisons use the fallback, not fresh Haiku/Qwen + Laya calls.'));
    render();
  }
  async function example(state={}){try{const url=new URL(EXAMPLE,location.href).href;const run=await window.UnderGlassTools.readRecording(url,location.href);accept(run,url,state);}catch(e){error(e);}}
  function select(id){selected=id;renderGraph();renderInspector();}
  function closeInspector(){const previous=selected;selected=null;renderGraph();renderInspector();const node=[...$('graph').querySelectorAll('[data-node]')].find(n=>n.dataset.node===previous);node?.focus({preventScroll:true});}
  function render(){
    if(!model||model.missing)return;
    current=M.view(model,{tick,who,observer,thread});thread=current.selected?.id??null;
    if(!current.nodes.some(n=>n.id===selected))selected=null;
    $('thread').replaceChildren(...current.threads.map(t=>{const o=el('option',t.title);o.value=t.id;return o;}));$('thread').value=thread??'';
    $('thread-summary').textContent=current.selected?.summary??'No thread is visible at this moment.';
    $('time').value=tick;$('clock').textContent=clock(tick);
    $('lens-note').textContent=observer?'World facts and private records · observer only':`Only ${model.run.names[who]}'s recorded knowledge`;
    const moments=M.moments(model,who,observer).filter(t=>t>=Number($('time').min));$('previous').disabled=!moments.some(t=>t<tick);$('next').disabled=!moments.some(t=>t>tick);$('play').disabled=$('next').disabled;
    $('comparison-section').hidden=!observer||!current.comparisons.length;
    if(!observer){$('comparisons').hidden=true;$('compare-toggle').setAttribute('aria-expanded','false');$('compare-toggle').textContent='Open isolated comparisons';}
    renderGraph();renderInspector();renderComparisons();notify();
  }
  function renderGraph(){
    const stages=$('stages');stages.replaceChildren();
    const focus=current.all.get(selected);const related=new Set([...(focus?.refs??[]),...current.nodes.filter(n=>n.refs.includes(selected)).map(n=>n.id)]);
    const order=['History','Recall','Choice','Attempt',...(observer?['Outcome']:[]),'Perceived','Learned'];
    stages.style.setProperty('--stage-count',order.length);
    for(const [index,stage] of order.entries()){
      const col=el('section',undefined,'stage'),nodes=current.nodes.filter(n=>n.stage===stage);const h=el('h2');h.append(el('span',String(index+1),'stage-number'),el('span',stage,'stage-name'),el('span',String(nodes.length),'stage-count'));col.append(h);
      // Always show the selected node and its immediate path, even among older collapsed history.
      const shown=expanded.has(stage)?nodes:nodes.filter(n=>nodes.slice(-4).includes(n)||n.id===selected||related.has(n.id));
      const cards=el('div',undefined,'stage-nodes');col.append(cards);
      if(!nodes.length)col.append(el('p','Nothing visible yet','stage-empty'));
      for(const n of shown){const b=button('',()=>select(n.id),'chain-node');b.dataset.node=n.id;b.setAttribute('aria-pressed',String(n.id===selected));b.setAttribute('aria-label',`${stage}: ${n.label}`);b.title=n.label;
        if(related.has(n.id))b.classList.add('related');if(selected&&n.id!==selected&&!related.has(n.id))b.classList.add('off-path');b.append(el('small',`${n.owner!==null?model.run.names[n.owner]+' · ':''}${clock(n.at)}`),el('span',n.label,'node-label'));cards.append(b);}
      if(nodes.length>shown.length||expanded.has(stage))col.append(button(expanded.has(stage)?'Show fewer':`Show ${nodes.length-shown.length} earlier`,()=>{expanded.has(stage)?expanded.delete(stage):expanded.add(stage);renderGraph();},'more'));
      stages.append(col);
    }
    requestAnimationFrame(drawLinks);
  }
  function drawLinks(){
    const svg=$('connections'),graph=$('graph');svg.replaceChildren();svg.setAttribute('width',graph.clientWidth);svg.setAttribute('height',graph.clientHeight);
    const ns='http://www.w3.org/2000/svg',defs=document.createElementNS(ns,'defs'),marker=document.createElementNS(ns,'marker');
    for(const [key,value] of Object.entries({id:'chain-arrow',viewBox:'0 0 10 10',refX:'9',refY:'5',markerWidth:'6',markerHeight:'6',orient:'auto-start-reverse'}))marker.setAttribute(key,value);
    const tip=document.createElementNS(ns,'path');tip.setAttribute('d','M 0 0 L 10 5 L 0 10 z');marker.append(tip);defs.append(marker);svg.append(defs);
    const buttons=new Map([...graph.querySelectorAll('[data-node]')].map(b=>[b.dataset.node,b]));const box=graph.getBoundingClientRect();
    for(const n of current?.nodes??[])for(const ref of n.refs){if(n.id!==selected&&ref!==selected)continue;const a=buttons.get(ref),b=buttons.get(n.id);if(!a||!b)continue;
      const x=a.getBoundingClientRect(),y=b.getBoundingClientRect(),stacked=Math.abs(x.left-y.left)<5;
      const x1=(stacked?x.left+x.width/2:x.right)-box.left,y1=(stacked?x.bottom:x.top+x.height/2)-box.top,x2=(stacked?y.left+y.width/2:y.left)-box.left,y2=(stacked?y.top:y.top+y.height/2)-box.top;
      const path=document.createElementNS(ns,'path');path.setAttribute('marker-end','url(#chain-arrow)');path.setAttribute('d',stacked?`M${x1},${y1} C${x1},${y1+14} ${x2},${y2-14} ${x2},${y2}`:`M${x1},${y1} C${x1+12},${y1} ${x2-12},${y2} ${x2},${y2}`);svg.append(path);
    }
  }
  function linkGroup(parent,title,nodes){if(!nodes.length)return;const d=details(parent,title,null);const body=d.lastChild;body.replaceChildren();body.classList.add('relation-links');for(const n of nodes)body.append(button(n.label,()=>{expanded.add(n.stage);select(n.id);}));}
  function renderInspector(){
    const panel=$('inspector');panel.replaceChildren();const n=current.all.get(selected);
    panel.hidden=!n;
    if(!n)return;
    const close=button('×',closeInspector,'inspector-close');close.setAttribute('aria-label','Close node details');panel.append(close);
    panel.append(el('span',`${n.stage} · ${clock(n.at)}`,'node-meta'),el('h2',n.label));
    if(n.table==='decisions'){
      if(n.data.thought){panel.append(el('p','Private imagination · not evidence','meta'),el('blockquote',n.data.thought));}
      panel.append(el('p',n.data.choice?`Chosen: ${n.data.alternatives.find(a=>a.id===n.data.choice.selected)?.label??'Unknown alternative'}`:'Choice pending at this moment.'));
      if(observer&&n.data.choice)panel.append(el('p',`Recorded method: ${n.data.choice.backend}`,'meta'));
      for(const a of n.data.alternatives){const card=el('div',undefined,'option'+(n.data.choice?.selected===a.id?' selected':''));card.append(el('p',a.label));
        if(observer&&n.data.choice)probability(card,n.data.choice.probabilities?.[a.id]);
        const d=details(card,`${a.reasons.length} reasons`,null);d.lastChild.replaceChildren();
        for(const r of a.reasons){const line=el('div',undefined,'reason');line.append(el('small',r.kind==='supports'?'Supports':r.kind==='conflicts'?'Pulls against':'Context only'),el('p',r.label??M.human(r.code)));
          if(observer&&Number.isFinite(r.value))line.append(el('small',`Recorded value ${r.value}`));
          for(const id of r.cites){const ref=current.all.get(id);if(ref)line.append(button(ref.label,()=>{expanded.add(ref.stage);select(id);},'quiet'));}d.lastChild.append(line);}
        panel.append(card);
      }
      details(panel,'Unavailable options & constraints',{excluded:n.data.excluded,constraints:n.data.constraints});
      if(observer&&n.data.choice)details(panel,'Method, draw & provenance',n.data.choice);
    }else if(n.table==='perceptions'){
      const p=n.data.perceived;
      panel.append(el('p',p?.observed??(p?.answer?`Answer: ${M.human(p.answer)}`:M.human(p?.kind))));
      if(n.data.unknown?.length)panel.append(el('p',`Still unknown: ${n.data.unknown.map(x=>x.about).join('; ')}.`));
      for(const guess of n.data.inferences??[])panel.append(el('blockquote',`A guess: ${guess.guess}. This does not establish what happened.`));
      details(panel,'Perception details',p);
      if(observer&&n.data.observerContext)details(panel,'Observer-only context',n.data.observerContext);
    }else if(n.table==='learning'){
      if(Number.isFinite(n.data.next.chance))panel.append(el('p',`Expected chance: ${pct(n.data.previous?.chance)} → ${pct(n.data.next.chance)}`));
      if(Number.isFinite(n.data.next.strength))panel.append(el('p',`Feeling strength: ${n.data.previous?.strength??'No previous entry'} → ${n.data.next.strength}`));
      if(Number.isFinite(n.data.next.moodChange))panel.append(el('p',`Mood change: ${n.data.next.moodChange}`));
      details(panel,'Recorded change',n.data);
    }else if(n.table==='recalls'){
      panel.append(el('p',`${n.data.items.length} memories came to mind.`));
      for(const item of n.data.items){const ref=current.all.get(item.record);if(ref)panel.append(button(ref.label,()=>{expanded.add(ref.stage);select(ref.id);},'option'));}
      details(panel,'What brought them to mind',n.data.items.map(x=>({memory:current.all.get(x.record)?.label,cues:x.matched})));
      if(observer&&n.data.recallDetails)details(panel,'Recall selection details',n.data.recallDetails);
    }else if(n.table==='records'){
      if(n.data.state)panel.append(el('p',Number.isFinite(n.data.state.chance)?`Expected chance: ${pct(n.data.state.chance)}`:n.data.state.status));
      if(n.data.stance)panel.append(el('p',`Reading: ${M.human(n.data.stance)}`));
      if(Number.isFinite(n.data.strength))panel.append(el('p',`Strength: ${n.data.strength} · ${n.data.sign==='+'?'warm association':'uneasy association'}`));
      const {provenance,...record}=n.data;details(panel,'Record details',record);
      if(observer&&provenance)details(panel,'Source provenance',provenance);
    }else{
      const {provenance,recallDetails,observerContext,...visible}=n.data;
      renderValue(panel,visible);
      if(observer&&provenance)details(panel,'Source provenance',provenance);
      if(observer&&recallDetails)details(panel,'Recall selection details',recallDetails);
      if(observer&&observerContext)details(panel,'Observer-only context',observerContext);
    }
    linkGroup(panel,'Supporting records',n.refs.map(id=>current.all.get(id)).filter(Boolean));
    linkGroup(panel,'Recorded consequences',current.nodes.filter(x=>x.refs.includes(n.id)));
    if(observer)details(panel,'Record identifier',n.id);
  }
  function probability(parent,p){parent.append(el('span',pct(p),'meta'));if(Number.isFinite(p)){const bar=el('div',undefined,'probability'),fill=el('span');fill.style.width=`${Math.max(0,Math.min(1,p))*100}%`;bar.append(fill);parent.append(bar);}}
  function renderComparisons(){
    const root=$('comparisons');root.replaceChildren();if(!observer||root.hidden)return;
    root.append(el('p','Isolated choice · fallback held fixed · no later lives simulated. The illustrative run itself used Laya for this decision.','notice'));
    for(const row of current.comparisons){let c;try{c=M.comparison(model,row);}catch(e){root.append(el('p',e.message));continue;}
      const card=el('article',undefined,'comparison');card.append(el('h3',c.question));const result=el('div',undefined,'result');
      for(const [k,label] of [['reasons','Reasons'],['probabilities','Probabilities'],['sampledAction','Selected action']])result.append(el('span',`${label}: ${c.differs[k]?'changed':'unchanged'}`,c.differs[k]?'changed':''));card.append(result);
      const arms=el('div',undefined,'arms');for(const a of c.arms){const arm=el('section',undefined,'arm');arm.append(el('h4',a.label),el('p',`${a.method} · same draw ${a.draw}`,'meta'));
        for(const o of a.options){const opt=el('div',undefined,'option');opt.append(el('p',o.label));probability(opt,o.probability);details(opt,'Recorded reasons',o.reasons);arm.append(opt);}arm.append(el('p',`Selected: ${a.selected}`));arms.append(arm);}card.append(arms);
      details(card,'What changed, what stayed fixed',{intervention:c.intervention,heldConstant:c.heldConstant,validity:c.validity,derivedDifferences:c.unavoidableDifferences,actualRun:c.runChoice,scope:c.wholeRun});root.append(card);
    }
  }
  function step(direction){const points=[Number($('time').min),...M.moments(model,who,observer)].filter(t=>t>=Number($('time').min));const t=direction>0?points.find(t=>t>tick):points.filter(t=>t<tick).at(-1);if(t===undefined){stop();return;}tick=t;render();}
  $('previous').onclick=()=>{stop();step(-1);};$('next').onclick=()=>{stop();step(1);};$('play').onclick=()=>{if(timer){stop();return;}timer=setInterval(()=>step(1),900);$('play').textContent='Pause';};
  $('time').oninput=()=>{stop();tick=Number($('time').value);render();};
  $('person').onchange=()=>{stop();who=Number($('person').value);selected=null;render();};
  $('lens').onchange=()=>{stop();observer=$('lens').value==='observer';selected=null;render();};
  $('thread').onchange=()=>{thread=$('thread').value;selected=null;render();};
  $('compare-toggle').onclick=()=>{$('comparisons').hidden=!$('comparisons').hidden;$('compare-toggle').setAttribute('aria-expanded',String(!$('comparisons').hidden));$('compare-toggle').textContent=$('comparisons').hidden?'Open isolated comparisons':'Close comparisons';renderComparisons();};
  $('example').onclick=()=>example();$('open-recording').onclick=()=>$('recording-file').click();
  $('recording-file').onchange=async()=>{const f=$('recording-file').files[0];if(!f)return;try{accept(window.UnderGlassTools.parseRecording(await f.text()),null);}catch(e){error(e);}finally{$('recording-file').value='';}};
  window.addEventListener('resize',()=>requestAnimationFrame(drawLinks));
  document.addEventListener('keydown',e=>{if(e.key==='Escape'&&selected){e.preventDefault();closeInspector();}});
  new ResizeObserver(()=>requestAnimationFrame(drawLinks)).observe($('graph'));
  window.underGlassToolPage={page:'chains',load:accept,source:()=>model?source:undefined,hasRecording:()=>!!model,state:()=>({t:tick,person:model?.run.names[who]??'',tab:'inner'})};
  document.addEventListener('DOMContentLoaded',()=>{if(!new URLSearchParams(location.search).has('run'))example(window.UnderGlassTools.readState(location.hash));});
})();
