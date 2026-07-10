const fs=require("fs");const {JSDOM,VirtualConsole}=require("jsdom");
const errs=[];const vc=new VirtualConsole();vc.on("jsdomError",e=>errs.push("jsdomError: "+e.message));
const w=new JSDOM(fs.readFileSync(""+require("path").join(__dirname,"..","eprocure-portal.html")+"","utf8"),{runScripts:"dangerously",virtualConsole:vc}).window;
const D=w.document;const main=()=>D.getElementById("main").innerHTML;const side=()=>D.getElementById("side").innerHTML;
const who=()=>D.getElementById("whoami").innerHTML;const dock=()=>D.getElementById("chatDock").innerHTML;const ov=()=>D.getElementById("overlay").innerHTML;
let pass=0,fail=0;const ok=(l,c)=>{c?pass++:fail++;console.log((c?"  ✓ ":"  ✗ FAIL ")+l);};
const step=s=>console.log("\n=== "+s+" ===");
const T=(l,fn)=>{try{return fn();}catch(e){fail++;console.log("  ✗ THREW "+l+": "+e.message);}};
const asRole=r=>w.switchRole(r);const asVendor=v=>w.switchVendorUser(v);

// ===== A · Identity & role switching =====
step("A · Identity hover menu + role switching");
ok("profile menu rendered (no slider)", who().includes("prof-menu")&&!D.getElementById("roleswitch"));
ok("menu lists all 4 proc roles", who().includes("Buyer")&&who().includes("Technical Evaluator")&&who().includes("Commercial Evaluator")&&who().includes("Admin"));
ok("menu lists assigned vendors", who().includes("Vendors assigned to me")&&(who().includes("Sentausa")||who().includes("Pantai")));
T("buyer",()=>asRole("buyer"));ok("Buyer → Sourcing nav + Dashboard", side().includes("Sourcing")&&main().length>200);
T("tech",()=>asRole("tech"));ok("Technical Evaluator → Evaluation nav + RFQs", side().includes("Technical Evaluator")&&main().includes("RFQ"));
T("comm",()=>asRole("comm"));ok("Commercial Evaluator nav", side().includes("Commercial Evaluator"));
T("admin",()=>asRole("admin"));ok("Admin → User Management screen", side().includes("Administration")&&main().includes("User Management"));
T("vend",()=>asVendor("pantai"));ok("Vendor context (company nav)", main().includes("RFQ Invitations"));
ok("profile shows active vendor label", who().includes("Vendor"));
T("blocked",()=>asVendor("megatech"));ok("cannot act as unassigned vendor (stays Pantai)", who().includes("Pantai")&&!who().includes("MegaTech Tenders"));

// ===== B · Admin user management =====
step("B · Admin user management (provisioning)");
asRole("admin");
ok("lists seeded users", main().includes("Ir. Hafiz Rahman")&&main().includes("Tan Mei Ling"));
T("edit",()=>w.adminEdit("u_hafiz"));
ok("edit modal open", ov().includes("Edit User"));
T("addrole",()=>{w.adminToggleRole("comm");w.adminSaveUser();});
{const rows=main().split(/<tr/);const hr=rows.find(r=>r.includes("Ir. Hafiz Rahman"));ok("Hafiz now Tech + Comm", hr&&hr.includes("Technical Evaluator")&&hr.includes("Commercial Evaluator"));}
T("newuser",()=>{w.adminEdit();D.getElementById("euName").value="Zara QA";w.adminToggleRole("tech");w.adminSaveUser();});
ok("new user added", main().includes("Zara QA"));

