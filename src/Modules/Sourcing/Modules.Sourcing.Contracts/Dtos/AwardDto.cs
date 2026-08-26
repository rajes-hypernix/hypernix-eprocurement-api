namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record AwardDto(
    Guid Id,
    string Code,
    Guid RfqId,
    string Status,
    string CreatedByUserId,
    string? ApproverUserId,
    DateTime? ApprovedUtc,
    decimal TotalValue,
    IReadOnlyList<AwardAllocationDto> Allocations,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record AwardListItemDto(
    Guid Id,
    string Code,
    Guid RfqId,
    string RfqCode,
    string Status,
    decimal TotalValue,
    DateTime CreatedUtc,
    string? ApproverUserId = null);
