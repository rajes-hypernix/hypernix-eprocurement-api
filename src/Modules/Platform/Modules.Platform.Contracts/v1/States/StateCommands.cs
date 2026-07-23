using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.States;

public sealed record ListStatesQuery(Guid CountryId, bool ActiveOnly = true) : IQuery<IReadOnlyList<StateDto>>;

public sealed record CreateStateCommand(Guid CountryId, string Code, string Name) : ICommand<Guid>;

public sealed record UpdateStateCommand(Guid Id, string Code, string Name) : ICommand<Guid>;

public sealed record SetStateActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed record DeleteStateCommand(Guid Id) : ICommand<Guid>;
