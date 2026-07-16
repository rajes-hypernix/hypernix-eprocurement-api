using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.GetVendorById;

public sealed class GetVendorByIdQueryHandler(SuppliersDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetVendorByIdQuery, VendorDto>
{
    public async ValueTask<VendorDto> Handle(GetVendorByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vendor = await dbContext.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == query.VendorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Vendor {query.VendorId} not found.");

        bool canViewBankDetails = currentUser.GetUserClaims()?.Any(c =>
            c.Type == ClaimConstants.Permission && c.Value == SuppliersPermissions.Vendors.Update) ?? false;

        return VendorDtoMapper.ToDto(vendor, canViewBankDetails);
    }
}
