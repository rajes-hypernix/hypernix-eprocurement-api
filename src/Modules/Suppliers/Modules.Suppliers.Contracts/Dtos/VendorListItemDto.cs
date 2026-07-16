namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record VendorListItemDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    IReadOnlyList<string> Categories,
    string Region,
    string State,
    decimal Rating,
    string Status);
