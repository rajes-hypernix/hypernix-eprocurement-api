namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record RfqListItemDto(
    Guid Id,
    string Code,
    string Title,
    string Envelope,
    string Status,
    string Currency,
    DateTime? ClosesUtc,
    int InvitedCount,
    int LineCount,
    int BidCount);

