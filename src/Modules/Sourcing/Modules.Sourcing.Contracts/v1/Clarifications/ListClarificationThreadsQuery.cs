using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Clarifications;

public sealed record ListClarificationThreadsQuery : IQuery<IReadOnlyList<ClarificationThreadDto>>;
