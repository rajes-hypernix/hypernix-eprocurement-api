const fs=require("fs");const {JSDOM}=require("jsdom");
const w=new JSDOM(fs.readFileSync(""+require("path").join(__dirname,"..","eprocure-portal.html")+"","utf8"),{runScripts:"dangerously"}).window;
const D=w.document;const main=()=>D.getElementById("main").innerHTML;
let ok=0,n=0;const A=(l,c)=>{n++;c&&ok++;console.log((c?"  \u2713 ":"  \u2717 ")+l);};

// ===== 1) PR/RFQ -> PO conversion (award generates a real PO) =====
w.switchRole("buyer");
let awarded=null;
["RFQ-2026-0079","RFQ-2026-0083","RFQ-2026-0087","RFQ-2026-0074"].forEach(id=>{if(awarded)return;w.awardFlow(id);if(main().includes("setAllocVendor"))awarded=id;});
A("found an awardable RFQ (submitted bids)", !!awarded);
if(awarded){
  w.confirmAward(awarded);
  const modal=D.getElementById("modal").innerHTML;
  const m=modal.match(/PO-2026-13\d{2}/);
  A("award generated a real PO id", !!m);
  if(m){const po=w.poById(m[0]);
    A("generated PO: status Draft, rfqId set, vendor set", !!po&&po.status==="Draft"&&po.rfqId===awarded&&!!po.vendorId);
    A("generated PO lines carry awarded qty + price", !!po&&po.lines.length>0&&po.lines.every(l=>l.qty>0&&l.price>0));
    A("award->PO audited", w.auditFor(m[0]).some(a=>/Generated PO from award/.test(a.action)));
  }
}
w.closeModal&&w.closeModal();

// ===== 2) ASN over-ship is clamped to remaining =====
w.switchVendorUser("klind");w.poAck("PO-2026-1190");
w.openASNNew("PO-2026-1190");
const setQ=[...D.querySelectorAll("input")].find(i=>/asnSetQty/.test(i.getAttribute("oninput")||""));
w.asnSetQty("MRO-LOT-01","99"); // try to over-ship (PO qty is 1)
w.asnSubmit();
const klAsn=w.asnsForVendor("klind").find(a=>a.status==="In transit");
A("ASN over-ship clamped to remaining (1, not 99)", !!klAsn && klAsn.lines[0].shippedQty===1);

// ===== 3) duplicate ASN blocked once line fully committed =====
w.openASNNew("PO-2026-1190");
A("duplicate ASN blocked (nothing left to ship)", main().includes("Nothing left to ship"));

// ===== 4) GRN over-receipt capped to shipped/outstanding =====
w.switchRole("buyer");w.openReceive(klAsn.id);
w.grnSetQty("MRO-LOT-01","99"); // try to over-receive (shipped=1)
w.grnPost(klAsn.id);
A("GRN over-receipt capped (recv=1, not 99)", w.poById("PO-2026-1190").lines[0].recv===1);

// ===== 5) GRN under-receipt frees the shortfall for re-shipment =====
// PO-1186: recv16, ASN-0511 in transit 8 -> receive only 5 (short)
const po86=w.poById("PO-2026-1186");
w.openReceive("ASN-2026-0511");
w.grnSetQty("VLV-GAT-150","5");
w.grnPost("ASN-2026-0511");
A("under-receipt recorded (recv = 16 + 5 = 21)", w.poRecvQty(po86)===21);
A("shortfall (3) freed for re-shipment", w.poRemainingToShip(po86,"VLV-GAT-150")===3);

// ===== 6) invoice over-bill capped to billable (received - already invoiced) =====
w.switchVendorUser("pantai");
w.openInvNew("PO-2026-1186");
w.invSetQty("VLV-GAT-150","999"); // billable is 21
w.invSubmit();
const pInv=w.invForVendor("pantai").slice(-1)[0];
A("invoice over-bill capped to billable (21, not 999)", !!pInv && pInv.lines[0].qty===21);

console.log("SAFETY "+ok+"/"+n);
if(ok!==n)process.exit(1);
