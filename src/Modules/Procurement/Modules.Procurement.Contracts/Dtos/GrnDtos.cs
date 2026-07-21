namespace FSH.Modules.Procurement.Contracts.Dtos;

public sealed record GrnDto(
    Guid Id,
    string Code,
    Guid AsnId,
    Guid PoId,
    IReadOnlyList<GrnLineDto> Lines,
    DateTime CreatedUtc);

public sealed record GrnLineDto(
    Guid Id,
    string ItemCode,
    decimal ExpectedQty,
    decimal ReceivedQty,
    string Condition);
