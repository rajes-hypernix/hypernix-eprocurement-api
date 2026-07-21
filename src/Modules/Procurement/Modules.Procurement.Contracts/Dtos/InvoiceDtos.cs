namespace FSH.Modules.Procurement.Contracts.Dtos;

public sealed record InvoiceDto(
    Guid Id,
    string Code,
    Guid PoId,
    Guid? GrnId,
    string InvoiceNo,
    string Status,
    DateOnly? InvoiceDate,
    decimal Subtotal,
    decimal SstAmount,
    decimal WhtAmount,
    decimal Total,
    decimal SstRate,
    decimal WhtRate,
    string? ExceptionReason,
    IReadOnlyList<InvoiceLineDto> Lines,
    DateTime CreatedUtc);

public sealed record InvoiceLineDto(
    Guid Id,
    string ItemCode,
    decimal Qty,
    decimal UnitPrice,
    decimal LineTotal);
