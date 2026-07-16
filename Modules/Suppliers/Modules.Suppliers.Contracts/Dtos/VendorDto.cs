namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record VendorDto(
    Guid Id,
    string Code,
    string Name,
    string RegisteredName,
    string Type,
    string Status,
    string Country,
    decimal Rating,
    decimal CreditLimit,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
