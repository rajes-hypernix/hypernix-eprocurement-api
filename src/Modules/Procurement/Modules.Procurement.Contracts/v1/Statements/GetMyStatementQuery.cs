using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Statements;

public sealed record GetMyStatementQuery : IQuery<StatementDetailDto?>;
