namespace FSH.Modules.Procurement.Contracts.Dtos;

public sealed record PurchaseOrderDto(
    Guid Id,
    string Code,
    Guid AwardId,
    Guid RfqId,
    Guid VendorId,
    string Status,
    string Currency,
    decimal TotalValue,
    IReadOnlyList<PoLineDto> Lines,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record PoLineDto(
    Guid Id,
    string ItemCode,
    string Description,
    string Uom,
    decimal Qty,
    decimal UnitPrice,
    decimal ReceivedQty,
    decimal InvoicedQty,
    string RfqLineCode);

public sealed record PurchaseOrderListItemDto(
    Guid Id,
    string Code,
    Guid RfqId,
    Guid VendorId,
    string Status,
    string Currency,
    decimal TotalValue,
    DateTime CreatedUtc);
