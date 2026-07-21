namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record BidOpeningStatusDto(
    Guid RfqId,
    string Envelope,
    string RfqStatus,
    bool TechnicalOpened,
    bool TechFinalized,
    bool CommercialOpened,
    int InvitedCount,
    int SubmittedBidCount);
