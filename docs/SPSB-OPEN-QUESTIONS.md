# SPSB open questions

Decisions and dependencies that sit with SPSB. Defensible defaults exist
today where noted; the point is that SPSB decides deliberately rather than
discovering a default later.

## A. Product decisions

1. **Rescinded vendors** — should a vendor whose invitation was rescinded see an "invitation withdrawn"
   notice in their portal, or should the RFQ disappear from their view entirely? Current behaviour:
   disappears. (Buyer-side already keeps rescinded rows visible, struck-through, with reason.)
   **Needed before UAT.**

*Raised by: RFQ Lifecycle Slice I/J. These are product stances, not technical constraints — either
behaviour is a small change; the point is that SPSB decides it, not us.*

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
