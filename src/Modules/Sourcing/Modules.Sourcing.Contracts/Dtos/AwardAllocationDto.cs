namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record AwardAllocationDto(string RfqLineCode, Guid VendorId, decimal Qty, decimal UnitPrice);
