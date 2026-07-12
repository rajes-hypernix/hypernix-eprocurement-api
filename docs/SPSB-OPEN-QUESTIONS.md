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
   but no category dimension exists on PO/Invoice lines, so D4's spend chart is single-series
   (honest). Should P2P lines carry a spend category? Note: **D6's custom segments may be the
   delivery vehicle** — a "Spend Category" segment applied to PO/Invoice lines is exactly what D6
   builds.

4. **Vendor segmentation for onboarding analytics** — the mock split onboarded vendors by
   Manufacturer/Distributor/Service-provider; the data model only carries SWEC/Non-SWEC. Should
   vendors carry a business-type dimension? Same note: a D6 custom segment on Vendor may deliver it.

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
