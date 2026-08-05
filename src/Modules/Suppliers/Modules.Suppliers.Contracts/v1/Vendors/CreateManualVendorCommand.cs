using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Vendors;

/// <summary>
/// Manual New-Vendor entry: goes straight to Registered, no approval workflow. A duplicate
/// match on RegistrationNo or exact Name never blocks creation — it only surfaces a warning
/// (see <see cref="CreateManualVendorResult.DuplicateWarning"/>).
/// </summary>
public sealed record CreateManualVendorCommand(
    string Name,
    string RegistrationNo,
    string Type,
    string? Region = null,
    string? State = null,
    string? City = null,
    string? CountryCode = null,
    Guid? StateId = null,
    Guid? CityId = null,
    string? CurrencyCode = null,
    string? PaymentTerms = null,
    Guid? BankId = null,
    string? BankName = null,
    string? AccountNo = null,
    string? Swift = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? AddressLine = null,
    string? RegisteredName = null,
    string? TaxId = null,
    IReadOnlyList<string>? Categories = null) : ICommand<CreateManualVendorResult>;

public sealed record CreateManualVendorResult(Guid VendorId, string Code, string? DuplicateWarning);
