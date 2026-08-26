namespace FSH.Modules.Procurement.Contracts.Dtos;

public sealed record AsnDto(
    Guid Id,
    string Code,
    Guid PoId,
    string Status,
    string Carrier,
    string TrackingNo,
    DateOnly? ShippedDate,
    DateOnly? ExpectedDate,
    IReadOnlyList<AsnLineDto> Lines,
    DateTime CreatedUtc,
    string? PoCode = null,
    string? VendorName = null);

public sealed record AsnLineDto(
    Guid Id,
    string ItemCode,
    decimal ShippedQty,
    string? LotNo,
    string? Description = null,
    string? Uom = null);