// ===== C · Buyer: requisition → RFQ draft → release =====
step("C · Buyer RFQ lifecycle (draft + release gate)");
asRole("buyer");
T("draft",()=>{w.goProc("reqs");w.reqGroupChk("PR-2026-0412",true);w.reqOpenDrawer();w.reqCreateRFQ();w.bNext();const ti=D.getElementById("bTitle");ti.value="ZZ E2E RFQ";ti.oninput({target:{value:"ZZ E2E RFQ"}});w.bToggleV("sentausa");w.bToggleV("pantai");});
ok("Save as draft + Release present", main().includes("Save as draft"));
T("save",()=>w.bSaveDraft());
T("list",()=>w.goProc("rfqs"));
let DID=null;{for(const r of main().split(/<tr/))if(r.includes("ZZ E2E RFQ")){const m=r.match(/bEditDraft\('([^']+)'\)/);if(m)DID=m[1];}}
ok("draft saved + listed", !!DID&&main().includes(">Draft<"));
T("release",()=>{w.bEditDraft(DID);w.bConfirmRelease();w.bSend();});
ok("released to open", main().includes("ZZ E2E RFQ"));

// ===== D · Form builder + selection helpers =====
step("D · Form builder + vendor selection");
T("b2",()=>{w.goProc("reqs");w.reqGroupChk("PR-2026-0415",true);w.reqOpenDrawer();w.reqCreateRFQ();w.bNext();w.bNext();});
const n0=w.qForm().items.length;
T("ins",()=>{w.qInsert("end::commercial::-1","question","money");w.qInsert("end::technical::-1","question","short_text");});
ok("two questions inserted", w.qForm().items.length===n0+2);
const li=w.qForm().items[w.qForm().items.length-1];
T("move",()=>w.qMoveTo(li.id,"commercial","",null));
ok("drag-move changed group", w.qForm().items.find(x=>x.id===li.id).group==="commercial");
T("toV",()=>w.bNext());
T("selall",()=>w.selAllShown());ok("select-all", (main().match(/vpick on/g)||[]).length>0);
T("clr",()=>w.selClearInvited());ok("unselect-all", (main().match(/vpick on/g)||[]).length===0);
T("col",()=>w.selToggleFilters());ok("collapse filters", main().includes("Show filters"));

// ===== E · Two-envelope scoring =====
step("E · Technical + commercial scoring");
const RID="RFQ-2026-0083";
T("close",()=>w.demoCloseBids(RID));
T("open",()=>w.openBidsFlow(RID));
T("tech",()=>{w.tryOpen(RID,"tech");const s=D.getElementById("evWho");if(s)s.value="haf";w.doOpenTech(RID);});
ok("scoring screen", main().includes("Scoring Matrix"));
const vids=[...new Set([...main().matchAll(/setScore\('[^']+','([^']+)','[^']+','compliance'/g)].map(x=>x[1]))];
T("score",()=>{["haf","nur"].forEach(ev=>vids.forEach((v,i)=>{const b=i===0?[92,90,88,90]:[55,52,50,54];w.setScore(RID,v,ev,"compliance",b[0]);w.setScore(RID,v,ev,"experience",b[1]);w.setScore(RID,v,ev,"delivery",b[2]);w.setScore(RID,v,ev,"qa",b[3]);}));});
T("fin",()=>w.finalizeTech(RID));ok("PASS/FAIL", main().includes("PASS")&&main().includes("FAIL"));
T("comm",()=>{w.tryOpen(RID,"comm");w.openRFQ(RID);});
ok("commercial + combined ranking", main().includes("Commercial Comparison")&&main().includes("Combined Evaluation")&&main().includes("Recommended"));

// ===== F · Tech-evaluator role can score =====
step("F · Technical Evaluator role reaches scoring");
T("teval",()=>{asRole("tech");w.openRFQ(RID);});
ok("evaluator can view evaluation", main().includes("Technical Evaluation")||main().includes("Commercial Comparison"));

// ===== G · Vendor: bid draft + submit + price focus =====
step("G · Vendor bid draft, submit, price focus-fix");
asVendor("pantai");
T("bid",()=>w.venOpenBid("RFQ-2026-0087"));
ok("Save draft available", main().includes("Save draft"));
const lc=[...main().matchAll(/vbPrice\(.([A-Z0-9-]+)./g)][0][1];
const node=D.querySelector(`input[oninput*="vbPrice('${lc}'"]`);node.setAttribute("data-m","x");
T("price",()=>w.vbPrice(lc,"650"));
ok("price input survives keystroke", D.querySelector(`input[oninput*="vbPrice('${lc}'"]`).getAttribute("data-m")==="x");
ok("line total patched", /RM/.test((D.getElementById("lt_"+lc)||{}).innerHTML||""));
T("sd",()=>w.vbSaveDraft("RFQ-2026-0087"));ok("draft saved state", main().includes("Draft saved"));
T("fill",()=>{[...new Set([...main().matchAll(/vbSet\('([^']+)'/g)].map(x=>x[1]))].forEach(id=>{if(!id.endsWith('_exp'))w.vbSet(id,"Confirmed.");});[...new Set([...main().matchAll(/vbYesNo\('([^']+)'/g)].map(x=>x[1]))].forEach(id=>w.vbYesNo(id,'yes'));[...new Set([...main().matchAll(/vbTerms\('([^']+)'/g)].map(x=>x[1]))].forEach(id=>w.vbTerms(id,true));});
T("sub",()=>w.vbSubmit("RFQ-2026-0087"));ok("submitted", ov().includes("Bid Submitted"));T("cl",()=>w.closeModal());

// ===== H · Clarifications: scopes, attachments, broadcast, cross-role =====
step("H · Clarifications end-to-end");
asRole("buyer");
T("chats",()=>w.goProc("chats"));
ok("chats two-pane + scopes", main().includes("cp-wrap")&&main().includes("General inquiry")&&main().includes("RFQ-2026-0087"));
T("self",()=>w.chatSelect("RFQ-2026-0087|pantai"));
ok("broadcast toggle (rfq)", main().includes("Share answer with all bidders"));
T("bc",()=>{D.getElementById("ci_RFQ_2026_0087_pantai").value="E2E broadcast answer.";D.getElementById("cb_RFQ_2026_0087_pantai").checked=true;w.chatSend("RFQ-2026-0087","pantai");});
T("k",()=>w.chatSelect("RFQ-2026-0087|klind"));
ok("broadcast reached klind (published)", main().includes("E2E broadcast answer.")&&main().includes("Published to all bidders"));
T("g",()=>w.chatSelect("general|pantai"));ok("no broadcast on general", !main().includes("Share answer with all bidders"));
T("att",()=>{w.chatStage("general","pantai","Doc.pdf");D.getElementById("ci_general_pantai").value="file";w.chatSend("general","pantai");});
ok("attachment posted", main().includes("Doc.pdf"));
w.goProc("rfqs");asVendor("klind");T("vc",()=>w.goVendor("chats"));
ok("vendor sees broadcast", main().includes("E2E broadcast answer."));
T("vask",()=>{w.chatSelect("RFQ-2026-0087|klind");D.getElementById("ci_RFQ_2026_0087_klind").value="vendor q";w.chatSend("RFQ-2026-0087","klind");});
asRole("buyer");const u1=w.clarThreads().reduce((n,x)=>n+x.unread,0);
ok("proc sees vendor unread", u1>0);
T("read",()=>{w.goProc("chats");w.chatSelect("RFQ-2026-0087|klind");});
ok("reading reduces unread", w.clarThreads().reduce((n,x)=>n+x.unread,0)<u1);

// ===== I · Full screen sweep per role =====
step("I · Screen sweep across roles");
asRole("buyer");["dashboard","reqs","rfqs","awards","vendors","forms","chats"].forEach(s=>{T("b:"+s,()=>w.goProc(s));ok("buyer/"+s,main().length>200);});
asRole("admin");T("a:admin",()=>w.goProc("admin"));ok("admin/users",main().includes("User Management"));
asVendor("sentausa");["dashboard","bids","chats"].forEach(s=>{T("v:"+s,()=>w.goVendor(s));ok("vendor/"+s,main().length>200);});

console.log("\n========================================");
console.log("E2E TOTAL  PASS:",pass,"  FAIL:",fail);
console.log("RUNTIME ERRORS:",errs.length?errs:"NONE");
console.log("========================================");
