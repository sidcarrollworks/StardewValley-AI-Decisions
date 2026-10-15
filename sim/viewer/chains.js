(() => {
  'use strict';
  const M=window.UnderGlassChains, $=id=>document.getElementById(id);
  const EXAMPLE='fixtures/lives-observer-example-v0.1.json';
  let model=null, source=null, tick=0, who=0, observer=false, thread='all', selected=null, current=null, moment=null, timer=null;
  let tape=[], totalMin=0,totalMax=0,windowStart=null,windowEnd=null,focusedEvent=null,preview=null,inspection=null,graphView=null,branch=null;
  let pages={},memoriesOpen=false,momentPage=0,zoomSpan=1440;
  let playbackAt=0,lastFrame=0,gates=[];
  const symbols={History:'◈',Recall:'↗',Choice:'◆',Attempt:'➜',Outcome:'●',Perceived:'◉',Learned:'✦'};
  const el=(tag,text,cls)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;};
  const button=(text,fn,cls)=>{const b=el('button',text,cls);b.type='button';b.onclick=fn;return b;};
  const pct=n=>Number.isFinite(n)?`${(n*100).toFixed(1)}%`:'Not recorded';
  const amount=n=>String(Number(n.toPrecision(4)));
  const signed=n=>`${n>0?'+':n<0?'−':''}${amount(Math.abs(n))}`;
  function learningMetric(m){
    const root=el('span',undefined,`learning-metric change-${m.status}`),head=el('span',undefined,'metric-heading');
    const symbol={up:'↑',down:'↓',same:'=',new:'✦',unknown:'?'}[m.status];
    const difference=m.key==='chance' && m.delta!==null ? `${m.delta>0?'+':'−'}${Math.abs(m.delta*100)<0.1?'<0.1':amount(Math.abs(m.delta*100))} pp` : m.delta===null?'':signed(m.delta);
    const status=m.status==='same'?'Unchanged':m.status==='new'?'New':m.status==='unknown'?'Prior unknown':difference;
    head.append(el('span',m.label),el('span',`${symbol} ${status}`,'change-badge'));root.append(head);
    const format=v=>m.key==='chance'?pct(v).replace('.0%','%'):amount(v);
    if(m.key!=='mood'){
      const values=el('span',undefined,'change-values');
      if(m.before!==null)values.append(el('span',format(m.before),'previous-value'),el('span','→','change-arrow'));
      root.title=m.status==='new'?'New recorded feeling; no previous entry.':m.before===null?'Previous value was not recorded.':`${m.label}: ${format(m.before)} before, ${format(m.after)} after`;
      values.append(el('strong',format(m.after)));root.append(values);
      if(m.key==='chance'){
        const track=el('span',undefined,'change-track');track.setAttribute('aria-hidden','true');
        const fill=el('span',undefined,'current-fill');fill.style.width=`${Math.max(0,Math.min(1,m.after))*100}%`;track.append(fill);
        if(m.before!==null){const mark=el('span',undefined,'previous-mark');mark.style.left=`${Math.max(0,Math.min(1,m.before))*100}%`;track.append(mark);}
        track.title='Fill: after · marker: before';root.append(track);
      }
    }
    return root;
  }
  const clock=n=>n==='pre-run'?'Before the run':`Day ${Math.floor(n/1440)+1} · ${String(Math.floor(n%1440/60)).padStart(2,'0')}:${String(Math.floor(n%60)).padStart(2,'0')}`;
  function error(e){$('error').hidden=false;$('error').textContent=e.message;}
  function stop(){cancelAnimationFrame(timer);timer=null;$('play').textContent='Play';}
  function details(parent,title,value){const d=el('details',undefined,'tool-disclosure');d.append(el('summary',title));const body=el('div',undefined,'detail-body');renderValue(body,value);d.append(body);parent.append(d);return d;}
  function renderValue(parent,value){
    if(value===null || value===undefined){parent.append(el('span','Not recorded'));return;}
    if(Array.isArray(value)){const ul=el('ul');for(const v of value){const li=el('li');renderValue(li,v);ul.append(li);}parent.append(ul);return;}
    if(typeof value==='object'){const dl=el('dl',undefined,'detail-list');for(const [k,v] of Object.entries(value)){dl.append(el('dt',M.human(k)));const dd=el('dd');renderValue(dd,v);dl.append(dd);}parent.append(dl);return;}
    parent.append(el('span',String(value)));
  }
  function notify(){history.replaceState(null,'',`${location.pathname}${location.search}#${new URLSearchParams({t:tick,person:model?.run.names[who]??'',tab:'inner'})}`);window.dispatchEvent(new Event('under-glass-recording'));}
  function accept(run,url,state={}){
    const parsed=M.open(run);stop();model=parsed;source=url;selected=null;focusedEvent=null;preview=null;inspection=null;thread='all';pages={};memoriesOpen=false;windowStart=null;$('error').hidden=true;
    $('workspace').hidden=model.missing;$('empty').hidden=!model.missing;$('illustrative').hidden=!(run.illustrative||run.lives?.illustrative);
    $('source-state').textContent=model.missing?'No Lives data':model.lives.runtime?'Lives pilot · actual recording':`Lives ${model.lives.version} · ${model.lives.illustrative?'illustrative fixture':'recorded run'}`;
    if(model.missing){notify();return;}
    const chosen=run.names.indexOf(state.person);who=chosen>=0?chosen:Math.max(0,run.names.indexOf('Penny'));
    const holders=model.lives.holders?.length?model.lives.holders:run.names;
    if(!holders.includes(run.names[who]))who=run.names.indexOf(holders[0]);
    $('person').replaceChildren(...run.names.flatMap((name,i)=>{if(!holders.includes(name))return [];const o=el('option',name);o.value=i;return [o];}));$('person').value=who;
    const moments=M.moments(model,who,true);totalMax=Math.max(0,run.live?.at??0,...moments);
    tick=Number.isFinite(state.t)?Math.max(0,Math.min(state.t,totalMax)):(M.eventIndex(model,who,false)[0]?.at??0);
    totalMin=Math.min(tick,model.lives.excerpt?.from??0);reindex();
    $('recording-info').replaceChildren(el('p',model.lives.runtime?model.lives.scope:`Format: proposed Lives ${model.lives.version}. Rules: ${model.lives.rules}.`),el('p','This adapter does not validate experimental formulas or run a simulation. It reads recorded links and values.'));
    $('recording-info').append(el('p','Archetype IDs identify assignments only. This view has no immutable profile snapshot; the recorded archetype definition is unavailable. Current Studio drafts are never substituted.'));
    if(model.lives.illustrative)$('recording-info').append(el('p','Hand-built Penny–Pam–Gus excerpt supplied with the contract. The two history comparisons use the fallback, not fresh Haiku/Qwen + Laya calls.'));
    render();
  }
  async function example(state={}){try{const url=new URL(EXAMPLE,location.href).href;const run=await window.UnderGlassTools.readRecording(url,location.href);accept(run,url,state);}catch(e){error(e);}}
  function reindex(){
    gates=M.moments(model,who,observer);
    tape=M.eventIndex(model,who,observer).filter(e=>e.at>=totalMin&&e.at<=totalMax);
    if(thread!=='all'){
      const ids=new Set(M.view(model,{tick:totalMax,who,observer,thread}).nodes.map(n=>n.id));
      tape=tape.map(e=>({...e,ids:e.ids.filter(id=>ids.has(id))})).filter(e=>e.ids.length);
    }
  }
  function setWindow(at){
    const requested=Number($('zoom').value);if(Number.isFinite(requested)&&requested>0)zoomSpan=requested;
    const size=zoomSpan;$('zoom').value=String(size);
    windowStart=Math.max(totalMin,Math.floor(at/size)*size);windowEnd=Math.min(totalMax,Math.floor(at/size)*size+size);
    if(windowEnd<=windowStart)windowEnd=windowStart+1;
  }
  function select(id){
    stop();focusedEvent=id;preview=null;selected=null;inspection=null;pages={};memoriesOpen=false;
    renderTimeline();renderMoment();renderGraph();renderInspector();
  }
  function closeInspector(){selected=null;inspection=null;renderInspector();}
  function focusTime(){focusedEvent=null;preview=null;selected=null;inspection=null;pages={};memoriesOpen=false;render();}
  function seek(at,id){stop();tick=at;focusedEvent=id??null;preview=null;$('timeline-preview').hidden=true;selected=null;inspection=null;pages={};memoriesOpen=false;momentPage=0;render();}
  function render(){
    if(!model||model.missing)return;
    current=M.view(model,{tick,who,observer,thread});
    if(thread!=='all'&&current.selected?.id!==thread){thread=current.selected?.id??'all';reindex();}
    const latest=tape.filter(e=>e.at<=tick).at(-1);moment={at:latest?.at??-1,exact:latest?.at===tick,ids:latest?.ids??[]};
    if(!current.all.has(focusedEvent))focusedEvent=null;
    preview=null;
    $('thread').replaceChildren(Object.assign(el('option','All events'),{value:'all'}),...current.threads.map(t=>Object.assign(el('option',t.title),{value:t.id})));$('thread').value=thread;
    $('clock').textContent=clock(tick);
    $('lens-note').textContent=observer?'Observer view · includes world facts':`Only ${model.run.names[who]}'s recorded knowledge`;
    $('previous').disabled=!tape.some(e=>e.at<tick);$('next').disabled=!tape.some(e=>e.at>tick);$('play').disabled=tick>=totalMax;
    if(windowStart===null||tick<windowStart||tick>windowEnd)setWindow(tick);
    $('comparison-section').hidden=!observer||!current.comparisons.length;
    if(!observer){$('comparisons').hidden=true;$('compare-toggle').setAttribute('aria-expanded','false');$('compare-toggle').textContent='Open isolated comparisons';}
    renderTimeline();renderMoment();renderGraph();renderInspector();renderComparisons();renderRoutine();notify();
  }
  function renderRoutine(){
    const checks=M.routineChecks(model,tick,who,observer);$('routine-checks').hidden=!checks.length;
    $('routine-count').textContent=`Routine observations · ${checks.length} unchanged confirmations`;
    if($('routine-checks').open){
      const root=$('routine-list');root.replaceChildren();
      const groups=new Map();for(const c of checks){const key=`Day ${Math.floor(c.at/1440)+1} · ${model.run.names[c.holder]}`;if(!groups.has(key))groups.set(key,[]);groups.get(key).push(c);}
      for(const [day,items]of [...groups].reverse()){
        const d=el('details',undefined,'tool-disclosure');d.append(el('summary',`${day} · ${items.length} checks`));
        for(const c of items)d.append(button(`${clock(c.at)} · ${c.label}`,()=>seek(c.at,c.id)));
        root.append(d);
      }
    }
    // Daily counts are privileged diagnostics, never a character's knowledge. Incomplete
    // aggregates are withheld until their last recorded check to avoid leaking future totals.
    const gates=(model.run.livesEvenings?.gates||[]).filter(g=>g.holder===model.run.names[who]&&g.lastAt<=tick);
    $('offer-gates').hidden=!observer||!gates.length;
    if(observer&&$('offer-gates').open){
      const root=$('gate-list');root.replaceChildren(el('p','First blocking gate per check, before 17:00. These are observer diagnostics, not refusals or beliefs.'));
      const labels={'self-unavailable':'Unavailable to propose','partner-unavailable':'Partner unavailable','self-plan-active':'Already has a plan','partner-plan-active':'Partner already has a plan','already-considered-today':'Already considered today','no-local-encounter':'No nearby encounter','company-active':'Company activity underway','no-recalled-opening':'No relevant warmth or wish recalled',considered:'Considered an offer',age:'Age restriction'};
      for(const g of gates.slice().reverse())root.append(el('p',`Day ${g.day+1} · ${g.partner} · ${labels[g.reason]||g.reason} · ${g.checks} checks`));
    }
  }
  $('routine-checks').ontoggle=()=>{if(model)renderRoutine();};
  $('offer-gates').ontoggle=()=>{if(model)renderRoutine();};
  function atEvent(e){return M.view(model,{tick:e.at,who,observer,thread});}
  function preferred(ids,v){return ids.map(id=>v.all.get(id)).filter(Boolean).sort((a,b)=>['Choice','Attempt','Perceived','Learned','Recall','Outcome','History'].indexOf(a.stage)-['Choice','Attempt','Perceived','Learned','Recall','Outcome','History'].indexOf(b.stage))[0]?.id;}
  function previewEvent(e){
    if(e.at>tick){$('timeline-preview').hidden=false;$('timeline-preview').textContent=`${clock(e.at)} · Recorded moment · click to move here`;return;}
    const v=atEvent(e),id=preferred(e.ids,v);if(focusedEvent===id&&e.at===tick){endPreview();return;}preview={id,view:v};
    const n=v.all.get(id);$('timeline-preview').hidden=false;$('timeline-preview').textContent=`${clock(e.at)} · ${n?.label??'Recorded moment'}${e.ids.length>1?` · ${e.ids.length} events`:''}`;renderGraph();
  }
  function endPreview(){preview=null;$('timeline-preview').hidden=true;renderGraph();}
  function renderTimeline(){
    const root=$('timeline-markers');root.replaceChildren();
    $('time').min=windowStart;$('time').max=windowEnd;$('time').value=Math.min(windowEnd,Math.max(windowStart,tick));
    const ruler=M.timelineRuler(windowStart,windowEnd,$('event-timeline').clientWidth);
    $('window-label').textContent=ruler.label;
    $('window-back').hidden=windowStart<=totalMin;$('window-next').hidden=windowEnd>=totalMax;
    const position=t=>100*(t-windowStart)/(windowEnd-windowStart);
    $('time-cursor').style.left=`${position(tick)}%`;$('time-cursor').hidden=tick<windowStart||tick>windowEnd;
    const buckets=Math.max(8,Math.min(72,Math.floor(($('event-timeline').clientWidth||600)/28)));
    const selectedAt=tape.filter(e=>e.at<=tick&&e.ids.includes(focusedEvent)).at(-1)?.at;
    for(const bin of M.timelineBins(tape,windowStart,windowEnd,buckets)){
      const first=bin.events[0],future=first.at>tick,summary=future?[]:M.eventSummary(model,first,who,observer),n=summary.find(n=>n.stage==='Choice')??summary[0];
      const b=button(bin.count>1?String(bin.count):symbols[n?.stage]??'·',()=>{
        if(bin.events.length>1){windowStart=Math.max(totalMin,bin.from-1);windowEnd=Math.min(totalMax,bin.to+1);$('zoom').value='custom';seek(first.at,preferred(first.ids,atEvent(first)));}
        else seek(first.at,preferred(first.ids,atEvent(first)));
      },'event-marker'+(future?' future':''));
      b.style.left=`${position((bin.from+bin.to)/2)}%`;
      const active=bin.events.some(e=>e.at===moment.at),chosen=bin.events.some(e=>e.at===selectedAt);
      b.classList.toggle('current',active);b.classList.toggle('selected-event',chosen);b.setAttribute('aria-pressed',String(chosen));
      const label=bin.events.length>1?`${clock(bin.from)} to ${clock(bin.to)} · ${bin.count} events · zoom in`:`${clock(first.at)} · ${future?'Recorded moment':n?.label??'Recorded moment'}${bin.count>1?` · ${bin.count} events`:''}`;
      b.setAttribute('aria-label',label);b.title=label;
      b.onmouseenter=b.onfocus=()=>{if(bin.events.length===1)previewEvent(first);else{$('timeline-preview').hidden=false;$('timeline-preview').textContent=label;}};
      b.onmouseleave=b.onblur=endPreview;root.append(b);
    }
    $('time-ruler').replaceChildren(...ruler.ticks.map(t=>{const n=el('span',t.label);n.style.left=`${t.position}%`;n.style.transform=t.position<5?'none':t.position>95?'translateX(-100%)':'translateX(-50%)';return n;}));
  }
  function renderMoment(){
    $('moment-label').textContent=moment.at<0?'No event yet':`${moment.exact?'Now':'Latest event'} · ${clock(moment.at)}`;
    $('moment-strip').classList.toggle('at-now',moment.exact);$('moment-links').replaceChildren();
    const nodes=moment.ids.map(id=>current.all.get(id)).filter(Boolean);
    for(const stage of [...new Set(nodes.map(n=>n.stage))]){
      const subset=nodes.filter(n=>n.stage===stage),b=button(`${symbols[stage]} ${stage} · ${subset.length}`,()=>{momentPage=Math.floor(nodes.indexOf(subset[0])/4);select(subset[0].id);},'moment-jump');
      b.setAttribute('aria-label',`Select latest ${stage.toLowerCase()} event`);$('moment-links').append(b);
    }
    const root=$('moment-events');root.replaceChildren();
    const head=el('div',undefined,'moment-heading');head.append(el('h2',moment.at<0?'Move forward to an event':clock(moment.at)),el('span',`${nodes.length} ${nodes.length===1?'event':'events'}${moment.exact?'':' · most recent'}`,'meta'));root.append(head);
    if(momentPage*4>=nodes.length)momentPage=0;
    const row=el('div',undefined,'event-cards');
    for(const n of nodes.slice(momentPage*4,momentPage*4+4)){
      const b=nodeCard(n,()=>select(n.id),false);b.querySelector('small').textContent=`${symbols[n.stage]} ${n.stage} · ${clock(moment.at)}${M.time(n.at)<moment.at?' · updated':''}`;b.classList.toggle('selected-event',n.id===focusedEvent);b.dataset.event=n.id;
      b.onmouseenter=b.onfocus=()=>{if(focusedEvent===n.id)return;preview={id:n.id,view:current};renderGraph();};b.onmouseleave=b.onblur=endPreview;row.append(b);
    }
    root.append(row);paginate(root,nodes.length,momentPage,4,p=>{momentPage=p;renderMoment();});
  }
  function paginate(parent,count,page,size,change){
    if(count<=size)return;const nav=el('div',undefined,'branch-pages');
    const back=button('‹',()=>change(page-1)),next=button('›',()=>change(page+1));back.disabled=page===0;next.disabled=(page+1)*size>=count;
    back.setAttribute('aria-label','Previous items');next.setAttribute('aria-label','Next items');nav.append(back,el('span',`${page*size+1}–${Math.min(count,(page+1)*size)} of ${count}`),next);parent.append(nav);
  }
  function nodeCard(n,action,full=true){
    const b=button('',action,'chain-node');b.dataset.node=n.id;b.setAttribute('aria-label',`${n.stage}: ${n.label}`);b.title=n.label;
    b.append(el('small',`${symbols[n.stage]} ${n.stage} · ${n.owner!==null&&(observer||n.owner!==who)?model.run.names[n.owner]+' · ':''}${clock(n.at)}`),el('span',n.label,'node-label'));
    const status=M.cardStatus(n);
    if(status){const summary=el('span',undefined,`card-status change-${status.tone}`);summary.append(el('span',`${status.symbol} ${status.label}`,'change-badge'));if(status.detail&&full)summary.append(el('strong',status.detail,'chosen-option'));b.append(summary);}
    if(full&&n.data.type==='expectation'){const chance=n.data.state?.chance;b.append(el('span',Number.isFinite(chance)?`${pct(chance)} expected chance`:'Earlier chance not recorded','node-chance'));}
    if(full)for(const metric of M.learningMetrics(n))b.append(learningMetric(metric));
    const roles=branch?.roles.get(n.id);if(full&&roles){const r=el('span',undefined,'memory-roles');for(const role of roles)r.append(el('span',role==='supports'?'↑ Supports':role==='conflicts'?'↓ Pulls against':'· Context',`change-badge change-${role==='supports'?'up':role==='conflicts'?'down':'same'}`));b.append(r);}
    return b;
  }
  function renderGraph(){
    graphView=preview?.view??current;
    const rootId=preview?.id??focusedEvent??preferred(moment.ids,current);
    branch=M.neighborhood(graphView,rootId);const stages=$('stages');stages.replaceChildren();$('focus-heading').replaceChildren();
    if(!branch){stages.append(el('p','Select a timeline marker to follow an event.','meta'));drawLinks();return;}
    $('focus-heading').append(el('h2',preview?'Preview':focusedEvent?'Selected event':'At this moment'),el('span',preview?'Click to pause and explore':'Click a branch to follow it','meta'));
    const lane=(title,ns,key)=>{
      const col=el('section',undefined,`branch branch-${key}`);col.append(el('h3',title));
      const page=preview?0:pages[key]??0;
      for(const n of ns.slice(page*3,page*3+3))col.append(nodeCard(n,()=>{preview=null;select(n.id);}));
      paginate(col,ns.length,page,3,p=>{pages[key]=p;renderGraph();});return col;
    };
    const before=lane('What led here',branch.before,'before');
    const seeded=branch.memories.filter(n=>M.time(n.at)<0),lived=branch.memories.filter(n=>M.time(n.at)>=0);
    for(const [label,ns,key] of [['Earlier life',seeded,'seeded'],['Memories from this run',lived,'lived']])if(ns.length){
      const d=el('details',undefined,'memory-branch');d.open=memoriesOpen;const summary=el('summary',`${label} · ${ns.length}`);
      for(const [role,symbol,tone] of [['supports','↑','up'],['conflicts','↓','down']]){const count=ns.filter(n=>branch.roles.get(n.id)?.has(role)).length;if(count){const badge=el('span',`${symbol} ${count}`,`change-badge change-${tone}`);badge.title=role==='supports'?'Supports this choice':'Pulls against this choice';summary.append(badge);}}
      d.append(summary);const page=pages[key]??0;
      for(const n of ns.slice(page*3,page*3+3))d.append(nodeCard(n,()=>select(n.id)));
      paginate(d,ns.length,page,3,p=>{pages[key]=p;memoriesOpen=true;renderGraph();});d.ontoggle=()=>{memoriesOpen=d.open;requestAnimationFrame(drawLinks);};before.append(d);
    }
    if(!branch.before.length&&!branch.memories.length)before.append(el('p','No earlier link recorded','branch-empty'));
    const center=el('section',undefined,'branch branch-focus');center.append(el('h3',clock(branch.root.updatedAt>=0?branch.root.updatedAt:branch.root.at)));
    const showDetails=()=>{stop();focusedEvent=branch.root.id;preview=null;inspection=graphView;selected=branch.root.id;renderTimeline();renderInspector();};
    const card=nodeCard(branch.root,showDetails,true);card.classList.add('focus-node');center.append(card);
    center.append(button('Open details',showDetails,'flat-button'));
    const after=lane('What followed',branch.after,'after');if(!branch.after.length)after.append(el('p','No later link recorded at this time','branch-empty'));
    stages.append(before,center,after);requestAnimationFrame(drawLinks);
  }
  function drawLinks(){
    const svg=$('connections'),graph=$('graph');svg.replaceChildren();if(!branch)return;
    svg.setAttribute('width',graph.clientWidth);svg.setAttribute('height',graph.clientHeight);
    const ns='http://www.w3.org/2000/svg',defs=document.createElementNS(ns,'defs'),marker=document.createElementNS(ns,'marker');
    for(const [key,value] of Object.entries({id:'chain-arrow',viewBox:'0 0 10 10',refX:'9',refY:'5',markerWidth:'5',markerHeight:'5',orient:'auto-start-reverse'}))marker.setAttribute(key,value);
    const tip=document.createElementNS(ns,'path');tip.setAttribute('d','M 0 0 L 10 5 L 0 10 z');marker.append(tip);defs.append(marker);svg.append(defs);
    const box=graph.getBoundingClientRect(),focus=graph.querySelector('.focus-node');if(!focus)return;
    const connect=(a,b)=>{const x=a.getBoundingClientRect(),y=b.getBoundingClientRect();if(!a.getClientRects().length||!b.getClientRects().length)return;
      const stacked=Math.abs(x.left-y.left)<5,x1=(stacked?x.left+x.width/2:x.right)-box.left,y1=(stacked?x.bottom:x.top+x.height/2)-box.top,x2=(stacked?y.left+y.width/2:y.left)-box.left,y2=(stacked?y.top:y.top+y.height/2)-box.top;
      const path=document.createElementNS(ns,'path');path.setAttribute('marker-end','url(#chain-arrow)');path.setAttribute('d',stacked?`M${x1},${y1} C${x1},${y1+16} ${x2},${y2-16} ${x2},${y2}`:`M${x1},${y1} C${x1+24},${y1} ${x2-24},${y2} ${x2},${y2}`);svg.append(path);};
    for(const b of graph.querySelectorAll('.branch-before>.chain-node'))connect(b,focus);
    // Collapsed memories are a bundle, not fabricated individual causal edges.
    for(const d of graph.querySelectorAll('.memory-branch'))connect(d.querySelector('summary'),focus);
    for(const b of graph.querySelectorAll('.branch-after>.chain-node'))connect(focus,b);
  }
  function linkGroup(parent,title,nodes){if(!nodes.length)return;const d=details(parent,title,null);const body=d.lastChild;body.replaceChildren();body.classList.add('relation-links');for(const n of nodes)body.append(button(n.label,()=>{select(n.id);}));}
  function renderInspector(){
    const panel=$('inspector');panel.replaceChildren();const v=inspection??current,n=v.all.get(selected);
    panel.hidden=!n;
    if(!n)return;
    const close=button('×',closeInspector,'inspector-close');close.setAttribute('aria-label','Close node details');panel.append(close);
    panel.append(el('span',`${n.stage} · ${clock(n.at)}`,'node-meta'),el('h2',n.label));
    if(n.table==='decisions'){
      if(n.data.thought){panel.append(el('p','Private imagination · not evidence','meta'),el('blockquote',n.data.thought));}
      panel.append(el('p',n.data.choice?`Chosen: ${n.data.alternatives.find(a=>a.id===n.data.choice.selected)?.label??'Unknown alternative'}`:'Choice pending at this moment.'));
      if(observer&&n.data.choice)panel.append(el('p',`Recorded method: ${n.data.choice.backend}`,'meta'));
      for(const a of n.data.alternatives){const card=el('div',undefined,'option'+(n.data.choice?.selected===a.id?' selected':''));card.append(el('p',a.label));
        if(observer&&n.data.choice){probability(card,n.data.choice.probabilities?.[a.id]);if(n.data.choice.modelWeights)card.append(el('small',`Authored chance above · model weight ${n.data.choice.modelWeights[a.id]??'not recorded'}`));}
        const d=details(card,`${a.reasons.length} reasons`,null);d.lastChild.replaceChildren();
        for(const r of a.reasons){const line=el('div',undefined,'reason');line.append(el('small',r.kind==='supports'?'Supports':r.kind==='conflicts'?'Pulls against':'Context only'),el('p',r.label??M.human(r.code)));
          if(observer&&Number.isFinite(r.value))line.append(el('small',`Recorded value ${r.value}`));
          for(const id of r.cites){const ref=v.all.get(id);if(ref)line.append(button(ref.label,()=>{select(id);},'quiet'));}d.lastChild.append(line);}
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
      for(const item of n.data.items){const ref=v.all.get(item.record);if(ref)panel.append(button(ref.label,()=>{select(ref.id);},'option'));}
      details(panel,'What brought them to mind',n.data.items.map(x=>({memory:v.all.get(x.record)?.label,cues:x.matched})));
      if(observer&&n.data.recallDetails)details(panel,'Recall selection details',n.data.recallDetails);
    }else if(n.table==='records'){
      if(n.data.state)panel.append(el('p',Number.isFinite(n.data.state.chance)?`Expected chance: ${pct(n.data.state.chance)}`:n.data.state.status));
      if(n.data.stance)panel.append(el('p',`Reading: ${M.human(n.data.stance)}`));
      if(Number.isFinite(n.data.strength))panel.append(el('p',`Strength: ${n.data.strength} · ${n.data.sign==='+'?'warm association':'uneasy association'}`));
      const {provenance,...record}=n.data;details(panel,'Record details',record);
      if(observer&&provenance)details(panel,'Source provenance',provenance);
    }else{
      const {provenance,recallDetails,observerContext,recordedLabel,...visible}=n.data;
      renderValue(panel,visible);
      if(recordedLabel)details(panel,'Recorded wording',recordedLabel);
      if(observer&&provenance)details(panel,'Source provenance',provenance);
      if(observer&&recallDetails)details(panel,'Recall selection details',recallDetails);
      if(observer&&observerContext)details(panel,'Observer-only context',observerContext);
    }
    linkGroup(panel,'Supporting records',n.refs.map(id=>v.all.get(id)).filter(Boolean));
    linkGroup(panel,'Recorded consequences',v.nodes.filter(x=>x.refs.includes(n.id)));
    if(observer)details(panel,'Record identifier',n.id);
  }
  function probability(parent,p){parent.append(el('span',pct(p),'meta'));if(Number.isFinite(p)){const bar=el('div',undefined,'probability'),fill=el('span');fill.style.width=`${Math.max(0,Math.min(1,p))*100}%`;bar.append(fill);parent.append(bar);}}
  function renderComparisons(){
    const root=$('comparisons');root.replaceChildren();if(!observer||root.hidden)return;
    root.append(el('p','Isolated choice · fallback held fixed · no later lives simulated. Actual choices may use Laya; these comparison arms do not call a model.','notice'));
    for(const row of current.comparisons){let c;try{c=M.comparison(model,row);}catch(e){root.append(el('p',e.message));continue;}
      const card=el('article',undefined,'comparison');card.append(el('h3',c.question));const result=el('div',undefined,'result');
      for(const [k,label] of [['reasons','Reasons'],['probabilities','Probabilities'],['sampledAction','Selected action']])result.append(el('span',`${label}: ${c.differs[k]?'changed':'unchanged'}`,c.differs[k]?'changed':''));card.append(result);
      const arms=el('div',undefined,'arms');for(const a of c.arms){const arm=el('section',undefined,'arm');arm.append(el('h4',a.label),el('p',`${a.method} · same draw ${a.draw}`,'meta'));
        for(const o of a.options){const opt=el('div',undefined,'option');opt.append(el('p',o.label));probability(opt,o.probability);details(opt,'Recorded reasons',o.reasons);arm.append(opt);}arm.append(el('p',`Selected: ${a.selected}`));arms.append(arm);}card.append(arms);
      details(card,'What changed, what stayed fixed',{intervention:c.intervention,heldConstant:c.heldConstant,validity:c.validity,derivedDifferences:c.unavoidableDifferences,actualRun:c.runChoice,scope:c.wholeRun});root.append(card);
    }
  }
  function step(direction){const point=direction>0?tape.find(e=>e.at>tick):tape.filter(e=>e.at<tick).at(-1);if(!point){stop();return;}tick=point.at;focusedEvent=null;selected=null;inspection=null;momentPage=0;pages={};memoriesOpen=false;render();}
  $('previous').onclick=()=>{stop();step(-1);};$('next').onclick=()=>{stop();step(1);};$('play').onclick=()=>{
    if(timer!==null){stop();render();return;}
    focusedEvent=null;preview=null;selected=null;inspection=null;playbackAt=tick;lastFrame=performance.now();render();
    $('play').textContent='Pause';timer=requestAnimationFrame(animate);
  };
  function animate(now){
    const previous=tick;
    playbackAt=M.advancePlayback(playbackAt,now-lastFrame,Number($('speed').value),totalMax);lastFrame=now;
    tick=Math.floor(playbackAt);
    if(gates.some(at=>at>previous&&at<=tick)||moment?.exact&&tick!==previous||playbackAt>windowEnd){
      momentPage=0;pages={};render();
    }
    $('clock').textContent=clock(playbackAt);
    $('time').value=playbackAt;
    $('time-cursor').style.left=`${Math.max(0,Math.min(100,(playbackAt-windowStart)/(windowEnd-windowStart)*100))}%`;
    if(tick!==previous)notify();
    if(playbackAt>=totalMax){stop();render();return;}
    timer=requestAnimationFrame(animate);
  }
  $('time').oninput=()=>{stop();tick=Math.floor(Number($('time').value));focusedEvent=null;selected=null;inspection=null;momentPage=0;pages={};render();};
  $('zoom').onchange=()=>{setWindow(tick);renderTimeline();};
  $('window-back').onclick=()=>{const size=$('zoom').value==='custom'?windowEnd-windowStart:zoomSpan;windowEnd=windowStart;windowStart=Math.max(totalMin,windowEnd-size);seek(tape.filter(e=>e.at>=windowStart&&e.at<windowEnd).at(-1)?.at??windowStart);};
  $('window-next').onclick=()=>{const size=$('zoom').value==='custom'?windowEnd-windowStart:zoomSpan;windowStart=windowEnd;windowEnd=Math.min(totalMax,windowStart+size);seek(tape.find(e=>e.at>=windowStart&&e.at<=windowEnd)?.at??windowStart);};
  $('person').onchange=()=>{stop();who=Number($('person').value);thread='all';reindex();focusTime();};
  $('lens').onchange=()=>{stop();observer=$('lens').value==='observer';reindex();focusTime();};
  $('thread').onchange=()=>{stop();thread=$('thread').value;reindex();focusTime();};
  $('compare-toggle').onclick=()=>{$('comparisons').hidden=!$('comparisons').hidden;$('compare-toggle').setAttribute('aria-expanded',String(!$('comparisons').hidden));$('compare-toggle').textContent=$('comparisons').hidden?'Open isolated comparisons':'Close comparisons';renderComparisons();};
  $('example').onclick=()=>example();$('open-recording').onclick=()=>$('recording-file').click();
  $('recording-file').onchange=async()=>{const f=$('recording-file').files[0];if(!f)return;try{accept(window.UnderGlassTools.parseRecording(await f.text()),null);}catch(e){error(e);}finally{$('recording-file').value='';}};
  window.addEventListener('resize',()=>{if(model&&!model.missing){renderTimeline();requestAnimationFrame(drawLinks);}});
  document.addEventListener('keydown',e=>{if(e.key==='Escape'&&selected){e.preventDefault();closeInspector();}});
  new ResizeObserver(()=>requestAnimationFrame(drawLinks)).observe($('graph'));
  window.underGlassToolPage={page:'chains',update:(run,url)=>{const filter=thread;accept(run,url,{t:run.live?.at??run.days*1440-1,person:model?.run.names[who]??'',tab:'inner'});if(filter!=='all'){thread=filter;reindex();render();}},load:accept,source:()=>model?source:undefined,hasRecording:()=>!!model,state:()=>({t:tick,person:model?.run.names[who]??'',tab:'inner'})};
  document.addEventListener('DOMContentLoaded',()=>{if(!new URLSearchParams(location.search).has('run'))example(window.UnderGlassTools.readState(location.hash));});
})();
