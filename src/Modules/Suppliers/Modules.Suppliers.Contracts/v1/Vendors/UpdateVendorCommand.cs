using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Vendors;

public sealed record UpdateVendorCommand(
    Guid VendorId,
    string Name,
    string RegisteredName,
    string RegistrationNo,
    string TaxId,
    string Type,
    string? LlrcTier,
    string Region,
    string State,
    string City,
    string CountryCode,
    Guid? StateId,
    Guid? CityId,
    string PaymentTerms,
    decimal CreditLimit,
    decimal Rating) : ICommand<Guid>;
