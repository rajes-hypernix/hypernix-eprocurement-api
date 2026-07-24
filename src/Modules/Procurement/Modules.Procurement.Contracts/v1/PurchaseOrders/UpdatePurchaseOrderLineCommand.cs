using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

/// <summary>
/// Draft-only line edit — the only way to resolve IC16's "confirm the unit price" gap once a line
/// already exists (price can only be set at creation time otherwise). Editing the price to a
/// different value confirms it; an explicit <see cref="PriceConfirmed"/> flag confirms without a
/// change.
/// </summary>
public sealed record UpdatePurchaseOrderLineCommand(
    Guid PoId,
    Guid LineId,
    decimal? UnitPrice,
    bool PriceConfirmed) : ICommand<PurchaseOrderDto>;
