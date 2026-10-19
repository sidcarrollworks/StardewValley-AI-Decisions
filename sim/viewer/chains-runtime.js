/* Presentation-only projection of the evening pilot's version-1 receipts.
 * This is an internal graph, not a Lives observer-contract export. No formulas or model calls. */
(function(root,factory){
  const api=factory();
  if(typeof module==='object'&&module.exports)module.exports=api;
  else root.UnderGlassRuntimeChains=api;
})(typeof globalThis!=='undefined'?globalThis:this,()=>{
  'use strict';
  const tables=['records','opportunities','recalls','decisions','attempts','plans','outcomes','perceptions','learning'];
  const enc=s=>encodeURIComponent(String(s));
  const id=(...parts)=>'runtime:'+parts.map(enc).join(':');
  const clock=m=>Number.isFinite(m)&&m>=0?m:'pre-run';
  const key=k=>`${k.kind}:${k.subject}:${enc(k.context)}`;
  const place=s=>s==='Home:Trailer'?'home':s?.startsWith('Home:')?'their home':s==='Saloon'?'the Saloon':s||'the place';
  function expectation(k){
    if(k.kind==='Whereabouts'){
      const match=k.context.match(/^(.*):(\d+)-(\d+):(weekday|weekend)$/);
      return match?`Will ${k.subject} be at ${place(match[1])} between ${match[2]}:00 and ${match[3]}:00 on ${match[4]}s?`:`Will ${k.subject} be where expected?`;
    }
    return ({Answers:`Will ${k.subject} agree?`,Availability:`Will ${k.subject} be available?`,FollowThrough:`Will ${k.subject} keep the plan?`,Activity:`Will a game with ${k.subject} finish?`})[k.kind]||`Expectation about ${k.subject}`;
  }
  function feeling(a){
    const o=a.object, target=o.kind==='Person'?o.key:o.kind==='Place'?place(o.key):o.kind==='Situation'?'the evening':o.key==='PlayedGame'?'playing a game':o.key==='Reading'?'reading':o.key.replace(/([a-z])([A-Z])/g,'$1 $2').toLowerCase();
    return `${a.amount<0?(o.kind==='Situation'?'Disappointed about':'Uneasy about'):'Warmth toward'} ${target}`;
  }
  const outcomeLabels={'yes':'Heard yes','no':'Heard no','not-now':'Heard “not now”',available:'Partner was available',completed:'Finished the game together',interrupted:'The game was interrupted','chose-other-plans':'Chose other plans','did-not-witness-window':'Did not observe the agreed window','not-seen-during-plan':'Did not see them during the agreed window','evening-did-not-happen':'The agreed game did not happen','whereabouts-found':'Saw them where expected','whereabouts-missed':'Did not see them where expected','concern-waiting':'Waiting for a chance to help','concern-acted':'Helped when they met','concern-expired':'No chance to help arose'};
  const stages={Pending:'Offered an evening game',Agreed:'Agreed to an evening game',Playing:'Started the game',Completed:'Finished the game',Interrupted:'The game was interrupted',Missed:'The evening did not happen',NotNow:'Asked to decide another time',Refused:'Declined the plan',Unavailable:'The encounter ended'};
  function adapt(run){
    if(!run.livesEvenings&&!run.livesCompany&&!run.livesRouting)return null;
    for(const name of ['livesEvenings','livesRouting','livesCompany'])if(run[name]&&run[name].version!==1)throw Error(`Unsupported ${name} version ${run[name].version}.`);
    const e=run.livesEvenings||{choices:[],updates:[],outcomes:[]},r=run.livesRouting||{},company=run.livesCompany||{};
    for(const field of ['choices','updates','outcomes'])if(!Array.isArray(e[field]))throw Error(`Missing evening ${field}.`);
    const holders=r.observerProjection?.holders||run.names, included=h=>holders.includes(h);
    const companyUpdates=new Map();
    for(const u of company.updates||[]){const ep=u.episode;if(ep.id.startsWith('evening:')||!included(ep.asker)&&!included(ep.asked))continue;if(!companyUpdates.has(ep.id))companyUpdates.set(ep.id,[]);companyUpdates.get(ep.id).push(u);}
    const answerChoices=(company.answers||[]).filter(a=>included(a.holder)).map(a=>({phase:'company-answer',episodeId:a.episodeId,decision:{opportunity:{id:`company:answer:${a.episodeId}:${a.at}`,holder:a.holder,at:a.at},selectedId:a.selectedId,unit:a.unit,alternatives:a.options.map(o=>({option:o,probability:o.probability,reasons:[]}))}}));
    const choices=[...e.choices,...(company.choices||[]).filter(c=>included(c.decision.opportunity.holder)).map(c=>({...c,phase:'company-'+c.phase})),...answerChoices,...(r.decisions||[]).map(c=>({...c,phase:'routing'}))];
    const l={version:'runtime-1',rules:'Recorded evening pilot receipts · no recomputed simulation rules',runtime:true,threads:[],comparisons:[]};
    tables.forEach(t=>l[t]=[]);
    const rows=new Map(), versions=new Map(), causes=new Map(), choiceByPlan=new Map(), plans=new Map();
    const owner=name=>{const n=run.names.indexOf(name);if(n<0)throw Error(`Unknown recorded person: ${name}`);return n;};
    const add=(table,row)=>{if(rows.has(row.id))throw Error(`Duplicate runtime node: ${row.id}`);rows.set(row.id,row);l[table].push(row);return row;};
    const own=(holder,at)=>({holder:owner(holder),visibleAt:clock(at),knownBy:[{who:owner(holder),availableAt:clock(at)}]});
    const version=(holder,ref,at,row)=>{const k=id(holder,ref);if(!versions.has(k))versions.set(k,[]);versions.get(k).push({at,row:row.id});};
    const lookup=(holder,ref,at)=>{
      const vs=versions.get(id(holder,ref));if(vs){for(let n=vs.length-1;n>=0;n--)if(vs[n].at<=at)return vs[n].row;}
      const c=causes.get(id(holder,ref));return c?.filter(x=>x.at<=at).at(-1)?.row;
    };
    const cause=(holder,ref,at,row)=>{const k=id(holder,ref);if(!causes.has(k))causes.set(k,[]);causes.get(k).push({at,row});};
    const pending=[];
    const defer=(row,holder,at,refs)=>pending.push({row,holder,at,refs:refs.filter(Boolean)});
    const history=r.history||{},accounts=new Map((history.accounts||[]).map(a=>[a.id,a]));
    const sources=new Map((history.sources||[]).map(s=>[s.id,s]));
    for(const a of history.accounts||[]){
      const n=add('records',{id:id('history',a.holder,a.id),...own(a.holder,-1),type:'account',label:a.text,scope:a.how,sources:(a.sources||[]).map(s=>sources.get(s)||s)});
      version(a.holder,a.id,-1,n);
    }
    for(const a of history.readings||[]){
      const holder=accounts.get(a.accountId)?.holder;if(!holder)continue;
      const n=add('records',{id:id('history',holder,a.id),...own(holder,-1),type:'reading',label:a.text,stance:a.stance,origin:a.origin,sources:(a.sources||[]).map(s=>sources.get(s)||s),support:a.review});
      version(holder,a.id,-1,n);defer(n,holder,-1,[a.accountId]);
    }
    // Plan and choice IDs are explicit joins; temporal proximity never establishes a causal edge.
    for(const u of e.updates){if(!u.plan||!Number.isFinite(u.at))throw Error('Invalid evening update.');const p=u.plan;if(!plans.has(p.id))plans.set(p.id,[]);plans.get(p.id).push(u);}
    for(const [index,c]of choices.entries()){
      const d=c.decision,o=d?.opportunity;if(!o||!Number.isFinite(o.at))throw Error('Invalid evening decision.');
      const cid=id('choice',o.id), oid=id('opportunity',o.id), rid=id('recall',o.id);
      const op=add('opportunities',{id:oid,...own(o.holder,o.at),label:c.context||({'routing':'Available routine destinations','company-seek':'Consider looking for company','company-offer':'A chance to offer a game','company-answer':'Someone offered a game',offer:'Consider an evening together',answer:'Answer an invitation','follow-through':'Decide whether to keep the plan','expectation-miss':'Consider what to do after a missed sighting'}[c.phase]||'Consider another way to help'),trigger:c.phase,causes:[]});
      const recall=add('recalls',{id:rid,...own(o.holder,o.at),label:'What came to mind',opportunity:oid,items:[]});
      const evaluation=c.evaluation, selected=c.phase==='routing'?c.selectedOption:evaluation?.selectedId??d.selectedId;
      const alternatives=d.alternatives?.length?d.alternatives:(o.options||[]).map(option=>({option,reasons:[]}));
      // Receipts record selection at opportunity time; execution is separately recorded later.
      const n=add('decisions',{id:cid,...own(o.holder,o.at),label:({'company-seek':'Look for company?','company-offer':'Offer a game here?','company-answer':'Accept the game?','routing':'Where to spend time?',offer:'Make an evening plan?',answer:'Agree to the evening?', 'follow-through':'Keep the evening plan?', 'expectation-miss':'Offer help after not seeing them?'}[c.phase]||'Offer company instead?'),opportunity:oid,recall:rid,
        alternatives:alternatives.map(a=>({id:a.option.id,slug:a.option.id,label:a.option.line.replace(/Home:([A-Za-z]+)/g,(_,name)=>name==='Trailer'?'home':name+' home'),reasons:(a.reasons||[]).map(z=>({kind:z.kind.toLowerCase(),code:z.code,value:z.value,label:z.code}))})),
        excluded:d.excluded||[],constraints:d.constraints||[],choice:selected==null?undefined:{visibleAt:o.at,selected,backend:c.usedLegacy?'routine draw':evaluation?.answer?.backend||'authored',probabilities:Object.fromEntries((d.alternatives||[]).map(a=>[a.option.id,a.probability])),provenance:evaluation?{model:evaluation.answer.model,reportedModel:evaluation.answer.reportedModel,packet:evaluation.answer.packet,response:evaluation.answer.response}:undefined,sampled:d.unit}});
      // Model weights are recorded, not claimed to be authored probabilities or explanations.
      if(evaluation?.answer?.weights)n.choice.modelWeights=evaluation.answer.weights;
      const episode=c.planId||(c.episodeId?'company:'+c.episodeId:null);
      if(episode){if(!choiceByPlan.has(episode))choiceByPlan.set(episode,[]);choiceByPlan.get(episode).push({c,n,op});}
      pending.push({choice:c,node:n,recall});
      if(c.comparison?.comparable){const cmp=c.comparison;
        l.comparisons.push({id:id('comparison',o.id),decision:cid,visibleAt:o.at,scope:'isolated-choice',question:`${o.holder}: remove one history from this choice`,intervention:{removed:cmp.removedHistoryIds},heldConstant:'Scene, remaining memory, option inputs and random draw',validity:'Authored calculation only; no model calls or alternate future lives',differs:{reasons:cmp.reasonsChanged,probabilities:cmp.probabilitiesChanged,sampledAction:cmp.selectionChanged},runChoice:{selected,backend:n.choice?.backend},arms:[['With history',cmp.withHistory],['Without this history',cmp.withoutHistory]].map(([label,a])=>({label,inputs:{method:{backend:'authored'},randomInput:{unit:a.unit}},derived:{sampled:a.selectedId,probabilities:Object.fromEntries(a.alternatives.map(b=>[b.option.id,b.probability])),reasons:Object.fromEntries(a.alternatives.map(b=>[b.option.id,b.reasons]))}}))});
      }
    }
    for(const [pid,updates]of plans){
      const p=updates[0].plan, choices=choiceByPlan.get(pid)||[], offered=updates.find(u=>u.plan.stage==='Pending'), agreed=updates.find(u=>u.plan.stage==='Agreed');
      const answer=choices.find(x=>x.c.phase==='answer'), offer=choices.find(x=>['offer','commitment-refusal'].includes(x.c.phase));
      let attempt;
      if(offered){const resolution=updates.find(u=>['Agreed','NotNow','Refused','Unavailable'].includes(u.plan.stage));
        attempt=add('attempts',{id:id('offer',pid),visibleAt:offered.at,actor:owner(p.asker),label:`Ask ${p.asked} about an evening game`,kind:'ask',decision:offer?.n.id,attempted:true,resolvedAt:resolution?.at,status:resolution?.plan.stage==='Unavailable'?'unsuccessful':'answered',knownBy:answer?[{who:owner(p.asked),availableAt:answer.c.decision.opportunity.at}]:[]});
        if(answer)answer.op.causes.push(attempt.id);
      }
      let agreement;
      if(agreed){agreement=add('plans',{id:id('plan',pid),visibleAt:agreed.at,label:`Game with ${p.asker} and ${p.asked}`,knownBy:[p.asker,p.asked].map(h=>({who:owner(h),availableAt:agreed.at})),origin:[attempt?.id,answer?.n.id].filter(Boolean),answer:'yes',activity:'Play a game',place:p.place,window:{from:p.start,until:p.end},formal:false});}
      for(const x of choices.filter(x=>x.c.phase==='follow-through')){
        if(agreement)x.op.causes.push(agreement.id);
        const d=x.c.decision,h=d.opportunity.holder,selected=x.c.evaluation?.selectedId??d.selectedId;
        if(selected==='keep'){
          const out=e.outcomes.find(o=>o.planId===pid&&o.holder===h&&o.at>=d.opportunity.at),playing=updates.find(u=>u.plan.stage==='Playing');
          add('attempts',{id:id('keep',pid,h),visibleAt:d.opportunity.at,actor:owner(h),label:'Intends to keep the evening plan',kind:'planned-intention',attempted:null,progress:playing?[{at:playing.at,status:'playing'}]:[],decision:x.n.id,resolvedAt:out?.at,status:out?.kind==='completed'?'completed':out?.kind==='interrupted'?'interrupted':'unsuccessful'});
        }
      }
      const ids=[attempt?.id,agreement?.id,...choices.flatMap(x=>[x.n.opportunity,x.n.recall,x.n.id])].filter(Boolean);
      for(const [i,u]of updates.entries()){
        const world=add('outcomes',{id:id('world',pid,i),visibleAt:u.at,settledAt:u.at,label:stages[u.plan.stage]||u.plan.stage,kind:u.plan.stage,world:{reason:u.reason,actId:u.plan.actId},plan:agreement?.id,attempts:[attempt?.id,...choices.filter(x=>x.c.phase==='follow-through'&&x.c.decision.opportunity.at<u.at).map(x=>x.n.id)].filter(Boolean)});ids.push(world.id);
      }
      l.threads.push({id:id('thread',pid),visibleAt:updates[0].at,stages:[{ids}],summary:[{visibleAt:updates[0].at,label:`${p.asker} & ${p.asked} · evening plan`}]});
    }
    // Immediate company uses its own explicit episode IDs. A search outcome belongs to the
    // searcher; an unavailable execution gate is not evidence of the partner's private reason.
    for(const [pid,updates] of companyUpdates){
      const ep=updates[0].episode, group=choiceByPlan.get('company:'+pid)||[], seeking=updates.find(u=>u.episode.stage==='Seeking'), offered=updates.find(u=>u.episode.stage==='Pending'), playing=updates.find(u=>u.episode.stage==='Playing');
      const ended=updates.find(u=>['Completed','Interrupted','NotFound','Unavailable','NotNow','Refused','Aborted'].includes(u.episode.stage));
      const seek=group.find(x=>x.c.phase==='company-seek'), offer=group.find(x=>x.c.phase==='company-offer'), answer=group.find(x=>x.c.phase==='company-answer');
      const state={Completed:'completed',Interrupted:'interrupted',NotFound:'unsuccessful',Unavailable:'unsuccessful',NotNow:'answered',Refused:'answered',Aborted:'unsuccessful'}[ended?.episode.stage];
      let search,ask;
      if(seeking){
        search=add('attempts',{id:id('company-search',pid),visibleAt:seeking.at,actor:owner(ep.asker),label:`Look for ${ep.asked} at ${place(ep.place)}`,kind:'go',attempted:true,decision:seek?.n.id,resolvedAt:ended?.at,status:state,progress:playing?[{at:playing.at,status:'playing'}]:[]});
        if(offer)offer.op.causes.push(search.id);
      }
      if(offered){
        const heard=(company.answers||[]).find(a=>a.episodeId===pid);
        ask=add('attempts',{id:id('company-offer',pid),visibleAt:offered.at,actor:owner(ep.asker),label:`Ask ${ep.asked} to play here`,kind:'ask',attempted:true,decision:offer?.n.id,resolvedAt:ended?.at,status:state,progress:playing?[{at:playing.at,status:'playing'}]:[],knownBy:heard?[{who:owner(ep.asked),availableAt:heard.at}]:[]});
        if(answer)answer.op.causes.push(ask.id);
      }
      const ids=group.flatMap(x=>[x.n.id,x.n.recall,x.n.opportunity]);
      for(const [i,u]of updates.entries()){
        const n=add('outcomes',{id:id('company-world',pid,i),visibleAt:u.at,settledAt:u.at,label:({Seeking:'Set out to look for company',Searching:'Looked around for them',Pending:'Offered a game',Playing:'Started the game',Completed:'Finished the game',Interrupted:'Game interrupted',NotFound:'Did not find them',Unavailable:'Could not offer a game',Aborted:'Search ended',Refused:'Declined the game',NotNow:'Asked to wait'})[u.episode.stage]||u.episode.stage,kind:u.episode.stage,world:{reason:u.reason,actId:u.episode.actId},attempts:[search?.id,ask?.id].filter(Boolean)});ids.push(n.id);
      }
      // Search completion/abandonment is the asker's own action; do not expose gate reasons.
      if(ended&&included(ep.asker)&&!(company.outcomes||[]).some(o=>o.episodeId===pid&&o.holder===ep.asker&&o.at===ended.at))add('perceptions',{id:id('company-ended',pid),...own(ep.asker,ended.at),who:owner(ep.asker),label:ended.episode.stage==='Aborted'?'Stopped looking for company':'The company attempt ended',of:ask?.id||search?.id,perceived:{kind:'attempt-ended'},causes:[]});
      l.threads.push({id:id('company-thread',pid),visibleAt:updates[0].at,stages:[{ids}],summary:[{visibleAt:updates[0].at,label:`${ep.asker} & ${ep.asked} · company`}]});
    }
    // Own outcome receipts, including the managed game's separate activity learning.
    const observationById=new Map(), primaryPerceptions=new Set(), personalOutcomes=new Map();
    const observed=[...e.outcomes.map((o,i)=>({o,i,source:'evening',pid:o.planId})),...(company.outcomes||[]).filter(o=>included(o.holder)).map((o,i)=>({o,i,source:'company',pid:o.episodeId.startsWith('evening:')?o.episodeId.slice(8):'company:'+o.episodeId}))];
    for(const {o,i,source,pid}of observed){
      const choices=choiceByPlan.get(pid)||[], ownChoice=choices.filter(x=>x.c.decision.opportunity.holder===o.holder&&x.c.decision.opportunity.at<=o.at).at(-1);
      const immediate=pid.startsWith('company:'),episodeId=immediate?pid.slice(8):pid;
      // A search is the asker's private journey: only the asker's outcome may cite it. The asked
      // partner knows the offer they heard, so their game outcome cites that offer instead.
      const asker=immediate?companyUpdates.get(episodeId)?.[0]?.episode.asker:undefined, partner=immediate&&o.holder!==asker;
      const offerId=immediate?id('company-offer',episodeId):id('offer',pid), keepId=immediate?(partner?undefined:id('company-search',episodeId)):id('keep',pid,o.holder), planId=id('plan',pid);
      const answered=['yes','available','no','not-now'].includes(o.kind);
      const target=keepId&&rows.has(keepId)&&!answered?keepId:rows.has(offerId)&&(answered||partner)?offerId:rows.has(planId)?planId:undefined;
      const personalKey=id(pid,o.holder,o.at,o.kind);
      const n=personalOutcomes.get(personalKey)||add('perceptions',{id:id('perception',source,i),...own(o.holder,o.at),who:owner(o.holder),label:outcomeLabels[o.kind]||({found:'Found the person sought','not-found':'Did not see them during the search',unavailable:'Saw that they were unavailable'})[o.kind]||o.kind,of:target,causes:ownChoice?[ownChoice.n.id]:[],perceived:{kind:o.kind,answer:['yes','no'].includes(o.kind)?o.kind:undefined,observed:outcomeLabels[o.kind]||({found:'Found the person sought','not-found':'Did not see them during the search',unavailable:'Saw that they were unavailable'})[o.kind]||o.kind,observedMinutes:o.observedMinutes,sawPartner:o.sawPartner,felt:o.felt},unknown:['not-seen-during-plan','whereabouts-missed','not-found'].includes(o.kind)?[{about:'Where they went and why',status:'unknown'}]:[]});
      // The managed game and evening consumer report the same personal completion.
      // Join those explicit episode receipts, retaining both learning causes behind one node.
      personalOutcomes.set(personalKey,n);
      if(source==='company'&&o.felt)n.perceived.activityFelt=o.felt;
      primaryPerceptions.add(n.id);
      if(o.kind==='whereabouts-missed'){observationById.set(id(o.holder,pid),n);const decision=rows.get(id('choice',`evening:expectation-miss:none:${o.holder}:${o.at}`));if(decision)rows.get(decision.opportunity).causes.push(n.id);}
      if(o.kind.startsWith('concern-')){const observation=observationById.get(id(o.holder,pid));if(observation){n.causes.push(observation.id);const decision=rows.get(id('choice',`evening:expectation-miss:none:${o.holder}:${observation.visibleAt}`));if(decision)n.causes.push(decision.id);}}
      const raw=source==='evening'?pid:o.episodeId;
      cause(o.holder,`${source}:${raw}:${o.kind}`,o.at,n.id);
      if(o.kind.startsWith('whereabouts-'))cause(o.holder,`evening:${pid}`,o.at,n.id);
      const thread=l.threads.find(t=>t.id===(immediate?id('company-thread',episodeId):id('thread',pid)));if(thread)thread.stages[0].ids.push(n.id);
    }
    // Learnings use the recorded before/after snapshots, never the final town or a new formula.
    for(const [i,m]of (r.memoryChanges||[]).entries()){
      if(m.at>=0&&m.causeId?.startsWith('act:')){
        const acquired=(r.perceptions||[]).find(p=>p.holder===m.holder&&'act:'+p.actId===m.causeId&&p.at<=m.at);
        const a=(m.addedAssociations||[]).find(a=>a.object.kind==='Activity');
        const n=add('perceptions',{id:id('experience',m.holder,i),...own(m.holder,m.at),who:owner(m.holder),label:a?`Remembered ${a.object.key==='PlayedGame'?'playing a game':a.object.key.replace(/([a-z])([A-Z])/g,'$1 $2').toLowerCase()}`:'Revised a remembered experience',perceived:{kind:'felt-account',observed:'A personal feeling was recorded for this encounter.',how:acquired?.how,basis:acquired?.basis},causes:[]});
        cause(m.holder,m.causeId,m.at,n.id);
      }
      const beforeA=new Map((m.before?.associations||[]).map(a=>[a.id,a]));
      for(const a of m.after?.associations||[]){
        const before=beforeA.get(a.id);if(before&&JSON.stringify(before)===JSON.stringify(a))continue;
        const n=add('records',{id:id('association',m.holder,a.id,i),...own(m.holder,m.at),type:'association',label:feeling(a),strength:a.amount,sign:a.amount<0?'-':'+',object:`${a.object.kind.toLowerCase()}:${a.object.key}`,scope:a.how});
        version(m.holder,a.id,m.at,n);defer(n,m.holder,m.at,[a.causeId]);
        if(a.seed){const reading=(history.readings||[]).find(h=>`association:${enc(h.id)}:${h.object.kind}:${enc(h.object.key)}`===a.id);if(reading)defer(n,m.holder,m.at,[reading.id]);}
        if(m.at>=0){const learn=add('learning',{id:id('feeling',m.holder,i,a.id),...own(m.holder,m.at),label:feeling(a),record:n.id,previous:before?{strength:before.amount}:null,new:{strength:a.amount},evidence:[],passive:m.reason==='decay',rule:{reason:m.reason}});defer(learn,m.holder,m.at,[m.causeId]);}
      }
      const beforeE=new Map((m.before?.expectations||[]).map(x=>[key(x.key),x]));
      for(const a of m.after?.expectations||[]){
        const k=key(a.key),before=beforeE.get(k);if(before&&JSON.stringify(before)===JSON.stringify(a))continue;
        const n=add('records',{id:id('expectation',m.holder,k,i),...own(m.holder,m.at),type:'expectation',label:expectation(a.key),initial:{asOf:clock(m.at),chance:a.chance},key:k});
        version(m.holder,'expectation:'+k,m.at,n);defer(n,m.holder,m.at,a.entries.map(x=>x.causeId));
        if(m.at>=0){const learn=add('learning',{id:id('learn',m.holder,i,k),...own(m.holder,m.at),label:expectation(a.key),record:n.id,previous:before?{chance:before.chance}:null,new:{chance:a.chance},evidence:[],passive:m.reason==='decay',rule:{reason:m.reason}});defer(learn,m.holder,m.at,[m.causeId]);}
      }
    }
    // Bind snapshot recall and citations after indexing all receipts, always at the choice's time.
    for(const job of pending.filter(x=>x.choice)){
      const {choice:c,node:n,recall}=job,d=c.decision,o=d.opportunity;
      for(const item of d.recall?.items||[]){
        let ref=lookup(o.holder,item.id,o.at);
        if(!ref){const rec=add('records',{id:id('recalled',o.id,item.id),...own(o.holder,o.at),type:'recalled',label:item.text||'Remembered experience',scope:item.how,qualifications:'Only this recall snapshot was recorded; earlier detail unavailable.'});defer(rec,o.holder,o.at,item.causes||[]);ref=rec.id;}
        recall.items.push({record:ref,matched:d.recall.cues,slot:item.kind});
      }
      for(const [i,a]of (d.alternatives||[]).entries())for(const [j,z]of (a.reasons||[]).entries()){
        n.alternatives[i].reasons[j].label=({warm:'Remembered warmth',cold:'Remembered unease','warm-recall':'Remembered company opens this option','context-only':'Came to mind; no direct weight',base:'Ordinary preference',boldness:'Willingness to act',intended:'Hoped-for benefit'}[z.code])||(z.code.startsWith('chance:')?'Expected outcome':z.code.replace(/-/g,' '));
        n.alternatives[i].reasons[j].cites=(z.cites||[]).map(ref=>lookup(o.holder,ref,o.at)).filter(Boolean);
      }
    }
    for(const job of pending.filter(x=>x.row)){
      const refs=job.refs.map(ref=>lookup(job.holder,ref,job.at)).filter(ref=>ref&&ref!==job.row.id);
      const field=job.row.record?'evidence':'causes';job.row[field]=[...new Set([...(job.row[field]||[]),...refs])];
    }
    // Each holder's learned rows join only their recorded outcome; no world-stage inference.
    for(const t of l.threads){const ids=new Set(t.stages[0].ids);for(const n of l.learning)if(n.evidence.some(x=>ids.has(x)))t.stages[0].ids.push(n.id);}
    // Keep the evening thread and the exact memories it uses; unrelated daily updates do not
    // flood this pilot timeline. Supporting experiences remain reachable through their citations.
    const links=n=>[n.fact,n.record,n.evidence,n.causes,n.origin,n.decision,n.of,n.plan,n.attempts,n.opportunity,n.recall,...(n.items||[]).map(x=>x.record),...(n.alternatives||[]).flatMap(a=>a.reasons.flatMap(z=>z.cites||[]))].flat(Infinity).filter(x=>typeof x==='string');
    const kept=new Set([...l.perceptions.filter(n=>n.id.startsWith('runtime:company-ended:')).map(n=>n.id),...primaryPerceptions,...['opportunities','recalls','decisions','attempts','plans','outcomes'].flatMap(t=>l[t].map(n=>n.id))]);
    for(const n of l.learning)if(n.evidence.some(x=>primaryPerceptions.has(x)))kept.add(n.id);
    const visit=id=>{for(const ref of links(rows.get(id)||{}))if(rows.has(ref)&&!kept.has(ref)){kept.add(ref);visit(ref);}};
    [...kept].forEach(visit);
    for(const n of l.learning)if(kept.has(n.record)){kept.add(n.id);visit(n.id);}
    for(const table of tables)l[table]=l[table].filter(n=>kept.has(n.id));
    l.holders=holders;
    // Stable confirmations remain available on demand; a changed or new estimate stays prominent.
    for(const n of l.perceptions.filter(n=>n.perceived?.kind==='whereabouts-found')){
      const learned=l.learning.filter(x=>x.evidence.includes(n.id));
      if(learned.length&&learned.every(x=>x.previous?.chance===x.new?.chance)){n.routine=true;for(const x of learned)x.routine=true;}
    }
    l.scope='Recorded evening plans, company choices and free-time destinations, with personal outcomes and supporting memories. A destination choice does not by itself prove arrival. Other town actions without these receipts are not included.';
    return l;
  }
  return {adapt};
});
