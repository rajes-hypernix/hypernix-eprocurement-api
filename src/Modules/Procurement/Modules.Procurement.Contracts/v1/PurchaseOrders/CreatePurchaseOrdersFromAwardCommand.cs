using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;

public sealed record CreatePurchaseOrdersFromAwardCommand(Guid RfqId) : ICommand<IReadOnlyList<Guid>>;
