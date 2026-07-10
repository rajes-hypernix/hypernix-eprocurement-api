# SEED-DATA.md — full demo seed (no NetSuite integration)

There is **no NetSuite integration**. Seed ALL of this locally in a Development
`DataSeeder` (idempotent; runs after migrations in Development only). The aim: **every
screen in the app is populated and every flow is walkable for a live client demo** —
matching the prototype. In production these would come from NetSuite; here they are dummy.

> Principle: open each screen in `prototype/eprocure-portal.html`, and make sure the
> seeded data makes the equivalent .NET screen look just as full. **Payment Vouchers stay
> blank.** NetSuite client is a stub.

---

## 1. Reference / master data

- **Currency:** MYR (RM). **UOMs:** Unit, Set, m, kg, Lot.
- **Cost centres:** CC-MAINT-01 (Maintenance), CC-OPS-02 (Operations),
  CC-PROJ-07 (Projects), CC-HSE-03 (HSE).
- **Projects:** TA-2026 Turnaround, Facilities Upgrade, HSE Refresh.

### SWEC category tree (dummy — drives vendor categories + sourcing picker)

Hierarchy: Discipline → Category → Sub-category (leaf code). Seed all leaves.

```
M  Mechanical
   M01 Rotating Equipment      M0101 Pumps · M0102 Compressors · M0103 Turbines
   M02 Static Equipment        M0201 Pressure Vessels · M0202 Heat Exchangers · M0203 Tanks
   M03 Valves                  M0301 Gate · M0302 Globe · M0303 Ball · M0304 Control
   M04 Piping & Fittings       M0401 CS Pipe · M0402 SS Pipe · M0403 Fittings · M0404 Flanges
   M05 Mechanical Spares       M0501 Bearings · M0502 Seals · M0503 Gaskets
E  Electrical
   E01 Drives & Motors         E0101 VFD · E0102 LV Motors · E0103 HV Motors
   E02 Switchgear              E0201 LV Switchgear · E0202 MV Switchgear
   E03 Cables & Accessories    E0301 Power Cable · E0302 Control Cable
I  Instrumentation & Control
   I01 Field Instruments       I0101 Transmitters · I0102 Gauges
   I02 Control Systems         I0201 PLC/DCS · I0202 SCADA
   I03 Analysers & Detection   I0301 Gas Detection · I0302 Flow Analysers
C  Civil & Structural
   C01 Structural              C0101 Structural Steel · C0102 Platforms
   C02 Civil Works             C0201 Concrete · C0202 Coatings
H  HSE & Safety
   H01 PPE                     H0101 Harnesses · H0102 Respiratory · H0103 Protective Clothing
   H02 Fire & Gas              H0201 Fire Fighting · H0202 Gas Detectors
S  Services
   S01 Fabrication             S0101 Mechanical Fab · S0102 Piping Fab
   S02 Inspection & Testing    S0201 NDT · S0202 Certification
   S03 Logistics & Marine      S0301 Freight · S0302 Marine Support
G  General / MRO
   G01 Consumables             G0101 Hardware · G0102 Fasteners
   G02 Tools                   G0201 Hand Tools · G0202 Power Tools
   G03 Lubricants              G0301 Oils · G0302 Greases
```

### Item master (codes reused across PR/RFQ/PO)

