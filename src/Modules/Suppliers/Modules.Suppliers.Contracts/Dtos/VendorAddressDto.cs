namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record VendorAddressDto(
    string Type,
    string Line,
    string City,
    string State,
    string CountryCode,
    Guid? StateId,
    Guid? CityId,
    string Postcode,
    bool IsPrimary);
