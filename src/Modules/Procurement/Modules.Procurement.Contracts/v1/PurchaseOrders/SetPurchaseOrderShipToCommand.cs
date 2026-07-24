using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

/// <summary>
/// Either a Location + one of its addresses, or a free-text ad-hoc address — never both. Supplying
/// <paramref name="LocationId"/>/<paramref name="AddressId"/> clears any ad-hoc text and vice versa.
/// </summary>
public sealed record SetPurchaseOrderShipToCommand(
    Guid PoId,
    Guid? LocationId,
    Guid? AddressId,
    string? Adhoc) : ICommand<PurchaseOrderDto>;
