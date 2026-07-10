namespace eProcure.Application.Procurement;

public sealed record StatementSummaryDto(
    Guid VendorId, string VendorName, decimal Invoiced, decimal Paid, decimal Balance, decimal Grni);

public sealed record AgingDto(decimal Current, decimal D30, decimal D60, decimal D90);

public sealed record LedgerEntryDto(string Date, string Reference, string Type, decimal? Memo, decimal Debit, decimal Credit, decimal Balance);

public sealed record StatementDetailDto(
    Guid VendorId, string VendorName, decimal Invoiced, decimal Paid, decimal Balance, decimal Grni,
    AgingDto Aging, IReadOnlyList<LedgerEntryDto> Ledger);

public interface IStatementService
{
    /// <summary>Per-vendor statement summaries (buyer view).</summary>
    Task<IReadOnlyList<StatementSummaryDto>> ListAsync(CancellationToken ct = default);
    /// <summary>One vendor's statement; vendor principals may only read their own ([G]).</summary>
    Task<StatementDetailDto?> GetAsync(Guid vendorId, CancellationToken ct = default);
    /// <summary>The current vendor principal's own statement.</summary>
    Task<StatementDetailDto?> GetMineAsync(CancellationToken ct = default);
}
