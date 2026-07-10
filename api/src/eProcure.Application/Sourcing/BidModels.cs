namespace eProcure.Application.Sourcing;

public sealed record BidLineDto(string ItemCode, bool Bidding, decimal Price, decimal Qty, bool Partial, string? AltItem);
public sealed record BidAnswerDto(int QuestionOrder, string Value);

public sealed record BidDto(
    Guid Id, string Code, Guid RfqId, Guid VendorId, bool Submitted, bool SavedDraft,
    DateTime? SubmittedUtc, int Lead, int Warranty,
    IReadOnlyList<BidLineDto> Lines, IReadOnlyList<BidAnswerDto> Answers, IReadOnlyList<string> Files);

public sealed record SaveBidRequest(
    int Lead, int Warranty,
    IReadOnlyList<BidLineDto> Lines,
    IReadOnlyList<BidAnswerDto> Answers,
    IReadOnlyList<string> Files);

/// <summary>Buyer/evaluator-facing bid summary (no commercial data unless the gate is open).</summary>
public sealed record BidSummaryDto(
    Guid VendorId, string VendorDisplayName, bool Submitted, DateTime? SubmittedUtc, int FileCount);

public sealed record InvitationDto(
    Guid RfqId, string Code, string Title, string Envelope, string Status, string Currency,
    DateTime? ClosesUtc, bool BidSubmitted, bool BidDraft, string? OwnerUserId, string? OwnerName,
    // The vendor's own invitation status (Slice J: surfaced in the invitation list so a Declined
    // vendor sees their state). Appended (default null) to preserve positional callers.
    string? InvitationStatus = null);

public interface IBidService
{
    /// <summary>RFQs the current vendor principal is invited to, with its bid status.</summary>
    Task<IReadOnlyList<InvitationDto>> ListMyInvitationsAsync(CancellationToken ct = default);
    /// <summary>The current vendor principal's bid for an RFQ (own vendor only).</summary>
    Task<BidDto?> GetMyBidAsync(Guid rfqId, CancellationToken ct = default);
    Task<BidDto> SaveDraftAsync(Guid rfqId, SaveBidRequest req, CancellationToken ct = default);
    Task<BidDto> SubmitAsync(Guid rfqId, SaveBidRequest req, CancellationToken ct = default);
}
