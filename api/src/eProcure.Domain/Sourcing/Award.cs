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
    public AwardStatus Status { get; private set; } = AwardStatus.PendingApproval;
    public string CreatedByUserId { get; set; } = default!;

    public List<AwardAllocation> Allocations { get; set; } = [];

    // Derived from the allocations — never persisted (mirrors PurchaseOrder.Total; DBA-10). The DoA
    // gate and the AwardDto read this computed value, so it cannot drift from the stored allocations.
    public decimal TotalValue => Allocations.Sum(a => a.Qty * a.UnitPrice);

    // Approval (DoA gate)
    // A2F-T5 (NIT-3): approval stamps are private-set, written only inside the guarded
    // transitions below — the Invoice.MarkApproved / Rfq.MarkAwarded discipline.
    public string? ApproverUserId { get; private set; }
    public DateTime? ApprovedUtc { get; private set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // ===== Lifecycle transitions (T3). =====

    /// <summary>(Re)submits the award for approval. The service blocks re-submitting an already-approved
    /// award before calling this; the guard here is the invariant.</summary>
    public void MarkPendingApproval()
    {
        if (Status == AwardStatus.Approved) throw new DomainRuleException($"Award {Code} is already approved.");
        Status = AwardStatus.PendingApproval;
        // A (re)submission has no approval yet — the stamps reset HERE, not in the service.
        ApproverUserId = null;
        ApprovedUtc = null;
    }

    /// <summary>PendingApproval → Approved, stamping the approver (DoA/SoD checks live in the service).</summary>
    public void Approve(string approverUserId, DateTime nowUtc)
    {
        if (Status != AwardStatus.PendingApproval)
            throw new DomainRuleException($"Award {Code} is not pending approval.");
        Status = AwardStatus.Approved;
        ApproverUserId = approverUserId;
        ApprovedUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }

    /// <summary>TEST/SEED ONLY — sets the status directly, bypassing transitions. Never call from
    /// production service code (enforced by the ArchitectureTests source-scan).</summary>
    public Award SeededAs(AwardStatus status, string? approverUserId = null, DateTime? approvedUtc = null)
    {
        Status = status;
        ApproverUserId = approverUserId;
        ApprovedUtc = approvedUtc;
        return this;
    }
}

public class AwardAllocation
{
    public Guid Id { get; private set; } = Guid.NewGuid();   // stable grain key for facts/lineage (Slice H T1)
    public string RfqLineCode { get; set; } = default!;    // item code
    public Guid VendorId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}
