using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

/// <summary>
/// {Draft|Verified} -&gt; Cancelled, irreversible. If the PO's source is FromRequisition, this also
/// releases the reserved requisition quantity back via Sourcing.
/// </summary>
public sealed record CancelPurchaseOrderCommand(Guid PoId, string? Reason) : ICommand<PurchaseOrderDto>;