| Item code | Description | UOM | SWEC | Indicative RM |
|-----------|-------------|-----|------|---------------|
| MEP-PUMP-075 | Centrifugal Pump, 75 kW, end-suction | Unit | M0101 | 48,500 |
| MEC-COMP-200 | Reciprocating Compressor, 200 kW | Unit | M0102 | 142,000 |
| VLV-GAT-150 | Gate Valve, 6", CS, flanged | Unit | M0301 | 980 |
| VLV-CTL-100 | Control Valve, 4", pneumatic | Unit | M0304 | 7,400 |
| PIP-CS6-SCH40 | Carbon Steel Pipe, 6" SCH40 | m | M0401 | 165 |
| MEC-BRG-22220 | Bearing, spherical roller, 22220 | Unit | M0501 | 820 |
| ELE-VFD-075 | VFD Drive, 75 kW, IP55 | Unit | E0101 | 18,200 |
| ELE-MOT-055 | LV Motor, 55 kW, IE3 | Unit | E0102 | 9,600 |
| INS-TX-PRES | Pressure Transmitter, HART | Unit | I0101 | 2,150 |
| HSE-GAS-4 | Gas Detector, 4-gas, portable | Unit | H0202 | 3,100 |
| HSE-HARN-2L | Safety Harness, double lanyard | Unit | H0101 | 240 |

---

## 2. Internal users (login + roles)

Dev login issues a JWT for any of these (`<user>@hypernix.test`). Internal users are
NOT vendors. **Include a separate approver** so the award/payment approval (DoA) demo has
a different person to approve.

| Code | Name | Roles |
|------|------|-------|
| u_faridah | Faridah Yusof | Buyer |
| u_lim | Lim Chee Kong | Buyer, **Approver** (DoA) |
| u_hafiz | Ir. Hafiz Rahman | TechEvaluator |
| u_nur | Nurul Aina | TechEvaluator |
| u_raj | Rajesh Kumar | TechEvaluator |
| u_tan | Tan Mei Ling | CommEvaluator |
| u_admin | System Admin | Admin |

> The SoD demo: Faridah creates/scores/recommends; **Lim** approves the award and (later)
> payment. One person cannot do both.

---

## 3. Vendors (10) — full Vendor Master tab data

Each vendor: profile, **banking**, **certifications**, **SWEC categories**, **performance
metrics**, **contacts**, **documents**, plus a VendorUser login (`vendor@<code>.test`).

| Code | Name | Status | SWEC categories | Risk | On-time % | Quality issues |
|------|------|--------|-----------------|------|-----------|----------------|
| sentausa | Sentausa Engineering Sdn Bhd | Active | M0101, M0102, M0501 | Low | 96 | 1 |
| pantai | Pantai Valve & Fittings Sdn Bhd | Active | M0301, M0302, M0303, M0401 | Low | 92 | 2 |
| megatech | MegaTech Instrumentation Sdn Bhd | Active | I0101, I0201, I0301 | Medium | 84 | 5 |
| tenaga | Tenaga Electrical Supplies Sdn Bhd | Active | E0101, E0102, E0201 | Low | 95 | 1 |
| hidro | Hidro Process Systems Sdn Bhd | Active | M0201, M0202, M0102 | Medium | 88 | 3 |
| klind | Klind Industrial Supplies Sdn Bhd | Active | G0101, G0102, M0501 | Low | 90 | 2 |
| delta | Delta Mechanical Works Sdn Bhd | Active | S0101, S0102, M0401 | Medium | 86 | 4 |
| mutiara | Mutiara Safety & PPE Sdn Bhd | Active | H0101, H0102, H0202 | Low | 97 | 0 |
| borneo | Borneo Pipe & Steel Sdn Bhd | Active | M0401, M0402, C0101 | Medium | 89 | 3 |
| sabah | Sabah Marine Services Sdn Bhd | Suspended | S0301, S0302 | High | 78 | 7 |

For each vendor seed at least: 1 banking record (bank, account no masked),
2 certifications (e.g. ISO 9001 valid, ISO 45001 valid; one with an expiry within 60 days
to show the "expiring" badge), 2 contacts (primary + secondary), 2 documents (registration,
ISO cert).

---

## 4. Requisitions (PRs) — dummy

