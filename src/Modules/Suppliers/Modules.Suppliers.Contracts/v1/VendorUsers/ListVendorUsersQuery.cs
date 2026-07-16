using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.VendorUsers;

public sealed record ListVendorUsersQuery(Guid VendorId) : IQuery<IReadOnlyList<VendorUserDto>>;
