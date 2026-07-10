const fs=require("fs");const {JSDOM}=require("jsdom");
const w=new JSDOM(fs.readFileSync(""+require("path").join(__dirname,"..","eprocure-portal.html")+"","utf8"),{runScripts:"dangerously"}).window;
const D=w.document;const main=()=>D.getElementById("main").innerHTML;
let ok=0,n=0;const A=(l,c)=>{n++;c&&ok++;console.log((c?"  \u2713 ":"  \u2717 FAIL ")+l);};
const fillReq=()=>{[...new Set([...main().matchAll(/vbSet\('([^']+)'/g)].map(x=>x[1]))].forEach(id=>{if(!id.endsWith('_exp'))w.vbSet(id,"x");});[...new Set([...main().matchAll(/vbYesNo\('([^']+)'/g)].map(x=>x[1]))].forEach(id=>w.vbYesNo(id,"yes"));[...new Set([...main().matchAll(/vbTerms\('([^']+)'/g)].map(x=>x[1]))].forEach(id=>w.vbTerms(id,true));};

// ===== FIX 1: technically-failed vendor NOT awardable (dual + finalized) =====
const RID="RFQ-2026-0083";w.switchRole("buyer");
w.demoCloseBids(RID);w.openBidsFlow(RID);w.tryOpen&&w.tryOpen(RID,"tech");
const s=D.getElementById("evWho");if(s)s.value="haf";w.doOpenTech&&w.doOpenTech(RID);
const vids=[...new Set([...main().matchAll(/setScore\('[^']+','([^']+)','[^']+','compliance'/g)].map(x=>x[1]))];
["haf","nur"].forEach(ev=>vids.forEach((v,i)=>["compliance","experience","delivery","qa"].forEach(c=>w.setScore(RID,v,ev,c,i===0?95:30))));
w.finalizeTech(RID);
const failed=vids[1];
w.awardFlow(RID);
const optVids=[...new Set([...main().matchAll(/<option value="([a-z]+)"/g)].map(x=>x[1]))];
A("failed vendor ("+failed+") NOT selectable for award", !optVids.includes(failed));
A("passed vendor ("+vids[0]+") still selectable", optVids.includes(vids[0]));
A("default allocation excludes failed vendor", ![...main().matchAll(/<option value="([a-z]+)"[^>]*selected/g)].map(x=>x[1]).includes(failed));
w.confirmAward(RID);
const pid=(D.getElementById("modal").innerHTML.match(/PO-2026-13\d{2}/)||[])[0];
A("award still generates a PO for a passed vendor", !!pid && !!w.poById(pid) && w.poById(pid).vendorId!==failed);
w.closeModal&&w.closeModal();

// ===== CONTROL: submission on an OPEN rfq still works =====
w.switchVendorUser("pantai");w.venOpenBid("RFQ-2026-0087");
const c1=(main().match(/vbPrice\('([A-Z0-9-]+)'/)||[])[1];if(c1)w.vbPrice(c1,"700");fillReq();
w.vbSubmit("RFQ-2026-0087");
A("control: submit on OPEN rfq succeeds", D.getElementById("overlay").innerHTML.includes("Bid Submitted"));
w.closeModal&&w.closeModal();

// ===== FIX 2: cannot submit after bids close =====
w.switchRole("buyer");w.demoCloseBids("RFQ-2026-0087");
w.switchVendorUser("klind");w.venOpenBid("RFQ-2026-0087");
const c2=(main().match(/vbPrice\('([A-Z0-9-]+)'/)||[])[1];if(c2)w.vbPrice(c2,"500");fillReq();
w.vbSubmit("RFQ-2026-0087");
A("submit after close is BLOCKED (no modal shown)", !D.getElementById("overlay").classList.contains("show"));
A("klind bid not marked submitted after close", w.curBid && w.curBid().submitted!==true);

console.log("\nFIXES "+ok+"/"+n);
if(ok!==n)process.exit(1);
