using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record CreatePurchaseOrderFromRequisitionLineInput(Guid PrLineId, decimal Qty, decimal UnitPrice, bool PriceConfirmed = false);

/// <summary>
/// PR-direct creation (no RFQ/award) — the buyer picks the vendor and prices the selected Open
/// PR lines. Reserves the requested quantity against the requisition (IC14/IC15) via Sourcing's
/// reservation ledger before the PO itself is created; a failed save releases the reservation.
/// </summary>
public sealed record CreatePurchaseOrderFromRequisitionCommand(
    Guid PrId,
    Guid VendorId,
    string Currency,
    IReadOnlyList<CreatePurchaseOrderFromRequisitionLineInput> Lines) : ICommand<Guid>;
