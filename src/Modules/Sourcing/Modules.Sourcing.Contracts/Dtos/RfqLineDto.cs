namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record RfqLineDto(
    string LineCode,
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    string? PrRef,
    IReadOnlyList<string> SourcePrLineIds);

public sealed record RfqLineInput(
    string LineCode,
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    string? PrRef,
    IReadOnlyList<string>? SourcePrLineIds);
