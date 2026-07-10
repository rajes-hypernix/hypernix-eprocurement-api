namespace eProcure.Domain.Sourcing;

/// <summary>
/// A vendor's bid on an RFQ (one per Rfq+Vendor). Created/updated/submitted only
/// while the RFQ is Open and before ClosesUtc (BUSINESS-RULES [G] bid deadline).
/// A vendor may only read/write its own bid (access scoping).
/// </summary>
public class Bid
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;
    public Guid RfqId { get; set; }
    public Guid VendorId { get; set; }
    public bool Submitted { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public bool SavedDraft { get; set; }

    /// <summary>Set when the vendor withdraws a submitted bid before close (RFQ-LIFECYCLE-ADDENDUM §2.4);
    /// cleared on resubmission. The invitation transition (T6) is driven from the Rfq aggregate.</summary>
    public DateTime? WithdrawnUtc { get; set; }

    public int Lead { get; set; }
    public int Warranty { get; set; }
    public List<BidLine> Lines { get; set; } = [];
    public List<BidAnswer> Answers { get; set; } = [];
    public List<BidAttachment> Files { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>Withdraw a submitted bid (before close), returning it to an editable draft. Caller drives
    /// the invitation T6 transition on the Rfq aggregate + writes the BidWithdrawn event.</summary>
    public void Withdraw(DateTime nowUtc)
    {
        if (!Submitted)
            throw new DomainRuleException("Only a submitted bid can be withdrawn.");
        Submitted = false;
        SavedDraft = true;
        WithdrawnUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }
}

public class BidLine
{
    public string ItemCode { get; set; } = default!;
    public bool Bidding { get; set; } = true;     // is the vendor quoting this line?
    public decimal Price { get; set; }            // unit price
    public decimal Qty { get; set; }              // offered quantity
    public bool Partial { get; set; }
    public string? AltItem { get; set; }
}

public class BidAnswer
{
    public int QuestionOrder { get; set; }        // aligns to RfqForm FormItem.Order
    public string Value { get; set; } = "";
}

public class BidAttachment
{
    public string FileName { get; set; } = default!;
}
