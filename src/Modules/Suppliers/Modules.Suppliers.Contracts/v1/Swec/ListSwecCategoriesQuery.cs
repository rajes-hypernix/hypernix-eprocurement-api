using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Swec;

/// <summary>Flat SWEC taxonomy list; the client builds the tree via ParentCode.</summary>
public sealed record ListSwecCategoriesQuery : IQuery<IReadOnlyList<SwecCategoryDto>>;
