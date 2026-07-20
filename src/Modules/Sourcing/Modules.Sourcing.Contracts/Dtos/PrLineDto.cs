namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record PrLineDto(
    Guid Id,
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    decimal EstUnitPrice,
    string LifecycleStatus,
    string? Ref);

public sealed record PrLineInput(string ItemCode, string Description, decimal Qty, string Uom, decimal EstUnitPrice);
