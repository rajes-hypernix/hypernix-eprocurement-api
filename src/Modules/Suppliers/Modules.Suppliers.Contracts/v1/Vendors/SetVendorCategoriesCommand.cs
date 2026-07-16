using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Vendors;

public sealed record SetVendorCategoriesCommand(Guid VendorId, IReadOnlyList<string> Categories) : ICommand<Guid>;

public sealed record SetVendorCategoriesRequest(IReadOnlyList<string> Categories);
