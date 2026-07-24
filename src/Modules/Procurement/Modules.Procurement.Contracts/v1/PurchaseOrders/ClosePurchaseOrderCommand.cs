using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

/// <summary>Manual admin action — no computed gate, an administrative "done with this PO" marker.</summary>
public sealed record ClosePurchaseOrderCommand(Guid PoId) : ICommand<PurchaseOrderDto>;
