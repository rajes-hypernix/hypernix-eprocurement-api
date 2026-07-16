namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record VendorContactDto(string Name, string Role, string Email, string Phone, bool IsPrimary);
