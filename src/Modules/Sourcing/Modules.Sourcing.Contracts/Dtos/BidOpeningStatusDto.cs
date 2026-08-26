namespace FSH.Modules.Sourcing.Contracts.Dtos;

public sealed record BidOpeningStatusDto(
    Guid RfqId,
    string Code,
    string Title,
    string Envelope,
    string RfqStatus,
    bool TechnicalOpened,
    bool TechFinalized,
    bool CommercialOpened,
    int InvitedCount,
    int SubmittedBidCount,
    IReadOnlyList<EvaluatorDto> Evaluators,
    bool CanOpenTechnical,
    bool CanOpenCommercial);
