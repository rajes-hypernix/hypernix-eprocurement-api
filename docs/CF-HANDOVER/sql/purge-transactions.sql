-- CFH-T1: purge ALL test transaction data (dev DB), FK-safe.
-- Owned line tables (PrLines, RfqLines, BidLines, PoLines, AsnLines, GrnLines,
-- InvoiceLines, BidAnswers, BidAttachments, AwardAllocations, RfqFormItems) are
-- truncated automatically via CASCADE from their aggregate roots. Reference/config
-- (Vendors, Users, *Defs, Lists, Segments, EntryForms, Numbering) is NOT touched.
BEGIN;
TRUNCATE
  "RfqEvents", "RfqInvitations", "PrLineSourcings",
  "Bids", "TechnicalScores", "Awards", "Rfqs",
  "PurchaseRequisitions",
  "PurchaseOrders", "Asns", "Grns", "Invoices", "Clarifications",
  "CustomFieldValues", "SegmentAssignments"
  RESTART IDENTITY CASCADE;
-- Transaction audit only (keep config audit: Vendor / CustomField / CustomList / onboarding).
DELETE FROM "AuditEntries" WHERE "EntityType" IN
  ('PurchaseRequisition','PrLine','Rfq','Bid','Award','Po','Asn','Grn','Invoice');
-- The seeded Bid attachment now dangles (its bid is gone).
DELETE FROM "StoredFiles" WHERE "OwnerKind" = 'Bid';
COMMIT;
