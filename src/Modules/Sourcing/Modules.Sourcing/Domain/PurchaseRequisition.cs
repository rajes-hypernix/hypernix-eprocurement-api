using FSH.Framework.Core.Domain;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// A purchase requisition — the aggregate root for demand. <see cref="HeaderStatus"/> is always
/// derived from its lines via <see cref="RecomputeHeaderStatus"/>, never set directly.
/// </summary>
public sealed class PurchaseRequisition : AggregateRoot<Guid>
{
    private readonly List<PrLine> _lines = [];

    public string Code { get; private set; } = default!;
    public string Requestor { get; private set; } = default!;
    public string Department { get; private set; } = string.Empty;
    public string? DepartmentCode { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? LocationCode { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string? CategoryCode { get; private set; }
    public string Job { get; private set; } = string.Empty;
    public string? JobCode { get; private set; }
    public string Memo { get; private set; } = string.Empty;
    public string CostCentre { get; private set; } = string.Empty;
    public string? Project { get; private set; }

    /// <summary>Opaque entry-form layout choice — no Forms module exists yet, so no submit-gate validation.</summary>
    public Guid? EntryFormId { get; private set; }

    public DateOnly? RaisedOn { get; private set; }
    public DateOnly? RequiredOn { get; private set; }
    public DateTime? SubmittedUtc { get; private set; }
    public bool Submitted { get; private set; }
    public PrHeaderStatus HeaderStatus { get; private set; } = PrHeaderStatus.Draft;
    public string Currency { get; private set; } = "MYR";
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<PrLine> Lines => _lines;

    private PurchaseRequisition() { }

    public static PurchaseRequisition Create(
        string code,
        string requestor,
        string department,
        string? departmentCode,
        string location,
        string? locationCode,
        string category,
        string? categoryCode,
        string job,
        string? jobCode,
        string memo,
        string costCentre,
        string? project,
        Guid? entryFormId,
        DateOnly? raisedOn,
        DateOnly? requiredOn,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestor);

        var now = DateTime.UtcNow;
        return new PurchaseRequisition
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Requestor = requestor.Trim(),
            Department = department,
            DepartmentCode = departmentCode,
            Location = location,
            LocationCode = locationCode,
            Category = category,
            CategoryCode = categoryCode,
            Job = job,
            JobCode = jobCode,
            Memo = memo,
            CostCentre = costCentre,
            Project = project,
            EntryFormId = entryFormId,
            RaisedOn = raisedOn,
            RequiredOn = requiredOn,
            Currency = string.IsNullOrWhiteSpace(currency) ? "MYR" : currency,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public void UpdateHeader(
        string department,
        string? departmentCode,
        string location,
        string? locationCode,
        string category,
        string? categoryCode,
        string job,
        string? jobCode,
        string memo,
        string costCentre,
        string? project,
        DateOnly? requiredOn)
    {
        RequireDraftable();
        Department = department;
        DepartmentCode = departmentCode;
        Location = location;
        LocationCode = locationCode;
        Category = category;
        CategoryCode = categoryCode;
        Job = job;
        JobCode = jobCode;
        Memo = memo;
        CostCentre = costCentre;
        Project = project;
        RequiredOn = requiredOn;
        UpdatedUtc = DateTime.UtcNow;
    }

    public PrLine AddLine(string itemCode, string description, decimal qty, string uom, decimal estUnitPrice)
    {
        RequireDraftable();
        var line = PrLine.Create(Id, itemCode, description, qty, uom, estUnitPrice);
        _lines.Add(line);
        RecomputeHeaderStatus();
        UpdatedUtc = DateTime.UtcNow;
        return line;
    }

    /// <summary>Draft -&gt; Submitted. Requires at least one Open line.</summary>
    public void Submit(DateTime nowUtc)
    {
        if (Submitted)
        {
            throw new SourcingRuleException($"Requisition {Code} has already been submitted.");
        }

        if (!_lines.Any(l => l.LifecycleStatus == PrLineStatus.Open))
        {
            throw new SourcingRuleException("A requisition needs at least one open line before it can be submitted.");
        }

        Submitted = true;
        SubmittedUtc = nowUtc;
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
    }

    /// <summary>[C7] Cannot cancel a PR while a line is in an RFQ or awarded.</summary>
    public void Cancel(DateTime nowUtc)
    {
        if (_lines.Any(l => l.LifecycleStatus is PrLineStatus.InRfq or PrLineStatus.Awarded))
        {
            throw new SourcingRuleException("Cannot cancel a PR while a line is in an RFQ or awarded.");
        }

        foreach (var line in _lines.Where(l => l.LifecycleStatus is PrLineStatus.Open or PrLineStatus.InDraftRfq))
        {
            line.Cancel("Requisition cancelled", nowUtc);
        }

        HeaderStatus = PrHeaderStatus.Cancelled;
        UpdatedUtc = nowUtc;
    }

    public PrLine GetLine(Guid lineId) =>
        _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new SourcingRuleException($"Line {lineId} does not belong to requisition {Code}.");

    public bool HasLine(Guid lineId) => _lines.Any(l => l.Id == lineId);

    /// <summary>InDraftRfq -&gt; InRfq, on RFQ release.</summary>
    public PrLineTransition ReleaseLineToRfq(Guid lineId, string rfqCode, DateTime nowUtc)
    {
        var result = GetLine(lineId).ReleaseToRfq(rfqCode, nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    /// <summary>InRfq -&gt; Open, when the RFQ is cancelled or the vendor isn't awarded.</summary>
    public PrLineTransition ReturnLineFromRfq(Guid lineId, string? reason, DateTime nowUtc)
    {
        var result = GetLine(lineId).ReturnFromRfq(reason, nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    public PrLineTransition CancelLine(Guid lineId, string? reason, DateTime nowUtc)
    {
        var result = GetLine(lineId).Cancel(reason, nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    public PrLineTransition ReleaseLineForResourcing(Guid lineId, string? reason, DateTime nowUtc)
    {
        var result = GetLine(lineId).ReleaseForResourcing(reason, nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    public PrLineTransition ReserveLine(Guid lineId, DateTime nowUtc)
    {
        var result = GetLine(lineId).AddToDraft(nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    public PrLineTransition UnreserveLine(Guid lineId, DateTime nowUtc)
    {
        var result = GetLine(lineId).AbandonDraft(nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    public PrLineTransition ReopenLine(Guid lineId, DateTime nowUtc)
    {
        var result = GetLine(lineId).Reopen(nowUtc);
        RecomputeHeaderStatus();
        UpdatedUtc = nowUtc;
        return result;
    }

    /// <summary>
    /// Recomputes the derived header status from current line states:
    /// all lines Cancelled -&gt; Cancelled; not submitted and nothing sourced -&gt; Draft;
    /// sourced with remaining open demand -&gt; PartiallySourced; sourced with none left -&gt; Sourced;
    /// otherwise -&gt; Submitted.
    /// </summary>
    public void RecomputeHeaderStatus()
    {
        if (_lines.Count > 0 && _lines.All(l => l.LifecycleStatus == PrLineStatus.Cancelled))
        {
            HeaderStatus = PrHeaderStatus.Cancelled;
            return;
        }

        bool hasSourced = _lines.Any(l => l.LifecycleStatus is PrLineStatus.InDraftRfq or PrLineStatus.InRfq or PrLineStatus.Awarded);
        bool hasOpenDemand = _lines.Any(l => l.LifecycleStatus == PrLineStatus.Open);

        HeaderStatus = !Submitted && !hasSourced
            ? PrHeaderStatus.Draft
            : hasSourced && hasOpenDemand
                ? PrHeaderStatus.PartiallySourced
                : hasSourced && !hasOpenDemand
                    ? PrHeaderStatus.Sourced
                    : PrHeaderStatus.Submitted;
    }

    private void RequireDraftable()
    {
        if (Submitted)
        {
            throw new SourcingRuleException($"Requisition {Code} has already been submitted and can no longer be edited.");
        }
    }
}
