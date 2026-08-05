using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.VendorUsers;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.VendorUsers.ListVendorUsers;

public sealed class ListVendorUsersQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<ListVendorUsersQuery, IReadOnlyList<VendorUserDto>>
{
    public async ValueTask<IReadOnlyList<VendorUserDto>> Handle(ListVendorUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await dbContext.VendorUsers
            .AsNoTracking()
            .Where(u => u.VendorId == query.VendorId)
            .OrderBy(u => u.Name)
            .Select(u => new VendorUserDto(u.Id, u.Code, u.VendorId, u.Name, u.Email, u.IsActive, u.CreatedOnUtc, u.LastModifiedOnUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
