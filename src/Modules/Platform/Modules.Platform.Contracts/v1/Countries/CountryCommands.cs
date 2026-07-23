using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Countries;

public sealed record ListCountriesQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<CountryDto>>;

public sealed record CreateCountryCommand(string Code, string Name) : ICommand<Guid>;

public sealed record SetCountryActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;

public sealed record UpdateCountryCommand(Guid Id, string Code, string Name) : ICommand<Guid>;

public sealed record DeleteCountryCommand(Guid Id) : ICommand<Guid>;
