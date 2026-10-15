using System.Text.Json;

/// <summary>A view of recorded results, never a second implementation of the choice rule.</summary>
internal static class RoutingTrialReport
{
    public static string Render(Dictionary<string, object?> recording, JsonElement summary)
    {
        var routing = (JsonElement)recording["livesRouting"]!;
        var payload = new
        {
            summary,
            history = routing.GetProperty("history"),
            choices = routing.GetProperty("decisions").EnumerateArray()
                .Where(c => c.GetProperty("comparison").ValueKind == JsonValueKind.Object).ToArray(),
        };
        // A source quote must never terminate the embedded data script.
        string data = JsonSerializer.Serialize(payload).Replace("<", "\\u003c")
            .Replace(">", "\\u003e").Replace("&", "\\u0026");
        return Template.Replace("__RECORDED_DATA__", data);
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Under Glass · History and choices</title>
<style>
:root{color-scheme:light;--bg:#e8e3d9;--panel:#f2eee6;--ink:#1d1a17;--muted:#655e53;--line:#bab2a4;--accent:#e0520c;--warm:#2a55c4}
@media(prefers-color-scheme:dark){:root{color-scheme:dark;--bg:#1d1b18;--panel:#28251f;--ink:#eee8dd;--muted:#b6ac9c;--line:#51493d;--warm:#8ba8f8}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.5 system-ui,sans-serif}
main{max-width:1080px;margin:auto;padding:28px 24px 64px}h1{font-size:28px;margin:6px 0}h2{font-size:21px;margin:0 0 12px}h3{font-size:17px;margin:0}
p{margin:8px 0 16px}.eyebrow,.muted,small{color:var(--muted)}.eyebrow{text-transform:uppercase;font:12px ui-monospace,monospace;letter-spacing:.1em}
.metrics{display:flex;flex-wrap:wrap;gap:12px;margin:24px 0}.metric{flex:1;min-width:150px;border:1px solid var(--line);border-radius:10px;padding:14px 18px}.metric strong{display:block;font-size:27px}
button,select{font:inherit;color:inherit;background:var(--panel);border:1px solid var(--line);border-radius:7px;padding:8px 12px;box-shadow:none}button{cursor:pointer}button:disabled{opacity:.4;cursor:default}button:focus-visible,select:focus-visible,summary:focus-visible{outline:2px solid var(--accent);outline-offset:3px}
.controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:22px 0 12px}.controls label{display:flex;gap:8px;align-items:center}.controls select{max-width:100%}
.panel{border:1px solid var(--line);border-radius:12px;background:var(--panel);padding:22px;margin:14px 0}.result{display:flex;align-items:center;gap:16px;flex-wrap:wrap}.result div{flex:1;min-width:180px}.result strong{display:block;font-size:22px}.changed{color:var(--accent)}.result .arrow{flex:0;color:var(--muted);font-size:24px;min-width:auto}
.table{width:100%;border-collapse:collapse;margin-top:20px}.table th{text-align:left;font-weight:500;color:var(--muted)}.table td,.table th{padding:12px 10px;border-bottom:1px solid var(--line)}.table td:first-child,.table th:first-child{padding-left:0}.bar{height:5px;max-width:180px;background:var(--line);margin-top:7px;border-radius:3px;overflow:hidden}.bar span{display:block;background:var(--warm);height:100%}.number{font-variant-numeric:tabular-nums}
blockquote{margin:12px 0;font-size:19px;border-left:3px solid var(--warm);padding-left:16px}summary{cursor:pointer;color:var(--muted);padding:10px 0}details{margin-top:14px}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:12px;max-height:450px;overflow:auto;padding:12px;border:1px solid var(--line)}a{color:var(--warm)}.links{display:flex;gap:18px;flex-wrap:wrap}.note{font-size:14px;color:var(--muted)}
@media(max-width:600px){main{padding:20px 14px}.panel{padding:16px}.table td,.table th{padding:10px 5px;font-size:13px}.controls select{width:100%}.result .arrow{display:none}}
</style>
<main>
  <div class="eyebrow">Under Glass / offline routing trial</div>
  <h1>Does a memory change the choice?</h1>
  <p class="muted">A synthetic Ann and Bo town. These are actual simulation results; no model was called.</p>
  <div class="metrics" id="metrics"></div>
  <div class="controls">
    <button id="prev" aria-label="Previous comparison">←</button>
    <select id="choices" aria-label="Choose a recorded decision"></select>
    <button id="next" aria-label="Next comparison">→</button>
    <label><input type="checkbox" id="changedOnly" checked> Changed choices only</label>
  </div>
  <section class="panel" aria-live="polite">
    <div class="eyebrow" id="moment"></div>
    <h2 id="heading"></h2>
    <div class="result">
      <div><small>With the memory · actual choice</small><strong id="actual"></strong></div>
      <span class="arrow" aria-hidden="true">↔</span>
      <div><small>Without the reading · same draw</small><strong id="counterfactual"></strong></div>
    </div>
    <p class="note" id="verdict"></p>
    <table class="table"><thead><tr><th>Destination</th><th>With memory</th><th>Without reading</th><th>Difference</th></tr></thead><tbody id="probabilities"></tbody></table>
    <details><summary>What was held fixed?</summary><p class="note">The available options and their order, current state, retained lived experience and random draw. Only the specified starting-history reading and its dependent seed are removed. This is one isolated choice; it does not simulate an alternate future.</p><p class="note" id="draw"></p></details>
    <details><summary>Inspect the recorded reasons</summary><pre id="receipt"></pre></details>
  </section>
  <section class="panel">
    <h2>The starting memory</h2>
    <blockquote id="memory"></blockquote>
    <p class="note">The account of spending time at the Hall remains in both versions. The comparison removes Ann's warm reading of it. This invented history tests the mechanism; it is not Stardew canon.</p>
    <details><summary>History and admission evidence</summary><pre id="history"></pre></details>
  </section>
  <details><summary>Open the whole recordings</summary>
    <p class="note">These three whole runs can diverge after the first different choice. Compare their movement in Run Viewer; use the matched choices above to isolate the effect of the memory. The existing Life chains page does not yet read these routing receipts.</p>
    <div class="links" id="runs"></div>
    <p class="note"><a href="summary.json">Summary JSON</a> · <a href="history.json">History input</a></p>
  </details>
  <p class="note">This increment changes existing free-time destinations. Seeking company, shared plans and learning from their outcomes are still to come.</p>
  <noscript>Enable JavaScript to inspect comparisons, or open summary.json and with-history.json in this folder.</noscript>
</main>
<script type="application/json" id="data">__RECORDED_DATA__</script>
<script>
'use strict';
const data=JSON.parse(document.getElementById('data').textContent), $=id=>document.getElementById(id), summary=data.summary.comparisons;
const el=(tag,text,cls)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n};
const pct=n=>(n*100).toFixed(1)+'%', time=m=>'Day '+(Math.floor(m/1440)+1)+' · '+String(Math.floor(m%1440/60)).padStart(2,'0')+':'+String(m%60).padStart(2,'0');
const label=(decision,id)=>decision.alternatives.find(a=>a.option.id===id)?.option.objects.find(o=>o.kind==='Place')?.key||id||'Existing routine draw';
for(const [number,text] of [[summary.comparable,'matched choices'],[summary.probabilitiesChanged,'changed odds'],[summary.selectedActionChanged,'different destinations'],[data.summary.modelCalls,'model calls']]){
  const box=el('div',undefined,'metric');box.append(el('strong',String(number)),el('small',text));$('metrics').append(box);
}
$('memory').textContent=data.history.readings[0]?.text||'No starting reading';$('history').textContent=JSON.stringify(data.history,null,2);
let rows=[],index=0;
function render(){
  const row=rows[index];$('prev').disabled=index<=0;$('next').disabled=index>=rows.length-1;$('choices').value=String(index);
  if(!row){$('heading').textContent='No comparisons match this filter';return;}
  const pair=row.comparison,before=pair.withHistory,after=pair.withoutHistory;
  $('moment').textContent=time(before.opportunity.at);$('heading').textContent=before.opportunity.holder+' chooses where to spend free time';
  $('actual').textContent=row.selectedPlace;$('counterfactual').textContent=label(after,after.selectedId);
  $('counterfactual').className=pair.selectionChanged?'changed':'';
  $('verdict').textContent=!pair.comparable?'This choice is not comparable: '+pair.nonComparableReason:pair.selectionChanged?'The same draw selects a different destination.':'The odds changed; this draw still selects the same destination.';
  $('probabilities').replaceChildren();
  for(const a of before.alternatives){
    const b=after.alternatives.find(x=>x.option.id===a.option.id),tr=el('tr');tr.append(el('td',label(before,a.option.id)));
    for(const p of [a.probability,b?.probability]){const td=el('td',p===undefined?'—':pct(p),'number');if(p!==undefined){const bar=el('div',undefined,'bar'),fill=el('span');fill.style.width=(p*100)+'%';bar.append(fill);td.append(bar)}tr.append(td)}
    const difference=b?(a.probability-b.probability)*100:null;tr.append(el('td',difference===null?'—':(difference>0?'+':'')+difference.toFixed(1)+' pp',difference?'number changed':'number'));$('probabilities').append(tr);
  }
  $('draw').textContent='Recorded random draw: '+before.unit+' · Removed: '+pair.removedHistoryIds.join(', ');
  $('receipt').textContent=JSON.stringify(row,null,2);
}
function filter(){rows=data.choices.filter(r=>!$('changedOnly').checked||r.comparison.selectionChanged);index=0;$('choices').replaceChildren();rows.forEach((r,i)=>{const option=el('option',time(r.decision.opportunity.at)+(r.comparison.selectionChanged?' · changed':' · same selection'));option.value=String(i);$('choices').append(option)});render()}
$('changedOnly').addEventListener('change',filter);$('choices').addEventListener('change',()=>{index=Number($('choices').value);render()});$('prev').addEventListener('click',()=>{if(index>0){index--;render()}});$('next').addEventListener('click',()=>{if(index<rows.length-1){index++;render()}});
for(const [file,title] of [['with-history.json','With history'],['without-reading.json','Without the reading'],['routing-off.json','Routing off']]){
 const a=el('a',title);a.href=location.protocol==='file:'?file:'/sim/viewer/index.html?run='+encodeURIComponent(new URL(file,location.href).href);$('runs').append(a);
}
if(!data.choices.some(r=>r.comparison.selectionChanged))$('changedOnly').checked=false;filter();
</script>
</html>
""";
}
