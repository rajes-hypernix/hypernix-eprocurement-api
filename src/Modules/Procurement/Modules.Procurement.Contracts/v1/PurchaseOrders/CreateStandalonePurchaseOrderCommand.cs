using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record CreateStandalonePurchaseOrderLineInput(string ItemCode, string Description, string Uom, decimal Qty, decimal UnitPrice);

/// <summary>Standalone creation — no provenance at all; every field user-entered.</summary>
public sealed record CreateStandalonePurchaseOrderCommand(
    Guid VendorId,
    string Currency,
    IReadOnlyList<CreateStandalonePurchaseOrderLineInput> Lines) : ICommand<Guid>;
