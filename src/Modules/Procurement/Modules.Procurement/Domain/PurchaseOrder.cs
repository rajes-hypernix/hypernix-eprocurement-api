using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class PurchaseOrder : AggregateRoot<Guid>
{
    private readonly List<PoLine> _lines = [];

    public string Code { get; private set; } = default!;

    /// <summary>Set only when <see cref="SourceKind"/> is FromAward.</summary>
    public Guid? AwardId { get; private set; }

    /// <summary>Set only when <see cref="SourceKind"/> is FromAward.</summary>
    public Guid? RfqId { get; private set; }

    /// <summary>Set only when <see cref="SourceKind"/> is FromRequisition.</summary>
    public Guid? SourcePrId { get; private set; }

    public PoSourceKind SourceKind { get; private set; } = PoSourceKind.FromAward;

    public Guid VendorId { get; private set; }
    public PoStatus Status { get; private set; } = PoStatus.Draft;
    public string Currency { get; private set; } = default!;

    public Guid? ShipToLocationId { get; private set; }
    public Guid? ShipToAddressId { get; private set; }
    public string? ShipToAdhoc { get; private set; }

    public string? IncotermCode { get; private set; }
    public string? IncotermSuffix { get; private set; }

    /// <summary>Platform Incoterm master id (no cross-DB FK — mirrors RFQ / TaxCodeId pattern).</summary>
    public Guid? IncotermId { get; private set; }
    public string? Memo { get; private set; }
    public string? VendorRef { get; private set; }
    public DateOnly? RequiredDate { get; private set; }
    public DateOnly? DeliveryDate { get; private set; }

    public DateTime? VerifiedUtc { get; private set; }
    public DateTime? IssuedUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    /// <summary>Opaque entry-form layout choice (Phase 5/6) — mirrors the same stub on <c>PurchaseRequisition</c>.</summary>
    public Guid? EntryFormId { get; private set; }

    public IReadOnlyList<PoLine> Lines => _lines;

    public decimal TotalValue => _lines.Sum(l => l.Qty * l.UnitPrice);

    public bool HasShipTo =>
        (ShipToLocationId is not null && ShipToAddressId is not null) || !string.IsNullOrWhiteSpace(ShipToAdhoc);

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        string code,
        Guid vendorId,
        string currency,
        PoSourceKind sourceKind,
        Guid? awardId = null,
        Guid? rfqId = null,
        Guid? sourcePrId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var now = DateTime.UtcNow;
        return new PurchaseOrder
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            AwardId = awardId,
            RfqId = rfqId,
            SourcePrId = sourcePrId,
            SourceKind = sourceKind,
            VendorId = vendorId,
            Currency = currency,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public PoLine AddLine(
        string itemCode,
        string description,
        string uom,
        decimal qty,
        decimal unitPrice,
        string? rfqLineCode = null,
        Guid? sourcePrLineId = null,
        Guid? taxCodeId = null,
        bool priceConfirmed = false)
    {
        var line = PoLine.Create(Id, itemCode, description, uom, qty, unitPrice, rfqLineCode, sourcePrLineId, taxCodeId, priceConfirmed);
        _lines.Add(line);
        return line;
    }

    public void SetShipToLocation(Guid locationId, Guid addressId)
    {
        ShipToLocationId = locationId;
        ShipToAddressId = addressId;
        ShipToAdhoc = null;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetShipToAdhoc(string? text)
    {
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmed is { Length: > 400 })
        {
            throw new ProcurementRuleException("An ad-hoc ship-to address is at most 400 characters.");
        }

        ShipToAdhoc = trimmed;
        ShipToLocationId = null;
        ShipToAddressId = null;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetEntryForm(Guid? entryFormId)
    {
        EntryFormId = entryFormId;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetDetails(
        Guid? incotermId, string? incotermCode, string? incotermSuffix, string? memo, string? vendorRef,
        DateOnly? requiredDate, DateOnly? deliveryDate)
    {
        IncotermId = incotermId;
        IncotermCode = string.IsNullOrWhiteSpace(incotermCode) ? null : incotermCode.Trim().ToUpperInvariant();
        IncotermSuffix = string.IsNullOrWhiteSpace(incotermSuffix) ? null : incotermSuffix.Trim();
        Memo = memo;
        VendorRef = vendorRef;
        RequiredDate = requiredDate;
        DeliveryDate = deliveryDate;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Draft-only line edit — the only way to resolve IC16's "confirm the unit price" gap once a
    /// line already exists. Mirrors the old source's rule exactly: editing the price to a different
    /// value confirms it; an explicit <paramref name="priceConfirmed"/> flag confirms without a
    /// change (accepting an estimate as-is); confirmation is one-way — a kept-confirmed line stays
    /// confirmed even if this is called again with <c>priceConfirmed: false</c> and no price change.
    /// </summary>
    public void UpdateLinePrice(Guid lineId, decimal? unitPrice, bool priceConfirmed)
    {
        if (Status != PoStatus.Draft)
        {
            throw new ProcurementRuleException($"PO {Code} is {Status} — lines can only be edited while Draft.");
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new ProcurementRuleException($"Line {lineId} not found on PO {Code}.");

        line.UpdatePrice(unitPrice, priceConfirmed);
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Draft -&gt; Verified. Throws with the itemized gap list if any required field is missing.</summary>
    public void Verify(DateTime nowUtc)
    {
        if (Status != PoStatus.Draft)
        {
            throw new ProcurementRuleException($"PO {Code} is {Status} — only a Draft PO can be verified.");
        }

        var gaps = VerificationGaps();
        if (gaps.Count > 0)
        {
            throw new ProcurementRuleException(
                $"PO {Code} cannot be verified — {gaps.Count} gap(s): {string.Join("; ", gaps)}");
        }

        Status = PoStatus.Verified;
        VerifiedUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }

    /// <summary>Verified -&gt; Draft.</summary>
    public void ReopenDraft()
    {
        if (Status != PoStatus.Verified)
        {
            throw new ProcurementRuleException($"PO {Code} is {Status} — only a Verified PO can be reopened to Draft.");
        }

        Status = PoStatus.Draft;
        VerifiedUtc = null;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// {Draft|Verified} -&gt; Cancelled, irreversible. If this PO's <see cref="SourceKind"/> is
    /// FromRequisition, the caller (handler) is responsible for releasing the requisition's
    /// reserved quantity back via Sourcing's ReleaseRequisitionQuantityCommand — the domain has no
    /// way to reach across the module boundary itself.
    /// </summary>
    public void Cancel(DateTime nowUtc)
    {
        if (Status is not (PoStatus.Draft or PoStatus.Verified))
        {
            throw new ProcurementRuleException($"PO {Code} is {Status} — only a Draft or Verified PO can be cancelled.");
        }

        Status = PoStatus.Cancelled;
        UpdatedUtc = nowUtc;
    }

    /// <summary>Manual admin action, no computed gate — an administrative "done with this PO" marker.</summary>
    public void Close(DateTime nowUtc)
    {
        if (Status is not (PoStatus.Matched or PoStatus.Received or PoStatus.Discrepancy))
        {
            throw new ProcurementRuleException($"PO {Code} is {Status} — only a Matched, Received, or Discrepancy PO can be closed.");
        }

        Status = PoStatus.Closed;
        UpdatedUtc = nowUtc;
    }

    /// <summary>
    /// IC16 issue gate: Verified -&gt; Issued only. Re-runs <see cref="VerificationGaps"/> as a
    /// safety net (a line could have been mutated between Verify and Issue) so Verify and Issue can
    /// never disagree on what's outstanding.
    /// </summary>
    public void Issue()
    {
        if (Status == PoStatus.Draft)
        {
            throw new ProcurementRuleException($"PO {Code} must be verified before it can be issued.");
        }

        if (Status != PoStatus.Verified)
        {
            throw new ProcurementRuleException($"Cannot issue a PO that is {Status}.");
        }

        var gaps = VerificationGaps();
        if (gaps.Count > 0)
        {
            throw new ProcurementRuleException(
                $"PO {Code} cannot be issued — {gaps.Count} gap(s): {string.Join("; ", gaps)}");
        }

        Status = PoStatus.Issued;
        IssuedUtc = DateTime.UtcNow;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Acknowledge()
    {
        if (Status != PoStatus.Issued)
        {
            throw new ProcurementRuleException($"Cannot acknowledge a PO that is {Status}.");
        }

        Status = PoStatus.Acknowledged;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ApplyReceivedQty(string itemCode, decimal qty)
    {
        var line = _lines.FirstOrDefault(l => string.Equals(l.ItemCode, itemCode, StringComparison.Ordinal))
            ?? throw new ProcurementRuleException($"Line {itemCode} not found on PO {Code}.");
        line.AddReceivedQty(qty);
        RecomputeReceiptStatus();
    }

    public void RecordReceipt()
    {
        RecomputeReceiptStatus();
    }

    public void MarkDiscrepancy()
    {
        Status = PoStatus.Discrepancy;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void MarkMatched()
    {
        Status = PoStatus.Matched;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ClearDiscrepancy()
    {
        RecomputeReceiptStatus();
    }

    /// <summary>
    /// Reused by both <see cref="Verify"/> and <see cref="Issue"/> (once Step 4 extends Issue's
    /// gate) so they can never disagree on what's outstanding. IC16: an unconfirmed line price or a
    /// missing ship-to address blocks progress past Draft.
    /// </summary>
    private List<string> VerificationGaps()
    {
        var gaps = new List<string>();

        if (VendorId == Guid.Empty)
        {
            gaps.Add("Vendor is required");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            gaps.Add("Currency is required");
        }

        if (_lines.Count == 0)
        {
            gaps.Add("At least one line is required");
        }

        if (!HasShipTo)
        {
            gaps.Add("Ship-to address is required");
        }

        foreach (var line in _lines)
        {
            var label = string.IsNullOrWhiteSpace(line.ItemCode) ? "(line)" : line.ItemCode;
            if (string.IsNullOrWhiteSpace(line.ItemCode))
            {
                gaps.Add($"{label}: item code is required");
            }

            if (string.IsNullOrWhiteSpace(line.Description))
            {
                gaps.Add($"{label}: description is required");
            }

            if (line.Qty <= 0)
            {
                gaps.Add($"{label}: quantity must be positive");
            }

            if (line.UnitPrice <= 0)
            {
                gaps.Add($"{label}: unit price must be positive");
            }
            else if (!line.PriceConfirmed)
            {
                gaps.Add($"{label}: confirm the unit price before issue");
            }
        }

        return gaps;
    }

    private void RecomputeReceiptStatus()
    {
        bool allReceived = _lines.All(l => l.ReceivedQty >= l.Qty);
        Status = allReceived ? PoStatus.Received : PoStatus.PartiallyReceived;
        UpdatedUtc = DateTime.UtcNow;
    }
}
