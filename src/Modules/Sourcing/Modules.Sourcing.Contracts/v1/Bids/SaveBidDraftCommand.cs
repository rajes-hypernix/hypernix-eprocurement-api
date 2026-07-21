using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Bids;

/// <summary>VendorId is never a client-supplied field — it's derived server-side from the caller's vendorId claim.</summary>
public sealed record SaveBidDraftCommand(
    Guid RfqId,
    string? Lead,
    string? Warranty,
    IReadOnlyList<BidLineDto> Lines,
    IReadOnlyList<BidAnswerDto> Answers,
    IReadOnlyList<string> Files) : ICommand<BidDto>;
