using FSH.Modules.Platform.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Platform.Contracts.v1.Catalog;

/// <summary>
/// Nested geo + banks catalog for forms (e.g. vendor onboarding). No auth in the handler —
/// callers gate access (anonymous magic-link or permissioned endpoints).
/// </summary>
public sealed record GetGeoCatalogQuery(string? BankCountryCode = "MY") : IQuery<GeoCatalogDto>;
