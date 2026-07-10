namespace eProcure.Domain.Sourcing;

public enum AwardStatus { Draft, PendingApproval, Approved }

/// <summary>
/// An award decision for an RFQ. Created in <see cref="AwardStatus.PendingApproval"/>
/// and only becomes <see cref="AwardStatus.Approved"/> (generating POs) after an
/// Approval by a DIFFERENT authorised user (segregation of duties / DoA,
/// BUSINESS-RULES [G]). Allocations must respect eligibility + the quantity cap.
/// </summary>
public class Award
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // AWD-2026-0001
    public Guid RfqId { get; set; }
    public AwardStatus Status { get; set; } = AwardStatus.PendingApproval;
    public string CreatedByUserId { get; set; } = default!;

    public List<AwardAllocation> Allocations { get; set; } = [];

    // Derived from the allocations — never persisted (mirrors PurchaseOrder.Total; DBA-10). The DoA
    // gate and the AwardDto read this computed value, so it cannot drift from the stored allocations.
    public decimal TotalValue => Allocations.Sum(a => a.Qty * a.UnitPrice);

    // Approval (DoA gate)
    public string? ApproverUserId { get; set; }
    public DateTime? ApprovedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public class AwardAllocation
{
    public string RfqLineCode { get; set; } = default!;    // item code
    public Guid VendorId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}
