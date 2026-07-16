namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>
/// SWEC taxonomy only for now — Country/State/City/Bank custom-list lookups are deferred until a
/// Platform/Configuration module exists (per the migration plan's module map, that's Platform work,
/// not Suppliers).
/// </summary>
public sealed record OnboardingLookupsDto(IReadOnlyList<SwecCategoryDto> Swec);
