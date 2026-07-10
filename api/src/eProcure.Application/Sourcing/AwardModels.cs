namespace eProcure.Application.Sourcing;

public sealed record AwardVendorOptionDto(Guid VendorId, string VendorName, decimal UnitPrice, decimal OfferedQty);

public sealed record AwardLineDto(
    string LineCode, string Description, decimal RequiredQty, string Uom,
    IReadOnlyList<AwardVendorOptionDto> Options, Guid? RecommendedVendorId);

public sealed record AwardRankRow(
    Guid VendorId, string VendorName, double? TechnicalScore, double PriceScore, double Combined, bool Recommended);

public sealed record AwardResponseDto(Guid VendorId, string VendorName, IReadOnlyList<QaAnswerDto> Answers);

public sealed record AwardEligibilityDto(
    Guid RfqId, string Code, string Title, string Envelope, bool CommercialRevealed, bool TechFinalized, bool Masked,
    IReadOnlyList<AwardLineDto> Lines, IReadOnlyList<AwardRankRow> Ranking,
    IReadOnlyList<QaItemDto> TechnicalQuestions, IReadOnlyList<QaItemDto> CommercialQuestions,
    IReadOnlyList<AwardResponseDto> Responses);

public sealed record AllocationInput(string LineCode, Guid VendorId, decimal Qty);
public sealed record SubmitAwardRequest(IReadOnlyList<AllocationInput> Allocations);

public sealed record AllocationDto(string LineCode, Guid VendorId, string VendorName, decimal Qty, decimal UnitPrice);

public sealed record AwardDto(
    Guid Id, string Code, Guid RfqId, string RfqCode, string Status, string CreatedByUserId,
    decimal TotalValue, string? ApproverUserId, DateTime? ApprovedUtc,
    IReadOnlyList<AllocationDto> Allocations, IReadOnlyList<string> PoCodes);

public interface IAwardService
{
    Task<AwardEligibilityDto?> GetEligibilityAsync(Guid rfqId, CancellationToken ct = default);
    Task<AwardDto?> GetForRfqAsync(Guid rfqId, CancellationToken ct = default);
    Task<IReadOnlyList<AwardDto>> ListAsync(CancellationToken ct = default);
    Task<AwardDto> SubmitForApprovalAsync(Guid rfqId, SubmitAwardRequest req, CancellationToken ct = default);
    Task<AwardDto> ApproveAsync(Guid awardId, CancellationToken ct = default);
}
