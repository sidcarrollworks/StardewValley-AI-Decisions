/* Pure authoring operations. Canon input is never mutated in place. */
(function(root){
 const clone=x=>JSON.parse(JSON.stringify(x));
 function save(pack, existingId, input, id, date){
  const p=clone(pack), old=p.accounts.find(a=>a.id===existingId), accountId=old?.id||id+'-account', sourceId=id+'-source', factId=id+'-fact';
  if(!input.holder?.trim()||!input.text?.trim())throw Error('Choose a character and write their account.');
  if(input.how==='Told'&&!input.teller?.trim())throw Error('Name who told them.');
  if(input.kind==='Past'&&input.readings.some(r=>r.stance!=='None'))throw Error('A finished past is recall-only. Add a separate present situation for current feelings.');
  const cues=[...new Set(input.cues.map(s=>s.trim()).filter(Boolean))].map(key=>({key,sourceId:null}));
  p.sources.push({id:sourceId,key:input.provenance,kind:'Authored'});p.facts.push({id:factId,kind:input.kind,sources:[sourceId]});
  p.accounts=p.accounts.filter(a=>a.id!==accountId);p.readings=p.readings.filter(r=>r.accountId!==accountId);
  p.accounts.push({id:accountId,factId,holder:input.holder,text:input.text.trim(),how:input.how,teller:input.how==='Told'?input.teller.trim():null,sources:[sourceId],cues});
  input.readings.forEach((r,i)=>{if(!r.text?.trim()||!r.object.key?.trim())throw Error('Every feeling needs an object and their own words.');if(r.stance==='Expects'&&(!r.expectation?.subject?.trim()||!r.expectation?.context?.trim()||typeof r.expectedSuccess!=='boolean'))throw Error('An expectation needs its person, context, and expected outcome.');p.readings.push({id:id+'-reading-'+i,accountId,object:{kind:r.object.kind,key:r.object.key.trim()},stance:r.stance,text:r.text.trim(),origin:'ExperimentalStated',admission:null,review:{status:'Reviewed',by:'Studio author',date},sources:[sourceId],cues:clone(cues),presentLinkSource:null,expectation:r.stance==='Expects'?clone(r.expectation):null,expectedSuccess:r.stance==='Expects'?r.expectedSuccess:null});});
  return p;
 }
 function remove(pack,id){const p=clone(pack);p.accounts=p.accounts.filter(a=>a.id!==id);p.readings=p.readings.filter(r=>r.accountId!==id);return p;}
 function parse(text){const p=JSON.parse(text);if(p.version!==1||!['sources','facts','accounts','readings'].every(k=>Array.isArray(p[k])))throw Error('Choose a version 1 history pack, not an inspiration profile.');return p;}
 root.HistoryEditor={save,remove,parse};if(typeof module!=='undefined')module.exports=root.HistoryEditor;
})(typeof window!=='undefined'?window:globalThis);
