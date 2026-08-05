namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>AccountNo/Swift are masked to last-4 unless the caller has Vendors.Update.</summary>
public sealed record VendorBankAccountDto(Guid BankId, string BankName, string AccountNo, string Swift, string CurrencyCode, bool IsPrimary);
