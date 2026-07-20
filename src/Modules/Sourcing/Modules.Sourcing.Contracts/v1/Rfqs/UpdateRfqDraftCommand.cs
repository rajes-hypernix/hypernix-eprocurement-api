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
    IReadOnlyList<string> CommercialSections) : ICommand<Guid>;
