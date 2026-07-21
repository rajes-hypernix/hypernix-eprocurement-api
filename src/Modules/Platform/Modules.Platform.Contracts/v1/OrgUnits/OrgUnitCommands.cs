using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.OrgUnits;

public sealed record ListOrgUnitsQuery(string? Type = null, bool ActiveOnly = true)
    : IQuery<IReadOnlyList<OrgUnitDto>>;

public sealed record GetOrgCatalogQuery(bool ActiveOnly = true) : IQuery<OrgCatalogDto>;

public sealed record CreateOrgUnitCommand(string Code, string Name, string Type, Guid? ParentId = null)
    : ICommand<Guid>;

public sealed record SetOrgUnitActiveCommand(Guid Id, bool IsActive) : ICommand<Guid>;
