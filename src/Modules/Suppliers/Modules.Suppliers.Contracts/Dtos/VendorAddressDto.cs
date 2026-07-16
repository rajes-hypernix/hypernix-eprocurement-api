namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record VendorAddressDto(
    string Type,
    string Line,
    string City,
    string State,
    string Country,
    string Postcode,
    bool IsPrimary);
