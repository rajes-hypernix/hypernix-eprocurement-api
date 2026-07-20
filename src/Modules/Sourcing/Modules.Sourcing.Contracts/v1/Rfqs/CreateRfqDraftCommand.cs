using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

public sealed record CreateRfqDraftCommand(
    string? Title,
    string Envelope,
    string Currency,
    IReadOnlyList<string> PrRefs,
    IReadOnlyList<RfqLineInput> Lines) : ICommand<Guid>;
