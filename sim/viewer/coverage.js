/* Mounted by Run Viewer; reads its current recording and clock, never calls a model. */
(() => {
  'use strict';
  const labels = {laya:'Laya answer', fallback:'Authored / fallback', none:'No evaluation recorded', unknown:'Other / unknown'};
  const phases = {'offer':'Offer a plan','answer':'Answer a plan','follow-through':'Keep a plan',
    'commitment-refusal':'After refusing an errand','concern':'Offer concern'};
  const fmt = n => n == null ? 'Not recorded' : n.toLocaleString();
  const when = t => `Day ${Math.floor(t / 1440) + 1} · ${String(Math.floor(t % 1440 / 60)).padStart(2,'0')}:${String(t % 60).padStart(2,'0')}`;
  function el(tag, attrs={}, ...children) {
    const node = document.createElement(tag);
    for (const [k,v] of Object.entries(attrs)) {
      if (k === 'text') node.textContent = v;
      else if (k.startsWith('on')) node.addEventListener(k.slice(2),v);
      else node.setAttribute(k,v);
    }
    node.append(...children.flat().filter(v => v != null)); return node;
  }
  const badge = status => el('span',{class:`coverage-badge coverage-${status}`,text:labels[status]});
  const disclosure = (title,...children) => el('details',{},el('summary',{text:title}),...children);
  function mount(host, api) {
    let run, data, error, key='', listKey='', page=0, filtered=[];
    const people = el('select',{'aria-label':'Coverage character',onchange:() => {page=0;api.selectPerson(people.value);}});
    const whole = el('input',{type:'checkbox',onchange:() => {page=0;key='';listKey='';render();}});
    const status = el('select',{'aria-label':'Filter evening choices',onchange:() => {page=0;key='';render();}},
      el('option',{value:'all',text:'All evening choices'}),
      ...Object.entries(labels).map(([value,text])=>el('option',{value,text})));
    const scope = el('p',{class:'coverage-scope'}), summary = el('div',{class:'coverage-stats'});
    const metrics = el('div',{class:'coverage-metrics'}), list = el('div',{class:'coverage-list'});
    const previous = el('button',{text:'Previous',onclick:()=>{page--;drawRows();}});
    const next = el('button',{text:'Next',onclick:()=>{page++;drawRows();}});
    const pageInfo = el('span',{}), empty = el('p',{class:'coverage-empty'});
    const others = el('div',{class:'coverage-others'}), receipts = el('div',{});
    host.classList.add('coverage');
    host.replaceChildren(el('div',{class:'coverage-head'},el('h2',{text:'Model coverage'}),
      el('label',{},'Character ',people), el('label',{class:'coverage-whole'},whole,' Whole recording (spoilers)')),
      scope,summary,metrics,
      el('div',{class:'coverage-list-head'},el('h3',{text:'Evening choices'}),status),empty,list,
      el('nav',{class:'coverage-pager','aria-label':'Evening choice pages'},previous,pageInfo,next),
      others,disclosure('Coverage receipts and limits',receipts,
        el('p',{text:'Observer diagnostics, not a character’s knowledge. Coverage counts recorded evening choices only; ordinary acts are not included. A selection is not proof that an action happened.'}),
        el('p',{text:'Laya answer means the receipt labels its backend as Laya. Model names are recorded strings, not verified checkpoints. Recorded HTTP attempts can be replayed from a tape; they do not prove fresh network calls.'}),
        el('p',{text:'Evening receipts appear one minute after their opportunity, when answers can apply. Reflections appear after their considered event. Whole recording includes future choices; live runs include only the received snapshot.'})));
    function drawRows() {
      page = Math.max(0,Math.min(page,Math.ceil(filtered.length/20)-1));
      const open = new Set([...list.querySelectorAll('details[open]')].map(n=>n.dataset.id));
      list.replaceChildren(...filtered.slice(page*20,page*20+20).map(r => {
        const info = el('dl',{class:'coverage-receipt'});
        const field = (name,value) => info.append(el('dt',{text:name}),el('dd',{text:value ?? 'Unknown'}));
        field('Choice ID',r.id); field('Opportunity',when(r.opportunityAt));
        field('Configured model',r.configured);field('Answer model',r.model);field('Reported model',r.reported);
        field('Backend',r.backend); field('HTTP attempt recorded',r.called === null ? 'Unknown' : r.called ? 'Yes (may be tape replay)' : 'No');
        field('Request characters',r.characters);field('Recall omitted',r.omitted === null ? 'Unknown' : r.omitted.length ? r.omitted.join(', ') : 'None');
        field('Authored selection',r.fallbackLine);field('Recorded note',r.note);
        const details = el('details',{'data-id':r.id},
          el('summary',{},el('span',{class:'coverage-choice-meta'},
            el('span',{class:'data',text:when(r.at)}),
            ...(!people.value ? [el('strong',{text:r.person})] : []),
            el('span',{text:phases[r.phase] ?? r.phase}),badge(r.status)),
            el('span',{class:'coverage-choice-line',text:r.line})),
          el('div',{class:'coverage-choice-detail'},
            el('button',{text:'Go to this choice',onclick:()=>{whole.checked=false;key='';api.seek(r.at,r.person);}}),info));
        if(open.has(r.id))details.open=true;
        return details;
      }));
      empty.hidden=filtered.length>0;
      empty.textContent=data?.recorded ? 'No evening choices match this character, time and filter.' : 'Evening choices were not recorded in this run.';
      previous.disabled=page===0;next.disabled=(page+1)*20>=filtered.length;
      pageInfo.textContent=filtered.length ? `${page*20+1}–${Math.min((page+1)*20,filtered.length)} of ${fmt(filtered.length)}` : '0 choices';
    }
    function render() {
      const state = api.state();
      if (!state.run) {scope.textContent='Open a recording to inspect its model coverage.';return;}
      if (run !== state.run) {
        const sameNames=JSON.stringify(run?.names)===JSON.stringify(state.run.names);
        run=state.run; error=null;key='';listKey='';
        try {data=UnderGlassCoverageModel.index(run);} catch(e) {data=null;error=e.message;}
        if(!sameNames)people.replaceChildren(el('option',{value:'',text:'Everyone'}),...run.names.map(n=>el('option',{value:n,text:n})));
      }
      const nextKey=JSON.stringify([Math.floor(state.at),state.person,whole.checked,status.value]);
      if(key===nextKey)return;key=nextKey;people.value=state.person ?? '';
      if(error) {scope.textContent=`Cannot read coverage: ${error}`;summary.replaceChildren();metrics.replaceChildren();list.replaceChildren();others.replaceChildren();receipts.replaceChildren();empty.hidden=true;previous.disabled=next.disabled=true;pageInfo.textContent='';return;}
      const result=UnderGlassCoverageModel.view(data,{person:people.value,at:state.at,whole:whole.checked});
      scope.textContent=`${people.value || 'Everyone'} · ${whole.checked ? 'Whole recording' : 'Through '+when(Math.floor(state.at))}${result.partial ? ' · Live snapshot — incomplete' : ''}`;
      summary.replaceChildren(...[
        ['Recorded choices',result.total,'total'],['Laya answers',result.total==null?null:result.counts.laya,'laya'],
        ['Authored / fallback',result.total==null?null:result.counts.fallback,'fallback'],['No evaluation',result.total==null?null:result.counts.none,'none']
      ].map(([label,value,kind]) => el('div',{class:`coverage-stat coverage-${kind}`},el('strong',{text:fmt(value)}),el('span',{text:label}))));
      const fraction=result.total ? `${result.evaluated} / ${result.total} (${Math.round(result.evaluated/result.total*100)}%)` : '—';
      metrics.replaceChildren(el('span',{text:`Evaluated: ${fraction}`}),
        ...(result.counts.unknown ? [el('span',{text:`Other / unknown: ${result.counts.unknown}`})] : []),
        ...(result.orphans ? [el('span',{class:'coverage-fallback',text:`${result.orphans} unmatched evaluations excluded`})] : []));
      filtered=result.rows.filter(r=>status.value==='all'||r.status===status.value);
      // Do not replace focused/open choice cards on every playback frame between decisions.
      const nextListKey=JSON.stringify([people.value,status.value,filtered.length]);
      if(listKey!==nextListKey) {listKey=nextListKey;drawRows();}
      const route=result.other.livesRouting,company=result.other.livesCompany;
      // These are different consumers; never include them in the evening denominator.
      const otherOpen=others.querySelector('details')?.open;
      const details=disclosure('Other recorded decisions',
        el('div',{class:'coverage-other-grid'},
          el('div',{},el('h3',{text:'Free-time routing'}),el('p',{text:`${fmt(route.choices)} choices`}),el('p',{class:'muted',text:`Backend: ${route.backend ?? 'not recorded'}`}),
            ...(route.choices==null?[]:[el('p',{text:`${route.laya} Laya answers · ${route.fallback} fallbacks`})])),
          el('div',{},el('h3',{text:'Immediate company'}),el('p',{text:`${fmt(company.choices)} choices · ${fmt(company.answers)} answers`}),el('p',{class:'muted',text:`Backend: ${company.backend ?? 'not recorded'}`})),
          el('div',{},el('h3',{text:'Private inspiration'}),el('p',{text:`${fmt(result.reflectionCount)} considered reflections`}),
            ...(result.reflectionCount == null ? [] : [
              el('p',{text:`${result.passes} evaluator passes · ${result.generated} generated responses`}),
              el('p',{class:'muted',text:`${result.passResponses} pass responses · ${result.passErrors} errors · ${result.passUnknown} unknown pass lists. HTTP attempts not recorded.`})]))),
        ...(route.rows.some(r=>r.backend) ? [disclosure('Recent destination selections',
          ...route.rows.slice(-20).reverse().map(r=>el('p',{},
            el('button',{text:`${when(r.at)} · ${r.person}`,onclick:()=>{whole.checked=false;key='';api.seek(r.at,r.person);}}),
            el('span',{text:` ${r.line} · ${r.backend ?? 'No evaluation recorded'} · ${r.resolution==='applied' ? 'Destination set (arrival not confirmed)' : r.resolution ?? (r.backend ? 'Awaiting execution' : 'Routine selection (arrival not recorded)')}`}),
            ...(r.note ? [el('small',{class:'muted',text:' '+r.note})] : []))),
          el('p',{text:'Last 20 selections in scope. Counts include all recorded destination choices.'}))] : []),
        ...result.reflections.slice(-20).map(r=>el('p',{},el('span',{class:'data',text:when(r.at)+' · '}),
          el('strong',{text:r.person+' · '}),r.answer?.backend ?? 'Backend not recorded')),
        ...(result.reflections.length>20 ? [el('p',{text:'Showing the last 20 reflection receipts. Counts include all considered reflections in scope.'})] : []));
      details.open=!!otherOpen;others.replaceChildren(details);
      receipts.replaceChildren(el('p',{text:`Evening policy: ${result.settings.evaluation ?? 'Not recorded (older recordings may use selective evaluation)'}. Configured model: ${result.settings.model ?? 'not recorded'}.`}),
        el('p',{text:`Matched evaluations: ${result.called} recorded HTTP attempts; ${result.callsUnknown} unknown attempt flags. Recall omitted in ${result.omissions} requests; ${result.omissionUnknown} unknown omission lists.`}),
        el('p',{text:`${result.changed} / ${result.comparable} comparable selections differ from the authored selection. This measures disagreement, not quality.`}));
    }
    return {render};
  }
  window.UnderGlassCoverage = {mount};
})();
