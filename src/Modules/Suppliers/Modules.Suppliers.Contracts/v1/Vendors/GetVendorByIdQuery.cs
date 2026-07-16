using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Vendors;

public sealed record GetVendorByIdQuery(Guid VendorId) : IQuery<VendorDto>;
