namespace FSH.Modules.Procurement.Domain;

public enum PoStatus { Draft, Verified, Issued, Acknowledged, PartiallyReceived, Received, Matched, Discrepancy, Cancelled, Closed }

/// <summary>Provenance of a PO — which creation route produced it (Phase 3).</summary>
public enum PoSourceKind { FromAward, FromRequisition, Standalone }

public enum AsnStatus { InTransit, Received }

public enum InvoiceStatus { Submitted, Exception, Approved }
