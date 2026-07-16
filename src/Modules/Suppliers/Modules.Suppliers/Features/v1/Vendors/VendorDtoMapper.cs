using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Domain;

namespace FSH.Modules.Suppliers.Features.v1.Vendors;

/// <summary>
/// Shared Vendor -&gt; VendorDto mapping. Bank details (AccountNo/Swift) are masked to the
/// last 4 characters unless the caller has Vendors.Update (mirrors the old Buyer/Admin-only
/// unmasking rule) — full mask if the value is 4 characters or shorter.
/// </summary>
internal static class VendorDtoMapper
{
    internal static VendorDto ToDto(Vendor vendor, bool canViewBankDetails)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        return new VendorDto(
            vendor.Id,
            vendor.Code,
            vendor.Name,
            vendor.RegisteredName,
            vendor.RegistrationNo,
            vendor.TaxId,
            vendor.Type,
            vendor.LlrcTier,
            vendor.Status,
            vendor.Region,
            vendor.State,
            vendor.City,
            vendor.Country,
            vendor.Rating,
            vendor.PaymentTerms,
            vendor.CreditLimit,
            vendor.Categories,
            [.. vendor.Contacts.Select(c => new VendorContactDto(c.Name, c.Role, c.Email, c.Phone, c.IsPrimary))],
            [.. vendor.Addresses.Select(a => new VendorAddressDto(a.Type, a.Line, a.City, a.State, a.Country, a.Postcode, a.IsPrimary))],
            [.. vendor.BankAccounts.Select(b => new VendorBankAccountDto(
                b.Bank,
                canViewBankDetails ? b.AccountNo : MaskTail(b.AccountNo),
                canViewBankDetails ? b.Swift : MaskTail(b.Swift),
                b.Currency,
                b.IsPrimary))],
            [.. vendor.Certifications.Select(c => new VendorCertificationDto(c.Name, c.Number, c.ValidTo, c.Status))],
            [.. vendor.Currencies.Select(c => new VendorCurrencyDto(c.Code, c.IsPrimary))],
            vendor.CreatedUtc,
            vendor.UpdatedUtc);
    }

    private static string MaskTail(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= 4
            ? new string('•', value.Length)
            : new string('•', value.Length - 4) + value[^4..];
    }
}
