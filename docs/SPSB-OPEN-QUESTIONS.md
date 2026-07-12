# SPSB open questions

Decisions and dependencies that sit with SPSB. Defensible defaults exist
today where noted; the point is that SPSB decides deliberately rather than
discovering a default later.

## A. Product decisions

1. **Rescinded vendors** — should a vendor whose invitation was rescinded see an "invitation withdrawn"
   notice in their portal, or should the RFQ disappear from their view entirely? Current behaviour:
   disappears. (Buyer-side already keeps rescinded rows visible, struck-through, with reason.)
   **Needed before UAT.**

2. **Vendor PO audit visibility** — should vendors see an audit/history view of their own POs
   (actor names redacted)? Current: no — the PO audit trail is internal-only (it contains internal
   actor names; AUTHORIZATION-MATRIX ruling OD-10). Defensible default; SPSB may prefer transparency.

3. **Spend category dimension** — the demo dashboard mocked a Materials/Services/MRO spend split,
   but no category dimension existed on P2P lines, so D4's spend chart is single-series (honest).
   **D6 update: the engine is READY** — an admin defines a "Spend Category" segment, applies it to
   PurchaseOrder (per-line if wanted — the line-level path is live and gate-proven on PO lines) and
   Invoice, and every saved view, KPI and series can slice by it, unassigned records surfaced as
   their own named bucket. The open question is now **which segments and which values, not
   whether**: SPSB names the category list (e.g. Materials/Services/MRO) and it is data entry in
   the Setup screen, zero deployments.

4. **Vendor segmentation for onboarding analytics** — the mock split onboarded vendors by
   Manufacturer/Distributor/Service-provider; the data model only carries SWEC/Non-SWEC. **D6
   update: the engine is READY** — a "Business Type" segment applied to Vendor slices onboarding
   analytics the day SPSB names its values (the Vendor detail's Custom-fields tab already renders
   segment assignment). Again: which segments and which values, not whether.

*Raised by: RFQ Lifecycle Slice I/J (1); Slice RM role-matrix rulings (2); D4 dashboard honesty
census (3, 4). These are product stances, not technical constraints — either behaviour is a small
change; the point is that SPSB decides it, not us.*

## B. Integration dependencies — required by Sprint 2 of the delivery roadmap (Master Process & Control Map Rev D, page 7)

2. **NetSuite sandbox access** with an integration record created for
   eProcure, using OAuth 2.0 client-credentials (certificate exchange with
   Hypernix). Required before Sprint 2 begins.

3. **Restricted NetSuite integration role**, scoped to the record types
   eProcure touches: vendor, purchase order, item receipt, vendor bill,
   bill payment, return authorization, vendor credit. Not an administrator
   role. Required before Sprint 2 begins.

4. **AP process decisions** — invoice rejection reason codes, invoice
   approval thresholds and routing, and the payment-advice format vendors
   should see in the portal. Required before Sprint 5.

5. **Contract representation in NetSuite** — blanket purchase order (the
   native amount-limit vehicle for Approved Contract Value) or a custom
   contract record (richer SLA / penalty / warranty fields, more build)?
   Shapes Sprint 7 design; decision needed before Sprint 6 ends.
