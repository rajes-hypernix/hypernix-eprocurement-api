namespace FSH.Modules.Sourcing.Contracts.Dtos;

/// <summary>
/// Vendor-facing RFQ shape for the bid form — lines + questions only, no invitation/event data
/// from other vendors. <c>GetRfqByIdQuery</c> is buyer/internal-only (gated by
/// <c>Rfqs.View</c>, which the Vendor role never gets), so this is the vendor-scoped equivalent.
/// </summary>
public sealed record RfqForBiddingDto(
    Guid RfqId,
    string Code,
    string Title,
    string Envelope,
    string Status,
    string Currency,
    DateTime? OpensUtc,
    DateTime? ClosesUtc,
    IReadOnlyList<RfqLineDto> Lines,
    IReadOnlyList<FormItemDto> FormItems,
    IReadOnlyList<string> TechnicalSections,
    IReadOnlyList<string> CommercialSections);
