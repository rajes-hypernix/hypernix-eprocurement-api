namespace eProcure.Application.Suppliers;

public sealed record VendorListItem(
    Guid Id,
    string Code,
    string Name,
    string Type,                 // "SWEC" | "Non-SWEC"
    IReadOnlyList<string> Categories,
    string Region,
    string State,
    decimal Rating,
    int? Otd,                    // derived (Slice H T7); null = not yet available
    string Status);

public sealed record ContactDto(string Name, string Role, string Email, string Phone, bool IsPrimary);
public sealed record AddressDto(string Type, string Line, string City, string State, string Country, string Postcode, bool IsPrimary);
public sealed record BankAccountDto(string Bank, string AccountNo, string Swift, string Currency, bool IsPrimary);
public sealed record CertificationDto(string Name, string Number, string ValidTo, string Status);
public sealed record CurrencyDto(string Code, bool IsPrimary);
// Derived metrics (Slice H T7). Nullable = "not yet available" (never a stale/fabricated number):
// Otd/Breaches have no computable source this slice; Quality/Lead/Response/WinRate are null with no
// underlying facts. Lead is actual DAYS (PO issued -> goods received), not the legacy weeks int.
public sealed record PerformanceDto(int? Otd, int? Quality, int? Breaches, int? Lead, int? Response, int? WinRate, decimal SpendYtd, int Pos);

public sealed record VendorDetail(
    Guid Id,
    string Code,
    string Name,
    string RegisteredName,
    string RegistrationNo,
    string TaxId,
    string Type,
    string? LlrcTier,
    string Status,
    string Region,
    string State,
    string City,
    string Country,
    decimal Rating,
    string PaymentTerms,
    decimal CreditLimit,
    IReadOnlyList<string> Categories,
    PerformanceDto Performance,
    IReadOnlyList<ContactDto> Contacts,
    IReadOnlyList<AddressDto> Addresses,
    IReadOnlyList<BankAccountDto> BankAccounts,
    IReadOnlyList<CertificationDto> Certifications,
    IReadOnlyList<CurrencyDto> Currencies,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record VendorFilter(string? Query, string? Type, string? Region);

public sealed record CreateVendorRequest(string Name, string? Region, string? State, string? City);

/// <summary>Manual New-Vendor entry (VENDOR-ONBOARDING-SPEC §1): keyed straight into the master with
/// NO onboarding approval. Conformed-dimension fields (country/state/currency/paymentTerms/bank/type)
/// carry seeded lookup CODES; free-form fields stay text (DATA-MODEL-ANALYTICS §4).</summary>
public sealed record CreateManualVendorRequest(
    string Name, string RegistrationNo, string Type, string? Region, string? State, string? City,
    string? Country = null, string? Currency = null, string? PaymentTerms = null,
    string? Bank = null, string? AccountNo = null, string? Swift = null,
    string? ContactName = null, string? ContactEmail = null, string? AddressLine = null,
    string? RegisteredName = null, string? TaxId = null, IReadOnlyList<string>? Categories = null);

/// <summary>Result of a manual create: the vendor plus any duplicate warning surfaced (C2).</summary>
public sealed record ManualVendorResult(VendorDetail Vendor, string? DuplicateWarning);
public sealed record UpdateVendorRequest(
    string Name,
    string RegisteredName,
    string RegistrationNo,
    string TaxId,
    string Type,
    string? LlrcTier,
    string Region,
    string State,
    string City,
    string PaymentTerms,
    decimal CreditLimit);
public sealed record SetCategoriesRequest(IReadOnlyList<string> Categories);

public interface IVendorService
{
    Task<IReadOnlyList<VendorListItem>> ListAsync(VendorFilter filter, CancellationToken ct = default);
    Task<VendorDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<VendorDetail> CreateAsync(CreateVendorRequest req, CancellationToken ct = default);

    /// <summary>Manual New-Vendor entry — straight to the master as Registered, no approval (C1); surfaces
    /// a duplicate warning if the reg. no. / name already exists (C2).</summary>
    Task<ManualVendorResult> CreateManualAsync(CreateManualVendorRequest req, CancellationToken ct = default);
    Task<VendorDetail> UpdateAsync(Guid id, UpdateVendorRequest req, CancellationToken ct = default);
    Task<VendorDetail> SetCategoriesAsync(Guid id, SetCategoriesRequest req, CancellationToken ct = default);
    Task<VendorDetail> ToggleStatusAsync(Guid id, CancellationToken ct = default);
}
