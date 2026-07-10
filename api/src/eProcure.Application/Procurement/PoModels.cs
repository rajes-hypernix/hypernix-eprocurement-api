namespace eProcure.Application.Procurement;

public sealed record PoLineDto(
    string ItemCode, string Description, decimal Qty, string Uom, decimal UnitPrice,
    decimal ReceivedQty, decimal InvoicedQty, decimal LineTotal);

public sealed record PoListItem(
    Guid Id, string Code, Guid VendorId, string VendorName, string? RfqCode, string Status,
    decimal Total, decimal ReceivedQty, decimal TotalQty, bool Acknowledged, string? NsId);

public sealed record PoDetail(
    Guid Id, string Code, Guid VendorId, string VendorName, string? RfqCode, string? AwardCode,
    string Status, string Currency, string Incoterm, string? NsId, bool Acknowledged, decimal Total,
    IReadOnlyList<PoLineDto> Lines);

public interface IPoService
{
    /// <summary>POs scoped to the principal — a vendor sees only its own (BUSINESS-RULES [G]).</summary>
    Task<IReadOnlyList<PoListItem>> ListAsync(CancellationToken ct = default);
    Task<PoDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PoDetail> IssueAsync(Guid id, CancellationToken ct = default);
    Task<PoDetail> AcknowledgeAsync(Guid id, CancellationToken ct = default);
}
