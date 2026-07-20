namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record RfqEventDto(
    Guid Id,
    string EventType,
    Guid? VendorId,
    string? ActorUserId,
    string? ReasonCode,
    string? ReasonNote,
    DateTime? OldClosesUtc,
    DateTime? NewClosesUtc,
    DateTime OccurredUtc);
