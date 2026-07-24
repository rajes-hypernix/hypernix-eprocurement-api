namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record EligibleOrderLineDto(
    Guid PrId,
    string PrCode,
    Guid PrLineId,
    string ItemCode,
    string Description,
    decimal Qty,
    string Uom,
    decimal EstUnitPrice,
    decimal QtyOrdered,
    decimal QtyRemaining,
    bool Sourceable,
    string? IneligibleReason);