| Code | Cost centre | Project / Maint | Required | Status | Lines (item × qty) |
|------|------------|-----------------|----------|--------|--------------------|
| PR-2026-0412 | CC-MAINT-01 | TA-2026 Turnaround | 2026-08-15 | Approved | MEP-PUMP-075×4, ELE-VFD-075×4 |
| PR-2026-0415 | CC-OPS-02 | WO-55821 | 2026-07-30 | Approved | VLV-GAT-150×24, VLV-CTL-100×6 |
| PR-2026-0418 | CC-PROJ-07 | Facilities Upgrade | 2026-09-10 | Approved | PIP-CS6-SCH40×120, MEC-BRG-22220×30 |
| PR-2026-0421 | CC-MAINT-01 | WO-55903 | 2026-07-22 | Approved | ELE-MOT-055×3, MEC-BRG-22220×20 |
| PR-2026-0423 | CC-HSE-03 | HSE Refresh | 2026-08-01 | Approved | HSE-HARN-2L×60, HSE-GAS-4×8 |
| PR-2026-0425 | CC-OPS-02 | — | 2026-08-20 | Pending approval | INS-TX-PRES×12 |
| PR-2026-0428 | CC-PROJ-07 | Facilities Upgrade | 2026-09-25 | Approved | ELE-VFD-075×3, ELE-MOT-055×2 |

---

## 5. RFQ forms (library) — all field types

Seed 2 reusable library forms so the builder + bid form show variety:
- **"Standard goods RFQ"** — terms (commercial), number (delivery lead time weeks),
  number (validity days), instruction (technical), long_text (company profile),
  number (years experience), multi (ISO certs: 9001/14001/45001), attachment (ISO certs),
  yesno (local agent?), list (warranty: 12/24/36 months).
- **"Financial standing form"** — terms, table (audited financials FY23–25 ×
  Revenue/Net profit/Total assets), attachment (audited statements), percent (current ratio),
  group (key personnel: name + role + years).

These collectively exercise every type: short_text, long_text, number, money, percent,
list, multi, yesno, date, attachment, table, group, plus terms + instruction blocks.

---

## 6. RFQs (4 states — one of each)

| Code | Title | Envelope | Invited | Evaluators | Status | ClosesUtc |
|------|-------|----------|---------|-----------|--------|-----------|
| RFQ-2026-0079 | Pump & VFD Package — Facilities Upgrade | Dual | sentausa, hidro, megatech | tech: hafiz,nur,raj · comm: tan | **Evaluation** | 5 days ago |
| RFQ-2026-0083 | Valve Package DN100–DN150 | Dual | pantai, borneo, delta | tech: hafiz,nur · comm: tan | **Closed (ready to open)** | yesterday |
| RFQ-2026-0087 | Piping Materials — 6" CS Line | Single | borneo, delta | — | **Open** | **+5 days from today** |
| RFQ-2026-0074 | HSE Equipment Refresh | Single | mutiara, klind | — | **Awarded** | 14 days ago |

> RFQ-0087 ClosesUtc must be in the **future** so the live "submit a bid" demo works AND
> the deadline rule can be shown (move it to the past to demo rejection). Use the
> "Standard goods RFQ" form for 0079/0087 and add the "Financial standing form" to 0083.

---

## 7. Bids (so opening / comparison / ranking have data)

