using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Vendors;

/// <summary>Buyer/Admin master-data governance toggle between Registered and Inactive.</summary>
public sealed record ToggleVendorStatusCommand(Guid VendorId) : ICommand<Guid>;
