namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record BidDto(
    Guid Id,
    string Code,
    Guid RfqId,
    Guid VendorId,
    bool Submitted,
    DateTime? SubmittedUtc,
    bool SavedDraft,
    DateTime? WithdrawnUtc,
    string? Lead,
    string? Warranty,
    IReadOnlyList<BidLineDto> Lines,
    IReadOnlyList<BidAnswerDto> Answers,
    IReadOnlyList<string> Files);
