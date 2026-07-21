namespace FSH.Modules.Sourcing.Contracts.Dtos;

/// <summary>One row in the vendor portal's "my RFQs" list — the invitation joined with the vendor's own bid status.</summary>
public sealed record MyInvitationDto(
    Guid RfqId,
    string RfqCode,
    string Title,
    string Envelope,
    string RfqStatus,
    string InvitationStatus,
    DateTime? OpensUtc,
    DateTime? ClosesUtc,
    bool HasSubmittedBid);
