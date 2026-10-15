/* Live generation controls are distinct from finished-recording playback. */
window.UnderGlassLive = function(jobId,page,nav) {
 const el=(tag,text)=>{const n=document.createElement(tag);if(text)n.textContent=text;return n;};
 const bar=el('div');bar.style.cssText='display:flex;flex-wrap:wrap;gap:12px;align-items:center;width:100%';
 const status=el('span');status.setAttribute('role','status');const pause=el('button','Pause run'),rate=el('select');rate.setAttribute('aria-label','Generation pace');
 for(const [v,l] of [[10,'10 min/sec'],[60,'1 hour/sec'],[120,'2 hours/sec'],[1440,'1 day/sec'],[0,'As fast as possible']])rate.add(new Option(l,v));
 const follow=el('input');follow.type='checkbox';follow.checked=true;const label=el('label');label.append(follow,' Follow latest');bar.append(status,pause,rate,label);nav.append(bar);
 let token,paused=false,ready=false,editing=false,desiredPause=null,seenPaused=false;
 async function control(want){try{editing=true;const r=await fetch('/api/run-studio/jobs/'+jobId+'/control',{method:'POST',headers:{'Content-Type':'application/json','X-Run-Studio-Token':token},body:JSON.stringify({paused:want,rate:Number(rate.value)})});if(!r.ok)throw Error((await r.json()).error);desiredPause=want;paused=want;follow.checked=!paused;pause.textContent=paused?'Resume run':'Pause run';}catch(e){status.textContent=e.message;}finally{editing=false;}}
 pause.onclick=()=>control(!paused);rate.onchange=()=>control(paused);
 for(const id of ['scrub','time'])document.getElementById(id)?.addEventListener('input',()=>follow.checked=false);
 for(const id of ['strip','play','previous','next','previousMoment','nextMoment','backDay','backHour','fwdHour','fwdDay','event-timeline','moment-events','graph'])document.getElementById(id)?.addEventListener('click',()=>follow.checked=false);

 (async function poll(){let done=false;try{
  if(!token)token=(await(await fetch('/api/run-studio',{cache:'no-store'})).json()).token;
  const response=await fetch('/api/run-studio/jobs/'+jobId+'/live',{cache:'no-store'});if(!response.ok)throw Error('Live run unavailable.');
  const {job,state}=await response.json();done=!['running','cancelling'].includes(job.status);
  if(desiredPause===!!state.paused)desiredPause=null;
  if(!editing){paused=desiredPause??!!state.paused;rate.value=String(state.rate??job.rate??60);pause.textContent=paused?'Resume run':'Pause run';}
  pause.disabled=rate.disabled=done;
  status.textContent=done?'Run '+job.status:desiredPause===true?'Pausing after the current model answer…':({reflection:'Waiting for private reflection', 'evening-choice':'Waiting for evening choice',paused:'Simulation paused',starting:'Starting simulation',running:'Live simulation'})[state.phase]||state.phase;
  const data=await fetch(job.live,{cache:'no-store'});if(data.ok){const run=await data.json();if(!ready){await page.load(run,new URL(job.live,location.href).href,{...UnderGlassTools.readState(location.hash),t:run.live?.at??state.at});ready=true;}else if(follow.checked||state.paused&&!seenPaused)await page.update(run,new URL(job.live,location.href).href,true);}
  seenPaused=!!state.paused;window.dispatchEvent(new Event("under-glass-recording"));
  if(done){const a=el('a',job.status==='succeeded'?'Open finished recording →':'Keep this partial recording');a.href=UnderGlassTools.toolUrl(page.page,job.status==='succeeded'?(job.outputs.json||job.outputs.html):job.live,page.state?.(),location.href);bar.append(a);}
 }catch(e){status.textContent=e.message;}if(!done)setTimeout(poll,1000);})();
};
