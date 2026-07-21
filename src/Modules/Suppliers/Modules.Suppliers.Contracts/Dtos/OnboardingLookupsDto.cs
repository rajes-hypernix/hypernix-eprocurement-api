using FSH.Modules.Platform.Contracts.Dtos;

namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>Onboarding form lookups: SWEC taxonomy, Platform geo/banks, and selected form templates.</summary>
public sealed record OnboardingLookupsDto(
    IReadOnlyList<SwecCategoryDto> Swec,
    IReadOnlyList<CountryLookupDto> Countries,
    IReadOnlyList<BankDto> Banks,
    IReadOnlyList<FormTemplateDto> FormTemplates);
