namespace FSH.Modules.Sourcing.Contracts.Dtos;

/// <summary>
/// One vendor's award eligibility. <see cref="Masked"/> is a temporal gate on the RFQ itself
/// (true until <c>Rfq.CommercialRevealed</c>) — real identity and pricing stay sealed for every
/// caller, buyer and approver included, until the commercial envelope opens.
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
