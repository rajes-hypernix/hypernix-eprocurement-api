using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

public sealed record UpdateRfqDraftCommand(
    Guid RfqId,
    string Title,
    string Envelope,
    string Currency,
    DateTime? OpensUtc,
    DateTime? ClosesUtc,
    IReadOnlyList<RfqLineInput> Lines,
    IReadOnlyList<FormItemDto> FormItems,
    IReadOnlyList<string> TechnicalSections,
    IReadOnlyList<string> CommercialSections,
    IReadOnlyList<string> TechnicalEvaluatorIds,
    IReadOnlyList<string> CommercialEvaluatorIds,
    DateTime? ClarificationDeadlineUtc = null,
    int? BidValidityDays = null,
    bool PartialBidsAllowed = true,
    Guid? IncotermId = null,
    string? IncotermCode = null,
    string? IncotermSuffix = null) : ICommand<Guid>;