- **RFQ-0079 (Evaluation):** submitted bids from all 3 invited vendors, with priced lines
  (vary prices so there's a clear low bidder per line) and answers to every question +
  one attachment each.
- **RFQ-0083 (Closed):** submitted bids from pantai, borneo, delta (priced + answered),
  ready to open.
- **RFQ-0087 (Open):** one saved **draft** bid (borneo) to show resume; leave delta with
  no bid so you can demo a fresh submission live.
- **RFQ-0074 (Awarded):** submitted bids from mutiara + klind (mutiara won).

## 8. Technical scores (RFQ-0079) — include a FAIL

Threshold = 70. Seed committee scores so:
- **sentausa ≈ 88 → PASS**, **hidro ≈ 82 → PASS**, **megatech ≈ 58 → FAIL**.
Finalize technical so the eligibility demo works: megatech must be **excluded** from award.

## 9. Award + approval (RFQ-0074)

- Allocation: **mutiara** wins HSE lines (split a line with klind if you want to show
  splitting). Award is **Approved by u_lim** (different from the buyer) — demonstrates DoA.
- Approval generates **one PO** (PO-2026-0074-derived) to mutiara.

---

## 10. Purchase Orders (across states for the PO list)

| PO | Vendor | From | Status | Notes |
|----|--------|------|--------|-------|
| PO-2026-1185 | sentausa | RFQ-0074-class | **Matched** | fully received + invoiced + (historically) paid |
| PO-2026-1186 | pantai | manual/award | **Partially received** | ASN in transit + partial GRN |
| PO-2026-1190 | klind | award | **Issued** | not yet acknowledged (demo acknowledge) |
| PO-2026-1193 | megatech | award | **Discrepancy** | invoice price variance → exception |
| PO-2026-1199 | tenaga | award | **Acknowledged** | ready for ASN |
| PO-2026-0074* | mutiara | RFQ-0074 award | **Issued** | generated by the seeded award |

Each PO: 2–3 lines from the item master with qty/price; `NsId` shown as a fake reference
(e.g. NS-PO-44812) since NetSuite is stubbed.

## 11. ASNs + GRNs

- **sentausa / PO-1185:** ASN **Received** + matching **GRN** (full receipt).
- **pantai / PO-1186:** ASN **In transit** (partial), plus a partial GRN already posted
  (so "partially received" shows and remaining-to-ship > 0).
- **tenaga / PO-1199:** no ASN yet (demo vendor creating an ASN live).
- **klind / PO-1190:** no ASN (PO not acknowledged yet).

## 12. Invoices (across states + 3-way match)

| Invoice | Vendor | PO | Status | Match | Notes |
|---------|--------|----|--------|-------|-------|
| INV-2026-0091 | sentausa | PO-1185 | **Paid** | Matched | seed as Paid (status) so Statements show a settlement |
| INV-2026-0093 | pantai | PO-1186 | **Submitted** | Matched | awaiting buyer approval (demo approve) |
| INV-2026-0094 | megatech | PO-1193 | **Exception** | Price variance | unit price > PO by >2% → blocked from payment |
| INV-2026-0096 | tenaga | PO-1199 | **Approved** | Matched | approved, unpaid |

> Payment Vouchers are NOT seeded (module blank). INV-0091 is marked Paid directly via
> status so the Statement ledger shows one settled line; everything else is outstanding.

## 13. Clarifications / Messages Center

Seed a few threads so the dock + page + broadcast demo well:
- **General** thread: pantai ↔ Faridah (2–3 messages), 1 unread from pantai.
- **RFQ-0087** thread: borneo asks a question; Faridah answers and **broadcasts** it to
  all bidders (so it appears for delta too, marked "Published to all bidders").
- **PO-1185** thread: sentausa ↔ Faridah about delivery (1–2 messages).
- One message with an **attachment** (e.g. "Datasheet.pdf").

## 14. Audit trail

The seeder should write **AuditEntry** rows as it creates records (RFQ released, bids
submitted, envelope opened, scored, finalized, award approved, PO issued, GRN posted,
invoice approved/exception) so PO/RFQ detail **timelines are populated** in the demo —
not empty.

---

## 15. Expected dashboard (verify against the prototype)

After seeding, the buyer Dashboard should show roughly: **Open requisitions** = count of
Approved PRs not yet sourced; **RFQs awaiting bids** = 1 (RFQ-0087 Open); **Ready to open**
= 1 (RFQ-0083 Closed); **Under evaluation** = 1 (RFQ-0079). The "Active RFQs" table lists
all four with correct envelope/vendors/closes/status. Confirm these match the prototype's
dashboard.

## 16. Not seeded

- **Payment Vouchers** — none (blank placeholder screen).
- **NetSuite** — no real calls; client is a no-op/log stub; NsId values are cosmetic.
