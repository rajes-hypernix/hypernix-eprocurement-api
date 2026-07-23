using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Cities;

public sealed record ListCitiesQuery(Guid StateId, bool ActiveOnly = true) : IQuery<IReadOnlyList<CityDto>>;

public sealed record CreateCityCommand(Guid StateId, string Name) : ICommand<Guid>;

public sealed record UpdateCityCommand(Guid Id, string Name) : ICommand<Guid>;

public sealed record SetCityActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed record DeleteCityCommand(Guid Id) : ICommand<Guid>;
