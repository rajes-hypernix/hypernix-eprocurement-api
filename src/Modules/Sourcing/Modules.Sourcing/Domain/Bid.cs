using FSH.Framework.Core.Domain;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// One vendor's bid on an RFQ — the aggregate root for the vendor-portal bidding workflow.
/// Exactly one <see cref="Bid"/> exists per (RfqId, VendorId) pair; drafts and the eventual
/// submission live on the same row, matching the old system's single-record-per-vendor model.
/// </summary>
public sealed class Bid : AggregateRoot<Guid>
{
    private readonly List<BidLine> _lines = [];
    private readonly List<BidAnswer> _answers = [];
    private readonly List<BidAttachment> _files = [];

    public string Code { get; private set; } = default!;
    public Guid RfqId { get; private set; }

    /// <summary>Bare reference into Modules.Suppliers — no cross-schema FK, matching RfqInvitation.VendorId.</summary>
    public Guid VendorId { get; private set; }

    public bool Submitted { get; private set; }
    public DateTime? SubmittedUtc { get; private set; }
    public bool SavedDraft { get; private set; }
    public DateTime? WithdrawnUtc { get; private set; }
    public string? Lead { get; private set; }
    public string? Warranty { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<BidLine> Lines => _lines;
    public IReadOnlyList<BidAnswer> Answers => _answers;
    public IReadOnlyList<BidAttachment> Files => _files;

    private Bid() { }

    public static Bid CreateDraft(string code, Guid rfqId, Guid vendorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var now = DateTime.UtcNow;
        return new Bid
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            RfqId = rfqId,
            VendorId = vendorId,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    /// <summary>Replaces lines/answers/files wholesale — same clear+add pattern as <c>Rfq.UpdateDraft</c>.</summary>
    public void SaveDraft(
        string? lead,
        string? warranty,
        IReadOnlyList<BidLine> lines,
        IReadOnlyList<BidAnswer> answers,
        IReadOnlyList<BidAttachment> files,
        DateTime nowUtc)
    {
        if (Submitted)
        {
            throw new SourcingRuleException("Withdraw your submitted bid before editing it.");
        }

        Lead = lead;
        Warranty = warranty;
        _lines.Clear();
        _lines.AddRange(lines);
        _answers.Clear();
        _answers.AddRange(answers);
        _files.Clear();
        _files.AddRange(files);
        SavedDraft = true;
        UpdatedUtc = nowUtc;
    }

    /// <summary>Requires at least one line with Bidding &amp;&amp; Price &gt; 0 — matches the old BidService rule.</summary>
    public void Submit(DateTime nowUtc)
    {
        if (Submitted)
        {
            throw new SourcingRuleException("This bid has already been submitted.");
        }

        if (!_lines.Exists(l => l.Bidding && l.Price > 0))
        {
            throw new SourcingRuleException("Quote a price on at least one line before submitting your bid.");
        }

        Submitted = true;
        SubmittedUtc = nowUtc;
        SavedDraft = true;
        UpdatedUtc = nowUtc;
    }

    public void Withdraw(DateTime nowUtc)
    {
        if (!Submitted)
        {
            throw new SourcingRuleException("This bid has not been submitted, so it cannot be withdrawn.");
        }

        Submitted = false;
        SavedDraft = true;
        WithdrawnUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }
}
