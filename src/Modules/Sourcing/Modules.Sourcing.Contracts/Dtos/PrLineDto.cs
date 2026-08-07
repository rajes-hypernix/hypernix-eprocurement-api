namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record PrLineDto(
    Guid Id,
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    decimal EstUnitPrice,
    string LifecycleStatus,
    string? Ref,
    int LineSequence,
    Guid? TaxCodeId = null,
    string? TaxCode = null,
    decimal? TaxRatePct = null,
    decimal SstAmount = 0m,
    decimal EstAmount = 0m);

/// <summary>
/// Create/update line input. <see cref="Id"/> null = new line; set = keep/update existing Open line.
/// </summary>
public sealed record PrLineInput(
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    decimal EstUnitPrice,
    Guid? Id = null,
    Guid? TaxCodeId = null);
