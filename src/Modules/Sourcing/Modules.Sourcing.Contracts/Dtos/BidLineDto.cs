namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record BidLineDto(
    string ItemCode,
    bool Bidding,
    decimal Price,
    decimal Qty,
    bool Partial,
    string? AltItem);
