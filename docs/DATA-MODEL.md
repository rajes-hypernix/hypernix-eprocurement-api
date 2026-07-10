# DATA-MODEL.md

All entities have: `Guid Id` (PK), `string Code` (human-readable, sequence-generated
where applicable), `DateTime CreatedUtc`, `DateTime UpdatedUtc`. Money is `decimal`.
Statuses are C# enums. Relationships below are the minimum; add fields as slices need.

## Identity & supplier

**User** (internal) — `Name`, `Email`, `Roles` (Buyer/TechEvaluator/CommEvaluator/Admin
flags), `IsActive`. *Internal users cannot be vendors (SoD).*

**Vendor** — `Name`, `Status` (Active/Suspended/Pending), `Categories` (SWEC codes),
`RiskClass`, profile/banking/cert fields. A **VendorUser** (separate principal) logs
into the supplier portal and is linked to exactly one Vendor.

**AuditEntry** (append-only) — `EntityType`, `EntityId`, `EntityCode`, `Action`,
`ActorId`, `ActorName`, `ActorRole`, `BeforeJson`, `AfterJson`, `Utc`. Never updated or
deleted.

## Sourcing

**PurchaseRequisition (PR)** — `Status`, `CostCentre`, `Project`, `MaintenanceRef`,
`RequiredDate`, lines: **PrLine** (`ItemCode`, `Description`, `Qty`, `Uom`,
`EstUnitPrice`).

**Rfq** — `Title`, `Envelope` (Single/Dual), `Status` (Draft/Open/Closed/Evaluation/
Awarded), `ClosesUtc` (the real deadline), `InvitedVendorIds`, `TechnicalEvaluatorIds`,
`CommercialEvaluatorId`, `TechFinalized` (bool), `CommercialOpened` (bool). Children:
- **RfqLine** (`ItemCode`, `Description`, `Qty`, `Uom`) — sourced from PR lines.
- **RfqForm** — the question form: **FormItem** (`Kind`: Question/Terms/Instruction;
  `Type`: short_text/long_text/number/money/percent/list/multi/yesno/date/attachment/
  table/group; `Group`: Technical/Commercial; `Section`; `Label`; `Required`; `Config`
  JSON for options/columns/rows/fields). Reusable forms live in a **FormLibrary**.

**Bid** — one per (Rfq, Vendor). `Submitted` (bool), `SubmittedUtc`, `SavedDraft`.
Children: **BidLine** (`RfqLineCode`, `Price`, `Qty`, `Partial`, `AltItem`),
**BidAnswer** (`FormItemId`, value), attachments.

**TechnicalScore** — (`RfqId`, `VendorId`, `EvaluatorId`, `Criterion`, `Score` 0–100).
Derived: per-evaluator weighted score, committee average, pass/fail vs `TechThreshold`.

**Award** — (`RfqId`), children **AwardAllocation** (`RfqLineCode`, `VendorId`, `Qty`,
`UnitPrice`). `Status` (Draft/PendingApproval/Approved). An **Approval** record
(approver, decision, utc) gates it (DoA).

## Procure-to-Pay

**PurchaseOrder (PO)** — `VendorId`, `RfqId`, `PrRefs`, `Status` (Draft/Issued/
Acknowledged/PartiallyReceived/Received/Matched/Closed/Discrepancy), `NsId`,
`Incoterm`, `Currency`. Lines: **PoLine** (`ItemCode`, `Qty`, `Uom`, `UnitPrice`,
`ReceivedQty`, `InvoicedQty`).

**Asn** — `PoId`, `VendorId`, `Carrier`, `TrackingNo`, `ShippedDate`, `ExpectedDate`,
`Status` (Draft/InTransit/Received). Lines: **AsnLine** (`PoLineCode`, `ShippedQty`,
`LotNo`).

**Grn** (Goods Receipt) — `AsnId`, `PoId`. Lines: **GrnLine** (`PoLineCode`,
`ExpectedQty`, `ReceivedQty`, `Condition`).

**Invoice** — `PoId`, `VendorId`, `SupplierRef`, `Status` (Draft/Submitted/Approved/
Exception/Paid), `MatchStatus`, `Subtotal`, `Sst`, `Wht`, `Total`, `NsId`. Lines:
**InvoiceLine** (`PoLineCode`, `Qty`, `UnitPrice`). Match computed vs PO + GRN.

**PaymentVoucher** — `VendorId`, `Status` (Draft/PendingApproval/Paid), invoice refs,
`Gross`, deductions (`Retention`, `Ld`, `Contra`), `Net`, `NsId`, an **Approval** gate.

**Statement / SOA** — derived (not stored): per-vendor running ledger from POs, GRNs,
invoices, payments; aging buckets; GRNI accrual. Supplier reconciliation upload is a
transient compare.

## Key relationships

```
PR.lines ──sourced──> RFQ.lines ──awarded──> Award.allocations ──generates──> PO(one per vendor)
PO ──> ASN(s) ──> GRN(s) ;  PO + GRN + Invoice = 3-way match ;  Invoice ──> PaymentVoucher
Vendor ──> Bid, PO, ASN, Invoice, Voucher (vendor sees ONLY its own)
Every state change ──> AuditEntry
```
