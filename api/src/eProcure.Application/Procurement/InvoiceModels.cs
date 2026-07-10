namespace eProcure.Application.Procurement;

public sealed record InvoiceLineDto(
    string ItemCode, string Description, decimal Qty, decimal ReceivedQty, decimal UnitPrice, decimal PoUnitPrice,
    bool QtyOk, bool PriceOk, decimal LineTotal);

public sealed record InvoiceBillablePlanLine(
    string ItemCode, string Description, decimal ReceivedQty, decimal AlreadyInvoiced, decimal Billable, decimal PoUnitPrice, string Uom);

public sealed record InvoiceBillablePlan(Guid PoId, string PoCode, IReadOnlyList<InvoiceBillablePlanLine> Lines);

public sealed record InvoiceListDto(
    Guid Id, string Code, string PoCode, string VendorName, string InvoiceNo, string Status, string MatchStatus,
    decimal Subtotal, decimal Total, bool Payable);

public sealed record InvoiceDetailDto(
    Guid Id, string Code, Guid PoId, string PoCode, string VendorName, string InvoiceNo, string Date,
    string Status, string MatchStatus, string? ExceptionReason, string? NsId, decimal WhtRate,
    decimal Subtotal, decimal Sst, decimal Wht, decimal Total, bool Payable,
    IReadOnlyList<InvoiceLineDto> Lines);

public sealed record CreateInvoiceLine(string ItemCode, decimal Qty, decimal UnitPrice);
public sealed record SubmitInvoiceRequest(string InvoiceNo, string Date, decimal WhtRate, IReadOnlyList<CreateInvoiceLine> Lines);

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceListDto>> ListAsync(CancellationToken ct = default);
    Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceBillablePlan> GetBillablePlanAsync(Guid poId, CancellationToken ct = default);
    Task<InvoiceDetailDto> SubmitAsync(Guid poId, SubmitInvoiceRequest req, CancellationToken ct = default);
    Task<InvoiceDetailDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDetailDto> ResolveExceptionAsync(Guid id, CancellationToken ct = default);
}
