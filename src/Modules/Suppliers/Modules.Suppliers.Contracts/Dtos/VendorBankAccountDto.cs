namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>AccountNo/Swift are masked to last-4 unless the caller has Vendors.Update.</summary>
public sealed record VendorBankAccountDto(string Bank, string AccountNo, string Swift, string Currency, bool IsPrimary);
