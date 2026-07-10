# BUSINESS-RULES.md

These rules are **mandatory** and were derived from a formal audit of the prototype.
Each is enforced **server-side** (Application/Domain) and covered by a test. The audit
found the prototype enforced the quantity rules but only *simulated* the governance
rules in the UI — in this build they must be real.

Legend: **[Q]** quantity/match (enforce in domain), **[G]** governance (enforce + audit),
**[A]** audit, **[$]** money/data integrity.

## Sourcing & award

- **[G] Bid deadline.** A bid may be created/updated/submitted **only while
  `Rfq.Status == Open` and `IClock.UtcNow <= Rfq.ClosesUtc`**. After either fails,
  reject with 409. Closing is a server timestamp, never a manual button.
- **[G] Sealed bids.** Commercial envelope/prices are not readable until
  `Rfq.CommercialOpened == true`, which may only be set after `TechFinalized == true`
  (dual envelope). No endpoint returns commercial data before that gate.
- **[G] Award eligibility.** For a dual-envelope RFQ that is technically finalized, only
  vendors with `TechPass == true` may receive an allocation. Reject any allocation to a
  failed or non-bidding vendor.
- **[Q] Award quantity.** For each line, `allocatedQty <= min(requiredQty, offeredQty)`
  across all allocations. No over-allocation.
- **[G] Award approval / DoA.** `confirmAward` creates an Award in `PendingApproval`. It
  becomes `Approved` (and generates POs) only after an **Approval** by a user who is
  **not** the creator/evaluator, against a configurable value threshold (Delegation of
  Authority). One person cannot create + score + award alone.
- **[G] Evaluator masking.** During technical scoring, evaluators never receive vendor
  identities (server returns aliases "Bidder A/B/C"). Masking is bound to the
  authenticated principal + RFQ phase — it cannot be turned off by switching roles.

## Procure-to-Pay

- **[Q] ASN no over-ship / no duplicate.** `remainingToShip(line) = orderedQty −
  inTransitQty − receivedQty`. An ASN line's `ShippedQty` is clamped to
  `remainingToShip`; if all lines are 0, reject ASN creation ("nothing left to ship").
- **[Q] GRN receipt caps.** `ReceivedQty <= min(asnLine.ShippedQty, orderedQty −
  alreadyReceived)`. Over-receipt is rejected/clamped. Under-receipt is allowed, flagged
  "Short", and frees the shortfall back into `remainingToShip` for re-shipment.
- **[Q] 3-way match.** Invoice line `Qty <= receivedQty − alreadyInvoicedQty`
  (no over-billing). Match compares PO price vs invoice price within `PriceTolerance`
  (default 2%). A variance beyond tolerance sets the invoice to **Exception**.
- **[G] Exception blocks payment.** Invoices in `Exception` cannot be added to a payment
  voucher until resolved/approved.
- **[G] Payment approval.** A `PaymentVoucher` is `PendingApproval` on submit and only
  `Paid` after an **Approval** by a different, authorised user (DoA). Releasing payment
  flips its invoices to `Paid`.

## Cross-cutting

- **[A] Audit everything.** Every state transition across BOTH sourcing/eval/award AND
  procure-to-pay writes an immutable `AuditEntry` (who, what, when, before, after). This
  includes RFQ release, bid submission, envelope opening, scoring, finalize, the award
  decision, PO issue, GRN, invoice approve/exception, and payment release.
- **[G] Access scoping.** A VendorUser may read/write only records belonging to its
  Vendor. Internal users are never vendors. Resource-based authorization on every
  vendor-scoped endpoint.
- **[$] Money.** All monetary values `decimal` (`numeric(18,2)`). SST 8% and WHT applied
  on the server. Round half-up to 2 dp at calculation boundaries; never trust client
  totals — recompute server-side.
- **[$] IDs & codes.** `Guid` PKs; human codes from a server sequence per type
  (`RFQ-2026-0001`, `PO-2026-0001`…). Never length-based or random.
- **[G] NetSuite integration.** All ERP pushes (PO, Vendor Bill, Item Receipt, Bill
  Payment) go through a queue with retry; failures are surfaced and retryable, never
  silently assumed successful.

## Reference data (mirror the prototype seeds for dev)

Vendors: sentausa, pantai, megatech, tenaga, hidro, klind, delta, mutiara, borneo, sabah.
Sample RFQs: dual + single envelopes; one awarded, one in evaluation, one open, one
closed. PRs across cost centres. Seed enough to exercise every flow end-to-end.
