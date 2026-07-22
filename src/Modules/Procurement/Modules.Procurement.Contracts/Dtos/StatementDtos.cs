namespace FSH.Modules.Procurement.Contracts.Dtos;

public sealed record StatementSummaryDto(
    Guid VendorId,
    string VendorName,
    decimal Invoiced,
    decimal Paid,
    decimal Balance,
    decimal Grni);

public sealed record AgingDto(decimal Current, decimal D30, decimal D60, decimal D90);

public sealed record LedgerEntryDto(
    string Date,
    string Reference,
    string Type,
    decimal? Memo,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public sealed record StatementDetailDto(
    Guid VendorId,
    string VendorName,
    decimal Invoiced,
    decimal Paid,
    decimal Balance,
    decimal Grni,
    AgingDto Aging,
    IReadOnlyList<LedgerEntryDto> Ledger);
