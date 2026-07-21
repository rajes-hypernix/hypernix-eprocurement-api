using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Asns;

public sealed record CreateAsnCommand(
    Guid PoId,
    string Carrier,
    string TrackingNo,
    DateOnly? ShippedDate,
    DateOnly? ExpectedDate,
    IReadOnlyList<AsnLineInput> Lines) : ICommand<AsnDto>;

public sealed record AsnLineInput(string ItemCode, decimal ShippedQty, string? LotNo);
