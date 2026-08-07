using FSH.Framework.Core.Domain;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// A purchase requisition — the aggregate root for demand. <see cref="HeaderStatus"/> is always
/// derived from its lines via <c>RecomputeHeaderStatus</c>, never set directly.
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

    /// <summary>
    /// Optional ship-to suggestion carried to a direct PO — Location+Address XOR ad-hoc text XOR neither.
    /// Not a submit gate (unlike PO).
    /// </summary>
    public Guid? ShipToLocationId { get; private set; }
    public Guid? ShipToAddressId { get; private set; }
    public string? ShipToAdhoc { get; private set; }

    public bool HasShipTo =>
        (ShipToLocationId is not null && ShipToAddressId is not null) || !string.IsNullOrWhiteSpace(ShipToAdhoc);

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
        EnsureRequiredOnNotInPast(requiredOn);

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
        DateOnly? requiredOn,
        Guid? entryFormId = null)
    {
        RequireDraftable();
        // Keep an already-stored past date (e.g. draft raised yesterday); reject only a newly chosen past date.
        if (requiredOn != RequiredOn)
        {
            EnsureRequiredOnNotInPast(requiredOn);
        }
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
        EntryFormId = entryFormId;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Choose the Location-address ship-to source; clears any ad-hoc text (XOR).</summary>
    public void SetShipToLocation(Guid locationId, Guid addressId)
    {
        RequireDraftable();
        ShipToLocationId = locationId;
        ShipToAddressId = addressId;
        ShipToAdhoc = null;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Choose the ad-hoc free-text ship-to source (≤400); clears the Location-address pair (XOR).</summary>
    public void SetShipToAdhoc(string? text)
    {
        RequireDraftable();
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmed is { Length: > 400 })
        {
            throw new SourcingRuleException("An ad-hoc ship-to address is at most 400 characters.");
        }

        ShipToAdhoc = trimmed;
        ShipToLocationId = null;
        ShipToAddressId = null;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ClearShipTo()
    {
        RequireDraftable();
        ShipToLocationId = null;
        ShipToAddressId = null;
        ShipToAdhoc = null;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Apply ship-to from request: ad-hoc wins; else both location+address; else clear.
    /// Location existence is validated by the application layer when needed.
    /// </summary>
    public void ApplyShipTo(Guid? locationId, Guid? addressId, string? adhoc)
    {
        if (!string.IsNullOrWhiteSpace(adhoc))
        {
            SetShipToAdhoc(adhoc);
            return;
        }

        if (locationId is null && addressId is null)
        {
            ClearShipTo();
            return;
        }

        if (locationId is not { } locId || addressId is not { } addrId)
        {
            throw new SourcingRuleException("A location ship-to needs both a location and an address.");
        }

        SetShipToLocation(locId, addrId);
    }

    public PrLine AddLine(
        string itemCode,
        string description,
        decimal qty,
        string uom,
        decimal estUnitPrice,
        Guid? taxCodeId = null)
    {
        RequireDraftable();
        var line = PrLine.Create(Id, itemCode, description, qty, uom, estUnitPrice, taxCodeId);
        _lines.Add(line);
        ResequenceLines();
        RecomputeHeaderStatus();
        UpdatedUtc = DateTime.UtcNow;
        return line;
    }

    /// <summary>
    /// Draft open-line sync (POC parity): Open lines absent from <paramref name="inputs"/> are removed;
    /// matching Open lines are updated; id-less inputs are added; locked lines are kept read-only.
    /// Entry order follows the submitted array; locked-but-omitted lines keep relative order after.
    /// </summary>
    public void SyncOpenLines(IReadOnlyList<PrLineDraftInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        RequireDraftable();

        foreach (var gone in _lines
            .Where(l => l.LifecycleStatus == PrLineStatus.Open && inputs.All(i => i.Id != l.Id))
            .ToList())
        {
            _lines.Remove(gone);
        }

        foreach (var line in _lines.Where(l => l.LifecycleStatus == PrLineStatus.Open))
        {
            var input = inputs.FirstOrDefault(i => i.Id == line.Id);
            if (input is null)
            {
                continue;
            }

            line.UpdateDraft(input.ItemCode, input.Description, input.Qty, input.Uom, input.EstUnitPrice, input.TaxCodeId);
        }

        var submittedPos = new Dictionary<Guid, int>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.Id is { } keptId)
            {
                submittedPos[keptId] = i;
                continue;
            }

            var line = PrLine.Create(Id, input.ItemCode, input.Description, input.Qty, input.Uom, input.EstUnitPrice, input.TaxCodeId);
            submittedPos[line.Id] = i;
            _lines.Add(line);
        }

        _lines.Sort((a, b) =>
            (submittedPos.TryGetValue(a.Id, out var ap) ? ap : int.MaxValue, a.LineSequence)
            .CompareTo((submittedPos.TryGetValue(b.Id, out var bp) ? bp : int.MaxValue, b.LineSequence)));
        ResequenceLines();
        RecomputeHeaderStatus();
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Renumber <see cref="PrLine.LineSequence"/> to exactly 1..n in current list order.</summary>
    public void ResequenceLines()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].SetSequence(i + 1);
        }
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

    /// <summary>InRfq -&gt; Awarded (terminal), on award approval.</summary>
    public PrLineTransition MarkLineAwarded(Guid lineId, DateTime nowUtc)
    {
        var result = GetLine(lineId).MarkAwarded(nowUtc);
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
    /// <remarks>
    /// This overload does not know about direct-order commitments (Phase 3's <see cref="PrLineOrder"/>
    /// ledger, which lives outside this aggregate). Call sites that touch a PR with active order
    /// reservations on any of its lines should use the <see cref="RecomputeHeaderStatus(IReadOnlyDictionary{Guid, decimal})"/>
    /// overload instead, or the ordered dimension can regress silently. Known, bounded gap — see
    /// MIGRATION-PLAN-2.md Phase 3 notes.
    /// </remarks>
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

    /// <summary>
    /// Same recompute, but checks the direct-ordering dimension first — "ordering outranks
    /// sourcing" (Phase 3 / IC14). <paramref name="orderedByLine"/> is the current active
    /// <see cref="PrLineOrder"/> quantity per line id, supplied by the caller (the ledger lives
    /// outside this aggregate). Falls through to the sourcing-only recompute above when no line
    /// has an active order.
    /// </summary>
    public void RecomputeHeaderStatus(IReadOnlyDictionary<Guid, decimal> orderedByLine)
    {
        ArgumentNullException.ThrowIfNull(orderedByLine);

        if (_lines.Count > 0 && _lines.All(l => l.LifecycleStatus == PrLineStatus.Cancelled))
        {
            HeaderStatus = PrHeaderStatus.Cancelled;
            return;
        }

        var active = _lines.Where(l => l.LifecycleStatus != PrLineStatus.Cancelled).ToList();
        decimal Ordered(PrLine l) => orderedByLine.GetValueOrDefault(l.Id);

        if (active.Any(l => Ordered(l) > 0))
        {
            bool Done(PrLine l) => l.LifecycleStatus is PrLineStatus.Awarded or PrLineStatus.Closed || Ordered(l) >= l.Qty;
            HeaderStatus = active.All(Done) ? PrHeaderStatus.Ordered : PrHeaderStatus.PartiallyOrdered;
            return;
        }

        RecomputeHeaderStatus();
    }

    private void RequireDraftable()
    {
        if (HeaderStatus == PrHeaderStatus.Cancelled)
        {
            throw new SourcingRuleException($"Requisition {Code} is cancelled and can no longer be edited.");
        }

        if (Submitted)
        {
            throw new SourcingRuleException($"Requisition {Code} has already been submitted and can no longer be edited.");
        }
    }

    private static void EnsureRequiredOnNotInPast(DateOnly? requiredOn)
    {
        if (requiredOn is null)
        {
            return;
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (requiredOn.Value < todayUtc)
        {
            throw new SourcingRuleException("Required on cannot be a date in the past.");
        }
    }
}

/// <summary>Draft line payload for <see cref="PurchaseRequisition.SyncOpenLines"/>.</summary>
public sealed record PrLineDraftInput(
    Guid? Id,
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    decimal EstUnitPrice,
    Guid? TaxCodeId);
