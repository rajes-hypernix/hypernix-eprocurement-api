namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record VendorUserDto(
    Guid Id,
    string Code,
    Guid VendorId,
    string Name,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? LastModifiedOnUtc);
