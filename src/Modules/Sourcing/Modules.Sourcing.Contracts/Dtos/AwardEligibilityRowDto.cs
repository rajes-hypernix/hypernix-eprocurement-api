namespace FSH.Modules.Sourcing.Contracts.Dtos;

/// <summary>
/// One vendor's award eligibility. <see cref="Masked"/> is true until commercial reveal —
/// real identity stays sealed for every caller until the commercial envelope opens.
/// </summary>
public sealed record AwardEligibilityRowDto(
    Guid VendorId,
    string? VendorName,
    string? VendorCode,
    string Alias,
    bool Masked,
    bool Eligible,
    decimal? CommitteeScore,
    bool TechnicallyPassed);

public sealed record AwardVendorOptionDto(
    Guid VendorId,
    string VendorName,
    decimal UnitPrice,
    decimal OfferedQty);

public sealed record AwardCompareLineDto(
    string LineCode,
    string ItemCode,
    string Description,
    decimal RequiredQty,
    string Uom,
    IReadOnlyList<AwardVendorOptionDto> Options,
    Guid? RecommendedVendorId);

public sealed record AwardRankRowDto(
    Guid VendorId,
    string VendorName,
    decimal? TechnicalScore,
    double PriceScore,
    double Combined,
    bool Recommended);

public sealed record AwardQaItemDto(
    int Order,
    string Label,
    string Type,
    string? ConfigJson,
    string Group);

public sealed record AwardQaAnswerDto(int QuestionOrder, string Value);

public sealed record AwardResponseDto(
    Guid VendorId,
    string VendorName,
    IReadOnlyList<AwardQaAnswerDto> Answers);

/// <summary>
/// Commercial compare / award workspace payload — line×vendor prices, ranking, and questionnaire responses.
/// </summary>
public sealed record AwardEligibilityDto(
    Guid RfqId,
    string Code,
    string Title,
    string Envelope,
    string Currency,
    bool CommercialRevealed,
    bool TechFinalized,
    bool Masked,
    IReadOnlyList<AwardCompareLineDto> Lines,
    IReadOnlyList<AwardRankRowDto> Ranking,
    IReadOnlyList<AwardEligibilityRowDto> Vendors,
    IReadOnlyList<AwardQaItemDto> TechnicalQuestions,
    IReadOnlyList<AwardQaItemDto> CommercialQuestions,
    IReadOnlyList<AwardResponseDto> Responses);
