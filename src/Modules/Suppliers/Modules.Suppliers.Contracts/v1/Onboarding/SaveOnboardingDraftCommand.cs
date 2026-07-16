using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

/// <summary>Partial-patch draft save — only non-null fields are applied. Only valid while Invited/InProgress.</summary>
public sealed record SaveOnboardingDraftCommand(
    string Token,
    string? Name = null,
    string? RegisteredName = null,
    string? RegistrationNo = null,
    string? TaxId = null,
    string? Email = null,
    string? ContactName = null,
    string? ContactPhone = null,
    string? Region = null,
    string? State = null,
    string? City = null,
    string? Country = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<VendorContactDto>? Contacts = null,
    IReadOnlyList<VendorAddressDto>? Addresses = null,
    IReadOnlyList<VendorBankAccountDto>? BankAccounts = null,
    IReadOnlyList<VendorCertificationDto>? Certifications = null,
    IReadOnlyList<OnboardingFinancialYearDto>? FinancialYears = null) : ICommand<OnboardingDraftDto>;
