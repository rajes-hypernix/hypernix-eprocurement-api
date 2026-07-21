using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Grns;

public sealed record ReceiveAsnCommand(Guid AsnId, IReadOnlyList<ReceiveLineInput> Lines) : ICommand<GrnDto>;

public sealed record ReceiveLineInput(string ItemCode, decimal ReceivedQty);
