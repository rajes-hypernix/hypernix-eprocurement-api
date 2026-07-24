namespace FSH.Modules.Sourcing.Domain;

/// <summary>"No quotes" is derived (an Open line whose latest PrLineSourcing link is Returned), never stored.</summary>
public enum PrLineStatus { Open, InDraftRfq, InRfq, Awarded, Cancelled, Closed }

public enum LinkStatus { Active, Returned, Cancelled }

/// <summary>
/// Derived only via PurchaseRequisition.RecomputeHeaderStatus() — never set directly.
/// PartiallyOrdered/Ordered are the direct-from-requisition ordering dimension (Phase 3) and take
/// precedence over the sourcing dimension below them — "ordering outranks sourcing."
/// </summary>
public enum PrHeaderStatus { Draft, Submitted, PartiallySourced, Sourced, PartiallyOrdered, Ordered, Cancelled }

public enum RfqEnvelope { Single, Dual }

/// <summary>Closing is a server timestamp (ClosesUtc), never a button.</summary>
public enum RfqStatus { Draft, Open, Closed, Evaluation, Awarded, Cancelled }

/// <summary>Distinct from RfqStatus; lives entirely on RfqInvitation.</summary>
public enum RfqInvitationStatus { Invited, Viewed, IntendToBid, Declined, BidSubmitted, Rescinded }

/// <summary>Append-only business-fact log, separate from generic audit entries.</summary>
public enum RfqEventType { Released, Extended, VendorInvited, InvitationRescinded, VendorDeclined, DeclineReversed, BidSubmitted, BidWithdrawn, Closed, Cancelled, Awarded }

/// <summary>The 4 fixed technical-evaluation criteria — weights live on <see cref="TechnicalCriteria.Weights"/>.</summary>
public enum TechnicalCriterion { Compliance, Experience, Delivery, QA }

public enum AwardStatus { PendingApproval, Approved }

public enum ClarificationSenderKind { Buyer, Vendor }
